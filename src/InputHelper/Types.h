#ifndef _IHTYPES_H_
#define _IHTYPES_H_

#include <cstdint>

#include "XGDLog.h"

enum class Platform { UNKNOWN, OGX, X360 };
enum class FileType { UNKNOWN, CCI, CSO, ISO, ZAR, DIR, GoD, XBE, LIST };
enum class ScrubType { NONE, PARTIAL, FULL };
enum class AutoFormat { NONE, OGXBOX, XBOX360, XEMU, XENIA };

struct OutputSettings 
{
    AutoFormat auto_format{AutoFormat::NONE};
    FileType file_type{FileType::UNKNOWN};
    ScrubType scrub_type{ScrubType::NONE};
    bool split{false};
    bool attach_xbe{false};
    bool allowed_media_patch{false};
    bool offline_mode{false};
    bool keep_name{false};
    bool rename_xbe{false};
    bool xemu_paths{false};
    int compression_level{0};      // 0 = default, 1 = fast, 2 = balanced, 3 = max, or explicit 1-19
    bool generate_dvd{false};      // Auto-generate .dvd with LayerBreak
    bool calculate_checksum{false};// Compute CRC32 / MD5 / SHA-1
    int threads{1};                // Parallel batch jobs
};

#endif // _IHTYPES_H_