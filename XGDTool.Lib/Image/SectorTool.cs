using System.Runtime.CompilerServices;
using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.Exe;

namespace XGDTool.Lib.Image;

public class SectorTool(IReader reader)
{
    private readonly IReader Reader = reader;
    private List<SectorRange> SectorRanges = [];

    public Task<List<SectorRange>> GetSectorRanges(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        if (SectorRanges.Count > 0)
            return Task.FromResult(SectorRanges);

        var dsRanges = GetDataSectorRanges(progress, ct);
        var maxDs = dsRanges.Max(r => r.End);
        List<SectorRange>? ssRanges = null;

        if (Reader.Platform == Platform.Xbox)
            ssRanges = GetSecuritySectorRanges(dsRanges, progress, ct);

        if (ssRanges == null || ssRanges.Count == 0)
        {
            SectorRanges = [ new SectorRange(Reader.SectorOffset, maxDs) ] ;
            return Task.FromResult(SectorRanges);
        }
        
        for (int i = 0; i < ssRanges.Count; i++)
        {
            if (ssRanges[i].Start > maxDs)
            {
                ssRanges.RemoveAt(i);
                i--;
            }
            else if (ssRanges[i].End > maxDs)
            {
                ssRanges[i] = new SectorRange(ssRanges[i].Start, maxDs);
            }
        }

        var sRanges = dsRanges.Union(ssRanges).OrderBy(s => s.Start).ToList();

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

    private List<SectorRange> GetDataSectorRanges(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        if (Reader.DirectoryEntries.Count == 0)
            throw new InvalidOperationException("reader must be initialized before getting data sectors.");

        var dsRanges = new List<SectorRange>();
        var processedCount = 0;
        var unprocessed = new Queue<DirectoryEntryExt>();
        var readBuf = new byte[XDVDFS.SECTOR_SIZE];

        var progData = new Converter.Progress()
        {
            Stage = Converter.Stage.LoadingDataSectors,
            Current = 0,
            Total = Reader.TotalSizeOfFiles
        };

        progress?.Report(progData);

        var headerSector = Reader.SectorOffset + XDVDFS.SectorIndex(XDVDFS.MAGIC_OFFSET);
        dsRanges.Add(new SectorRange(headerSector, headerSector + 1));

        unprocessed.Enqueue(Reader.GetRootEntry());

        while (unprocessed.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            if (++processedCount > 4000)
                throw new InvalidDataException("Too many directory entries found in image, likely malformed.");

            var cEntry = unprocessed.Dequeue();
            var cPos = Reader.ImageOffset + cEntry.RelativeOffset + (cEntry.LROffsetFromParent * sizeof(uint));
            {
                var cStart = XDVDFS.SectorIndex(cPos);
                var cEnd = 
                    cStart + 
                    XDVDFS.SectorCount(cEntry.FileSize - (cEntry.LROffsetFromParent * sizeof(uint))) - 
                    1;

                dsRanges.Add(new SectorRange(cStart, cEnd));
            }

            progData.Current += cEntry.FileSize;
            progress?.Report(progData);

            if (cEntry.LROffsetFromParent * 4 >= cEntry.FileSize)
                continue;

            var rEntry = Reader.ReadEntry(cPos, readBuf);

            if (rEntry.LeftOffset == XDVDFS.PAD_WORD)
                continue;

            if (rEntry.LeftOffset != 0)
            {
                cEntry.LROffsetFromParent = rEntry.LeftOffset;
                unprocessed.Enqueue(cEntry.Clone());
            }

            if (rEntry.Attributes.HasFlag(XDVDFS.DirAttributes.Directory))
            {
                if (rEntry.FileSize > 0)
                {
                    var dEntry = rEntry.Clone();
                    dEntry.LROffsetFromParent = 0;
                    dEntry.RelativeOffset = XDVDFS.SectorToOffset(rEntry.StartSector);
                    unprocessed.Enqueue(dEntry);
                }
            }
            else
            {
                if (rEntry.FileSize > 0)
                {
                    var start = checked(Reader.SectorOffset + rEntry.StartSector);
                    var end = start + XDVDFS.SectorCount(rEntry.FileSize) - 1;
                    dsRanges.Add(new SectorRange(start, end));

                    progData.Current += rEntry.FileSize;
                    progress?.Report(progData);
                }
            }

            if (rEntry.RightOffset != 0)
            {
                cEntry.LROffsetFromParent = rEntry.RightOffset;
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

        if (Reader.TotalSectors != XDVDFS.REDUMP_GAME_SECTORS &&
            Reader.TotalSectors != XDVDFS.REDUMP_TOTAL_SECTORS)
        {
            return ssRanges;
        }

        const uint BufferSectors = 512;
        var buf = new byte[BufferSectors * XDVDFS.SECTOR_SIZE];
        var progData = new Converter.Progress
        {
            Stage = Converter.Stage.LoadingSecuritySectors,
            Current = 0,
            Total = XDVDFS.REDUMP_END_SECTOR + 1
        };

        progress?.Report(progData);

        bool inEmptyRun = false;
        uint runStart = 0;
        int dataIndex = 0;

        for (uint sectorIndex = 0; sectorIndex <= XDVDFS.REDUMP_END_SECTOR;)
        {
            ct.ThrowIfCancellationRequested();

            uint sectorsRemaining = XDVDFS.REDUMP_END_SECTOR - sectorIndex + 1;
            uint readCount = Math.Min(BufferSectors, sectorsRemaining);

            uint firstSector = Reader.SectorOffset + sectorIndex;
            int byteCount = checked((int)(readCount * XDVDFS.SECTOR_SIZE));

            Reader.ReadSectors(firstSector, buf.AsSpan(0, byteCount), ct);

            for (uint i = 0; i < readCount; i++)
            {
                uint currentSector = firstSector + i;

                while (dataIndex < dsRanges.Count && dsRanges[dataIndex].End < currentSector)
                    dataIndex++;

                bool isDataSector =
                    dataIndex < dsRanges.Count &&
                    currentSector >= dsRanges[dataIndex].Start &&
                    currentSector <= dsRanges[dataIndex].End;

                var sectorSpan = buf.AsSpan(
                    checked((int)(i * XDVDFS.SECTOR_SIZE)),
                    XDVDFS.SECTOR_SIZE);

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
                        ssRanges.Add(new SectorRange(runStart, runEnd));
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsEmpySpan(ReadOnlySpan<byte> span)
    {
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] != 0)
                return false;
        }
        return true;
    }
}