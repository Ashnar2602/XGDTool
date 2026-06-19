using System.Runtime.CompilerServices;
using System.Text;
using System.Buffers.Binary;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Image.Formats;

public static class XDVDFS
{
    public const int SECTOR_SIZE = 2048;
    public const int SECTOR_SHIFT = 11;
    public const byte PAD_BYTE = 0xFF;
    public const ushort PAD_WORD = 0xFFFF;

    public const uint REDUMP_VIDEO_SECTORS = 0x30600;
    public const uint REDUMP_TOTAL_SECTORS = 0x3A4D50;
    public const uint REDUMP_GAME_SECTORS = REDUMP_TOTAL_SECTORS - REDUMP_VIDEO_SECTORS;
    public const uint REDUMP_END_SECTOR = 0x345B60;
    public const uint SPLIT_MARGIN = 0xFF000000;

    public static ReadOnlySpan<byte> MAGIC => "MICROSOFT*XBOX*MEDIA"u8;
    public const int MAGIC_SIZE = 20;
    public const int MAGIC_OFFSET = 0x10000;
    public const uint VOLUME_DESCRIPTOR_SECTOR = MAGIC_OFFSET / SECTOR_SIZE;

    public const long LSEEK_OFFSET_GLOBAL = 0x0FD90000;
    public const long LSEEK_OFFSET_XGD3 = 0x02080000;
    public const long LSEEK_OFFSET_XGD1 = 0x18300000;

    public const string SYSTEM_UPDATE_DIRECTORY_NAME = "$SystemUpdate";

    public readonly static long[] ImageOffsets = 
    [
        0,
        LSEEK_OFFSET_GLOBAL,
        LSEEK_OFFSET_XGD3,
        LSEEK_OFFSET_XGD1
    ];

    [Flags]
    public enum DirAttributes : byte
    {
        ReadOnly = 0x01,
        Hidden = 0x02,
        System = 0x04,
        Directory = 0x10,
        File = 0x20,
        Normal = 0x80
    }

    public class DirectoryEntry : ISerializable
    {
        public ushort LeftOffset;
        public ushort RightOffset;
        public uint StartSector;
        public uint FileSize;
        public DirAttributes Attributes;
        public byte NameLength { get; protected set; }
        public string FileName { get; protected set; } = string.Empty;

        public const int HEADER_SIZE = 14;

        public int Size() => HEADER_SIZE + NameLength;

        public void DeserializeHeader(ReadOnlySpan<byte> data)
        {
            if (data.Length < HEADER_SIZE)
                throw new ArgumentException($"Data length must be at least {HEADER_SIZE} bytes.", nameof(data));

            LeftOffset = BinaryPrimitives.ReadUInt16LittleEndian(data[..2]);
            RightOffset = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(2, 2));
            StartSector = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
            FileSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4));
            Attributes = (DirAttributes)data[12];
            NameLength = data[13];
            FileName = string.Empty;
        }

        public void DeserializeName(byte[] data) => DeserializeName(data.AsSpan());
        
        public void DeserializeName(ReadOnlySpan<byte> data)
        {
            int nullIndex = data.IndexOf((byte)0);
            int nameLen = Math.Min(nullIndex >= 0 ? nullIndex : data.Length, byte.MaxValue);

            FileName = Encoding.ASCII.GetString(data[..nameLen]);
            NameLength = (byte)nameLen;
        }

        public void Deserialize(ReadOnlySpan<byte> data)
        {
            if (data.Length < HEADER_SIZE)
                throw new ArgumentException($"Data length must be at least {HEADER_SIZE} bytes.", nameof(data));

            DeserializeHeader(data[..HEADER_SIZE]);

            if (data.Length > HEADER_SIZE) 
            {
                if (data.Length < HEADER_SIZE + NameLength)
                    throw new ArgumentException($"Data length must be at least {HEADER_SIZE + NameLength} bytes to deserialize name.", nameof(data));
                
                DeserializeName(data.Slice(HEADER_SIZE, NameLength));
            }
        }

        public void Serialize(Span<byte> buffer)
        {
            if (buffer.Length < Size())
                throw new ArgumentException($"Buffer length must be at least {Size()} bytes.", nameof(buffer));

            BinaryPrimitives.WriteUInt16LittleEndian(buffer[..2], LeftOffset);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(2, 2), RightOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(4, 4), StartSector);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(8, 4), FileSize);
            buffer[12] = (byte)Attributes;
            buffer[13] = NameLength;
            var nameBytes = Encoding.ASCII.GetBytes(FileName);
            nameBytes.CopyTo(buffer.Slice(HEADER_SIZE, NameLength));
        }

        public void SetName(string name)
        {
            if (name.Length > byte.MaxValue)
                throw new ArgumentException($"Name length cannot exceed {byte.MaxValue} characters.");

            FileName = new string(name.AsSpan());
            NameLength = (byte)FileName.Length;
        }

        public virtual DirectoryEntry Clone()
        {
            return new DirectoryEntry
            {
                LeftOffset = this.LeftOffset,
                RightOffset = this.RightOffset,
                StartSector = this.StartSector,
                FileSize = this.FileSize,
                Attributes = this.Attributes,
                NameLength = this.NameLength,
                FileName = this.FileName
            };
        }
    }

    public class VolumeDescriptor : ISerializable
    {
        public readonly byte[] Magic1 = new byte[MAGIC_SIZE];
        public uint RootDirectoryTableSector;
        public uint RootDirectoryTableSize;
        public ulong FileTime;

        // End of sector - MAGIC_SIZE
        public readonly byte[] Magic2 = new byte[MAGIC_SIZE];

        public const int SIZE = SECTOR_SIZE;

        public VolumeDescriptor()
        {
            MAGIC.CopyTo(Magic1);
            MAGIC.CopyTo(Magic2);
            FileTime = (ulong)DateTime.UtcNow.ToFileTimeUtc();
        }

        public int Size() => SIZE;

        public void Deserialize(ReadOnlySpan<byte> data)
        {
            if (data.Length < SIZE)
                throw new ArgumentException($"Data length must be at least {SIZE} bytes.", nameof(data));

            data[..MAGIC_SIZE].CopyTo(Magic1);
            RootDirectoryTableSector = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(MAGIC_SIZE, 4));
            RootDirectoryTableSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(MAGIC_SIZE + 4, 4));
            FileTime = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(MAGIC_SIZE + 8, 8));
            data.Slice(SIZE - MAGIC_SIZE, MAGIC_SIZE).CopyTo(Magic2);
        }

        public void Serialize(Span<byte> buffer)
        {
            if (buffer.Length < SIZE)
                throw new ArgumentException($"Buffer length must be at least {SIZE} bytes.", nameof(buffer));

            buffer[..SIZE].Clear();
            Magic1.CopyTo(buffer.Slice(0, MAGIC_SIZE));
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(MAGIC_SIZE, 4), RootDirectoryTableSector);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(MAGIC_SIZE + 4, 4), RootDirectoryTableSize);
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(MAGIC_SIZE + 8, 8), FileTime);
            Magic2.CopyTo(buffer.Slice(SIZE - MAGIC_SIZE, MAGIC_SIZE));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint SectorCount(long value) => (uint)((value + SECTOR_SIZE - 1) >> SECTOR_SHIFT);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint SectorCount(int value) => ((uint)value + SECTOR_SIZE - 1) >> SECTOR_SHIFT;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint SectorCount(ulong value) => checked((uint)((value + SECTOR_SIZE - 1) >> SECTOR_SHIFT));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint SectorIndex(long value) => (uint)(value >> SECTOR_SHIFT);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSectorAligned(long value) => (value & (SECTOR_SIZE - 1)) == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long SectorToOffset(uint sector) => (long)sector << SECTOR_SHIFT;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long SectorToOffset(long sector) => sector << SECTOR_SHIFT;

    // private const int ECMA119_HEADER_SIZE = SECTOR_SIZE + 7;
    // private const int ECMA119_DATA_START = 0x8000;
    // private const int ECMA119_VOL_SPACE_SIZE = ECMA119_DATA_START + 80;
    // private const int ECMA119_VOL_SET_SIZE = ECMA119_DATA_START + 120;
    // private const int ECMA119_VOL_SET_ID = ECMA119_DATA_START + 190;
    // private const int ECMA119_VOL_CREATION_DATE = ECMA119_DATA_START + 813;

    // [StructLayout(LayoutKind.Sequential, Pack = 1)]
    // internal class Ecma119Header : IMarshalable<Ecma119Header>
    // {
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
    //     public byte[] DataStart = new byte[7] { 0x01, 0x43, 0x44, 0x30, 0x30, 0x31, 0x01 };
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = ECMA119_VOL_SPACE_SIZE - ECMA119_DATA_START - 7)]
    //     public byte[] Reserved1 = new byte[ECMA119_VOL_SPACE_SIZE - ECMA119_DATA_START - 7];
    //     public uint VolumeSpaceLittle;
    //     public uint VolumeSpaceBig;
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = ECMA119_VOL_SET_SIZE - ECMA119_VOL_SPACE_SIZE - 8)]
    //     public byte[] Reserved2 = new byte[ECMA119_VOL_SET_SIZE - ECMA119_VOL_SPACE_SIZE - 8];
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
    //     public byte[] VolumeSetSize = new byte[12] { 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x08, 0x08, 0x00 };
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = ECMA119_VOL_SET_ID - ECMA119_VOL_SET_SIZE - 12)]
    //     public byte[] Reserved3 = new byte[ECMA119_VOL_SET_ID - ECMA119_VOL_SET_SIZE - 12];
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = ECMA119_VOL_CREATION_DATE - ECMA119_VOL_SET_ID)]
    //     public byte[] Spaces = new byte[ECMA119_VOL_CREATION_DATE - ECMA119_VOL_SET_ID];
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
    //     public byte[] Date1 = new byte[17];
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
    //     public byte[] Date2 = new byte[17];
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
    //     public byte[] Date3 = new byte[17];
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
    //     public byte[] Date4 = new byte[17];
    //     public byte FinalByte = 0x01;
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)SECTOR_SIZE - (7 + (ECMA119_VOL_SPACE_SIZE - ECMA119_DATA_START - 7) + 8 + (ECMA119_VOL_SET_SIZE - ECMA119_VOL_SPACE_SIZE - 8) + 12 + (ECMA119_VOL_SET_ID - ECMA119_VOL_SET_SIZE - 12) + (ECMA119_VOL_CREATION_DATE - ECMA119_VOL_SET_ID) + 68 + 1))]
    //     public byte[] SectorPadding = new byte[(int)SECTOR_SIZE - (7 + (ECMA119_VOL_SPACE_SIZE - ECMA119_DATA_START - 7) + 8 + (ECMA119_VOL_SET_SIZE - ECMA119_VOL_SPACE_SIZE - 8) + 12 + (ECMA119_VOL_SET_ID - ECMA119_VOL_SET_SIZE - 12) + (ECMA119_VOL_CREATION_DATE - ECMA119_VOL_SET_ID) + 68 + 1)];
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
    //     public byte[] SectorStart = new byte[7] { 0xFF, 0x43, 0x44, 0x30, 0x30, 0x31, 0x01 };

    //     public Ecma119Header(uint totalSectors)
    //     {
    //         Spaces.AsSpan().Fill((byte)0x20);
    //         Date1.AsSpan().Fill((byte)0x30);
    //         Date2.AsSpan().Fill((byte)0x30);
    //         Date3.AsSpan().Fill((byte)0x30);
    //         Date4.AsSpan().Fill((byte)0x30);
    //         Date1[^1] = (byte)0x00;
    //         Date2[^1] = (byte)0x00;
    //         Date3[^1] = (byte)0x00;
    //         Date4[^1] = (byte)0x00;
    //         VolumeSpaceLittle = Bits.ToLittle(totalSectors);
    //         VolumeSpaceBig = Bits.ToBig(totalSectors);
    //     }

    //     public static int SIZE => ECMA119_HEADER_SIZE;
    // }

    // [StructLayout(LayoutKind.Sequential, Pack = 1)]
    // public class FileHeader : IMarshalable<FileHeader>
    // {
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = OPTIMIZED_TAG_OFFSET)]
    //     private readonly byte[] Reserved1 = new byte[OPTIMIZED_TAG_OFFSET];
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = ECMA119_DATA_START - OPTIMIZED_TAG_OFFSET)]
    //     private readonly byte[] OptimizedTag = new byte[ECMA119_DATA_START - OPTIMIZED_TAG_OFFSET];
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = ECMA119_HEADER_SIZE)]
    //     private readonly byte[] Ecma119Header = new byte[ECMA119_HEADER_SIZE];
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAGIC_OFFSET - (ECMA119_DATA_START + ECMA119_HEADER_SIZE))]
    //     private readonly byte[] Reserved3 = new byte[MAGIC_OFFSET - (ECMA119_DATA_START + ECMA119_HEADER_SIZE)];
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAGIC_SIZE)]
    //     private readonly byte[] Magic1 = new byte[MAGIC_SIZE];
    //     private readonly uint RootSector;
    //     private readonly uint RootSize;
    //     private readonly uint FileTimeLow;
    //     private readonly uint FileTimeHigh;
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAGIC_UNUSED_LENGTH)]
    //     private readonly byte[] Reserved4 = new byte[MAGIC_UNUSED_LENGTH];
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAGIC_SIZE)]
    //     private readonly byte[] Magic2 = new byte[MAGIC_SIZE];

    //     public FileHeader(uint rootSector, uint rootSize, uint totalSectors, uint? fileTimeLow = null, uint? fileTimeHigh = null)
    //     {
    //         var ecma = new Ecma119Header(totalSectors);
    //         Ecma119Header = ecma.ToBytes();
    //         RootSector = rootSector;
    //         RootSize = rootSize;
    //         FileTimeLow = fileTimeLow ?? 0;
    //         FileTimeHigh = fileTimeHigh ?? 0;
    //         Magic1 = MAGIC.ToArray();
    //         Magic2 = MAGIC.ToArray();
    //     }

    //     public static int SIZE => MAGIC_OFFSET + SECTOR_SIZE;
    // }
}
