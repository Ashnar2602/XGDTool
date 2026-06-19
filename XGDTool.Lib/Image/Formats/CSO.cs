using System.Buffers.Binary;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Image.Formats;

public static class CSO
{
    public static uint MAGIC => StringExt.GetUint("CISO");
    public const byte VERSION = 2;
    public const byte INDEX_ALIGNMENT = 2;
    public const uint COMPRESSED_FLAG = 1u << 31;
    public const long SPLIT_OFFSET = 0xFFBF6000;
    public const long FILE_MODULUS = 0x400;

    public class Header : ISerializable
    {
        public uint Magic;
        public uint HeaderSize;
        public ulong UncompressedSize;
        public uint BlockSize;
        public byte Version;
        public byte IndexAlignment;

        public const int SIZE = 24;

        public Header(ulong uncompressedSize)
        {
            Magic = MAGIC;
            HeaderSize = SIZE;
            UncompressedSize = uncompressedSize;
            BlockSize = XDVDFS.SECTOR_SIZE;
            Version = VERSION;
            IndexAlignment = INDEX_ALIGNMENT;
        }
        public Header() { }

        public int Size() => SIZE;
        public void Deserialize(ReadOnlySpan<byte> data)
        {
            if (data.Length < SIZE)
                throw new ArgumentException($"Data must be at least {SIZE} bytes long", nameof(data));

            Magic = BinaryPrimitives.ReadUInt32LittleEndian(data);
            HeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
            UncompressedSize = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(8, 8));
            BlockSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4));
            Version = data[20];
            IndexAlignment = data[21];
        }
        public void Serialize(Span<byte> buffer)
        {
            if (buffer.Length < SIZE)
                throw new ArgumentException($"Buffer length must be at least {SIZE} bytes.", nameof(buffer));

            BinaryPrimitives.WriteUInt32LittleEndian(buffer, Magic);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(4, 4), HeaderSize);
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(8, 8), UncompressedSize);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(16, 4), BlockSize);
            buffer[20] = Version;
            buffer[21] = IndexAlignment;
        }
    }

    public static bool IsHeaderValid(Header header, long imageSize) => 
        header.Magic == MAGIC && 
        header.Version == VERSION && 
        header.BlockSize == XDVDFS.SECTOR_SIZE &&
        header.HeaderSize == Header.SIZE &&
        ((XDVDFS.SectorCount(header.UncompressedSize) + 1) * sizeof(uint)) + Header.SIZE < SPLIT_OFFSET &&
        ((XDVDFS.SectorCount(header.UncompressedSize) + 1) * sizeof(uint)) + Header.SIZE + XDVDFS.SECTOR_SIZE < imageSize &&
        (XDVDFS.SECTOR_SIZE & ((1u << header.IndexAlignment) - 1)) == 0;

    public static uint EncodeIndexEntry(uint offset, bool compressed, byte align = INDEX_ALIGNMENT) => 
#if DEBUG
        (offset != ((offset >> align) << align)) 
            ? throw new ArgumentOutOfRangeException(nameof(offset), $"Offset must be <= {uint.MaxValue >> align}") : 
#endif
        (offset >> align) | (compressed ? COMPRESSED_FLAG : 0u);

    public static (uint offset, bool compressed) DecodeIndexEntry(uint entry, byte align = INDEX_ALIGNMENT)
    {
        bool compressed = (entry & COMPRESSED_FLAG) != 0;
        uint offset = (entry & ~COMPRESSED_FLAG) << align;
        return (offset, compressed);
    }
}
