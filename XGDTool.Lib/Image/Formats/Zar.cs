using System.Runtime.InteropServices;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Image.Formats;

public static class Zar
{
    public const int COMPRESSED_BLOCK_SIZE = 64 * 1024;
    public const int ENTRIES_PER_OFFSETRECORD = 16;
    public const uint ENTRY_TYPE_FILE = 0x80000000;
    public const uint FOOTER_MAGIC = 0x169f52d6;
    public const uint FOOTER_VERSION = 0x61bf3a01;


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class CompressionOffsetRecord : IMarshalable
    {
        private ulong BaseOffset;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = ENTRIES_PER_OFFSETRECORD)]
        private ushort[] SizeTable = new ushort[ENTRIES_PER_OFFSETRECORD];

        public int Size() => 8 + (2 * ENTRIES_PER_OFFSETRECORD);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class FileDirectoryEntry : IMarshalable
    {
        private uint NameOffsetAndTypeFlag;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        private uint[] Record = new uint[3];

        private const int FileOffsetLow = 0;
        private const int FileSizeLow = 1;
        private const int FileOffsetAndSizeHigh = 2;
        private const int DirNodeStartIndex = 0;
        private const int DirNodeCount = 1;
        private const int DirReserved = 2;

        public bool IsFile 
        { 
            get { return (Bits.FromBig(NameOffsetAndTypeFlag) & ENTRY_TYPE_FILE) != 0; }
            set 
            { 
                NameOffsetAndTypeFlag = Bits.ToBig(
                    (Bits.FromBig(NameOffsetAndTypeFlag) & ~ENTRY_TYPE_FILE) | 
                    (value ? ENTRY_TYPE_FILE : 0)); 
            }
        }
        public bool IsDirectory
        {
            get { return (Bits.FromBig(NameOffsetAndTypeFlag) & ENTRY_TYPE_FILE) == 0; }
            set 
            { 
                NameOffsetAndTypeFlag = Bits.ToBig(
                    (Bits.FromBig(NameOffsetAndTypeFlag) & ~ENTRY_TYPE_FILE) | 
                    (value ? 0 : ENTRY_TYPE_FILE)); 
            }
        }
        public uint NameOffset
        {
            get { return Bits.FromBig(NameOffsetAndTypeFlag) & ~ENTRY_TYPE_FILE; }
            set 
            { 
                NameOffsetAndTypeFlag = Bits.ToBig(
                    (Bits.FromBig(NameOffsetAndTypeFlag) & ENTRY_TYPE_FILE) | 
                    (value & ~ENTRY_TYPE_FILE)); 
            }
        }
        public ulong FileOffset
        {
            get 
            { 
                return (ulong)Bits.FromBig(Record[FileOffsetLow]) | 
                       (((ulong)Bits.FromBig(Record[FileOffsetAndSizeHigh]) & 0xFFFF) << 32); 
            }
            set
            {
                Record[FileOffsetLow] = Bits.ToBig((uint)(value & 0xFFFFFFFF));
                Record[FileOffsetAndSizeHigh] = Bits.ToBig(
                    (Bits.FromBig(Record[FileOffsetAndSizeHigh]) & 0xFFFF0000) | 
                    (uint)((value >> 32) & 0xFFFF));
            }
        }
        public ulong FileSize
        {
            get 
            { 
                return (ulong)Bits.FromBig(Record[FileSizeLow]) | 
                       (((ulong)Bits.FromBig(Record[FileOffsetAndSizeHigh]) & 0xFFFF0000) << 16); 
            }
            set
            {
                Record[FileSizeLow] = Bits.ToBig((uint)(value & 0xFFFFFFFF));
                Record[FileOffsetAndSizeHigh] = Bits.ToBig(
                    (Bits.FromBig(Record[FileOffsetAndSizeHigh]) & 0x0000FFFF) | 
                    (uint)((value >> 16) & 0xFFFF0000));
            }
        }

        public int Size() => 16;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class Footer : IMarshalable
    {
        private ulong _SectionCompressedDataOffset;
        private ulong _SectionCompressedDataSize;
        private ulong _SectionOffsetRecordsOffset;
        private ulong _SectionOffsetRecordsSize;
        private ulong _SectionNamesOffset;
        private ulong _SectionNamesSize;
        private ulong _SectionFileTreeOffset;
        private ulong _SectionFileTreeSize;
        private ulong _SectionMetaDirectoryOffset;
        private ulong _SectionMetaDirectorySize;
        private ulong _SectionMetaDataOffset;
        private ulong _SectionMetaDataSize;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] IntegrityHash = new byte[32];
        private ulong _TotalSize;
        private uint _Version;
        private uint _Magic;

        public ulong SectionCompressedDataOffset 
        { 
            get { return Bits.FromBig(_SectionCompressedDataOffset); }
            set { _SectionCompressedDataOffset = Bits.ToBig(value); }
        }
        public ulong SectionCompressedDataSize
        {
            get { return Bits.FromBig(_SectionCompressedDataSize); }
            set { _SectionCompressedDataSize = Bits.ToBig(value); }
        }
        public ulong SectionOffsetRecordsOffset
        {
            get { return Bits.FromBig(_SectionOffsetRecordsOffset); }
            set { _SectionOffsetRecordsOffset = Bits.ToBig(value); }
        }
        public ulong SectionOffsetRecordsSize
        {
            get { return Bits.FromBig(_SectionOffsetRecordsSize); }
            set { _SectionOffsetRecordsSize = Bits.ToBig(value); }
        }
        public ulong SectionNamesOffset
        {
            get { return Bits.FromBig(_SectionNamesOffset); }
            set { _SectionNamesOffset = Bits.ToBig(value); }
        }
        public ulong SectionNamesSize
        {
            get { return Bits.FromBig(_SectionNamesSize); }
            set { _SectionNamesSize = Bits.ToBig(value); }
        }
        public ulong SectionFileTreeOffset
        {
            get { return Bits.FromBig(_SectionFileTreeOffset); }
            set { _SectionFileTreeOffset = Bits.ToBig(value); }
        }
        public ulong SectionFileTreeSize
        {
            get { return Bits.FromBig(_SectionFileTreeSize); }
            set { _SectionFileTreeSize = Bits.ToBig(value); }
        }
        public ulong SectionMetaDirectoryOffset
        {
            get { return Bits.FromBig(_SectionMetaDirectoryOffset); }
            set { _SectionMetaDirectoryOffset = Bits.ToBig(value); }
        }
        public ulong SectionMetaDirectorySize
        {
            get { return Bits.FromBig(_SectionMetaDirectorySize); }
            set { _SectionMetaDirectorySize = Bits.ToBig(value); }
        }
        public ulong SectionMetaDataOffset
        {
            get { return Bits.FromBig(_SectionMetaDataOffset); }
            set { _SectionMetaDataOffset = Bits.ToBig(value); }
        }
        public ulong SectionMetaDataSize
        {
            get { return Bits.FromBig(_SectionMetaDataSize); }
            set { _SectionMetaDataSize = Bits.ToBig(value); }
        }
        public ulong TotalSize
        {
            get { return Bits.FromBig(_TotalSize); }
            set { _TotalSize = Bits.ToBig(value); }
        }
        public uint Version
        {
            get { return Bits.FromBig(_Version); }
            set { _Version = Bits.ToBig(value); }
        }
        public uint Magic
        {
            get { return Bits.FromBig(_Magic); }
            set { _Magic = Bits.ToBig(value); }
        }

        public int Size() => ((16 * 6) + 32 + 8 + 4 + 4);
    }
}
