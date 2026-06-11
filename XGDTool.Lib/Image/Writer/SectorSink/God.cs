using System.Runtime.CompilerServices;
using System.Text;
using System.Security.Cryptography;
using XGDTool.Lib.Exe;
using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Image.Writer.SectorSink;

internal class God(IWriterOptions options, Title.Info titleInfo) : ISectorSink
{
    private readonly IWriterOptions Options = options;
    private readonly Title.Info TitleInfo = titleInfo;
    private readonly List<FileStream> Streams = new();
    private readonly SemaphoreSlim WriteLock = new(1, 1);

    private string PlatformString => TitleInfo.Platform switch
        {
            Platform.Xbox => GOD.Type.OriginalXbox.ToString("X8"),
            Platform.Xbox360 => GOD.Type.GamesOnDemand.ToString("X8"),
            _ => throw new InvalidOperationException(
                $"Unsupported platform: {TitleInfo.Platform}")
        };

    private string GodFolderPath => Path.Join(Options.OutputDirectory, TitleInfo.GodFolderName);
    private string OutDataDirectory => Path.Join(GodFolderPath, PlatformString, TitleInfo.GodUniqueName + ".data");
    private string LiveHeaderPath => Path.Join(GodFolderPath, PlatformString, TitleInfo.GodUniqueName);

    public Task Initialize(long totalOutSize, IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        if (!Directory.Exists(OutDataDirectory))
            Directory.CreateDirectory(OutDataDirectory);

        if (!Directory.Exists(GodFolderPath))
            throw new DirectoryNotFoundException(
                $"Expected directory not found: {GodFolderPath}");

        return Task.CompletedTask;
    }

    public async Task WriteSectorsAsync(uint startSector, ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        if (!XISO.IsSectorAligned(buffer.Length))
            throw new ArgumentException(
                $"Buffer length must be a multiple of {XISO.SECTOR_SIZE}", nameof(buffer));

        await WriteLock.WaitAsync(ct);
        try
        {
            var (stream, offset) = RemapSector(startSector);

            stream.Seek(offset, SeekOrigin.Begin);
            await stream.WriteAsync(buffer.Slice(0, XISO.SECTOR_SIZE), ct);

            if (buffer.Length > XISO.SECTOR_SIZE)
                await WriteSectorsAsync(
                    startSector + 1,
                    buffer.Slice(XISO.SECTOR_SIZE),
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
            foreach (var stream in Streams)
            {
                if (!GOD.IsBlockAligned(stream.Length))
                {
                    stream.Seek(0, SeekOrigin.End);
                    var paddingSize = (int)(GOD.AlignUpToBlock(stream.Length) - stream.Length);

                    if (paddingSize > 0)
                        stream.Write(new byte[paddingSize], 0, paddingSize);
                }
            }

            long tableCount = Streams.Aggregate(0L, (acc, s) =>
                checked(acc + GOD.SubHashTableCount(s.Length) + 1));
            tableCount += Streams.Count; // master hash tables
            tableCount++; // Just add one to represent the live header so we're not showing 100% until it's written

            var progData = new Converter.Progress
            {
                Stage = Converter.Stage.Finalizing,
                Current = 0,
                Total = tableCount
            };

            WriteSubHashTables(ref progData, progress, ct);
            var finalMhtHash = FinalizeHashTables(ref progData, progress, ct);

            WriteLiveHeader(finalMhtHash);

            progData.Current = progData.Total;
            progress?.Report(progData);

            return new List<string> { GodFolderPath };
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public void CleanupCancelled()
    {
        Streams.ForEach(s => { s.Dispose(); File.Delete(s.Name); });
        Streams.Clear();

        if (File.Exists(LiveHeaderPath))
            File.Delete(LiveHeaderPath);

        if (Directory.Exists(GodFolderPath))
            Directory.Delete(GodFolderPath, true);
    }

    private void WriteSubHashTables(ref Converter.Progress progData, IProgress<Converter.Progress>? progress, CancellationToken ct)
    {
        foreach (var stream in Streams)
        {
            ct.ThrowIfCancellationRequested();

            if (!GOD.IsBlockAligned(stream.Length))
                throw new InvalidOperationException(
                    $"Stream length must be a multiple of {GOD.BLOCK_SIZE}: {stream.Name}");

            var blocksRemaining = GOD.AlignUpToBlock(stream.Length);
            var subHashTableCount = GOD.SubHashTableCount(stream.Length);

            var masterHashTable = new byte[subHashTableCount * SHA1.HashSizeInBytes];

            stream.Seek(GOD.BLOCK_SIZE, SeekOrigin.Begin);

            --blocksRemaining;

            for (int i = 0; i < subHashTableCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                var blocksInSht = 0;
                var blockBuffer = new byte[GOD.BLOCK_SIZE];
                var subhashTable = new byte[GOD.DATA_BLOCKS_PER_SHT * SHA1.HashSizeInBytes];

                stream.Seek(GOD.BLOCK_SIZE, SeekOrigin.Current);

                --blocksRemaining;

                while (blocksInSht < GOD.DATA_BLOCKS_PER_SHT && 0 < blocksRemaining)
                {
                    ct.ThrowIfCancellationRequested();

                    var bytesRead = stream.Read(blockBuffer, 0, blockBuffer.Length);

                    if (bytesRead != blockBuffer.Length)
                    {
                        throw new InvalidOperationException(
                            $"Expected to read {blockBuffer.Length} bytes, but only read {bytesRead} bytes.");
                    }

                    ReadOnlySpan<byte> blockData = blockBuffer.AsSpan(0, bytesRead);

                    var ret = SHA1.TryHashData(
                        blockData, 
                        subhashTable.AsSpan(blocksInSht * SHA1.HashSizeInBytes, SHA1.HashSizeInBytes), 
                        out int written);

                    if (!ret || written != SHA1.HashSizeInBytes)
                        throw new InvalidOperationException("Failed to compute hash for block.");

                    blocksInSht++;
                    --blocksRemaining;
                }

                var pos = stream.Position;

                stream.Seek(
                    (i * (GOD.DATA_BLOCKS_PER_SHT + 1) * GOD.BLOCK_SIZE) + GOD.BLOCK_SIZE,
                    SeekOrigin.Begin);

                stream.Write(subhashTable, 0, subhashTable.Length);

                progData.Current++;
                progress?.Report(progData);

                var masterHash = SHA1.HashData(subhashTable);
                masterHash.CopyTo(masterHashTable, i * SHA1.HashSizeInBytes);

                if (blocksRemaining == 0)
                    break;
            }

            stream.Seek(0, SeekOrigin.Begin);
            stream.Write(masterHashTable, 0, masterHashTable.Length);

            progData.Current++;
            progress?.Report(progData);
        }
    }

    private byte[] FinalizeHashTables(ref Converter.Progress progData, IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        var finalMhtHash = new byte[SHA1.HashSizeInBytes];
        var currHash = new byte[SHA1.HashSizeInBytes];
        var blockBuffer = new byte[GOD.BLOCK_SIZE];

        for (int i = Streams.Count - 1; i > 0; i--)
        {
            ct.ThrowIfCancellationRequested();

            var currStream = Streams[i];
            var prevStream = Streams[i - 1];

            currStream.Seek(0, SeekOrigin.Begin);
            currStream.Read(blockBuffer, 0, blockBuffer.Length);

            if (!SHA1.TryHashData(blockBuffer, currHash, out int written) ||
                written != SHA1.HashSizeInBytes)
            {
                throw new InvalidOperationException(
                    "Failed to compute hash for master hash table.");
            }

            prevStream.Seek(SHA1.HashSizeInBytes * GOD.SHT_PER_MHT, SeekOrigin.Begin);
            prevStream.Write(currHash, 0, currHash.Length);

            if (i == 1)
            {
                var lastMht = new byte[GOD.BLOCK_SIZE];
                prevStream.Seek(0, SeekOrigin.Begin);
                prevStream.Read(lastMht, 0, lastMht.Length);

                if (!SHA1.TryHashData(lastMht, finalMhtHash, out written) ||
                    written != SHA1.HashSizeInBytes)
                {
                    throw new InvalidOperationException(
                        "Failed to compute hash for final master hash table.");
                }
            }

            progData.Current++;
            progress?.Report(progData);
        }

        return finalMhtHash;
    }

    private void WriteLiveHeader(byte[] finalMhtHash)
    {
        var headerBuf = GOD.GetLiveHeaderTemplate();
        using var ms = new MemoryStream(headerBuf, writable: true);

        {
            var xexInfo = TitleInfo.XexExecutionInfo;

            ms.Seek(0x354, SeekOrigin.Begin);
            ms.Write(xexInfo.ToBytes(), 0, xexInfo.Size());

            //ms.Write(BitConverter.GetBytes(Bits.ToBig(xexInfo.MediaId)), 0, 4);
            ////ms.Seek(0x360, SeekOrigin.Begin);
            //ms.Write(BitConverter.GetBytes(Bits.ToBig(xexInfo.Version)), 0, 4);
            //ms.Write(BitConverter.GetBytes(Bits.ToBig(xexInfo.BaseVersion)), 0, 4);
            //ms.Write(BitConverter.GetBytes(Bits.ToBig(xexInfo.TitleId)), 0, 4);
            //ms.WriteByte(xexInfo.Platform);
            //ms.WriteByte(xexInfo.ExecutableType);
            //ms.WriteByte(xexInfo.DiscNumber);
            //ms.WriteByte(xexInfo.DiscCount);

            ulong totalSize = Streams.Aggregate(0UL, (acc, s) => checked(acc + (ulong)s.Length));
            uint partsWrittenSize = (uint)(totalSize / 0x100);
            uint partCount = (uint)Streams.Count;
            uint contentType =
                (TitleInfo.Platform == Platform.Xbox360)
                    ? (uint)GOD.Type.GamesOnDemand
                    : (uint)GOD.Type.OriginalXbox;

            ms.Seek(0x344, SeekOrigin.Begin);
            ms.Write(BitConverter.GetBytes(Bits.ToBig(contentType)), 0, 4);

            ms.Seek(0x37D, SeekOrigin.Begin);
            ms.Write(finalMhtHash, 0, finalMhtHash.Length);

            ms.Seek(0x3A0, SeekOrigin.Begin);
            ms.Write(BitConverter.GetBytes(partCount), 0, 4);
            ms.Write(BitConverter.GetBytes(Bits.ToBig(partsWrittenSize)), 0, 4);
        }
        {
            int titleNameSize = Encoding.Unicode.GetByteCount(TitleInfo.TitleName);
            titleNameSize = Math.Min(titleNameSize, XEX.TITLE_NAME_MAX_LENGTH);

            var titleNameBytes = new byte[titleNameSize];
            Encoding.Unicode.GetBytes(
                TitleInfo.TitleName, 
                0, 
                TitleInfo.TitleName.Length, 
                titleNameBytes,
                0);

            ms.Seek(0x411 + 1, SeekOrigin.Begin);
            ms.Write(titleNameBytes, 0, titleNameSize);
            ms.Seek(0x1691 + 1, SeekOrigin.Begin);
            ms.Write(titleNameBytes, 0, titleNameSize);
        }
        if (TitleInfo.TitleIconData != null && TitleInfo.TitleIconData.Length > 0)
        {
            if (TitleInfo.TitleIconData.Length > headerBuf.Length - 0x171A ||
                TitleInfo.TitleIconData.Length > headerBuf.Length - 0x571a)
            {
                // TODO: this isn't totally safe, calculate max image bounds
                throw new InvalidOperationException(
                    $"Title icon data is too large: {TitleInfo.TitleIconData.Length} bytes.");
            }

            uint titleIconSize = Bits.ToBig((uint)TitleInfo.TitleIconData.Length);

            ms.Seek(0x1712, SeekOrigin.Begin);
            ms.Write(BitConverter.GetBytes(titleIconSize), 0, 4);
            ms.Seek(0x1716, SeekOrigin.Begin);
            ms.Write(BitConverter.GetBytes(titleIconSize), 0, 4);

            ms.Seek(0x171A, SeekOrigin.Begin);
            ms.Write(TitleInfo.TitleIconData, 0, TitleInfo.TitleIconData.Length);

            ms.Seek(0x571a, SeekOrigin.Begin);
            ms.Write(TitleInfo.TitleIconData, 0, TitleInfo.TitleIconData.Length);
        }
        {
            var headerHash = new byte[SHA1.HashSizeInBytes];
            var headerData = headerBuf.AsSpan(0x344, headerBuf.Length - 0x344);

            if (!SHA1.TryHashData(headerData, headerHash, out int written) ||
                written != SHA1.HashSizeInBytes)
            {
                throw new InvalidOperationException(
                    "Failed to compute hash for live header.");
            }

            ms.Seek(0x32C, SeekOrigin.Begin);
            ms.Write(headerHash, 0, SHA1.HashSizeInBytes);
        }

        var f = new FileStream(LiveHeaderPath, FileMode.Create, FileAccess.Write);
        f.Write(headerBuf, 0, headerBuf.Length);
        f.Close();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (FileStream stream, long offset) RemapSector(uint sector)
    {
        long blockNum = (sector * XISO.SECTOR_SIZE) / GOD.BLOCK_SIZE;
        int fileIndex = (int)(blockNum / GOD.BLOCKS_PER_PART);
        int dataBlockInFile = (int)(blockNum % GOD.BLOCKS_PER_PART);
        int hashIndex = dataBlockInFile / GOD.DATA_BLOCKS_PER_SHT;

        long newOffset = GOD.BLOCK_SIZE;
        newOffset += ((hashIndex + 1) * GOD.BLOCK_SIZE);
        newOffset += (dataBlockInFile * GOD.BLOCK_SIZE);
        newOffset += (sector * XISO.SECTOR_SIZE) % GOD.BLOCK_SIZE;

        return (GetOrCreateFile(fileIndex), newOffset);
    }

    private FileStream GetOrCreateFile(int index)
    {
        if (index < Streams.Count)
            return Streams[index];

        for (int i = Streams.Count; i <= index; i++)
        {
            var path = GetFilePartPath(i);
            var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);

            if (i < index)
                stream.SetLength(GOD.BLOCKS_PER_PART * GOD.BLOCK_SIZE);

            Streams.Add(stream);
        }

        return Streams[index];
    }

    private string GetFilePartPath(int index)
    {
        string s = index.ToString("D4");
        return Path.Join(OutDataDirectory, $"Data{s}");
    }
}
