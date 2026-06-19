using System.Buffers;
using System.Buffers.Binary;
using K4os.Compression.LZ4.Streams;
using K4os.Compression.LZ4;
using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Image.Readers;

internal class Cso(IReadOnlyList<string> files) : Base(files)
{
    private class FileParts
    {
        public readonly List<uint> IndexEntries = [];
        public uint SectorsInFile => (uint)IndexEntries.Count - 1;
        public required FileStream Stream;
    }

    private readonly CSO.Header Header = new();
    private readonly List<FileParts> CsoFiles = [];

    public override Format ImageFormat => Format.CSO;
    public override uint TotalSectors => XDVDFS.SectorCount(checked((long)Header.UncompressedSize));

    public static bool IsValid(string path)
    {
        try 
        {
            var headerBuf = new byte[CSO.Header.SIZE];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            stream.ReadExactly(headerBuf);

            return CSO.IsHeaderValid(ISerializable.Deserialize<CSO.Header>(headerBuf), stream.Length);
        }
        catch (Exception)
        {
            return false;
        }
    }

    protected override Task InitializeType(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        if (FilePaths.Count == 0)
            throw new InvalidDataException("No files provided for CSO reader.");
            
        foreach (var path in FilePaths)
        {
            CsoFiles.Add(new FileParts
            {
                Stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
            });
        }

        var headerBuf = new byte[CSO.Header.SIZE];
        var firstStream = CsoFiles[0].Stream;
        firstStream.ReadExactly(headerBuf);
        Header.Deserialize(headerBuf);

        if (!CSO.IsHeaderValid(Header, firstStream.Length))
            throw new InvalidDataException($"File {FilePaths[0]} does not have a valid CSO header.");

        var indexCount = checked((int)TotalSectors);
        var indexBuf = new byte[indexCount * sizeof(uint)];
        var currFileIdx = 0;
        firstStream.ReadExactly(indexBuf);

        var progData = new Converter.Progress()
        {
            Stage = Converter.Stage.Initializing,
            Current = 0,
            Total = indexCount
        };

        for (var i = 0; i < indexCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            var indexEntry = BitConverter.ToUInt32(indexBuf, i * sizeof(uint));
            var offset = CSO.DecodeIndexEntry(indexEntry, Header.IndexAlignment).offset;
            var lastOffset = CsoFiles[currFileIdx].IndexEntries.Count > 0
                ? CSO.DecodeIndexEntry(CsoFiles[currFileIdx].IndexEntries[^1]).offset
                : 0u;

            if (offset < lastOffset) 
            {
                var currFile = CsoFiles[currFileIdx];
                var lastSize = checked((uint)Math.Min(XDVDFS.SECTOR_SIZE, currFile.Stream.Length - lastOffset));
                currFile.IndexEntries.Add(
                    CSO.EncodeIndexEntry(
                        lastOffset + lastSize, 
                        false, 
                        Header.IndexAlignment
                    ));
                lastOffset = 0;
                currFileIdx++;
            }

            if (currFileIdx >= CsoFiles.Count)
                throw new InvalidDataException("Not enough CSO files provided for index entries.");

            CsoFiles[currFileIdx].IndexEntries.Add(indexEntry);

            if (i == indexCount - 1)
            {
                var currFile = CsoFiles[currFileIdx];
                var lastSize = checked((uint)Math.Min(XDVDFS.SECTOR_SIZE, currFile.Stream.Length - lastOffset));
                currFile.IndexEntries.Add(
                    CSO.EncodeIndexEntry(
                        lastOffset + lastSize, 
                        false, 
                        Header.IndexAlignment
                    ));
            }

            progData.Current = i + 1;
            progress?.Report(progData);
        }

        return Task.CompletedTask;
    }

    public override void ReadSectors(uint startSector, Span<byte> buffer, CancellationToken ct = default)
    {
        if (!XDVDFS.IsSectorAligned(buffer.Length))
            throw new ArgumentException("Buffer length must be a multiple of sector size.", nameof(buffer));

        uint remaining = XDVDFS.SectorCount(buffer.Length);
        uint globalSector = startSector;
        int outOffset = 0;
        var pool = ArrayPool<byte>.Shared;
        byte[] block = pool.Rent(XDVDFS.SECTOR_SIZE);

        try
        {
            while (remaining > 0)
            {
                var (part, localSector) = GetPartForSector(globalSector);

                while (remaining > 0 && localSector < part.SectorsInFile)
                {
                    ct.ThrowIfCancellationRequested();

                    var (offset, compressed) = CSO.DecodeIndexEntry(
                        part.IndexEntries[(int)localSector], 
                        Header.IndexAlignment);

                    var nextOffset = CSO.DecodeIndexEntry(
                        part.IndexEntries[(int)localSector + 1], 
                        Header.IndexAlignment).offset;

                    long size = nextOffset - offset;

                    if (!compressed)
                    {
                        if (size != XDVDFS.SECTOR_SIZE)
                        {
                            throw new InvalidDataException(
                                $"Uncompressed block at sector {globalSector} has invalid size {size}.");
                        }

                        int runCount = 1;
                        while (runCount < remaining && localSector + runCount < part.SectorsInFile)
                        {
                            var (localOffset, localCompressed) = CSO.DecodeIndexEntry(
                                part.IndexEntries[(int)(localSector + runCount)], 
                                Header.IndexAlignment);

                            var localNextOffset = CSO.DecodeIndexEntry(
                                part.IndexEntries[(int)(localSector + runCount + 1)], 
                                Header.IndexAlignment).offset;

                            if (localCompressed || localNextOffset - localOffset != XDVDFS.SECTOR_SIZE) 
                                break;

                            runCount++;
                        }

                        int runBytes = runCount * XDVDFS.SECTOR_SIZE;
                        ReadExactlyAt(part.Stream, buffer.Slice(outOffset, runBytes), offset);
                        localSector += (uint)runCount;
                        globalSector += (uint)runCount;
                        remaining -= (uint)runCount;
                        outOffset += runBytes;
                    }
                    else
                    {
                        int blockSize = checked((int)size);
                        ReadExactlyAt(part.Stream, block.AsSpan(0, blockSize), offset);
                        uint compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(0, sizeof(uint)));

                        if (compressedSize > blockSize - sizeof(uint))
                        {
                            throw new InvalidDataException(
                                $"Compressed block at sector {globalSector} has invalid compressed size.");
                        }

                        int decodedSize = LZ4Codec.Decode(
                            block.AsSpan(sizeof(uint), (int)compressedSize),
                            buffer.Slice(outOffset, XDVDFS.SECTOR_SIZE));

                        if (decodedSize != XDVDFS.SECTOR_SIZE)
                        {
                            throw new InvalidDataException(
                                $"Decompressed block at sector {globalSector} has invalid size {decodedSize}.");
                        }

                        localSector++;
                        globalSector++;
                        remaining--;
                        outOffset += XDVDFS.SECTOR_SIZE;
                    }
                }
            }
        }
        finally
        {
            pool.Return(block);
        }
    }

    // public override async Task ReadSectorsAsync(uint startSector, Memory<byte> buffer, CancellationToken ct = default)
    // {
    // }

    private (FileParts part, uint sector) GetPartForSector(uint sector)
    {
        foreach (var csoFile in CsoFiles)
        {
            if (csoFile.SectorsInFile > sector)
                return (csoFile, sector);

            sector -= csoFile.SectorsInFile;
        }

        throw new ArgumentOutOfRangeException(nameof(sector), $"Sector {sector} is out of range of the CSO files.");
    }
}
