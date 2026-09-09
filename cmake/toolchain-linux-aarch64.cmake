# CMake toolchain file for cross-compiling XGDTool for Linux aarch64 (ARM64)
# from an x86_64 host running Ubuntu/Debian.
#
# Prerequisites (install once on the build host):
#   sudo dpkg --add-architecture arm64
#   sudo apt update
#   sudo apt install gcc-aarch64-linux-gnu g++-aarch64-linux-gnu \
#        liblz4-dev:arm64 libzstd-dev:arm64 libssl-dev:arm64 \
#        libcurl4-openssl-dev:arm64
#
# For the GUI variant also install:
#   sudo apt install libwxgtk3.2-dev:arm64
#
# Usage (CLI):
#   cmake -B build_aarch64_cli -S . -DENABLE_GUI=OFF \
#         -DCMAKE_TOOLCHAIN_FILE=cmake/toolchain-linux-aarch64.cmake \
#         -DCMAKE_BUILD_TYPE=Release
#   cmake --build build_aarch64_cli -j$(nproc)
#
# Usage (GUI):
#   cmake -B build_aarch64_gui -S . -DENABLE_GUI=ON \
#         -DCMAKE_TOOLCHAIN_FILE=cmake/toolchain-linux-aarch64.cmake \
#         -DCMAKE_BUILD_TYPE=Release
#   cmake --build build_aarch64_gui -j$(nproc)

set(CMAKE_SYSTEM_NAME Linux)
set(CMAKE_SYSTEM_PROCESSOR aarch64)

# Cross-compiler binaries (provided by gcc-aarch64-linux-gnu package)
set(CMAKE_C_COMPILER   aarch64-linux-gnu-gcc)
set(CMAKE_CXX_COMPILER aarch64-linux-gnu-g++)
set(CMAKE_AR           aarch64-linux-gnu-ar)
set(CMAKE_STRIP        aarch64-linux-gnu-strip)

# Sysroot: Ubuntu multiarch places arm64 libs here
set(AARCH64_SYSROOT_CANDIDATES
    "/usr/aarch64-linux-gnu"
    "/usr/lib/aarch64-linux-gnu"
)
foreach(candidate ${AARCH64_SYSROOT_CANDIDATES})
    if(EXISTS ${candidate})
        set(AARCH64_SYSROOT ${candidate})
        break()
    endif()
endforeach()

set(CMAKE_FIND_ROOT_PATH
    /usr/aarch64-linux-gnu
    /usr/lib/aarch64-linux-gnu
)

# pkg-config must use arm64 libraries, not host x86_64 libraries
set(PKG_CONFIG_EXECUTABLE pkg-config)
set(ENV{PKG_CONFIG_PATH}         "/usr/lib/aarch64-linux-gnu/pkgconfig:/usr/share/pkgconfig")
set(ENV{PKG_CONFIG_SYSROOT_DIR}  "/")
set(ENV{PKG_CONFIG_LIBDIR}       "/usr/lib/aarch64-linux-gnu/pkgconfig:/usr/share/pkgconfig")

# wx-config for aarch64 (if the arm64 wxWidgets dev package provides it)
# The standard multiarch install places it at:
set(wxWidgets_CONFIG_EXECUTABLE /usr/bin/wx-config)

# Search for programs in the BUILD host directories
set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
# Search for libraries and headers in the TARGET directories only
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_PACKAGE ONLY)
