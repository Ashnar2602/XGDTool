#include "GUI/MainFrame.h"
#include "Utils/LocalizationManager.h"

void MainFrame::create_frame()
{
    #ifdef WIN32
        wxIcon icon;
        icon.LoadFile("IDI_APP_ICON", wxBITMAP_TYPE_ICO_RESOURCE);
        SetIcon(icon);
    #endif

    wxPanel* panel = new wxPanel(this, wxID_ANY);
    main_panel_ = panel;

    wxBoxSizer* main_sizer = new wxBoxSizer(wxVERTICAL);

    wxFlexGridSizer* fg_sizer = new wxFlexGridSizer(12, 2, 10, 10);
    fg_sizer->AddGrowableCol(1, 1); 
    fg_sizer->AddGrowableRow(2, 1);

    ui_labels_.input_path = new wxStaticText(panel, wxID_ANY, "Input Path:");
    fg_sizer->Add(ui_labels_.input_path, 0, wxALIGN_CENTER_VERTICAL | wxALIGN_RIGHT);
    fg_sizer->Add(create_input_picker_box(panel), 1, wxEXPAND);

    ui_labels_.output_dir = new wxStaticText(panel, wxID_ANY, "Output Directory:");
    fg_sizer->Add(ui_labels_.output_dir, 1, wxALIGN_CENTER_VERTICAL | wxALIGN_RIGHT);
    fg_sizer->Add(create_output_picker_box(panel), 1, wxEXPAND);

    ui_labels_.file_list = new wxStaticText(panel, wxID_ANY, "File List:");
    fg_sizer->Add(ui_labels_.file_list, 2, wxALIGN_TOP | wxALIGN_RIGHT);

    file_list_ = new wxListCtrl(panel, wxID_ANY, wxDefaultPosition, wxDefaultSize, wxLC_REPORT | wxLC_SINGLE_SEL);
    file_list_->InsertColumn(0, "Format", wxLIST_FORMAT_LEFT, 60);
    file_list_->InsertColumn(1, "Filename", wxLIST_FORMAT_LEFT, 600);

    fg_sizer->Add(file_list_, 1, wxEXPAND);

    wxBoxSizer* progress_lables_sizer = new wxBoxSizer(wxVERTICAL);
    ui_labels_.status = new wxStaticText(panel, wxID_ANY, "Status:");
    ui_labels_.current_progress = new wxStaticText(panel, wxID_ANY, "Current Progress:");
    ui_labels_.total_progress = new wxStaticText(panel, wxID_ANY, "Total Progress:");

    progress_lables_sizer->Add(ui_labels_.status, 0, wxALIGN_RIGHT);
    progress_lables_sizer->AddSpacer(18);
    progress_lables_sizer->Add(ui_labels_.current_progress, 0, wxALIGN_RIGHT);
    progress_lables_sizer->AddSpacer(18);
    progress_lables_sizer->Add(ui_labels_.total_progress, 0, wxALIGN_RIGHT);
    progress_lables_sizer->AddSpacer(4);

    fg_sizer->Add(progress_lables_sizer, 0, wxALIGN_BOTTOM);

    wxBoxSizer* bottom_sizer = new wxBoxSizer(wxHORIZONTAL);
    wxBoxSizer* settings_progress_bar_sizer = new wxBoxSizer(wxVERTICAL);
    wxBoxSizer* settings_sizer = new wxBoxSizer(wxHORIZONTAL);

    settings_sizer->Add(create_out_format_radio_box(panel), 0, wxEXPAND);
    settings_sizer->AddSpacer(25);
    settings_sizer->Add(create_out_scrub_radio_box(panel), 0, wxEXPAND);
    settings_sizer->AddSpacer(25);
    settings_sizer->Add(create_out_settings_check_box(panel), 0, wxEXPAND);
    settings_sizer->AddSpacer(25);
    settings_sizer->Add(create_language_radio_box(panel), 0, wxEXPAND);

    settings_progress_bar_sizer->Add(settings_sizer, 0, wxEXPAND);
    settings_progress_bar_sizer->AddSpacer(10);

    current_progress_bar_ = new wxGauge(panel, wxID_ANY, 100, wxDefaultPosition, wxSize(-1, 25));
    total_progress_bar_ = new wxGauge(panel, wxID_ANY, 100, wxDefaultPosition, wxSize(-1, 25));

    status_field_ = new wxTextCtrl(panel, wxID_ANY, "", wxDefaultPosition, wxDefaultSize, wxTE_READONLY);
    status_field_->SetBackgroundStyle(wxBG_STYLE_ERASE);
    status_field_->ChangeValue("Idle");

    settings_progress_bar_sizer->Add(status_field_, 0, wxEXPAND);
    settings_progress_bar_sizer->AddSpacer(10);
    settings_progress_bar_sizer->Add(current_progress_bar_, 0, wxEXPAND);
    settings_progress_bar_sizer->AddSpacer(10);
    settings_progress_bar_sizer->Add(total_progress_bar_, 0, wxEXPAND);

    bottom_sizer->Add(settings_progress_bar_sizer, 1, wxEXPAND);
    bottom_sizer->AddSpacer(10);

    bottom_sizer->Add(create_process_buttons_box(panel), 0, wxALIGN_BOTTOM);

    fg_sizer->Add(bottom_sizer, 10, wxEXPAND);
    fg_sizer->AddSpacer(10);
    fg_sizer->Add(create_info_box(panel), 11, wxALIGN_CENTER_VERTICAL | wxALIGN_LEFT);

    main_sizer->Add(fg_sizer, 1, wxALL | wxEXPAND, 10);

    panel->SetSizer(main_sizer);

    update_ui_language();
    update_controls_state();
}

wxBoxSizer* MainFrame::create_input_picker_box(wxPanel* panel)
{
    wxBoxSizer* input_sizer = new wxBoxSizer(wxHORIZONTAL);
    input_picker_.field = new wxTextCtrl(panel, wxID_ANY, "", wxDefaultPosition, wxDefaultSize, wxTE_READONLY);
    input_picker_.field->SetBackgroundColour(wxColour(250, 250, 250));

    input_sizer->Add(input_picker_.field, 1, wxEXPAND);

    input_picker_.button = new wxButton(panel, wxID_ANY, "Browse");
    input_picker_.button->SetToolTip("Select the input file or directory to process");
    input_picker_.button->Bind(wxEVT_BUTTON, &MainFrame::on_pick_input_path, this);

    input_sizer->Add(input_picker_.button, 0, wxLEFT, 5);

    return input_sizer;
}

wxBoxSizer* MainFrame::create_output_picker_box(wxPanel* panel)
{
    wxBoxSizer* output_sizer = new wxBoxSizer(wxHORIZONTAL);
    output_picker_.field = new wxTextCtrl(panel, wxID_ANY, "", wxDefaultPosition, wxDefaultSize, wxTE_READONLY);
    output_picker_.field->SetBackgroundColour(wxColour(250, 250, 250));

    output_sizer->Add(output_picker_.field, 1, wxEXPAND);

    output_picker_.button = new wxButton(panel, wxID_ANY, "Browse");
    output_picker_.button->SetToolTip("Select the output directory to save the processed files");
    output_picker_.button->Bind(wxEVT_BUTTON, &MainFrame::on_pick_output_path, this);

    output_sizer->Add(output_picker_.button, 0, wxLEFT, 5);

    return output_sizer;    
}

wxBoxSizer* MainFrame::create_process_buttons_box(wxPanel* panel)
{
    wxBoxSizer* buttons_sizer = new wxBoxSizer(wxVERTICAL);

    process_buttons_.process = new wxButton(panel, wxID_ANY, "Process All", wxDefaultPosition, wxSize(100, 25));
    process_buttons_.pause = new wxButton(panel, wxID_ANY, "Pause", wxDefaultPosition, wxSize(100, 25));
    process_buttons_.cancel = new wxButton(panel, wxID_ANY, "Cancel", wxDefaultPosition, wxSize(100, 25));

    process_buttons_.process->SetToolTip("Process all files in the File List");
    process_buttons_.process->Bind(wxEVT_BUTTON, &MainFrame::on_process_all, this);

    process_buttons_.pause->SetToolTip("Pause processing of files");
    process_buttons_.pause->Bind(wxEVT_BUTTON, &MainFrame::on_pause_process, this);

    process_buttons_.cancel->SetToolTip("Processing will stop after the current file is finished");
    process_buttons_.cancel->Bind(wxEVT_BUTTON, &MainFrame::on_cancel_process, this);

    buttons_sizer->Add(process_buttons_.process, 0, wxEXPAND);
    buttons_sizer->AddSpacer(10);
    buttons_sizer->Add(process_buttons_.pause, 0, wxEXPAND);
    buttons_sizer->AddSpacer(10);
    buttons_sizer->Add(process_buttons_.cancel, 0, wxEXPAND);

    return buttons_sizer;
}

wxBoxSizer* MainFrame::create_out_scrub_radio_box(wxPanel* panel)
{
    wxBoxSizer* scrub_rbs_sizer = new wxBoxSizer(wxVERTICAL);
    ui_labels_.scrub = new wxStaticText(panel, wxID_ANY, "Scrub:", wxDefaultPosition, wxDefaultSize, wxALIGN_LEFT);
    scrub_rbs_sizer->Add(ui_labels_.scrub, 0, wxALIGN_LEFT);
    scrub_rbs_sizer->AddSpacer(5);

    out_scrub_rbs_.none = new wxRadioButton(panel, wxID_ANY, "None", wxDefaultPosition, wxDefaultSize, wxRB_GROUP);
    out_scrub_rbs_.partial = new wxRadioButton(panel, wxID_ANY, "Partial");
    out_scrub_rbs_.full = new wxRadioButton(panel, wxID_ANY, "Full");

    out_scrub_rbs_.none->SetToolTip("No scrubbing, only video partion is removed if present");
    out_scrub_rbs_.partial->SetToolTip("Scrubs and trims the output image, random padding data is removed");
    out_scrub_rbs_.full->SetToolTip("Completely reauthor the resulting image, this will produce the smallest file possible");

    scrub_rbs_sizer->Add(out_scrub_rbs_.none, 0, wxEXPAND);
    scrub_rbs_sizer->Add(out_scrub_rbs_.partial, 0, wxEXPAND);
    scrub_rbs_sizer->Add(out_scrub_rbs_.full, 0, wxEXPAND);

    return scrub_rbs_sizer;
}

wxBoxSizer* MainFrame::create_out_settings_check_box(wxPanel* panel)
{
    wxBoxSizer* out_settings_sizer = new wxBoxSizer(wxVERTICAL);
    ui_labels_.settings = new wxStaticText(panel, wxID_ANY, "Settings:", wxDefaultPosition, wxDefaultSize, wxALIGN_LEFT);

    out_settings_sizer->Add(ui_labels_.settings, 0, wxALIGN_LEFT);
    out_settings_sizer->AddSpacer(5);

    out_settings_cbs_.split = new wxCheckBox(panel, wxID_ANY, "Split XISO");
    out_settings_cbs_.attach_xbe = new wxCheckBox(panel, wxID_ANY, "Generate Attach XBE");
    out_settings_cbs_.allowed_media_xbe = new wxCheckBox(panel, wxID_ANY, "Allowed Media XBE Patch");
    out_settings_cbs_.rename_xbe = new wxCheckBox(panel, wxID_ANY, "Rename XBE Title");
    out_settings_cbs_.offline_mode = new wxCheckBox(panel, wxID_ANY, "Offline Mode");
    out_settings_cbs_.keep_name = new wxCheckBox(panel, wxID_ANY, "Keep Original Name");
    
    out_settings_cbs_.split->SetToolTip("Splits the resulting XISO file if it's too large for OG Xbox");
    out_settings_cbs_.attach_xbe->SetToolTip("Generates an attach XBE file along with the output file");
    out_settings_cbs_.allowed_media_xbe->SetToolTip("Patches the Allowed Media field in resulting XBE files");
    out_settings_cbs_.rename_xbe->SetToolTip("Replaces the title field of resulting XBE files with one found in the database");
    out_settings_cbs_.offline_mode->SetToolTip("Disables online functionality, will result in less accurate file naming");
    out_settings_cbs_.keep_name->SetToolTip("Keeps the original input filename for output files, preventing overwrites for multi-disc games");
    
    out_settings_sizer->Add(out_settings_cbs_.split, 0, wxEXPAND);
    out_settings_sizer->Add(out_settings_cbs_.attach_xbe, 0, wxEXPAND);
    out_settings_sizer->Add(out_settings_cbs_.allowed_media_xbe, 0, wxEXPAND);
    out_settings_sizer->Add(out_settings_cbs_.rename_xbe, 0, wxEXPAND);
    out_settings_sizer->Add(out_settings_cbs_.offline_mode, 0, wxEXPAND);
    out_settings_sizer->Add(out_settings_cbs_.keep_name, 0, wxEXPAND);

    return out_settings_sizer;
}

wxBoxSizer* MainFrame::create_language_radio_box(wxPanel* panel)
{
    wxBoxSizer* lang_sizer = new wxBoxSizer(wxVERTICAL);
    ui_labels_.language = new wxStaticText(panel, wxID_ANY, "Language:", wxDefaultPosition, wxDefaultSize, wxALIGN_LEFT);
    lang_sizer->Add(ui_labels_.language, 0, wxALIGN_LEFT);
    lang_sizer->AddSpacer(5);

    language_rbs_.system     = new wxRadioButton(panel, wxID_ANY, wxString::FromUTF8(I18n::get("lang_system")), wxDefaultPosition, wxDefaultSize, wxRB_GROUP);
    language_rbs_.english    = new wxRadioButton(panel, wxID_ANY, wxString::FromUTF8(I18n::get("lang_english")));
    language_rbs_.italian    = new wxRadioButton(panel, wxID_ANY, wxString::FromUTF8(I18n::get("lang_italian")));
    language_rbs_.german     = new wxRadioButton(panel, wxID_ANY, wxString::FromUTF8(I18n::get("lang_german")));
    language_rbs_.french     = new wxRadioButton(panel, wxID_ANY, wxString::FromUTF8(I18n::get("lang_french")));
    language_rbs_.spanish    = new wxRadioButton(panel, wxID_ANY, wxString::FromUTF8(I18n::get("lang_spanish")));
    language_rbs_.portuguese = new wxRadioButton(panel, wxID_ANY, wxString::FromUTF8(I18n::get("lang_portuguese")));

    language_rbs_.system->SetValue(true);

    lang_sizer->Add(language_rbs_.system, 0, wxEXPAND);
    lang_sizer->Add(language_rbs_.english, 0, wxEXPAND);
    lang_sizer->Add(language_rbs_.italian, 0, wxEXPAND);
    lang_sizer->Add(language_rbs_.german, 0, wxEXPAND);
    lang_sizer->Add(language_rbs_.french, 0, wxEXPAND);
    lang_sizer->Add(language_rbs_.spanish, 0, wxEXPAND);
    lang_sizer->Add(language_rbs_.portuguese, 0, wxEXPAND);

    language_rbs_.system->Bind(wxEVT_RADIOBUTTON, [this](wxCommandEvent&) { on_language_selected(""); });
    language_rbs_.english->Bind(wxEVT_RADIOBUTTON, [this](wxCommandEvent&) { on_language_selected("en"); });
    language_rbs_.italian->Bind(wxEVT_RADIOBUTTON, [this](wxCommandEvent&) { on_language_selected("it"); });
    language_rbs_.german->Bind(wxEVT_RADIOBUTTON, [this](wxCommandEvent&) { on_language_selected("de"); });
    language_rbs_.french->Bind(wxEVT_RADIOBUTTON, [this](wxCommandEvent&) { on_language_selected("fr"); });
    language_rbs_.spanish->Bind(wxEVT_RADIOBUTTON, [this](wxCommandEvent&) { on_language_selected("es"); });
    language_rbs_.portuguese->Bind(wxEVT_RADIOBUTTON, [this](wxCommandEvent&) { on_language_selected("pt"); });

    return lang_sizer;
}

wxBoxSizer* MainFrame::create_out_format_radio_box(wxPanel* panel)
{
    wxBoxSizer* out_format_sizer = new wxBoxSizer(wxVERTICAL);
    ui_labels_.out_format = new wxStaticText(panel, wxID_ANY, "Output Format:", wxDefaultPosition, wxDefaultSize, wxALIGN_LEFT);

    out_format_sizer->Add(ui_labels_.out_format, 0, wxALIGN_LEFT);
    out_format_sizer->AddSpacer(5);

    wxBoxSizer* out_format_rbox = new wxBoxSizer(wxHORIZONTAL);
    wxBoxSizer* out_format_rbox_1 = new wxBoxSizer(wxVERTICAL);
    wxBoxSizer* out_format_rbox_2 = new wxBoxSizer(wxVERTICAL);

    out_format_rbs_.iso     = new wxRadioButton(panel, wxID_ANY, "XISO", wxDefaultPosition, wxDefaultSize, wxRB_GROUP);
    out_format_rbs_.god     = new wxRadioButton(panel, wxID_ANY, "GoD");
    out_format_rbs_.cci     = new wxRadioButton(panel, wxID_ANY, "CCI");
    out_format_rbs_.cso     = new wxRadioButton(panel, wxID_ANY, "CSO");
    out_format_rbs_.zar     = new wxRadioButton(panel, wxID_ANY, "ZAR");
    out_format_rbs_.extract = new wxRadioButton(panel, wxID_ANY, "Extract");

    auto_format_rbs_.ogxbox  = new wxRadioButton(panel, wxID_ANY, "OG XBox");
    auto_format_rbs_.xbox360 = new wxRadioButton(panel, wxID_ANY, "Xbox 360");
    auto_format_rbs_.xemu    = new wxRadioButton(panel, wxID_ANY, "Xemu");
    auto_format_rbs_.xenia   = new wxRadioButton(panel, wxID_ANY, "Xenia");

    out_format_rbs_.iso->SetToolTip("Creates an XISO image");
    out_format_rbs_.god->SetToolTip("Creates a Games on Demand image");
    out_format_rbs_.cci->SetToolTip("Creates a CCI archive");
    out_format_rbs_.cso->SetToolTip("Creates a CSO archive");
    out_format_rbs_.zar->SetToolTip("Creates a ZAR archive");
    
    out_format_rbs_.extract->SetToolTip("Extracts all files to a directory");
    auto_format_rbs_.ogxbox->SetToolTip("Automatically choose format and settings for use with OG Xbox");
    auto_format_rbs_.xbox360->SetToolTip("Automatically choose format and settings for use with Xbox 360");
    auto_format_rbs_.xemu->SetToolTip("Automatically choose format and settings for use with Xemu");
    auto_format_rbs_.xenia->SetToolTip("Automatically choose format and settings for use with Xenia");

    out_format_rbox_1->Add(out_format_rbs_.iso, 0, wxEXPAND);
    out_format_rbox_1->Add(out_format_rbs_.god, 0, wxEXPAND);
    out_format_rbox_1->Add(out_format_rbs_.cci, 0, wxEXPAND);
    out_format_rbox_1->Add(out_format_rbs_.cso, 0, wxEXPAND);
    out_format_rbox_1->Add(out_format_rbs_.zar, 0, wxEXPAND);
    out_format_rbox_1->Add(out_format_rbs_.extract, 0, wxEXPAND);

    out_format_rbox_2->Add(auto_format_rbs_.ogxbox, 0, wxEXPAND);
    out_format_rbox_2->Add(auto_format_rbs_.xbox360, 0, wxEXPAND);
    out_format_rbox_2->Add(auto_format_rbs_.xemu, 0, wxEXPAND);
    out_format_rbox_2->Add(auto_format_rbs_.xenia, 0, wxEXPAND);
    
    out_format_rbox->Add(out_format_rbox_1, 0, wxEXPAND);
    out_format_rbox->AddSpacer(10);
    out_format_rbox->Add(out_format_rbox_2, 0, wxEXPAND);
    out_format_sizer->Add(out_format_rbox, 0, wxEXPAND);

    out_format_rbs_.extract->Bind(wxEVT_RADIOBUTTON, [this](wxCommandEvent&) { update_controls_state(); });
    out_format_rbs_.iso->Bind(wxEVT_RADIOBUTTON, [this](wxCommandEvent&) { update_controls_state(); });
    out_format_rbs_.god->Bind(wxEVT_RADIOBUTTON, [this](wxCommandEvent&) { update_controls_state(); });
    out_format_rbs_.cci->Bind(wxEVT_RADIOBUTTON, [this](wxCommandEvent&) { update_controls_state(); });
    out_format_rbs_.cso->Bind(wxEVT_RADIOBUTTON, [this](wxCommandEvent&) { update_controls_state(); });
    out_format_rbs_.zar->Bind(wxEVT_RADIOBUTTON, [this](wxCommandEvent&) { update_controls_state(); });

    auto_format_rbs_.ogxbox->Bind(wxEVT_RADIOBUTTON, [this](wxCommandEvent&) { update_controls_state(); });
    auto_format_rbs_.xbox360->Bind(wxEVT_RADIOBUTTON, [this](wxCommandEvent&) { update_controls_state(); });
    auto_format_rbs_.xemu->Bind(wxEVT_RADIOBUTTON, [this](wxCommandEvent&) { update_controls_state(); });
    auto_format_rbs_.xenia->Bind(wxEVT_RADIOBUTTON, [this](wxCommandEvent&) { update_controls_state(); });

    return out_format_sizer;
}

wxBoxSizer* MainFrame::create_info_box(wxPanel* panel)
{
    wxBoxSizer* wo_sizer = new wxBoxSizer(wxHORIZONTAL);
    wxStaticText* version_label = new wxStaticText(panel, wxID_ANY, wxString("v") + XGD::VERSION);
    wxStaticText* fork_label = new wxStaticText(panel, wxID_ANY, " | By Ashnar2602 | Github: ");
    wxHyperlinkCtrl* fork_github_link = new wxHyperlinkCtrl(panel, wxID_ANY, "Ashnar2602/XGDTool", "https://github.com/Ashnar2602/XGDTool");
    wxStaticText* orig_label = new wxStaticText(panel, wxID_ANY, " | (Original: ");
    wxHyperlinkCtrl* wo_link = new wxHyperlinkCtrl(panel, wxID_ANY, "WiredOpposite", "https://github.com/wiredopposite/xgdtool");
    wxStaticText* close_label = new wxStaticText(panel, wxID_ANY, ")");
    
    wo_sizer->Add(version_label, 0, wxALIGN_CENTER_VERTICAL);
    wo_sizer->Add(fork_label, 0, wxALIGN_CENTER_VERTICAL);
    wo_sizer->Add(fork_github_link, 0, wxALIGN_CENTER_VERTICAL);
    wo_sizer->Add(orig_label, 0, wxALIGN_CENTER_VERTICAL);
    wo_sizer->Add(wo_link, 0, wxALIGN_CENTER_VERTICAL);
    wo_sizer->Add(close_label, 0, wxALIGN_CENTER_VERTICAL);

    return wo_sizer;
}