using System.Buffers.Binary;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Image.Formats;

public static class ZAR
{
    public const int COMPRESSED_BLOCK_SIZE = 64 * 1024;
    public const uint ENTRY_TYPE_FILE = 0x80000000;

    public class CompressedOffsetRecord : ISerializable
    {
        public ulong BaseOffset;
        private readonly List<ushort> _SizeTable = [];
        public IReadOnlyList<ushort> SizeTableEntries => _SizeTable;

        public const int ENTRIES_MAX = 16;
        public const int SIZE = sizeof(ulong) + (sizeof(ushort) * ENTRIES_MAX);

        public int Size() => SIZE;

        public bool AddSize(ushort size)
        {
            if (_SizeTable.Count >= ENTRIES_MAX)
                return false;

            _SizeTable.Add(size);
            return true;
        }

        public void Serialize(Span<byte> buffer)
        {
            if (buffer.Length < SIZE)
                throw new ArgumentException($"Buffer length must be at least {SIZE} bytes.", nameof(buffer));

            BinaryPrimitives.WriteUInt64BigEndian(buffer, BaseOffset);
            for (int i = 0; i < ENTRIES_MAX; i++)
            {
                ushort size = i < _SizeTable.Count ? _SizeTable[i] : (ushort)0;
                BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(8 + (i * 2), 2), size);
            }
        }

        public void Deserialize(ReadOnlySpan<byte> data)
        {
            if (data.Length < SIZE)
                throw new ArgumentException($"Data must be at least {SIZE} bytes long", nameof(data));

            BaseOffset = BinaryPrimitives.ReadUInt64BigEndian(data);
            _SizeTable.Clear();

            for (int i = 0; i < ENTRIES_MAX; i++)
                _SizeTable.Add(BinaryPrimitives.ReadUInt16BigEndian(data.Slice(8 + (i * 2), 2)));
        }
    }

    public abstract class PathEntry : ISerializable
    {
        public uint NameOffset;

        public const int SIZE = 16;

        public int Size() => SIZE;

        public static PathEntry DeserializeToType(ReadOnlySpan<byte> data)
        {
            if (data.Length < SIZE)
                throw new ArgumentException($"Data must be at least {SIZE} bytes long");
                
            var nameOffset = BinaryPrimitives.ReadUInt32BigEndian(data);

            if ((nameOffset & ENTRY_TYPE_FILE) != 0) 
                return ISerializable.Deserialize<FileEntry>(data);
            else 
                return ISerializable.Deserialize<DirectoryEntry>(data);
        }

        public abstract void Deserialize(ReadOnlySpan<byte> data);

        public abstract void Serialize(Span<byte> buffer);
    }

    public class DirectoryEntry : PathEntry
    {
        public uint NodeStartIndex;
        public uint NodeCount;

        public override void Serialize(Span<byte> buffer)
        {
            if (buffer.Length < SIZE)
                throw new ArgumentException($"Buffer length must be at least {SIZE} bytes.", nameof(buffer));

            BinaryPrimitives.WriteUInt32BigEndian(buffer, NameOffset & ~ENTRY_TYPE_FILE);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(4, 4), NodeStartIndex);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(8, 4), NodeCount);
        }

        public override void Deserialize(ReadOnlySpan<byte> data)
        {
            NameOffset = BinaryPrimitives.ReadUInt32BigEndian(data) & ~ENTRY_TYPE_FILE;
            NodeStartIndex = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4));
            NodeCount = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(8, 4));
        }
    }

    public class FileEntry : PathEntry
    {
        public ulong FileOffset;
        public ulong FileSize;

        public override void Serialize(Span<byte> buffer)
        {
            if (buffer.Length < SIZE)
                throw new ArgumentException($"Buffer length must be at least {SIZE} bytes.", nameof(buffer));

            BinaryPrimitives.WriteUInt32BigEndian(buffer, NameOffset | ENTRY_TYPE_FILE);
            
            var record0 = (uint)(FileOffset & 0xFFFFFFFF);
            var record1 = (uint)(FileSize & 0xFFFFFFFF);
            var record2 = (uint)(((FileOffset >> 32) & 0xFFFF) | ((FileSize >> 16) & 0xFFFF0000));

            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(4, 4), record0);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(8, 4), record1);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(12, 4), record2);
        }

        public override void Deserialize(ReadOnlySpan<byte> data)
        {
            NameOffset = BinaryPrimitives.ReadUInt32BigEndian(data) & ~ENTRY_TYPE_FILE;

            var record0 = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4));
            var record1 = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(8, 4));
            var record2 = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(12, 4));

            FileOffset = (ulong)record0 | (((ulong)record2 & 0xFFFF) << 32);
            FileSize = (ulong)record1 | (((ulong)record2 & 0xFFFF0000) << 16);
        }
    }

    public class SectionRecord : ISerializable
    {
        public ulong Offset;
        public ulong Length;

        public const int SIZE = sizeof(ulong) * 2;

        public int Size() => SIZE;

        public void Serialize(Span<byte> buffer)
        {
            if (buffer.Length < SIZE)
                throw new ArgumentException($"Buffer length must be at least {SIZE} bytes.", nameof(buffer));

            BinaryPrimitives.WriteUInt64BigEndian(buffer, Offset);
            BinaryPrimitives.WriteUInt64BigEndian(buffer.Slice(sizeof(ulong), sizeof(ulong)), Length);
        }

        public void Deserialize(ReadOnlySpan<byte> data)
        {
            if (data.Length < SIZE)
                throw new ArgumentException($"Data must be at least {SIZE} bytes long", nameof(data));

            Offset = BinaryPrimitives.ReadUInt64BigEndian(data);
            Length = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(sizeof(ulong), sizeof(ulong)));
        }
    }

    public class Footer : ISerializable
    {
        public SectionRecord CompressedData = new();
        public SectionRecord OffsetRecords = new();
        public SectionRecord Names = new();
        public SectionRecord FileTree = new();
        public SectionRecord MetaDirectory = new();
        public SectionRecord MetaData = new();
        public readonly byte[] IntegrityHash = new byte[32];
        public ulong TotalSize;
        public uint Version;
        public uint Magic;

        public const uint MAGIC = 0x169f52d6;
        public const uint VERSION = 0x61bf3a01;
        public const int SIZE = (SectionRecord.SIZE * 6) + 32 + sizeof(ulong) + sizeof(uint) + sizeof(uint);

        public int Size() => SIZE;

        public void Serialize(Span<byte> buffer)
        {
            CompressedData.Serialize(buffer);
            OffsetRecords.Serialize(buffer.Slice(SectionRecord.SIZE, SectionRecord.SIZE));
            Names.Serialize(buffer.Slice(SectionRecord.SIZE * 2, SectionRecord.SIZE));
            FileTree.Serialize(buffer.Slice(SectionRecord.SIZE * 3, SectionRecord.SIZE));
            MetaDirectory.Serialize(buffer.Slice(SectionRecord.SIZE * 4, SectionRecord.SIZE));
            MetaData.Serialize(buffer.Slice(SectionRecord.SIZE * 5, SectionRecord.SIZE));
            IntegrityHash.CopyTo(buffer.Slice(SectionRecord.SIZE * 6, IntegrityHash.Length));
            BinaryPrimitives.WriteUInt64BigEndian(buffer.Slice((SectionRecord.SIZE * 6) + IntegrityHash.Length, sizeof(ulong)), TotalSize);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice((SectionRecord.SIZE * 6) + IntegrityHash.Length + sizeof(ulong), sizeof(uint)), Version);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice((SectionRecord.SIZE * 6) + IntegrityHash.Length + sizeof(ulong) + sizeof(uint), sizeof(uint)), Magic);
        }

        public void Deserialize(ReadOnlySpan<byte> data)
        {
            if (data.Length < SIZE)
                throw new ArgumentException($"Data must be at least {SIZE} bytes long", nameof(data));

            CompressedData = ISerializable.Deserialize<SectionRecord>(data);
            OffsetRecords = ISerializable.Deserialize<SectionRecord>(data.Slice(SectionRecord.SIZE, SectionRecord.SIZE));
            Names = ISerializable.Deserialize<SectionRecord>(data.Slice(SectionRecord.SIZE * 2, SectionRecord.SIZE));
            FileTree = ISerializable.Deserialize<SectionRecord>(data.Slice(SectionRecord.SIZE * 3, SectionRecord.SIZE));
            MetaDirectory = ISerializable.Deserialize<SectionRecord>(data.Slice(SectionRecord.SIZE * 4, SectionRecord.SIZE));
            MetaData = ISerializable.Deserialize<SectionRecord>(data.Slice(SectionRecord.SIZE * 5, SectionRecord.SIZE));
            data.Slice(SectionRecord.SIZE * 6, IntegrityHash.Length).CopyTo(IntegrityHash);
            TotalSize = BinaryPrimitives.ReadUInt64BigEndian(data.Slice((SectionRecord.SIZE * 6) + IntegrityHash.Length, sizeof(ulong)));
            Version = BinaryPrimitives.ReadUInt32BigEndian(data.Slice((SectionRecord.SIZE * 6) + IntegrityHash.Length + sizeof(ulong), sizeof(uint)));
            Magic = BinaryPrimitives.ReadUInt32BigEndian(data.Slice((SectionRecord.SIZE * 6) + IntegrityHash.Length + sizeof(ulong) + sizeof(uint), sizeof(uint)));
        }
    }
}
