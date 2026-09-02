#ifndef _ZAR_WRITER_H_
#define _ZAR_WRITER_H_

#include <cstdint>
#include <filesystem>
#include <fstream>

#include "zarchive/zarchivewriter.h"

#include "XGD.h"
#include "AvlTree/AvlTree.h"
#include "ImageReader/ImageReader.h"
#include "ImageWriter/ImageWriter.h"

class ZARWriter : public ImageWriter {
public:
    ZARWriter(std::shared_ptr<ImageReader> image_reader, int compression_level = 0, int threads = 0);
    ZARWriter(const std::filesystem::path& in_dir_path, int compression_level = 0, int threads = 0);

    ~ZARWriter() override = default;

    std::vector<std::filesystem::path> convert(const std::filesystem::path& out_zar_path) override;

private:
    std::shared_ptr<ImageReader> image_reader_{nullptr};
    std::filesystem::path in_dir_path_;
    int compression_level_{0};
    int threads_{0};

    int get_effective_zstd_level() const;

    void convert_from_iso(const std::filesystem::path& out_zar_path);
    void convert_from_dir(const std::filesystem::path& out_zar_path);
};

#endif // _ZAR_WRITER_H_