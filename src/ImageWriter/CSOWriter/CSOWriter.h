#ifndef _CSO_WRITER_H_
#define _CSO_WRITER_H_

#include <cstdint>
#include <vector>
#include <fstream>
#include <filesystem>
#include <queue>
#include <mutex>
#include <condition_variable>
#include <future>

#include <lz4frame.h>

#include "ImageReader/ImageReader.h"
#include "ImageWriter/ImageWriter.h"
#include "Formats/CSO.h"
#include "Formats/Xiso.h"
#include "AvlTree/AvlTree.h"
#include "XGD.h"

class CSOWriter : public ImageWriter {
public:
    CSOWriter(std::shared_ptr<ImageReader> image_reader, const ScrubType scrub_type, int compression_level = 0);
    CSOWriter(const std::filesystem::path& in_dir_path, int compression_level = 0);
    
    ~CSOWriter();

    std::vector<std::filesystem::path> convert(const std::filesystem::path& out_cso_path);

private:
    struct CompressedTaskResult 
    {
        uint32_t sector_idx{0};
        size_t compressed_size{0};
        const char* buffer_to_write{nullptr};
        bool compressed{false};
    };

    struct BatchContext 
    {
        const char* in_buffer{nullptr};
        char* out_buffer{nullptr};
        CompressedTaskResult* results{nullptr};
        uint32_t num_sectors{0};
        std::atomic<uint32_t> next_sector{0};
        std::atomic<uint32_t> active_workers{0};
        bool has_work{false};
    };

    const int ALIGN_B = 1 << CSO::INDEX_ALIGNMENT;
    const int ALIGN_M = ALIGN_B - 1;

    std::vector<std::thread> thread_pool_;
    BatchContext batch_ctx_;
    std::vector<char> batch_compress_buffer_;
    std::vector<CompressedTaskResult> batch_results_;
    std::mutex batch_mutex_;
    std::condition_variable cv_start_;
    std::condition_variable cv_done_;
    std::atomic<bool> stop_flag_{false};

    std::shared_ptr<ImageReader> image_reader_{nullptr};
    std::filesystem::path in_dir_path_; 

    ScrubType scrub_type_{ScrubType::NONE};

    std::filesystem::path out_filepath_base_;
    std::filesystem::path out_filepath_1_;
    std::filesystem::path out_filepath_2_;

    size_t lz4f_max_size_;
    std::vector<LZ4F_compressionContext_t> lz4f_ctx_pool_;
    LZ4F_preferences_t lz4f_prefs_ = 
    { 
        { LZ4F_default, LZ4F_blockIndependent, LZ4F_noContentChecksum, LZ4F_frame, 0ULL, 0U, LZ4F_noBlockChecksum }, 
        12, 
        1, 
        0u, 
        { 0u, 0u, 0u } 
    };

    uint64_t prog_total_{0};
    uint64_t prog_processed_{0};

    void init_cso_writer();

    void convert_to_cso(const bool scrub);
    void convert_to_cso_from_avl(AvlTree& avl_tree);

    void write_iso_header(std::ofstream& out_file, std::vector<uint32_t>& block_index, AvlTree& avl_tree);
    void write_file_from_reader(std::ofstream& out_file, std::vector<uint32_t>& block_index, AvlTree::Node& node);
    void write_file_from_directory(std::ofstream& out_file, std::vector<uint32_t>& block_index, AvlTree::Node& node);

    void write_cso_header(std::ofstream& out_file, const uint32_t total_sectors);
    void write_dummy_index(std::ofstream& out_file, const uint32_t total_sectors);
    void finalize_out_files(std::ofstream& out_file, std::vector<uint32_t>& block_index);
    void write_padding_sectors(std::ofstream& out_file, std::vector<uint32_t>& block_index, const uint32_t num_sectors, const char pad_byte);

    void compress_and_write_sectors_managed(std::ofstream& out_file, std::vector<uint32_t>& block_index, const uint32_t num_sectors, const char* in_buffer);
    void write_sector(std::ofstream& out_file, std::vector<uint32_t>& block_index, const CompressedTaskResult& result);
    void thread_worker(size_t thread_idx);

    void pad_to_modulus(std::ofstream& out_file, const uint64_t modulus, const char pad_byte);
    std::vector<std::filesystem::path> out_paths();
};

#endif // _CSO_WRITER_H_