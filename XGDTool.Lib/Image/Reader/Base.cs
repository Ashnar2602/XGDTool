using XGDTool.Lib.Image.Format;
using XGDTool.Lib.Util;
using XGDTool.Lib.Exe;

namespace XGDTool.Lib.Image.Reader;

internal abstract class Base(IReadOnlyList<string> files) : IReader
{
    private List<SectorRange> DataSectorRanges = new();

    public abstract Type ImageType { get; }
    public abstract uint TotalSectors { get; protected set; }

    public List<string> FilePaths { get; } = files.ToList().OrderBy(f => f).ToList();
    public long ImageOffset { get; private set; }
    public uint SectorOffset => XISO.NumSectors(ImageOffset);
    public Platform Platform { get; private set; } = Platform.Unknown;
    public List<DirectoryEntry> DirectoryEntries { get; } = new();
    public DirectoryEntry ExecutableEntry { get; private set; } = new();

    public async Task Initialize(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        if (DirectoryEntries.Count > 0)
            return;

        await InitializeType(progress, ct);

        var imgOff = DetectImageOffset();

        if (imgOff == null)
            throw new InvalidDataException("No valid image offset found, not a valid XISO image.");

        ImageOffset = imgOff.Value;

        var progData = new Converter.Progress()
        {
            Stage = Converter.Stage.ParsingDirectoryEntries,
            Current = 0,
            Total = 1
        };
        progress?.Report(progData);

        var readBuf = new byte[XISO.SECTOR_SIZE];
        var unprocessed = new Queue<DirectoryEntry>();
        var processedCount = 0;
        unprocessed.Enqueue(GetRootEntry());

        while (unprocessed.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            if (++processedCount > 4000)
                throw new InvalidDataException("Too many directory entries found in image, likely malformed.");

            var cEntry = unprocessed.Dequeue();

            if (cEntry.LROffsetFromParent * 4 >= cEntry.Header.FileSize)
                continue;

            var entryPos = ImageOffset + cEntry.RelativeOffset + (cEntry.LROffsetFromParent * 4);
            var rEntry = ReadEntry(entryPos, readBuf);

            if (rEntry.Header.LeftOffset == XISO.PAD_BYTE)
                continue;

            if (rEntry.Header.LeftOffset != 0)
            {
                cEntry.LROffsetFromParent = rEntry.Header.LeftOffset;
                unprocessed.Enqueue(cEntry.Clone());
            }

            if (rEntry.Header.Attributes.HasFlag(XISO.DirAttribute.Directory))
            {
                var dEntry = rEntry.Clone();
                dEntry.LROffsetFromParent = 0;
                dEntry.RelativeOffset = XISO.SectorToOffset(rEntry.Header.StartSector);
                dEntry.Filepath = Path.Join(cEntry.Filepath, rEntry.GetName());

                DirectoryEntries.Add(dEntry);

                if (dEntry.Header.FileSize > 0)
                    unprocessed.Enqueue(dEntry);
            }
            else if (rEntry.Header.FileSize > 0)
            {
                rEntry.Filepath = Path.Join(cEntry.Filepath, rEntry.GetName());
                DirectoryEntries.Add(rEntry);

                if (rEntry.GetName().Equals("default.xex", StringComparison.OrdinalIgnoreCase))
                {
                    ExecutableEntry = rEntry;
                    Platform = Platform.Xbox360;
                }
                else if (rEntry.GetName().Equals("default.xbe", StringComparison.OrdinalIgnoreCase))
                {
                    ExecutableEntry = rEntry;
                    Platform = Platform.OriginalXbox;
                }
            }

            if (rEntry.Header.RightOffset != 0)
            {
                cEntry.LROffsetFromParent = rEntry.Header.RightOffset;
                unprocessed.Enqueue(cEntry.Clone());
            }
        }

        if (Platform == Platform.Unknown)
            throw new InvalidDataException("No executable entry found in image.");

        progData.Current = progData.Total;
        progress?.Report(progData);
    }

    private HashSet<uint> GetDataSectors(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        if (DirectoryEntries.Count == 0)
            throw new InvalidOperationException("Directory entries must be initialized before getting data sectors.");

        var processedCount = 0;
        var unprocessed = new Queue<DirectoryEntry>();
        var dataSectors = new HashSet<uint>();
        var readBuf = new byte[XISO.SECTOR_SIZE];
        var headerSector = SectorOffset + XISO.NumSectors(XISO.MAGIC_OFFSET);
        var progData = new Converter.Progress()
        {
            Stage = Converter.Stage.LoadingDataSectors,
            Current = 0,
            Total = DirectoryEntries.Sum(e => e.Header.FileSize)
        };

        progress?.Report(progData);

        dataSectors.Add(headerSector);
        dataSectors.Add(headerSector + 1);

        unprocessed.Enqueue(GetRootEntry());

        while (unprocessed.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            if (++processedCount > 4000)
                throw new InvalidDataException("Too many directory entries found in image, likely malformed.");

            var cEntry = unprocessed.Dequeue();
            var cPos = ImageOffset + cEntry.RelativeOffset + (cEntry.LROffsetFromParent * 4);
            var cEnd = ((cEntry.Header.FileSize - (cEntry.LROffsetFromParent * 4) + 2047) >> 11);

            dataSectors.UnionWith(Enumerable.Range((int)XISO.NumSectors(cPos), (int)cEnd).Select(s => (uint)s));

            progData.Current += cEntry.Header.FileSize;
            progress?.Report(progData);

            if (cEntry.LROffsetFromParent * 4 >= cEntry.Header.FileSize)
                continue;

            var rEntry = ReadEntry(cPos, readBuf);

            if (rEntry.Header.LeftOffset == XISO.PAD_WORD)
                continue;

            if (rEntry.Header.LeftOffset != 0)
            {
                cEntry.LROffsetFromParent = rEntry.Header.LeftOffset;
                unprocessed.Enqueue(cEntry.Clone());
            }

            if (rEntry.Header.Attributes.HasFlag(XISO.DirAttribute.Directory))
            {
                if (rEntry.Header.FileSize > 0)
                {
                    var dEntry = rEntry.Clone();
                    dEntry.LROffsetFromParent = 0;
                    dEntry.RelativeOffset = XISO.SectorToOffset(rEntry.Header.StartSector);
                    unprocessed.Enqueue(dEntry);
                }
            }
            else
            {
                if (rEntry.Header.FileSize > 0)
                {
                    var start = SectorOffset + rEntry.Header.StartSector;
                    var count = XISO.NumSectors(rEntry.Header.FileSize);
                    dataSectors.UnionWith(Enumerable.Range((int)start, (int)count).Select(s => (uint)s));

                    progData.Current += rEntry.Header.FileSize;
                    progress?.Report(progData);
                }
            }

            if (rEntry.Header.RightOffset != 0)
            {
                cEntry.LROffsetFromParent = rEntry.Header.RightOffset;
                unprocessed.Enqueue(cEntry.Clone());
            }
        }

        HashSet<uint>? ss = null;

        if (Platform == Platform.OriginalXbox)
        {
            ss = GetSecuritySectors(dataSectors, progress, ct);
        }

        if (ss != null && ss.Count > 0)
        {
            dataSectors.UnionWith(ss);
        }
        else
        {
            var maxDataSector = dataSectors.Max();
            dataSectors.UnionWith(
                Enumerable.Range((int)SectorOffset, (int)(maxDataSector - SectorOffset)).Select(s => (uint)s));
        }

        return dataSectors;
    }

    public Task<List<SectorRange>> GetDataSectorRanges(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        if (DataSectorRanges.Count > 0)
            return Task.FromResult(DataSectorRanges);

        var ds = GetDataSectors(progress, ct).OrderBy(s => s).ToList();

        var progData = new Converter.Progress()
        {
            Stage = Converter.Stage.LoadingDataSectors,
            Current = 0,
            Total = ds.Max()
        };

        var ranges = new List<SectorRange>();
        uint start = ds[0];
        uint prev = start;

        for (int i = 1; i < ds.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            uint curr = ds[i];
            if (curr == prev + 1)
            {
                prev = curr;
                continue;
            }

            ranges.Add(new SectorRange(start, prev + 1));
            start = curr;
            prev = curr;

            progData.Current = curr;
            progress?.Report(progData);
        }

        ranges.Add(new SectorRange(start, prev + 1));
        DataSectorRanges = ranges;

        progData.Current = progData.Total;
        progress?.Report(progData);

        return Task.FromResult(DataSectorRanges);
    }

    public abstract void ReadSectors(uint startSector, Span<byte> buffer);

    public abstract Task ReadSectorsAsync(uint startSector, Memory<byte> buffer, CancellationToken ct = default);

    public virtual int ReadBytes(long offset, Span<byte> buffer)
    {
        int size = buffer.Length;

        if (XISO.IsSectorAligned(size) && XISO.IsSectorAligned(offset))
        {
            ReadSectors(XISO.NumSectors(offset), buffer);
            return size;
        }

        var offsetInSector = (int)(offset % XISO.SECTOR_SIZE);
        var startSector = XISO.NumSectors(offset - offsetInSector);
        var numSectors = XISO.NumSectors(offsetInSector + size);
        var tmpBuf = new byte[numSectors * XISO.SECTOR_SIZE];

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

    protected virtual Task InitializeType(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    private DirectoryEntry GetRootEntry()
    {
        var rootEntry = new DirectoryEntry();
        var rootOffset = ImageOffset + XISO.MAGIC_OFFSET + XISO.MAGIC_SIZE;
        rootEntry.Header.StartSector = ReadUInt32(rootOffset);
        rootEntry.Header.FileSize = ReadUInt32(rootOffset + 4);
        rootEntry.RelativeOffset = XISO.SectorToOffset(rootEntry.Header.StartSector);
        return rootEntry;
    }

    private DirectoryEntry ReadEntry(long offset, byte[]? buf = null)
    {
        if (buf == null)
            buf = new byte[XISO.SECTOR_SIZE];

        var entry = new DirectoryEntry();

        ReadBytes(offset, buf.AsSpan(0, entry.Header.Size()));
        entry.Header.FromBytes(buf.AsSpan(0, entry.Header.Size()));

        ReadBytes(offset + entry.Header.Size(), buf.AsSpan(0, entry.Header.NameLength));
        entry.SetNameFromBytes(buf.AsSpan(0, entry.Header.NameLength));

        return entry;
    }

    private HashSet<uint> GetSecuritySectors(IReadOnlySet<uint> dSectors, IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        var sSectors = new HashSet<uint>();

        if (TotalSectors != XISO.REDUMP_GAME_SECTORS || TotalSectors != XISO.REDUMP_TOTAL_SECTORS)
            return sSectors;

        bool compareMode = false;
        bool flag = false;
        uint start = 0;
        var buf = new byte[XISO.SECTOR_SIZE];
        var progData = new Converter.Progress()
        {
            Stage = Converter.Stage.LoadingDataSectors,
            Current = 0,
            Total = XISO.REDUMP_END_SECTOR
        };
        progress?.Report(progData);

        for (uint s = 0; s < XISO.REDUMP_END_SECTOR; s++)
        {
            ct.ThrowIfCancellationRequested();

            if (dSectors.Contains(s))
            {
                flag = false;
                continue;
            }

            uint cSector = SectorOffset + s;
            ReadSectors(cSector, buf);

            var empty = buf.Sum(b => b) == 0;

            if (empty && !flag)
            {
                start = cSector;
                flag = true;
            }
            else if (!empty && flag)
            {
                uint end = cSector - 1;
                flag = false;

                if (end - start == 0xFFFF)
                {
                    sSectors.UnionWith(
                        Enumerable.Range((int)start, (int)(end - start + 1))
                        .Select(i => (uint)i));
                }
                else if (compareMode && (end - start) > 0xFFF)
                {
                    sSectors.Clear();
                    return sSectors;
                }
            }

            progData.Current = s;
            progress?.Report(progData);
        }

        return sSectors;
    }

    private long? DetectImageOffset()
    {
        var buf = new byte[XISO.MAGIC_SIZE];

        foreach (var offset in XISO.ImageOffsets)
        {
            ReadBytes(offset + XISO.MAGIC_OFFSET, buf);

            if (XISO.MAGIC.SequenceEqual(buf))
                return offset;
        }

        return null;
    }
}