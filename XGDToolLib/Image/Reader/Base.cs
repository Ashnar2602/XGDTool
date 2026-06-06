using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Image.Format;
using XGDToolLib.Util;

namespace XGDToolLib.Image.Reader;

internal abstract class Base(IReadOnlyList<string> files) : IReader
{
    private HashSet<uint> DataSectors = new();
    private List<SectorRange> DataSectorRanges = new();

    protected List<string> InFiles { get; } = files.ToList().OrderBy(f => f).ToList();

    public abstract Type ImageType { get; }
    public abstract uint TotalSectors { get; }

    public long ImageOffset { get; private set; }
    public uint SectorOffset => XISO.AlignUp(ImageOffset);
    public Exe.Platform Platform { get; private set; } = Exe.Platform.Unknown;
    public List<DirectoryEntry> DirectoryEntries { get; } = new();
    public DirectoryEntry ExecutableEntry { get; private set; } = new();

    public Task Initialize(IProgress<Converter.Progress>? progress = null, CancellationToken cancelToken = default)
    {
        if (DirectoryEntries.Count > 0)
            return Task.CompletedTask;

        InitializeType(progress, cancelToken).WaitAsync(cancelToken);

        if (cancelToken.IsCancellationRequested)
            return Task.FromCanceled(cancelToken);

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
        unprocessed.Enqueue(GetRootEntry());

        while (unprocessed.Count > 0 && unprocessed.Count < 2000)
        {
            if (cancelToken.IsCancellationRequested)
                return Task.FromCanceled<(List<DirectoryEntry>, DirectoryEntry)>(cancelToken);

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
                unprocessed.Enqueue(cEntry);
            }

            if (rEntry.Header.Attributes.HasFlag(XISO.DirAttribute.Directory))
            {
                var dEntry = rEntry;
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
                    Platform = Exe.Platform.Xbox360;
                }
                else if (rEntry.GetName().Equals("default.xbe", StringComparison.OrdinalIgnoreCase))
                {
                    ExecutableEntry = rEntry;
                    Platform = Exe.Platform.OriginalXbox;
                }
            }

            if (rEntry.Header.RightOffset != 0)
            {
                cEntry.LROffsetFromParent = rEntry.Header.RightOffset;
                unprocessed.Enqueue(cEntry);
            }
        }

        if (unprocessed.Count >= 2000)
            throw new InvalidDataException("Too many directory entries found in image, likely malformed.");

        if (Platform == Exe.Platform.Unknown)
            throw new InvalidDataException("No executable entry found in image.");

        progData.Current = progData.Total;
        progress?.Report(progData);

        return Task.CompletedTask;
    }

    public Task<HashSet<uint>> GetDataSectors(IProgress<Converter.Progress>? progress = null, CancellationToken cancelToken = default)
    {
        if (DirectoryEntries.Count == 0)
            throw new InvalidOperationException("Directory entries must be initialized before getting data sectors.");

        if (DataSectors.Count > 0)
            return Task.FromResult(DataSectors);

        var unprocessed = new Queue<DirectoryEntry>();
        var dataSectors = new HashSet<uint>();
        var readBuf = new byte[XISO.SECTOR_SIZE];
        var headerSector = SectorOffset + XISO.AlignUp(XISO.MAGIC_OFFSET);
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

        while (unprocessed.Count > 0 && unprocessed.Count < 4000)
        {
            if (cancelToken.IsCancellationRequested)
                return Task.FromCanceled<HashSet<uint>>(cancelToken);

            var cEntry = unprocessed.Dequeue();
            var cPos = ImageOffset + cEntry.RelativeOffset + (cEntry.LROffsetFromParent * 4);
            var cEnd = ((cEntry.Header.FileSize - (cEntry.LROffsetFromParent * 4) + 2047) >> 11);

            dataSectors.UnionWith(Enumerable.Range((int)XISO.AlignUp(cPos), (int)cEnd).Select(s => (uint)s));

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
                unprocessed.Enqueue(cEntry);
            }

            if (rEntry.Header.Attributes.HasFlag(XISO.DirAttribute.Directory))
            {
                if (rEntry.Header.FileSize > 0)
                {
                    var dEntry = rEntry;
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
                    var count = XISO.AlignUp(rEntry.Header.FileSize);
                    dataSectors.UnionWith(Enumerable.Range((int)start, (int)count).Select(s => (uint)s));

                    progData.Current += rEntry.Header.FileSize;
                    progress?.Report(progData);
                }
            }

            if (rEntry.Header.RightOffset != 0)
            {
                cEntry.LROffsetFromParent = rEntry.Header.RightOffset;
                unprocessed.Enqueue(cEntry);
            }
        }

        if (unprocessed.Count >= 4000)
            throw new InvalidDataException("Too many directory entries found in image, likely malformed.");

        HashSet<uint>? ss = null;

        if (Platform == Exe.Platform.OriginalXbox)
        {
            var ret = GetSecuritySectors(dataSectors, progress, cancelToken).WaitAsync(cancelToken);
            if (ret.IsCanceled)
                return Task.FromCanceled<HashSet<uint>>(cancelToken);

            ss = ret.Result;
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

        DataSectors = dataSectors;
        return Task.FromResult(DataSectors);
    }

    public Task<List<SectorRange>> GetDataSectorRanges(IProgress<Converter.Progress>? progress = null, CancellationToken cancelToken = default)
    {
        if (DataSectorRanges.Count > 0)
            return Task.FromResult(DataSectorRanges);

        var ret = GetDataSectors(progress, cancelToken).WaitAsync(cancelToken);

        if (ret.IsCanceled)
            return Task.FromCanceled<List<SectorRange>>(cancelToken);

        var ds = ret.Result.OrderBy(s => s).ToList();

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
            if (cancelToken.IsCancellationRequested)
                return Task.FromCanceled<List<SectorRange>>(cancelToken);

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

    public abstract void ReadSector(uint sector, Span<byte> buffer);

    public virtual int ReadBytes(long offset, Span<byte> buffer)
    {
        int size = buffer.Length;

        if ((size % XISO.SECTOR_SIZE) == 0 && (offset % XISO.SECTOR_SIZE) == 0)
        {
            var read = 0;
            while (read < size)
            {
                ReadSector(XISO.AlignUp(offset + read), buffer.Slice(read, XISO.SECTOR_SIZE));
                read += XISO.SECTOR_SIZE;
            }
            return size;
        }

        var tempBuffer = new byte[XISO.SECTOR_SIZE];
        var readBytes = 0;
        var offsetInSector = (int)(offset % XISO.SECTOR_SIZE);
        var currSector = XISO.AlignUp(offset - offsetInSector);

        while (readBytes < size)
        {
            ReadSector(currSector, tempBuffer);
            var bytesToCopy = (int)Math.Min(size - readBytes, XISO.SECTOR_SIZE - offsetInSector);
            tempBuffer.AsSpan(offsetInSector, bytesToCopy).CopyTo(buffer.Slice(readBytes, bytesToCopy));
            readBytes += bytesToCopy;
            offsetInSector = 0;
            currSector++;
        }

        return size;
    }

    public uint ReadUInt32(long offset)
    {
        var buffer = new byte[4];
        ReadBytes(offset, buffer);
        return BitConverter.ToUInt32(buffer, 0);
    }

    public ushort ReadUInt16(long offset)
    {
        var buffer = new byte[2];
        ReadBytes(offset, buffer);
        return BitConverter.ToUInt16(buffer, 0);
    }

    public byte ReadByte(long offset)
    {
        var buffer = new byte[1];
        ReadBytes(offset, buffer);
        return buffer[0];
    }

    protected virtual Task InitializeType(IProgress<Converter.Progress>? progress = null, CancellationToken cancelToken = default)
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

    private Task<HashSet<uint>> GetSecuritySectors(HashSet<uint> dataSectors, IProgress<Converter.Progress>? progress = null, CancellationToken cancelToken = default)
    {
        if (TotalSectors != XISO.REDUMP_GAME_SECTORS || TotalSectors != XISO.REDUMP_TOTAL_SECTORS)
            return Task.FromResult(dataSectors);

        var securitySectors = new HashSet<uint>();
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
            if (cancelToken.IsCancellationRequested)
                return Task.FromCanceled<HashSet<uint>>(cancelToken);

            if (dataSectors.Contains(s))
            {
                flag = false;
                continue;
            }

            uint cSector = SectorOffset + s;
            ReadSector(cSector, buf);

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
                    securitySectors.UnionWith(
                        Enumerable.Range((int)start, (int)(end - start + 1))
                        .Select(i => (uint)i));
                }
                else if (compareMode && (end - start) > 0xFFF)
                {
                    securitySectors.Clear();
                    return Task.FromResult(securitySectors);
                }
            }

            progData.Current = s;
            progress?.Report(progData);
        }

        return Task.FromResult(securitySectors);
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