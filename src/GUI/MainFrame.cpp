#include <chrono>
#include <wx/notifmsg.h>

#include "GUI/MainFrame.h"
#include "GUI/CompletionDialog.h"
#include "Utils/LocalizationManager.h"

wxDEFINE_EVENT(wxEVT_UPDATE_CURRENT_PROGRESS, wxThreadEvent);
wxDEFINE_EVENT(wxEVT_UPDATE_TOTAL_PROGRESS, wxThreadEvent);
wxDEFINE_EVENT(wxEVT_THREAD_COMPLETED, wxThreadEvent);
wxDEFINE_EVENT(wxEVT_UPDATE_CURRENT_STAGE, wxThreadEvent);

wxBEGIN_EVENT_TABLE(MainFrame, wxFrame)
    EVT_BUTTON(wxID_ANY, MainFrame::on_process_all)
    EVT_BUTTON(wxID_ANY, MainFrame::on_cancel_process)
    EVT_THREAD(wxEVT_UPDATE_CURRENT_PROGRESS, MainFrame::on_update_current_progress)
    EVT_THREAD(wxEVT_UPDATE_TOTAL_PROGRESS, MainFrame::on_update_total_progress)
    EVT_THREAD(wxEVT_THREAD_COMPLETED, MainFrame::on_thread_completed)
    EVT_THREAD(wxEVT_UPDATE_CURRENT_STAGE, MainFrame::on_update_current_stage)
wxEND_EVENT_TABLE()

wxGauge* MainFrame::current_progress_bar_ = nullptr;

MainFrame::MainFrame(const wxString& title, const wxPoint& pos, const wxSize& size)
    : wxFrame(nullptr, wxID_ANY, title, pos, size) 
{
    create_frame();
    update_button_states();
    update_controls_state();

    Bind(wxEVT_UPDATE_CURRENT_PROGRESS, &MainFrame::on_update_current_progress, this);
    Bind(wxEVT_UPDATE_TOTAL_PROGRESS, &MainFrame::on_update_total_progress, this);
    Bind(wxEVT_THREAD_COMPLETED, &MainFrame::on_thread_completed, this);
    Bind(wxEVT_UPDATE_CURRENT_STAGE, &MainFrame::on_update_current_stage, this);
}

void MainFrame::stop_all_processing()
{
    if (current_status_ != Status::IDLE && input_helper_) 
    {
        input_helper_->cancel_processing();
    }

    if (processing_thread_ && processing_thread_->joinable())
    {
        processing_thread_->join();
        processing_thread_.reset();
    }
}

MainFrame::~MainFrame()
{
    stop_all_processing();
}

void MainFrame::on_pause_process(wxCommandEvent& event)
{
    current_status_ = current_status_ == Status::PAUSED ? Status::PROCESSING : Status::PAUSED;
    process_buttons_.pause->SetLabel(current_status_ == Status::PAUSED ? wxString::FromUTF8(I18n::get("btn_resume")) : wxString::FromUTF8(I18n::get("btn_pause")));

    if (current_status_ == Status::PAUSED)
    {
        stored_status_ = status_field_->GetValue().ToStdString();
        status_field_->ChangeValue(wxString::FromUTF8(I18n::get("status_paused")));

        if (input_helper_)
        {
            input_helper_->pause_processing();
        }
    }
    else
    {
        if (status_field_->GetValue().ToStdString() == "Paused") // status field was not updated by the processing thread
        {
            status_field_->ChangeValue(stored_status_);
        }

        if (input_helper_)
        {
            input_helper_->resume_processing();
        }
    }
}

void MainFrame::on_cancel_process(wxCommandEvent& event)
{
    if (current_status_ == Status::PAUSED)
    {
        input_helper_->resume_processing();
    }
    else if (current_status_ != Status::PROCESSING)
    {
        return;
    }

    current_status_ = Status::CANCELED;
    
    if (input_helper_)
    {
        input_helper_->cancel_processing();
    }
}

void MainFrame::on_process_all(wxCommandEvent& event)
{
    if (input_paths_.empty())
    {
        wxLogMessage(wxString::FromUTF8(I18n::get("msg_no_input_files")));
        return;
    }

    if (output_path_.empty())
    {
        wxLogMessage(wxString::FromUTF8(I18n::get("msg_no_output_dir")));
        return;
    }

    OutputSettings settings = parse_ui_settings();
    input_helper_ = std::make_unique<InputHelper>(input_paths_, output_path_, settings);

    if (input_helper_->input_infos().empty())
    {
        wxLogMessage(wxString::FromUTF8(I18n::get("msg_no_valid_files")));
        input_helper_.reset();
        return;
    }

    log_output_settings(settings);

    status_field_->ChangeValue(wxString::FromUTF8(I18n::get("status_processing")));

    total_files_count_ = input_helper_->input_infos().size();
    current_file_index_ = 0;
    total_progress_bar_->SetRange(1000);
    total_progress_bar_->SetValue(0);

    current_status_ = Status::PROCESSING;
    update_button_states();
    
    processing_thread_ = std::make_unique<std::thread>(&MainFrame::process_files, this);
}

void MainFrame::process_files()
{
    for (const auto& input_info : input_helper_->input_infos())
    {
        input_helper_->process_single(input_info);
        current_file_index_++;

        double overall_ratio = (total_files_count_ > 0) ? (static_cast<double>(current_file_index_.load()) / static_cast<double>(total_files_count_.load())) : 1.0;
        wxThreadEvent* total_progress_event = new wxThreadEvent(wxEVT_UPDATE_TOTAL_PROGRESS);
        total_progress_event->SetPayload(std::make_pair(static_cast<uint64_t>(overall_ratio * 1000.0), static_cast<uint64_t>(1000)));
        wxQueueEvent(this, total_progress_event);
    }

    wxThreadEvent* thread_completed_event = new wxThreadEvent(wxEVT_THREAD_COMPLETED);
    wxQueueEvent(this, thread_completed_event);
}

void MainFrame::on_thread_completed(wxThreadEvent& event)
{
    if (processing_thread_ && processing_thread_->joinable())
    {
        processing_thread_->join();
        processing_thread_.reset();
    }

    status_field_->ChangeValue(wxString::FromUTF8(I18n::get("status_complete")));

    uint64_t total = input_helper_ ? input_helper_->input_infos().size() : 0;
    uint64_t failed = input_helper_ ? input_helper_->failed_inputs().size() : 0;
    uint64_t succeeded = (total >= failed) ? (total - failed) : 0;
    bool was_cancelled = (current_status_ == Status::CANCELED);

    if (failed > 0)
    {
        for (const auto& failed_input : input_helper_->failed_inputs())
        {
            wxLogMessage("Failed to process input: " + wxString(failed_input.string()));
        }

        if (was_cancelled)
        {
            status_field_->ChangeValue(wxString::FromUTF8(I18n::get("status_cancelled")));
        }
    }

    current_status_ = Status::IDLE;

    update_button_states();
    update_current_progress_bar(100, 100);

    total_progress_bar_->SetRange(1000);
    total_progress_bar_->SetValue(1000);

    input_helper_.reset();

    // Prepare Notification & Dialog strings from Localization
    std::string toast_msg;
    std::string dialog_title;
    std::string dialog_msg;
    bool has_errors = (failed > 0);

    if (was_cancelled)
    {
        toast_msg = I18n::get("dialog_msg_cancelled", {std::to_string(succeeded), std::to_string(failed)});
        dialog_title = I18n::get("dialog_title_warning");
        dialog_msg = toast_msg;
    }
    else if (has_errors)
    {
        toast_msg = I18n::get("batch_completed_with_errors", {std::to_string(succeeded), std::to_string(total), std::to_string(failed)});
        dialog_title = I18n::get("dialog_title_warning");
        dialog_msg = I18n::get("dialog_msg_errors", {std::to_string(succeeded), std::to_string(failed)});
    }
    else
    {
        if (total <= 1)
        {
            toast_msg = I18n::get("dialog_msg_single_ok");
            dialog_msg = toast_msg;
        }
        else
        {
            toast_msg = I18n::get("batch_completed_all", {std::to_string(succeeded), std::to_string(total)});
            dialog_msg = I18n::get("dialog_msg_all_ok", {std::to_string(total)});
        }
        dialog_title = I18n::get("dialog_title_success");
    }

    // Windows Toast / Balloon notification in bottom-right corner
    wxNotificationMessage notif(
        wxString::FromUTF8(I18n::get("notification_title")),
        wxString::FromUTF8(toast_msg),
        this,
        has_errors ? wxICON_WARNING : wxICON_INFORMATION
    );
    notif.Show(wxNotificationMessage::Timeout_Auto);

    // Modal Completion Dialog (with 'Open Log' button if errors occurred)
    CompletionDialog dlg(this, dialog_title, dialog_msg, has_errors, "xgdtool.log");
    dlg.ShowModal();
}

void MainFrame::on_pick_input_path(wxCommandEvent& event)
{
    wxString choices[] = { 
        wxString::FromUTF8(I18n::get("choice_select_files")), 
        wxString::FromUTF8(I18n::get("choice_select_dir")) 
    };
    int choice = wxGetSingleChoiceIndex(
        wxString::FromUTF8(I18n::get("choose_selection_type_title")), 
        wxString::FromUTF8(I18n::get("choose_selection_type_caption")), 
        2, choices, this
    );

    file_list_->DeleteAllItems();
    input_picker_.field->Clear();
    input_paths_.clear();
    input_helper_.reset();

    if (choice == 0)
    {
        wxString wildcard = wxString::FromUTF8(I18n::get("wildcard_xbox_images"));
        wxFileDialog open_file_dialog(this, wxString::FromUTF8(I18n::get("dialog_select_files_title")), "", "", wildcard, wxFD_OPEN | wxFD_FILE_MUST_EXIST | wxFD_MULTIPLE);

        if (open_file_dialog.ShowModal() == wxID_OK)
        {
            wxArrayString file_paths;
            open_file_dialog.GetPaths(file_paths);

            for (const auto& file_path : file_paths)
            {
                input_picker_.field->AppendText(file_path + "\n"); 
                input_paths_.push_back(std::filesystem::path(file_path.ToStdString()));
            }
        }
    }
    else if (choice == 1)
    {
        wxDirDialog open_dir_dialog(this, wxString::FromUTF8(I18n::get("dialog_select_dir_title")), "", wxDD_DEFAULT_STYLE | wxDD_DIR_MUST_EXIST);

        if (open_dir_dialog.ShowModal() == wxID_OK)
        {
            wxString dir_path = open_dir_dialog.GetPath();
            input_picker_.field->SetValue(dir_path);
            input_paths_.push_back(dir_path.ToStdString());
        }
    }

    if (input_paths_.empty())
    {
        return;
    }

    input_helper_ = std::make_unique<InputHelper>(input_paths_, "", OutputSettings());

    if (input_helper_->input_infos().empty())
    {
        wxLogMessage(wxString::FromUTF8(I18n::get("msg_no_valid_files")));
        return;
    }

    for (const auto& input_info : input_helper_->input_infos())
    {
        long item_index = file_list_->InsertItem(file_list_->GetItemCount(), get_file_type_string(input_info.file_type));
        file_list_->SetItem(item_index, 1, input_info.paths.front().filename().string());
    }
}

void MainFrame::on_pick_output_path(wxCommandEvent& event)
{
    wxDirDialog open_dir_dialog(this, wxString::FromUTF8(I18n::get("dialog_select_out_dir_title")), "", wxDD_DEFAULT_STYLE | wxDD_DIR_MUST_EXIST);

    if (open_dir_dialog.ShowModal() == wxID_OK)
    {
        wxString dir_path = open_dir_dialog.GetPath();
        output_picker_.field->Clear();
        output_picker_.field->SetValue(dir_path);
        output_path_ = std::filesystem::path(dir_path.ToStdString());
    }
}

void MainFrame::update_progress_bar(uint64_t progress, uint64_t total)
{
    if (wxTheApp && wxTheApp->GetTopWindow())
    {
        wxThreadEvent* event = new wxThreadEvent(wxEVT_UPDATE_CURRENT_PROGRESS);
        event->SetPayload(std::make_pair(progress, total));
        wxQueueEvent(wxTheApp->GetTopWindow(), event);
    }
}

void MainFrame::update_current_progress_bar(uint64_t progress, uint64_t total)
{
    if (!current_progress_bar_) 
    {
        return;
    }

    double percentage = static_cast<double>(progress) / static_cast<double>(total) * 100.0;
    auto gauge_range = current_progress_bar_->GetRange();
    int gauge_value = static_cast<int>(percentage / 100.0 * gauge_range);

    if (gauge_value < 0) 
    {
        gauge_value = 0;
    } 
    else if (gauge_value > gauge_range) 
    {
        gauge_value = gauge_range;
    }

    current_progress_bar_->SetValue(gauge_value);
}

void MainFrame::on_update_current_progress(wxThreadEvent& event)
{
    auto data = event.GetPayload<std::pair<uint64_t, uint64_t>>();
    uint64_t progress = data.first;
    uint64_t total = data.second;

    update_current_progress_bar(progress, total);

    // Update real-time total progress
    if (total_progress_bar_ && total_files_count_ > 0)
    {
        double current_ratio = (total > 0) ? (static_cast<double>(progress) / static_cast<double>(total)) : 0.0;
        if (current_ratio > 1.0) current_ratio = 1.0;
        if (current_ratio < 0.0) current_ratio = 0.0;

        double overall_ratio = (static_cast<double>(current_file_index_.load()) + current_ratio) / static_cast<double>(total_files_count_.load());
        if (overall_ratio > 1.0) overall_ratio = 1.0;
        if (overall_ratio < 0.0) overall_ratio = 0.0;

        int gauge_range = total_progress_bar_->GetRange();
        if (gauge_range <= 0) gauge_range = 1000;
        total_progress_bar_->SetValue(static_cast<int>(overall_ratio * gauge_range));
    }
}

void MainFrame::on_update_total_progress(wxThreadEvent& event)
{
    auto data = event.GetPayload<std::pair<uint64_t, uint64_t>>();
    uint64_t progress = data.first;
    uint64_t total = data.second;

    if (total_progress_bar_) 
    {
        total_progress_bar_->SetRange(static_cast<int>(total));
        total_progress_bar_->SetValue(static_cast<int>(progress));
    }
}

void MainFrame::update_status_field(const std::string status)
{
    if (wxTheApp && wxTheApp->GetTopWindow())
    {
        wxThreadEvent* event = new wxThreadEvent(wxEVT_UPDATE_CURRENT_STAGE);
        event->SetPayload(status);
        wxQueueEvent(wxTheApp->GetTopWindow(), event);
    }
}

void MainFrame::on_update_current_stage(wxThreadEvent& event)
{
    auto data = event.GetPayload<std::string>();
    status_field_->ChangeValue(data);
}

std::string MainFrame::get_file_type_string(FileType type)
{
    switch (type)
    {
        case FileType::ISO:
            return "ISO";
        case FileType::GoD:
            return "GoD";
        case FileType::CCI:
            return "CCI";
        case FileType::CSO:
            return "CSO";
        case FileType::ZAR:
            return "ZAR";
        case FileType::DIR:
            return "DIR";
        case FileType::XBE:
            return "XBE";
        default:
            return "UNKNOWN";
    }
}

void MainFrame::update_controls_state()
{
    bool enable_scrub_options = out_format_rbs_.iso->GetValue() || out_format_rbs_.god->GetValue() || out_format_rbs_.cci->GetValue() || out_format_rbs_.cso->GetValue();
    out_scrub_rbs_.none->Enable(enable_scrub_options);
    out_scrub_rbs_.partial->Enable(enable_scrub_options);
    out_scrub_rbs_.full->Enable(enable_scrub_options);
    if (!enable_scrub_options)
    {
        out_scrub_rbs_.none->SetValue(true);
    }

    bool enable_split_option = out_format_rbs_.iso->GetValue();
    out_settings_cbs_.split->Enable(enable_split_option);
    if (!enable_split_option)
    {
        out_settings_cbs_.split->SetValue(false);
    }

    bool enable_attach_xbe_option = out_format_rbs_.iso->GetValue() || out_format_rbs_.cci->GetValue() || out_format_rbs_.cso->GetValue();
    out_settings_cbs_.attach_xbe->Enable(enable_attach_xbe_option);
    if (!enable_attach_xbe_option)
    {
        out_settings_cbs_.attach_xbe->SetValue(false);
    }

    bool allowed_media_patch = out_format_rbs_.extract->GetValue();
    out_settings_cbs_.allowed_media_xbe->Enable(allowed_media_patch);
    if (!allowed_media_patch)
    {
        out_settings_cbs_.allowed_media_xbe->SetValue(false);
    }

    bool enable_rename_xbe_option = out_format_rbs_.extract->GetValue();
    out_settings_cbs_.rename_xbe->Enable(enable_rename_xbe_option);
    if (!enable_rename_xbe_option)
    {
        out_settings_cbs_.rename_xbe->SetValue(false);
    }
}

void MainFrame::update_button_states()
{
    bool processing = current_status_ == Status::PROCESSING;
    bool paused = current_status_ == Status::PAUSED;

    process_buttons_.pause->SetLabel(!paused ? wxString::FromUTF8(I18n::get("btn_pause")) : wxString::FromUTF8(I18n::get("btn_resume")));

    process_buttons_.process->Enable(!processing);
    process_buttons_.pause->Enable(processing);    
    process_buttons_.cancel->Enable(processing);
    
    input_picker_.button->Enable(!processing);
    output_picker_.button->Enable(!processing);

    out_format_rbs_.iso->Enable(!processing);
    out_format_rbs_.god->Enable(!processing);
    out_format_rbs_.cci->Enable(!processing);
    out_format_rbs_.cso->Enable(!processing);
    out_format_rbs_.zar->Enable(!processing);
    out_format_rbs_.extract->Enable(!processing);

    auto_format_rbs_.ogxbox->Enable(!processing);
    auto_format_rbs_.xbox360->Enable(!processing);
    auto_format_rbs_.xemu->Enable(!processing);
    auto_format_rbs_.xenia->Enable(!processing);

    out_scrub_rbs_.none->Enable(!processing);
    out_scrub_rbs_.partial->Enable(!processing);
    out_scrub_rbs_.full->Enable(!processing);

    out_settings_cbs_.split->Enable(!processing);
    out_settings_cbs_.attach_xbe->Enable(!processing);
    out_settings_cbs_.allowed_media_xbe->Enable(!processing);
    out_settings_cbs_.rename_xbe->Enable(!processing);
    out_settings_cbs_.offline_mode->Enable(!processing);
    out_settings_cbs_.keep_name->Enable(!processing);

    language_rbs_.system->Enable(!processing);
    language_rbs_.english->Enable(!processing);
    language_rbs_.italian->Enable(!processing);
}

OutputSettings MainFrame::parse_ui_settings()
{
    OutputSettings output_settings;

    if (out_format_rbs_.iso->GetValue())
    {
        output_settings.file_type = FileType::ISO;
    }
    else if (out_format_rbs_.god->GetValue())
    {
        output_settings.file_type = FileType::GoD;
    }
    else if (out_format_rbs_.cci->GetValue())
    {
        output_settings.file_type = FileType::CCI;
    }
    else if (out_format_rbs_.cso->GetValue())
    {
        output_settings.file_type = FileType::CSO;
    }
    else if (out_format_rbs_.zar->GetValue())
    {
        output_settings.file_type = FileType::ZAR;
    }
    else if (out_format_rbs_.extract->GetValue())
    {
        output_settings.file_type = FileType::DIR;
    }
    else if (auto_format_rbs_.ogxbox->GetValue())
    {
        output_settings.auto_format = AutoFormat::OGXBOX;
    }
    else if (auto_format_rbs_.xbox360->GetValue())
    {
        output_settings.auto_format = AutoFormat::XBOX360;
    }
    else if (auto_format_rbs_.xemu->GetValue())
    {
        output_settings.auto_format = AutoFormat::XEMU;
    }
    else if (auto_format_rbs_.xenia->GetValue())
    {
        output_settings.auto_format = AutoFormat::XENIA;
    }

    if (out_scrub_rbs_.none->GetValue())
    {
        output_settings.scrub_type = ScrubType::NONE;
    }
    else if (out_scrub_rbs_.partial->GetValue())
    {
        output_settings.scrub_type = ScrubType::PARTIAL;
    }
    else if (out_scrub_rbs_.full->GetValue())
    {
        output_settings.scrub_type = ScrubType::FULL;
    }

    output_settings.split = out_settings_cbs_.split->GetValue();
    output_settings.attach_xbe = out_settings_cbs_.attach_xbe->GetValue();
    output_settings.allowed_media_patch = out_settings_cbs_.allowed_media_xbe->GetValue();
    output_settings.rename_xbe = out_settings_cbs_.rename_xbe->GetValue();
    output_settings.offline_mode = out_settings_cbs_.offline_mode->GetValue();
    output_settings.keep_name = out_settings_cbs_.keep_name->GetValue();

    return output_settings;
}

void MainFrame::on_language_selected(const std::string& lang_code)
{
    LocalizationManager::instance().init(lang_code);
    update_ui_language();
}

void MainFrame::update_ui_language()
{
    if (ui_labels_.input_path) ui_labels_.input_path->SetLabel(wxString::FromUTF8(I18n::get("label_input_path")));
    if (ui_labels_.output_dir) ui_labels_.output_dir->SetLabel(wxString::FromUTF8(I18n::get("label_output_dir")));
    if (ui_labels_.file_list) ui_labels_.file_list->SetLabel(wxString::FromUTF8(I18n::get("label_file_list")));
    if (ui_labels_.status) ui_labels_.status->SetLabel(wxString::FromUTF8(I18n::get("label_status")));
    if (ui_labels_.current_progress) ui_labels_.current_progress->SetLabel(wxString::FromUTF8(I18n::get("label_current_progress")));
    if (ui_labels_.total_progress) ui_labels_.total_progress->SetLabel(wxString::FromUTF8(I18n::get("label_total_progress")));
    if (ui_labels_.out_format) ui_labels_.out_format->SetLabel(wxString::FromUTF8(I18n::get("section_output_format")));
    if (ui_labels_.scrub) ui_labels_.scrub->SetLabel(wxString::FromUTF8(I18n::get("section_scrub")));
    if (ui_labels_.settings) ui_labels_.settings->SetLabel(wxString::FromUTF8(I18n::get("section_settings")));
    if (ui_labels_.language) ui_labels_.language->SetLabel(wxString::FromUTF8(I18n::get("section_language")));

    if (input_picker_.button) {
        input_picker_.button->SetLabel(wxString::FromUTF8(I18n::get("btn_browse")));
        input_picker_.button->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_browse_input")));
    }
    if (output_picker_.button) {
        output_picker_.button->SetLabel(wxString::FromUTF8(I18n::get("btn_browse")));
        output_picker_.button->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_browse_output")));
    }

    if (file_list_)
    {
        wxListItem col0, col1;
        col0.SetId(0);
        col0.SetText(wxString::FromUTF8(I18n::get("col_format")));
        file_list_->SetColumn(0, col0);
        col1.SetId(1);
        col1.SetText(wxString::FromUTF8(I18n::get("col_filename")));
        file_list_->SetColumn(1, col1);
    }

    if (out_format_rbs_.iso) out_format_rbs_.iso->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_fmt_iso")));
    if (out_format_rbs_.god) out_format_rbs_.god->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_fmt_god")));
    if (out_format_rbs_.cci) out_format_rbs_.cci->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_fmt_cci")));
    if (out_format_rbs_.cso) out_format_rbs_.cso->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_fmt_cso")));
    if (out_format_rbs_.zar) out_format_rbs_.zar->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_fmt_zar")));
    if (out_format_rbs_.extract) out_format_rbs_.extract->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_fmt_extract")));

    if (auto_format_rbs_.ogxbox) auto_format_rbs_.ogxbox->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_auto_ogxbox")));
    if (auto_format_rbs_.xbox360) auto_format_rbs_.xbox360->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_auto_xbox360")));
    if (auto_format_rbs_.xemu) auto_format_rbs_.xemu->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_auto_xemu")));
    if (auto_format_rbs_.xenia) auto_format_rbs_.xenia->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_auto_xenia")));

    if (out_scrub_rbs_.none) {
        out_scrub_rbs_.none->SetLabel(wxString::FromUTF8(I18n::get("scrub_none")));
        out_scrub_rbs_.none->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_scrub_none")));
    }
    if (out_scrub_rbs_.partial) {
        out_scrub_rbs_.partial->SetLabel(wxString::FromUTF8(I18n::get("scrub_partial")));
        out_scrub_rbs_.partial->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_scrub_partial")));
    }
    if (out_scrub_rbs_.full) {
        out_scrub_rbs_.full->SetLabel(wxString::FromUTF8(I18n::get("scrub_full")));
        out_scrub_rbs_.full->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_scrub_full")));
    }

    if (out_settings_cbs_.split) {
        out_settings_cbs_.split->SetLabel(wxString::FromUTF8(I18n::get("setting_split")));
        out_settings_cbs_.split->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_split")));
    }
    if (out_settings_cbs_.attach_xbe) {
        out_settings_cbs_.attach_xbe->SetLabel(wxString::FromUTF8(I18n::get("setting_attach_xbe")));
        out_settings_cbs_.attach_xbe->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_attach_xbe")));
    }
    if (out_settings_cbs_.allowed_media_xbe) {
        out_settings_cbs_.allowed_media_xbe->SetLabel(wxString::FromUTF8(I18n::get("setting_am_patch")));
        out_settings_cbs_.allowed_media_xbe->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_am_patch")));
    }
    if (out_settings_cbs_.rename_xbe) {
        out_settings_cbs_.rename_xbe->SetLabel(wxString::FromUTF8(I18n::get("setting_rename_xbe")));
        out_settings_cbs_.rename_xbe->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_rename_xbe")));
    }
    if (out_settings_cbs_.offline_mode) {
        out_settings_cbs_.offline_mode->SetLabel(wxString::FromUTF8(I18n::get("setting_offline_mode")));
        out_settings_cbs_.offline_mode->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_offline_mode")));
    }
    if (out_settings_cbs_.keep_name) {
        out_settings_cbs_.keep_name->SetLabel(wxString::FromUTF8(I18n::get("setting_keep_name")));
        out_settings_cbs_.keep_name->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_keep_name")));
    }

    if (language_rbs_.system) {
        language_rbs_.system->SetLabel(wxString::FromUTF8(I18n::get("lang_system")));
        language_rbs_.system->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_lang_system")));
    }
    if (language_rbs_.english) {
        language_rbs_.english->SetLabel(wxString::FromUTF8(I18n::get("lang_english")));
        language_rbs_.english->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_lang_english")));
    }
    if (language_rbs_.italian) {
        language_rbs_.italian->SetLabel(wxString::FromUTF8(I18n::get("lang_italian")));
        language_rbs_.italian->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_lang_italian")));
    }
    if (language_rbs_.german) {
        language_rbs_.german->SetLabel(wxString::FromUTF8(I18n::get("lang_german")));
        language_rbs_.german->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_lang_german")));
    }
    if (language_rbs_.french) {
        language_rbs_.french->SetLabel(wxString::FromUTF8(I18n::get("lang_french")));
        language_rbs_.french->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_lang_french")));
    }
    if (language_rbs_.spanish) {
        language_rbs_.spanish->SetLabel(wxString::FromUTF8(I18n::get("lang_spanish")));
        language_rbs_.spanish->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_lang_spanish")));
    }
    if (language_rbs_.portuguese) {
        language_rbs_.portuguese->SetLabel(wxString::FromUTF8(I18n::get("lang_portuguese")));
        language_rbs_.portuguese->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_lang_portuguese")));
    }

    if (process_buttons_.process) {
        process_buttons_.process->SetLabel(wxString::FromUTF8(I18n::get("btn_process_all")));
        process_buttons_.process->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_process_all")));
    }
    if (process_buttons_.pause) {
        if (current_status_ == Status::PAUSED) {
            process_buttons_.pause->SetLabel(wxString::FromUTF8(I18n::get("btn_resume")));
        } else {
            process_buttons_.pause->SetLabel(wxString::FromUTF8(I18n::get("btn_pause")));
        }
        process_buttons_.pause->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_pause")));
    }
    if (process_buttons_.cancel) {
        process_buttons_.cancel->SetLabel(wxString::FromUTF8(I18n::get("btn_cancel")));
        process_buttons_.cancel->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_cancel")));
    }

    if (status_field_ && current_status_ == Status::IDLE) {
        status_field_->ChangeValue(wxString::FromUTF8(I18n::get("status_idle")));
    }

    if (main_panel_ && main_panel_->GetSizer())
    {
        main_panel_->GetSizer()->Layout();
        wxSize min_size = main_panel_->GetSizer()->GetMinSize();
        wxSize frame_needed = ClientToWindowSize(min_size);

        wxSize cur_size = GetSize();
        int new_w = std::max(cur_size.GetWidth(), std::max(900, frame_needed.GetWidth()));
        int new_h = std::max(cur_size.GetHeight(), std::max(620, frame_needed.GetHeight()));

        SetMinSize(wxSize(std::max(880, frame_needed.GetWidth()), std::max(600, frame_needed.GetHeight())));

        if (new_w > cur_size.GetWidth() || new_h > cur_size.GetHeight())
        {
            SetSize(new_w, new_h);
        }
    }

    Layout();
}