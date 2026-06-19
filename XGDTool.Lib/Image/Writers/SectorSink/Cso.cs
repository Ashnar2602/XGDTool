using System.Diagnostics;
using System.Buffers;
using System.Buffers.Binary;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using K4os.Compression.LZ4.Encoders;
using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Image.Writers.SectorSink;

internal class Cso(IWriterOptions options, Title.Info titleInfo) : ISectorSink
{
    private struct CompressedSector
    {
        public byte[]? Buffer;
        public int CompressedSize;
    }

    private readonly IWriterOptions Options = options;
    private readonly Title.Info TitleInfo = titleInfo;
    private readonly List<FileStream> FileStreams = [];
    private FileStream CurrentFile => FileStreams[^1];
    private List<uint> IndexEntries = [];
    private bool DirectoryCreated = false;
    private uint NextWriteSector = 0;
    private const int AlignMult = 1 << CSO.INDEX_ALIGNMENT;
    private const int AlignMask = AlignMult - 1;
    private const int BufferSectors = 256;
    private const int LZ4FHeaderSize = 7;
    private const int LZ4FFooterSize = 4;
    private readonly byte[] ZeroPadding = new byte[AlignMult];
    private readonly SemaphoreSlim WriteLock = new(1, 1);
    private long TotalUncompressedSize;
    private readonly LZ4EncoderSettings FrameSettings = new()
    {
        ContentLength = null,
        ChainBlocks = false,
        CompressionLevel = LZ4Level.L12_MAX,
        BlockSize = 65536,
        ContentChecksum = false,
        BlockChecksum = false
    };

    public Task Initialize(long totalOutSize, IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        var dir = Path.Join(Options.OutputDirectory, TitleInfo.FolderName);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            DirectoryCreated = true;
        }

        FileStreams.Clear();
        FileStreams.Add(CreateStream(GetFilePath()));
        TotalUncompressedSize = totalOutSize;
        var indexCount = XDVDFS.SectorCount(TotalUncompressedSize) + 1;
        IndexEntries = new List<uint>(checked((int)indexCount));

        var header = new CSO.Header((ulong)TotalUncompressedSize);
        CurrentFile.Write(header.Serialize());
        CurrentFile.Seek(CSO.Header.SIZE + (indexCount * sizeof(uint)), SeekOrigin.Begin);
        return Task.CompletedTask;
    }

    public async Task WriteSectorsAsync(uint startSector, ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        if (startSector < NextWriteSector)
            throw new Exception(
                $"Cannot write sector {startSector} before the current max written sector {NextWriteSector}.");

        await WriteLock.WaitAsync(ct);
        try
        {
            if (startSector > NextWriteSector)
                WritePadSectors(NextWriteSector, (int)(startSector - NextWriteSector), ct);

            CompressAndWriteSectors(startSector, buffer, ct);
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
            if (XDVDFS.SectorCount(TotalUncompressedSize) > NextWriteSector)
                WritePadSectors(
                    NextWriteSector, 
                    checked((int)(XDVDFS.SectorCount(TotalUncompressedSize) - NextWriteSector)), 
                    ct);

            Debug.Assert(XDVDFS.SectorCount(TotalUncompressedSize) == NextWriteSector);
            Debug.Assert(IndexEntries.Count == NextWriteSector);

            CurrentFile.Flush();
            // Last index isn't aligned properly, per the original cso tool, just shift raw value
            // this might be a bug in the original code, but we're shooting for 100% binary parity
            IndexEntries.Add(checked((uint)CurrentFile.Position) >> CSO.INDEX_ALIGNMENT);

            {
                var firstStream = FileStreams[0];
                firstStream.Seek(CSO.Header.SIZE, SeekOrigin.Begin);

                foreach (var indexEntry in IndexEntries)
                    firstStream.Write(BitConverter.GetBytes(indexEntry));
            }

            foreach (var stream in FileStreams)
            {
                stream.Seek(0, SeekOrigin.End);
                if (stream.Length % CSO.FILE_MODULUS != 0)
                {
                    var paddingSize = (int)(CSO.FILE_MODULUS - (stream.Length % CSO.FILE_MODULUS));
                    stream.Write(new byte[paddingSize], 0, paddingSize);
                }
                stream.Flush();
            }

            return [..FileStreams.Select(f => f.Name)];
        }
        finally
        {
            FileStreams.ForEach(f => f.Dispose());
            FileStreams.Clear();
            WriteLock.Release();
        }
    }

    public void CleanupCancelled()
    {
        FileStreams.ForEach(f => 
        { 
            try
            {
                f.Dispose();
                File.Delete(f.Name);
            }
            catch { }
        });
        FileStreams.Clear();

        if (DirectoryCreated)
        {
            try
            {
                Directory.Delete(Path.Join(Options.OutputDirectory, TitleInfo.FolderName), true);
            }
            catch { }
        }
    }

    private void WritePadSectors(uint startSector, int sectorCount, CancellationToken ct = default)
    {
        if (startSector != NextWriteSector)
            throw new Exception(
                $"Cannot write sector {startSector} before the current max written sector {NextWriteSector}.");

        Memory<byte> emptySectors = new byte[BufferSectors * XDVDFS.SECTOR_SIZE];

        while (sectorCount > 0)
        {
            ct.ThrowIfCancellationRequested();

            int padSectors = Math.Min(BufferSectors, sectorCount);

            CompressAndWriteSectors(
                startSector,
                emptySectors.Slice(0, padSectors * XDVDFS.SECTOR_SIZE),
                ct);

            startSector += (uint)padSectors;
            sectorCount -= padSectors;
        }
    }

    private void CompressAndWriteSectors(uint startSector, ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        if (startSector != NextWriteSector)
            throw new Exception(
                $"Cannot write sector {startSector} before the current max written sector {NextWriteSector}.");

        if (!XDVDFS.IsSectorAligned(buffer.Length))
            throw new Exception($"Buffer length {buffer.Length} is not sector aligned.");

        int sectorCount = checked((int)XDVDFS.SectorCount(buffer.Length));
        var results = new CompressedSector[sectorCount];

        try
        {
            Parallel.For(
            0, 
            sectorCount,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = ct
            },
            i =>
            {
                var sector = buffer.Slice(i * XDVDFS.SECTOR_SIZE, XDVDFS.SECTOR_SIZE);
                int maxCompressedSize = 
                    LZ4Codec.MaximumOutputSize(XDVDFS.SECTOR_SIZE) + LZ4FHeaderSize + 1 + LZ4FFooterSize;
                byte[]? rented = ArrayPool<byte>.Shared.Rent(maxCompressedSize);

                try
                {
                    using var ms = new MemoryStream(rented, 0, maxCompressedSize);
                    using (var lz4Stream = LZ4Stream.Encode(ms, FrameSettings, leaveOpen: true))
                    {
                        lz4Stream.Write(sector.Span);
                    }

                    int compressedSize = checked((int)ms.Position);
                    compressedSize -= LZ4FHeaderSize + LZ4FFooterSize;

                    var isCompressed =
                        compressedSize > 0 &&
                        (compressedSize + 12) < XDVDFS.SECTOR_SIZE;
                        
                    if (!isCompressed)
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                        rented = null;
                        results[i] = default;
                        return;
                    }

                    results[i].Buffer = rented;
                    results[i].CompressedSize = compressedSize;

                    rented = null;
                }
                finally
                {
                    if (rented != null)
                        ArrayPool<byte>.Shared.Return(rented);
                }
            });

            for (int i = 0; i < sectorCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                var result = results[i];
                var sourceSector = buffer.Slice(i * XDVDFS.SECTOR_SIZE, XDVDFS.SECTOR_SIZE);
                var currentPos = CurrentFile.Position;

                if (currentPos > CSO.SPLIT_OFFSET)
                {
                    if (FileStreams.Count == 1)
                    {
                        var currentName = FileStreams[0].Name;
                        FileStreams[0].Flush();
                        FileStreams[0].Dispose();

                        var newName = GetFilePath(0);
                        File.Move(currentName, newName);

                        FileStreams[0] = new FileStream(newName, FileMode.Open, FileAccess.Write);
                        FileStreams[0].Seek(0, SeekOrigin.End);
                    }

                    FileStreams.Add(CreateStream(GetFilePath(FileStreams.Count)));
                    currentPos = CurrentFile.Position;
                }

                if ((currentPos & AlignMask) != 0)
                {
                    var paddingSize = (int)(AlignMult - (currentPos & AlignMask));
                    CurrentFile.Write(ZeroPadding, 0, paddingSize);
                    currentPos += paddingSize;
                }

                IndexEntries.Add(CSO.EncodeIndexEntry(checked((uint)currentPos), result.Buffer != null));

                if (result.Buffer != null)
                    CurrentFile.Write(result.Buffer.AsSpan(LZ4FHeaderSize, result.CompressedSize));
                else 
                    CurrentFile.Write(sourceSector.Span);
            }
        }
        finally
        {
            foreach (var result in results)
            {
                if (result.Buffer != null)
                    ArrayPool<byte>.Shared.Return(result.Buffer);
            }
        }

        NextWriteSector = startSector + (uint)sectorCount;
    }

    private string GetFilePath(int? index = null) => 
        Path.Join(
            Options.OutputDirectory, 
            TitleInfo.FolderName, 
            $"{TitleInfo.ImageName}{(index.HasValue ? $".{index.Value + 1}" : "")}.cso");

    private static FileStream CreateStream(string path) => new(
        path,
        FileMode.Create,
        FileAccess.Write,
        FileShare.None,
        bufferSize: 1024 * 1024,
        options: FileOptions.Asynchronous | FileOptions.SequentialScan);
}
