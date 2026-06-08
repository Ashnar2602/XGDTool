using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Exe;

public static class XBE
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

    [Flags]
    public enum AllowedMedia : uint
    {
        HARD_DISK = 0x00000001,
        XGD1 = 0x00000002,
        DVD_CD = 0x00000004,
        CD = 0x00000008,
        DVD_5_RO = 0x00000010,
        DVD_9_RO = 0x00000020,
        DVD_5_RW = 0x00000040,
        DVD_9_RW = 0x00000080,
        DONGLE = 0x00000100,
        MEDIA_BOARD = 0x00000200,
        NONSECURE_HARD_DISK = 0x40000000,
        NONSECURE_MODE = 0x80000000,

        //Mask = 0x00FFFFFF
    }

    public enum Region : uint
    {
        [Description("USA")]
        USA = 0x00000001,
        [Description("JPN")]
        JPN = 0x00000002,
        [Description("PAL")]
        PAL = 0x00000004,
        [Description("GLO")]
        GLO = 0x00000007,
        [Description("TST")]
        TST = 0x40000000,
        [Description("DBG")]
        DBG = 0x80000000
    }

    public static uint MAGIC => BinaryPrimitives.ReverseEndianness(Bits.UintFromString("XBEH"));
    public const int HEADER_SIZE = 376;
    public const int SECTION_HEADER_SIZE = 56;
    public const int CERTIFICATE_SIZE = 464;
    public const int TITLE_NAME_CHARS_MAX = 40;
    public const int TITLE_NAME_BYTE_COUNT = TITLE_NAME_CHARS_MAX * 2;
    public const int FATX_MAX_FILENAME_LENGTH = 42;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class FileHeader : IMarshalable
    {
        public uint Magic;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (2048 / 8))]
        public byte[] Signature = new byte[2048 / 8];
        public uint BaseAddress; //memory load address
        public uint SizeOfHeaders;
        public uint SizeOfImage;
        public uint SizeOfImageHeader;
        public uint TimeStamp; //unix time
        public uint Certificate; //memory address of certificate
        public uint NumberOfSections;
        public uint SectionHeaders; //memory address of section headers
        public InitFlags InitFlags;
        public uint AddressOfEntryPoint; //memory address of entry point (typedef VOID (*PXBEIMAGE_ENTRY_POINT)(VOID)) - XOR ENCODED
        public uint TlsDirectory; //memory address of thread local storage (can be zeroed) (IMAGE_TLS_DIRECTORY*) - XOR ENCODED
        public uint SizeOfStackCommit; //default thread stack size
        public uint SizeOfHeapReserve;
        public uint SizeOfHeapCommit;
        public uint NtBaseOfDll; //memory address of PE header (can be zeroed)
        public uint NtSizeOfImage;
        public uint NtChecksum;
        public uint NtTimestamp;
        public uint DebugPathName; //memory address of string representing the debug executable file path (utf8)
        public uint DebugFileName; //memory address of string representing the debug executable file name (utf8)
        public uint DebugUnicodeFileName; //memory address of string representing the debug executable file name (unicode)
        public uint XboxKernelThunkData; //memory address of imported kernel thunks
        public uint ImportDirectory; //memory address of imported non-kernel thunks (zeroed on retail executables)
        public uint NumberOfLibraryVersions;
        public uint LibraryVersion; //memory address of library version structs
        public uint XboxKernelLibraryVersion; //memory address of kernel version library struct
        public uint XapiLibraryVersion; //memory address of XAPI version library struct
        public uint MicrosoftLogo; //memory address of the Microsoft logo
        public uint SizeOfMicrosoftLogo;

        public int Size() => HEADER_SIZE;
        public bool IsValid() => (Magic == MAGIC);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class CertificateHeader : IMarshalable
    {
        public uint SizeOfCertificate;
        public uint TimeStamp; //unix time
        public uint TitleID;                                       // 0x0008 - title id
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
        private ushort[] TitleName = new ushort[40];                 // 0x000C - title name (unicode)
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public uint[] AlternateTitleId = new uint[16];              // 0x005C - alternate title ids
        public AllowedMedia AllowedMedia;                           // 0x009C - allowed media types
        public Region GameRegion;                                   // 0x00A0 - game region
        public uint GameRatings;                                    // 0x00A4 - game ratings
        public uint DiskNumber;                                     // 0x00A8 - disk number
        public uint Version;                                        // 0x00AC - version
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] LanKey = new byte[16];                        // 0x00B0 - lan key
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] SignatureKey = new byte[16];                  // 0x00C0 - signature key
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] TitleAlternateSignatureKey = new byte[256];   // 0x00D0 - alternate signature keys (16 keys * 16 bytes)

        public int Size() => CERTIFICATE_SIZE;

        public string GetTitleName()
        {
            ReadOnlySpan<byte> bytes = MemoryMarshal.Cast<ushort, byte>(TitleName);
            return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
        }

        public void SetTitleName(string name)
        {
            Array.Clear(TitleName, 0, TitleName.Length);
            var maxChars = Math.Min(name.Length, TitleName.Length);

            for (int i = 0; i < maxChars; i++)
                TitleName[i] = name[i];
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

        public int Size() => SECTION_HEADER_SIZE;
    }
}
