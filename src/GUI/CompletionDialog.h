#ifndef _COMPLETION_DIALOG_H_
#define _COMPLETION_DIALOG_H_

#include <wx/wx.h>
#include <string>

class CompletionDialog : public wxDialog
{
public:
    CompletionDialog(wxWindow* parent,
                     const std::string& title,
                     const std::string& message,
                     bool has_errors,
                     const std::string& log_file_path = "xgdtool.log");

private:
    void on_open_log(wxCommandEvent& event);
    std::string log_file_path_;
};

#endif // _COMPLETION_DIALOG_H_
