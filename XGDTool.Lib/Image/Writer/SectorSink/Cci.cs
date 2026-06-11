using K4os.Compression.LZ4;
using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Image.Writer.SectorSink;

internal class Cci(IWriterOptions options, Title.Info titleInfo) : ISectorSink
{
    private class FileEntry
    {
        public List<uint> IndexEntries = new();
        public required FileStream Stream;
    }

    private struct CompressedSector
    {
        public byte[]? Data;
        public int CompressedSize;
        public byte PadLen;
    }

    private const int BufferSectors = 256;

    private readonly IWriterOptions Options = options;
    private readonly Title.Info TitleInfo = titleInfo;
    private readonly List<FileEntry> OutFiles = new();
    private uint NextWriteSector = 0;
    private bool DirectoryCreated = false;
    private readonly SemaphoreSlim WriteLock = new(1, 1);
    private long TotalUncompressedSize = 0;
    private FileEntry CurrentFile => OutFiles.Last();
    private readonly byte[] ZeroPad = new byte[1 << CCI.INDEX_ALIGNMENT];

    private string GetFilePath(int? index = null) => 
        Path.Join(
            Options.OutputDirectory, 
            TitleInfo.FolderName, 
            $"{TitleInfo.ImageName}{(index.HasValue ? $".{index.Value + 1}" : "")}.cci");

    public Task Initialize(long totalOutSize, IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        var dir = Path.Join(Options.OutputDirectory, TitleInfo.FolderName);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            DirectoryCreated = true;
        }

        OutFiles.Add(new FileEntry()
        {
            Stream = CreateOutputStream(GetFilePath())
        });
        TotalUncompressedSize = totalOutSize;
        return Task.CompletedTask;
    }

    public async Task WriteSectorsAsync(uint startSector, ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        if (startSector < NextWriteSector)
            throw new Exception(
                $"Cannot write sector {startSector} before the current max written sector {NextWriteSector}.");

        if (!XISO.IsSectorAligned(buffer.Length))
            throw new Exception($"Buffer length {buffer.Length} is not sector aligned.");

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
            if (XISO.SectorCount(TotalUncompressedSize) > NextWriteSector)
                WritePadSectors(
                    NextWriteSector, 
                    (int)(XISO.SectorCount(TotalUncompressedSize) - NextWriteSector), 
                    ct);

            foreach (var file in OutFiles)
            {
                file.Stream.Flush();
                file.Stream.Seek(0, SeekOrigin.End);

                ulong indexOffset = (ulong)file.Stream.Position;
                ulong uncompressedSize = (ulong)file.IndexEntries.Count * XISO.SECTOR_SIZE;

                file.IndexEntries.Add(CCI.EncodeIndexEntry((uint)file.Stream.Position, false));

                foreach (var indexEntry in file.IndexEntries)
                    file.Stream.Write(BitConverter.GetBytes(indexEntry));

                var header = new CCI.Header(uncompressedSize, indexOffset);
                file.Stream.Seek(0, SeekOrigin.Begin);
                file.Stream.Write(header.ToBytes());
                file.Stream.Dispose();
            }

            return OutFiles.Select(f => f.Stream.Name).ToList();
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public void CleanupCancelled()
    {
        OutFiles.ForEach(f => 
        { 
            try
            {
                f.Stream.Dispose();
                File.Delete(f.Stream.Name);
            }
            catch { }
        });

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

        Memory<byte> emptySectors = new byte[BufferSectors * XISO.SECTOR_SIZE];

        while (sectorCount > 0)
        {
            ct.ThrowIfCancellationRequested();

            int padSectors = Math.Min(BufferSectors, sectorCount);

            CompressAndWriteSectors(
                startSector,
                emptySectors.Slice(0, padSectors * XISO.SECTOR_SIZE),
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

        int sectorCount = buffer.Length / XISO.SECTOR_SIZE;
        var results = new CompressedSector[sectorCount];
        const int alignMult = 1 << CCI.INDEX_ALIGNMENT;

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
                var sector = buffer.Slice(i * XISO.SECTOR_SIZE, XISO.SECTOR_SIZE);

                int maxCompressedSize = LZ4Codec.MaximumOutputSize(XISO.SECTOR_SIZE);
                byte[]? rented = System.Buffers.ArrayPool<byte>.Shared.Rent(maxCompressedSize);

                try
                {
                    int compressedSize = LZ4Codec.Encode(
                        sector.Span,
                        rented.AsSpan(0, maxCompressedSize),
                        LZ4Level.L12_MAX);

                    var isCompressed =
                        compressedSize > 0 &&
                        compressedSize < (XISO.SECTOR_SIZE - (4 + alignMult));

                    if (!isCompressed)
                    {
                        System.Buffers.ArrayPool<byte>.Shared.Return(rented);
                        rented = null;
                        results[i] = default;
                        return;
                    }

                    byte padLen = (byte)
                        (((compressedSize + 1 + alignMult - 1) / alignMult * alignMult) -
                         (compressedSize + 1));

                    results[i].Data = rented;
                    results[i].CompressedSize = compressedSize;
                    results[i].PadLen = padLen;

                    rented = null;
                }
                finally
                {
                    if (rented != null)
                        System.Buffers.ArrayPool<byte>.Shared.Return(rented);
                }
            });

            for (int i = 0; i < sectorCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                var result = results[i];
                var sourceSector = buffer.Slice(i * XISO.SECTOR_SIZE, XISO.SECTOR_SIZE);

                long currentPos = CurrentFile.Stream.Position;
                CurrentFile.IndexEntries.Add(CCI.EncodeIndexEntry((uint)currentPos, result.Data != null));

                if (result.Data != null)
                {
                    CurrentFile.Stream.WriteByte(result.PadLen);
                    CurrentFile.Stream.Write(result.Data, 0, result.CompressedSize);

                    if (result.PadLen > 0)
                        CurrentFile.Stream.Write(ZeroPad, 0, result.PadLen);
                }
                else
                {
                    CurrentFile.Stream.Write(sourceSector.Span);
                }

                if (CurrentFile.Stream.Position > CCI.SPLIT_OFFSET)
                {
                    if (OutFiles.Count == 1)
                    {
                        var currentName = OutFiles[0].Stream.Name;
                        OutFiles[0].Stream.Dispose();

                        var newName = GetFilePath(0);
                        File.Move(currentName, newName);

                        OutFiles[0].Stream = new FileStream(newName, FileMode.Open, FileAccess.Write);
                        OutFiles[0].Stream.Seek(0, SeekOrigin.End);
                    }

                    OutFiles.Add(new FileEntry()
                    {
                        Stream = CreateOutputStream(GetFilePath(OutFiles.Count))
                    });
                }
            }
        }
        finally
        {
            foreach (var result in results)
            {
                if (result.Data != null)
                    System.Buffers.ArrayPool<byte>.Shared.Return(result.Data);
            }
        }

        NextWriteSector = startSector + (uint)sectorCount;
    }

    private static FileStream CreateOutputStream(string path)
    {
        var stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        stream.Seek(CCI.HEADER_SIZE, SeekOrigin.Begin);
        return stream;
    }
}
