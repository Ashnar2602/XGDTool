using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using XGDTool.Util;

namespace XGDTool.Format
{
    public class XBE
    {
        [Flags]
        public enum InitFlags : uint
        {
            MountUtilityDrive = (1 << 0),
            FormatUtilityDrive = (1 << 1),
            Limit64MB = (1 << 2),
            DontSetupHarddisk = (1 << 3)
        }

        [Flags]
        public enum SectionFlags : uint
        {
            Writable = (1 << 0),
            Preload = (1 << 1),
            Executable = (1 << 2),
            InsertedFile = (1 << 3),
            HeadPageRO = (1 << 4),
            TailPageRO = (1 << 5)
        }

        public const uint HEADER_MAGIC = 0x48454258; // "XBEH"
        public const int HEADER_SIZE = 376;
        public const int SECTION_HEADER_SIZE = 56;
        public const int CERTIFICATE_SIZE = 464;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class FileHeader : IMarshalable
        {
            public uint Magic;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public byte[] Signature = new byte[256];
            public uint BaseAddress;
            public uint SizeOfHeaders;
            public uint SizeOfImage;
            public uint SizeOfImageHeader;
            public uint TimeDateStamp;
            public uint CertificateAddress;
            public uint SectionCount;
            public uint SectionHeaderAddress;
            public InitFlags InitFlags;
            public uint EntryPointAddress;
            public uint TLSDirectoryAddress;

            public uint PeStackCommit;            // 0x0130 - size of stack commit
            public uint PeHeapReserve;            // 0x0134 - size of heap reserve
            public uint PeHeapCommit;             // 0x0138 - size of heap commit
            public uint PeBaseAddr;               // 0x013C - original base address
            public uint PeSizeofImage;            // 0x0140 - size of original image
            public uint PeChecksum;               // 0x0144 - original checksum
            public uint PeTimeDate;               // 0x0148 - original timedate stamp
            public uint DebugPathnameAddr;        // 0x014C - debug pathname address
            public uint DebugFilenameAddr;        // 0x0150 - debug filename address
            public uint DebugUnicodeFilenameAddr; // 0x0154 - debug unicode filename address
            public uint KernelImageThunkAddr;     // 0x0158 - kernel image thunk address
            public uint NonKernelImportDirAddr;   // 0x015C - non kernel import directory address
            public uint LibraryVersions;          // 0x0160 - number of library versions
            public uint LibraryVersionsAddr;      // 0x0164 - library versions address
            public uint KernelLibraryVersionAddr; // 0x0168 - kernel library version address
            public uint XAPILibraryVersionAddr;   // 0x016C - xapi library version address
            public uint LogoBitmapAddr;           // 0x0170 - logo bitmap address
            public uint SizeofLogoBitmap;         // 0x0174 - logo bitmap size

            protected override int GetMarshalSize() => XBE.HEADER_SIZE;
            public bool IsValid() => (Magic == XBE.HEADER_MAGIC);
        }

        //public class Key
        //{
        //    public byte[] Data = new byte[16];
        //}

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class CertificateHeader : IMarshalable
        {
            public uint Size;                                           // 0x0000 - size of certificate
            public uint TimeDate;                                       // 0x0004 - timedate stamp
            public uint TitleId;                                        // 0x0008 - title id
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
            private ushort[] TitleName = new ushort[40];                 // 0x000C - title name (unicode)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public uint[] AlternateTitleId = new uint[16];              // 0x005C - alternate title ids
            public uint AllowedMedia;                                   // 0x009C - allowed media types
            public uint GameRegion;                                     // 0x00A0 - game region
            public uint GameRatings;                                    // 0x00A4 - game ratings
            public uint DiskNumber;                                     // 0x00A8 - disk number
            public uint Version;                                        // 0x00AC - version
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] LanKey = new byte[16];                        // 0x00B0 - lan key
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] SignatureKey = new byte[16];                  // 0x00C0 - signature key
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public byte[] TitleAlternateSignatureKey = new byte[256];   // 0x00D0 - alternate signature keys (16 keys * 16 bytes)

            protected override int GetMarshalSize() => XBE.CERTIFICATE_SIZE;

            public String GetTitleName()
            {
                ReadOnlySpan<byte> bytes = MemoryMarshal.Cast<ushort, byte>(TitleName);
                return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
            }

            public void SetTitleName(String name)
            {
                Array.Clear(TitleName, 0, TitleName.Length);
                var maxChars = Math.Min(name.Length, TitleName.Length);
                for (int i = 0; i < maxChars; i++)
                {
                    TitleName[i] = name[i];
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class SectionHeader : IMarshalable
        {
            public SectionFlags Flags;
            public uint VirtualAddr;                                // virtual address
            public uint VirtualSize;                                // virtual size
            public uint RawAddr;                                    // file offset to raw data
            public uint SizeofRaw;                                  // size of raw data
            public uint SectionNameAddr;                            // section name addr
            public uint SectionRefCount;                            // section reference count
            public uint HeadSharedRefCountAddr;                     // head shared page reference count address
            public uint TailSharedRefCountAddr;                     // tail shared page reference count address
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] SectionDigest = new byte[20];             // section digest

            protected override int GetMarshalSize() => XBE.SECTION_HEADER_SIZE;
        }

        public FileHeader Header = new FileHeader();
        public CertificateHeader Certificate = new CertificateHeader();
        public List<SectionHeader> Sections = new List<SectionHeader>();
        private byte[] BitmapData = Array.Empty<byte>();

        public XBE() { }

        public void FromBytes(byte[] data)
        {
            if (data.Length < XBE.HEADER_SIZE)
                throw new ArgumentException("Data is too small to be a valid XBE file.");

            Header.FromBytes(data[..XBE.HEADER_SIZE]);

            var certOffset = (int)(Header.CertificateAddress - Header.BaseAddress);
            if (certOffset < 0 || certOffset + XBE.CERTIFICATE_SIZE > data.Length)
                throw new ArgumentException("Certificate address is out of bounds.");

            Certificate.FromBytes(data[certOffset..(certOffset + XBE.CERTIFICATE_SIZE)]);

            var sectionsOffset = (int)(Header.SectionHeaderAddress - Header.BaseAddress);
            if (sectionsOffset < 0 || 
                sectionsOffset + Header.SectionCount * XBE.SECTION_HEADER_SIZE > data.Length)
                throw new ArgumentException("Section headers address is out of bounds.");

            for (int i = 0; i < Header.SectionCount; i++)
            {
                var sectionData = data[
                    (sectionsOffset + i * XBE.SECTION_HEADER_SIZE)..
                    (sectionsOffset + (i + 1) * XBE.SECTION_HEADER_SIZE)
                ];
                var sectionHeader = new SectionHeader();
                sectionHeader.FromBytes(sectionData);
                Sections.Add(sectionHeader);
            }

            var bitmapOffset = (int)(Header.LogoBitmapAddr - Header.BaseAddress);
            if (bitmapOffset < 0 || bitmapOffset + Header.SizeofLogoBitmap > data.Length)
                throw new ArgumentException("Logo bitmap address is out of bounds.");

            BitmapData = data[bitmapOffset..(bitmapOffset + (int)Header.SizeofLogoBitmap)];
        }

        public void SetBitmapData(byte[] data)
        {
            if (BitmapData.Length > Header.SizeofLogoBitmap)
                throw new ArgumentException("Bitmap data is too large.");

            BitmapData = data;
            Header.SizeofLogoBitmap = (uint)data.Length;
        }

        public byte[] GetBitmapData() => BitmapData;
    }
}
