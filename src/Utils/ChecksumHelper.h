#ifndef _CHECKSUM_HELPER_H_
#define _CHECKSUM_HELPER_H_

#include <cstdint>
#include <string>
#include <vector>
#include <filesystem>
#include <fstream>
#include <sstream>
#include <iomanip>
#include <cstring>
#include <openssl/evp.h>

#if (defined(__aarch64__) || defined(_M_ARM64)) && defined(__ARM_FEATURE_CRC32)
#  include <arm_acle.h>
#  define XGD_HAS_ARM_CRC32 1
#endif

namespace Detail {

// Precalculated tables for standard IEEE 802.3 CRC32 (polynomial 0xEDB88320)
// Using Slice-by-8 algorithm for 8 bytes/cycle processing in L1 cache
struct Crc32Slice8Tables {
    uint32_t table[8][256];

    constexpr Crc32Slice8Tables() : table{} {
        constexpr uint32_t POLY = 0xEDB88320u;
        for (uint32_t i = 0; i < 256; ++i) {
            uint32_t c = i;
            for (uint32_t j = 0; j < 8; ++j) {
                c = (c >> 1) ^ ((c & 1) ? POLY : 0);
            }
            table[0][i] = c;
        }
        for (uint32_t i = 0; i < 256; ++i) {
            for (uint32_t j = 1; j < 8; ++j) {
                table[j][i] = (table[j - 1][i] >> 8) ^ table[0][table[j - 1][i] & 0xFF];
            }
        }
    }
};

inline const Crc32Slice8Tables& get_crc32_tables() {
    static constexpr Crc32Slice8Tables tables{};
    return tables;
}

#if defined(XGD_HAS_ARM_CRC32)
inline uint32_t compute_crc32_arm(uint32_t crc, const uint8_t* data, size_t len) {
    crc = ~crc;
    while (len && (reinterpret_cast<uintptr_t>(data) & 7)) {
        crc = __crc32b(crc, *data++);
        --len;
    }
    while (len >= 8) {
        uint64_t v;
        std::memcpy(&v, data, sizeof(v));
        crc = __crc32d(crc, v);
        data += 8;
        len -= 8;
    }
    if (len >= 4) {
        uint32_t v;
        std::memcpy(&v, data, sizeof(v));
        crc = __crc32w(crc, v);
        data += 4;
        len -= 4;
    }
    if (len >= 2) {
        uint16_t v;
        std::memcpy(&v, data, sizeof(v));
        crc = __crc32h(crc, v);
        data += 2;
        len -= 2;
    }
    if (len) {
        crc = __crc32b(crc, *data);
    }
    return ~crc;
}
#endif

inline uint32_t compute_crc32_slice8(uint32_t crc, const uint8_t* data, size_t len) {
    const auto& tbl = get_crc32_tables().table;
    crc = ~crc;

    // Align to 8-byte boundary
    while (len && (reinterpret_cast<uintptr_t>(data) & 7)) {
        crc = (crc >> 8) ^ tbl[0][(crc & 0xFF) ^ *data++];
        --len;
    }

    // Process 8 bytes per iteration (Slice-by-8)
    while (len >= 8) {
        uint64_t val;
        std::memcpy(&val, data, sizeof(val));
        data += 8;

        uint32_t low = static_cast<uint32_t>(val) ^ crc;
        uint32_t high = static_cast<uint32_t>(val >> 32);

        crc = tbl[7][low & 0xFF] ^
              tbl[6][(low >> 8) & 0xFF] ^
              tbl[5][(low >> 16) & 0xFF] ^
              tbl[4][(low >> 24) & 0xFF] ^
              tbl[3][high & 0xFF] ^
              tbl[2][(high >> 8) & 0xFF] ^
              tbl[1][(high >> 16) & 0xFF] ^
              tbl[0][(high >> 24) & 0xFF];

        len -= 8;
    }

    // Trailing bytes
    while (len--) {
        crc = (crc >> 8) ^ tbl[0][(crc & 0xFF) ^ *data++];
    }

    return ~crc;
}

inline uint32_t compute_crc32(uint32_t crc, const void* buffer, size_t size) {
    if (!buffer || size == 0) return crc;
    const uint8_t* data = reinterpret_cast<const uint8_t*>(buffer);
#if defined(XGD_HAS_ARM_CRC32)
    return compute_crc32_arm(crc, data, size);
#else
    return compute_crc32_slice8(crc, data, size);
#endif
}

} // namespace Detail

struct ChecksumResult
{
    uint32_t crc32{0};
    std::string md5;
    std::string sha1;
    bool valid{false};
};

class StreamingChecksum
{
public:
    StreamingChecksum() = default;

    void init()
    {
        md5_ctx_ = EVP_MD_CTX_new();
        sha1_ctx_ = EVP_MD_CTX_new();
        EVP_DigestInit_ex(md5_ctx_, EVP_md5(), nullptr);
        EVP_DigestInit_ex(sha1_ctx_, EVP_sha1(), nullptr);
        crc_ = 0;
        active_ = true;
    }

    void update(const char* data, size_t size)
    {
        if (!active_ || size == 0) return;
        crc_ = Detail::compute_crc32(crc_, data, size);
        EVP_DigestUpdate(md5_ctx_, data, size);
        EVP_DigestUpdate(sha1_ctx_, data, size);
    }

    ChecksumResult finalize()
    {
        if (!active_) return {};

        unsigned char md5_digest[EVP_MAX_MD_SIZE];
        unsigned int md5_len = 0;
        EVP_DigestFinal_ex(md5_ctx_, md5_digest, &md5_len);
        EVP_MD_CTX_free(md5_ctx_);
        md5_ctx_ = nullptr;

        unsigned char sha1_digest[EVP_MAX_MD_SIZE];
        unsigned int sha1_len = 0;
        EVP_DigestFinal_ex(sha1_ctx_, sha1_digest, &sha1_len);
        EVP_MD_CTX_free(sha1_ctx_);
        sha1_ctx_ = nullptr;
        active_ = false;

        auto to_hex = [](const unsigned char* d, unsigned int l) {
            std::ostringstream oss;
            oss << std::hex << std::setfill('0');
            for (unsigned int i = 0; i < l; ++i) {
                oss << std::setw(2) << static_cast<int>(d[i]);
            }
            return oss.str();
        };

        ChecksumResult res;
        res.crc32 = crc_;
        res.md5 = to_hex(md5_digest, md5_len);
        res.sha1 = to_hex(sha1_digest, sha1_len);
        res.valid = true;
        return res;
    }

    bool is_active() const { return active_; }

    ~StreamingChecksum()
    {
        if (md5_ctx_) EVP_MD_CTX_free(md5_ctx_);
        if (sha1_ctx_) EVP_MD_CTX_free(sha1_ctx_);
    }

private:
    EVP_MD_CTX* md5_ctx_{nullptr};
    EVP_MD_CTX* sha1_ctx_{nullptr};
    uint32_t crc_{0};
    bool active_{false};
};

inline ChecksumResult calculate_file_checksums(const std::filesystem::path& file_path)
{
    std::ifstream is(file_path, std::ios::binary);
    if (!is.is_open()) return {};

    StreamingChecksum chk;
    chk.init();

    // 4MB buffer for fast sequential reading utilizing OS page cache
    constexpr size_t BUF_SIZE = 4 * 1024 * 1024;
    std::vector<char> buffer(BUF_SIZE);
    while (is.read(buffer.data(), buffer.size()) || is.gcount() > 0)
    {
        chk.update(buffer.data(), static_cast<size_t>(is.gcount()));
    }

    return chk.finalize();
}

#endif // _CHECKSUM_HELPER_H_
