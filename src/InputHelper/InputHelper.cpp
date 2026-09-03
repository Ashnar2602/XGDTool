#include <iomanip>
#include <sstream>
#include <openssl/evp.h>
#include <zlib.h>
#include <thread>
#include <atomic>

#include "ImageReader/ImageReader.h"
#include "InputHelper/InputHelper.h"
#include "Executable/AttachXbeTool.h"
#include "Executable/ExeTool.h"
#include "Utils/LocalizationManager.h"
#include "Utils/ChecksumHelper.h"

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
            case FileType::VERIFY:
                verify_image(input_info);
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
                    ChecksumResult chk;
                    if (image_writer_)
                    {
                        chk = image_writer_->get_precalculated_checksum(out_file);
                    }
                    if (!chk.valid)
                    {
                        chk = calculate_file_checksums(out_file);
                    }
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
    const std::filesystem::path orig_input_path = input_info.paths.front();

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

    std::filesystem::path out_path = get_output_path(output_directory_, *title_helper, orig_input_path);

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

void InputHelper::verify_image(const InputInfo& input_info)
{
    if (input_info.file_type == FileType::DIR)
    {
        throw XGDException(ErrCode::ISO_INVALID, HERE(), "Cannot verify a directory, please specify an image file (.iso, .cso, .cci, .god, .zar)");
    }

    XGDLog(Normal) << "========================================================\n"
                   << "               XGDTool Image Verification               \n"
                   << "========================================================" << XGDLog::Endl;

    std::shared_ptr<ImageReader> image_reader;
    if (input_info.file_type == FileType::ZAR)
    {
        XGDLog(Normal) << "  Format:           ZArchive (.zar)\n"
                       << "  Archive Path:     " << input_info.paths.front().string() << XGDLog::Endl;
        ZARExtractor zar_extractor(input_info.paths.front());
        zar_extractor.list_files();
        return;
    }
    else
    {
        image_reader = ImageReader::create_instance(input_info.file_type, input_info.paths);
    }

    if (!image_reader)
    {
        throw XGDException(ErrCode::ISO_INVALID, HERE(), "Failed to open image reader for verification");
    }

    std::string plat_str;
    switch (image_reader->platform())
    {
        case Platform::OGX: plat_str = "Original Xbox"; break;
        case Platform::X360: plat_str = "Xbox 360"; break;
        default: plat_str = "Unknown"; break;
    }

    uint32_t total_sec = image_reader->total_sectors();
    uint64_t total_size = static_cast<uint64_t>(total_sec) * Xiso::SECTOR_SIZE;
    uint64_t img_off = image_reader->image_offset();

    XGDLog(Normal) << "  File Name:        " << input_info.paths.front().filename().string() << XGDLog::Endl;
    if (input_info.paths.size() > 1)
    {
        XGDLog(Normal) << "  Split Segments:   " << input_info.paths.size() << " parts" << XGDLog::Endl;
    }
    XGDLog(Normal) << "  Platform:         " << plat_str << XGDLog::Endl;
    XGDLog(Normal) << "  Total Sectors:    " << total_sec << " (" << std::fixed << std::setprecision(2) << (total_size / (1024.0 * 1024.0 * 1024.0)) << " GB)" << XGDLog::Endl;
    XGDLog(Normal) << "  Filesystem Offset:0x" << std::hex << img_off << std::dec << " (Sector " << (img_off / Xiso::SECTOR_SIZE) << ")" << XGDLog::Endl;

    if (total_sec == 4267008 || total_sec == 4267009)
    {
        XGDLog(Normal) << "  Disc Profile:     XGD3 (Xbox 360 8.7 GB / LayerBreak 2133520)" << XGDLog::Endl;
    }
    else if (total_sec == 3825920 || total_sec == 3825921)
    {
        XGDLog(Normal) << "  Disc Profile:     XGD2 (Xbox 360 7.8 GB / LayerBreak 1913760)" << XGDLog::Endl;
    }
    else if (total_sec == 3697984)
    {
        XGDLog(Normal) << "  Disc Profile:     Xbox Original DVD9 (LayerBreak 1913760)" << XGDLog::Endl;
    }
    else
    {
        XGDLog(Normal) << "  Disc Profile:     Custom / Trimmed (" << total_sec << " sectors)" << XGDLog::Endl;
    }

    const auto& entries = image_reader->directory_entries();
    XGDLog(Normal) << "  Filesystem Items: " << entries.size() << " files/directories" << XGDLog::Endl;
    XGDLog(Normal) << "  Uncompressed Data:" << std::fixed << std::setprecision(2) << (image_reader->total_file_bytes() / (1024.0 * 1024.0)) << " MB" << XGDLog::Endl;

    try
    {
        const auto& exe_entry = image_reader->executable_entry();
        XGDLog(Normal) << "  Primary Executable:" << exe_entry.filename << " (Start Sector: " << exe_entry.header.start_sector << ")" << XGDLog::Endl;
        ExeTool exe_tool(*image_reader, exe_entry.path);
        uint32_t tid = exe_tool.title_id();
        std::ostringstream tid_ss;
        tid_ss << std::uppercase << std::hex << std::setfill('0') << std::setw(8) << tid;
        XGDLog(Normal) << "  Title ID:         0x" << tid_ss.str() << XGDLog::Endl;

        TitleHelper th(image_reader, true);
        std::string tname = th.folder_name();
        XGDLog(Normal) << "  Game Title:       " << tname << XGDLog::Endl;
    }
    catch (...)
    {
        XGDLog(Normal) << "  Executable:       No standard default.xex / default.xbe found" << XGDLog::Endl;
    }

    XGDLog(Normal) << "--------------------------------------------------------" << XGDLog::Endl;
    XGDLog(Normal) << "Calculating streaming checksums..." << XGDLog::Endl;
    ChecksumResult chk = calculate_file_checksums(input_info.paths.front());
    std::ostringstream crc_ss;
    crc_ss << std::uppercase << std::hex << std::setfill('0') << std::setw(8) << chk.crc32;
    XGDLog(Normal) << "  CRC32:            " << crc_ss.str() << XGDLog::Endl;
    XGDLog(Normal) << "  MD5:              " << chk.md5 << XGDLog::Endl;
    XGDLog(Normal) << "  SHA-1:            " << chk.sha1 << XGDLog::Endl;
    XGDLog(Normal) << "========================================================" << XGDLog::Endl;
    XGDLog(Normal) << "  [PASS] Image structure and filesystem integrity verified!" << XGDLog::Endl;
    XGDLog(Normal) << "========================================================" << XGDLog::Endl;
}

std::filesystem::path InputHelper::extract_temp_zar(const std::filesystem::path& in_path)
{
    std::filesystem::path temp_path = output_directory_ / ".xgd_temp";

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