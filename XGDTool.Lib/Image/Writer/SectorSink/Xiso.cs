using System.Runtime.CompilerServices;
using XGDTool.Lib.Image.Formats;

namespace XGDTool.Lib.Image.Writer.SectorSink;

internal class Xiso(IWriterOptions options, Title.Info titleInfo) : ISectorSink
{
    private readonly IWriterOptions Options = options;
    private readonly Title.Info TitleInfo = titleInfo;
    private readonly List<FileStream> Streams = new();
    private readonly SemaphoreSlim WriteLock = new(1, 1);
    private long TotalOutSize = 0;
    private bool Split => Options.Split == true;
    private bool DirectoryCreated = false;

    public Task Initialize(long totalOutSize, IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        if (!Directory.Exists(Options.OutputDirectory))
        {
            Directory.CreateDirectory(Options.OutputDirectory);

            if (!Directory.Exists(Options.OutputDirectory))
                throw new IOException($"Failed to create output directory: {Options.OutputDirectory}");

            DirectoryCreated = true;
        }

        TotalOutSize = totalOutSize;
        return Task.CompletedTask;
    }

    public async Task WriteSectorsAsync(uint startSector, ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        if (!XISO.IsSectorAligned(buffer.Length))
            throw new ArgumentException($"Buffer length must be a multiple of {XISO.SECTOR_SIZE}", nameof(buffer));

        await WriteLock.WaitAsync(ct);
        try
        {
            var (stream, offset) = GetStreamForSector(startSector);
            var remaingFileBytes = Split ? (XISO.SPLIT_MARGIN - offset) : long.MaxValue;
            var writeCount = (int)Math.Min(buffer.Length, remaingFileBytes);

            stream.Seek(offset, SeekOrigin.Begin);
            await stream.WriteAsync(buffer.Slice(0, writeCount), ct);

            if (writeCount < buffer.Length)
                await WriteSectorsAsync(
                    startSector + XISO.SectorCount(writeCount),
                    buffer.Slice(writeCount),
                    ct);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public async Task<List<string>> FinalizeImage(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        await WriteLock.WaitAsync(ct);
        try
        {
            long totalWritten = Streams.Sum(s => s.Length);

            if (!XISO.IsSectorAligned(totalWritten))
                throw new InvalidOperationException($"Total written bytes must be sector aligned, but was {totalWritten}");

            while (totalWritten < TotalOutSize)
            {
                var padding = new byte[256 * XISO.SECTOR_SIZE];
                var writeCount = (int)Math.Min(padding.Length, TotalOutSize - totalWritten);
                await WriteSectorsAsync(XISO.SectorIndex(totalWritten), padding.AsMemory(0, writeCount), ct);
                totalWritten += writeCount;
            }

            Streams.ForEach(s => { s.Flush(); s.Dispose(); });
            return Streams.AsEnumerable().Select(s => s.Name).ToList();
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public void CleanupCancelled()
    {
        Streams.ForEach(s => s.Dispose());
        Streams.ForEach(s => File.Delete(s.Name));
        Streams.Clear();

        if (DirectoryCreated)
        {
            try
            {
                Directory.Delete(Options.OutputDirectory, true);
            }
            catch { }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (FileStream stream, long offset) GetStreamForSector(uint sector)
    {
        var offset = XISO.SectorToOffset(sector);

        if (!Split || offset < XISO.SPLIT_MARGIN)
            return (GetOrCreateStream(0), XISO.SectorToOffset(sector));

        return (GetOrCreateStream((int)(offset / XISO.SPLIT_MARGIN)), offset % XISO.SPLIT_MARGIN);
    }

    private FileStream GetOrCreateStream(int index)
    {
        if (index < Streams.Count)
            return Streams[index];

        if (index == 0)
        {
            Streams.Add(new FileStream(
                Path.Join(Options.OutputDirectory, TitleInfo.ImageName + ".iso"), 
                FileMode.Create, 
                FileAccess.Write));
            return Streams[0];
        }

        if (!Streams[0].Name.EndsWith(".1.iso", StringComparison.OrdinalIgnoreCase))
        {
            var name = Streams[0].Name;
            var newName = Path.Join(Options.OutputDirectory, $"{TitleInfo.ImageName}.1.iso");

            Streams[0].Flush();
            Streams[0].Dispose();
            File.Move(name, newName);
            Streams[0] = new FileStream(newName, FileMode.Open, FileAccess.Write);
        }

        for (int i = Streams.Count; i <= index; i++)
        {
            var stream = new FileStream(
                Path.Join(Options.OutputDirectory, $"{TitleInfo.ImageName}.{i + 1}.iso"),
                FileMode.Create,
                FileAccess.Write);

            if (i < index)
                stream.SetLength(XISO.SPLIT_MARGIN);

            Streams.Add(stream);
        }

        return Streams[index];
    }
}
