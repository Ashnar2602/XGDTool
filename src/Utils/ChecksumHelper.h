#ifndef _CHECKSUM_HELPER_H_
#define _CHECKSUM_HELPER_H_

#include <cstdint>
#include <string>
#include <vector>
#include <filesystem>
#include <fstream>
#include <sstream>
#include <iomanip>
#include <openssl/evp.h>
#include <zlib.h>

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
        crc_ = crc32(0L, Z_NULL, 0);
        active_ = true;
    }

    void update(const char* data, size_t size)
    {
        if (!active_ || size == 0) return;
        crc_ = crc32(crc_, reinterpret_cast<const Bytef*>(data), static_cast<uInt>(size));
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
        res.crc32 = static_cast<uint32_t>(crc_);
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
    uLong crc_{0};
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
