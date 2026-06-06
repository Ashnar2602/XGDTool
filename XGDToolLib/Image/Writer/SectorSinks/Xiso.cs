using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Image.Format;

namespace XGDToolLib.Image.Writer.SectorSinks;

internal class Xiso(IReader reader, IWriterOptions options, Title.Info titleInfo) : ISectorSink
{
    private readonly IReader Reader = reader;
    private readonly IWriterOptions Options = options;
    private readonly Title.Info TitleInfo = titleInfo;
    private readonly List<FileStream> Streams = new();
    private bool Split => Options.Split == true;
    private bool FirstRenamed = false;
    private bool DirectoryCreated = false;

    public Task Initialize(long outImageSize, IProgress<Converter.Progress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(Options.OutDirectory))
        {
            Directory.CreateDirectory(Options.OutDirectory);
            if (!Directory.Exists(Options.OutDirectory))
                throw new IOException($"Failed to create output directory: {Options.OutDirectory}");

            DirectoryCreated = true;
        }

        return Task.CompletedTask;
    }

    public async Task WriteSectorsAsync(uint startSector, ReadOnlyMemory<byte> buffer, CancellationToken cancelToken = default)
    {
        if (!XISO.IsAligned(buffer.Length))
            throw new ArgumentException($"Buffer length must be a multiple of {XISO.SECTOR_SIZE}", nameof(buffer));

        var (stream, offset) = GetStreamForSector(startSector);
        var remaingFileBytes = Split ? (XISO.SPLIT_MARGIN - offset) : int.MaxValue;
        var writeCount = (int)Math.Min(buffer.Length, remaingFileBytes);

        stream.Seek(offset, SeekOrigin.Begin);
        await stream.WriteAsync(buffer.Slice(0, writeCount), cancelToken);

        if (writeCount < buffer.Length)
            await WriteSectorsAsync(
                startSector + XISO.AlignUp(writeCount), 
                buffer.Slice(writeCount), 
                cancelToken);
    }

    public async Task<List<string>> FinalizeImage(IProgress<Converter.Progress>? progress = null, CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < Streams.Count; i++)
        {
            var stream = Streams[i];
            await stream.FlushAsync(cancellationToken);

            if (stream.Length % XISO.SECTOR_SIZE != 0)
            {
                var padLen = XISO.SECTOR_SIZE - (stream.Length % XISO.SECTOR_SIZE);
                if (padLen != XISO.SECTOR_SIZE)
                    stream.SetLength(stream.Length + padLen);
            }
            if (i == (Streams.Count - 1) && (stream.Length % XISO.FILE_MODULUS) != 0)
            {
                var padLen = XISO.FILE_MODULUS - (stream.Length % XISO.FILE_MODULUS);
                if (padLen != XISO.FILE_MODULUS)
                    stream.SetLength(stream.Length + padLen);
            }
        }

        var names = Streams.AsEnumerable().Select(s => s.Name).ToList();
        Streams.ForEach(s => s.Dispose());
        return names;
    }

    public void CleanupCanceled()
    {
        Streams.ForEach(s => s.Dispose());
        Streams.ForEach(s => File.Delete(s.Name));
        Streams.Clear();

        if (DirectoryCreated)
        {
            try
            {
                Directory.Delete(Options.OutDirectory);
            }
            catch
            {
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (FileStream stream, long offset) GetStreamForSector(uint sector)
    {
        var offset = XISO.SectorToOffset(sector);

        if (!Split || offset < XISO.SPLIT_MARGIN)
            return (GetOrCreateStream(0), sector * XISO.SECTOR_SIZE);

        return (GetOrCreateStream((int)(offset / XISO.SPLIT_MARGIN)), offset % XISO.SPLIT_MARGIN);
    }

    private FileStream GetOrCreateStream(int index)
    {
        if (index < Streams.Count)
            return Streams[index];

        if (index == 0)
        {
            Streams.Add(new FileStream(
                Path.Join(Options.OutDirectory, TitleInfo.ImageName + ".iso"), 
                FileMode.Create, 
                FileAccess.Write));
            return Streams[0];
        }

        if (!FirstRenamed)
        {
            var name = Streams[0].Name;
            var newName = Path.Join(Options.OutDirectory, $"{TitleInfo.ImageName}.1.iso");

            Streams[0].Dispose();
            File.Move(name, newName);
            FirstRenamed = true;
            Streams[0] = new FileStream(newName, FileMode.Open, FileAccess.Write);
        }

        for (int i = Streams.Count; i <= index; i++)
        {
            Streams.Add(new FileStream(
                Path.Join(Options.OutDirectory, $"{TitleInfo.ImageName}.{i}.iso"), 
                FileMode.Create, 
                FileAccess.Write));
        }

        return Streams[index];
    }
}
