using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDTool.Lib.Image.Formats;

namespace XGDTool.Lib.Image.Writer;

internal class Rewrite : IWriter
{
    private readonly IReader Reader;
    private readonly IWriterOptions Options;
    private readonly Title.Info TitleInfo;
    private readonly ISectorSink SectorSink;
    private const int BufferSectors = 256;

    public Rewrite(IReader reader, IWriterOptions options, Title.Info titleInfo)
    {
        Reader = reader;
        Options = options;
        TitleInfo = titleInfo;
        SectorSink = ISectorSink.Create(Options, TitleInfo);
    }

    public async Task<IReadOnlyList<string>> Convert(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        List<Reader.SectorRange>? dsRanges = null;

        if (Options.Scrub == true)
        {
            dsRanges = await Reader.GetSectorRanges(progress, ct);
        }
        else
        {
            dsRanges = new List<Reader.SectorRange> 
            { 
                new(Reader.SectorOffset, Reader.TotalSectors - 1) 
            };
        }

        var totalSectors = dsRanges.Max(r => r.End) + 1;
        await SectorSink.Initialize(totalSectors * XISO.SECTOR_SIZE, progress, ct);

        byte[][] buffers =
        {
            new byte[BufferSectors * XISO.SECTOR_SIZE],
            new byte[BufferSectors * XISO.SECTOR_SIZE]
        };
        var progData = new Converter.Progress
        {
            Stage = Converter.Stage.WritingData,
            Current = 0,
            Total = dsRanges.Sum(r => r.End - r.Start)
        };
        Task pendingWrite = Task.CompletedTask;
        int buffIndex = 0;

        foreach (var range in dsRanges)
        {
            var sector = range.Start;
            if (range.Start > range.End)
                throw new InvalidOperationException($"Invalid sector range: {range.Start} - {range.End}");

            var remaining = checked(range.End - range.Start) + 1;

            while (remaining > 0)
            {
                await pendingWrite;

                var buf = buffers[buffIndex];
                buffIndex ^= 1;

                int sectorsToRead = (int)Math.Min(BufferSectors, remaining);
                int byteCount = sectorsToRead * XISO.SECTOR_SIZE;

                await Reader.ReadSectorsAsync(
                    sector,
                    buf.AsMemory(0, byteCount),
                    ct);

                uint writeSector = checked(sector - Reader.SectorOffset);
                Memory<byte> writeBuffer = buf.AsMemory(0, byteCount);

                pendingWrite = SectorSink.WriteSectorsAsync(
                    writeSector,
                    writeBuffer,
                    ct);

                sector += (uint)sectorsToRead;
                remaining -= (uint)sectorsToRead;

                progData.Current += (uint)sectorsToRead;
                progress?.Report(progData);
            }
        }

        await pendingWrite;

        progData.Current = progData.Total;
        progress?.Report(progData);

        return await SectorSink.FinalizeImage(progress, ct);
    }

    public void CleanupCancelled() => SectorSink.CleanupCancelled();
}
