# AGENTS.md — Development Guidelines & Operational Rules for XGDTool

This document defines the architecture, design principles, performance rules, build procedures, and operational guidelines for any AI agent, assistant (Codex, Antigravity, Claude, Copilot), or developer working on this codebase.

---

## 1. Project Overview & Context

* **Project**: **XGDTool** (High-Performance Fork by **Ashnar2602**)
* **Repository Root**: `XGDTool/`
* **Language & Standard**: C++17, CMake
* **Primary Platform**: Windows (MSVC 2022 / x64), portable standalone executables
* **Targets**:
  * `XGDTool-GUI`: Full wxWidgets-based GUI application with Drag & Drop, Dark Theme, Queue management, and embedded multilingual support.
  * `XGDTool-CLI`: Lean, standalone command-line executable for terminal scripting, automation, and batch processing.
  * `XGDTool-Android`: Native Android application with Material 3 UI, Xbox neon-green dark theme, SAF storage integration, background conversion service, and 7-language localization.
* **Supported Formats**:
  * **ISO / XISO**: Xbox and Xbox 360 disc images (including XGD2 / XGD3, layerbreak handling, and scrubbed zero-padding).
  * **CSO**: Compressed ISO format using LZ4.
  * **CCI**: Compressed disc image format using LZ4 with custom block allocation.
  * **GoD (Games on Demand)**: Xbox 360 Games on Demand container packages with Master Hash Tables (MHT) and Sub-Hash Tables (SHT).
  * **ZAR (ZArchive)**: High-ratio compressed archive format (Zstandard), compatible with Cemu / emulators.
  * **Folder Extraction**: Direct extraction of ISO/XISO filesystem contents.

---

## 2. Core Architectural & Performance Rules (MANDATORY)

Every modification to this codebase must adhere strictly to the following performance and design mandates established in the **v1.3.0 High-Performance Engine Overhaul**:

### Rule 1: Zero Micro-I/O (Batch / Chunked Operations Only)
* **Never** perform single-sector (2 KB) reads or writes in loops. Micro-syscall loops cause massive kernel context switching and thrash modern NVMe/SSD controllers.
* Always use **2 MB sequential chunking** (`CHUNK_SECTORS = 1024` or `XGD::BUFFER_SIZE = 2 * 1024 * 1024`).
* For **GoD**, use contiguous block batching via `get_contiguous_sectors()` (up to 816 KB / 408 sectors per syscall).
* For Sub-Hash Tables (SHT), read all 204 blocks (816 KB) in a single read syscall into RAM before hashing.
* For OG Xbox scrubbed regions, zero out the memory buffer in RAM instead of issuing disk seeks (`seekp`/`seekg`).

### Rule 2: Zero-Overhead Streaming Checksums
* **Never** re-read an 8 GB disc from disk after writing just to calculate checksums.
* All checksums (CRC32, MD5, and hardware-accelerated SHA-1 via SHA-NI) must be accumulated on the fly in RAM using `ChecksumHelper` while sectors/buffers are written to disk.
* Checksum verification overhead during creation must remain **0.0 seconds**.

### Rule 3: Lock-Free Multithreaded Worker Pools
* In compression (`CSOWriter`, `CCIWriter`), **never** use per-sector mutex queues, lock contention, or individual `std::promise` heap allocations.
* Use coarse-grained 2 MB batch coordinators (`BatchContext`) with atomic work-stealing (`fetch_add`) in slices of 8 sectors.
* Preallocate batch compression buffers and result containers on the writer instance to eliminate per-batch heap thrashing.

### Rule 4: Zstd / ZArchive Multithreading & Submodule Patch Integrity
* `external/ZArchive` is maintained as a git submodule.
* The multithreaded Zstd engine is applied via `cmake/zarchive_multithread.patch`.
* **Never** commit raw uncontrolled edits inside the submodule directory. Always update the patch file and ensure [CMakeLists.txt](file:///c:/Progetti/XGD_Tools_Merge/XGDTool/XGDTool/CMakeLists.txt) applies it cleanly during configuration.
* Decompression speed in emulators is ultra-fast (~2 GB/s) across all compression levels (Levels 1 to 6).

### Rule 5: 100% Bit-for-Bit Integrity & Compatibility
* Optimizations must **never** alter the underlying data format, file headers, padding alignment, hash hierarchies, or encryption boundaries.
* Images produced must remain 100% compatible with Xbox hardware, burning tools (ImgBurn with `.dvd` LayerBreak), and emulators (Xenia, Cemu).

---

## 3. Directory Layout & Key Files

```
XGDTool/
├── src/
│   ├── XGD.h                           <-- Central versioning (XGDTOOL_VERSION), constants, and sizes
│   ├── Utils/
│   │   └── ChecksumHelper.h            <-- Zero-overhead streaming CRC32, MD5, SHA-1 (SHA-NI)
│   ├── ImageReader/
│   │   ├── ImageReader.h / .cpp        <-- Base reader with read_sectors() chunking
│   │   ├── XisoReader/                 <-- ISO/XISO multi-sector sequential reader
│   │   ├── CSOReader/                  <-- LZ4 parallel multi-threaded block reader
│   │   ├── CCIReader/                  <-- LZ4 parallel multi-threaded chunk reader
│   │   └── GoDReader/                  <-- Games on Demand contiguous block reader
│   ├── ImageWriter/
│   │   ├── ImageWriter.h / .cpp        <-- Base writer with streaming checksum hooks
│   │   ├── XisoWriter/                 <-- 2 MB chunked ISO writer with in-memory scrubbing
│   │   ├── CSOWriter/                  <-- Lock-free multithreaded batch CSO engine
│   │   ├── CCIWriter/                  <-- Lock-free multithreaded batch CCI engine
│   │   ├── GoDWriter/                  <-- Contiguous chunked GoD engine + batch SHT hashing
│   │   └── ZARWriter/                  <-- Multithreaded Zstd ZArchive engine
│   ├── ImageExtractor/                 <-- ISO filesystem extraction with 2 MB stream buffers
│   ├── Resources/
│   │   └── i18n.h                      <-- Embedded multilingual XML resources (EN, IT, ES, FR, DE, etc.)
│   └── GUI/                            <-- wxWidgets GUI (Dark theme, D&D, Queue manager)
├── android/                            <-- Android Studio project (Gradle 8.7+, Kotlin UI, SAF service)
│   ├── app/                            <-- Android app module, layouts, and prebuilt/packaged jniLibs
│   └── jni/                            <-- Native JNI bridge (xgd_jni.cpp, XGDLog_JNI.cpp, CMakeLists.txt)
├── docs/                               <-- Comprehensive user manuals in 7 languages (EN, IT, ES, DE, FR, PT, ZH)
├── dist/                               <-- Release binaries (GUI, CLI, Android APK) & notes
├── cmake/
│   └── zarchive_multithread.patch      <-- Patch for parallel multi-core ZArchive compression
├── CMakeLists.txt                      <-- Master CMake configuration (supports -DBUILD_CLI_ONLY)
├── CHANGELOG.md                        <-- Version history and release documentation
└── README.md                           <-- Project landing page and quick start
```

---

## 4. Build, Test & Release Workflow

### Build Commands

* **Build GUI (Windows / Release)**:
  ```powershell
  cmake -B build -S XGDTool
  cmake --build build --config Release -j
  ```
  Executable: `build/Release/XGDTool-GUI.exe`

* **Build CLI (Windows / Release)**:
  ```powershell
  cmake -B build_cli -S XGDTool -DBUILD_CLI_ONLY=ON
  cmake --build build_cli --config Release -j
  ```
  Executable: `build_cli/Release/XGDTool-CLI.exe`

* **Build Android APK (Release)**:
  ```powershell
  cd XGDTool/android
  ./gradlew assembleRelease
  ```
  Package: `android/app/build/outputs/apk/release/app-release.apk`

### Release Packaging Protocol
When publishing a new release:
1. **Bump Version**: Update `XGDTOOL_VERSION` and `XGDTOOL_DATE` in `src/XGD.h`.
2. **Update Changelog**: Add a structured entry in `CHANGELOG.md` detailing new features, optimizations, and fixes.
3. **Update Release Notes**: Refresh `dist/release_notes.md` and `README.md`.
4. **Compile Both Targets**: Build both GUI and CLI in `Release` configuration with 0 errors and 0 warnings.
5. **Copy to Dist**: Copy binaries to `dist/XGDTool-GUI.exe` and `dist/XGDTool-CLI.exe`.
6. **Git Tag & Push**:
   ```powershell
   git -C XGDTool add .
   git -C XGDTool commit -m "Release vX.Y.Z: ..."
   git -C XGDTool push origin master
   git -C XGDTool tag vX.Y.Z
   git -C XGDTool push origin vX.Y.Z
   ```
7. **Create GitHub Release**: Create release using `gh release create vX.Y.Z dist/XGDTool-GUI.exe dist/XGDTool-CLI.exe --notes-file dist/release_notes.md`.

---

## 5. Agent Operational Rules (When Resuming or Starting a Session)

1. **Check Current State**:
   Always run `git status` and `git log -n 5` in the repository to verify current branch, recent commits, and any uncommitted changes before taking action.
2. **Preserve Integrity**:
   Do not delete or comment out existing features, error handling, translations, or format validations unless explicitly requested.
3. **Keep High-Throughput Buffers**:
   Never introduce synchronous single-sector reads/writes. If adding a new feature or format, design it for chunked streaming I/O from day one.
4. **Verify Build After Modifying Code**:
   Always run CMake build after C++ source changes to verify compilation before concluding the task.
