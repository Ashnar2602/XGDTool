using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Buffers;
using XGDToolLib.Exe;
using XGDToolLib.Image.Format;

namespace XGDToolLib.Image.Writers;

internal class XisoPassthrough : Writer
{
    private readonly SplitIoStream.Out Out;

    public XisoPassthrough(Reader reader, Options options, Title.Info titleInfo)
        : base(reader, options, titleInfo)
    {
        Out = new SplitIoStream.Out(
            Path.Join(OutOptions.OutDirectory, TitleInfo.ImageName + ".iso"), 
            OutOptions.Split ?? false ? XISO.SPLIT_MARGIN : null);
    }

    public override async Task<IReadOnlyList<string>> Convert(
        IProgress<Converter.Progress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        bool scrubOg = OutOptions.ConvertType == Converter.Type.Scrub &&
                       Reader.Platform == Platform.OriginalXbox;

        uint startSector = Reader.SectorOffset;
        uint endSectorExclusive = Reader.TotalSectors;

        if (OutOptions.ConvertType == Converter.Type.Scrub && Reader.DataSectors.Count > 0)
            endSectorExclusive = Math.Min(endSectorExclusive, Reader.DataSectors.Max() + 1);

        const int chunkSectors = 512; // 1MB per chunk at 2048-byte sectors
        int chunkBytes = chunkSectors * XISO.SECTOR_SIZE;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(chunkBytes);

        try
        {
            long totalBytes = ((long)endSectorExclusive - startSector) * XISO.SECTOR_SIZE;
            long writtenBytes = 0;
            long outOffset = 0;

            var progData = new Converter.Progress
            {
                Stage = Converter.Stage.WritingData,
                Current = 0,
                Total = totalBytes
            };

            async Task WriteZeroSectorsAsync(uint sectorCount)
            {
                while (sectorCount > 0)
                {
                    int takeSectors = (int)Math.Min((uint)chunkSectors, sectorCount);
                    int bytes = takeSectors * XISO.SECTOR_SIZE;
                    buffer.AsSpan(0, bytes).Clear();

                    await Out.WriteAsync(
                        outOffset, 
                        buffer.AsMemory(0, bytes), 
                        cancellationToken);

                    outOffset += bytes;
                    writtenBytes += bytes;
                    sectorCount -= (uint)takeSectors;

                    progData.Current = writtenBytes;
                    progress?.Report(progData);
                }
            }

            async Task CopySectorsAsync(uint sectorStart, uint sectorEndExclusive)
            {
                uint sector = sectorStart;
                while (sector < sectorEndExclusive)
                {
                    uint remaining = sectorEndExclusive - sector;
                    int takeSectors = (int)Math.Min((uint)chunkSectors, remaining);
                    int bytes = takeSectors * XISO.SECTOR_SIZE;

                    long inOffset = (long)sector * XISO.SECTOR_SIZE;

                    //await Reader.ReadBytesAsync(
                    //    inOffset, 
                    //    buffer.AsMemory(0, bytes),
                    //    cancellationToken);
                    Reader.ReadBytes(inOffset, buffer.AsSpan(0, bytes));

                    await Out.WriteAsync(
                        outOffset, 
                        buffer.AsMemory(0, bytes), 
                        cancellationToken);

                    outOffset += bytes;
                    writtenBytes += bytes;
                    sector += (uint)takeSectors;

                    progData.Current = writtenBytes;
                    progress?.Report(progData);
                }
            }

            if (!scrubOg)
            {
                await CopySectorsAsync(startSector, endSectorExclusive);
            }
            else
            {
                uint cursor = startSector;
                foreach (var r in Reader.GetSectorRanges(startSector, endSectorExclusive, dataOnly: true))
                {
                    if (cursor < r.Start)
                        await WriteZeroSectorsAsync(r.Start - cursor);

                    await CopySectorsAsync(r.Start, r.EndExclusive);
                    cursor = r.EndExclusive;
                }

                if (cursor < endSectorExclusive)
                    await WriteZeroSectorsAsync(endSectorExclusive - cursor);
            }

            progData.Current = progData.Total;
            progress?.Report(progData);

            return Out.Filepaths;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
