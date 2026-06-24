using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.Util;
using XGDTool.Lib.Exe;

namespace XGDTool.Lib.Image.Readers;

internal abstract class Base(IReadOnlyList<string> files) : IReader
{
    public abstract Format ImageFormat { get; }
    public abstract uint TotalSectors { get; }

    public List<string> FilePaths { get; } = [..files.ToList().OrderBy(f => f)];
    public long ImageOffset { get; private set; }
    public uint SectorOffset => XDVDFS.SectorCount(ImageOffset);
    public Platform Platform { get; private set; } = Platform.Unknown;
    public List<DirectoryEntryExt> DirectoryEntries { get; } = [];
    public DirectoryEntryExt ExecutableEntry { get; private set; } = new();
    public long TotalSizeOfFiles => DirectoryEntries.Sum(e =>
    {
        return e.Attributes.HasFlag(XDVDFS.DirAttributes.Directory) ? 0u : e.FileSize;
    });
    public ulong FileTime { get; private set; }

    public async Task Initialize(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        if (DirectoryEntries.Count > 0)
            return;

        await InitializeType(progress, ct);

        {
            var imgOff = DetectImageOffset();
            if (!imgOff.HasValue)
                throw new InvalidDataException("No valid image offset found, not a valid XISO image.");

            ImageOffset = imgOff.Value;
        }

        var progData = new Converter.Progress()
        {
            Stage = Converter.Stage.ParsingDirectoryEntries,
            Current = 0,
            Total = 1
        };
        progress?.Report(progData);

        var readBuf = new byte[XDVDFS.SECTOR_SIZE];
        var unprocessed = new Queue<DirectoryEntryExt>();
        var processedCount = 0;

        unprocessed.Enqueue(GetRootEntry());

        while (unprocessed.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            if (++processedCount > 4000)
                throw new InvalidDataException("Too many directory entries found in image, likely malformed.");

            var cEntry = unprocessed.Dequeue();

            if (cEntry.LROffsetFromParent * 4 >= cEntry.FileSize)
                continue;

            var entryPos = ImageOffset + cEntry.RelativeOffset + (cEntry.LROffsetFromParent * 4);
            var rEntry = ReadEntry(entryPos, readBuf);

            if (rEntry.LeftOffset == 0xFFFF && rEntry.RightOffset == 0xFFFF)
                continue; // padding entry at end of sector, not a real node

            if (rEntry.LeftOffset != 0)
            {
                cEntry.LROffsetFromParent = rEntry.LeftOffset;
                unprocessed.Enqueue(cEntry.Clone());
            }

            if (rEntry.Attributes.HasFlag(XDVDFS.DirAttributes.Directory))
            {
                var dEntry = rEntry.Clone();
                dEntry.LROffsetFromParent = 0;
                dEntry.RelativeOffset = XDVDFS.SectorToOffset((uint)rEntry.StartSector);
                dEntry.FilePath = Path.Join(cEntry.FilePath, rEntry.FileName);

                DirectoryEntries.Add(dEntry);

                if (dEntry.FileSize > 0)
                    unprocessed.Enqueue(dEntry.Clone());
            }
            else if (rEntry.FileSize > 0)
            {
                rEntry.FilePath = Path.Join(cEntry.FilePath, rEntry.FileName);
                DirectoryEntries.Add(rEntry.Clone());

                if (rEntry.FilePath.Equals("default.xex", StringComparison.OrdinalIgnoreCase))
                {
                    ExecutableEntry = rEntry;
                    Platform = Platform.Xbox360;
                }
                else if (rEntry.FilePath.Equals("default.xbe", StringComparison.OrdinalIgnoreCase))
                {
                    ExecutableEntry = rEntry;
                    Platform = Platform.Xbox;
                }
            }

            if (rEntry.RightOffset != 0)
            {
                cEntry.LROffsetFromParent = rEntry.RightOffset;
                unprocessed.Enqueue(cEntry.Clone());
            }
        }

        if (Platform == Platform.Unknown)
            throw new InvalidDataException("No executable entry found in image.");

        progData.Current = progData.Total;
        progress?.Report(progData);
    }

    public abstract void ReadSectors(uint startSector, Span<byte> buffer, CancellationToken ct = default);

    public virtual Task ReadSectorsAsync(uint startSector, Memory<byte> buffer, CancellationToken ct = default)
    {
        ReadSectors(startSector, buffer.Span, ct);
        return Task.CompletedTask;
    }

    public int ReadBytes(long offset, Span<byte> buffer)
    {
        int size = buffer.Length;

        if (XDVDFS.IsSectorAligned(size) && XDVDFS.IsSectorAligned(offset))
        {
            ReadSectors(XDVDFS.SectorIndex(offset), buffer);
            return size;
        }

        var offsetInSector = (int)(offset % XDVDFS.SECTOR_SIZE);
        var startSector = XDVDFS.SectorIndex(offset - offsetInSector);
        var numSectors = XDVDFS.SectorCount(offsetInSector + size);
        var tmpBuf = new byte[numSectors * XDVDFS.SECTOR_SIZE];

        ReadSectors(startSector, tmpBuf);
        tmpBuf.AsSpan(offsetInSector, buffer.Length).CopyTo(buffer);

        return size;
    }
    
    public byte[] ReadBytes(long offset, int count)
    {
        var buffer = new byte[count];
        ReadBytes(offset, buffer);
        return buffer;
    }

    public uint ReadUInt32(long offset) => BitConverter.ToUInt32(ReadBytes(offset, 4), 0);

    public ushort ReadUInt16(long offset) => BitConverter.ToUInt16(ReadBytes(offset, 2), 0);

    public byte ReadByte(long offset) => ReadBytes(offset, 1)[0];

    public XDVDFS.VolumeDescriptor ReadVolumeDescriptor(long? imageOffset = null) => 
        ISerializable.Deserialize<XDVDFS.VolumeDescriptor>(
            ReadBytes((imageOffset ?? ImageOffset) + XDVDFS.MAGIC_OFFSET, XDVDFS.VolumeDescriptor.SIZE));

    public DirectoryEntryExt GetRootEntry()
    {
        var fsDesc = ReadVolumeDescriptor();
        FileTime = fsDesc.FileTime;

        return new DirectoryEntryExt
        {
            StartSector = fsDesc.RootDirectoryTableSector,
            FileSize = fsDesc.RootDirectoryTableSize,
            RelativeOffset = XDVDFS.SectorToOffset(fsDesc.RootDirectoryTableSector)
        };
    }

    public DirectoryEntryExt ReadEntry(long offset, byte[]? buf = null)
    {
        if (buf == null)
            buf = new byte[256];
        else if (buf.Length < 0xff)
            throw new ArgumentException($"Buffer must be at least {byte.MaxValue} bytes.", nameof(buf));

        var rEntry = new XDVDFS.DirectoryEntry();

        ReadBytes(offset, buf.AsSpan(0, XDVDFS.DirectoryEntry.HEADER_SIZE));
        rEntry.DeserializeHeader(buf.AsSpan(0, XDVDFS.DirectoryEntry.HEADER_SIZE));

        ReadBytes(offset + XDVDFS.DirectoryEntry.HEADER_SIZE, buf.AsSpan(0, rEntry.NameLength));
        rEntry.DeserializeName(buf.AsSpan(0, rEntry.NameLength));

        return DirectoryEntryExt.FromDirectoryEntry(rEntry);
    }

    protected virtual Task InitializeType(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    protected static void ReadExactlyAt(FileStream stream, Span<byte> buffer, long offset)
    {
        int totalRead = 0;

        while (totalRead < buffer.Length)
        {
            int read = RandomAccess.Read(
                stream.SafeFileHandle,
                buffer.Slice(totalRead),
                offset + totalRead);

            if (read == 0)
                throw new EndOfStreamException("Unexpected end of stream while reading.");

            totalRead += read;
        }
    }

    protected static async Task ReadExactlyAtAsync(FileStream stream, Memory<byte> buffer, long offset, CancellationToken ct = default)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await RandomAccess.ReadAsync(
                stream.SafeFileHandle,
                buffer.Slice(totalRead),
                offset + totalRead,
                ct);

            if (read == 0)
                throw new EndOfStreamException("Unexpected end of stream while reading.");

            totalRead += read;
        }
    }

    private long? DetectImageOffset()
    {
        foreach (var offset in XDVDFS.ImageOffsets)
        {
            var volDesc = ReadVolumeDescriptor(offset);
            if (XDVDFS.MAGIC.SequenceEqual(volDesc.Magic1))
                return offset;
        }

        return null;
    }
}