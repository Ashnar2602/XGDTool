using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Image.Format;

public static class XISO
{
    public const int SECTOR_SIZE = 2048;
    public const int SECTOR_SHIFT = 11;
    public const byte PAD_BYTE = 0xFF;
    public const ushort PAD_WORD = 0xFFFF;
    public const long FILE_MODULUS = 0x10000;

    public const uint REDUMP_VIDEO_SECTORS = 0x30600;
    public const uint REDUMP_TOTAL_SECTORS = 0x3A4D50;
    public const uint REDUMP_GAME_SECTORS = REDUMP_TOTAL_SECTORS - REDUMP_VIDEO_SECTORS;
    public const uint REDUMP_END_SECTOR = 0x345B60;
    public const uint SPLIT_MARGIN = 0xFF000000;

    public static ReadOnlySpan<byte> MAGIC => "MICROSOFT*XBOX*MEDIA"u8;
    public const int MAGIC_SIZE = 20;
    public const int MAGIC_OFFSET = 0x10000;
    public const int MAGIC_UNUSED_LENGTH = 0x7c8;

    public const long LSEEK_OFFSET_GLOBAL = 0x0FD90000;
    public const long LSEEK_OFFSET_XGD3 = 0x02080000;
    public const long LSEEK_OFFSET_XGD1 = 0x18300000;
    public const uint ROOT_DIRECTORY_SECTOR = 0x108;

    private const int ECMA119_HEADER_SIZE = SECTOR_SIZE + 7;
    private const int ECMA119_DATA_START = 0x8000;
    private const int ECMA119_VOL_SPACE_SIZE = ECMA119_DATA_START + 80;
    private const int ECMA119_VOL_SET_SIZE = ECMA119_DATA_START + 120;
    private const int ECMA119_VOL_SET_ID = ECMA119_DATA_START + 190;
    private const int ECMA119_VOL_CREATION_DATE = ECMA119_DATA_START + 813;

    private const int OPTIMIZED_TAG_OFFSET = 31337;
    public const int DIRECTORY_HEADER_SIZE = 14;

    public const int MAX_FILENAME_CHARS_MAX = 42;

    public static long[] ImageOffsets = new long[4]
        {
            0,
            LSEEK_OFFSET_GLOBAL,
            LSEEK_OFFSET_XGD3,
            LSEEK_OFFSET_XGD1
        };

    [Flags]
    public enum DirAttribute : byte
    {
        ReadOnly = 0x01,
        Hidden = 0x02,
        System = 0x04,
        Directory = 0x10,
        File = 0x20,
        Normal = 0x80
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DirectoryHeader : IMarshalable
    {
        public ushort LeftOffset;
        public ushort RightOffset;
        public uint StartSector;
        public uint FileSize;
        public DirAttribute Attributes;
        internal byte NameLength;

        public int Size() => DIRECTORY_HEADER_SIZE;
    }

    public class DirectoryEntry
    {
        public DirectoryHeader Header = new();
        private string Name = "";

        public string GetName() => Name;

        public void SetNameFromBytes(byte[] nameBytes) => SetNameFromBytes(nameBytes.AsSpan());

        public void SetNameFromBytes(ReadOnlySpan<byte> nameBytes)
        {
            if (nameBytes.Length > 0xFF)
                throw new ArgumentException("Name bytes length cannot exceed 255.");

            int nameLen = Header.NameLength;

            if (nameLen <= 0 || nameLen > nameBytes.Length)
                nameLen = nameBytes.IndexOf((byte)0);

            if (nameLen < 0)
                nameLen = nameBytes.Length;

            Name = Encoding.ASCII.GetString(nameBytes[..nameLen]);
            Header.NameLength = (byte)nameLen;
        }

        public void SetName(string name)
        {
            if (name.Length > 0xFF)
                throw new ArgumentException("Name length cannot exceed 255.");

            Name = name;
            Header.NameLength = (byte)name.Length;
        }

        public byte[] ToBytes()
        {
            var headerBytes = Header.ToBytes();
            var nameBytes = Encoding.ASCII.GetBytes(Name);
            var bytes = new byte[headerBytes.Length + Header.NameLength];
            Array.Copy(headerBytes, 0, bytes, 0, headerBytes.Length);
            Array.Copy(nameBytes, 0, bytes, headerBytes.Length, Header.NameLength);
            return bytes;
        }
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal class Ecma119Header : IMarshalable
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
        public byte[] DataStart = new byte[7] { 0x01, 0x43, 0x44, 0x30, 0x30, 0x31, 0x01 };
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = ECMA119_VOL_SPACE_SIZE - ECMA119_DATA_START - 7)]
        public byte[] Reserved1 = new byte[ECMA119_VOL_SPACE_SIZE - ECMA119_DATA_START - 7];
        public uint VolumeSpaceLittle;
        public uint VolumeSpaceBig;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = ECMA119_VOL_SET_SIZE - ECMA119_VOL_SPACE_SIZE - 8)]
        public byte[] Reserved2 = new byte[ECMA119_VOL_SET_SIZE - ECMA119_VOL_SPACE_SIZE - 8];
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public byte[] VolumeSetSize = new byte[12] { 0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x08, 0x08, 0x00 };
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = ECMA119_VOL_SET_ID - ECMA119_VOL_SET_SIZE - 12)]
        public byte[] Reserved3 = new byte[ECMA119_VOL_SET_ID - ECMA119_VOL_SET_SIZE - 12];
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = ECMA119_VOL_CREATION_DATE - ECMA119_VOL_SET_ID)]
        public byte[] Spaces = new byte[ECMA119_VOL_CREATION_DATE - ECMA119_VOL_SET_ID];
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public byte[] Date1 = new byte[17];
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public byte[] Date2 = new byte[17];
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public byte[] Date3 = new byte[17];
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public byte[] Date4 = new byte[17];
        public byte FinalByte = 0x01;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)SECTOR_SIZE - (7 + (ECMA119_VOL_SPACE_SIZE - ECMA119_DATA_START - 7) + 8 + (ECMA119_VOL_SET_SIZE - ECMA119_VOL_SPACE_SIZE - 8) + 12 + (ECMA119_VOL_SET_ID - ECMA119_VOL_SET_SIZE - 12) + (ECMA119_VOL_CREATION_DATE - ECMA119_VOL_SET_ID) + 68 + 1))]
        public byte[] SectorPadding = new byte[(int)SECTOR_SIZE - (7 + (ECMA119_VOL_SPACE_SIZE - ECMA119_DATA_START - 7) + 8 + (ECMA119_VOL_SET_SIZE - ECMA119_VOL_SPACE_SIZE - 8) + 12 + (ECMA119_VOL_SET_ID - ECMA119_VOL_SET_SIZE - 12) + (ECMA119_VOL_CREATION_DATE - ECMA119_VOL_SET_ID) + 68 + 1)];
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
        public byte[] SectorStart = new byte[7] { 0xFF, 0x43, 0x44, 0x30, 0x30, 0x31, 0x01 };

        public Ecma119Header(uint totalSectors)
        {
            Spaces.AsSpan().Fill((byte)0x20);
            Date1.AsSpan().Fill((byte)0x30);
            Date2.AsSpan().Fill((byte)0x30);
            Date3.AsSpan().Fill((byte)0x30);
            Date4.AsSpan().Fill((byte)0x30);
            Date1[^1] = (byte)0x00;
            Date2[^1] = (byte)0x00;
            Date3[^1] = (byte)0x00;
            Date4[^1] = (byte)0x00;
            VolumeSpaceLittle = Bits.ToLittle(totalSectors);
            VolumeSpaceBig = Bits.ToBig(totalSectors);
        }

        public int Size() => ECMA119_HEADER_SIZE;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class FileHeader : IMarshalable
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = OPTIMIZED_TAG_OFFSET)]
        private readonly byte[] Reserved1 = new byte[OPTIMIZED_TAG_OFFSET];
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = ECMA119_DATA_START - OPTIMIZED_TAG_OFFSET)]
        private readonly byte[] OptimizedTag = new byte[ECMA119_DATA_START - OPTIMIZED_TAG_OFFSET];
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = ECMA119_HEADER_SIZE)]
        private readonly byte[] Ecma119Header = new byte[ECMA119_HEADER_SIZE];
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAGIC_OFFSET - ECMA119_DATA_START + ECMA119_HEADER_SIZE)]
        private readonly byte[] Reserved3 = new byte[MAGIC_OFFSET - ECMA119_DATA_START + ECMA119_HEADER_SIZE];
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAGIC_SIZE)]
        private readonly byte[] Magic1 = new byte[MAGIC_SIZE];
        private readonly uint RootSector;
        private readonly uint RootSize;
        private readonly uint FileTimeLow;
        private readonly uint FileTimeHigh;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAGIC_UNUSED_LENGTH)]
        private readonly byte[] Reserved4 = new byte[MAGIC_UNUSED_LENGTH];
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAGIC_SIZE)]
        private readonly byte[] Magic2 = new byte[MAGIC_SIZE];

        public FileHeader(uint rootSector, uint rootSize, uint totalSectors, uint? fileTimeLow = null, uint? fileTimeHigh = null)
        {
            var ecma = new Ecma119Header(totalSectors);
            Ecma119Header = ecma.ToBytes();
            RootSector = rootSector;
            RootSize = rootSize;
            FileTimeLow = fileTimeLow ?? 0;
            FileTimeHigh = fileTimeHigh ?? 0;
            Magic1 = MAGIC.ToArray();
            Magic2 = MAGIC.ToArray();
        }

        public int Size() => MAGIC_OFFSET + SECTOR_SIZE;
    }

    public static long CalculateTotalSize(Avl.Node rootNode)
    {
        long outSize = 0;
        var nodes = new List<Avl.Node>();
        var root = rootNode;

        Avl.Tree.Traverse(Avl.Traversal.Prefix, ref root, 0, CollectNodesCb, ref nodes);

        var maxStartSector = nodes.Max(n => n.StartSector);

        if (nodes.Count > 0)
        {
            outSize = (maxStartSector * SECTOR_SIZE) + nodes.Last().FileSize;

            if ((outSize % SECTOR_SIZE) != 0)
                outSize += SECTOR_SIZE - (outSize % SECTOR_SIZE);
        }
        else
        {
            throw new ArgumentException("Root node must have at least one file or directory.");
        }

        nodes.Sort((a, b) => a.DirectoryStart.CompareTo(b.DirectoryStart));

        long currentDirStart = 0;
        long offsetInFile = 0;

        foreach (var node in nodes)
        {
            if (node.DirectoryStart != currentDirStart)
            {
                offsetInFile = node.DirectoryStart * SECTOR_SIZE;
                currentDirStart = node.DirectoryStart;
            }

            long padLen = node.DirectoryOffset + node.DirectoryStart - offsetInFile;
            long entrySize = (2 * 4) + (2 * 4) + (2 * 1) + Math.Min(Encoding.ASCII.GetByteCount(node.Filename), 0xFF);

            offsetInFile += entrySize + padLen;

            if (offsetInFile > outSize)
                outSize = offsetInFile;
        }

        if ((outSize % FILE_MODULUS) != 0)
            outSize += FILE_MODULUS - (outSize % FILE_MODULUS);

        return outSize;
    }

    public static DirectoryEntry CreateDirectoryEntry(Avl.Node node)
    {
        var entry = new DirectoryEntry();
        ref var header = ref entry.Header;
        var subDirEmpy = node.Subdirectory is Avl.EmptyNode;

        header.LeftOffset =
            (node.LeftChild != null)
                ? (ushort)(node.LeftChild.DirectoryOffset / 4)
                : (ushort)0;

        header.RightOffset =
            (node.RightChild != null)
                ? (ushort)(node.RightChild.DirectoryOffset / 4)
                : (ushort)0;

        header.StartSector = (uint)node.StartSector;

        if (node.Subdirectory != null || subDirEmpy)
        {
            header.FileSize =
                (uint)node.FileSize +
                (uint)((SECTOR_SIZE - (node.FileSize % SECTOR_SIZE)) % SECTOR_SIZE);
        }
        else
        {
            header.FileSize = (uint)node.FileSize;
        }

        header.Attributes = (node.Subdirectory != null || subDirEmpy)
            ? DirAttribute.Directory
            : DirAttribute.File;

        entry.SetName(node.Filename);

        return entry;
    }

    private static void CollectNodesCb(ref Avl.Node node, int depth, ref List<Avl.Node> nodes)
    {
        if (node is Avl.EmptyNode)
            return;

        nodes.Add(node);

        if (node.Subdirectory != null)
            Avl.Tree.Traverse(Avl.Traversal.Prefix, ref node.Subdirectory, 0, CollectNodesCb, ref nodes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint NumSectors(long value) => (uint)(((value + SECTOR_SIZE - 1) & ~(SECTOR_SIZE - 1)) >> SECTOR_SHIFT);

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public static uint AlignDown(long value) => (uint)(value & ~(SECTOR_SIZE - 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSectorAligned(long value) => (value & (SECTOR_SIZE - 1)) == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long SectorToOffset(uint sector) => (long)sector << SECTOR_SHIFT;
}
