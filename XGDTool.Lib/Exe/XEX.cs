using System.Buffers.Binary;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Exe;

public static class XEX
{
    public enum EntryKey : uint
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

	public static uint MAGIC => BinaryPrimitives.ReadUInt32BigEndian("XEX2"u8);
    public const int TITLE_NAME_MAX_CHARS = 40;
    public const int TITLE_NAME_MAX_LENGTH = TITLE_NAME_MAX_CHARS * 2;

	public class FileHeader : ISerializable
    {
		public uint Magic;
		public uint ModuleFlags;
		public uint SizeOfHeaders;
		public uint SizeOfDiscardableHeaders;
		public uint SecurityInfo;
		public uint HeaderCount;

        public const int SIZE = 0x18;
		public int Size() => SIZE;
		public bool IsValid() => (Magic == MAGIC);
		
		public void Deserialize(ReadOnlySpan<byte> data)
		{
			if (data.Length < SIZE)
				throw new ArgumentException($"Data must be at least {SIZE} bytes long", nameof(data));
			
			Magic = BinaryPrimitives.ReadUInt32BigEndian(data);
			ModuleFlags = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4));
			SizeOfHeaders = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(8, 4));
			SizeOfDiscardableHeaders = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(12, 4));
			SecurityInfo = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(16, 4));
			HeaderCount = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(20, 4));
		}

		public void Serialize(Span<byte> buffer)
		{
			if (buffer.Length < SIZE)
				throw new ArgumentException($"Buffer length must be at least {SIZE} bytes.", nameof(buffer));

			BinaryPrimitives.WriteUInt32BigEndian(buffer, Magic);
			BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(4, 4), ModuleFlags);
			BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(8, 4), SizeOfHeaders);
			BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(12, 4), SizeOfDiscardableHeaders);
			BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(16, 4), SecurityInfo);
			BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(20, 4), HeaderCount);
		}
    }

	public class DirectoryEntry : ISerializable
	{
		public EntryKey Key;
		public uint Value;

        public const int SIZE = 8;
		public int Size() => SIZE;
		public void Deserialize(ReadOnlySpan<byte> data)
		{
			if (data.Length < SIZE)
				throw new ArgumentException($"Data must be at least {SIZE} bytes long", nameof(data));

			Key = (EntryKey)BinaryPrimitives.ReadUInt32BigEndian(data);
			Value = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4));
		}
		public void Serialize(Span<byte> buffer)
		{
			if (buffer.Length < SIZE)
				throw new ArgumentException($"Buffer length must be at least {SIZE} bytes.", nameof(buffer));

			BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)Key);
			BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(4, 4), Value);
		}
    }

	public class ExecutionInfo : ISerializable
	{
		public uint MediaId;
        public uint Version;
		public uint BaseVersion;
		public uint TitleId;
		public byte Platform;
		public byte ExecutableType;
		public byte DiscNumber;
		public byte DiscCount;
		public uint SavegameId;

        public byte VersionMajor { get => Bits.Get4At(Version, 0); set => Version = Bits.Set4At(Version, value, 0); }
        public byte VersionMinor { get => Bits.Get4At(Version, 4); set => Version = Bits.Set4At(Version, value, 4); }
        public ushort VersionBuild { get => Bits.Get16At(Version, 8); set => Version = Bits.Set16At(Version, value, 8); }
        public byte VersionQfe { get => Bits.Get8At(Version, 24); set => Version = Bits.Set8At(Version, value, 24); }

        public byte BaseMajor { get => Bits.Get4At(BaseVersion, 0); set => BaseVersion = Bits.Set4At(BaseVersion, value, 0); }
        public byte BaseMinor { get => Bits.Get4At(BaseVersion, 4); set => BaseVersion = Bits.Set4At(BaseVersion, value, 4); }
        public ushort BaseBuild { get => Bits.Get16At(BaseVersion, 8); set => BaseVersion = Bits.Set16At(BaseVersion, value, 8); }
        public byte BaseQfe { get => Bits.Get8At(BaseVersion, 24); set => BaseVersion = Bits.Set8At(BaseVersion, value, 24); }

        public ushort PublisherId { get => Bits.Upper16(TitleId); set => TitleId = Bits.Combine32(value, GameId); }
        public ushort GameId { get => Bits.Lower16(TitleId); set => TitleId = Bits.Combine32(PublisherId, value); }

        public const int SIZE = 24;
		public int Size() => SIZE;
		public void Deserialize(ReadOnlySpan<byte> data)
		{
			if (data.Length < SIZE)
				throw new ArgumentException($"Data must be at least {SIZE} bytes long", nameof(data));

			MediaId = BinaryPrimitives.ReadUInt32BigEndian(data);
			Version = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4));
			BaseVersion = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(8, 4));
			TitleId = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(12, 4));
			Platform = data[16];
			ExecutableType = data[17];
			DiscNumber = data[18];
			DiscCount = data[19];
			SavegameId = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(20, 4));
		}
		public void Serialize(Span<byte> buffer)
		{
			if (buffer.Length < SIZE)
				throw new ArgumentException($"Buffer length must be at least {SIZE} bytes.", nameof(buffer));

			BinaryPrimitives.WriteUInt32BigEndian(buffer, MediaId);
			BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(4, 4), Version);
			BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(8, 4), BaseVersion);
			BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(12, 4), TitleId);
			buffer[16] = Platform;
			buffer[17] = ExecutableType;
			buffer[18] = DiscNumber;
			buffer[19] = DiscCount;
			BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(20, 4), SavegameId);
		}
    }
}
