#ifndef _MAIN_FRAME_H_
#define _MAIN_FRAME_H_

#include <filesystem>
#include <memory>
#include <string>
#include <vector>
#include <thread>
#include <atomic>
#include <mutex>

#include <wx/wx.h>
#include <wx/gbsizer.h>
#include <wx/grid.h>
#include <wx/listctrl.h>
#include <wx/hyperlink.h>

#include "XGD.h"
#include "InputHelper/Types.h"
#include "InputHelper/InputHelper.h"

wxDECLARE_EVENT(wxEVT_UPDATE_CURRENT_PROGRESS, wxThreadEvent);
wxDECLARE_EVENT(wxEVT_UPDATE_TOTAL_PROGRESS, wxThreadEvent);
wxDECLARE_EVENT(wxEVT_THREAD_COMPLETED, wxThreadEvent);
wxDECLARE_EVENT(wxEVT_UPDATE_CURRENT_STAGE, wxThreadEvent);
wxDECLARE_EVENT(wxEVT_UPDATE_ITEM_STATUS, wxThreadEvent);

struct ProgressPayload
{
    uint64_t progress{0};
    uint64_t total{0};
    double mb_s{0.0};
    uint32_t eta{0};
};

class MainFrame : public wxFrame
{
public:
    MainFrame(const wxString& title, const wxPoint& pos, const wxSize& size);
    ~MainFrame();

    static void update_progress_bar(uint64_t current, uint64_t total, double mb_per_sec = 0.0, uint32_t eta_seconds = 0);
    static void update_status_field(const std::string status);
    void update_ui_language();
    void handle_dropped_files(const wxArrayString& files);

private:
    enum class Status { IDLE, PROCESSING, PAUSED, CANCELED };

    struct Picker
    {
        wxTextCtrl* field{nullptr};
        wxButton* button{nullptr};
    };

    struct OutFormatRadioButtons
    {
        wxRadioButton* extract{nullptr};
        wxRadioButton* iso{nullptr};
        wxRadioButton* god{nullptr};
        wxRadioButton* cci{nullptr};
        wxRadioButton* cso{nullptr};
        wxRadioButton* zar{nullptr};
    };

    struct AutoFormatRadioButtons
    {
        wxRadioButton* ogxbox{nullptr};
        wxRadioButton* xbox360{nullptr};
        wxRadioButton* xemu{nullptr};
        wxRadioButton* xenia{nullptr};
    };

    struct ScrubRadioButtons
    {
        wxRadioButton* none{nullptr};
        wxRadioButton* partial{nullptr};
        wxRadioButton* full{nullptr};
    };

    struct SettingsCheckBoxes
    {
        wxCheckBox* split{nullptr};
        wxCheckBox* attach_xbe{nullptr};
        wxCheckBox* allowed_media_xbe{nullptr};
        wxCheckBox* rename_xbe{nullptr};
        wxCheckBox* offline_mode{nullptr};
        wxCheckBox* keep_name{nullptr};
        wxCheckBox* generate_dvd{nullptr};
        wxCheckBox* calculate_checksum{nullptr};
        wxCheckBox* smart_rename{nullptr};
        wxCheckBox* play_sound{nullptr};
        wxCheckBox* open_output_dir{nullptr};
        wxCheckBox* dark_mode{nullptr};
    };

    struct ProcessButtons
    {
        wxButton* process{nullptr};
        wxButton* verify{nullptr};
        wxButton* pause{nullptr};
        wxButton* cancel{nullptr};
    };

    struct LanguageRadioButtons
    {
        wxRadioButton* system{nullptr};
        wxRadioButton* english{nullptr};
        wxRadioButton* italian{nullptr};
        wxRadioButton* german{nullptr};
        wxRadioButton* french{nullptr};
        wxRadioButton* spanish{nullptr};
        wxRadioButton* portuguese{nullptr};
    };

    struct UILabels
    {
        wxStaticText* input_path{nullptr};
        wxStaticText* output_dir{nullptr};
        wxStaticText* file_list{nullptr};
        wxStaticText* status{nullptr};
        wxStaticText* current_progress{nullptr};
        wxStaticText* total_progress{nullptr};
        wxStaticText* out_format{nullptr};
        wxStaticText* scrub{nullptr};
        wxStaticText* settings{nullptr};
        wxStaticText* language{nullptr};
        wxStaticText* compression{nullptr};
        wxStaticText* threads{nullptr};
    };

    std::atomic<Status> current_status_{Status::IDLE};
    std::unique_ptr<std::thread> processing_thread_{nullptr};
    std::unique_ptr<InputHelper> input_helper_{nullptr};
    std::vector<std::filesystem::path> input_paths_;
    std::filesystem::path output_path_;

    Picker input_picker_;
    Picker output_picker_;

    wxListCtrl* file_list_{nullptr};

    wxTextCtrl* status_field_{nullptr};
    std::string stored_status_;

    OutFormatRadioButtons out_format_rbs_;
    AutoFormatRadioButtons auto_format_rbs_;
    ScrubRadioButtons out_scrub_rbs_;
    SettingsCheckBoxes out_settings_cbs_;
    LanguageRadioButtons language_rbs_;
    ProcessButtons process_buttons_;
    UILabels ui_labels_;

    wxChoice* compression_choice_{nullptr};
    wxChoice* threads_choice_{nullptr};
    bool is_dark_mode_{false};

    static wxGauge* current_progress_bar_;
    wxGauge* total_progress_bar_{nullptr};
    wxPanel* main_panel_{nullptr};

    std::atomic<uint64_t> current_file_index_{0};
    std::atomic<uint64_t> total_files_count_{1};

    void on_pick_input_path(wxCommandEvent& event);
    void on_pick_output_path(wxCommandEvent& event);
    void on_process_all(wxCommandEvent& event);
    void on_verify_image(wxCommandEvent& event);
    void on_pause_process(wxCommandEvent& event);
    void on_cancel_process(wxCommandEvent& event);
    void on_update_current_progress(wxThreadEvent& event);
    void on_update_total_progress(wxThreadEvent& event);
    void on_thread_completed(wxThreadEvent& event);
    void on_update_current_stage(wxThreadEvent& event);
    void on_update_item_status(wxThreadEvent& event);

    void on_list_item_right_click(wxListEvent& event);
    void on_list_key_down(wxListEvent& event);
    void on_remove_selected_items(wxCommandEvent& event);
    void on_clear_file_list(wxCommandEvent& event);
    void on_dark_mode_toggle(wxCommandEvent& event);
    void apply_theme(bool dark);

    void update_current_progress_bar(uint64_t progress, uint64_t total);
    void process_files();
    void update_controls_state();
    void update_button_states();
    void on_language_selected(const std::string& lang_code);
    OutputSettings parse_ui_settings();
    void stop_all_processing();

    void create_frame();
    wxBoxSizer* create_out_format_radio_box(wxPanel* panel);
    wxBoxSizer* create_out_scrub_radio_box(wxPanel* panel);
    wxBoxSizer* create_out_settings_check_box(wxPanel* panel);
    wxBoxSizer* create_language_radio_box(wxPanel* panel);
    wxBoxSizer* create_input_picker_box(wxPanel* panel);
    wxBoxSizer* create_output_picker_box(wxPanel* panel);
    wxBoxSizer* create_info_box(wxPanel* panel);
    wxBoxSizer* create_process_buttons_box(wxPanel* panel);

    std::string get_file_type_string(FileType type);
    const char* auto_format_to_string(AutoFormat format);
    const char* file_type_to_string(FileType type);
    const char* scrub_type_to_string(ScrubType type);
    void log_output_settings(const OutputSettings& settings);

    wxDECLARE_EVENT_TABLE();
};

#endif // _MAIN_FRAME_H_