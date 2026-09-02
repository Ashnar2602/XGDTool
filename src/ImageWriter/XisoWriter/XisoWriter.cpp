#include <algorithm>
#include <cstring>

#include "ImageWriter/XisoWriter/XisoWriter.h"
#include "AvlTree/AvlTree.h"

XisoWriter::XisoWriter(std::shared_ptr<ImageReader> image_reader, ScrubType scrub_type, const bool split, const bool calculate_checksum) 
    : image_reader_(image_reader), scrub_type_(scrub_type), split_(split), calculate_checksum_(calculate_checksum) {}

XisoWriter::XisoWriter(const std::filesystem::path& in_dir_path, const bool split, const bool calculate_checksum) 
    : split_(split), in_dir_path_(in_dir_path), calculate_checksum_(calculate_checksum) {}

std::vector<std::filesystem::path> XisoWriter::convert(const std::filesystem::path& out_xiso_path) 
{
    create_directory(out_xiso_path.parent_path());

    if (image_reader_ && scrub_type_ == ScrubType::FULL) //Full scrub ISO
    {
        AvlTree avl_tree(image_reader_->name(), image_reader_->directory_entries());
        return convert_to_xiso_from_avl(avl_tree, out_xiso_path);
    }
    else if (!in_dir_path_.empty()) //Create ISO from directory
    {
        AvlTree avl_tree(in_dir_path_.string(), in_dir_path_);
        return convert_to_xiso_from_avl(avl_tree, out_xiso_path);
    }

    if (!image_reader_) 
    {
        throw XGDException(ErrCode::MISC, HERE(), "No image reader or directory path specified");
    }

    return convert_to_xiso(out_xiso_path, scrub_type_ == ScrubType::PARTIAL); //Partial/no scrub ISO
}

std::vector<std::filesystem::path> XisoWriter::convert_to_xiso(const std::filesystem::path& out_xiso_path, const bool scrub) 
{
    ImageReader& image_reader = *image_reader_;
    uint32_t sector_offset = static_cast<uint32_t>(image_reader.image_offset() / Xiso::SECTOR_SIZE);
    uint32_t end_sector = image_reader.total_sectors();
    const std::unordered_set<uint32_t>* data_sectors;

    if (scrub) 
    {
        data_sectors = &image_reader.data_sectors();
        end_sector = std::min(end_sector, image_reader.max_data_sector() + 1);
    }

    split::ofstream out_file(out_xiso_path, split_ ? Xiso::SPLIT_MARGIN : UINT64_MAX);
    if (!out_file.is_open()) 
    {
        throw XGDException(ErrCode::FILE_OPEN, HERE(), out_xiso_path.string());
    }

    StreamingChecksum chk;
    if (calculate_checksum_ && !split_) 
    {
        chk.init();
    }

    constexpr uint32_t CHUNK_SECTORS = 1024; // 2MB chunk
    std::vector<char> buffer(CHUNK_SECTORS * Xiso::SECTOR_SIZE);

    XGDLog() << "Writing XISO" << XGDLog::Endl;

    for (uint32_t i = sector_offset; i < end_sector; ) 
    {
        uint32_t current_chunk_sectors = std::min(CHUNK_SECTORS, end_sector - i);
        size_t chunk_bytes = static_cast<size_t>(current_chunk_sectors) * Xiso::SECTOR_SIZE;

        image_reader.read_sectors(i, current_chunk_sectors, buffer.data());

        if (scrub && image_reader.platform() == Platform::OGX) 
        {
            for (uint32_t s = 0; s < current_chunk_sectors; ++s)
            {
                uint32_t sec = i + s;
                if (data_sectors->find(sec) == data_sectors->end())
                {
                    std::memset(buffer.data() + (static_cast<size_t>(s) * Xiso::SECTOR_SIZE), 0x00, Xiso::SECTOR_SIZE);
                }
            }
        }

        if (chk.is_active())
        {
            chk.update(buffer.data(), chunk_bytes);
        }

        out_file.write(buffer.data(), chunk_bytes);
        if (out_file.fail()) 
        {
            throw XGDException(ErrCode::FILE_WRITE, HERE(), "Failed to write sector to output file");
        }

        i += current_chunk_sectors;
        XGDLog().print_progress(i - sector_offset, end_sector - sector_offset);

        check_status_flags();
    }

    out_file.close();

    if (chk.is_active())
    {
        precalculated_checksums_[out_xiso_path.string()] = chk.finalize();
    }

    return out_file.paths();
}

std::vector<std::filesystem::path> XisoWriter::convert_to_xiso_from_avl(AvlTree& avl_tree, const std::filesystem::path& out_xiso_path) 
{
    total_bytes_ = avl_tree.total_bytes();
    bytes_processed_ = 0;

    split::ofstream out_file(out_xiso_path, split_ ? Xiso::SPLIT_MARGIN : UINT64_MAX);
    if (!out_file.is_open()) 
    {
        throw XGDException(ErrCode::FILE_OPEN, HERE(), out_xiso_path.string());
    }

    XGDLog() << "Writing XISO" << XGDLog::Endl;

    write_header(out_file, avl_tree);

    out_file.seekp(avl_tree.root()->start_sector * Xiso::SECTOR_SIZE, std::ios::beg);

    AvlTree::traverse<split::ofstream>(avl_tree.root(), AvlTree::TraversalMethod::PREFIX, 
        [this](AvlTree::Node* node, split::ofstream* out_file, int depth) {
            write_tree(node, out_file, depth);
        }, &out_file, 0);  

    out_file.seekp(0, std::ios::end);
    pad_to_modulus(out_file, Xiso::FILE_MODULUS, 0x00);

    out_file.close();
    return out_file.paths();
}

void XisoWriter::write_tree(AvlTree::Node* node, split::ofstream* out_file, int depth) 
{
    if (!node->subdirectory) 
    {
        return;
    }

    if (node->subdirectory != EMPTY_SUBDIRECTORY) 
    {
        if (image_reader_) 
        {
            AvlTree::traverse<split::ofstream>(node->subdirectory, AvlTree::TraversalMethod::PREFIX, 
                [this](AvlTree::Node* node, split::ofstream* out_file, int depth) {
                    write_file_from_reader(node, out_file, depth);
                }, out_file, 0);
        } 
        else
        {
            AvlTree::traverse<split::ofstream>(node->subdirectory, AvlTree::TraversalMethod::PREFIX, 
                [this](AvlTree::Node* node, split::ofstream* out_file, int depth) {
                    write_file_from_directory(node, out_file, depth);
                }, out_file, 0);
        }
        
        AvlTree::traverse<split::ofstream>(node->subdirectory, AvlTree::TraversalMethod::PREFIX, 
            [this](AvlTree::Node* node, split::ofstream* out_file, int depth) {
                write_tree(node, out_file, depth);
            }, out_file, 0);

        out_file->seekp(node->start_sector * Xiso::SECTOR_SIZE, std::ios::beg);

        AvlTree::traverse<split::ofstream>(node->subdirectory, AvlTree::TraversalMethod::PREFIX, 
            [this](AvlTree::Node* node, split::ofstream* out_file, int depth) {
                write_entry(node, out_file, depth);
            }, out_file, 0);

        pad_to_modulus(*out_file, Xiso::SECTOR_SIZE, Xiso::PAD_BYTE); 
    } 
    else 
    {
        std::vector<char> pad_sector(Xiso::SECTOR_SIZE, Xiso::PAD_BYTE);

        out_file->seekp(node->start_sector * Xiso::SECTOR_SIZE, std::ios::beg);
        out_file->write(pad_sector.data(), Xiso::SECTOR_SIZE);
        if (out_file->fail()) 
        {
            throw XGDException(ErrCode::FILE_WRITE, HERE(), "Failed to write padding sector");
        }
    }
}

void XisoWriter::write_entry(AvlTree::Node* node, split::ofstream* out_file, int depth) 
{
    Xiso::DirectoryEntry::Header header = get_directory_entry_header(*node);    

    uint32_t padding_length = static_cast<uint32_t>(node->offset + node->directory_start - out_file->tellp());
    std::vector<char> padding(padding_length, Xiso::PAD_BYTE);

    out_file->write(padding.data(), padding_length);
    out_file->write(reinterpret_cast<char*>(&header), sizeof(Xiso::DirectoryEntry::Header));
    out_file->write(node->filename.c_str(), header.name_length);

    if (out_file->fail()) 
    {
        throw XGDException(ErrCode::FILE_WRITE, HERE(), "Failed to write directory entry for: " + node->filename);
    }
}

void XisoWriter::write_file_from_reader(AvlTree::Node* node, split::ofstream* out_file, int depth) 
{
    if (node->subdirectory) 
    {
        return;
    }

    out_file->seekp(node->start_sector * Xiso::SECTOR_SIZE, std::ios::beg);
    if (out_file->fail() || out_file->tellp() != (node->start_sector * Xiso::SECTOR_SIZE)) 
    {
        throw XGDException(ErrCode::FILE_WRITE, HERE(), "Failed to seek to file sector: " + node->filename);
    }

    uint64_t bytes_remaining = node->file_size;
    uint64_t read_position = image_reader_->image_offset() + (node->old_start_sector * static_cast<uint64_t>(Xiso::SECTOR_SIZE));
    std::vector<char> buffer(XGD::BUFFER_SIZE, 0);

    while (bytes_remaining > 0) 
    {
        uint64_t read_size = std::min(bytes_remaining, XGD::BUFFER_SIZE);

        image_reader_->read_bytes(read_position, read_size, buffer.data());

        out_file->write(buffer.data(), read_size);
        if (out_file->fail()) 
        {
            throw XGDException(ErrCode::FILE_WRITE, HERE(), "Failed to write file data: " + node->filename);
        }

        bytes_remaining -= read_size;
        read_position += read_size;

        XGDLog().print_progress(bytes_processed_ += read_size, total_bytes_);

        check_status_flags();
    }

    if ((node->file_size + (node->start_sector * Xiso::SECTOR_SIZE)) != out_file->tellp()) 
    {
        throw XGDException(ErrCode::FILE_WRITE, HERE(), "File write size mismatch: " + node->filename);
    }

    pad_to_modulus(*out_file, Xiso::SECTOR_SIZE, Xiso::PAD_BYTE);
}

void XisoWriter::write_file_from_directory(AvlTree::Node* node, split::ofstream* out_file, int depth)
{
    if (node->subdirectory) 
    {
        return;
    }

    out_file->seekp(node->start_sector * Xiso::SECTOR_SIZE, std::ios::beg);
    if (out_file->fail()) 
    {
        throw XGDException(ErrCode::FILE_SEEK, HERE(), "Failed to seek to file sector: " + node->filename);
    }

    std::ifstream in_file(node->path, std::ios::binary);
    if (!in_file.is_open()) 
    {
        throw XGDException(ErrCode::FILE_OPEN, HERE(), node->path.string());
    }

    uint64_t bytes_remaining = node->file_size;
    std::vector<char> buffer(XGD::BUFFER_SIZE, 0);

    while (bytes_remaining > 0) 
    {
        uint64_t read_size = std::min(bytes_remaining, XGD::BUFFER_SIZE);

        in_file.read(buffer.data(), read_size);
        if (in_file.fail()) 
        {
            throw XGDException(ErrCode::FILE_READ, HERE(), "Failed to read file data: " + node->path.string());
        }

        out_file->write(buffer.data(), read_size);
        if (out_file->fail()) 
        {
            throw XGDException(ErrCode::FILE_WRITE, HERE(), "Failed to write file data: " + node->filename);
        }

        bytes_remaining -= read_size;

        XGDLog().print_progress(bytes_processed_ += read_size, total_bytes_);

        check_status_flags();
    }

    in_file.close();

    if ((node->file_size + (node->start_sector * Xiso::SECTOR_SIZE)) != out_file->tellp()) 
    {
        throw XGDException(ErrCode::FILE_WRITE, HERE(), "File write size mismatch, possible overflow issue: " + node->filename);
    }

    pad_to_modulus(*out_file, Xiso::SECTOR_SIZE, Xiso::PAD_BYTE);
}

void XisoWriter::write_header(split::ofstream& out_file, AvlTree& avl_tree) 
{
    Xiso::Header header(static_cast<uint32_t>(avl_tree.root()->start_sector), 
                        static_cast<uint32_t>(avl_tree.root()->file_size), 
                        static_cast<uint32_t>(avl_tree.out_iso_size() / Xiso::SECTOR_SIZE),
                        image_reader_ ? image_reader_->file_time() : Xiso::FileTime());

    out_file.write(reinterpret_cast<char*>(&header), sizeof(Xiso::Header));
    if (out_file.fail()) 
    {
        throw XGDException(ErrCode::FILE_WRITE, HERE(), "Failed to write header to output file");
    }
}

void XisoWriter::pad_to_modulus(split::ofstream& out_file, const uint64_t modulus, const char pad_byte) 
{
    if ((out_file.tellp() % modulus) == 0) 
    {
        return;
    }

    size_t padding_len = modulus - (out_file.tellp() % modulus);
    std::vector<char> buffer(padding_len, pad_byte);

    out_file.write(buffer.data(), padding_len); 
    if (out_file.fail()) 
    {
        throw XGDException(ErrCode::FILE_WRITE, HERE(), "Failed to write padding bytes");
    }
}