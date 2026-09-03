#include "LocalizationManager.h"
#include "EmbeddedLanguages.h"
#ifndef ANDROID_BUILD
#include <wx/xml/xml.h>
#include <wx/sstream.h>
#include <wx/stdpaths.h>
#include <wx/filename.h>
#include <wx/intl.h>
#endif
#include <filesystem>
#include "XGDLog.h"

namespace fs = std::filesystem;

LocalizationManager& LocalizationManager::instance()
{
    static LocalizationManager inst;
    return inst;
}

LocalizationManager::LocalizationManager()
{
    load_default_fallback_strings();
}

void LocalizationManager::load_default_fallback_strings()
{
    // Embedded fallback strings (English)
    strings_["app_name"] = "XGDTool";
    strings_["notification_title"] = "XGDTool - Processing Complete";
    strings_["batch_completed_all"] = "Conversion complete: {0} of {1} succeeded";
    strings_["batch_completed_with_errors"] = "Conversion complete: {0} of {1} succeeded, {2} failed";
    strings_["dialog_title_success"] = "Processing Complete";
    strings_["dialog_title_warning"] = "Processing Completed with Errors";
    strings_["dialog_msg_all_ok"] = "All {0} files have been successfully processed!";
    strings_["dialog_msg_single_ok"] = "File successfully processed!";
    strings_["dialog_msg_errors"] = "Processing finished with errors:\n\n• Succeeded: {0}\n• Failed: {1}\n\nCheck the log file for detailed diagnostics.";
    strings_["dialog_msg_cancelled"] = "Processing cancelled by user.\n\n• Succeeded: {0}\n• Incomplete/Failed: {1}";
    strings_["btn_open_log"] = "Open Log File";
    strings_["btn_close"] = "Close";
    strings_["btn_ok"] = "OK";

    strings_["label_input_path"] = "Input Path:";
    strings_["label_output_dir"] = "Output Directory:";
    strings_["label_file_list"] = "File List:";
    strings_["btn_browse"] = "Browse";
    strings_["col_format"] = "Format";
    strings_["col_filename"] = "Filename";
    strings_["label_status"] = "Status:";
    strings_["label_current_progress"] = "Current Progress:";
    strings_["label_total_progress"] = "Total Progress:";

    strings_["section_output_format"] = "Output Format:";
    strings_["section_scrub"] = "Scrub:";
    strings_["section_settings"] = "Settings:";
    strings_["section_language"] = "Language:";

    strings_["scrub_none"] = "None";
    strings_["scrub_partial"] = "Partial";
    strings_["scrub_full"] = "Full";

    strings_["setting_split"] = "Split XISO";
    strings_["setting_attach_xbe"] = "Generate Attach XBE";
    strings_["setting_am_patch"] = "Allowed Media XBE Patch";
    strings_["setting_rename_xbe"] = "Rename XBE Title";
    strings_["setting_offline_mode"] = "Offline Mode";
    strings_["setting_keep_name"] = "Keep Original Name";

    strings_["lang_system"] = "System";
    strings_["lang_english"] = "English";
    strings_["lang_italian"] = "Italiano";
    strings_["lang_german"] = "Deutsch";
    strings_["lang_french"] = "Français";
    strings_["lang_spanish"] = "Español";
    strings_["lang_portuguese"] = "Português";

    strings_["btn_process_all"] = "Process All";
    strings_["btn_pause"] = "Pause";
    strings_["btn_resume"] = "Resume";
    strings_["btn_cancel"] = "Cancel";

    strings_["status_idle"] = "Idle";
    strings_["status_paused"] = "Paused";
    strings_["status_processing"] = "Processing input files";
    strings_["status_complete"] = "Processing complete";
    strings_["status_cancelled"] = "Processing cancelled";

    strings_["choose_selection_type_title"] = "Choose the type of selection:";
    strings_["choose_selection_type_caption"] = "Select";
    strings_["choice_select_files"] = "Select File(s)";
    strings_["choice_select_dir"] = "Select Directory";
    strings_["dialog_select_files_title"] = "Select file(s)";
    strings_["dialog_select_dir_title"] = "Select a directory";
    strings_["dialog_select_out_dir_title"] = "Select a GoD/Game/Batch directory";
    strings_["wildcard_xbox_images"] = "Xbox image files (*.iso;*.cci;*.cso;*.zar)|*.iso;*.cci;*.cso;*.zar|All files (*.*)|*.*";
    strings_["msg_no_input_files"] = "No input files selected";
    strings_["msg_no_output_dir"] = "No output directory selected";
    strings_["msg_no_valid_files"] = "No valid files found in selected input path";

    strings_["tooltip_browse_input"] = "Select the input file or directory to process";
    strings_["tooltip_browse_output"] = "Select the output directory to save the processed files";
    strings_["tooltip_fmt_iso"] = "Creates an XISO image";
    strings_["tooltip_fmt_god"] = "Creates a Games on Demand image";
    strings_["tooltip_fmt_cci"] = "Creates a CCI archive";
    strings_["tooltip_fmt_cso"] = "Creates a CSO archive";
    strings_["tooltip_fmt_zar"] = "Creates a ZAR archive";
    strings_["tooltip_fmt_extract"] = "Extracts all files to a directory";
    strings_["tooltip_auto_ogxbox"] = "Automatically choose format and settings for use with OG Xbox";
    strings_["tooltip_auto_xbox360"] = "Automatically choose format and settings for use with Xbox 360";
    strings_["tooltip_auto_xemu"] = "Automatically choose format and settings for use with Xemu";
    strings_["tooltip_auto_xenia"] = "Automatically choose format and settings for use with Xenia";
    strings_["tooltip_scrub_none"] = "No scrubbing, only video partion is removed if present";
    strings_["tooltip_scrub_partial"] = "Scrubs and trims the output image, random padding data is removed";
    strings_["tooltip_scrub_full"] = "Completely reauthor the resulting image, this will produce the smallest file possible";
    strings_["tooltip_split"] = "Splits the resulting XISO file if it's too large for OG Xbox";
    strings_["tooltip_attach_xbe"] = "Generates an attach XBE file along with the output file";
    strings_["tooltip_am_patch"] = "Patches the Allowed Media field in resulting XBE files";
    strings_["tooltip_rename_xbe"] = "Replaces the title field of resulting XBE files with one found in the database";
    strings_["tooltip_offline_mode"] = "Disables online functionality, will result in less accurate file naming";
    strings_["tooltip_keep_name"] = "Keeps the original input filename for output files, preventing overwrites for multi-disc games";
    strings_["tooltip_lang_system"] = "Use system default language";
    strings_["tooltip_lang_english"] = "Set UI language to English";
    strings_["tooltip_lang_italian"] = "Set UI language to Italian";
    strings_["tooltip_lang_german"] = "Set UI language to German";
    strings_["tooltip_lang_french"] = "Set UI language to French";
    strings_["tooltip_lang_spanish"] = "Set UI language to Spanish";
    strings_["tooltip_lang_portuguese"] = "Set UI language to Portuguese";
    strings_["tooltip_process_all"] = "Process all files in the File List";
    strings_["tooltip_pause"] = "Pause processing of files";
    strings_["tooltip_cancel"] = "Processing will stop after the current file is finished";

    strings_["cli_opt_input_path"] = "Input path";
    strings_["cli_opt_output_dir"] = "Output directory";
    strings_["cli_group_output_format"] = "Output Format";
    strings_["cli_group_settings"] = "Settings";
    strings_["cli_flag_extract"] = "Extracts all files to a directory";
    strings_["cli_flag_xiso"] = "Creates an XISO image";
    strings_["cli_flag_god"] = "Creates a Games on Demand image";
    strings_["cli_flag_cci"] = "Creates a CCI archive";
    strings_["cli_flag_cso"] = "Creates a CSO archive";
    strings_["cli_flag_zar"] = "Creates a ZAR archive";
    strings_["cli_flag_xbe"] = "Generates an attach XBE file";
    strings_["cli_flag_ogxbox"] = "Automatically choose format and settings for use with OG Xbox";
    strings_["cli_flag_xbox360"] = "Automatically choose format and settings for use with Xbox 360";
    strings_["cli_flag_xemu"] = "Automatically choose format and settings for use with Xemu";
    strings_["cli_flag_xenia"] = "Automatically choose format and settings for use with Xenia";
    strings_["cli_flag_list"] = "List file contents of input image";
    strings_["cli_flag_help"] = "Print this help message and exit";
    strings_["cli_flag_partial_scrub"] = "Scrubs and trims the output image, random padding data is removed";
    strings_["cli_flag_full_scrub"] = "Completely reauthor the resulting image, this will produce the smallest file possible";
    strings_["cli_flag_split"] = "Splits the resulting XISO file if it's too large for OG Xbox";
    strings_["cli_flag_rename"] = "Patches the title field of resulting XBE files to one found in the database";
    strings_["cli_flag_attach_xbe"] = "Generates an attach XBE file along with the output file";
    strings_["cli_flag_am_patch"] = "Patches the Allowed Media field in resulting XBE files";
    strings_["cli_flag_offline"] = "Disables online functionality, will result in less accurate file naming";
    strings_["cli_flag_keep_name"] = "Keep original input filename for output instead of database title lookup";
    strings_["cli_flag_lang"] = "Set interface language (e.g. 'it', 'en', 'system')";
    strings_["cli_flag_debug"] = "Enable debug logging";
    strings_["cli_flag_quiet"] = "Disable all logging except for warnings and errors";

    strings_["cli_msg_input_not_exist"] = "Input path does not exist: {0}";
    strings_["cli_msg_failed_input"] = "Failed to process input: {0}";
    strings_["cli_msg_finished"] = "Finished processing input files.";
    strings_["cli_msg_processing"] = "Processing: {0}";
    strings_["cli_msg_success_created"] = "Successfully created: {0}";
    strings_["cli_msg_files_in_image"] = "Files in image:";
}

#ifdef _WIN32
#include <windows.h>
#include <winnls.h>
#endif

#ifndef ANDROID_BUILD
static std::string detect_system_language()
{
#ifdef _WIN32
    auto check_langid = [](LANGID id) -> std::string {
        WORD primary = PRIMARYLANGID(id);
        if (primary == LANG_ITALIAN) return "it";
        if (primary == LANG_GERMAN) return "de";
        if (primary == LANG_FRENCH) return "fr";
        if (primary == LANG_SPANISH) return "es";
        if (primary == LANG_PORTUGUESE) return "pt";
        return "";
    };

    std::string l = check_langid(GetUserDefaultUILanguage());
    if (!l.empty()) return l;

    l = check_langid(GetSystemDefaultUILanguage());
    if (!l.empty()) return l;

    l = check_langid(GetUserDefaultLCID());
    if (!l.empty()) return l;

    l = check_langid(GetSystemDefaultLCID());
    if (!l.empty()) return l;

    auto check_name = [](const wchar_t* name) -> std::string {
        if (_wcsnicmp(name, L"it", 2) == 0) return "it";
        if (_wcsnicmp(name, L"de", 2) == 0) return "de";
        if (_wcsnicmp(name, L"fr", 2) == 0) return "fr";
        if (_wcsnicmp(name, L"es", 2) == 0) return "es";
        if (_wcsnicmp(name, L"pt", 2) == 0) return "pt";
        return "";
    };

    wchar_t locale_name[LOCALE_NAME_MAX_LENGTH] = {0};
    if (GetUserDefaultLocaleName(locale_name, LOCALE_NAME_MAX_LENGTH) > 0)
    {
        l = check_name(locale_name);
        if (!l.empty()) return l;
    }

    wchar_t sys_locale_name[LOCALE_NAME_MAX_LENGTH] = {0};
    if (GetSystemDefaultLocaleName(sys_locale_name, LOCALE_NAME_MAX_LENGTH) > 0)
    {
        l = check_name(sys_locale_name);
        if (!l.empty()) return l;
    }
#endif

    int sys_lang = wxLocale::GetSystemLanguage();
    if (sys_lang == wxLANGUAGE_ITALIAN || sys_lang == wxLANGUAGE_ITALIAN_SWISS) return "it";
    if (sys_lang == wxLANGUAGE_GERMAN || sys_lang == wxLANGUAGE_GERMAN_AUSTRIAN || sys_lang == wxLANGUAGE_GERMAN_SWISS) return "de";
    if (sys_lang == wxLANGUAGE_FRENCH || sys_lang == wxLANGUAGE_FRENCH_BELGIAN || sys_lang == wxLANGUAGE_FRENCH_CANADIAN || sys_lang == wxLANGUAGE_FRENCH_SWISS) return "fr";
    if (sys_lang == wxLANGUAGE_SPANISH || sys_lang == wxLANGUAGE_SPANISH_MODERN) return "es";
    if (sys_lang == wxLANGUAGE_PORTUGUESE || sys_lang == wxLANGUAGE_PORTUGUESE_BRAZILIAN) return "pt";

    wxString canon = wxLocale::GetLanguageCanonicalName(sys_lang).Lower();
    if (canon.StartsWith("it")) return "it";
    if (canon.StartsWith("de")) return "de";
    if (canon.StartsWith("fr")) return "fr";
    if (canon.StartsWith("es")) return "es";
    if (canon.StartsWith("pt")) return "pt";

    auto check_env = [](const char* env_var) -> std::string {
        const char* val = getenv(env_var);
        if (val) {
            if (_strnicmp(val, "it", 2) == 0) return "it";
            if (_strnicmp(val, "de", 2) == 0) return "de";
            if (_strnicmp(val, "fr", 2) == 0) return "fr";
            if (_strnicmp(val, "es", 2) == 0) return "es";
            if (_strnicmp(val, "pt", 2) == 0) return "pt";
        }
        return "";
    };

    l = check_env("LANG");
    if (!l.empty()) return l;
    l = check_env("LC_ALL");
    if (!l.empty()) return l;
    l = check_env("LC_MESSAGES");
    if (!l.empty()) return l;

    return "en";
}

static bool parse_xml_node(wxXmlNode* root, std::unordered_map<std::string, std::string>& strings)
{
    if (!root || root->GetName() != "resources")
    {
        return false;
    }

    wxXmlNode* child = root->GetChildren();
    while (child)
    {
        if (child->GetName() == "string")
        {
            wxString name_attr = child->GetAttribute("name", "");
            if (!name_attr.empty())
            {
                wxString content = child->GetNodeContent();
                // Replace escaped \n with actual newlines
                content.Replace("\\n", "\n");
                strings[std::string(name_attr.utf8_str())] = std::string(content.utf8_str());
            }
        }
        child = child->GetNext();
    }

    return true;
}

bool LocalizationManager::load_from_string(std::string_view xml_content)
{
    if (xml_content.empty())
    {
        return false;
    }

    wxString str = wxString::FromUTF8(xml_content.data(), xml_content.size());
    wxStringInputStream stream(str);
    wxXmlDocument doc;
    if (!doc.Load(stream))
    {
        return false;
    }

    return parse_xml_node(doc.GetRoot(), strings_);
}

bool LocalizationManager::load_from_file(const std::string& xml_file_path)
{
    wxXmlDocument doc;
    if (!doc.Load(wxString::FromUTF8(xml_file_path)))
    {
        return false;
    }

    return parse_xml_node(doc.GetRoot(), strings_);
}

void LocalizationManager::init(const std::string& preferred_lang)
{
    std::string lang = preferred_lang;
    if (lang.empty() || lang == "system" || lang == "System" || lang == "default")
    {
        lang = detect_system_language();
    }

    current_lang_ = lang;
    load_default_fallback_strings();

    // 1. Load embedded XML compiled directly into the binary
    std::string_view embedded_xml = EmbeddedLanguages::get(lang);
    if (!embedded_xml.empty())
    {
        load_from_string(embedded_xml);
    }
    else
    {
        // Fallback to embedded English if requested language is not recognized
        load_from_string(EmbeddedLanguages::XML_EN);
    }

    // 2. Search disk for custom external languages/<lang>.xml overrides
    std::vector<std::string> candidate_paths;

    wxString exe_path = wxStandardPaths::Get().GetExecutablePath();
    wxFileName fn(exe_path);
    std::string exe_dir = fn.GetPath().ToStdString();
    candidate_paths.push_back(exe_dir + "/languages/" + lang + ".xml");
    candidate_paths.push_back(exe_dir + "/../languages/" + lang + ".xml");
    candidate_paths.push_back(exe_dir + "/../../languages/" + lang + ".xml");
    candidate_paths.push_back("languages/" + lang + ".xml");
    candidate_paths.push_back("../languages/" + lang + ".xml");

    for (const auto& path : candidate_paths)
    {
        if (fs::exists(path))
        {
            if (load_from_file(path))
            {
                XGDLog(Debug) << "Loaded external localization override from: " << path << XGDLog::Endl;
                break;
            }
        }
    }

    XGDLog(Normal) << "Language initialized: '" << lang << "' (preferred='" << preferred_lang << "')" << XGDLog::Endl;
}
#else
bool LocalizationManager::load_from_string(std::string_view) { return true; }
bool LocalizationManager::load_from_file(const std::string&) { return true; }
void LocalizationManager::init(const std::string& preferred_lang)
{
    current_lang_ = preferred_lang.empty() ? "en" : preferred_lang;
    load_default_fallback_strings();
}
#endif

std::string LocalizationManager::format_string(const std::string& template_str, const std::vector<std::string>& args) const
{
    std::string result = template_str;
    for (size_t i = 0; i < args.size(); ++i)
    {
        std::string placeholder = "{" + std::to_string(i) + "}";
        size_t pos = 0;
        while ((pos = result.find(placeholder, pos)) != std::string::npos)
        {
            result.replace(pos, placeholder.length(), args[i]);
            pos += args[i].length();
        }
    }
    return result;
}

std::string LocalizationManager::get(const std::string& key, const std::vector<std::string>& args) const
{
    auto it = strings_.find(key);
    if (it != strings_.end())
    {
        if (args.empty())
        {
            return it->second;
        }
        return format_string(it->second, args);
    }
    return key;
}
