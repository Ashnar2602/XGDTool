using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Util;

namespace XGDToolLib.Image.Format;

public static class CCI
{
    public const uint MAGIC = 1307107395; // 'CCIM'
    public const byte HEADER_SIZE = 32;
    public const byte VERSION = 1;
    public const byte INDEX_ALIGNMENT = 2;
    public const long SPLIT_OFFSET = 0xFF000000;

    struct IndexInfo
    {
        public readonly uint Value;
        public readonly bool Compressed;
    }

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
}

