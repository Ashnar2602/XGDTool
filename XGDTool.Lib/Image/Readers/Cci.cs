using System.Runtime.InteropServices;
using K4os.Compression.LZ4;
using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Image.Readers;

internal class Cci(IReadOnlyList<string> files) : Base(files)
{
    private class FilePart
    {
        public required FileStream Stream;
        public required CCI.Header Header;
        public required uint[] SectorIndex;
        public uint NumSectors => XDVDFS.SectorCount((long)Header.UncompressedSize);
    }

    private readonly List<FilePart> FileParts = [];

    public override Format ImageFormat => Format.CCI;
    public override uint TotalSectors => (uint)FileParts.Sum(p => p.NumSectors);

    public static bool IsValid(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using var stream = new FileStream(
                path, 
                FileMode.Open, 
                FileAccess.Read, 
                FileShare.Read);

            var headerBuf = new byte[CCI.Header.SIZE];
            stream.ReadExactly(headerBuf);

            return IsValidHeader(ISerializable.Deserialize<CCI.Header>(headerBuf));
        }
        catch
        {
            return false;
        }
    }

    protected override async Task InitializeType(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        var progData = new Converter.Progress
        {
            Stage = Converter.Stage.Initializing,
            Current = 0,
            Total = FilePaths.Count
        };
        progress?.Report(progData);

        foreach (var file in FilePaths)
        {
            var stream = new FileStream(
                file, 
                FileMode.Open, 
                FileAccess.Read, 
                FileShare.Read,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.RandomAccess);

            var headerBuf = new byte[CCI.Header.SIZE];
            await stream.ReadExactlyAsync(headerBuf, ct);

            var header = ISerializable.Deserialize<CCI.Header>(headerBuf);

            if (!IsValidHeader(header))
                throw new ArgumentException($"File '{file}' is not a valid CCI part.", nameof(file));

            var numSectors = XDVDFS.SectorCount(checked((long)header.UncompressedSize));
            var sectorIndex = new uint[numSectors + 1];
            var indexBytes = new byte[sectorIndex.Length * sizeof(uint)];

            stream.Seek(checked((long)header.IndexOffset), SeekOrigin.Begin);
            await stream.ReadExactlyAsync(indexBytes, ct);

            MemoryMarshal.Cast<byte, uint>(indexBytes).CopyTo(sectorIndex);

            FileParts.Add(new FilePart
            {
                Stream = stream,
                Header = header,
                SectorIndex = sectorIndex
            });

            progData.Current++;
            progress?.Report(progData);
        }
    }

    public override void ReadSectors(uint startSector, Span<byte> buffer, CancellationToken ct = default)
    {
        if (!XDVDFS.IsSectorAligned(buffer.Length))
        {
            throw new ArgumentException(
                "Buffer length must be aligned to sector size.",
                nameof(buffer));
        }

        uint remaining = XDVDFS.SectorCount(buffer.Length);
        uint globalSector = startSector;
        int outOffset = 0;
        var pool = System.Buffers.ArrayPool<byte>.Shared;
        byte[] block = pool.Rent(XDVDFS.SECTOR_SIZE);

        try
        {
            while (remaining > 0)
            {
                var (part, localSector) = GetPartForSector(globalSector);

                while (remaining > 0 && localSector < part.NumSectors)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    int runCount = GetContiguousUncompressedRun(part, localSector, remaining, out long runOffset);

                    if (runCount > 0)
                    {
                        int runBytes = checked(runCount * XDVDFS.SECTOR_SIZE);
                        ReadExactlyAt(part.Stream, buffer.Slice(outOffset, runBytes), runOffset);

                        localSector += (uint)runCount;
                        globalSector += (uint)runCount;
                        remaining -= (uint)runCount;
                        outOffset += runBytes;
                        continue;
                    }

                    var curr = CCI.DecodeIndexEntry(part.SectorIndex[localSector], part.Header.IndexAlignment);
                    var next = CCI.DecodeIndexEntry(part.SectorIndex[localSector + 1], part.Header.IndexAlignment);
                    long size = checked(next.offset - curr.offset);
                    bool treatAsCompressed = curr.compressed || size < XDVDFS.SECTOR_SIZE;
                    var dest = buffer.Slice(outOffset, XDVDFS.SECTOR_SIZE);

                    if (!treatAsCompressed)
                    {
                        if (size != XDVDFS.SECTOR_SIZE)
                        {
                            throw new InvalidOperationException(
                                $"Expected uncompressed sector to be {XDVDFS.SECTOR_SIZE} bytes, but got {size} bytes.");
                        }

                        ReadExactlyAt(part.Stream, dest, curr.offset);
                    }
                    else
                    {
                        if (size < 1 || size > XDVDFS.SECTOR_SIZE)
                        {
                            throw new InvalidOperationException(
                                $"Compressed sector size must be between 1 and {XDVDFS.SECTOR_SIZE} bytes, but got {size} bytes.");
                        }

                        int blockSize = checked((int)size);
                        ReadExactlyAt(part.Stream, block.AsSpan(0, blockSize), curr.offset);

                        int padLen = block[0];
                        int compressedSize = blockSize - 1 - padLen;

                        if (compressedSize <= 0)
                        {
                            throw new InvalidOperationException(
                                $"Invalid pad length {padLen} for compressed sector of size {blockSize} bytes.");
                        }

                        int decodedSize = LZ4Codec.Decode(
                            block.AsSpan(1, compressedSize),
                            dest);

                        if (decodedSize != XDVDFS.SECTOR_SIZE)
                        {
                            throw new InvalidOperationException(
                                $"Expected decompressed sector to be {XDVDFS.SECTOR_SIZE} bytes, but got {decodedSize} bytes.");
                        }
                    }

                    localSector++;
                    globalSector++;
                    remaining--;
                    outOffset += XDVDFS.SECTOR_SIZE;
                }
            }
        }
        finally
        {
            pool.Return(block);
        }
    }

    public override async Task ReadSectorsAsync(uint startSector, Memory<byte> buffer, CancellationToken ct = default)
    {
        if (!XDVDFS.IsSectorAligned(buffer.Length))
            throw new ArgumentException(
                "Buffer length must be aligned to sector size.", nameof(buffer));

        uint remaining = XDVDFS.SectorCount(buffer.Length);
        uint globalSector = startSector;
        int outOffset = 0;
        var pool = System.Buffers.ArrayPool<byte>.Shared;
        byte[] block = pool.Rent(XDVDFS.SECTOR_SIZE);

        try
        {
            while (remaining > 0)
            {
                ct.ThrowIfCancellationRequested();

                var (part, localSector) = GetPartForSector(globalSector);

                while (remaining > 0 && localSector < part.NumSectors)
                {
                    ct.ThrowIfCancellationRequested();

                    int runCount = GetContiguousUncompressedRun(
                        part, localSector, remaining, out long runOffset);

                    if (runCount > 0)
                    {
                        int runBytes = checked(runCount * XDVDFS.SECTOR_SIZE);
                        await ReadExactlyAtAsync(
                            part.Stream, 
                            buffer.Slice(outOffset, runBytes), 
                            runOffset, 
                            ct);

                        localSector += (uint)runCount;
                        globalSector += (uint)runCount;
                        remaining -= (uint)runCount;
                        outOffset += runBytes;
                        continue;
                    }

                    var cur = CCI.DecodeIndexEntry(
                        part.SectorIndex[localSector], 
                        part.Header.IndexAlignment);
                    var next = CCI.DecodeIndexEntry(
                        part.SectorIndex[localSector + 1], 
                        part.Header.IndexAlignment);

                    long size = next.offset - cur.offset;
                    bool treatAsCompressed = cur.compressed || size < XDVDFS.SECTOR_SIZE;
                    var dest = buffer.Slice(outOffset, XDVDFS.SECTOR_SIZE);

                    if (!treatAsCompressed)
                    {
                        if (size != XDVDFS.SECTOR_SIZE)
                            throw new InvalidOperationException(
                                $"Expected uncompressed sector to be {XDVDFS.SECTOR_SIZE} bytes, but got {size} bytes.");

                        await ReadExactlyAtAsync(
                            part.Stream, dest, cur.offset, ct);
                    }
                    else
                    {
                        if (size < 1 || size > XDVDFS.SECTOR_SIZE)
                        {
                            throw new InvalidOperationException(
                                $"Compressed sector size must be between 1 and {XDVDFS.SECTOR_SIZE} bytes, but got {size} bytes.");
                        }

                        int blockSize = checked((int)size);
                        await ReadExactlyAtAsync(
                            part.Stream, 
                            block.AsMemory(0, blockSize), 
                            cur.offset, 
                            ct);

                        int padLen = block[0];
                        int compressedSize = blockSize - 1 - padLen;

                        if (compressedSize <= 0)
                        {
                            throw new InvalidOperationException(
                                $"Invalid pad length {padLen} for compressed sector of size {blockSize} bytes.");
                        }

                        int decodedSize = LZ4Codec.Decode(
                            block.AsSpan(1, compressedSize), dest.Span);

                        if (decodedSize != XDVDFS.SECTOR_SIZE)
                        {
                            throw new InvalidOperationException(
                                $"Expected decompressed sector to be {XDVDFS.SECTOR_SIZE} bytes, but got {decodedSize} bytes.");
                        }
                    }

                    localSector++;
                    globalSector++;
                    remaining--;
                    outOffset += XDVDFS.SECTOR_SIZE;
                }
            }
        }
        finally
        {
            pool.Return(block);
        }
    }

    private (FilePart part, uint localSector) GetPartForSector(uint sector)
    {
        foreach (var part in FileParts)
        {
            if (sector < part.NumSectors)
                return (part, sector);

            sector -= part.NumSectors;
        }
        throw new ArgumentOutOfRangeException(
            nameof(sector), 
            "Sector is out of range for the total sectors in the image.");
    }

    private static int GetContiguousUncompressedRun(FilePart part, uint localSector, uint maxSectors, out long runOffset)
    {
        runOffset = 0;
        int count = 0;

        while (count < maxSectors && localSector + count < part.NumSectors)
        {
            uint currentSector = localSector + (uint)count;

            var curr = CCI.DecodeIndexEntry(
                part.SectorIndex[currentSector], 
                part.Header.IndexAlignment);
            var next = CCI.DecodeIndexEntry(
                part.SectorIndex[currentSector + 1], 
                part.Header.IndexAlignment);

            long size = next.offset - curr.offset;

            if (curr.compressed || size < XDVDFS.SECTOR_SIZE)
                break;

            if (size != XDVDFS.SECTOR_SIZE)
            {
                throw new InvalidOperationException(
                    $"Expected uncompressed sector to be {XDVDFS.SECTOR_SIZE} bytes, but got {size} bytes.");
            }

            if (count == 0)
            {
                runOffset = curr.offset;
            }
            else
            {
                long expectedOffset = runOffset + (long)count * XDVDFS.SECTOR_SIZE;

                if (curr.offset != expectedOffset)
                    break;
            }

            count++;
        }

        return count;
    }

    private static bool IsValidHeader(CCI.Header header) =>
        header.Magic == CCI.MAGIC &&
        header.BlockSize == XDVDFS.SECTOR_SIZE &&
        header.HeaderSize == CCI.Header.SIZE &&
        header.Version == CCI.VERSION;
}
