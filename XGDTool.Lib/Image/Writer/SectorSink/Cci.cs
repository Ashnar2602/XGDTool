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

    private record CompressedSector(bool Compressed, byte[] Data, int Length);

    private readonly IWriterOptions Options = options;
    private readonly Title.Info TitleInfo = titleInfo;
    private readonly List<FileEntry> OutFiles = new();
    private uint NextWriteSector = 0;
    private bool DirectoryCreated = false;
    private readonly SemaphoreSlim WriteLock = new(1, 1);
    private long TotalUncompressedSize = 0;
    private const int BufferSectors = 256;
    private FileEntry CurrentFile => OutFiles.Last();

    private string GetFilePath(int? index = null) => 
        Path.Join(
            Options.OutDirectory, 
            TitleInfo.FolderName, 
            $"{TitleInfo.ImageName}{(index.HasValue ? $".{index.Value + 1}" : "")}.cci");

    public Task Initialize(long totalOutSize, IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        var dir = Path.Join(Options.OutDirectory, TitleInfo.FolderName);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            DirectoryCreated = true;
        }

        OutFiles.Add(new FileEntry()
        {
            Stream = new FileStream(GetFilePath(), FileMode.Create, FileAccess.Write)
        });

        CurrentFile.Stream.Seek(CCI.HEADER_SIZE, SeekOrigin.Begin);
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
                await WritePaddingAsync(NextWriteSector, (int)(startSector - NextWriteSector), ct);

            await CompressAndWriteSectors(startSector, buffer, ct);
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
                await WritePaddingAsync(
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
                Directory.Delete(Path.Join(Options.OutDirectory, TitleInfo.FolderName), true);
            }
            catch { }
        }
    }

    private async Task WritePaddingAsync(uint startSector, int sectorCount, CancellationToken ct = default)
    {
        if (startSector != NextWriteSector)
            throw new Exception(
                $"Cannot write sector {startSector} before the current max written sector {NextWriteSector}.");

        Memory<byte> emptySectors = new byte[BufferSectors * XISO.SECTOR_SIZE];

        while (sectorCount > 0)
        {
            ct.ThrowIfCancellationRequested();

            int padSectors = (int)Math.Min(BufferSectors, sectorCount);

            await CompressAndWriteSectors(
                startSector,
                emptySectors.Slice(0, padSectors * XISO.SECTOR_SIZE),
                ct);

            startSector += (uint)padSectors;
            sectorCount -= padSectors;
        }
    }

    private async Task CompressAndWriteSectors(uint startSector, ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        if (startSector != NextWriteSector)
            throw new Exception(
                $"Cannot write sector {startSector} before the current max written sector {NextWriteSector}.");

        int sectorCount = buffer.Length / XISO.SECTOR_SIZE;
        var results = new CompressedSector[sectorCount];
        const int alignMult = 1 << CCI.INDEX_ALIGNMENT;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, sectorCount),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = ct
            },
            async (i, token) =>
            {
                var sector = buffer.Slice(i * XISO.SECTOR_SIZE, XISO.SECTOR_SIZE);

                int maxCompressedSize = LZ4Codec.MaximumOutputSize(XISO.SECTOR_SIZE);
                byte[] compressed = new byte[maxCompressedSize];

                int compressedSize = LZ4Codec.Encode(
                    sector.Span,
                    compressed.AsSpan(),
                    LZ4Level.L12_MAX);

                var isCompressed =
                    compressedSize > 0 &&
                    compressedSize < (XISO.SECTOR_SIZE - (4 + alignMult));

                results[i] = new CompressedSector
                (
                    isCompressed,
                    isCompressed
                        ? compressed.AsSpan(0, compressedSize).ToArray()
                        : sector.ToArray(),
                    compressedSize
                );

                await ValueTask.CompletedTask;
            });

        for (int i = 0; i < sectorCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            var result = results[i];
            byte padLen = 0;
            int writeLen = 0;

            if (result.Compressed)
            {
                padLen = (byte)
                    (((result.Data.Length + 1 + alignMult - 1) / alignMult * alignMult) -
                     (result.Data.Length + 1));
                writeLen = result.Data.Length + 1 + padLen;
            }
            else
            {
                writeLen = result.Data.Length;
            }

            uint currentPos = (uint)CurrentFile.Stream.Position;
            CurrentFile.IndexEntries.Add(CCI.EncodeIndexEntry(currentPos, result.Compressed));

            if (result.Compressed)
            {
                CurrentFile.Stream.WriteByte(padLen);
                CurrentFile.Stream.Write(result.Data);

                if (padLen > 0)
                    CurrentFile.Stream.Write(new byte[padLen]);
            }
            else
            {
                CurrentFile.Stream.Write(result.Data);
            }

            if (CurrentFile.Stream.Position > CCI.SPLIT_OFFSET)
            {
                if (OutFiles.Count == 1)
                {
                    OutFiles[0].Stream.Dispose();
                    var currentName = OutFiles[0].Stream.Name;
                    var newName = GetFilePath(0);
                    File.Move(currentName, newName);
                    OutFiles[0].Stream = new FileStream(newName, FileMode.Open, FileAccess.Write);
                    OutFiles[0].Stream.Seek(0, SeekOrigin.End);
                }

                OutFiles.Add(new FileEntry()
                {
                    Stream = new FileStream(GetFilePath(OutFiles.Count), FileMode.Create, FileAccess.Write)
                });

                if (CurrentFile.Stream.Position == 0)
                    CurrentFile.Stream.Seek(CCI.HEADER_SIZE, SeekOrigin.Begin);
            }
        }

        NextWriteSector = startSector + (uint)sectorCount;
    }
}
