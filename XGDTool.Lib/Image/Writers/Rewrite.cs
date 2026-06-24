using XGDTool.Lib.Image.Formats;

namespace XGDTool.Lib.Image.Writers;

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
        List<SectorRange>? dsRanges = null;

        if (Options.Scrub == true)
        {
            var sectorTool = new SectorTool(Reader);
            dsRanges = await sectorTool.GetSectorRanges(progress, ct);
        }
        else
        {
            dsRanges = [ new(Reader.SectorOffset, Reader.TotalSectors - 1) ];
        }

        var totalSectors = dsRanges.Max(r => r.End) + 1;
        await SectorSink.Initialize(totalSectors * XDVDFS.SECTOR_SIZE, progress, ct);

        byte[][] buffers =
        [
            new byte[BufferSectors * XDVDFS.SECTOR_SIZE],
            new byte[BufferSectors * XDVDFS.SECTOR_SIZE]
        ];
        var progData = new Converter.Progress
        {
            Stage = Converter.Stage.WritingData,
            Current = 0,
            Total = dsRanges.Sum(r => r.End - r.Start)
        };

        Task pendingWrite = Task.CompletedTask;
        int buffIndex = 0;
        Dictionary<uint, byte>? sysUpdateRanges = null;

        if (Options.SkipSystemUpdate == true && Reader.Platform == Exe.Platform.Xbox360)
            sysUpdateRanges = GetSystemUpdateRanges(ct);

        foreach (var range in dsRanges)
        {
            var sector = range.Start;
            if (range.Start > range.End)
                throw new InvalidOperationException(
                    $"Invalid sector range: {range.Start} - {range.End}");

            var remaining = checked(range.End - range.Start) + 1;

            while (remaining > 0)
            {
                await pendingWrite;

                ct.ThrowIfCancellationRequested();

                var buf = buffers[buffIndex];
                buffIndex ^= 1;

                int sectorsToRead = (int)Math.Min(BufferSectors, remaining);
                int byteCount = sectorsToRead * XDVDFS.SECTOR_SIZE;

                await Reader.ReadSectorsAsync(
                    sector,
                    buf.AsMemory(0, byteCount),
                    ct);

                if (sysUpdateRanges != null)
                {
                    for (int i = 0; i < sectorsToRead; i++)
                    {
                        if (sysUpdateRanges.TryGetValue(sector + (uint)i, out var fill))
                            buf.AsSpan(i * XDVDFS.SECTOR_SIZE, XDVDFS.SECTOR_SIZE).Fill(fill);
                    }
                }

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

    private Dictionary<uint, byte>? GetSystemUpdateRanges(CancellationToken ct)
    {
        Dictionary<uint, byte> sysUpdateRanges = [];

        void InsertRange(uint sector, uint count, byte padByte)
        {
            for (uint i = 0; i < count; i++)
            {
                if (!sysUpdateRanges.ContainsKey(sector + i))
                    sysUpdateRanges[sector + i] = padByte;
            }
        }

        var sysUpdateEntries = Reader.DirectoryEntries
            .Where(e => e.FilePath.StartsWith(
                XDVDFS.SYSTEM_UPDATE_DIRECTORY_NAME, 
                StringComparison.OrdinalIgnoreCase));

        var sysUpdateRoot = sysUpdateEntries.FirstOrDefault(e => 
            e.FilePath.Equals(XDVDFS.SYSTEM_UPDATE_DIRECTORY_NAME, StringComparison.OrdinalIgnoreCase) &&
            e.Attributes.HasFlag(XDVDFS.DirAttributes.Directory));

        if (sysUpdateRoot == null)
            return null;

        // Pad $SystemUpdate's entire directory-table sector so it appears empty.
        // Leave its own entry in the root dir table so the AVL tree stays valid.
        {
            var dirTableSector = (uint)sysUpdateRoot.StartSector + Reader.SectorOffset;
            var dirTableSectors = XDVDFS.SectorCount(sysUpdateRoot.FileSize);
            InsertRange(dirTableSector, dirTableSectors, XDVDFS.PAD_BYTE);
        }

        foreach (var entry in sysUpdateEntries)
        {
            ct.ThrowIfCancellationRequested();

            if (entry == sysUpdateRoot)
                continue;

            if (entry.Attributes.HasFlag(XDVDFS.DirAttributes.Directory) || entry.FileSize == 0)
                continue;

            var startSector = (uint)entry.StartSector + Reader.SectorOffset;
            var sectorCount = XDVDFS.SectorCount(entry.FileSize);
            InsertRange(startSector, sectorCount, 0);
        }

        return sysUpdateRanges;
    }
}
