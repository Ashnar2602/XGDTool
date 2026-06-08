using System.Runtime.InteropServices;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Exe;

public static class XEX
{
    public enum DirectoryEntryKey : uint
    {
		RESOURCE_INFO                 = 0x000002FF,
		FILE_FORMAT_INFO              = 0x000003FF,
		DELTA_PATCH_DESCRIPTOR        = 0x000005FF,
		BASE_REFERENCE                = 0x00000405,
		BOUNDING_PATH                 = 0x000080FF,
		DEVICE_ID                     = 0x00008105,
		ORIGINAL_BASE_ADDRESS         = 0x00010001,
		ENTRY_POINT                   = 0x00010100,
		IMAGE_BASE_ADDRESS            = 0x00010201,
		IMPORT_LIBRARIES              = 0x000103FF,
		CHECKSUM_TIMESTAMP            = 0x00018002,
		ENABLED_FOR_CALLCAP           = 0x00018102,
		ENABLED_FOR_FASTCAP           = 0x00018200,
		ORIGINAL_PE_NAME              = 0x000183FF,
		STATIC_LIBRARIES              = 0x000200FF,
		TLS_INFO                      = 0x00020104,
		DEFAULT_STACK_SIZE            = 0x00020200,
		DEFAULT_FILESYSTEM_CACHE_SIZE = 0x00020301,
		DEFAULT_HEAP_SIZE             = 0x00020401,
		PAGE_HEAP_SIZE_AND_FLAGS      = 0x00028002,
		SYSTEM_FLAGS                  = 0x00030000,
		EXECUTION_INFO                = 0x00040006,
		TITLE_WORKSPACE_SIZE          = 0x00040201,
		GAME_RATINGS                  = 0x00040310,
		LAN_KEY                       = 0x00040404,
		XBOX360_LOGO                  = 0x000405FF,
		MULTIDISC_MEDIA_IDS           = 0x000406FF,
		ALTERNATE_TITLE_IDS           = 0x000407FF,
		ADDITIONAL_TITLE_MEMORY       = 0x00040801,
		EXPORTS_BY_NAME               = 0x00E10402
    }

	public static uint MAGIC => Bits.FromBig(Bits.UintFromString("XEX2"));
	public const int HEADER_SIZE = 0x18;
	public const int DIRECTORY_ENTRY_SIZE = 8;
	public const int EXECUTION_INFO_SIZE = 24;
    public const int TITLE_NAME_MAX_CHARS = 40;
    public const int TITLE_NAME_MAX_LENGTH = TITLE_NAME_MAX_CHARS * 2;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
	public class FileHeader : IMarshalable
    {
		private readonly uint _Magic;
		private readonly uint _ModuleFlags;
		private readonly uint _SizeOfHeaders;
		private readonly uint _SizeOfDiscardableHeaders;
		private readonly uint _SecurityInfo;
		private readonly uint _HeaderCount;

		public uint Magic => Bits.FromBig(_Magic);
        public uint HeaderCount => Bits.FromBig(_HeaderCount);

        public int Size() => HEADER_SIZE;
		public bool IsValid() => (Magic == MAGIC);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
	public class DirectoryEntry : IMarshalable
	{
		private readonly uint _Key;
		private readonly uint _Value;

		public DirectoryEntryKey Key => (DirectoryEntryKey)Bits.FromBig(_Key);
		public uint Value => Bits.FromBig(_Value);

        public int Size() => DIRECTORY_ENTRY_SIZE;
    }

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public class ExecutionInfo : IMarshalable
	{
		private uint _MediaId;
        private uint _Version;
		private uint _BaseVersion;
		private uint _TitleId;
		public byte Platform;
		public byte ExecutableType;
		public byte DiscNumber;
		public byte DiscCount;
		private uint _SavegameId;

        public uint MediaId
        {
            get { return Bits.FromBig(_MediaId); }
            set { _MediaId = Bits.ToBig(value); }
        }

        public uint Version
        {
            get { return Bits.FromBig(_Version); }
            set { _Version = Bits.ToBig(value); }
        }

        public uint BaseVersion
        {
            get { return Bits.FromBig(_BaseVersion); }
            set { _BaseVersion = Bits.ToBig(value); }
        }

        public uint TitleId
        {
            get { return Bits.FromBig(_TitleId); }
            set { _TitleId = Bits.ToBig(value); }
        }

        // version bitfields
        public uint VersionMajor { get => (Version >> 28) & 0xF; set => Version = (Version & 0x0FFFFFFF) | ((value & 0xF) << 28); }
        public uint VersionMinor { get => (Version >> 24) & 0xF; set => Version = (Version & 0xF0FFFFFF) | ((value & 0xF) << 24); }
        public uint VersionBuild { get => (Version >> 8) & 0xFFFF; set => Version = (Version & 0xFF0000FF) | ((value & 0xFFFF) << 8); }
        public uint VersionQfe { get => Version & 0xFF; set => Version = (Version & 0xFFFFFF00) | (value & 0xFF); }

        // base version bitfields
        public uint BaseMajor { get => (BaseVersion >> 28) & 0xF; set => BaseVersion = (BaseVersion & 0x0FFFFFFF) | ((value & 0xF) << 28); }
        public uint BaseMinor { get => (BaseVersion >> 24) & 0xF; set => BaseVersion = (BaseVersion & 0xF0FFFFFF) | ((value & 0xF) << 24); }
        public uint BaseBuild { get => (BaseVersion >> 8) & 0xFFFF; set => BaseVersion = (BaseVersion & 0xFF0000FF) | ((value & 0xFFFF) << 8); }
        public uint BaseQfe { get => BaseVersion & 0xFF; set => BaseVersion = (BaseVersion & 0xFFFFFF00) | (value & 0xFF); }

        // title union view
        public ushort PublisherId { get => (ushort)(TitleId >> 16); set => TitleId = (TitleId & 0x0000FFFF) | ((uint)value << 16); }
        public ushort GameId { get => (ushort)(TitleId & 0xFFFF); set => TitleId = (TitleId & 0xFFFF0000) | value; }

        public int Size() => EXECUTION_INFO_SIZE;
    }
}
