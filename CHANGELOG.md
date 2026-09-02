# Changelog

All notable changes to this project will be documented in this file.

## [1.1.0] - 2026-09-02

### Summary
Version 1.1.0 is a major maintenance and reliability release by **Ashnar2602**, bringing critical stability fixes backported from the Android port (`xgdtool-android`), integrating community upstream pull requests, adding automatic file logging, and fixing modern wxWidgets 3.2+/3.3+ GUI assertions.

---

### Added
- **Real-time Total Batch Progress Bar**:
  - The total progress gauge now advances continuously and smoothly in real time proportionally to the active file's progress (e.g. 10 files with file 1 at 50% = 5% total progress), instead of only stepping upon whole file completion.
- **Dynamic Window Auto-fitting on Language Change**:
  - Automatically recalculates layout and resizes/expands the main frame when switching between languages so longer strings in Italian/English are never truncated and no manual window resizing is needed.
- **XML-Based Localization System & Language Selectors (GUI & CLI)**:
  - Implemented `LocalizationManager` for extensible, non-hardcoded translations using XML resource files.
  - **Embedded Single-File Executable**: All 6 language translation XML resources are embedded directly into the executable binary ([`EmbeddedLanguages.h`](file:///c:/Progetti/XGD_Tools_Merge/XGDTool/XGDTool/src/Utils/EmbeddedLanguages.h)), enabling a true portable single-file binary with zero external asset dependencies, while still allowing optional external `languages/<lang>.xml` file overrides.
  - Added full translation support for **6 languages**: English (`en`), Italian (`it`), German (`de`), French (`fr`), Spanish (`es`), and Portuguese (`pt`).
  - Added **"Language"** section in GUI (positioned to the right of Settings and above the action buttons) with runtime switching between **System** (default) and all 6 languages, instantly updating all labels, buttons, tooltips, and dialogs without restart.
  - Automatic detection of OS system language across Windows Win32 API (`GetUserDefaultUILanguage`, `GetUserDefaultLocaleName`), wxLocale, and POSIX environment variables (`LANG`, `LC_ALL`).
  - Added `--lang` / `--language` flag in CLI mode (e.g. `--lang it`, `--lang de`, `--lang fr`, `--lang es`, `--lang pt`, `--lang en`, `--lang system`), translating `--help`, flag descriptions, option group titles, error messages, and progress outputs accordingly.
  - Parameterized string interpolation (`{0}`, `{1}`, etc.).
- **Windows Toast / Balloon System Notifications (`wxNotificationMessage`)**:
  - Silent, non-intrusive bottom-right toast notification when processing completes (even if app is in background or minimized).
  - Summarizes batch results (e.g., *"10 di 10 riusciti"* or *"9 di 10 riusciti, 1 fallito"*).
- **"Keep Original Name" Setting (`--keep-name`)**:
  - Added setting option in GUI and `--keep-name` CLI flag to preserve original source filenames and folder stems.
  - Prevents multi-disc games (such as *MagnaCarta 2 (Disk 1)* and *MagnaCarta 2 (Disk 2)*) from colliding and overwriting each other due to identical internal title headers / database lookups.
- **Completion Summary Dialog (`CompletionDialog`)**:
  - Displays detailed status upon task completion.
  - Dynamically shows an **"Apri File di Log" / "Open Log File"** button *only* when errors or failed inputs occurred, opening `xgdtool.log` directly.
- **Persistent Timestamped File Logging (`xgdtool.log`)**:
  - Implemented automatic, thread-safe file logging with `[YYYY-MM-DD HH:MM:SS]` timestamps and log levels (`[INFO]`, `[DEBUG]`, `[ERROR]`).
  - Added comprehensive dump of user batch conversion settings (`log_output_settings`) on start of processing.
  - Exception traces and conversion progress are now persisted to disk for easy debugging.
- **cURL Connection & Transfer Timeouts** in `TitleHelper`:
  - Added connect timeouts (3–5s) and total operation timeouts (5–15s) with connection test logging to prevent network hangs on unstable connections.

---

### Fixed
- **Silent Stream Truncation & State in `SplitIFStream`**:
  - Added `stream.clear()` before seeking to clear `eofbit`/`failbit`.
  - Added automatic stream position recovery when `stream_pos < 0` (preventing unsigned wraparound when `tellg()` returns `-1` on EOF).
- **Modern C++20 Endianness Detection in `EndianUtils.h`**:
  - Replaced legacy runtime pointer checks with standard `<bit>` header (`std::endian::native == std::endian::big`), fixing undefined behavior across modern compilers.
- **Enhanced `XisoReader` Read Failure Diagnostics**:
  - `read_bytes` now outputs detailed context on I/O failures (file offset, requested bytes, received bytes, and total file size).
- **`XGDException` Message Propagation**:
  - Fixed `XGDException::what()` returning empty strings by assigning `full_message_ = error_message;` and routed exceptions directly to `XGDLog(Error)`.
- **wxWidgets 3.2+/3.3+ `wxTextCtrl` Assertion in GUI**:
  - Replaced invalid `SetLabel()` / `GetLabel()` calls on `status_field_` (`wxTextCtrl`) with `ChangeValue()` / `GetValue()`, eliminating debug alert assertion popups on startup and stage updates.

---

### Merged Upstream PRs
- **PR #10 (Linux Build & Modern Standard Headers)**:
  - Added missing `#include <algorithm>` in `CCIWriter.cpp` and `CSOWriter.cpp`.
  - Added missing `#include <cstdint>` in `StringUtils.h` and `XGDLog.h`.
  - Updated CMake Linux dependency check to `pkg_check_modules(LZ4 REQUIRED liblz4)`.
- **PR #12 (Exception Handling & Network Diagnostics)**:
  - Incorporated exception message fixes, curl timeouts, and log flushing improvements.
