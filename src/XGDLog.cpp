#include <iomanip>
#include <chrono>
#include <fstream>
#include <mutex>
#include <ctime>

#include "XGDLog.h"

LogLevel XGDLog::current_level = Normal;

static std::mutex g_log_mutex;

static void write_to_log_file(LogLevel level, const std::string& msg)
{
    std::lock_guard<std::mutex> lock(g_log_mutex);
    static std::ofstream log_file("xgdtool.log", std::ios::app);
    if (!log_file.is_open()) return;

    auto now = std::chrono::system_clock::now();
    std::time_t now_time = std::chrono::system_clock::to_time_t(now);
    std::tm tm_buf;
#ifdef _WIN32
    localtime_s(&tm_buf, &now_time);
#else
    localtime_r(&now_time, &tm_buf);
#endif

    const char* level_str = "INFO";
    if (level == LogLevel::Error) level_str = "ERROR";
    else if (level == LogLevel::Debug) level_str = "DEBUG";

    log_file << std::put_time(&tm_buf, "[%Y-%m-%d %H:%M:%S] ")
             << "[" << level_str << "] "
             << msg;
    if (msg.empty() || msg.back() != '\n') {
        log_file << "\n";
    }
    log_file.flush();
}

#ifndef ENABLE_GUI

XGDLog& XGDLog::operator<<(Manip manip) 
{
    if (manip == Manip::Endl && should_log()) 
    {
        std::string s = oss.str();
        write_to_log_file(log_level, s);
        std::cerr << s << std::endl;
        oss.str("");  // Clear the stream after flushing
        oss.clear();
    }
    return *this;
}

void XGDLog::print_progress(uint64_t processed, uint64_t total) 
{
    static bool should_print = (current_level != Error);
    static auto last_update_time = std::chrono::steady_clock::now();

    if (!should_print) 
    {
        return;
    }

    const int bar_width = 50;
    float progress = static_cast<float>(processed) / total;

    auto now = std::chrono::steady_clock::now();
    auto duration_since_last_update = std::chrono::duration_cast<std::chrono::milliseconds>(now - last_update_time);

    if (duration_since_last_update.count() < 100 && processed < total) 
    {
        return;
    }

    last_update_time = now;

    std::cout << "\r[";

    int pos = static_cast<int>(bar_width * progress);
    for (int i = 0; i < bar_width; ++i) 
    {
        if (i < pos) std::cout << "=";
        else if (i == pos) std::cout << ">";
        else std::cout << " ";
    }

    std::cout << "] " << std::setw(6) << std::fixed << std::setprecision(2) << (progress * 100.0) << "%";
    std::cout.flush();

    if (processed >= total) 
    {
        std::cout << std::endl;
    }
}

#else // ENABLE_GUI

#include "GUI/MainFrame.h"

void XGDLog::print_progress(uint64_t processed, uint64_t total) 
{
    static auto last_update_time = std::chrono::steady_clock::now();

    auto now = std::chrono::steady_clock::now();
    auto duration_since_last_update = std::chrono::duration_cast<std::chrono::milliseconds>(now - last_update_time);

    if (duration_since_last_update.count() < 100 && processed < total) 
    {
        return;
    }

    last_update_time = now;

    MainFrame::update_progress_bar(processed, total);
}

XGDLog& XGDLog::operator<<(Manip manip) 
{
    if (manip == Manip::Endl && should_log()) 
    {
        std::string s = oss.str();
        write_to_log_file(log_level, s);
        MainFrame::update_status_field(s);
        oss.str(""); 
        oss.clear();
    }
    return *this;
}

#endif // ENABLE_GUI