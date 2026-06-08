using K4os.Compression.LZ4;
using XGDTool.Lib.Image.Format;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Image.Writer.SectorSink;

internal class Cci : ISectorSink
{
    private class FileEntry
    {
        public List<uint> IndexEntries = new();
        public required FileStream Stream;
    }

    private record CompressedSector(bool Compressed, byte[] Data, int Length);

    private readonly IWriterOptions Options;
    private readonly Title.Info TitleInfo;
    private List<FileEntry> OutFiles = new();
    private uint MaxWriteSector = 0;
    private bool DirectoryCreated = false;
    private FileEntry CurrentFile => OutFiles.Last();

    public Cci(IWriterOptions options, Title.Info titleInfo)
    {
        Options = options;
        TitleInfo = titleInfo;
        OutFiles.Add(new FileEntry() 
        { 
            Stream = new FileStream(GetFilePath(), FileMode.Create, FileAccess.Write)
        });
    }

    private string GetFilePath(int? index = null) => 
        Path.Join(
            Options.OutDirectory, 
            TitleInfo.FolderName, 
            $"{TitleInfo.ImageName}{(index.HasValue ? $".{index.Value + 1}" : "")}.cci");

    public Task Initialize(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        var dir = Path.Join(Options.OutDirectory, TitleInfo.FolderName);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            DirectoryCreated = true;
        }

        return Task.CompletedTask;
    }

    public async Task WriteSectorsAsync(uint startSector, ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        if (startSector < MaxWriteSector)
            throw new Exception(
                $"Cannot write sector {startSector} before the current max written sector {MaxWriteSector}.");

        if (!XISO.IsSectorAligned(buffer.Length))
            throw new Exception($"Buffer length {buffer.Length} is not sector aligned.");

        if (startSector > MaxWriteSector + 1)
        {
            var padSectors = (int)Math.Min(512, startSector - MaxWriteSector);
            var padBuffer = new byte[padSectors * XISO.SECTOR_SIZE];

            await WriteSectorsAsync(MaxWriteSector, padBuffer, ct);
        }

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
                    compressed.AsSpan());

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

            if (CurrentFile.Stream.Length + writeLen > CCI.SPLIT_OFFSET)
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
            }

            // Reserve space for header
            if (CurrentFile.Stream.Position == 0)
                CurrentFile.Stream.Seek(CCI.HEADER_SIZE, SeekOrigin.Begin);

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
        }

        MaxWriteSector = startSector + (uint)sectorCount;
    }

    public Task<List<string>> FinalizeImage(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        foreach (var file in OutFiles)
        {
            ulong indexOffset = (ulong)file.Stream.Length;
            ulong uncompressedSize = (ulong)file.IndexEntries.Count * XISO.SECTOR_SIZE;
            file.IndexEntries.Add((uint)CurrentFile.Stream.Position >> CCI.INDEX_ALIGNMENT);

            foreach (var indexEntry in file.IndexEntries)
                file.Stream.Write(BitConverter.GetBytes(indexEntry));

            var header = new CCI.Header(uncompressedSize, indexOffset);
            file.Stream.Seek(0, SeekOrigin.Begin);
            file.Stream.Write(header.ToBytes());
            file.Stream.Dispose();
        }

        return Task.FromResult(OutFiles.Select(f => f.Stream.Name).ToList());
    }

    public void CleanupCancelled()
    {
        foreach (var file in OutFiles)
        {
            try
            {
                file.Stream.Dispose();
                File.Delete(file.Stream.Name);
            }
            catch { }
        }

        if (DirectoryCreated)
        {
            try
            {
                Directory.Delete(Path.Join(Options.OutDirectory, TitleInfo.FolderName));
            }
            catch { }
        }
    }
}
