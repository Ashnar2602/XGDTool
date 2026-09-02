#include <functional>
#include <cstring>
#include <algorithm>

#include <lz4hc.h>

#include "ImageWriter/CCIWriter/CCIWriter.h"
#include "AvlTree/AvlIterator.h"

CCIWriter::CCIWriter(std::shared_ptr<ImageReader> image_reader, const ScrubType scrub_type, int compression_level)
    :   image_reader_(image_reader), 
        scrub_type_(scrub_type)
{
    if (compression_level == 1) compression_level_ = 3;
    else if (compression_level == 2) compression_level_ = 9;
    else if (compression_level == 3) compression_level_ = 12;
    else if (compression_level > 3) compression_level_ = std::min(compression_level, 12);
    else compression_level_ = 12;
    init_cci_writer();
}

CCIWriter::CCIWriter(const std::filesystem::path& in_dir_path, int compression_level)
    : in_dir_path_(in_dir_path)
{
    if (compression_level == 1) compression_level_ = 3;
    else if (compression_level == 2) compression_level_ = 9;
    else if (compression_level == 3) compression_level_ = 12;
    else if (compression_level > 3) compression_level_ = std::min(compression_level, 12);
    else compression_level_ = 12;
    init_cci_writer();
}

CCIWriter::~CCIWriter()
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
}

void CCIWriter::init_cci_writer()
{
    uint32_t num_threads = std::max(1u, std::min(std::thread::hardware_concurrency(), 32u));
    for (uint32_t i = 0; i < num_threads; ++i)
    {
        thread_pool_.emplace_back(&CCIWriter::thread_worker, this);
    }
}

std::vector<std::filesystem::path> CCIWriter::convert(const std::filesystem::path& out_cci_path) 
{
    out_filepath_base_ = out_cci_path;

    create_directory(out_cci_path.parent_path());

    out_filepath_1_ = out_cci_path;
    out_filepath_2_ = out_cci_path;
    out_filepath_1_.replace_extension(".1.cci");
    out_filepath_2_.replace_extension(".2.cci");

    if (image_reader_ && scrub_type_ == ScrubType::FULL) 
    {
        AvlTree avl_tree(image_reader_->name(), image_reader_->directory_entries());
        convert_to_cci_from_avl(avl_tree);
    }
    else if (!in_dir_path_.empty())
    {
        AvlTree avl_tree(in_dir_path_.filename().string(), in_dir_path_);
        convert_to_cci_from_avl(avl_tree);
    }
    else if (!image_reader_) 
    {
        throw XGDException(ErrCode::ISO_INVALID, HERE(), "No input data");
    }
    else
    {
        convert_to_cci(scrub_type_ == ScrubType::PARTIAL);
    }

    return out_paths();
}

void CCIWriter::convert_to_cci(const bool scrub)
{
    ImageReader& image_reader = *image_reader_;
    uint32_t end_sector = image_reader.total_sectors();
    uint32_t sector_offset = static_cast<uint32_t>(image_reader.image_offset() / Xiso::SECTOR_SIZE);
    const std::unordered_set<uint32_t>* data_sectors;

    if (scrub) 
    {
        data_sectors = &image_reader.data_sectors();
        end_sector = std::min(end_sector, image_reader.max_data_sector() + 1);
    }

    prog_total_ = end_sector - sector_offset - 1;
    prog_processed_ = 0;

    std::ofstream out_file(out_filepath_1_, std::ios::binary);
    if (!out_file.is_open()) 
    {
        throw std::runtime_error("Failed to open output file: " + out_filepath_1_.string());
    }

    XGDLog() << "Writing CCI file" << XGDLog::Endl;

    uint32_t current_sector = sector_offset;
    constexpr uint32_t BATCH_SECTORS = 1024; // 2MB batch
    std::vector<char> read_buffer(BATCH_SECTORS * Xiso::SECTOR_SIZE);
    
    std::vector<CCI::IndexInfo> index_infos;
    index_infos.reserve((end_sector - sector_offset) + 1);

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

        compress_and_write_sectors_managed(out_file, index_infos, read_sectors, read_buffer.data());

        XGDLog().print_progress(prog_processed_ += read_sectors, prog_total_);

        check_status_flags();
    }

    finalize_out_file(out_file, index_infos);
    out_file.close();
}

void CCIWriter::convert_to_cci_from_avl(AvlTree& avl_tree) 
{
    prog_total_ = avl_tree.total_bytes();
    prog_processed_ = 0;

    AvlIterator avl_iterator(avl_tree);
    std::vector<AvlIterator::Entry> avl_entries = avl_iterator.entries();

    std::ofstream out_file(out_filepath_1_, std::ios::binary);
    if (!out_file.is_open()) 
    {
        throw std::runtime_error("Failed to open output file: " + out_filepath_1_.string());
    }

    XGDLog() << "Writing CCI file" << XGDLog::Endl;

    uint32_t sectors_to_write = num_sectors(avl_tree.out_iso_size());
    std::vector<CCI::IndexInfo> index_infos;
    index_infos.reserve(sectors_to_write + 1);

    write_iso_header(out_file, index_infos, avl_tree);

    uint32_t pad_sectors = num_sectors(avl_entries.front().offset - sizeof(Xiso::Header));
    write_padding_sectors(out_file, index_infos, pad_sectors, 0x00);

    uint32_t sectors_written = num_sectors(avl_entries.front().offset);

    for (size_t i = 0; i < avl_entries.size(); i++) 
    {
        if (num_sectors(avl_entries[i].offset) > sectors_written)
        {
            uint32_t pad_sectors = num_sectors(avl_entries[i].offset) - sectors_written;

            XGDLog(Debug) << "Padding " << pad_sectors << " sectors\n";

            write_padding_sectors(out_file, index_infos, pad_sectors, Xiso::PAD_BYTE);
            sectors_written += pad_sectors;
        } 

        if (num_sectors(avl_entries[i].offset) != sectors_written || avl_entries[i].offset % Xiso::SECTOR_SIZE) 
        {
            throw XGDException(ErrCode::MISC, HERE(), "CCI file has become misaligned");
        }

        if (avl_entries[i].directory_entry) 
        {
            std::vector<char> dir_buffer;
            size_t entries_processed = write_directory_to_buffer(avl_entries, i, dir_buffer);
            i += entries_processed - 1;

            uint32_t write_sectors = num_sectors(dir_buffer.size());
            compress_and_write_sectors_managed(out_file, index_infos, write_sectors, dir_buffer.data());
            sectors_written += write_sectors;
        } 
        else 
        {
            if (image_reader_) 
            {
                write_file_from_reader(out_file, index_infos, *avl_entries[i].node);
            } 
            else 
            {
                write_file_from_dir(out_file, index_infos, *avl_entries[i].node);
            }

            sectors_written += num_sectors(avl_entries[i].node->file_size);
        }
    }

    pad_sectors = sectors_to_write - sectors_written;

    if (pad_sectors > 0) 
    {
        write_padding_sectors(out_file, index_infos, pad_sectors, 0x00);
    }

    finalize_out_file(out_file, index_infos);
    out_file.close();
}

void CCIWriter::write_iso_header(std::ofstream& out_file, std::vector<CCI::IndexInfo>& index_infos, AvlTree& avl_tree)
{
    Xiso::Header xiso_header(   static_cast<uint32_t>(avl_tree.root()->start_sector), 
                                static_cast<uint32_t>(avl_tree.root()->file_size), 
                                static_cast<uint32_t>(avl_tree.out_iso_size() / Xiso::SECTOR_SIZE),
                                image_reader_ ? image_reader_->file_time() : Xiso::FileTime());

    static_assert(!(sizeof(Xiso::Header) % Xiso::SECTOR_SIZE), "Xiso::Header size must be a multiple of Xiso::SECTOR_SIZE");

    compress_and_write_sectors_managed(out_file, index_infos, num_sectors(sizeof(Xiso::Header)), reinterpret_cast<char*>(&xiso_header));
}

void CCIWriter::write_padding_sectors(std::ofstream& out_file, std::vector<CCI::IndexInfo>& index_infos, const uint32_t num_sectors, const char pad_byte)
{
    std::vector<char> pad_sector(Xiso::SECTOR_SIZE * num_sectors, pad_byte);
    compress_and_write_sectors_managed(out_file, index_infos, num_sectors, pad_sector.data());
}

void CCIWriter::write_file_from_reader(std::ofstream& out_file, std::vector<CCI::IndexInfo>& index_infos, AvlTree::Node& node) 
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

        if (read_size % Xiso::SECTOR_SIZE) // Pad buffer to sector boundary with 0xFF
        {
            std::memset(read_buffer.data() + read_size, Xiso::PAD_BYTE, (Xiso::SECTOR_SIZE - (read_size % Xiso::SECTOR_SIZE)));
        }

        compress_and_write_sectors_managed(out_file, index_infos, num_sectors(read_size), read_buffer.data());

        bytes_remaining -= read_size;
        read_position += read_size;

        XGDLog().print_progress(prog_processed_ += read_size, prog_total_);

        check_status_flags();
    }
}

void CCIWriter::write_file_from_dir(std::ofstream& out_file, std::vector<CCI::IndexInfo>& index_infos, AvlTree::Node& node) 
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

        if (read_size % Xiso::SECTOR_SIZE) // Pad buffer to sector boundary with 0xFF
        {
            std::memset(read_buffer.data() + read_size, Xiso::PAD_BYTE, (Xiso::SECTOR_SIZE - (read_size % Xiso::SECTOR_SIZE)));
        }

        compress_and_write_sectors_managed(out_file, index_infos, num_sectors(read_size), read_buffer.data());

        bytes_remaining -= read_size;

        XGDLog().print_progress(prog_processed_ += read_size, prog_total_);

        check_status_flags();
    }

    in_file.close();
}

void CCIWriter::thread_worker()
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
        size_t max_size = LZ4_compressBound(Xiso::SECTOR_SIZE);

        while (true)
        {
            uint32_t sec_start = batch_ctx_.next_sector.fetch_add(STEAL_CHUNK, std::memory_order_relaxed);
            if (sec_start >= batch_ctx_.num_sectors) break;

            uint32_t sec_end = std::min(sec_start + STEAL_CHUNK, batch_ctx_.num_sectors);
            for (uint32_t sec = sec_start; sec < sec_end; ++sec)
            {
                const char* in_ptr = batch_ctx_.in_buffer + (static_cast<size_t>(sec) * Xiso::SECTOR_SIZE);
                char* out_ptr = batch_ctx_.out_buffer + (static_cast<size_t>(sec) * max_size);

                int compressed_size = LZ4_compress_HC(in_ptr, out_ptr, Xiso::SECTOR_SIZE, Xiso::SECTOR_SIZE, compression_level_);

                CompressedTaskResult& result = batch_ctx_.results[sec];
                result.sector_idx = sec;
                result.compressed_size = compressed_size;
                result.compressed = compressed_size > 0 && compressed_size < static_cast<int>(Xiso::SECTOR_SIZE - (4 + ALIGN_MULT));
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

/*  This will check if the out file needs to be split or if it needs room 
    for a CCI header using check_and_manage_write and finalize_out_file */
void CCIWriter::compress_and_write_sectors_managed(std::ofstream& out_file, std::vector<CCI::IndexInfo>& index_infos, const uint32_t num_sectors, const char* in_buffer)
{
    if (num_sectors == 0) return;

    size_t max_size = LZ4_compressBound(Xiso::SECTOR_SIZE);
    if (batch_compress_buffer_.size() < static_cast<size_t>(num_sectors) * max_size)
    {
        batch_compress_buffer_.resize(static_cast<size_t>(num_sectors) * max_size);
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
        const auto& result = batch_results_[i];
        check_and_manage_write(out_file, index_infos);

        if (result.compressed)
        {
            uint8_t padding = static_cast<uint8_t>(((result.compressed_size + 1 + ALIGN_MULT - 1) / ALIGN_MULT * ALIGN_MULT) - (result.compressed_size + 1));
            out_file.write(reinterpret_cast<const char*>(&padding), sizeof(uint8_t));
            out_file.write(result.buffer_to_write, result.compressed_size);

            if (padding != 0)
            {
                std::vector<char> empty_buffer(padding, 0);
                out_file.write(empty_buffer.data(), padding);
            }

            index_infos.push_back({ static_cast<uint32_t>(result.compressed_size + 1 + padding), true });
        }
        else
        {
            out_file.write(result.buffer_to_write, Xiso::SECTOR_SIZE);
            index_infos.push_back({ Xiso::SECTOR_SIZE, false });
        }

        if (out_file.fail())
        {
            throw std::runtime_error("Failed to write to output file");
        }
    }
}

void CCIWriter::check_and_manage_write(std::ofstream& out_file, std::vector<CCI::IndexInfo>& index_infos)
{
    if (static_cast<uint64_t>(out_file.tellp()) > CCI::SPLIT_OFFSET)
    {
        finalize_out_file(out_file, index_infos);
        out_file.close();

        out_file = std::ofstream(out_filepath_2_, std::ios::binary);
        if (!out_file.is_open())
        {
            throw XGDException(ErrCode::FILE_OPEN, HERE(), "Failed to open output file: " + out_filepath_2_.string());
        }
    }

    if (index_infos.size() == 0 && out_file.tellp() == 0)
    {
        std::vector<char> empty_buffer(sizeof(CCI::Header), 0);
        out_file.write(empty_buffer.data(), sizeof(CCI::Header));
    }
}

//  This writes the index info and finalized header to the current out file
void CCIWriter::finalize_out_file(std::ofstream& out_file, std::vector<CCI::IndexInfo>& index_infos) 
{
    out_file.seekp(0, std::ios::end);

    uint64_t index_offset = out_file.tellp();
    uint64_t uncompressed_size = index_infos.size() * Xiso::SECTOR_SIZE;
    uint32_t position = CCI::HEADER_SIZE;

    for (const auto& index_info : index_infos) 
    {
        uint32_t index = static_cast<uint32_t>(position >> CCI::INDEX_ALIGNMENT) | (index_info.compressed ? 0x80000000 : 0);
        out_file.write(reinterpret_cast<const char*>(&index), sizeof(uint32_t));
        position += index_info.value;
    }

    uint32_t index_end = static_cast<uint32_t>(position >> CCI::INDEX_ALIGNMENT);
    out_file.write(reinterpret_cast<const char*>(&index_end), sizeof(uint32_t));

    CCI::Header cci_header(uncompressed_size, index_offset);

    out_file.seekp(0, std::ios::beg);
    out_file.write(reinterpret_cast<char*>(&cci_header), sizeof(CCI::Header));

    if (out_file.fail()) 
    {
        throw XGDException(ErrCode::FILE_WRITE, HERE(), "Failed to write to output file");  
    }

    index_infos.clear();
}

std::vector<std::filesystem::path> CCIWriter::out_paths() 
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
    catch (const std::exception& e) 
    {
        XGDLog(Error) << "Warning: Failed to rename output file: " << e.what() << XGDLog::Endl;
    }

    return { out_filepath_1_ };
}