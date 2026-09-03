#ifndef _IO_HINTS_H_
#define _IO_HINTS_H_

#include <filesystem>
#include <cstdint>

#if defined(_WIN32)
#  ifndef NOMINMAX
#    define NOMINMAX
#  endif
#  ifndef WIN32_LEAN_AND_MEAN
#    define WIN32_LEAN_AND_MEAN
#  endif
#  include <windows.h>
#elif defined(__linux__) || defined(__ANDROID__)
#  include <fcntl.h>
#  include <unistd.h>
#endif

namespace IOHints {

// Advise the operating system kernel that the file will be read sequentially
inline void hint_sequential_read(const std::filesystem::path& path) {
#if defined(_WIN32)
    HANDLE h = CreateFileW(
        path.c_str(),
        GENERIC_READ,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        NULL,
        OPEN_EXISTING,
        FILE_FLAG_SEQUENTIAL_SCAN,
        NULL
    );
    if (h != INVALID_HANDLE_VALUE) {
        CloseHandle(h);
    }
#elif defined(__linux__) || defined(__ANDROID__)
    int fd = open(path.c_str(), O_RDONLY | O_CLOEXEC);
    if (fd >= 0) {
        posix_fadvise(fd, 0, 0, POSIX_FADV_SEQUENTIAL);
        posix_fadvise(fd, 0, 0, POSIX_FADV_WILLNEED);
        close(fd);
    }
#endif
}

// Advise the operating system kernel that sequential write is finished and pages can be freed
inline void hint_sequential_write_done(const std::filesystem::path& path) {
#if defined(__linux__) || defined(__ANDROID__)
    int fd = open(path.c_str(), O_RDONLY | O_CLOEXEC);
    if (fd >= 0) {
        posix_fadvise(fd, 0, 0, POSIX_FADV_DONTNEED);
        close(fd);
    }
#endif
}

// Preallocate contiguous space on disk to eliminate filesystem fragmentation
inline void preallocate_file(const std::filesystem::path& path, uint64_t size) {
    if (size == 0) return;
#if defined(__linux__) || defined(__ANDROID__)
    int fd = open(path.c_str(), O_WRONLY | O_CLOEXEC);
    if (fd >= 0) {
        posix_fallocate(fd, 0, static_cast<off_t>(size));
        close(fd);
        return;
    }
#endif
    std::error_code ec;
    std::filesystem::resize_file(path, size, ec);
}

} // namespace IOHints

#endif // _IO_HINTS_H_
