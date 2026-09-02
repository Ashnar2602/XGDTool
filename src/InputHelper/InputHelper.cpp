#include <iomanip>
#include <sstream>
#include <openssl/evp.h>
#include <zlib.h>
#include <thread>
#include <atomic>

#include "ImageReader/ImageReader.h"
#include "InputHelper/InputHelper.h"
#include "Executable/AttachXbeTool.h"
#include "Utils/LocalizationManager.h"

struct ChecksumResult
{
    uint32_t crc32{0};
    std::string md5;
    std::string sha1;
};

static ChecksumResult calculate_file_checksums(const std::filesystem::path& file_path)
{
    std::ifstream is(file_path, std::ios::binary);
    if (!is.is_open()) return {};

    EVP_MD_CTX* md5_ctx = EVP_MD_CTX_new();
    EVP_MD_CTX* sha1_ctx = EVP_MD_CTX_new();
    EVP_DigestInit_ex(md5_ctx, EVP_md5(), nullptr);
    EVP_DigestInit_ex(sha1_ctx, EVP_sha1(), nullptr);

    uLong crc = crc32(0L, Z_NULL, 0);

    std::vector<char> buffer(128 * 1024);
    while (is.read(buffer.data(), buffer.size()) || is.gcount() > 0)
    {
        size_t bytes_read = static_cast<size_t>(is.gcount());
        crc = crc32(crc, reinterpret_cast<const Bytef*>(buffer.data()), static_cast<uInt>(bytes_read));
        EVP_DigestUpdate(md5_ctx, buffer.data(), bytes_read);
        EVP_DigestUpdate(sha1_ctx, buffer.data(), bytes_read);
    }

    unsigned char md5_digest[EVP_MAX_MD_SIZE];
    unsigned int md5_len = 0;
    EVP_DigestFinal_ex(md5_ctx, md5_digest, &md5_len);
    EVP_MD_CTX_free(md5_ctx);

    unsigned char sha1_digest[EVP_MAX_MD_SIZE];
    unsigned int sha1_len = 0;
    EVP_DigestFinal_ex(sha1_ctx, sha1_digest, &sha1_len);
    EVP_MD_CTX_free(sha1_ctx);

    auto to_hex = [](const unsigned char* data, unsigned int len) {
        std::ostringstream oss;
        oss << std::hex << std::setfill('0');
        for (unsigned int i = 0; i < len; ++i) {
            oss << std::setw(2) << static_cast<int>(data[i]);
        }
        return oss.str();
    };

    ChecksumResult res;
    res.crc32 = static_cast<uint32_t>(crc);
    res.md5 = to_hex(md5_digest, md5_len);
    res.sha1 = to_hex(sha1_digest, sha1_len);
    return res;
}

static void generate_dvd_file(const std::filesystem::path& iso_path)
{
    if (!std::filesystem::exists(iso_path)) return;
    std::error_code ec;
    uint64_t size_bytes = std::filesystem::file_size(iso_path, ec);
    if (ec) return;

    // XGD3 is ~8.13 GB (8,738,846,720 bytes) or > 7.5 GB. LayerBreak is 2133520
    // XGD2 is ~7.3 GB (7,835,492,352 bytes) or OG Xbox DL. LayerBreak is 1913760
    uint32_t layer_break = (size_bytes > 8000000000ULL) ? 2133520 : 1913760;

    std::filesystem::path dvd_path = iso_path;
    dvd_path.replace_extension(".dvd");

    std::ofstream dvd_file(dvd_path);
    if (dvd_file.is_open())
    {
        dvd_file << "LayerBreak=" << layer_break << "\r\n";
        dvd_file << iso_path.filename().string() << "\r\n";
        dvd_file.close();
        XGDLog(Normal) << "Generated .dvd file: " << dvd_path.filename().string() << " (LayerBreak=" << layer_break << ")" << XGDLog::Endl;
    }
}

InputHelper::InputHelper(std::filesystem::path in_path, std::filesystem::path out_directory, OutputSettings output_settings)
    :   output_directory_(out_directory), 
        output_settings_((output_settings.auto_format != AutoFormat::NONE) ? get_auto_output_settings(output_settings.auto_format) : output_settings)
{
    add_input(in_path);

    if (output_directory_.empty()) 
    {
        output_directory_ = in_path.parent_path() / "XGDTool_Output";
    }

    output_directory_ = std::filesystem::absolute(output_directory_);
}

InputHelper::InputHelper(std::vector<std::filesystem::path> in_paths, std::filesystem::path out_directory, OutputSettings output_settings)
    :   output_directory_(out_directory), 
        output_settings_((output_settings.auto_format != AutoFormat::NONE) ? get_auto_output_settings(output_settings.auto_format) : output_settings)
{
    for (const auto& in_path : in_paths) 
    {
        add_input(in_path);
    }

    if (output_directory_.empty()) 
    {
        output_directory_ = in_paths.front().parent_path() / "XGDTool_Output";
    }

    output_directory_ = std::filesystem::absolute(output_directory_);
}

void InputHelper::add_input(const std::filesystem::path& in_path) 
{
    if (in_path.empty() || !std::filesystem::exists(in_path)) 
    {
        return;
    }

    FileType in_file_type = get_filetype(in_path);

    if (in_file_type == FileType::UNKNOWN) 
    {
        if (!is_batch_dir(in_path)) 
        {
            return;
        }

        for (const auto& entry : std::filesystem::directory_iterator(in_path)) 
        {
            if ((in_file_type = get_filetype(entry.path())) != FileType::UNKNOWN) 
            {
                if (entry.is_regular_file() && !is_part_2_file(entry.path()))
                {
                    input_infos_.push_back({ in_file_type, find_split_filepaths(entry.path()) });
                }
                else if (entry.is_directory())
                {
                    input_infos_.push_back({ in_file_type, { entry.path() } });
                }
            }
        }
    }
    else 
    {
        if (std::filesystem::is_regular_file(in_path)) 
        {
            input_infos_.push_back({ in_file_type, find_split_filepaths(in_path) });
        } 
        else if (std::filesystem::is_directory(in_path))
        {
            input_infos_.push_back({ in_file_type, { in_path } });
        }
    }

    remove_duplicate_infos(input_infos_);
}

void InputHelper::process_all() 
{
    failed_inputs_.clear();

    if (output_settings_.threads > 1 && input_infos_.size() > 1)
    {
        size_t max_threads = std::min(static_cast<size_t>(output_settings_.threads), input_infos_.size());
        std::atomic<size_t> next_index{0};
        std::vector<std::thread> workers;

        for (size_t t = 0; t < max_threads; ++t)
        {
            workers.emplace_back([this, &next_index]() {
                while (true)
                {
                    size_t idx = next_index.fetch_add(1);
                    if (idx >= input_infos_.size()) break;
                    process_single(input_infos_[idx]);
                }
            });
        }

        for (auto& w : workers)
        {
            if (w.joinable()) w.join();
        }
    }
    else
    {
        for (auto& input_info : input_infos_) 
        {
            process_single(input_info);
        }
    }
}

void InputHelper::process_single(InputInfo input_info)
{
    try 
    {
        std::string proc_target = input_info.paths.front().string() + ((input_info.paths.size() > 1) ? (" and " + input_info.paths.back().string()) : "");
        XGDLog() << I18n::format("cli_msg_processing", proc_target) << "\n";
        
        std::vector<std::filesystem::path> out_paths;

        switch (output_settings_.file_type) 
        {
            case FileType::UNKNOWN:
                throw XGDException(ErrCode::ISO_INVALID, HERE(), "Unknown output file type");
            case FileType::DIR:
                out_paths = create_dir(input_info);
                break;
            case FileType::XBE:
                out_paths = create_attach_xbe(input_info);
                break;
            case FileType::LIST:
                list_files(input_info);
                break;
            default:
                out_paths = create_image(input_info);
                break;
        } 

        if (!out_paths.empty())
        {
            std::string out_target = out_paths.front().string() + ((out_paths.size() > 1) ? (" and " + out_paths.back().string()) : "");
            XGDLog() << I18n::format("cli_msg_success_created", out_target) << "\n";

            for (const auto& out_file : out_paths)
            {
                if (output_settings_.generate_dvd && out_file.extension() == ".iso")
                {
                    generate_dvd_file(out_file);
                }
                if (output_settings_.calculate_checksum && std::filesystem::is_regular_file(out_file))
                {
                    XGDLog(Normal) << "Calculating checksums for " << out_file.filename().string() << "..." << XGDLog::Endl;
                    auto chk = calculate_file_checksums(out_file);
                    std::ostringstream crc_hex;
                    crc_hex << std::uppercase << std::hex << std::setfill('0') << std::setw(8) << chk.crc32;
                    XGDLog(Normal) << "  CRC32:  " << crc_hex.str() << XGDLog::Endl;
                    XGDLog(Normal) << "  MD5:    " << chk.md5 << XGDLog::Endl;
                    XGDLog(Normal) << "  SHA-1:  " << chk.sha1 << XGDLog::Endl;
                }
            }
        }
    } 
    catch (const XGDException& e) 
    {
        reset_processor();
        {
            std::lock_guard<std::mutex> lock(mutex_);
            failed_inputs_.insert(failed_inputs_.end(), input_info.paths.begin(), input_info.paths.end());
        }
        XGDLog(Error) << e.what() << "\n";
    }
    catch (const std::exception& e) 
    {
        reset_processor();
        {
            std::lock_guard<std::mutex> lock(mutex_);
            failed_inputs_.insert(failed_inputs_.end(), input_info.paths.begin(), input_info.paths.end());
        }
        XGDLog(Error) << e.what() << "\n";
    }
}

std::vector<std::filesystem::path> InputHelper::create_image(InputInfo& input_info)
{
    std::filesystem::path temp_path;

    if (input_info.file_type == FileType::ZAR) 
    {
        temp_path = extract_temp_zar(input_info.paths.front());
        input_info.paths = { temp_path };
        input_info.file_type = FileType::DIR;
    }
    else if (input_info.file_type == FileType::XBE)
    {
        throw XGDException(ErrCode::ISO_INVALID, HERE(), "Cannot create image from XBE file");
    }

    std::unique_ptr<TitleHelper> title_helper;
    std::shared_ptr<ImageReader> image_reader;

    switch (input_info.file_type) 
    {
        case FileType::DIR:
            title_helper = std::make_unique<TitleHelper>(input_info.paths.front(), output_settings_.offline_mode);
            break;
        default:
            image_reader = ImageReader::create_instance(input_info.file_type, input_info.paths);
            title_helper = std::make_unique<TitleHelper>(image_reader, output_settings_.offline_mode);
            break;
    }

    std::filesystem::path out_path = get_output_path(output_directory_, *title_helper, input_info.paths.front());

    switch (input_info.file_type) 
    {
        case FileType::DIR:
            image_writer_ = ImageWriter::create_instance(input_info.paths.front(), *title_helper, output_settings_);
            break;
        default:
            image_writer_ = ImageWriter::create_instance(image_reader, *title_helper, output_settings_);
            break;
    }

    std::vector<std::filesystem::path> final_out_paths = image_writer_->convert(out_path);
    
    reset_processor();

    if (!temp_path.empty()) 
    {
        try
        {
            std::filesystem::remove_all(temp_path);
        }
        catch (const std::filesystem::filesystem_error& e)
        {
            throw XGDException(ErrCode::FS_REMOVE, HERE(), e.what());
        }
    }

    if (output_settings_.attach_xbe && title_helper->platform() == Platform::OGX)
    {
        AttachXbeTool attach_xbe_tool(*title_helper);
        attach_xbe_tool.generate_attach_xbe(final_out_paths.front().parent_path() / "default.xbe");
    }

    return final_out_paths;
}

std::vector<std::filesystem::path> InputHelper::create_dir(const InputInfo& input_info)
{
    if (input_info.file_type == FileType::DIR)
    {
        throw XGDException(ErrCode::ISO_INVALID, HERE(), "Cannot create directory from directory");
    }
    else if (input_info.file_type == FileType::XBE)
    {
        throw XGDException(ErrCode::ISO_INVALID, HERE(), "Cannot extract XBE file");
    }
    else if (input_info.file_type == FileType::ZAR)
    {
        zar_extractor_ = std::make_unique<ZARExtractor>(input_info.paths.front());
        zar_extractor_->extract(output_directory_ / input_info.paths.front().stem());
        reset_processor();
        return { output_directory_ / input_info.paths.front().stem() };
    }

    std::shared_ptr<ImageReader> image_reader = ImageReader::create_instance(input_info.file_type, input_info.paths);

    TitleHelper title_helper(image_reader, output_settings_.offline_mode);

    std::filesystem::path out_path = get_output_path(output_directory_, title_helper, input_info.paths.front());

    image_extractor_ = std::make_unique<ImageExtractor>(*image_reader, title_helper, output_settings_.allowed_media_patch, output_settings_.rename_xbe);
    image_extractor_->extract(out_path);
    reset_processor();

    return { out_path };
}

std::vector<std::filesystem::path> InputHelper::create_attach_xbe(const InputInfo& input_info)
{
    if (input_info.file_type == FileType::DIR || input_info.file_type == FileType::ZAR || input_info.file_type == FileType::XBE)
    {
        throw XGDException(ErrCode::ISO_INVALID, HERE(), "Cannot create attach XBE from input type");
    }

    std::shared_ptr<ImageReader> image_reader = ImageReader::create_instance(input_info.file_type, input_info.paths);

    if (image_reader->platform() != Platform::OGX)
    {
        throw XGDException(ErrCode::ISO_INVALID, HERE(), "Attach XBE can only be created for OGX images");
    }

    TitleHelper title_helper(image_reader, output_settings_.offline_mode);

    std::filesystem::path out_path = get_output_path(input_info.paths.front().parent_path(), title_helper, input_info.paths.front());

    AttachXbeTool attach_xbe_tool(title_helper); 
    attach_xbe_tool.generate_attach_xbe(out_path);

    return { out_path };
}

void InputHelper::list_files(const InputInfo& input_info) 
{
    if (input_info.file_type == FileType::DIR) 
    {
        throw XGDException(ErrCode::ISO_INVALID, HERE(), "Cannot list files from directory");
    }

    XGDLog() << I18n::get("cli_msg_files_in_image") << "\n";

    if (input_info.file_type == FileType::ZAR) 
    {
        ZARExtractor zar_extractor(input_info.paths.front());
        zar_extractor.list_files();
        return;
    }

    std::shared_ptr<ImageReader> image_reader = ImageReader::create_instance(input_info.file_type, input_info.paths);

    for (const auto& entry : image_reader->directory_entries()) 
    {
        if ((entry.header.attributes & Xiso::ATTRIBUTE_DIRECTORY) || entry.path.empty())
        {
            continue;
        }

        XGDLog() << entry.path.string() << " (" << entry.header.file_size << " bytes)\n";
    }
}

std::filesystem::path InputHelper::extract_temp_zar(const std::filesystem::path& in_path)
{
    std::filesystem::path temp_path = output_directory_ / "_temp";

    try 
    {
        std::filesystem::create_directories(temp_path);
    } 
    catch (const std::filesystem::filesystem_error& e) 
    {
        XGDException(ErrCode::FS_MKDIR, HERE(), e.what());
    }
    
    ZARExtractor zar_extractor(in_path);
    zar_extractor.extract(temp_path);

    return temp_path;
}

void InputHelper::cancel_processing() 
{
    if (image_writer_) 
    {
        image_writer_->cancel_processing();
    }
    if (image_extractor_) 
    {
        image_extractor_->cancel_processing();
    }
    if (zar_extractor_) 
    {
        zar_extractor_->cancel_processing();
    }
}

void InputHelper::pause_processing() 
{
    if (image_writer_) 
    {
        image_writer_->pause_processing();
    }
    if (image_extractor_) 
    {
        image_extractor_->pause_processing();
    }
    if (zar_extractor_) 
    {
        zar_extractor_->pause_processing();
    }
}

void InputHelper::resume_processing() 
{
    if (image_writer_) 
    {
        image_writer_->resume_processing();
    }
    if (image_extractor_) 
    {
        image_extractor_->resume_processing();
    }
    if (zar_extractor_) 
    {
        zar_extractor_->resume_processing();
    }
}

void InputHelper::reset_processor() 
{
    if (image_writer_) 
    {
        image_writer_.reset();
    }
    if (image_extractor_) 
    {
        image_extractor_.reset();
    }
    if (zar_extractor_) 
    {
        zar_extractor_.reset();
    }
}