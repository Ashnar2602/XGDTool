# Building XGDTool on Linux

This guide covers building all four Linux variants of XGDTool from source on a Debian/Ubuntu-based system.

## Prerequisites

### Minimum System Requirements

- Linux kernel 4.x or newer
- GCC 11+ or Clang 14+ with C++20 support
- CMake 3.11+
- Git

### Supported Build Hosts

Any Debian/Ubuntu-based distribution can serve as a build host. For maximum binary
compatibility with other distributions, build on Ubuntu 22.04 LTS (glibc 2.35).
Binaries produced this way run on:

- Ubuntu 22.04+ / Debian 11+
- Fedora 35+ / RHEL 9+
- Arch Linux, Manjaro (rolling)
- openSUSE Leap 15.3+
- Pop!_OS 22.04+, Linux Mint 20+
- Raspberry Pi OS 64-bit (aarch64 builds)

---

## Cloning the Repository

```bash
git clone --recurse-submodules https://github.com/Ashnar2602/XGDTool.git
cd XGDTool
```

---

## CLI Build (x86_64)

### Install Dependencies

```bash
sudo apt update
sudo apt install -y cmake ninja-build pkg-config \
    liblz4-dev libzstd-dev libssl-dev libcurl4-openssl-dev
```

### Build

```bash
cmake -B build_cli -S . -DENABLE_GUI=OFF -DCMAKE_BUILD_TYPE=Release -G Ninja
cmake --build build_cli -j$(nproc)
```

### Run

```bash
./build_cli/XGDTool --help
./build_cli/XGDTool --version
```

---

## GUI Build (x86_64)

### Install Dependencies

```bash
sudo apt update
sudo apt install -y cmake ninja-build pkg-config \
    liblz4-dev libzstd-dev libssl-dev libcurl4-openssl-dev \
    libwxgtk3.2-dev
# On Ubuntu 22.04, use: libwxgtk3.0-gtk3-dev
```

### Build

```bash
cmake -B build_gui -S . -DENABLE_GUI=ON -DCMAKE_BUILD_TYPE=Release -G Ninja
cmake --build build_gui -j$(nproc)
```

### Run

```bash
./build_gui/XGDTool
```

### Build AppImage (optional)

After the GUI build, you can package it as a portable AppImage:

```bash
cmake --build build_gui --target appimage
# Output: build_gui/XGDTool-GUI-Linux-x86_64.AppImage
```

The AppImage requires no installation and runs on any compatible Linux distribution.

---

## CLI Build (aarch64 cross-compilation)

Cross-compile for ARM64 from an x86_64 host.

### Install Cross-Compiler and ARM64 Libraries

```bash
sudo dpkg --add-architecture arm64
sudo apt update
sudo apt install -y \
    gcc-aarch64-linux-gnu g++-aarch64-linux-gnu \
    liblz4-dev:arm64 libzstd-dev:arm64 \
    libssl-dev:arm64 libcurl4-openssl-dev:arm64
```

### Build

```bash
cmake -B build_cli_aarch64 -S . \
    -DENABLE_GUI=OFF \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_TOOLCHAIN_FILE=cmake/toolchain-linux-aarch64.cmake \
    -G Ninja
cmake --build build_cli_aarch64 -j$(nproc)
```

### Verify

```bash
file build_cli_aarch64/XGDTool
# Expected: ELF 64-bit LSB executable, ARM aarch64
```

Transfer to an ARM64 device or run with QEMU:

```bash
sudo apt install qemu-user-static
qemu-aarch64-static build_cli_aarch64/XGDTool --help
```

---

## GUI Build (aarch64 cross-compilation)

### Install Additional ARM64 GUI Library

```bash
sudo apt install -y libwxgtk3.0-gtk3-dev:arm64
# Or on newer systems: libwxgtk3.2-dev:arm64
```

### Build

```bash
cmake -B build_gui_aarch64 -S . \
    -DENABLE_GUI=ON \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_TOOLCHAIN_FILE=cmake/toolchain-linux-aarch64.cmake \
    -G Ninja
cmake --build build_gui_aarch64 -j$(nproc)
```

### Verify

```bash
file build_gui_aarch64/XGDTool
# Expected: ELF 64-bit LSB executable, ARM aarch64
```

---

## CMake Build Options

| Option | Default | Description |
|---|---|---|
| `ENABLE_GUI` | `ON` | Build the wxWidgets GUI. Set `OFF` for CLI-only. |
| `ENABLE_APPIMAGE` | `ON` | Enable the `appimage` CMake target (GUI+Unix only). |
| `LINUX_STATIC_LIBS` | `OFF` | Statically link lz4/zstd/openssl/curl for extra portability. |
| `CMAKE_BUILD_TYPE` | `Release` | `Debug` or `Release`. |

Example CLI-only static build:

```bash
cmake -B build_cli_static -S . \
    -DENABLE_GUI=OFF \
    -DLINUX_STATIC_LIBS=ON \
    -DCMAKE_BUILD_TYPE=Release
cmake --build build_cli_static -j$(nproc)
```

---

## Continuous Integration

All four Linux variants are built automatically on every tagged release via GitHub Actions
(`release.yml`). Pre-built binaries are attached to each GitHub Release:

- `XGDTool-CLI-Linux-x86_64`
- `XGDTool-GUI-Linux-x86_64.AppImage`
- `XGDTool-CLI-Linux-aarch64`
- `XGDTool-GUI-Linux-aarch64.AppImage`
