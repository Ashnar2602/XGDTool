using XGDTool.Lib.Image.Format;

namespace XGDTool.Lib.Image.Reader;

internal class Xiso : Base
{
    private readonly List<FileStream> Streams = new();

    public override Type ImageType => Type.XISO;
    public override uint TotalSectors { get; protected set; }

    public Xiso(IReadOnlyList<string> files) : base(files)
    {
        foreach (var file in files)
        {
            var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!XISO.IsSectorAligned(stream.Length))
            {
                stream.Dispose();
                Streams.Clear();
                throw new ArgumentException(
                    $"File '{file}' length is not aligned to sector size.", 
                    nameof(files));
            }
            Streams.Add(stream);
        }
    }

    public override void ReadSectors(uint startSector, Span<byte> buffer)
    {
        if (!XISO.IsSectorAligned(buffer.Length))
            throw new ArgumentException(
                "Buffer length must be aligned to sector size.", nameof(buffer));

        var (stream, offset) = GetStreamForSector(startSector);
        var maxReadBytes = (int)Math.Min(stream.Length - offset, buffer.Length);

        stream.Seek(offset, SeekOrigin.Begin);
        var len = stream.Read(buffer.Slice(0, maxReadBytes));

        if (len != maxReadBytes)
            throw new IOException(
                $"Expected to read {maxReadBytes} bytes but only read {len} bytes from stream.");

        if (maxReadBytes < buffer.Length)
            ReadSectors(
                startSector + XISO.AlignUpToSector(maxReadBytes), 
                buffer.Slice(maxReadBytes));
    }

    public override async Task ReadSectorsAsync(uint startSector, Memory<byte> buffer, CancellationToken ct = default)
    {
        if (!XISO.IsSectorAligned(buffer.Length))
            throw new ArgumentException(
                "Buffer length must be aligned to sector size.", nameof(buffer));

        var (stream, offset) = GetStreamForSector(startSector);
        var maxReadBytes = (int)Math.Min(stream.Length - offset, buffer.Length);

        stream.Seek(offset, SeekOrigin.Begin);
        var len = await stream.ReadAsync(buffer.Slice(0, maxReadBytes), ct);

        if (len != maxReadBytes)
            throw new IOException(
                $"Expected to read {maxReadBytes} bytes but only read {len} bytes from stream.");

        if (maxReadBytes < buffer.Length)
            await ReadSectorsAsync(
                startSector + XISO.AlignUpToSector(maxReadBytes), 
                buffer.Slice(maxReadBytes), 
                ct);
    }

    public static bool IsValid(string path)
    {
        var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buf = new byte[XISO.MAGIC_SIZE];

        foreach (var offset in XISO.ImageOffsets)
        {
            f.Seek(offset + XISO.MAGIC_OFFSET, SeekOrigin.Begin);
            f.Read(buf);

            if (XISO.MAGIC.SequenceEqual(buf))
                return true;
        }

        return false;
    }

    private (FileStream stream, long offset) GetStreamForSector(uint sector)
    {
        long byteOffset = XISO.SectorToOffset(sector);

        foreach (var stream in Streams)
        {
            if (byteOffset < stream.Length)
                return (stream, byteOffset);

            byteOffset -= stream.Length;
        }

        throw new ArgumentOutOfRangeException(
            nameof(sector), 
            "Sector offset exceeds total image size.");
    }
}
