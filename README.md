# XGDTool (v1.3.0 Fork)

> **Fork maintainer**: [Ashnar2602](https://github.com/Ashnar2602) | **Original Author**: [WiredOpposite](https://github.com/wiredopposite/XGDTool)  
> See [CHANGELOG.md](CHANGELOG.md) for full details on version updates, high-performance engine overhaul, multi-disc protection, multithreading, and localization.

XGDTool is an OG Xbox and Xbox 360 disc utility, capable of converting discs to and from any mainstream format at ultra-high speeds. It is available as a portable GUI or CLI application with zero external runtime dependencies.

## What's New in v1.3.0: High-Performance Engine
- **2 MB Chunked Sequential I/O**: Reduced disc I/O syscalls by over 99.9% for both ISO creation and extraction.
- **Zero-Overhead Streaming Checksums**: Simultaneous CRC32, MD5, and hardware-accelerated SHA-1 calculated in RAM on the fly (0.0s extra time).
- **Lock-Free Multithreaded CSO & CCI Engine**: Eliminates mutex contention and promise heap allocations, saturating all CPU cores.
- **Parallel Multi-Core ZArchive (ZAR) Compression**: Utilizes all CPU threads with dedicated Zstd contexts, plus configurable compression levels (1–6).
- **Contiguous Chunked GoD Engine**: Up to 816 KB per write syscall, eliminating 4 million individual 2 KB seek/write operations.
- **Parallel Chunked Readers**: High-speed multi-sector reading and multithreaded decompression for CSO, CCI, and GoD inputs.

## Key Features
- **Seamless Format Conversion**:
    - ISO / XISO
    - Extracted files (Xex / Xbe / HDD Ready)
    - GoD / Games on Demand
    - CCI
    - CSO
    - ZAR
- **Drag & Drop (GUI)**:
    - Drag files or directories directly onto the window to queue them instantly.
- **Advanced File Queue & Status**:
    - 3-column table (`Format`, `Filename`, `Status`). Right-click menu and `Delete` key support to manage queue items.
- **Dark Theme (GUI)**:
    - Built-in Dark Mode theme toggle with modern dark palette.
- **Auto `.dvd` File & LayerBreak Generation (`--dvd`)**:
    - Automatically calculates LayerBreak (`2133520` for XGD3, `1913760` for XGD2 / OG Xbox) and writes companion `.dvd` files for burning with ImgBurn.
- **CRC32 / MD5 / SHA-1 Checksum Calculation (`--checksum`)**:
    - Stream-calculated integrity verification logged directly in diagnostics.
- **Adjustable Compression Levels (`-l` / `--level`)**:
    - Configurable compression presets (`Default`, `Fast`, `Balanced`, `Maximum`) for CCI, CSO, and ZAR.
- **Multi-threaded Batch Processing (`-j` / `-t` / `--threads`)**:
    - Parallel worker thread pool to convert multiple disc images simultaneously.
- **Multilingual Support (GUI & CLI)**:
    - Native support for **6 languages**: English (`en`), Italian (`it`), German (`de`), French (`fr`), Spanish (`es`), and Portuguese (`pt`).
    - Automatic OS system language detection on startup.
    - Real-time language switching from the GUI with automatic window auto-fitting.
    - CLI `--lang` / `--language` option to localize all console output, group headers, and help menus.
- **Multi-disc Protection ("Keep Original Name" / `--keep-name`)**:
    - Prevents multi-disc games from colliding or overwriting each other by retaining source filenames.
- **Enhanced Batch Processing & Real-time Progress**:
    - Real-time combined progress tracking for batch operations across all files in progress.
- **Windows System Toast & Balloon Notifications**:
    - Silent completion notification in Windows taskbar tray when processing finishes.
- **Completion Summary & Diagnostic Dialog**:
    - Clear recap of processed vs failed files with a direct "Open Log File" button if errors occurred.
- **Automatic Diagnostics File Logging**:
    - Writes timestamped diagnostic logs automatically to `xgdtool.log` in the application directory.
- **Disc Optimization**:
    - Image scrubbing ("Partial Scrub"), removes random padding and trims the output file.
    - Image reauthoring ("Full Scrub"), completely rewrites disc structure for the smallest file size.
    - Image authoring, packs extracted game files into a new image.
- **Automated split file detection** (`name.1.extension`, `name.2.extension`).
- **Target presets** for Xemu, Xenia, OG Xbox, and Xbox 360.
- **Attach XBE generation & Allowed Media patching** for OG Xbox.
- **Online database lookup** for accurate title metadata and file naming (can be disabled).

## CLI Usage
```bash
XGDTool.exe <output_format> <settings_flags> <input_path> [output_directory]
```

or on Linux

```bash
./XGDTool <output_format> <settings_flags> <input_path> [output_directory]
```

*Settings flags and output directory are optional.*

### Output format arguments (mutually exclusive)
- `--extract`   Extracts all files to a directory
- `--xiso`      Creates an Xiso image
- `--god`       Creates a Games on Demand image/directory structure
- `--cci`       Creates a CCI archive (automatically split if too large for Xbox)
- `--cso`       Creates a CSO archive (automatically split if too large for Xbox)
- `--zar`       Creates a ZAR archive
- `--xbe`       Generates an attach XBE file, does not convert the input file
- `--ogxbox`    Automatically choose format and settings for use with OG Xbox
- `--xbox360`   Automatically choose format and settings for use with Xbox 360
- `--xemu`      Automatically choose format and settings for use with Xemu
- `--xenia`     Automatically choose format and settings for use with Xenia

### Information & Localization
- `--lang, --language <code|system>`  Set language (`en`, `it`, `de`, `fr`, `es`, `pt`, `system`)
- `--list`      List contents of input file
- `--version`   Print version information
- `--help`      Print usage information

### Settings flags
These arguments can be stacked:
- `--keep-name`                      Keep original input filename (recommended for multi-disc games).
- `--dvd`                            Generate companion .dvd file with correct LayerBreak for burning.
- `--checksum`                       Calculate CRC32, MD5, and SHA-1 checksums during processing.
- `-l, --level, --compression-level` Set compression level (`0`=Default, `1`=Fast, `2`=Balanced, `3`=Maximum, or 1-19).
- `-j, -t, --threads, --jobs`        Number of parallel jobs/threads for batch conversions (default: 1).
- `--partial-scrub`                  Scrubs and trims the output image, random padding data is removed.
- `--full-scrub`                     Completely reauthor the resulting image for the smallest file possible.
- `--split`                          Splits the resulting XISO file if it's too large for OG Xbox.
- `--rename`                         Patches the title field of resulting XBE files to one found in the database.
- `--attach-xbe`                     Generates an attach XBE file along with the output file.
- `--am-patch`                       Patches the "Allowed Media" field in resulting XBE files.
- `--offline`                        Disables online functionality.
- `--debug`                          Enable debug logging.
- `--quiet`                          Disable all logging except for warnings and errors.

## Build

### Windows
The project uses `vcpkg` to automatically fetch dependencies upon CMake configuration.

```bash
git clone --recursive https://github.com/Ashnar2602/XGDTool.git
cd XGDTool
mkdir build
cd build
```

Configure as GUI: 
```bash
cmake -S .. -B . -G "Visual Studio 17 2022" -A x64
``` 
or as CLI: 
```bash
cmake -S .. -B . -DENABLE_GUI=OFF -G "Visual Studio 17 2022" -A x64
```

Build Release:
```bash
cmake --build . --config Release
```

### Linux
```bash
sudo apt update
sudo apt-get install pkg-config liblz4-dev libzstd-dev libssl-dev libcurl4-openssl-dev libwxgtk3.0-gtk3-dev
```

```bash
git clone --recursive https://github.com/Ashnar2602/XGDTool.git
cd XGDTool
mkdir build
cd build
cmake ..
make -j$(nproc)
```
