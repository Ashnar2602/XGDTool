using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Exe;
using XGDToolLib.Image.Format;
using XGDToolLib.Util;

namespace XGDToolLib.Image;

public abstract class Reader1
{
    public readonly struct SectorRange(uint start, uint endExclusive)
    {
        public readonly uint Start = start;
        public readonly uint EndExclusive = endExclusive;

        public bool Contains(uint sector) => sector >= Start && sector < EndExclusive;
    }

    public class DirectoryEntry : XISO.DirectoryEntry
    {
        public long RelativeOffset;
        public long LROffsetFromParent;
        public string Filepath = "";
    }

    public IReadOnlyList<string> Filepaths;
    public uint SectorOffset => XISO.AlignUp(ImageOffset);
    public Platform Platform { get; private set; } = Platform.Unknown;
    public HashSet<uint> DataSectors { get; private set; } = new();
    public List<SectorRange> DataSectorRanges { get; private set; } = new();
    public List<DirectoryEntry> DirectoryEntries { get; private set; } = new();
    public DirectoryEntry ExecutableEntry { get; private set; } = new();

    public abstract long ImageOffset { get; protected set; }
    public abstract uint TotalSectors { get; protected set; }
    public abstract Type ImageType { get; }

    protected Reader(IReadOnlyList<string> files)
    {
        Filepaths = files;
        if (Filepaths.Count == 0)
            throw new ArgumentException("At least one file must be provided.", nameof(files));
    }

    public Task Initialize(
        //bool parseSectors = false,
        IProgress<Converter.Progress>? progress = null,
        CancellationToken cancelToken = default)
    {
        var progData = new Converter.Progress() 
        { 
            Stage = Converter.Stage.ParsingDirectoryEntries, 
            Current = 0,
            Total = 1
        };
        progress?.Report(progData);

        InitializeType();
        LoadDirectoryEntries();

        progData.Current = progData.Total;
        progress?.Report(progData);

        if (DirectoryEntries.Count == 0)
            throw new InvalidDataException("No directory entries found in the image.");

        //if (!parseSectors)
        //    return Task.CompletedTask;

        //InitializeSectors(ref progData, progress, cancelToken);

        if (cancelToken.IsCancellationRequested)
            return Task.FromCanceled(cancelToken);

        return Task.CompletedTask;
    }

    //public bool IsDataSector(uint sector)
    //{
    //    if (DataSectorRanges.Count == 0)
    //        return DataSectors.Contains(sector);

    //    int lo = 0;
    //    int hi = DataSectorRanges.Count - 1;

    //    while (lo <= hi)
    //    {
    //        int mid = lo + ((hi - lo) / 2);
    //        var range = DataSectorRanges[mid];

    //        if (sector < range.Start)
    //            hi = mid - 1;
    //        else if (sector >= range.EndExclusive)
    //            lo = mid + 1;
    //        else
    //            return true;
    //    }

    //    return false;
    //}

    public IEnumerable<SectorRange> GetSectorRanges(uint startSector, uint endExclusive, bool dataOnly)
    {
        if (startSector >= endExclusive)
            yield break;

        if (!dataOnly)
        {
            yield return new SectorRange(startSector, endExclusive);
            yield break;
        }

        var ranges = DataSectorRanges.Count > 0
            ? DataSectorRanges
            : BuildSectorRanges(DataSectors);

        foreach (var raw in ranges)
        {
            uint rangeStart = Math.Max(raw.Start, startSector);
            uint rangeEnd = Math.Min(raw.EndExclusive, endExclusive);

            if (rangeStart < rangeEnd)
                yield return new SectorRange(rangeStart, rangeEnd);
        }
    }

    public abstract void ReadSector(uint sector, Span<byte> buffer);

    //public abstract Task ReadSectorAsync(uint sector, Memory<byte> buffer, CancellationToken cancelToken = default);

    public virtual int ReadBytes(long offset, Span<byte> buffer)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                "Offset cannot be negative.");
        }

        var size = buffer.Length;
        long end = offset + size;
        long maxBytes = (long)TotalSectors * XISO.SECTOR_SIZE;

        if (end > maxBytes)
        {
            throw new EndOfStreamException(
                "Attempted to read beyond end of image.");
        }

        // Fast path: fully sector-aligned read can go directly to destination buffer.
        if ((offset % XISO.SECTOR_SIZE) == 0 && (size % XISO.SECTOR_SIZE) == 0)
        {
            long directSectorCount = size / XISO.SECTOR_SIZE;
            uint firstSector = (uint)(offset / XISO.SECTOR_SIZE);

            for (long i = 0; i < directSectorCount; i++)
            {
                ReadSector(
                    firstSector + (uint)i, 
                    buffer.Slice((int)(i * XISO.SECTOR_SIZE), 
                    XISO.SECTOR_SIZE));
            }
            return size;
        }

        var startSectorOffset = offset % XISO.SECTOR_SIZE;
        var startOffset = offset - startSectorOffset;
        var endOffset = startOffset + (XISO.AlignUp(size + startSectorOffset) * XISO.SECTOR_SIZE);

        var numSectors = XISO.AlignUp(endOffset - startOffset);
        var aligned = new byte[numSectors * XISO.SECTOR_SIZE];

        for (var i = 0; i < numSectors; i++)
        {
            var sector = (uint)((startOffset / XISO.SECTOR_SIZE) + i);
            ReadSector(sector, aligned.AsSpan((int)(i * XISO.SECTOR_SIZE), XISO.SECTOR_SIZE));
        }

        aligned.AsSpan((int)startSectorOffset, buffer.Length).CopyTo(buffer);
        return buffer.Length;
    }

    //public virtual async Task<int> ReadBytesAsync(long offset, Memory<byte> buffer, CancellationToken cancelToken = default)
    //{
    //    if (offset < 0)
    //        throw new ArgumentOutOfRangeException(nameof(offset));

    //    if (buffer.Length == 0)
    //        return 0;

    //    long end = offset + buffer.Length;
    //    long maxBytes = (long)TotalSectors * XISO.SECTOR_SIZE;
    //    if (end > maxBytes)
    //        throw new EndOfStreamException("Attempted to read beyond end of image.");

    //    // Fast path: fully sector-aligned read can go directly to destination memory.
    //    if ((offset % XISO.SECTOR_SIZE) == 0 && (buffer.Length % XISO.SECTOR_SIZE) == 0)
    //    {
    //        int alignedSectorCount = buffer.Length / XISO.SECTOR_SIZE;
    //        uint firstSector = (uint)(offset / XISO.SECTOR_SIZE);

    //        for (int i = 0; i < alignedSectorCount; i++)
    //        {
    //            await ReadSectorAsync(
    //                firstSector + (uint)i,
    //                buffer.Slice(i * XISO.SECTOR_SIZE, XISO.SECTOR_SIZE),
    //                cancelToken);
    //        }

    //        return buffer.Length;
    //    }

    //    long startSectorOffset = offset % XISO.SECTOR_SIZE;
    //    long startOffset = offset - startSectorOffset;

    //    long sectorSpanBytes = startSectorOffset + buffer.Length;
    //    long numSectorsLong = XISO.NumSectors(sectorSpanBytes);
    //    int numSectors = checked((int)numSectorsLong);

    //    int alignedSize = checked(numSectors * XISO.SECTOR_SIZE);
    //    byte[] aligned = ArrayPool<byte>.Shared.Rent(alignedSize);

    //    try
    //    {
    //        for (int i = 0; i < numSectors; i++)
    //        {
    //            uint sector = (uint)((startOffset / XISO.SECTOR_SIZE) + i);
    //            await ReadSectorAsync(
    //                sector,
    //                aligned.AsMemory(i * XISO.SECTOR_SIZE, XISO.SECTOR_SIZE),
    //                cancelToken);
    //        }

    //        aligned.AsSpan((int)startSectorOffset, buffer.Length).CopyTo(buffer.Span);
    //    }
    //    finally
    //    {
    //        ArrayPool<byte>.Shared.Return(aligned);
    //    }

    //    return buffer.Length;
    //}

    public byte[] ReadBytes(long offset, long size)
    {
        var bytes = new byte[size];
        ReadBytes(offset, bytes);
        return bytes;
    }

    public uint ReadUInt32(long offset) => BitConverter.ToUInt32(ReadBytes(offset, 4), 0);
    public ushort ReadUInt16(long offset) => BitConverter.ToUInt16(ReadBytes(offset, 2), 0);
    public byte ReadByte(long offset) => ReadBytes(offset, 1)[0];

    protected virtual void InitializeType() { }

    private void LoadDirectoryEntries()
    {
        DirectoryEntries = new();

        var unprocessed = new List<DirectoryEntry>();
        var readBuf = new byte[XISO.SECTOR_SIZE];

        {
            var rootEntry = new DirectoryEntry();
            var rootStart = ImageOffset + XISO.MAGIC_OFFSET + XISO.MAGIC_SIZE;

            rootEntry.Header.StartSector = ReadUInt32(rootStart);
            rootEntry.Header.FileSize = ReadUInt32(rootStart + 4);
            rootEntry.LROffsetFromParent = 0;
            rootEntry.RelativeOffset = rootEntry.Header.StartSector * XISO.SECTOR_SIZE;

            unprocessed.Add(rootEntry);
        }

        while (unprocessed.Count > 0 && unprocessed.Count < 2000)
        {
            var cEntry = unprocessed.First();
            unprocessed.RemoveAt(0);

            if (cEntry.LROffsetFromParent * 4 >= cEntry.Header.FileSize)
                continue;

            var rEntry = new DirectoryEntry();
            {
                var currPos = ImageOffset + cEntry.RelativeOffset + (cEntry.LROffsetFromParent * 4);

                ReadBytes(currPos, readBuf.AsSpan(0, rEntry.Header.Size()));
                rEntry.Header.FromBytes(readBuf.AsSpan(0, rEntry.Header.Size()));

                ReadBytes(currPos + rEntry.Header.Size(), readBuf.AsSpan(0, rEntry.Header.NameLength));
                rEntry.SetNameFromBytes(readBuf.AsSpan(0, rEntry.Header.NameLength));
            }

            if (rEntry.Header.LeftOffset == XISO.PAD_BYTE)
                continue;

            if (rEntry.Header.LeftOffset != 0)
            {
                cEntry.LROffsetFromParent = rEntry.Header.LeftOffset;
                unprocessed.Add(cEntry);
            }

            if (rEntry.Header.Attributes.HasFlag(XISO.DirAttribute.Directory))
            {
                var dEntry = rEntry;

                dEntry.LROffsetFromParent = 0;
                dEntry.RelativeOffset = dEntry.Header.StartSector * XISO.SECTOR_SIZE;
                dEntry.Filepath = Path.Join(cEntry.Filepath, rEntry.GetName());

                DirectoryEntries.Add(dEntry);

                if (rEntry.Header.FileSize > 0)
                    unprocessed.Add(dEntry);
            }
            else if (rEntry.Header.FileSize > 0)
            {
                if (rEntry.GetName().Equals("default.xbe", StringComparison.OrdinalIgnoreCase))
                {
                    ExecutableEntry = rEntry;
                    Platform = Platform.OriginalXbox;
                }
                else if (rEntry.GetName().Equals("default.xex", StringComparison.OrdinalIgnoreCase))
                {
                    ExecutableEntry = rEntry;
                    Platform = Platform.Xbox360;
                }

                rEntry.Filepath = Path.Join(cEntry.Filepath, rEntry.GetName());
                DirectoryEntries.Add(rEntry);
            }

            if (rEntry.Header.RightOffset != 0)
            {
                cEntry.LROffsetFromParent = rEntry.Header.RightOffset;
                unprocessed.Add(cEntry);
            }
        }

        if (unprocessed.Count >= 2000)
            throw new InvalidDataException("Too many directory entries, likely malformed image.");

        if (Platform == Platform.Unknown)
            throw new InvalidDataException("Unable to detect image platform.");

        if (string.IsNullOrEmpty(ExecutableEntry.GetName()))
            throw new InvalidDataException("Executable file was not found.");

        DirectoryEntries.Sort((a, b) =>
        {
            var aDir = a.Header.Attributes.HasFlag(XISO.DirAttribute.Directory);
            var bDir = b.Header.Attributes.HasFlag(XISO.DirAttribute.Directory);

            if (aDir != bDir)
                return bDir.CompareTo(aDir);

            return string.Compare(a.Filepath, b.Filepath, StringComparison.OrdinalIgnoreCase);
        });
    }

    private Task InitializeSectors(
        ref Converter.Progress progData,
        IProgress<Converter.Progress>? progress = null,
        CancellationToken cancelToken = default)
    {
        DataSectors = LoadDataSectors(ref progData, progress, cancelToken).Result;

        if (cancelToken.IsCancellationRequested)
            return Task.FromCanceled(cancelToken);

        var maxDataSector = DataSectors.Max();
        var securitySectors = new HashSet<uint>();

        if (Platform == Platform.OriginalXbox)
        {
            securitySectors = LoadSecuritySectors(ref progData, progress, cancelToken).Result;

            if (cancelToken.IsCancellationRequested)
                return Task.FromCanceled(cancelToken);
        }

        if (securitySectors.Count == 0)
        {
            var startSector = SectorOffset;

            progData.Stage = Converter.Stage.LoadingSecuritySectors;
            progData.Current = 0;
            progData.Total = maxDataSector - startSector;
            progress?.Report(progData);

            DataSectors.UnionWith(
                Enumerable.Range((int)startSector, (int)(maxDataSector - startSector))
                    .Select(i => (uint)i));

            //for (uint i = startSector; i < maxDataSector; i++)
            //{
            //    if (i % 100 == 0)
            //    {
            //        progData.Current = i - startSector;
            //        progress?.Report(progData);
            //    }

            //    DataSectors.Add(i);
            //}

            progData.Current = progData.Total;
            progress?.Report(progData);
        }
        else
        {
            DataSectors.UnionWith(securitySectors);
        }

        DataSectorRanges = BuildSectorRanges(DataSectors);

        return Task.CompletedTask;
    }

    private Task<HashSet<uint>> LoadDataSectors(
        ref Converter.Progress progData,
        IProgress<Converter.Progress>? progress = null, 
        CancellationToken cancelToken = default)
    {
        var dataSectors = new HashSet<uint>();
        var unprocessed = new List<DirectoryEntry>();

        progData.Stage = Converter.Stage.LoadingDataSectors;
        progData.Current = 0;
        progData.Total = DirectoryEntries.Sum(e => e.Header.FileSize);

        uint headerSector = SectorOffset + (XISO.MAGIC_OFFSET / XISO.SECTOR_SIZE);

        dataSectors.Add(headerSector);
        dataSectors.Add(headerSector + 1);

        {
            var rootEntry = new DirectoryEntry();
            var rootStart = ImageOffset + XISO.MAGIC_OFFSET + XISO.MAGIC_SIZE;

            rootEntry.Header.StartSector = ReadUInt32(rootStart);
            rootEntry.Header.FileSize = ReadUInt32(rootStart + 4);
            rootEntry.LROffsetFromParent = 0;
            rootEntry.RelativeOffset = XISO.SectorToOffset(rootEntry.Header.StartSector);

            unprocessed.Add(rootEntry);
        }

        while (unprocessed.Count > 0)
        {
            if (cancelToken.IsCancellationRequested)
                return Task.FromCanceled<HashSet<uint>>(cancelToken);

            var rEntry = new DirectoryEntry();
            var cEntry = unprocessed.First();
            unprocessed.RemoveAt(0);

            {
                var currOffset = ImageOffset + cEntry.RelativeOffset + (cEntry.LROffsetFromParent * 4);
                var currSector = (uint)(currOffset >> 11);
                var totalSectors = (cEntry.Header.FileSize - (cEntry.LROffsetFromParent * 4) + 2047) >> 11;

                dataSectors.UnionWith(
                    Enumerable.Range((int)currSector, (int)totalSectors)
                        .Select(i => (uint)i));

                if (cEntry.LROffsetFromParent * 4 >= cEntry.Header.FileSize)
                    continue;

                rEntry.Header.FromBytes(ReadBytes(currOffset, rEntry.Header.Size()));
            }

            if (rEntry.Header.LeftOffset == XISO.PAD_BYTE)
                continue;

            if (rEntry.Header.LeftOffset != 0)
            {
                cEntry.LROffsetFromParent = rEntry.Header.LeftOffset;
                unprocessed.Add(cEntry);
            }

            if (rEntry.Header.Attributes.HasFlag(XISO.DirAttribute.Directory))
            {
                if (rEntry.Header.FileSize > 0)
                {
                    var dEntry = rEntry;
                    dEntry.RelativeOffset = 0;
                    dEntry.LROffsetFromParent = XISO.SectorToOffset(cEntry.Header.StartSector);
                    unprocessed.Add(dEntry);
                }
            }
            else
            {
                if (rEntry.Header.FileSize > 0)
                {
                    var startSector = SectorOffset + rEntry.Header.StartSector;
                    //var endSector = startSector + XISO.NumSectors(rEntry.Header.FileSize);

                    dataSectors.UnionWith(
                        Enumerable.Range((int)startSector, (int)XISO.AlignUp(rEntry.Header.FileSize))
                            .Select(i => (uint)i));

                    progData.Current += rEntry.Header.FileSize;
                    progress?.Report(progData);
                }
            }

            if (rEntry.Header.RightOffset != 0)
            {
                cEntry.LROffsetFromParent = rEntry.Header.RightOffset;
                unprocessed.Add(cEntry);
            }
        }

        progData.Current = progData.Total;
        progress?.Report(progData);

        return Task.FromResult(dataSectors);
    }

    protected Task<HashSet<uint>> LoadSecuritySectors(
        ref Converter.Progress progData,
        IProgress<Converter.Progress>? progress = null, 
        CancellationToken cancelToken = default)
    {
        var securitySectors = new HashSet<uint>();

        progData.Stage = Converter.Stage.LoadingSecuritySectors;
        progData.Current = 0;
        progData.Total = TotalSectors;

        if (DataSectors.Count == 0)
        {
            throw new InvalidOperationException(
                "Data sectors must be loaded before loading security sectors.");
        }
        else if ((TotalSectors != XISO.REDUMP_GAME_SECTORS) &&
                 (TotalSectors != XISO.REDUMP_TOTAL_SECTORS))
        {
            progData.Current = progData.Total;
            progress?.Report(progData);
            return Task.FromResult(securitySectors);
        }

        var compareMode = false;
        uint sectorOffset = (uint)(ImageOffset / XISO.SECTOR_SIZE);
        bool flag = false;
        uint start = 0;
        var sectorBuf = new byte[XISO.SECTOR_SIZE];

        progData.Total = XISO.REDUMP_END_SECTOR + 1;

        for (uint sectorIdx = 0; sectorIdx <= XISO.REDUMP_END_SECTOR; sectorIdx++)
        {
            if (cancelToken.IsCancellationRequested)
                return Task.FromCanceled<HashSet<uint>>(cancelToken);

            uint currSector = sectorOffset + sectorIdx;
            var isDataSector = DataSectors.Contains(currSector);

            if (isDataSector)
            {
                flag = false;
                continue;
            }

            ReadSector(currSector, sectorBuf);

            var isEmptySector = sectorBuf.All(b => b == 0);

            if (isEmptySector && !flag && !isDataSector)
            {
                start = currSector;
                flag = true;
            }
            else if (!isEmptySector && flag)
            {
                uint end = currSector - 1;
                flag = false;

                if (end - start == 0xFFFF)
                {
                    for (uint i = start; i <= end; i++)
                    {
                        if (!DataSectors.Contains(i))
                            securitySectors.Add(i);
                    }
                }
                else if (compareMode && ((end - start) > 0xFFFF))
                {
                    progData.Current = progData.Total;
                    progress?.Report(progData);

                    securitySectors.Clear();
                    return Task.FromResult(securitySectors);
                }
            }

            progData.Current = sectorIdx;
            progress?.Report(progData);
        }

        progData.Current = progData.Total;
        progress?.Report(progData);

        return Task.FromResult(securitySectors);
    }

    private static List<SectorRange> BuildSectorRanges(HashSet<uint> sectors)
    {
        var ranges = new List<SectorRange>();
        if (sectors.Count == 0)
            return ranges;

        var ordered = sectors.OrderBy(s => s).ToList();
        uint start = ordered[0];
        uint prev = start;

        for (int i = 1; i < ordered.Count; i++)
        {
            uint current = ordered[i];
            if (current == prev + 1)
            {
                prev = current;
                continue;
            }

            ranges.Add(new SectorRange(start, prev + 1));
            start = current;
            prev = current;
        }

        ranges.Add(new SectorRange(start, prev + 1));
        return ranges;
    }
}
