#include <cstdint>
#include <filesystem>

#include "XGD.h"
#include "InputHelper/Types.h"
#include "InputHelper/InputHelper.h" 
#include "Utils/LocalizationManager.h"

#ifndef ENABLE_GUI

#include <CLI/CLI.hpp>

int main(int argc, char** argv)
{
    std::string lang_code;
    for (int i = 1; i < argc; ++i)
    {
        std::string arg = argv[i];
        if (arg == "--lang" || arg == "--language")
        {
            if (i + 1 < argc)
            {
                lang_code = argv[i + 1];
            }
        }
        else if (arg.rfind("--lang=", 0) == 0)
        {
            lang_code = arg.substr(7);
        }
        else if (arg.rfind("--language=", 0) == 0)
        {
            lang_code = arg.substr(11);
        }
    }

    LocalizationManager::instance().init(lang_code);

    CLI::App app{"XGDTool"};
    argv = app.ensure_utf8(argv);

    std::filesystem::path in_path;
    std::filesystem::path out_directory;
    OutputSettings output_settings;
    AutoFormat auto_format = AutoFormat::NONE;

    auto* output_format_group = app.add_option_group(I18n::get("cli_group_output_format"))->require_option(1);

    output_format_group->add_flag_function("--extract",  [&](int64_t) { output_settings.file_type = FileType::DIR; }, I18n::get("cli_flag_extract"));
    output_format_group->add_flag_function("--xiso",     [&](int64_t) { output_settings.file_type = FileType::ISO; }, I18n::get("cli_flag_xiso"));
    output_format_group->add_flag_function("--god",      [&](int64_t) { output_settings.file_type = FileType::GoD; }, I18n::get("cli_flag_god"));
    output_format_group->add_flag_function("--cci",      [&](int64_t) { output_settings.file_type = FileType::CCI; }, I18n::get("cli_flag_cci"));
    output_format_group->add_flag_function("--cso",      [&](int64_t) { output_settings.file_type = FileType::CSO; }, I18n::get("cli_flag_cso"));
    output_format_group->add_flag_function("--zar",      [&](int64_t) { output_settings.file_type = FileType::ZAR; }, I18n::get("cli_flag_zar"));
    output_format_group->add_flag_function("--xbe",      [&](int64_t) { output_settings.file_type = FileType::XBE; }, I18n::get("cli_flag_xbe"));

    output_format_group->add_flag_function("--ogxbox",   [&](int64_t) { output_settings.auto_format = AutoFormat::OGXBOX;  }, I18n::get("cli_flag_ogxbox"));
    output_format_group->add_flag_function("--xbox360",  [&](int64_t) { output_settings.auto_format = AutoFormat::XBOX360; }, I18n::get("cli_flag_xbox360"));
    output_format_group->add_flag_function("--xemu",     [&](int64_t) { output_settings.auto_format = AutoFormat::XEMU;    }, I18n::get("cli_flag_xemu"));
    output_format_group->add_flag_function("--xenia",    [&](int64_t) { output_settings.auto_format = AutoFormat::XENIA;   }, I18n::get("cli_flag_xenia"));

    output_format_group->add_flag_function("--list",     [&](int64_t) { output_settings.file_type = FileType::LIST; }, I18n::get("cli_flag_list"));
    output_format_group->set_help_flag    ("--help",     I18n::get("cli_flag_help"));
    output_format_group->set_version_flag ("--version",  XGD::VERSION);

    auto* settings_group = app.add_option_group(I18n::get("cli_group_settings"));

    settings_group->add_flag_function("--partial-scrub", [&](int64_t) { output_settings.scrub_type = ScrubType::PARTIAL; }, I18n::get("cli_flag_partial_scrub"));
    settings_group->add_flag_function("--full-scrub",    [&](int64_t) { output_settings.scrub_type = ScrubType::FULL;    }, I18n::get("cli_flag_full_scrub"));
    settings_group->add_flag_function("--split",         [&](int64_t) { output_settings.split = true;                    }, I18n::get("cli_flag_split"));
    settings_group->add_flag_function("--rename",        [&](int64_t) { output_settings.rename_xbe = true;               }, I18n::get("cli_flag_rename"));
    settings_group->add_flag_function("--attach-xbe",    [&](int64_t) { output_settings.attach_xbe = true;               }, I18n::get("cli_flag_attach_xbe"));
    settings_group->add_flag_function("--am-patch",      [&](int64_t) { output_settings.allowed_media_patch = true;      }, I18n::get("cli_flag_am_patch"));
    settings_group->add_flag_function("--offline",       [&](int64_t) { output_settings.offline_mode = true;             }, I18n::get("cli_flag_offline"));
    settings_group->add_flag_function("--keep-name",     [&](int64_t) { output_settings.keep_name = true;                }, I18n::get("cli_flag_keep_name"));
    settings_group->add_option       ("--lang,--language", lang_code,                                                        I18n::get("cli_flag_lang"));
    settings_group->add_flag_function("--debug",         [&](int64_t) { XGDLog().set_log_level(LogLevel::Debug);         }, I18n::get("cli_flag_debug"));
    settings_group->add_flag_function("--quiet",         [&](int64_t) { XGDLog().set_log_level(LogLevel::Error);         }, I18n::get("cli_flag_quiet"));

    app.add_option("input_path", in_path, I18n::get("cli_opt_input_path"))->required();
    app.add_option("output_directory", out_directory, I18n::get("cli_opt_output_dir"));

    CLI11_PARSE(app, argc, argv);

    if (!std::filesystem::exists(in_path)) 
    {
        XGDLog(Error) << I18n::format("cli_msg_input_not_exist", in_path.string()) << XGDLog::Endl;
        return 1;
    }

    InputHelper input_helper(std::filesystem::absolute(in_path), out_directory, output_settings);
    input_helper.process_all();

    for (const auto& failed_input : input_helper.failed_inputs()) 
    {
        XGDLog(Error) << I18n::format("cli_msg_failed_input", failed_input.string()) << "\n";
    }

    XGDLog() << I18n::get("cli_msg_finished") << XGDLog::Endl;

    return 0;
}

#else // ENABLE_GUI

#include <wx/wx.h>

#include "GUI/MainFrame.h"
#include "Utils/LocalizationManager.h"

class AppEntry : public wxApp
{
public:
    AppEntry() {};
    ~AppEntry() {};

    virtual bool OnInit();

private:
    MainFrame* frame_{nullptr};
};

bool AppEntry::OnInit()
{
    frame_ = new MainFrame(XGD::NAME, wxPoint(50, 50), wxSize(900, 620));
    SetTopWindow(frame_);
    LocalizationManager::instance().init();
    frame_->update_ui_language();
    frame_->Show();
    return true;
}

wxIMPLEMENT_APP(AppEntry);

#endif // ENABLE_GUI