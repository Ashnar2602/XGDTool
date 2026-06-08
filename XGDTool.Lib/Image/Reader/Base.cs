using XGDTool.Lib.Image.Format;
using XGDTool.Lib.Util;
using XGDTool.Lib.Exe;

namespace XGDTool.Lib.Image.Reader;

internal abstract class Base(IReadOnlyList<string> files) : IReader
{
    private List<SectorRange> SectorRanges = new();

    public abstract Type ImageType { get; }
    public abstract uint TotalSectors { get; protected set; }

    public List<string> FilePaths { get; } = files.ToList().OrderBy(f => f).ToList();
    public long ImageOffset { get; private set; }
    public uint SectorOffset => XISO.SectorCount(ImageOffset);
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
                    unprocessed.Enqueue(dEntry.Clone());
            }
            else if (rEntry.Header.FileSize > 0)
            {
                rEntry.Filepath = Path.Join(cEntry.Filepath, rEntry.GetName());
                DirectoryEntries.Add(rEntry.Clone());

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

    public Task<List<SectorRange>> GetSectorRanges(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        if (SectorRanges.Count > 0)
            return Task.FromResult(SectorRanges);

        var ds = GetDataSectorRanges(progress, ct);
        var maxDs = ds.Max(r => r.End);
        List<SectorRange>? ss = null;

        if (Platform == Platform.OriginalXbox)
            ss = GetSecuritySectorRanges(ds, progress, ct);

        if (ss == null || ss.Count == 0)
        {
            SectorRanges = new List<SectorRange>() 
            { 
                new SectorRange(SectorOffset, maxDs) 
            };
            return Task.FromResult(SectorRanges);
        }
        
        for (int i = 0; i < ss.Count; i++)
        {
            if (ss[i].Start > maxDs)
            {
                ss.RemoveAt(i);
                i--;
            }
            else if (ss[i].End > maxDs)
            {
                ss[i] = new SectorRange(ss[i].Start, maxDs);
            }
        }

        var sRanges = ds.Union(ss).OrderBy(s => s.Start).ToList();

        for (int i = 1; i < sRanges.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var prev = sRanges[i - 1];
            var curr = sRanges[i];
            if (prev.End + 1 >= curr.Start)
            {
                sRanges[i - 1] = new SectorRange(prev.Start, Math.Max(prev.End, curr.End));
                sRanges.RemoveAt(i);
                i--;
            }
        }

        SectorRanges = sRanges;
        return Task.FromResult(SectorRanges);
    }

    public abstract void ReadSectors(uint startSector, Span<byte> buffer);

    public abstract Task ReadSectorsAsync(uint startSector, Memory<byte> buffer, CancellationToken ct = default);

    public virtual int ReadBytes(long offset, Span<byte> buffer)
    {
        int size = buffer.Length;

        if (XISO.IsSectorAligned(size) && XISO.IsSectorAligned(offset))
        {
            ReadSectors(XISO.SectorIndex(offset), buffer);
            return size;
        }

        var offsetInSector = (int)(offset % XISO.SECTOR_SIZE);
        var startSector = XISO.SectorIndex(offset - offsetInSector);
        var numSectors = XISO.SectorCount(offsetInSector + size);
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

    private List<SectorRange> GetDataSectorRanges(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        if (DirectoryEntries.Count == 0)
            throw new InvalidOperationException("Directory entries must be initialized before getting data sectors.");

        var dsRanges = new List<SectorRange>();
        var processedCount = 0;
        var unprocessed = new Queue<DirectoryEntry>();
        var readBuf = new byte[XISO.SECTOR_SIZE];

        var progData = new Converter.Progress()
        {
            Stage = Converter.Stage.LoadingDataSectors,
            Current = 0,
            Total = DirectoryEntries.Sum(e => e.Header.FileSize)
        };

        progress?.Report(progData);

        var headerSector = SectorOffset + XISO.SectorIndex(XISO.MAGIC_OFFSET);
        dsRanges.Add(new SectorRange(headerSector, headerSector + 1));

        unprocessed.Enqueue(GetRootEntry());

        while (unprocessed.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            if (++processedCount > 4000)
                throw new InvalidDataException("Too many directory entries found in image, likely malformed.");

            var cEntry = unprocessed.Dequeue();
            var cPos = ImageOffset + cEntry.RelativeOffset + (cEntry.LROffsetFromParent * 4);
            {
                var cStart = XISO.SectorIndex(cPos);
                var cEnd = cStart + XISO.SectorCount(cEntry.Header.FileSize - (cEntry.LROffsetFromParent * 4)) - 1;

                dsRanges.Add(new SectorRange(cStart, cEnd));
            }

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
                    var start = checked(SectorOffset + rEntry.Header.StartSector);
                    var end = start + XISO.SectorCount(rEntry.Header.FileSize) - 1;
                    dsRanges.Add(new SectorRange(start, end));

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

        if (dsRanges.Count == 0)
            throw new InvalidDataException("No data sectors found in image, likely malformed.");

        progData.Current = progData.Total;
        progress?.Report(progData);

        return dsRanges.OrderBy(r => r.Start).ToList();
    }

    private List<SectorRange> GetSecuritySectorRanges(List<SectorRange> dsRanges, IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        var ssRanges = new List<SectorRange>();

        if (TotalSectors != XISO.REDUMP_GAME_SECTORS &&
            TotalSectors != XISO.REDUMP_TOTAL_SECTORS)
        {
            return ssRanges;
        }

        const uint BufferSectors = 512;
        var buf = new byte[BufferSectors * XISO.SECTOR_SIZE];

        var progData = new Converter.Progress
        {
            Stage = Converter.Stage.LoadingSecuritySectors,
            Current = 0,
            Total = XISO.REDUMP_END_SECTOR + 1
        };

        progress?.Report(progData);

        bool inEmptyRun = false;
        uint runStart = 0;

        int dataIndex = 0;

        for (uint sectorIndex = 0; sectorIndex <= XISO.REDUMP_END_SECTOR;)
        {
            ct.ThrowIfCancellationRequested();

            uint sectorsRemaining = XISO.REDUMP_END_SECTOR - sectorIndex + 1;
            uint readCount = Math.Min(BufferSectors, sectorsRemaining);

            uint firstSector = SectorOffset + sectorIndex;
            int byteCount = checked((int)(readCount * XISO.SECTOR_SIZE));

            ReadSectors(firstSector, buf.AsSpan(0, byteCount));

            for (uint i = 0; i < readCount; i++)
            {
                uint currentSector = firstSector + i;

                while (dataIndex < dsRanges.Count &&
                       dsRanges[dataIndex].End < currentSector)
                {
                    dataIndex++;
                }

                bool isDataSector =
                    dataIndex < dsRanges.Count &&
                    currentSector >= dsRanges[dataIndex].Start &&
                    currentSector <= dsRanges[dataIndex].End;

                var sectorSpan = buf.AsSpan(
                    checked((int)(i * XISO.SECTOR_SIZE)),
                    XISO.SECTOR_SIZE);

                bool isEmptySector = IsEmpySpan(sectorSpan);

                if (isEmptySector && !inEmptyRun && !isDataSector)
                {
                    runStart = currentSector;
                    inEmptyRun = true;
                }
                else if (!isEmptySector && inEmptyRun)
                {
                    uint runEnd = currentSector - 1;
                    inEmptyRun = false;

                    if (runEnd - runStart == 0xFFF)
                    {
                        ssRanges.Add(new SectorRange(runStart, runEnd));
                    }
                }
            }

            sectorIndex += readCount;

            progData.Current = sectorIndex;
            progress?.Report(progData);
        }

        progData.Current = progData.Total;
        progress?.Report(progData);

        return ssRanges;
    }

    private static bool IsEmpySpan(ReadOnlySpan<byte> span)
    {
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] != 0)
                return false;
        }
        return true;
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