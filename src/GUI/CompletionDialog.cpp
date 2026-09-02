#include "CompletionDialog.h"
#include <wx/artprov.h>
#include <wx/statline.h>
#include <wx/stdpaths.h>
#include <wx/filename.h>
#include <filesystem>
#include "Utils/LocalizationManager.h"

namespace fs = std::filesystem;

CompletionDialog::CompletionDialog(wxWindow* parent,
                                   const std::string& title,
                                   const std::string& message,
                                   bool has_errors,
                                   const std::string& log_file_path)
    : wxDialog(parent, wxID_ANY, wxString::FromUTF8(title), wxDefaultPosition, wxDefaultSize, wxDEFAULT_DIALOG_STYLE | wxRESIZE_BORDER),
      log_file_path_(log_file_path)
{
    SetBackgroundColour(wxSystemSettings::GetColour(wxSYS_COLOUR_FRAMEBK));

    wxBoxSizer* main_sizer = new wxBoxSizer(wxVERTICAL);
    wxBoxSizer* content_sizer = new wxBoxSizer(wxHORIZONTAL);

    // Icon
    wxArtID art_id = has_errors ? wxART_WARNING : wxART_INFORMATION;
    wxStaticBitmap* icon = new wxStaticBitmap(this, wxID_ANY, wxArtProvider::GetBitmap(art_id, wxART_MESSAGE_BOX, wxSize(48, 48)));
    content_sizer->Add(icon, 0, wxALIGN_TOP | wxALL, 15);

    // Message
    wxStaticText* text = new wxStaticText(this, wxID_ANY, wxString::FromUTF8(message));
    content_sizer->Add(text, 1, wxEXPAND | wxTOP | wxBOTTOM | wxRIGHT, 15);

    main_sizer->Add(content_sizer, 1, wxEXPAND);
    main_sizer->Add(new wxStaticLine(this), 0, wxEXPAND | wxLEFT | wxRIGHT, 10);

    // Button Bar
    wxBoxSizer* button_sizer = new wxBoxSizer(wxHORIZONTAL);
    button_sizer->AddStretchSpacer();

    if (has_errors)
    {
        wxButton* open_log_btn = new wxButton(this, wxID_ANY, wxString::FromUTF8(I18n::get("btn_open_log")));
        open_log_btn->Bind(wxEVT_BUTTON, &CompletionDialog::on_open_log, this);
        button_sizer->Add(open_log_btn, 0, wxALL, 10);
    }

    wxButton* ok_btn = new wxButton(this, wxID_OK, wxString::FromUTF8(I18n::get("btn_ok")));
    ok_btn->SetDefault();
    button_sizer->Add(ok_btn, 0, wxALL, 10);

    main_sizer->Add(button_sizer, 0, wxEXPAND);

    SetSizerAndFit(main_sizer);
    SetMinSize(wxSize(420, 180));
    CenterOnParent();
}

void CompletionDialog::on_open_log(wxCommandEvent&)
{
    std::string target = log_file_path_;
    if (!fs::exists(target))
    {
        wxString exe_path = wxStandardPaths::Get().GetExecutablePath();
        wxFileName fn(exe_path);
        std::string next_to_exe = fn.GetPath().ToStdString() + "/" + log_file_path_;
        if (fs::exists(next_to_exe))
        {
            target = next_to_exe;
        }
    }

    if (fs::exists(target))
    {
        wxLaunchDefaultApplication(wxString::FromUTF8(target));
    }
    else
    {
        wxMessageBox("Log file not found at: " + target, "Error", wxOK | wxICON_ERROR, this);
    }
}
