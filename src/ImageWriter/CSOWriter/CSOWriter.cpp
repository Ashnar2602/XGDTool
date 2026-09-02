#include <cstring>
#include <algorithm>

#include "AvlTree/AvlIterator.h"
#include "ImageWriter/CSOWriter/CSOWriter.h"

CSOWriter::CSOWriter(std::shared_ptr<ImageReader> image_reader, const ScrubType scrub_type, int compression_level) 
    :   image_reader_(image_reader),
        scrub_type_(scrub_type)
{
    if (compression_level == 1) lz4f_prefs_.compressionLevel = 1;
    else if (compression_level == 2) lz4f_prefs_.compressionLevel = 6;
    else if (compression_level == 3) lz4f_prefs_.compressionLevel = 12;
    else if (compression_level > 3) lz4f_prefs_.compressionLevel = std::min(compression_level, 12);
    else lz4f_prefs_.compressionLevel = 0;

    init_cso_writer();
}

CSOWriter::CSOWriter(const std::filesystem::path& in_dir_path, int compression_level)
    :   in_dir_path_(in_dir_path)
{
    if (compression_level == 1) lz4f_prefs_.compressionLevel = 1;
    else if (compression_level == 2) lz4f_prefs_.compressionLevel = 6;
    else if (compression_level == 3) lz4f_prefs_.compressionLevel = 12;
    else if (compression_level > 3) lz4f_prefs_.compressionLevel = std::min(compression_level, 12);
    else lz4f_prefs_.compressionLevel = 0;

    init_cso_writer();
}

CSOWriter::~CSOWriter() 
{
    {
        std::lock_guard<std::mutex> lock(batch_mutex_);
        stop_flag_ = true;
    }

    cv_start_.notify_all();

    for (std::thread& thread : thread_pool_) 
    {
        if (thread.joinable()) 
        {
            thread.join();
        }
    }

    for (auto& ctx : lz4f_ctx_pool_) 
    {
        LZ4F_freeCompressionContext(ctx);
    }
}

void CSOWriter::init_cso_writer() 
{
    uint32_t num_threads = std::max(1u, std::min(std::thread::hardware_concurrency(), 32u));

    for (size_t i = 0; i < num_threads; ++i)
    {
        lz4f_ctx_pool_.emplace_back(LZ4F_compressionContext_t());

        LZ4F_errorCode_t lz4f_error = LZ4F_createCompressionContext(&lz4f_ctx_pool_.back(), LZ4F_VERSION);
        if (LZ4F_isError(lz4f_error)) 
        {
            throw XGDException(ErrCode::MISC, HERE(), LZ4F_getErrorName(lz4f_error));
        }
    }

    lz4f_max_size_ = LZ4F_compressBound(Xiso::SECTOR_SIZE, &lz4f_prefs_);

    for (uint32_t i = 0; i < num_threads; ++i) 
    {
        // Pass i so each thread has a unique index and compression context
        thread_pool_.emplace_back(&CSOWriter::thread_worker, this, i);
    }
}

std::vector<std::filesystem::path> CSOWriter::convert(const std::filesystem::path& out_cso_path) 
{
    out_filepath_base_ = out_cso_path;

    create_directory(out_filepath_base_.parent_path());
    
    out_filepath_1_ = out_filepath_base_;
    out_filepath_2_ = out_filepath_base_;
    out_filepath_1_.replace_extension(".1.cso");
    out_filepath_2_.replace_extension(".2.cso");

    if (image_reader_ && scrub_type_ == ScrubType::FULL) 
    {
        AvlTree avl_tree(image_reader_->name(), image_reader_->directory_entries());
        convert_to_cso_from_avl(avl_tree);
    }
    else if (!in_dir_path_.empty())
    {
        AvlTree avl_tree(in_dir_path_.filename().string(), in_dir_path_);
        convert_to_cso_from_avl(avl_tree);
    }
    else if (!image_reader_)
    {
        throw XGDException(ErrCode::MISC, HERE(), "No input data to convert to CSO");
    }
    else
    {
        convert_to_cso(scrub_type_ == ScrubType::PARTIAL);
    }

    return out_paths();
}

void CSOWriter::convert_to_cso(const bool scrub) 
{
    ImageReader& image_reader = *image_reader_;
    uint32_t sector_offset = static_cast<uint32_t>(image_reader.image_offset() / Xiso::SECTOR_SIZE);
    uint32_t end_sector = image_reader.total_sectors();
    const std::unordered_set<uint32_t>* data_sectors;

    if (scrub) 
    {
        data_sectors = &image_reader.data_sectors();
        end_sector = std::min(image_reader.max_data_sector() + 1, end_sector);
    }

    uint32_t sectors_to_write = end_sector - sector_offset;

    prog_total_ = sectors_to_write - 1;
    prog_processed_ = 0;

    std::ofstream out_file(out_filepath_1_, std::ios::binary);
    if (!out_file.is_open()) 
    {
        throw XGDException(ErrCode::FILE_OPEN, HERE(), out_filepath_1_.string());
    }

    write_cso_header(out_file, sectors_to_write);
    write_dummy_index(out_file, sectors_to_write);

    uint32_t current_sector = sector_offset;

    std::vector<uint32_t> block_index;
    block_index.reserve((end_sector - sector_offset) + 1);

    constexpr uint32_t BATCH_SECTORS = 1024; // 2MB batch
    std::vector<char> read_buffer(BATCH_SECTORS * Xiso::SECTOR_SIZE);

    XGDLog() << "Writing CSO file" << XGDLog::Endl;

    while (current_sector < end_sector) 
    {
        uint32_t read_sectors = std::min(end_sector - current_sector, BATCH_SECTORS);

        image_reader.read_sectors(current_sector, read_sectors, read_buffer.data());

        if (scrub && image_reader.platform() == Platform::OGX) 
        {
            for (uint32_t i = 0; i < read_sectors; ++i)
            {
                if (data_sectors->find(current_sector + i) == data_sectors->end())
                {
                    std::memset(read_buffer.data() + (static_cast<size_t>(i) * Xiso::SECTOR_SIZE), 0x00, Xiso::SECTOR_SIZE);
                }
            }
        }

        current_sector += read_sectors;

        compress_and_write_sectors_managed(out_file, block_index, read_sectors, read_buffer.data());
        
        XGDLog().print_progress(prog_processed_ += read_sectors, prog_total_);

        check_status_flags();
    }

    finalize_out_files(out_file, block_index);
    out_file.close();
}

void CSOWriter::convert_to_cso_from_avl(AvlTree& avl_tree) 
{
    uint32_t out_iso_sectors = num_sectors(avl_tree.out_iso_size());

    prog_total_ = avl_tree.total_bytes();
    prog_processed_ = 0;

    XGDLog() << "Writing CSO file" << XGDLog::Endl;

    std::ofstream out_file(out_filepath_1_, std::ios::binary);
    if (!out_file.is_open()) 
    {
        throw XGDException(ErrCode::FILE_OPEN, HERE(), out_filepath_1_.string());
    }

    write_cso_header(out_file, out_iso_sectors);
    write_dummy_index(out_file, out_iso_sectors);

    std::vector<uint32_t> block_index;
    block_index.reserve(out_iso_sectors + 1);
    
    write_iso_header(out_file, block_index, avl_tree);

    AvlIterator avl_iterator(avl_tree);
    const std::vector<AvlIterator::Entry>& avl_entries = avl_iterator.entries();

    uint32_t pad_sectors = static_cast<uint32_t>((avl_entries.front().offset - sizeof(Xiso::Header)) / Xiso::SECTOR_SIZE);
    write_padding_sectors(out_file, block_index, pad_sectors, 0x00);

    for (size_t i = 0; i < avl_entries.size(); i++) 
    {
        if (avl_entries[i].offset > block_index.size() * Xiso::SECTOR_SIZE) 
        {
            uint32_t pad_sectors = num_sectors(avl_entries[i].offset) - static_cast<uint32_t>(block_index.size());
            write_padding_sectors(out_file, block_index, pad_sectors, Xiso::PAD_BYTE);
        } 
        
        if (num_sectors(avl_entries[i].offset) != static_cast<uint32_t>(block_index.size()) || (avl_entries[i].offset % Xiso::SECTOR_SIZE)) 
        {
            throw XGDException(ErrCode::MISC, HERE(), "CSO file has become misaligned");
        }

        if (avl_entries[i].directory_entry) 
        {
            std::vector<char> entry_buffer;
            size_t processed_entries = write_directory_to_buffer(avl_entries, i, entry_buffer);
            i += processed_entries - 1;

            compress_and_write_sectors_managed(out_file, block_index, num_sectors(entry_buffer.size()), entry_buffer.data());
        } 
        else 
        {
            if (image_reader_) 
            {
                write_file_from_reader(out_file, block_index, *avl_entries[i].node);
            } 
            else 
            {
                write_file_from_directory(out_file, block_index, *avl_entries[i].node);
            }
        }
    }

    if (block_index.size() < out_iso_sectors) 
    {
        uint32_t pad_sectors = out_iso_sectors - static_cast<uint32_t>(block_index.size());
        write_padding_sectors(out_file, block_index, pad_sectors, 0x00);
    }

    finalize_out_files(out_file, block_index);

    out_file.close();
}

void CSOWriter::write_file_from_reader(std::ofstream& out_file, std::vector<uint32_t>& block_index, AvlTree::Node& node) 
{
    ImageReader& image_reader = *image_reader_;
    uint64_t bytes_remaining = node.file_size;
    uint64_t read_position = image_reader.image_offset() + (node.old_start_sector * Xiso::SECTOR_SIZE);
    constexpr size_t BATCH_BYTES = 1024 * Xiso::SECTOR_SIZE; // 2MB
    std::vector<char> read_buffer(BATCH_BYTES);

    while (bytes_remaining > 0) 
    {
        uint64_t read_size = std::min(bytes_remaining, read_buffer.size());

        image_reader.read_bytes(read_position, read_size, read_buffer.data());

        if (read_size % Xiso::SECTOR_SIZE) 
        {
            std::memset(read_buffer.data() + read_size, Xiso::PAD_BYTE, Xiso::SECTOR_SIZE - (read_size % Xiso::SECTOR_SIZE));
        }

        compress_and_write_sectors_managed(out_file, block_index, num_sectors(read_size), read_buffer.data());

        bytes_remaining -= read_size;
        read_position += read_size;

        XGDLog().print_progress(prog_processed_ += read_size, prog_total_);

        check_status_flags();
    }
}

void CSOWriter::write_file_from_directory(std::ofstream& out_file, std::vector<uint32_t>& block_index, AvlTree::Node& node) 
{
    std::ifstream in_file(node.path, std::ios::binary);
    if (!in_file.is_open()) 
    {
        throw std::runtime_error("Failed to open input file: " + node.path.string());
    }

    uint64_t bytes_remaining = node.file_size;
    constexpr size_t BATCH_BYTES = 1024 * Xiso::SECTOR_SIZE; // 2MB
    std::vector<char> read_buffer(BATCH_BYTES);

    while (bytes_remaining > 0) 
    {
        uint64_t read_size = std::min(bytes_remaining, read_buffer.size());

        in_file.read(read_buffer.data(), read_size);
        if (in_file.fail()) 
        {
            throw std::runtime_error("Failed to read from input file: " + node.path.string());
        }

        if (read_size % Xiso::SECTOR_SIZE) 
        {
            std::memset(read_buffer.data() + read_size, Xiso::PAD_BYTE, Xiso::SECTOR_SIZE - (read_size % Xiso::SECTOR_SIZE));
        }

        compress_and_write_sectors_managed(out_file, block_index, num_sectors(read_size), read_buffer.data());

        bytes_remaining -= read_size;

        XGDLog().print_progress(prog_processed_ += read_size, prog_total_);

        check_status_flags();
    }

    in_file.close();
}

void CSOWriter::thread_worker(size_t thread_idx) 
{
    while (true) 
    {
        {
            std::unique_lock<std::mutex> lock(batch_mutex_);
            cv_start_.wait(lock, [this] { return stop_flag_ || batch_ctx_.has_work; });

            if (stop_flag_ && !batch_ctx_.has_work) 
            {
                return;
            }
        }

        constexpr uint32_t STEAL_CHUNK = 8;
        while (true) 
        {
            uint32_t sec_start = batch_ctx_.next_sector.fetch_add(STEAL_CHUNK, std::memory_order_relaxed);
            if (sec_start >= batch_ctx_.num_sectors) break;

            uint32_t sec_end = std::min(sec_start + STEAL_CHUNK, batch_ctx_.num_sectors);
            for (uint32_t sec = sec_start; sec < sec_end; ++sec) 
            {
                const char* in_ptr = batch_ctx_.in_buffer + (static_cast<size_t>(sec) * Xiso::SECTOR_SIZE);
                char* out_ptr = batch_ctx_.out_buffer + (static_cast<size_t>(sec) * lz4f_max_size_);

                LZ4F_compressBegin(lz4f_ctx_pool_[thread_idx], out_ptr, Xiso::SECTOR_SIZE, &lz4f_prefs_);
                size_t compressed_size = LZ4F_compressUpdate(lz4f_ctx_pool_[thread_idx], out_ptr, lz4f_max_size_, in_ptr, Xiso::SECTOR_SIZE, nullptr);

                CompressedTaskResult& result = batch_ctx_.results[sec];
                result.sector_idx = sec;
                result.compressed_size = compressed_size;
                result.compressed = !((compressed_size == 0) || ((compressed_size + 12) >= Xiso::SECTOR_SIZE));
                result.buffer_to_write = result.compressed ? out_ptr : in_ptr;
            }
        }

        if (batch_ctx_.active_workers.fetch_sub(1, std::memory_order_acq_rel) == 1) 
        {
            std::lock_guard<std::mutex> lock(batch_mutex_);
            batch_ctx_.has_work = false;
            cv_done_.notify_one();
        }
    }
}

void CSOWriter::compress_and_write_sectors_managed(std::ofstream& out_file, std::vector<uint32_t>& block_index, const uint32_t num_sectors, const char* in_buffer)
{
    if (num_sectors == 0) return;

    if (batch_compress_buffer_.size() < static_cast<size_t>(num_sectors) * lz4f_max_size_) 
    {
        batch_compress_buffer_.resize(static_cast<size_t>(num_sectors) * lz4f_max_size_);
    }
    if (batch_results_.size() < num_sectors) 
    {
        batch_results_.resize(num_sectors);
    }

    uint32_t num_workers = static_cast<uint32_t>(thread_pool_.size());

    {
        std::lock_guard<std::mutex> lock(batch_mutex_);
        batch_ctx_.in_buffer = in_buffer;
        batch_ctx_.out_buffer = batch_compress_buffer_.data();
        batch_ctx_.results = batch_results_.data();
        batch_ctx_.num_sectors = num_sectors;
        batch_ctx_.next_sector.store(0, std::memory_order_relaxed);
        batch_ctx_.active_workers.store(num_workers, std::memory_order_relaxed);
        batch_ctx_.has_work = true;
    }

    cv_start_.notify_all();

    {
        std::unique_lock<std::mutex> lock(batch_mutex_);
        cv_done_.wait(lock, [this] { return !batch_ctx_.has_work; });
    }

    for (uint32_t i = 0; i < num_sectors; ++i)
    {
        write_sector(out_file, block_index, batch_results_[i]);
    }
}

void CSOWriter::write_sector(std::ofstream& out_file, std::vector<uint32_t>& block_index, const CompressedTaskResult& result) 
{
    if (static_cast<uint64_t>(out_file.tellp()) > CSO::SPLIT_OFFSET) 
    {
        out_file.close();
        out_file = std::ofstream(out_filepath_2_, std::ios::binary);
        if (!out_file.is_open()) 
        {
            throw XGDException(ErrCode::FILE_OPEN, HERE(), out_filepath_2_.string());
        }
    }

    if (out_file.tellp() & ALIGN_M) 
    {
        auto padding = ALIGN_B - (out_file.tellp() & ALIGN_M);
        std::vector<char> alignment_buffer(padding, 0);
        out_file.write(alignment_buffer.data(), padding);
    }

    uint32_t block_info = static_cast<uint32_t>(out_file.tellp() >> CSO::INDEX_ALIGNMENT);

    if (!result.compressed) 
    {
        out_file.write(result.buffer_to_write, Xiso::SECTOR_SIZE);
    } 
    else 
    {
        block_info |= 0x80000000;
        out_file.write(result.buffer_to_write, result.compressed_size);
    }

    if (out_file.fail())
    {
        throw std::runtime_error("Failed to write to output file");
    }

    block_index.push_back(block_info);
}

void CSOWriter::write_cso_header(std::ofstream& out_file, const uint32_t total_sectors)
{
    CSO::Header cso_header(static_cast<uint64_t>(total_sectors) * Xiso::SECTOR_SIZE);

    out_file.write(reinterpret_cast<const char*>(&cso_header), sizeof(CSO::Header));
    if (out_file.fail()) 
    {
        throw XGDException(ErrCode::FILE_WRITE, HERE(), out_filepath_1_.string());
    }
}

void CSOWriter::write_dummy_index(std::ofstream& out_file, const uint32_t total_sectors)
{
    std::vector<uint32_t> buffer(total_sectors + 1, 0);

    out_file.write(reinterpret_cast<char*>(buffer.data()), buffer.size() * sizeof(uint32_t));
    if (out_file.fail()) 
    {
        throw XGDException(ErrCode::FILE_WRITE, HERE(), out_filepath_1_.string());
    }
}

void CSOWriter::write_iso_header(std::ofstream& out_file, std::vector<uint32_t>& block_index, AvlTree& avl_tree)
{
    Xiso::Header iso_header(static_cast<uint32_t>(avl_tree.root()->start_sector), 
                            static_cast<uint32_t>(avl_tree.root()->file_size), 
                            static_cast<uint32_t>(avl_tree.out_iso_size() / Xiso::SECTOR_SIZE), 
                            image_reader_ ? image_reader_->file_time() : Xiso::FileTime()); 

    compress_and_write_sectors_managed(out_file, block_index, num_sectors(sizeof(Xiso::Header)), reinterpret_cast<const char*>(&iso_header));
}

void CSOWriter::write_padding_sectors(std::ofstream& out_file, std::vector<uint32_t>& block_index, const uint32_t num_sectors, const char pad_byte)
{
    std::vector<char> padding(Xiso::SECTOR_SIZE * num_sectors, pad_byte);
    compress_and_write_sectors_managed(out_file, block_index, num_sectors, padding.data());
}

void CSOWriter::finalize_out_files(std::ofstream& out_file, std::vector<uint32_t>& block_index) 
{
    out_file.seekp(0, std::ios::end);

    block_index.push_back(static_cast<uint32_t>(out_file.tellp() >> CSO::INDEX_ALIGNMENT));
    pad_to_modulus(out_file, CSO::FILE_MODULUS, 0x00);

    if (std::filesystem::exists(out_filepath_2_)) 
    {
        out_file.close();
        out_file = std::ofstream(out_filepath_1_, std::ios::binary | std::ios::in | std::ios::out);
        if (!out_file.is_open()) 
        {
            throw XGDException(ErrCode::FILE_OPEN, HERE(), out_filepath_1_.string());
        }
    }

    out_file.seekp(sizeof(CSO::Header), std::ios::beg);
    out_file.write(reinterpret_cast<const char*>(block_index.data()), block_index.size() * sizeof(uint32_t));
    if (out_file.fail()) 
    {
        throw XGDException(ErrCode::FILE_WRITE, HERE(), out_filepath_1_.string());
    }

    out_file.seekp(0, std::ios::end);
    pad_to_modulus(out_file, CSO::FILE_MODULUS, 0x00);

    block_index.clear();
}

void CSOWriter::pad_to_modulus(std::ofstream& out_file, const uint64_t modulus, const char pad_byte) 
{
    if ((out_file.tellp() % modulus) == 0) 
    {
        return;
    }

    uint64_t padding_len = modulus - (out_file.tellp() % modulus);
    std::vector<char> buffer(padding_len, pad_byte);

    out_file.write(buffer.data(), padding_len); 
    if (out_file.fail()) 
    {
        throw XGDException(ErrCode::FILE_WRITE, HERE(), "Failed to write padding bytes");
    }
}

std::vector<std::filesystem::path> CSOWriter::out_paths() 
{
    if (std::filesystem::exists(out_filepath_2_)) 
    {
        return { out_filepath_1_, out_filepath_2_ };
    }

    try 
    {
        std::filesystem::rename(out_filepath_1_, out_filepath_base_);
        return { out_filepath_base_ };
    } 
    catch (const std::filesystem::filesystem_error& e) 
    {
        throw XGDException(ErrCode::FS_RENAME, HERE(), e.what());
    }

    return { out_filepath_1_ };
}