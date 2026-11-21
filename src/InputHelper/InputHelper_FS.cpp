#include <algorithm>

#include "Utils/StringUtils.h"
#include "InputHelper/InputHelper.h"

std::filesystem::path InputHelper::normalize_input_path(const std::filesystem::path& path)
{
    if (path.empty())
    {
        return path;
    }

    std::filesystem::path normalized = path.lexically_normal();
    while (normalized.filename().empty() && normalized.has_parent_path() && normalized != normalized.root_path())
    {
        normalized = normalized.parent_path();
    }
    return normalized;
}

std::vector<std::filesystem::path> InputHelper::find_xex_directories(const std::filesystem::path& root_dir, int max_depth)
{
    std::vector<std::filesystem::path> results;
    std::filesystem::path normalized = normalize_input_path(root_dir);
    std::error_code ec;
    if (normalized.empty() || !std::filesystem::is_directory(normalized, ec))
    {
        return results;
    }

    auto has_xex_or_xbe = [](const std::filesystem::path& dir) -> bool {
        bool exe_found = false;
        std::error_code iter_ec;
        for (const auto& entry : std::filesystem::directory_iterator(dir, iter_ec))
        {
            if (iter_ec) break;
            if (entry.is_regular_file(iter_ec))
            {
                std::string ext = entry.path().extension().string();
                if (ext.size() == 4 &&
                    (StringUtils::case_insensitive_search(ext, ".xex") ||
                     StringUtils::case_insensitive_search(ext, ".xbe")))
                {
                    exe_found = true;
                }
                else if (ext.size() == 4 &&
                    (StringUtils::case_insensitive_search(ext, ".iso") ||
                     StringUtils::case_insensitive_search(ext, ".cso") ||
                     StringUtils::case_insensitive_search(ext, ".cci")))
                {
                    return false;
                }
            }
        }
        return exe_found;
    };

    if (has_xex_or_xbe(normalized))
    {
        results.push_back(normalized);
        return results;
    }

    auto scan_recursive = [&](auto&& self, const std::filesystem::path& current, int depth) -> void {
        if (depth > max_depth)
        {
            return;
        }
        std::vector<std::filesystem::path> subdirs;
        std::error_code iter_ec;
        for (const auto& entry : std::filesystem::directory_iterator(current, iter_ec))
        {
            if (iter_ec) break;
            if (entry.is_directory(iter_ec))
            {
                std::string name = entry.path().filename().string();
                if (!name.empty() && name[0] != '.')
                {
                    subdirs.push_back(normalize_input_path(entry.path()));
                }
            }
        }
        std::sort(subdirs.begin(), subdirs.end());
        for (const auto& sub : subdirs)
        {
            if (has_xex_or_xbe(sub))
            {
                results.push_back(sub);
            }
            else
            {
                self(self, sub, depth + 1);
            }
        }
    };

    scan_recursive(scan_recursive, normalized, 1);
    return results;
}

bool InputHelper::has_extension(const std::filesystem::path& path, const std::string& extension) 
{
    std::string ext = path.extension().string();
    if (ext.size() == extension.size()) 
    {
        return StringUtils::case_insensitive_search(ext, extension);
    }
    return false;
}

bool InputHelper::is_extracted_dir(const std::filesystem::path& path) 
{
    bool exe_found = false; 
    
    for (const auto& entry : std::filesystem::directory_iterator(path)) 
    {
        if (entry.is_regular_file()) 
        {
            if (has_extension(entry.path(), ".xbe")) 
            {
                exe_found = true;
            }
            else if (has_extension(entry.path(), ".xex")) 
            {
                exe_found = true;
            }
            else if (has_extension(entry.path(), ".iso") ||
                     has_extension(entry.path(), ".cso") ||
                     has_extension(entry.path(), ".cci"))
            {
                return false;
            }
        }
    }
    return exe_found;
}

bool InputHelper::is_god_dir_helper(const std::filesystem::path& path, int current_depth, int max_depth) 
{
    if (current_depth > max_depth) 
    {
        return false;
    }

    for (const auto& entry : std::filesystem::directory_iterator(path)) 
    {
        if (entry.is_regular_file() && entry.path().filename().string().rfind("Data", 0) == 0) 
        {
            if (entry.path().parent_path().extension() == ".data") 
            {
                return true;
            }
        } 
        else if (entry.is_directory()) 
        {
            if (is_god_dir_helper(entry.path(), current_depth + 1, max_depth)) 
            {
                return true;
            }
        }
    }
    return false;
}

bool InputHelper::is_god_dir(const std::filesystem::path& path) 
{
    return is_god_dir_helper(path, 0, 2);
}

FileType InputHelper::get_filetype(const std::filesystem::path& path) 
{
    if (std::filesystem::is_directory(path)) 
    {
        if (is_extracted_dir(path)) 
        {
            return FileType::DIR;
        } 
        else if (is_god_dir(path)) 
        {
            return FileType::GoD;
        }
    }
    else if (std::filesystem::is_regular_file(path))
    {
        if (has_extension(path, ".iso")) 
        {
            return FileType::ISO;
        } 
        else if (has_extension(path, ".zar")) 
        {
            return FileType::ZAR;
        } 
        else if (has_extension(path, ".cso")) 
        {
            return FileType::CSO;
        } 
        else if (has_extension(path, ".cci")) 
        {
            return FileType::CCI;
        } 
        else if (has_extension(path, ".xbe")) 
        {
            return FileType::XBE;
        }
    }
    return FileType::UNKNOWN;
}

bool InputHelper::is_batch_dir(const std::filesystem::path& path) 
{
    if (!std::filesystem::is_directory(path))
    {
        return false;
    }

    for (const auto& entry : std::filesystem::directory_iterator(path)) 
    {
        if (get_filetype(entry.path()) != FileType::UNKNOWN) 
        {
            return true;
        }
        if (entry.is_directory())
        {
            for (const auto& nested : std::filesystem::directory_iterator(entry.path()))
            {
                if (nested.is_directory() && is_extracted_dir(nested.path()))
                {
                    return true;
                }
            }
        }
    }
    return false;
}

std::vector<std::filesystem::path> InputHelper::find_split_filepaths(const std::filesystem::path& in_filepath) 
{
    std::string extension = in_filepath.extension().string();
    std::filesystem::path stem = in_filepath.stem();
    std::string stem_str = stem.string();

    if (stem_str.size() > 2) 
    {
        std::string subextension = stem_str.substr(stem_str.size() - 2);
        if (subextension == ".1" || subextension == ".2") 
        {
            std::string base_filename = stem_str.substr(0, stem_str.size() - 2);
            std::filesystem::path base_path = in_filepath.parent_path() / base_filename;

            if (subextension == ".1") 
            {
                std::filesystem::path in_filepath1 = in_filepath;
                std::filesystem::path in_filepath2 = base_path.string() + ".2" + extension;
                if (std::filesystem::exists(in_filepath2)) 
                {
                    return { in_filepath1, in_filepath2 };
                }
            } 
            else if (subextension == ".2") 
            {
                std::filesystem::path in_filepath1 = base_path.string() + ".1" + extension;
                std::filesystem::path in_filepath2 = in_filepath;
                if (std::filesystem::exists(in_filepath1)) 
                {
                    return { in_filepath1, in_filepath2 };
                } 
                else 
                {
                    throw XGDException(ErrCode::FS_EXISTS, HERE(), "Missing first part of split file: " + in_filepath.string());
                }
            }
        }
    }
    return { in_filepath };
}

bool InputHelper::is_part_2_file(const std::filesystem::path& path) 
{
    std::string stem_str = path.stem().string();

    if (stem_str.size() > 2 && stem_str.substr(stem_str.size() - 2) == ".2") 
    {
        return true;
    }
    return false;
}

std::filesystem::path InputHelper::get_output_path(const std::filesystem::path& out_directory, TitleHelper& title_helper, const std::filesystem::path& in_path)
{
    std::filesystem::path out_path = out_directory;

    std::string name;
    if (output_settings_.keep_name && !in_path.empty())
    {
        name = in_path.stem().string();
        if (in_path.has_extension())
        {
            std::string ext = in_path.extension().string();
            if (ext == ".1" || ext == ".2" || ext == ".01" || ext == ".02")
            {
                name = in_path.stem().stem().string();
            }
        }
    }
    else
    {
        name = title_helper.iso_name();
    }

    std::string folder = (output_settings_.keep_name && !in_path.empty()) ? name : title_helper.folder_name();

    if (output_settings_.smart_rename)
    {
        uint32_t tid = title_helper.xex_cert().title_id ? title_helper.xex_cert().title_id : title_helper.xbe_cert().title_id;
        std::string tid_str = StringUtils::uint32_to_hex_string(tid);
        std::string clean_title = title_helper.folder_name();
        if (!tid_str.empty() && tid_str != "00000000")
        {
            name = "[" + tid_str + "] " + clean_title;
            folder = name;
        }
    }

    switch (output_settings_.file_type)
    {
        case FileType::DIR:
            out_path /= folder;
            break;
        case FileType::ISO:
            if (!output_settings_.xemu_paths) 
            {
                out_path /= folder;
            } 
            out_path /= name + ".iso";
            break;
        case FileType::CCI:
            out_path /= folder;
            out_path /= name + ".cci";
            break;
        case FileType::CSO:
            out_path /= folder;
            out_path /= name + ".cso";
            break;
        case FileType::ZAR:
            out_path /= name + ".zar";
            break;
        case FileType::XBE:
            out_path /= "default.xbe";
            break;
        case FileType::GoD:
            if (output_settings_.keep_name && !in_path.empty())
            {
                uint32_t tid = title_helper.xex_cert().title_id ? title_helper.xex_cert().title_id : title_helper.xbe_cert().title_id;
                out_path /= name + " [" + StringUtils::uint32_to_hex_string(tid) + "]";
            }
            else
            {
                out_path /= title_helper.god_folder_name();
            }
            break;
        default:
            throw XGDException(ErrCode::MISC, HERE(), "Invalid output file type");
    }
    return out_path;
}

OutputSettings InputHelper::get_auto_output_settings(const AutoFormat auto_format) 
{
    OutputSettings output_settings;

    switch (auto_format) 
    {
        case AutoFormat::OGXBOX:
            output_settings.file_type = FileType::DIR;
            output_settings.allowed_media_patch = true;
            output_settings.rename_xbe = true;
            break;
        case AutoFormat::XBOX360:
            output_settings.file_type = FileType::GoD;
            output_settings.scrub_type = ScrubType::FULL;
            break;
        case AutoFormat::XENIA:
        case AutoFormat::XEMU:
            output_settings.file_type = FileType::ISO;
            output_settings.scrub_type = ScrubType::FULL;
            output_settings.attach_xbe = false;
            output_settings.allowed_media_patch = false;
            output_settings.split = false;
            output_settings.xemu_paths = true;
            break;
        default:
            break;
    }

    return output_settings;
}

void InputHelper::remove_duplicate_infos(std::vector<InputInfo>& input_infos)
{
    if (input_infos.size() < 2)
    {
        return;
    }

    std::sort(input_infos.begin(), input_infos.end(), [](const InputInfo& a, const InputInfo& b) 
    {
        return a.paths.front() < b.paths.front();
    });

    auto it = input_infos.begin();

    while (it != input_infos.end() - 1) 
    {
        if (it->paths.front() == (it + 1)->paths.front()) 
        {
            it = input_infos.erase(it);
        } 
        else 
        {
            ++it;
        }
    }
}