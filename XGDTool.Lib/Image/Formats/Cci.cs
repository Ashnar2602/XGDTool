using System.Buffers.Binary;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Image.Formats;

public static class CCI
{
    public static uint MAGIC => StringExt.GetUint("CCIM");
    public const byte VERSION = 1;
    public const byte INDEX_ALIGNMENT = 2;
    public const uint COMPRESSED_FLAG = 0x80000000;
    public const long SPLIT_OFFSET = 0xFF000000;
    
    public class Header : ISerializable
    {
        public uint Magic;
        public uint HeaderSize;
        public ulong UncompressedSize;
        public ulong IndexOffset;
        public uint BlockSize;
        public byte Version;
        public byte IndexAlignment;
        public short Reserved;

        public const int SIZE = 32;
        public int Size() => SIZE;

        public Header() { }
        public Header(ulong uncompressedSize, ulong indexOffset)
        {
            Magic = MAGIC;
            HeaderSize = SIZE;
            UncompressedSize = uncompressedSize;
            IndexOffset = indexOffset;
            BlockSize = XDVDFS.SECTOR_SIZE;
            Version = VERSION;
            IndexAlignment = INDEX_ALIGNMENT;
        }

        public void Serialize(Span<byte> buffer)
        {
            if (buffer.Length < SIZE)
                throw new ArgumentException($"Buffer length must be at least {SIZE} bytes.", nameof(buffer));

            BinaryPrimitives.WriteUInt32LittleEndian(buffer, Magic);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(4, 4), HeaderSize);
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(8, 8), UncompressedSize);
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(16, 8), IndexOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(24, 4), BlockSize);
            buffer[28] = Version;
            buffer[29] = IndexAlignment;
            BinaryPrimitives.WriteInt16LittleEndian(buffer.Slice(30, 2), Reserved);
        }

        public void Deserialize(ReadOnlySpan<byte> data)
        {
            if (data.Length < SIZE)
                throw new ArgumentException($"Data must be at least {SIZE} bytes long", nameof(data));

            Magic = BinaryPrimitives.ReadUInt32LittleEndian(data);
            HeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
            UncompressedSize = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(8, 8));
            IndexOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(16, 8));
            BlockSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(24, 4));
            Version = data[28];
            IndexAlignment = data[29];
            Reserved = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(30, 2));
        }
    }

    static bool IsHeaderValid(Header header) => 
        header.Magic == MAGIC && 
        header.Version == VERSION && 
        header.BlockSize == XDVDFS.SECTOR_SIZE &&
        header.HeaderSize == Header.SIZE &&
        (XDVDFS.SECTOR_SIZE & ((1u << header.IndexAlignment) - 1)) == 0;

    public static uint EncodeIndexEntry(uint offset, bool compressed) => 
#if DEBUG
        (offset != ((offset >> INDEX_ALIGNMENT) << INDEX_ALIGNMENT)) 
            ? throw new ArgumentOutOfRangeException(nameof(offset), $"Offset must be <= {uint.MaxValue >> INDEX_ALIGNMENT}") : 
#endif
        (offset >> INDEX_ALIGNMENT) | (compressed ? COMPRESSED_FLAG : 0u);

    public static (uint offset, bool compressed) DecodeIndexEntry(uint entry, byte align = INDEX_ALIGNMENT)
    {
        bool compressed = (entry & COMPRESSED_FLAG) != 0;
        uint offset = (entry & ~COMPRESSED_FLAG) << align;
        return (offset, compressed);
    }
}
