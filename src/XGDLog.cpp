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

static std::string format_eta_time(uint32_t seconds)
{
    uint32_t s = seconds % 60;
    uint32_t m = (seconds / 60) % 60;
    uint32_t h = seconds / 3600;
    std::ostringstream oss;
    if (h > 0)
    {
        oss << std::setfill('0') << std::setw(2) << h << ":"
            << std::setfill('0') << std::setw(2) << m << ":"
            << std::setfill('0') << std::setw(2) << s;
    }
    else
    {
        oss << std::setfill('0') << std::setw(2) << m << ":"
            << std::setfill('0') << std::setw(2) << s;
    }
    return oss.str();
}

void XGDLog::print_progress(uint64_t processed, uint64_t total) 
{
    static bool should_print = (current_level != Error);
    static auto last_update_time = std::chrono::steady_clock::now();
    static auto start_time = std::chrono::steady_clock::now();
    static uint64_t last_total = 0;

    if (!should_print || total == 0) 
    {
        return;
    }

    auto now = std::chrono::steady_clock::now();
    if (processed == 0 || total != last_total)
    {
        start_time = now;
        last_total = total;
    }

    auto duration_since_last_update = std::chrono::duration_cast<std::chrono::milliseconds>(now - last_update_time);
    if (duration_since_last_update.count() < 100 && processed < total) 
    {
        return;
    }
    last_update_time = now;

    const int bar_width = 36;
    float progress = static_cast<float>(processed) / total;
    if (progress > 1.0f) progress = 1.0f;

    uint64_t bytes_proc = (total < 10000000) ? (processed * 2048) : processed;
    uint64_t bytes_tot = (total < 10000000) ? (total * 2048) : total;

    auto elapsed_ms = std::chrono::duration_cast<std::chrono::milliseconds>(now - start_time).count();
    double mb_per_sec = 0.0;
    uint32_t eta_sec = 0;
    if (elapsed_ms >= 300 && bytes_proc > 0)
    {
        double sec = elapsed_ms / 1000.0;
        double speed = static_cast<double>(bytes_proc) / sec;
        mb_per_sec = speed / (1024.0 * 1024.0);
        if (speed > 0 && bytes_tot > bytes_proc)
        {
            eta_sec = static_cast<uint32_t>((bytes_tot - bytes_proc) / speed);
        }
    }

    std::cout << "\r[";
    int pos = static_cast<int>(bar_width * progress);
    for (int i = 0; i < bar_width; ++i) 
    {
        if (i < pos) std::cout << "=";
        else if (i == pos) std::cout << ">";
        else std::cout << " ";
    }
    std::cout << "] " << std::setw(6) << std::fixed << std::setprecision(2) << (progress * 100.0) << "% ("
              << std::fixed << std::setprecision(1) << mb_per_sec << " MB/s | ETA: " << format_eta_time(eta_sec) << ")   ";
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
    static auto start_time = std::chrono::steady_clock::now();
    static uint64_t last_total = 0;

    auto now = std::chrono::steady_clock::now();
    if (processed == 0 || total != last_total)
    {
        start_time = now;
        last_total = total;
    }

    auto duration_since_last_update = std::chrono::duration_cast<std::chrono::milliseconds>(now - last_update_time);
    if (duration_since_last_update.count() < 100 && processed < total) 
    {
        return;
    }
    last_update_time = now;

    uint64_t bytes_proc = (total < 10000000) ? (processed * 2048) : processed;
    uint64_t bytes_tot = (total < 10000000) ? (total * 2048) : total;

    auto elapsed_ms = std::chrono::duration_cast<std::chrono::milliseconds>(now - start_time).count();
    double mb_per_sec = 0.0;
    uint32_t eta_sec = 0;
    if (elapsed_ms >= 300 && bytes_proc > 0)
    {
        double sec = elapsed_ms / 1000.0;
        double speed = static_cast<double>(bytes_proc) / sec;
        mb_per_sec = speed / (1024.0 * 1024.0);
        if (speed > 0 && bytes_tot > bytes_proc)
        {
            eta_sec = static_cast<uint32_t>((bytes_tot - bytes_proc) / speed);
        }
    }

    MainFrame::update_progress_bar(processed, total, mb_per_sec, eta_sec);
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