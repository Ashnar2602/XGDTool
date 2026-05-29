using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Buffers.Binary;
using XGDTool.Util;

namespace XGDTool.Format
{
    public static class XISO
    {
        public const int SECTOR_SIZE    = 2048;
        public const byte PAD_BYTE       = 0xFF;
        public const ushort PAD_WORD     = 0xFFFF;
        public const long FILE_MODULUS   = 0x10000;

        public const uint REDUMP_VIDEO_SECTORS = 0x30600;
        public const uint REDUMP_TOTAL_SECTORS = 0x3A4D50;
        public const uint REDUMP_GAME_SECTORS  = REDUMP_TOTAL_SECTORS - REDUMP_VIDEO_SECTORS;
        public const uint SPLIT_MARGIN         = 0xFF000000;

        public static ReadOnlySpan<byte> MAGIC => "MICROSOFT*XBOX*MEDIA"u8;
        public const int MAGIC_SIZE          = 20;
        public const int MAGIC_OFFSET        = 0x10000;
        public const int MAGIC_UNUSED_LENGTH = 0x7c8;

        public const long LSEEK_OFFSET_GLOBAL = 0x0FD90000;
        public const long LSEEK_OFFSET_XGD3   = 0x02080000;
        public const long LSEEK_OFFSET_XGD1   = 0x18300000;
        public const uint ROOT_DIRECTORY_SECTOR = 0x108;

        private const int ECMA119_HEADER_SIZE       = SECTOR_SIZE + 7;
        private const int ECMA119_DATA_START        = 0x8000;
        private const int ECMA119_VOL_SPACE_SIZE    = ECMA119_DATA_START + 80;
        private const int ECMA119_VOL_SET_SIZE      = ECMA119_DATA_START + 120;
        private const int ECMA119_VOL_SET_ID        = ECMA119_DATA_START + 190;
        private const int ECMA119_VOL_CREATION_DATE = ECMA119_DATA_START + 813;

        private const int OPTIMIZED_TAG_OFFSET = 31337;
        public const int DIRECTORY_HEADER_SIZE = 16;

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

            public override int Size() => DIRECTORY_HEADER_SIZE;
        }

        public class DirectoryEntry
        {
            public DirectoryHeader Header = new();
            private string Name = "";

            public string GetName() => Name;

            public void SetNameFromBytes(byte[] nameBytes)
            {
                if (nameBytes.Length > 0xFF)
                    throw new ArgumentException("Name bytes length cannot exceed 255.");

                var nameLen = Header.NameLength != 0 ? Header.NameLength : 0xFF;
                Name = Encoding.ASCII.GetString(nameBytes, 0, nameLen);

                if (nameLen == 0xFF)
                {
                    Name = Name.TrimEnd('\0');
                    Header.NameLength = (byte)Name.Length;
                }
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

            public Ecma119Header()
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
            }

            public override int Size() => ECMA119_HEADER_SIZE;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class FileHeader : IMarshalable
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = XISO.OPTIMIZED_TAG_OFFSET)]
            private readonly byte[] Reserved1 = new byte[XISO.OPTIMIZED_TAG_OFFSET];
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = XISO.ECMA119_DATA_START - XISO.OPTIMIZED_TAG_OFFSET)]
            private readonly byte[] OptimizedTag = new byte[XISO.ECMA119_DATA_START - XISO.OPTIMIZED_TAG_OFFSET];
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ECMA119_HEADER_SIZE)]
            private readonly byte[] Ecma119Header = new byte[ECMA119_HEADER_SIZE];
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)MAGIC_OFFSET - ECMA119_DATA_START + ECMA119_HEADER_SIZE)]
            private readonly byte[] Reserved3 = new byte[MAGIC_OFFSET - ECMA119_DATA_START + ECMA119_HEADER_SIZE];
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)MAGIC_SIZE)]
            private readonly byte[] Magic1 = new byte[MAGIC_SIZE];
            private readonly uint RootSector;
            private readonly uint RootSize;
            private readonly uint FileTimeLow;
            private readonly uint FileTimeHigh;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAGIC_UNUSED_LENGTH)]
            private readonly byte[] Reserved4 = new byte[MAGIC_UNUSED_LENGTH];
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)MAGIC_SIZE)]
            private readonly byte[] Magic2 = new byte[MAGIC_SIZE];

            public FileHeader(uint rootSector, uint rootSize, uint totalSectors, uint fileTimeLow, uint fileTimeHigh)
            {
                Ecma119Header ecmaHeader = new();
                ecmaHeader.VolumeSpaceLittle = totalSectors;
                ecmaHeader.VolumeSpaceBig = BinaryPrimitives.ReverseEndianness(totalSectors);
                
                Ecma119Header = ecmaHeader.ToBytes();
                RootSector = rootSector;
                RootSize = rootSize;
                FileTimeLow = fileTimeLow;
                FileTimeHigh = fileTimeHigh;
                Magic1 = MAGIC.ToArray();
                Magic2 = MAGIC.ToArray();
            }

            public override int Size() => MAGIC_OFFSET + SECTOR_SIZE;
        }
    }
}
