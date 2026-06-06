using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Exe;
using XGDToolLib.Image.Format;
using static XGDToolLib.Image.Writer;

namespace XGDToolLib.Image.Writers;

public class GodPassthrough(Reader reader, Options options, Title.Info titleInfo) 
    : GodBase(reader, options, titleInfo)
{
    private uint LastSectorExclusive =>
        (OutOptions.ConvertType == Converter.Type.Scrub)
            ? (Reader.DataSectors.Count == 0
                ? Reader.SectorOffset
                : Math.Min(Reader.TotalSectors, Reader.DataSectors.Max() + 1u))
            : Reader.TotalSectors;

    protected override long GetTotalOutDataBytes() => 
        ((LastSectorExclusive - Reader.SectorOffset) * XISO.SECTOR_SIZE);

    protected override async Task WriteData(
        IProgress<Converter.Progress>? progress,
        CancellationToken cancellationToken)
    {
        bool scrub = OutOptions.ConvertType == Converter.Type.Scrub;
        const int BatchSectors = 256;

        byte[] dataBuffer = ArrayPool<byte>.Shared.Rent(BatchSectors * XISO.SECTOR_SIZE);
        uint[] sectorIds = ArrayPool<uint>.Shared.Rent(BatchSectors);

        try
        {
            IEnumerable<Reader.SectorRange> ranges = Reader.GetSectorRanges(
                Reader.SectorOffset,
                LastSectorExclusive,
                scrub);

            foreach (var range in ranges)
            {
                uint sector = range.Start;

                while (sector < range.EndExclusive)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int count = 0;
                    uint batchStart = sector;
                    uint batchEnd = Math.Min(
                        range.EndExclusive, batchStart + (uint)BatchSectors);

                    // Read contiguous source sectors into batch buffer
                    while (sector < batchEnd)
                    {
                        int idx = count++;
                        sectorIds[idx] = sector;

                        //await Reader.ReadSectorAsync(
                        //    sector,
                        //    dataBuffer.AsMemory(idx * XISO.SECTOR_SIZE, XISO.SECTOR_SIZE),
                        //    cancellationToken);
                        Reader.ReadSector(
                            sector,
                            dataBuffer.AsSpan(idx * XISO.SECTOR_SIZE, XISO.SECTOR_SIZE));

                        sector++;
                    }

                    // Coalesce destination writes by contiguous remap
                    int runStart = 0;
                    var firstRemap = RemapSector(sectorIds[0]);
                    int runFileIndex = firstRemap.FileIndex;
                    long runOutOffset = firstRemap.Offset;
                    long prevOutOffset = firstRemap.Offset;

                    for (int i = 1; i <= count; i++)
                    {
                        bool endRun = (i == count);

                        if (!endRun)
                        {
                            var r = RemapSector(sectorIds[i]);
                            bool contiguous =
                                r.FileIndex == runFileIndex &&
                                r.Offset == prevOutOffset + XISO.SECTOR_SIZE;

                            if (contiguous)
                                prevOutOffset = r.Offset;
                            else
                                endRun = true;
                        }

                        if (endRun)
                        {
                            int sectorsInRun = i - runStart;
                            int bytesInRun = sectorsInRun * XISO.SECTOR_SIZE;
                            var stream = FileParts[runFileIndex].Stream;

                            await RandomAccess.WriteAsync(
                                stream.SafeFileHandle,
                                dataBuffer.AsMemory(runStart * XISO.SECTOR_SIZE, bytesInRun),
                                runOutOffset,
                                cancellationToken);

                            ProgData.Current += bytesInRun;
                            progress?.Report(ProgData);

                            if (i < count)
                            {
                                var next = RemapSector(sectorIds[i]);
                                runStart = i;
                                runFileIndex = next.FileIndex;
                                runOutOffset = next.Offset;
                                prevOutOffset = next.Offset;
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(dataBuffer);
            ArrayPool<uint>.Shared.Return(sectorIds);
        }
    }
}
