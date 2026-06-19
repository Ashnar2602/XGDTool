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
        MountUtilityDrive = 1 << 0,
        FormatUtilityDrive = 1 << 1,
        Limit64MB = 1 << 2,
        DontSetupHarddisk = 1 << 3
    }

    [Flags]
    public enum SectionFlags : uint
    {
        Writable = 1 << 0,
        Preload = 1 << 1,
        Executable = 1 << 2,
        InsertedFile = 1 << 3,
        HeadPageRO = 1 << 4,
        TailPageRO = 1 << 5
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
        [Description("UNK")]
        UNK = 0,
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

    public static uint MAGIC => StringExt.GetUint("XBEH");
    public const int TITLE_NAME_CHARS_MAX = 40;
    public const int TITLE_NAME_BYTE_COUNT = TITLE_NAME_CHARS_MAX * 2;
    public const int FATX_MAX_FILENAME_LENGTH = 42;

    public class FileHeader : ISerializable
    {
        public uint Magic;
        public readonly byte[] Signature = new byte[2048 / 8];
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

        public const int SIZE = 376;
        public int Size() => SIZE;
        public bool IsValid() => (Magic == MAGIC);

        public void Serialize(Span<byte> buffer)
        {
            if (buffer.Length < SIZE)
                throw new ArgumentException($"Buffer length must be at least {SIZE} bytes.", nameof(buffer));

            BinaryPrimitives.WriteUInt32LittleEndian(buffer, Magic);
            Signature.CopyTo(buffer.Slice(4, Signature.Length));
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(4 + Signature.Length, 4), BaseAddress);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(8 + Signature.Length, 4), SizeOfHeaders);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(12 + Signature.Length, 4), SizeOfImage);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(16 + Signature.Length, 4), SizeOfImageHeader);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(20 + Signature.Length, 4), TimeStamp);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(24 + Signature.Length, 4), Certificate);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(28 + Signature.Length, 4), NumberOfSections);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(32 + Signature.Length, 4), SectionHeaders);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(36 + Signature.Length, 4), (uint)InitFlags);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(40 + Signature.Length, 4), AddressOfEntryPoint);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(44 + Signature.Length, 4), TlsDirectory);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(48 + Signature.Length, 4), SizeOfStackCommit);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(52 + Signature.Length, 4), SizeOfHeapReserve);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(56 + Signature.Length, 4), SizeOfHeapCommit);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(60 + Signature.Length, 4), NtBaseOfDll);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(64 + Signature.Length, 4), NtSizeOfImage);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(68 + Signature.Length, 4), NtChecksum);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(72 + Signature.Length, 4), NtTimestamp);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(76 + Signature.Length, 4), DebugPathName);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(80 + Signature.Length, 4), DebugFileName);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(84 + Signature.Length, 4), DebugUnicodeFileName);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(88 + Signature.Length, 4), XboxKernelThunkData);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(92 + Signature.Length, 4), ImportDirectory);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(96 + Signature.Length, 4), NumberOfLibraryVersions);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(100 + Signature.Length, 4), LibraryVersion);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(104 + Signature.Length, 4), XboxKernelLibraryVersion);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(108 + Signature.Length, 4), XapiLibraryVersion);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(112 + Signature.Length, 4), MicrosoftLogo);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(116 + Signature.Length, 4), SizeOfMicrosoftLogo);
        }

        public void Deserialize(ReadOnlySpan<byte> data)
        {
            if (data.Length < SIZE)
                throw new ArgumentException($"Data must be at least {SIZE} bytes long", nameof(data));

            Magic = BinaryPrimitives.ReadUInt32LittleEndian(data);
            data.Slice(4, Signature.Length).CopyTo(Signature);
            BaseAddress = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4 + Signature.Length, 4));
            SizeOfHeaders = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8 + Signature.Length, 4));
            SizeOfImage = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12 + Signature.Length, 4));
            SizeOfImageHeader = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16 + Signature.Length, 4));
            TimeStamp = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(20 + Signature.Length, 4));
            Certificate = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(24 + Signature.Length, 4));
            NumberOfSections = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(28 + Signature.Length, 4));
            SectionHeaders = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(32 + Signature.Length, 4));
            InitFlags = (InitFlags)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(36 + Signature.Length, 4));
            AddressOfEntryPoint = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(40 + Signature.Length, 4));
            TlsDirectory = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(44 + Signature.Length, 4));
            SizeOfStackCommit = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(48 + Signature.Length, 4));
            SizeOfHeapReserve = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(52 + Signature.Length, 4));
            SizeOfHeapCommit = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(56 + Signature.Length, 4));
            NtBaseOfDll = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(60 + Signature.Length, 4));
            NtSizeOfImage = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(64 + Signature.Length, 4));
            NtChecksum = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(68 + Signature.Length, 4));
            NtTimestamp = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(72 + Signature.Length, 4));
            DebugPathName = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(76 + Signature.Length, 4));
            DebugFileName = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(80 + Signature.Length, 4));
            DebugUnicodeFileName = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(84 + Signature.Length, 4));
            XboxKernelThunkData = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(88 + Signature.Length, 4));
            ImportDirectory = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(92 + Signature.Length, 4));
            NumberOfLibraryVersions = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(96 + Signature.Length, 4));
            LibraryVersion = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(100 + Signature.Length, 4));
            XboxKernelLibraryVersion = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(104 + Signature.Length, 4));
            XapiLibraryVersion = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(108 + Signature.Length, 4));
            MicrosoftLogo = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(112 + Signature.Length, 4));
            SizeOfMicrosoftLogo = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(116 + Signature.Length, 4));
        }
    }

    public class CertificateHeader : ISerializable
    {
        public uint SizeOfCertificate;
        public uint TimeStamp; //unix time
        public uint TitleID;                                        // 0x0008 - title id
        public readonly ushort[] TitleName = new ushort[40];        // 0x000C - title name (unicode)
        public readonly uint[] AlternateTitleId = new uint[16];     // 0x005C - alternate title ids
        public AllowedMedia AllowedMedia;                           // 0x009C - allowed media types
        public Region GameRegion;                                   // 0x00A0 - game region
        public uint GameRatings;                                    // 0x00A4 - game ratings
        public uint DiskNumber;                                     // 0x00A8 - disk number
        public uint Version;                                        // 0x00AC - version
        public readonly byte[] LanKey = new byte[16];                        // 0x00B0 - lan key
        public readonly byte[] SignatureKey = new byte[16];                  // 0x00C0 - signature key
        public readonly byte[] TitleAlternateSignatureKey = new byte[256];   // 0x00D0 - alternate signature keys (16 keys * 16 bytes)

        public const int SIZE = 464;
        public int Size() => SIZE;

        public void Deserialize(ReadOnlySpan<byte> data)
        {
            if (data.Length < SIZE)
                throw new ArgumentException($"Data must be at least {SIZE} bytes long", nameof(data));

            SizeOfCertificate = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x0000, 4));
            TimeStamp = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x0004, 4));
            TitleID = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x0008, 4));
            AllowedMedia = (AllowedMedia)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x009C, 4));
            GameRegion = (Region)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x00A0, 4));
            GameRatings = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x00A4, 4));
            DiskNumber = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x00A8, 4));
            Version = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x00AC, 4));

            for (int i = 0; i < TitleName.Length; i++)
            {
                int offset = 0x000C + (i * 2);
                TitleName[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));
            }

            for (int i = 0; i < AlternateTitleId.Length; i++)
            {
                int offset = 0x005C + (i * 4);
                AlternateTitleId[i] = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
            }

            data.Slice(0x00B0, 16).CopyTo(LanKey);
            data.Slice(0x00C0, 16).CopyTo(SignatureKey);
            data.Slice(0x00D0, 256).CopyTo(TitleAlternateSignatureKey);
        }

        public void Serialize(Span<byte> buffer)
        {
            if (buffer.Length < SIZE)
                throw new ArgumentException($"Buffer length must be at least {SIZE} bytes.", nameof(buffer));

            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(0x0000, 4), SizeOfCertificate);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(0x0004, 4), TimeStamp);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(0x0008, 4), TitleID);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(0x009C, 4), (uint)AllowedMedia);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(0x00A0, 4), (uint)GameRegion);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(0x00A4, 4), GameRatings);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(0x00A8, 4), DiskNumber);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(0x00AC, 4), Version);

            for (int i = 0; i < TitleName.Length; i++)
            {
                int offset = 0x000C + (i * 2);
                BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(offset, 2), TitleName[i]);
            }

            for (int i = 0; i < AlternateTitleId.Length; i++)
            {
                int offset = 0x005C + (i * 4);
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset, 4), AlternateTitleId[i]);
            }

            LanKey.CopyTo(buffer.Slice(0x00B0, 16));
            SignatureKey.CopyTo(buffer.Slice(0x00C0, 16));
            TitleAlternateSignatureKey.CopyTo(buffer.Slice(0x00D0, 256));
        }
    }

    public class SectionHeader : ISerializable
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
        public readonly byte[] SectionDigest = new byte[20];    // section digest

        public const int SIZE = 56;
        public int Size() => SIZE;
        public void Deserialize(ReadOnlySpan<byte> data)
        {
            if (data.Length < SIZE)
                throw new ArgumentException($"Data must be at least {SIZE} bytes long", nameof(data));

            Flags = (SectionFlags)BinaryPrimitives.ReadUInt32LittleEndian(data);
            VirtualAddr = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
            VirtualSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4));
            RawAddr = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12, 4));
            SizeofRaw = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4));
            SectionNameAddr = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(20, 4));
            SectionRefCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(24, 4));
            HeadSharedRefCountAddr = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(28, 4));
            TailSharedRefCountAddr = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(32, 4));
            data.Slice(36, 20).CopyTo(SectionDigest);
        }
        public void Serialize(Span<byte> buffer)
        {
            if (buffer.Length < SIZE)
                throw new ArgumentException($"Buffer length must be at least {SIZE} bytes.", nameof(buffer));

            BinaryPrimitives.WriteUInt32LittleEndian(buffer, (uint)Flags);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(4, 4), VirtualAddr);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(8, 4), VirtualSize);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(12, 4), RawAddr);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(16, 4), SizeofRaw);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(20, 4), SectionNameAddr);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(24, 4), SectionRefCount);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(28, 4), HeadSharedRefCountAddr);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(32, 4), TailSharedRefCountAddr);
            SectionDigest.CopyTo(buffer.Slice(36, 20));
        }
    }
}
