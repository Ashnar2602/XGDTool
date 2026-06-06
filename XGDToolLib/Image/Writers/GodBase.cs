using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Exe;
using XGDToolLib.Image.Format;
using XGDToolLib.Util;
using static XGDToolLib.Image.Writer;

namespace XGDToolLib.Image.Writers;

public abstract class GodBase(Reader reader, Options options, Title.Info titleInfo)
    : Writer(reader, options, titleInfo)
{
    protected struct Remap
    {
        public long Offset;
        public int FileIndex;
    }

    protected class FilePart
    {
        public required FileStream Stream;
        public required string Path;
    }

    protected List<FilePart> FileParts = new();
    protected Converter.Progress ProgData = new();

    protected string PlatformString => TitleInfo.Platform switch
    {
        Platform.OriginalXbox => GOD.Type.OriginalXbox.ToString("X8"),
        Platform.Xbox360 => GOD.Type.GamesOnDemand.ToString("X8"),
        _ => throw new InvalidOperationException(
            $"Unsupported platform: {TitleInfo.Platform}")
    };

    protected string OutDataDirectory => Path.Join(
        OutOptions.OutDirectory,
        TitleInfo.GodFolderName,
        PlatformString,
        TitleInfo.GodUniqueName + ".data");

    protected string LiveHeaderPath => Path.Join(
        OutOptions.OutDirectory,
        TitleInfo.GodFolderName,
        PlatformString,
        TitleInfo.GodUniqueName);

    public override async Task<IReadOnlyList<string>> Convert(
        IProgress<Converter.Progress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureOutputDirectory(OutDataDirectory);

        int totalParts = GetTotalFileParts();

        for (int i = 0; i < totalParts; i++)
        {
            var partPath = GetFilePartPath(i);
            var stream = new FileStream(partPath, FileMode.Create, FileAccess.ReadWrite);
            FileParts.Add(new FilePart() { Stream = stream, Path = partPath });
        }

        ProgData.Stage = Converter.Stage.WritingData;
        ProgData.Current = 0;
        ProgData.Total = GetTotalOutDataBytes();

        await WriteData(progress, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
            return CleanupCancelledFiles();

        WriteSubHashTables(progress, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
            return CleanupCancelledFiles();

        var finalMhtHash = FinalizeHashTables(progress, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
            return CleanupCancelledFiles();

        WriteLiveHeader(finalMhtHash);
        if (cancellationToken.IsCancellationRequested)
            return CleanupCancelledFiles();

        return FileParts.Select(fp => fp.Path).ToList().AsReadOnly();
    }

    protected int GetTotalFileParts() => 
        NumFileParts(NumBlocks(GetTotalOutDataBytes()));

    protected abstract long GetTotalOutDataBytes();

    protected abstract Task WriteData(
        IProgress<Converter.Progress>? progress,
        CancellationToken cancellationToken);

    protected void WriteSubHashTables(
        IProgress<Converter.Progress>? progress,
        CancellationToken cancellationToken)
    {
        foreach (var part in FileParts)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var blocksRemaining = NumBlocks(part.Stream.Length);
            var subHashTableCount =
                (blocksRemaining - 1) /
                (GOD.DATA_BLOCKS_PER_SHT + 1) +
                ((blocksRemaining - 1) % (GOD.DATA_BLOCKS_PER_SHT + 1) > 0 ? 1 : 0);

            var masterHashTable = new byte[subHashTableCount * SHA1.HashSizeInBytes];
            var stream = part.Stream;

            stream.Seek(GOD.BLOCK_SIZE, SeekOrigin.Begin);

            --blocksRemaining;

            for (int i = 0; i < subHashTableCount; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var blocksInSht = 0;
                var blockBuffer = new byte[GOD.BLOCK_SIZE];
                var subhashTable = new byte[GOD.DATA_BLOCKS_PER_SHT * SHA1.HashSizeInBytes];

                stream.Seek(GOD.BLOCK_SIZE, SeekOrigin.Current);

                --blocksRemaining;

                while (blocksInSht < GOD.DATA_BLOCKS_PER_SHT && 0 < blocksRemaining)
                {
                    var bytesRead = stream.Read(blockBuffer, 0, blockBuffer.Length);

                    if (bytesRead != blockBuffer.Length)
                    {
                        throw new InvalidOperationException(
                            $"Expected to read {blockBuffer.Length} bytes, but only read {bytesRead} bytes.");
                    }

                    ReadOnlySpan<byte> blockData = blockBuffer.AsSpan(0, bytesRead);

                    if (!SHA1.TryHashData(blockData, subhashTable.AsSpan(blocksInSht * SHA1.HashSizeInBytes, SHA1.HashSizeInBytes), out int written) ||
                        written != SHA1.HashSizeInBytes)
                    {
                        throw new InvalidOperationException("Failed to compute hash for block.");
                    }

                    blocksInSht++;
                    --blocksRemaining;

                    ProgData.Current += bytesRead;
                    progress?.Report(ProgData);
                }

                var pos = stream.Position;

                stream.Seek(
                    (i * (GOD.DATA_BLOCKS_PER_SHT + 1) * GOD.BLOCK_SIZE) + GOD.BLOCK_SIZE,
                    SeekOrigin.Begin);

                stream.Write(subhashTable, 0, subhashTable.Length);

                var masterHash = SHA1.HashData(subhashTable);
                masterHash.CopyTo(masterHashTable, i * SHA1.HashSizeInBytes);

                if (blocksRemaining == 0)
                    break;
            }

            stream.Seek(0, SeekOrigin.Begin);
            stream.Write(masterHashTable, 0, masterHashTable.Length);
        }
    }

    protected byte[] FinalizeHashTables(
        IProgress<Converter.Progress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var finalMhtHash = new byte[SHA1.HashSizeInBytes];
        var currHash = new byte[SHA1.HashSizeInBytes];
        var blockBuffer = new byte[GOD.BLOCK_SIZE];

        for (int i = FileParts.Count - 1; i > 0; i--)
        {
            if (cancellationToken.IsCancellationRequested)
                return finalMhtHash;

            var currStream = FileParts[i].Stream;
            var prevStream = FileParts[i - 1].Stream;

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
        }

        return finalMhtHash;
    }

    protected void WriteLiveHeader(byte[] finalMhtHash)
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

            ulong totalSize = FileParts.Aggregate(
                0UL, (acc, fp) => checked(acc + (ulong)fp.Stream.Length));
            uint partsWrittenSize = (uint)(totalSize / 0x100);
            uint partCount = (uint)FileParts.Count;
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
            Encoding.Unicode.GetBytes(TitleInfo.TitleName, 0, TitleInfo.TitleName.Length, titleNameBytes, 0);

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

    protected string GetFilePartPath(int index)
    {
        string s = index.ToString("D4");
        return Path.Join(OutDataDirectory, $"Data{s}");
    }

    protected void WriteXisoSector(uint sector, ReadOnlySpan<byte> data)
    {
        var remap = RemapSector(sector);
        var stream = FileParts[remap.FileIndex].Stream;
        stream.Seek(remap.Offset, SeekOrigin.Begin);
        stream.Write(data);
    }

    protected static Remap RemapSector(uint sector)
    {
        long blockNum = (sector * XISO.SECTOR_SIZE) / GOD.BLOCK_SIZE;
        int fileIndex = (int)(blockNum / GOD.BLOCKS_PER_PART);
        int dataBlockInFile = (int)(blockNum % GOD.BLOCKS_PER_PART);
        int hashIndex = dataBlockInFile / GOD.DATA_BLOCKS_PER_SHT;

        long newOffset = GOD.BLOCK_SIZE;
        newOffset += ((hashIndex + 1) * GOD.BLOCK_SIZE);
        newOffset += (dataBlockInFile * GOD.BLOCK_SIZE);
        newOffset += (sector * XISO.SECTOR_SIZE) % GOD.BLOCK_SIZE;
        return new Remap() { Offset = newOffset, FileIndex = fileIndex };
    }

    protected static Remap RemapOffset(long offset)
    {
        var remap = RemapSector((uint)(offset / XISO.SECTOR_SIZE));
        remap.Offset += offset % XISO.SECTOR_SIZE;
        return remap;
    }

    protected static long ToIsoOffset(long offset, int fileIndex)
    {
        long blockNum = offset / GOD.BLOCK_SIZE;
        var prevDataBlocks = fileIndex * GOD.DATA_BLOCKS_PER_PART;
        var subHashIndex = blockNum / (GOD.DATA_BLOCKS_PER_SHT + 1);
        var dataBlockNum = blockNum - (subHashIndex + 2) + prevDataBlocks;
        return (dataBlockNum * GOD.BLOCK_SIZE) + (offset % GOD.BLOCK_SIZE);
    }

    protected static long NumBlocks(long size)
    {
        return (size / GOD.BLOCK_SIZE) + ((size % GOD.BLOCK_SIZE) > 0 ? 1 : 0);
    }

    protected static int NumFileParts(long numBlocks)
    {
        return (int)
            ((numBlocks / GOD.DATA_BLOCKS_PER_PART) +
             (((numBlocks % GOD.DATA_BLOCKS_PER_PART) > 0) ? 1 : 0));
    }

    private IReadOnlyList<string> CleanupCancelledFiles()
    {
        foreach (var part in FileParts)
        {
            try
            {
                part.Stream.Close();
                if (File.Exists(part.Path))
                    File.Delete(part.Path);
            }
            catch
            {
            }
        }

        if (File.Exists(LiveHeaderPath))
        {
            try
            {
                File.Delete(LiveHeaderPath);
            }
            catch
            {
            }
        }
        if (Directory.Exists(OutDataDirectory))
        {
            try
            {
                Directory.Delete(OutDataDirectory, recursive: true);
            }
            catch
            {
            }
        }
        return Array.Empty<string>();
    }
}
