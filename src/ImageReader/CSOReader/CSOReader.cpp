#include <cstring>
#include <thread>
#include <algorithm>

#include "ImageReader/CSOReader/CSOReader.h"

CSOReader::CSOReader(const std::vector<std::filesystem::path>& in_cso_paths)
    : in_cso_paths_(in_cso_paths)
{
    part_1_size_ = std::filesystem::file_size(in_cso_paths_.front());

    for (const auto& path : in_cso_paths_) 
    {
        in_files_.push_back(std::make_unique<std::ifstream>(path, std::ios::binary));
        if (!in_files_.back()->is_open()) 
        {
            throw XGDException(ErrCode::FILE_OPEN, HERE(), path.string());
        }
    }

    verify_and_populate_index_infos();

    total_sectors_ = static_cast<uint32_t>(index_infos_.size()) - 1;

    LZ4F_errorCode_t lz4f_error = LZ4F_createDecompressionContext(&lz4f_dctx_, LZ4F_VERSION);
    if (LZ4F_isError(lz4f_error)) 
    {
        throw XGDException(ErrCode::MISC, HERE(), LZ4F_getErrorName(lz4f_error));
    }
}

CSOReader::~CSOReader()
{
    LZ4F_freeDecompressionContext(lz4f_dctx_);

    for (auto& file : in_files_) 
    {
        file->close();
    }
    in_files_.clear();
}

void CSOReader::verify_and_populate_index_infos()
{
    index_infos_.clear();

    in_files_[0]->seekg(0, std::ios::beg);

    CSO::Header header;

    in_files_[0]->read(reinterpret_cast<char*>(&header), sizeof(CSO::Header));
    if (in_files_[0]->fail()) 
    {
        throw XGDException(ErrCode::FILE_READ, HERE());
    }

    if ((std::memcmp(&header.magic, CSO::MAGIC, CSO::MAGIC_LEN) != 0) ||
        header.block_size != CSO::BLOCK_SIZE ||
        header.header_size != CSO::HEADER_SIZE ||
        header.version != CSO::VERSION ||
        header.index_alignment != CSO::INDEX_ALIGNMENT) 
    {
        throw XGDException(ErrCode::ISO_INVALID, HERE());
    }

    index_infos_.reserve(static_cast<uint32_t>(header.uncompressed_size / Xiso::SECTOR_SIZE) + 1);

    for (uint32_t j = 0; j < (header.uncompressed_size / CSO::BLOCK_SIZE) + 1; ++j) 
    {
        uint32_t index;
        
        in_files_[0]->read(reinterpret_cast<char*>(&index), sizeof(uint32_t));
        if (in_files_[0]->fail()) 
        {
            throw XGDException(ErrCode::FILE_READ, HERE());
        }

        index_infos_.push_back({ (index & 0x7FFFFFFF) << CSO::INDEX_ALIGNMENT, ((index & 0x80000000) > 0) });
    }
}

void CSOReader::read_sector(const uint32_t sector, char* out_buffer)
{
    size_t read_len = index_infos_[sector + 1].value - index_infos_[sector].value;
    int file_idx = (index_infos_[sector].value > part_1_size_) ? 1 : 0;

    in_files_[file_idx]->seekg(index_infos_[sector].value, std::ios::beg);

    if (index_infos_[sector].compressed || read_len < Xiso::SECTOR_SIZE)
    {
        size_t compressed_size = sizeof(LZ4F_HEADER) + read_len + sizeof(LZ4F_FOOTER);
        size_t decompressed_size = Xiso::SECTOR_SIZE;

        std::vector<char> read_buffer(compressed_size, 0);
        std::memcpy(read_buffer.data(), LZ4F_HEADER, sizeof(LZ4F_HEADER));

        in_files_[file_idx]->read(read_buffer.data() + sizeof(LZ4F_HEADER), read_len);
        if (in_files_[file_idx]->fail()) 
        {
            throw XGDException(ErrCode::FILE_READ, HERE());
        }

        std::memcpy(read_buffer.data() + sizeof(LZ4F_HEADER) + read_len, LZ4F_FOOTER, sizeof(LZ4F_FOOTER));

        size_t lz4_decompressed_size = LZ4F_decompress(lz4f_dctx_, out_buffer, &decompressed_size, read_buffer.data(), &compressed_size, nullptr);
        if (LZ4F_isError(lz4_decompressed_size)) 
        {
            throw XGDException(ErrCode::MISC, HERE(), LZ4F_getErrorName(lz4_decompressed_size));
        }
    }
    else if (read_len != Xiso::SECTOR_SIZE)
    {
        throw XGDException(ErrCode::ISO_INVALID, HERE());
    }
    else
    {
        in_files_[file_idx]->read(out_buffer, Xiso::SECTOR_SIZE);
        if (in_files_[file_idx]->fail()) 
        {
            throw XGDException(ErrCode::FILE_READ, HERE());
        }
    }
}

void CSOReader::read_sectors(const uint32_t start_sector, const uint32_t count, char* out_buffer)
{
    if (count == 0) return;
    if (count == 1)
    {
        read_sector(start_sector, out_buffer);
        return;
    }

    // Check if crossing part 1 boundary
    if (index_infos_[start_sector].value <= part_1_size_ && index_infos_[start_sector + count].value > part_1_size_)
    {
        uint32_t s_mid = start_sector;
        while (s_mid < start_sector + count && index_infos_[s_mid].value <= part_1_size_)
        {
            s_mid++;
        }
        uint32_t count0 = s_mid - start_sector;
        uint32_t count1 = count - count0;
        read_sectors(start_sector, count0, out_buffer);
        read_sectors(s_mid, count1, out_buffer + (static_cast<size_t>(count0) * Xiso::SECTOR_SIZE));
        return;
    }

    int file_idx = (index_infos_[start_sector].value > part_1_size_) ? 1 : 0;
    if (file_idx > 0 && in_files_.size() < 2)
    {
        throw XGDException(ErrCode::MISC, HERE(), "Sector requested is out of bounds");
    }

    uint64_t start_offset = index_infos_[start_sector].value;
    uint64_t end_offset = index_infos_[start_sector + count].value;
    size_t total_compressed_bytes = static_cast<size_t>(end_offset - start_offset);

    std::vector<char> raw_chunk(total_compressed_bytes);
    in_files_[file_idx]->seekg(start_offset, std::ios::beg);
    in_files_[file_idx]->read(raw_chunk.data(), total_compressed_bytes);
    if (in_files_[file_idx]->fail())
    {
        throw XGDException(ErrCode::FILE_READ, HERE());
    }

    uint32_t num_threads = std::max(1u, std::min(std::thread::hardware_concurrency(), 16u));
    if (num_threads > count) num_threads = count;

    auto decompress_slice = [&](uint32_t sec_begin, uint32_t sec_finish) {
        LZ4F_decompressionContext_t local_dctx;
        LZ4F_createDecompressionContext(&local_dctx, LZ4F_VERSION);

        std::vector<char> frame_buf;

        for (uint32_t i = sec_begin; i < sec_finish; ++i)
        {
            uint32_t sec_idx = start_sector + i;
            size_t sec_offset = static_cast<size_t>(index_infos_[sec_idx].value - start_offset);
            size_t sec_len = index_infos_[sec_idx + 1].value - index_infos_[sec_idx].value;
            char* dest = out_buffer + (static_cast<size_t>(i) * Xiso::SECTOR_SIZE);

            if (index_infos_[sec_idx].compressed || sec_len < Xiso::SECTOR_SIZE)
            {
                size_t frame_size = sizeof(LZ4F_HEADER) + sec_len + sizeof(LZ4F_FOOTER);
                if (frame_buf.size() < frame_size) frame_buf.resize(frame_size);

                std::memcpy(frame_buf.data(), LZ4F_HEADER, sizeof(LZ4F_HEADER));
                std::memcpy(frame_buf.data() + sizeof(LZ4F_HEADER), raw_chunk.data() + sec_offset, sec_len);
                std::memcpy(frame_buf.data() + sizeof(LZ4F_HEADER) + sec_len, LZ4F_FOOTER, sizeof(LZ4F_FOOTER));

                size_t src_size = frame_size;
                size_t dst_size = Xiso::SECTOR_SIZE;
                size_t lz4_res = LZ4F_decompress(local_dctx, dest, &dst_size, frame_buf.data(), &src_size, nullptr);
                if (LZ4F_isError(lz4_res) || dst_size != Xiso::SECTOR_SIZE)
                {
                    LZ4F_freeDecompressionContext(local_dctx);
                    throw XGDException(ErrCode::MISC, HERE(), "LZ4F_decompress failed");
                }
            }
            else
            {
                std::memcpy(dest, raw_chunk.data() + sec_offset, Xiso::SECTOR_SIZE);
            }
        }

        LZ4F_freeDecompressionContext(local_dctx);
    };

    if (num_threads > 1 && count >= 8)
    {
        std::vector<std::thread> workers;
        uint32_t per_worker = (count + num_threads - 1) / num_threads;
        for (uint32_t t = 0; t < num_threads; ++t)
        {
            uint32_t b = t * per_worker;
            uint32_t f = std::min(b + per_worker, count);
            if (b >= f) break;
            workers.emplace_back(decompress_slice, b, f);
        }
        for (auto& w : workers)
        {
            if (w.joinable()) w.join();
        }
    }
    else
    {
        decompress_slice(0, count);
    }
}

void CSOReader::read_bytes(const uint64_t offset, const size_t size, char* out_buffer) 
{
    uint32_t sectors_to_read = static_cast<uint32_t>(size / Xiso::SECTOR_SIZE) + ((size % Xiso::SECTOR_SIZE) ? 1 : 0);
    uint32_t start_sector = static_cast<uint32_t>(offset / Xiso::SECTOR_SIZE);
    size_t position_in_sector = offset % Xiso::SECTOR_SIZE;

    std::vector<char> buffer(static_cast<size_t>(sectors_to_read) * Xiso::SECTOR_SIZE);
    read_sectors(start_sector, sectors_to_read, buffer.data());

    std::memcpy(out_buffer, buffer.data() + position_in_sector, size);
}