#include "GUI/MainFrame.h"

const char* MainFrame::auto_format_to_string(AutoFormat format) 
{
    switch (format) 
    {
        case AutoFormat::NONE: return "NONE";
        case AutoFormat::OGXBOX: return "OGXBOX";
        case AutoFormat::XBOX360: return "XBOX360";
        case AutoFormat::XEMU: return "XEMU";
        case AutoFormat::XENIA: return "XENIA";
        default: return "UNKNOWN";
    }
}

const char* MainFrame::file_type_to_string(FileType type) 
{
    switch (type) 
    {
        case FileType::UNKNOWN: return "UNKNOWN";
        case FileType::CCI: return "CCI";
        case FileType::CSO: return "CSO";
        case FileType::ISO: return "ISO";
        case FileType::ZAR: return "ZAR";
        case FileType::DIR: return "DIR";
        case FileType::GoD: return "GoD";
        case FileType::XBE: return "XBE";
        case FileType::LIST: return "LIST";
        default: return "UNKNOWN";
    }
}

const char* MainFrame::scrub_type_to_string(ScrubType type) 
{
    switch (type) 
    {
        case ScrubType::NONE: return "NONE";
        case ScrubType::PARTIAL: return "PARTIAL";
        case ScrubType::FULL: return "FULL";
        default: return "UNKNOWN";
    }
}

void MainFrame::log_output_settings(const OutputSettings& settings) 
{
    XGDLog() << "Starting batch processing with OutputSettings:\n"
             << "  AutoFormat: " << auto_format_to_string(settings.auto_format) << "\n"
             << "  FileType: " << file_type_to_string(settings.file_type) << "\n"
             << "  ScrubType: " << scrub_type_to_string(settings.scrub_type) << "\n"
             << "  Split: " << (settings.split ? "true" : "false") << "\n"
             << "  Attach XBE: " << (settings.attach_xbe ? "true" : "false") << "\n"
             << "  Allowed Media Patch: " << (settings.allowed_media_patch ? "true" : "false") << "\n"
             << "  Offline Mode: " << (settings.offline_mode ? "true" : "false") << "\n"
             << "  Keep Original Name: " << (settings.keep_name ? "true" : "false") << "\n"
             << "  Rename XBE: " << (settings.rename_xbe ? "true" : "false") << "\n"
             << "  XEMU Paths: " << (settings.xemu_paths ? "true" : "false") << XGDLog::Endl;
}