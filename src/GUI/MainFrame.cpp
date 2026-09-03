#include <chrono>
#include <wx/notifmsg.h>

#include "GUI/MainFrame.h"
#include "GUI/CompletionDialog.h"
#include "Utils/LocalizationManager.h"

wxDEFINE_EVENT(wxEVT_UPDATE_CURRENT_PROGRESS, wxThreadEvent);
wxDEFINE_EVENT(wxEVT_UPDATE_TOTAL_PROGRESS, wxThreadEvent);
wxDEFINE_EVENT(wxEVT_THREAD_COMPLETED, wxThreadEvent);
wxDEFINE_EVENT(wxEVT_UPDATE_CURRENT_STAGE, wxThreadEvent);
wxDEFINE_EVENT(wxEVT_UPDATE_ITEM_STATUS, wxThreadEvent);

wxBEGIN_EVENT_TABLE(MainFrame, wxFrame)
    EVT_BUTTON(wxID_ANY, MainFrame::on_process_all)
    EVT_BUTTON(wxID_ANY, MainFrame::on_cancel_process)
    EVT_THREAD(wxEVT_UPDATE_CURRENT_PROGRESS, MainFrame::on_update_current_progress)
    EVT_THREAD(wxEVT_UPDATE_TOTAL_PROGRESS, MainFrame::on_update_total_progress)
    EVT_THREAD(wxEVT_THREAD_COMPLETED, MainFrame::on_thread_completed)
    EVT_THREAD(wxEVT_UPDATE_CURRENT_STAGE, MainFrame::on_update_current_stage)
    EVT_THREAD(wxEVT_UPDATE_ITEM_STATUS, MainFrame::on_update_item_status)
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
    Bind(wxEVT_UPDATE_ITEM_STATUS, &MainFrame::on_update_item_status, this);
}

static void apply_theme_recursive(wxWindow* win, bool dark)
{
    if (!win) return;

    wxColour bg = dark ? wxColour(32, 33, 36) : wxNullColour;
    wxColour control_bg = dark ? wxColour(48, 49, 52) : wxNullColour;
    wxColour fg = dark ? wxColour(235, 235, 235) : wxNullColour;

    if (dynamic_cast<wxPanel*>(win))
    {
        win->SetBackgroundColour(bg);
        win->SetForegroundColour(fg);
    }
    else if (dynamic_cast<wxStaticText*>(win) || dynamic_cast<wxCheckBox*>(win) || dynamic_cast<wxRadioButton*>(win))
    {
        win->SetBackgroundColour(bg);
        win->SetForegroundColour(fg);
    }
    else if (dynamic_cast<wxTextCtrl*>(win) || dynamic_cast<wxListCtrl*>(win) || dynamic_cast<wxChoice*>(win))
    {
        win->SetBackgroundColour(control_bg);
        win->SetForegroundColour(fg);
    }
    else if (dynamic_cast<wxButton*>(win))
    {
        win->SetBackgroundColour(control_bg);
        win->SetForegroundColour(fg);
    }

    win->Refresh();

    for (wxWindowList::compatibility_iterator node = win->GetChildren().GetFirst(); node; node = node->GetNext())
    {
        apply_theme_recursive(node->GetData(), dark);
    }
}

void MainFrame::apply_theme(bool dark)
{
    is_dark_mode_ = dark;
    SetBackgroundColour(dark ? wxColour(32, 33, 36) : wxNullColour);
    apply_theme_recursive(this, dark);
    Refresh();
}

void MainFrame::on_dark_mode_toggle(wxCommandEvent& event)
{
    if (out_settings_cbs_.dark_mode)
    {
        apply_theme(out_settings_cbs_.dark_mode->GetValue());
    }
}

void MainFrame::handle_dropped_files(const wxArrayString& files)
{
    if (files.empty()) return;

    std::vector<std::filesystem::path> new_paths;
    for (const auto& file : files)
    {
        std::filesystem::path p(file.ToStdWstring());
        if (std::filesystem::exists(p))
        {
            new_paths.push_back(p);
        }
    }

    if (new_paths.empty()) return;

    InputHelper temp_helper(new_paths, "", OutputSettings());
    if (temp_helper.input_infos().empty())
    {
        wxLogMessage(wxString::FromUTF8(I18n::get("msg_no_valid_files")));
        return;
    }

    for (const auto& info : temp_helper.input_infos())
    {
        for (const auto& p : info.paths)
        {
            input_paths_.push_back(p);
        }

        long item_index = file_list_->InsertItem(file_list_->GetItemCount(), get_file_type_string(info.file_type));
        file_list_->SetItem(item_index, 1, info.paths.front().filename().string());
        file_list_->SetItem(item_index, 2, wxString::FromUTF8(I18n::get("status_queued")));
    }

    input_picker_.field->Clear();
    for (const auto& p : input_paths_)
    {
        input_picker_.field->AppendText(wxString::FromUTF8(p.string()) + "\n");
    }
}

void MainFrame::on_list_item_right_click(wxListEvent& event)
{
    wxMenu menu;
    menu.Append(1001, wxString::FromUTF8(I18n::get("menu_remove_selected")));
    menu.Append(1002, wxString::FromUTF8(I18n::get("menu_clear_list")));

    menu.Bind(wxEVT_COMMAND_MENU_SELECTED, &MainFrame::on_remove_selected_items, this, 1001);
    menu.Bind(wxEVT_COMMAND_MENU_SELECTED, &MainFrame::on_clear_file_list, this, 1002);

    PopupMenu(&menu);
}

void MainFrame::on_list_key_down(wxListEvent& event)
{
    if (event.GetKeyCode() == WXK_DELETE)
    {
        wxCommandEvent dummy;
        on_remove_selected_items(dummy);
    }
    else
    {
        event.Skip();
    }
}

void MainFrame::on_remove_selected_items(wxCommandEvent& event)
{
    std::vector<long> selected_indices;
    long item = -1;
    while ((item = file_list_->GetNextItem(item, wxLIST_NEXT_ALL, wxLIST_STATE_SELECTED)) != -1)
    {
        selected_indices.push_back(item);
    }

    if (selected_indices.empty()) return;

    for (auto it = selected_indices.rbegin(); it != selected_indices.rend(); ++it)
    {
        long idx = *it;
        file_list_->DeleteItem(idx);
        if (idx >= 0 && idx < static_cast<long>(input_paths_.size()))
        {
            input_paths_.erase(input_paths_.begin() + idx);
        }
    }

    input_picker_.field->Clear();
    for (const auto& p : input_paths_)
    {
        input_picker_.field->AppendText(wxString::FromUTF8(p.string()) + "\n");
    }
}

void MainFrame::on_clear_file_list(wxCommandEvent& event)
{
    file_list_->DeleteAllItems();
    input_paths_.clear();
    input_picker_.field->Clear();
    input_helper_.reset();
}

void MainFrame::on_update_item_status(wxThreadEvent& event)
{
    auto payload = event.GetPayload<std::pair<long, std::string>>();
    long row = payload.first;
    std::string status = payload.second;
    if (file_list_ && row >= 0 && row < file_list_->GetItemCount())
    {
        file_list_->SetItem(row, 2, wxString::FromUTF8(status));
    }
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
        if (status_field_->GetValue().ToStdString() == "Paused")
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
    long index = 0;
    for (const auto& input_info : input_helper_->input_infos())
    {
        wxThreadEvent* item_event_start = new wxThreadEvent(wxEVT_UPDATE_ITEM_STATUS);
        item_event_start->SetPayload(std::make_pair(index, I18n::get("status_in_progress")));
        wxQueueEvent(this, item_event_start);

        size_t prev_failed_count = input_helper_->failed_inputs().size();
        input_helper_->process_single(input_info);
        size_t new_failed_count = input_helper_->failed_inputs().size();

        wxThreadEvent* item_event_end = new wxThreadEvent(wxEVT_UPDATE_ITEM_STATUS);
        std::string final_status = (new_failed_count > prev_failed_count) ? I18n::get("status_error") : I18n::get("status_done");
        item_event_end->SetPayload(std::make_pair(index, final_status));
        wxQueueEvent(this, item_event_end);

        current_file_index_++;
        index++;

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
    if (out_settings_cbs_.play_sound && out_settings_cbs_.play_sound->GetValue())
    {
        wxBell();
    }
    if (out_settings_cbs_.open_output_dir && out_settings_cbs_.open_output_dir->GetValue() && !output_path_.empty())
    {
        wxLaunchDefaultApplication(output_path_.string());
    }

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

    if (choice == 0)
    {
        wxString wildcard = wxString::FromUTF8(I18n::get("wildcard_xbox_images"));
        wxFileDialog open_file_dialog(this, wxString::FromUTF8(I18n::get("dialog_select_files_title")), "", "", wildcard, wxFD_OPEN | wxFD_FILE_MUST_EXIST | wxFD_MULTIPLE);

        if (open_file_dialog.ShowModal() == wxID_OK)
        {
            wxArrayString file_paths;
            open_file_dialog.GetPaths(file_paths);
            handle_dropped_files(file_paths);
        }
    }
    else if (choice == 1)
    {
        wxDirDialog open_dir_dialog(this, wxString::FromUTF8(I18n::get("dialog_select_dir_title")), "", wxDD_DEFAULT_STYLE | wxDD_DIR_MUST_EXIST);

        if (open_dir_dialog.ShowModal() == wxID_OK)
        {
            wxArrayString dir_paths;
            dir_paths.Add(open_dir_dialog.GetPath());
            handle_dropped_files(dir_paths);
        }
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

void MainFrame::update_progress_bar(uint64_t progress, uint64_t total, double mb_per_sec, uint32_t eta_seconds)
{
    if (wxTheApp && wxTheApp->GetTopWindow())
    {
        wxThreadEvent* event = new wxThreadEvent(wxEVT_UPDATE_CURRENT_PROGRESS);
        event->SetPayload(ProgressPayload{progress, total, mb_per_sec, eta_seconds});
        wxQueueEvent(wxTheApp->GetTopWindow(), event);
    }
}

void MainFrame::update_current_progress_bar(uint64_t progress, uint64_t total)
{
    if (!current_progress_bar_) 
    {
        return;
    }

    double percentage = (total > 0) ? (static_cast<double>(progress) / static_cast<double>(total) * 100.0) : 0.0;
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
    auto data = event.GetPayload<ProgressPayload>();
    uint64_t progress = data.progress;
    uint64_t total = data.total;

    update_current_progress_bar(progress, total);

    if (ui_labels_.current_progress)
    {
        if (total > 0 && progress < total && data.mb_s > 0.0)
        {
            uint32_t s = data.eta % 60;
            uint32_t m = (data.eta / 60) % 60;
            uint32_t h = data.eta / 3600;
            wxString eta_str = (h > 0) ? wxString::Format("%02u:%02u:%02u", h, m, s) : wxString::Format("%02u:%02u", m, s);
            double percentage = (static_cast<double>(progress) / total) * 100.0;
            ui_labels_.current_progress->SetLabel(
                wxString::Format("%s (%.1f%% | %.1f MB/s | ETA: %s)",
                    wxString::FromUTF8(I18n::get("label_current_progress")),
                    percentage, data.mb_s, eta_str));
        }
        else if (progress >= total)
        {
            ui_labels_.current_progress->SetLabel(wxString::FromUTF8(I18n::get("label_current_progress")));
        }
    }

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
    bool processing = current_status_ == Status::PROCESSING || current_status_ == Status::PAUSED;
    process_buttons_.process->Enable(!processing);
    if (process_buttons_.verify) process_buttons_.verify->Enable(!processing);
    process_buttons_.pause->Enable(processing);
    process_buttons_.cancel->Enable(processing);

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
    if (out_settings_cbs_.generate_dvd) out_settings_cbs_.generate_dvd->Enable(!processing);
    if (out_settings_cbs_.calculate_checksum) out_settings_cbs_.calculate_checksum->Enable(!processing);
    if (compression_choice_) compression_choice_->Enable(!processing);
    if (threads_choice_) threads_choice_->Enable(!processing);

    language_rbs_.system->Enable(!processing);
    language_rbs_.english->Enable(!processing);
    language_rbs_.italian->Enable(!processing);
    language_rbs_.german->Enable(!processing);
    language_rbs_.french->Enable(!processing);
    language_rbs_.spanish->Enable(!processing);
    language_rbs_.portuguese->Enable(!processing);
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
    if (out_settings_cbs_.generate_dvd) output_settings.generate_dvd = out_settings_cbs_.generate_dvd->GetValue();
    if (out_settings_cbs_.calculate_checksum) output_settings.calculate_checksum = out_settings_cbs_.calculate_checksum->GetValue();
    if (out_settings_cbs_.smart_rename) output_settings.smart_rename = out_settings_cbs_.smart_rename->GetValue();
    if (out_settings_cbs_.play_sound) output_settings.play_sound = out_settings_cbs_.play_sound->GetValue();
    if (out_settings_cbs_.open_output_dir) output_settings.open_output_dir = out_settings_cbs_.open_output_dir->GetValue();

    if (compression_choice_)
    {
        int sel = compression_choice_->GetSelection();
        output_settings.compression_level = (sel == wxNOT_FOUND) ? 0 : sel;
    }
    if (threads_choice_)
    {
        int sel = threads_choice_->GetSelection();
        if (sel == 0) output_settings.threads = 1;
        else if (sel == 1) output_settings.threads = 2;
        else if (sel == 2) output_settings.threads = 4;
        else if (sel == 3) output_settings.threads = 8;
        else output_settings.threads = 1;
    }

    return output_settings;
}

void MainFrame::on_verify_image(wxCommandEvent& event)
{
    std::filesystem::path target_file;
    long item = -1;
    if (file_list_)
    {
        item = file_list_->GetNextItem(item, wxLIST_NEXT_ALL, wxLIST_STATE_SELECTED);
        if (item != -1)
        {
            wxString path_str = file_list_->GetItemText(item);
            target_file = std::filesystem::path(path_str.ToStdString());
        }
    }

    if (target_file.empty() && !input_paths_.empty())
    {
        target_file = input_paths_.front();
    }

    if (target_file.empty() || !std::filesystem::exists(target_file))
    {
        wxMessageBox(
            wxString::FromUTF8("Please select an image file to verify from the list or browse an input path."),
            wxString::FromUTF8("Verify Image"),
            wxOK | wxICON_INFORMATION, this);
        return;
    }

    OutputSettings verify_settings;
    verify_settings.file_type = FileType::VERIFY;

    current_status_ = Status::PROCESSING;
    update_button_states();
    status_field_->ChangeValue(wxString::FromUTF8("Verifying image integrity..."));

    processing_thread_ = std::make_unique<std::thread>([this, target_file, verify_settings]() {
        try {
            InputHelper helper(target_file, "", verify_settings);
            helper.process_all();
        } catch (const std::exception& e) {
            XGDLog(Error) << "Verification failed: " << e.what() << "\n";
        }
        wxThreadEvent* comp_event = new wxThreadEvent(wxEVT_THREAD_COMPLETED);
        wxQueueEvent(this, comp_event);
    });
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
    if (ui_labels_.compression) ui_labels_.compression->SetLabel(wxString::FromUTF8(I18n::get("label_compression_level")));
    if (ui_labels_.threads) ui_labels_.threads->SetLabel(wxString::FromUTF8(I18n::get("label_threads")));

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
        wxListItem col0, col1, col2;
        col0.SetId(0);
        col0.SetText(wxString::FromUTF8(I18n::get("col_format")));
        file_list_->SetColumn(0, col0);
        col1.SetId(1);
        col1.SetText(wxString::FromUTF8(I18n::get("col_filename")));
        file_list_->SetColumn(1, col1);
        col2.SetId(2);
        col2.SetText(wxString::FromUTF8(I18n::get("col_status")));
        file_list_->SetColumn(2, col2);
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
    if (out_settings_cbs_.generate_dvd) {
        out_settings_cbs_.generate_dvd->SetLabel(wxString::FromUTF8(I18n::get("setting_generate_dvd")));
        out_settings_cbs_.generate_dvd->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_generate_dvd")));
    }
    if (out_settings_cbs_.calculate_checksum) {
        out_settings_cbs_.calculate_checksum->SetLabel(wxString::FromUTF8(I18n::get("setting_checksum")));
        out_settings_cbs_.calculate_checksum->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_checksum")));
    }
    if (out_settings_cbs_.dark_mode) {
        out_settings_cbs_.dark_mode->SetLabel(wxString::FromUTF8(I18n::get("setting_dark_mode")));
        out_settings_cbs_.dark_mode->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_dark_mode")));
    }

    if (compression_choice_) {
        compression_choice_->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_compression")));
        int current_sel = compression_choice_->GetSelection();
        compression_choice_->SetString(0, wxString::FromUTF8(I18n::get("compress_default")));
        compression_choice_->SetString(1, wxString::FromUTF8(I18n::get("compress_fast")));
        compression_choice_->SetString(2, wxString::FromUTF8(I18n::get("compress_balanced")));
        compression_choice_->SetString(3, wxString::FromUTF8(I18n::get("compress_max")));
        compression_choice_->SetSelection(current_sel == wxNOT_FOUND ? 0 : current_sel);
    }

    if (threads_choice_) {
        threads_choice_->SetToolTip(wxString::FromUTF8(I18n::get("tooltip_threads")));
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
        int new_w = std::max(cur_size.GetWidth(), std::max(920, frame_needed.GetWidth()));
        int new_h = std::max(cur_size.GetHeight(), std::max(640, frame_needed.GetHeight()));

        SetMinSize(wxSize(std::max(900, frame_needed.GetWidth()), std::max(620, frame_needed.GetHeight())));

        if (new_w > cur_size.GetWidth() || new_h > cur_size.GetHeight())
        {
            SetSize(new_w, new_h);
        }
    }

    Layout();
}