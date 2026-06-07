using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using XGDToolLib.Util;

namespace XGDToolLib.Image.Format;

public static class CCI
{
    public static uint MAGIC => Bits.FromBig(Bits.UintFromString("CCIM"));
    public const byte HEADER_SIZE = 32;
    public const byte VERSION = 1;
    public const byte INDEX_ALIGNMENT = 2;
    public const uint COMPRESSED_FLAG = 0x80000000;
    public const long SPLIT_OFFSET = 0xFF000000;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class Header : IMarshalable
    {
        public uint Magic;
        public uint HeaderSize;
        public ulong UncompressedSize;
        public ulong IndexOffset;
        public uint BlockSize;
        public byte Version;
        public byte IndexAlignment;
        public short Reserved;

        public int Size() => HEADER_SIZE;

        public Header() { }
        public Header(ulong uncompressedSize, ulong indexOffset)
        {
            Magic = MAGIC;
            HeaderSize = HEADER_SIZE;
            UncompressedSize = uncompressedSize;
            IndexOffset = indexOffset;
            BlockSize = XISO.SECTOR_SIZE;
            Version = VERSION;
            IndexAlignment = INDEX_ALIGNMENT;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint EncodeIndexEntry(uint offset, bool compressed)
    {
        return (offset >> INDEX_ALIGNMENT) | (compressed ? COMPRESSED_FLAG : 0u);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (uint offset, bool compressed) DecodeIndexEntry(uint entry)
    {
        bool compressed = (entry & COMPRESSED_FLAG) != 0;
        uint offset = (entry & ~COMPRESSED_FLAG) << INDEX_ALIGNMENT;
        return (offset, compressed);
    }
}
