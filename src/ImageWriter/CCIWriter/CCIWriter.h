#ifndef _CCI_WRITER_H_
#define _CCI_WRITER_H_

#include <cstdint>

#include <thread>
#include <vector>
#include <queue>
#include <mutex>
#include <condition_variable>
#include <future>

#include "ImageReader/ImageReader.h"
#include "ImageWriter/ImageWriter.h"    
#include "Formats/CCI.h"
#include "Formats/Xiso.h"
#include "AvlTree/AvlTree.h"
#include "XGD.h"

class CCIWriter : public ImageWriter 
{
public:
    CCIWriter(std::shared_ptr<ImageReader> image_reader, const ScrubType scrub_type, int compression_level = 0);
    CCIWriter(const std::filesystem::path& in_dir_path, int compression_level = 0);
    
    ~CCIWriter() override;

    std::vector<std::filesystem::path> convert(const std::filesystem::path& out_cci_path) override;

private:
    struct CompressedTaskResult 
    {
        uint32_t sector_idx{0};
        int compressed_size{0};
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

    const int ALIGN_MULT = 1 << CCI::INDEX_ALIGNMENT;

    std::vector<std::thread> thread_pool_;
    BatchContext batch_ctx_;
    std::vector<char> batch_compress_buffer_;
    std::vector<CompressedTaskResult> batch_results_;
    std::mutex batch_mutex_;
    std::condition_variable cv_start_;
    std::condition_variable cv_done_;
    std::atomic<bool> stop_flag_{false};

    ScrubType scrub_type_;
    int compression_level_{12};

    std::shared_ptr<ImageReader> image_reader_{nullptr};
    std::filesystem::path in_dir_path_;

    std::filesystem::path out_filepath_base_;
    std::filesystem::path out_filepath_1_;
    std::filesystem::path out_filepath_2_;

    uint64_t prog_total_{0};
    uint64_t prog_processed_{0};

    void init_cci_writer();

    void convert_to_cci(const bool scrub);
    void convert_to_cci_from_avl(AvlTree& avl_tree);

    void write_file_from_reader(std::ofstream& out_file, std::vector<CCI::IndexInfo>& index_infos, AvlTree::Node& node);
    void write_file_from_dir(std::ofstream& out_file, std::vector<CCI::IndexInfo>& index_infos, AvlTree::Node& node);

    void write_iso_header(std::ofstream& out_file, std::vector<CCI::IndexInfo>& index_infos, AvlTree& avl_tree);
    void write_padding_sectors(std::ofstream& out_file, std::vector<CCI::IndexInfo>& index_infos, const uint32_t num_sectors, const char pad_byte);

    void compress_and_write_sectors_managed(std::ofstream& out_file, std::vector<CCI::IndexInfo>& index_infos, const uint32_t num_sectors, const char* in_buffer);
    void thread_worker();
    
    void check_and_manage_write(std::ofstream& out_file, std::vector<CCI::IndexInfo>& index_infos);
    void finalize_out_file(std::ofstream& out_file, std::vector<CCI::IndexInfo>& index_infos);

    std::vector<std::filesystem::path> out_paths();
};

#endif // _CCI_WRITER_H_