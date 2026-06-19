using XGDTool.Lib.Image.Formats;

namespace XGDTool.Lib.Image.Readers;

internal class Xiso : Base
{
    private readonly List<FileStream> Streams = [];

    public override Format ImageFormat => Format.XISO;
    public override uint TotalSectors => XDVDFS.SectorCount(Streams.Sum(s => s.Length));

    public Xiso(IReadOnlyList<string> files) : base(files)
    {
        foreach (var file in files)
        {
            var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (!XDVDFS.IsSectorAligned(stream.Length))
                throw new ArgumentException(
                    $"File '{file}' length is not aligned to sector size.", 
                    nameof(files));

            Streams.Add(stream);
        }
    }

    public override void ReadSectors(uint startSector, Span<byte> buffer, CancellationToken ct = default)
    {
        if (!XDVDFS.IsSectorAligned(buffer.Length))
            throw new ArgumentException(
                "Buffer length must be aligned to sector size.", nameof(buffer));

        uint sector = startSector;
        int bufferOffset = 0;
        int remainingBytes = buffer.Length;

        while (remainingBytes > 0)
        {
            var (stream, offset, available) = GetStreamForSector(sector);
            var toRead = (int)Math.Min(available, remainingBytes);

            ReadExactlyAt(stream, buffer.Slice(bufferOffset, toRead), offset);

            bufferOffset += toRead;
            remainingBytes -= toRead;
            sector += XDVDFS.SectorCount(toRead);
        }
    }

    // public override async Task ReadSectorsAsync(uint startSector, Memory<byte> buffer, CancellationToken ct = default)
    // {
    //     if (!XISO.IsSectorAligned(buffer.Length))
    //         throw new ArgumentException(
    //             "Buffer length must be aligned to sector size.", nameof(buffer));

    //     uint sector = startSector;
    //     int bufferOffset = 0;
    //     int remainingBytes = buffer.Length;

    //     while (remainingBytes > 0)
    //     {
    //         ct.ThrowIfCancellationRequested();

    //         var (stream, offset, available) = GetStreamForSector(sector);
    //         var toRead = (int)Math.Min(available, remainingBytes);

    //         await ReadExactlyAtAsync(stream, buffer.Slice(bufferOffset, toRead), offset, ct);

    //         bufferOffset += toRead;
    //         remainingBytes -= toRead;
    //         sector += XISO.SectorCount(toRead);
    //     }
    // }

    public static bool IsValid(string path)
    {
        var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buf = new byte[XDVDFS.MAGIC_SIZE];

        foreach (var offset in XDVDFS.ImageOffsets)
        {
            f.Seek(offset + XDVDFS.MAGIC_OFFSET, SeekOrigin.Begin);
            f.Read(buf);

            if (XDVDFS.MAGIC.SequenceEqual(buf))
                return true;
        }

        return false;
    }

    private (FileStream stream, long offset, long available) GetStreamForSector(uint sector)
    {
        long byteOffset = XDVDFS.SectorToOffset(sector);

        foreach (var stream in Streams)
        {
            if (byteOffset < stream.Length)
                return (stream, byteOffset, stream.Length - byteOffset);

            byteOffset -= stream.Length;
        }

        throw new ArgumentOutOfRangeException(
            nameof(sector), 
            "Sector offset exceeds total image size.");
    }
}
