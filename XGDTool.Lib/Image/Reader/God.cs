using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDTool.Lib.Image.Formats;

namespace XGDTool.Lib.Image.Reader;

internal class God : Base
{
    private readonly List<FileStream> Streams = new();

    public override Format ImageFormat => Format.GOD;
    public override uint TotalSectors { get; protected set; } = 0;

    public God(IReadOnlyList<string> files) : base(files)
    {
        if (!IsValid(files[0]))
            throw new ArgumentException("Invalid GOD file.", nameof(files));

        var dataFiles = Directory.GetFiles(files[0], "*.*", SearchOption.TopDirectoryOnly);

        foreach (var file in dataFiles)
        {
            if (!Path.GetFileName(file).StartsWith("Data", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!GOD.IsBlockAligned(file.Length))
                throw new ArgumentException(
                    $"File '{file}' length is not aligned to block size.",
                    nameof(files));

            Streams.Add(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        long totalDataBlocks = (Streams.Count - 1) * GOD.DATA_BLOCKS_PER_PART;
        var lastStream = Streams.Last();
        var lastShtCount = GOD.SubHashTableCount(lastStream.Length);
        totalDataBlocks += (lastShtCount - 1) * GOD.DATA_BLOCKS_PER_SHT;
        var lastDataOffset = (long)(((lastShtCount - 1) * (GOD.DATA_BLOCKS_PER_SHT + 1)) + 1) * GOD.BLOCK_SIZE;
        totalDataBlocks += (lastStream.Length - lastDataOffset) / GOD.BLOCK_SIZE;

        TotalSectors = XISO.SectorCount(totalDataBlocks * GOD.BLOCK_SIZE);
    }

    public override void ReadSectors(uint startSector, Span<byte> buffer)
    {
        if (!XISO.IsSectorAligned(buffer.Length))
            throw new ArgumentException(
                "Buffer length must be aligned to sector size.", nameof(buffer));

        if (startSector >= TotalSectors)
            throw new ArgumentOutOfRangeException(
                nameof(startSector),
                "Start sector is out of range for the total sectors in the image.");

        uint sector = startSector;
        int bufferOffset = 0;
        int remainingBytes = buffer.Length;

        while (remainingBytes > 0)
        {
            var (stream, offset, available) = GetStreamForSector(startSector);
            int toRead = checked((int)Math.Min(available, remainingBytes));

            ReadExactlyAt(stream, buffer.Slice(bufferOffset, toRead), offset);

            bufferOffset += toRead;
            remainingBytes -= toRead;
            sector += XISO.SectorCount(toRead);
        }
    }

    public override async Task ReadSectorsAsync(uint startSector, Memory<byte> buffer, CancellationToken ct = default)
    {
        if (!XISO.IsSectorAligned(buffer.Length))
            throw new ArgumentException(
                "Buffer length must be aligned to sector size.", nameof(buffer));

        if (startSector >= TotalSectors)
            throw new ArgumentOutOfRangeException(
                nameof(startSector),
                "Start sector is out of range for the total sectors in the image.");

        uint sector = startSector;
        int bufferOffset = 0;
        int remainingBytes = buffer.Length;

        while (remainingBytes > 0) 
        {
            ct.ThrowIfCancellationRequested();

            var (stream, offset, available) = GetStreamForSector(startSector);
            int toRead = checked((int)Math.Min(available, remainingBytes));

            await ReadExactlyAtAsync(stream, buffer.Slice(bufferOffset, toRead), offset, ct);

            bufferOffset += toRead;
            remainingBytes -= toRead;
            sector += XISO.SectorCount(toRead);
        }
    }

    public static bool IsValid(string path)
    {
        if (!Directory.Exists(path))
            return false;

        if (!Path.GetFileName(path).EndsWith(".data", StringComparison.OrdinalIgnoreCase))
            return false;

        var files = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly);
        foreach (var file in files)
        {
            if (Path.GetFileName(file).StartsWith("Data", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private (FileStream stream, long offset, long remaining) GetStreamForSector(uint sector)
    {
        long blockNum = (sector * XISO.SECTOR_SIZE) / GOD.BLOCK_SIZE;
        uint fileIndex = (uint)(blockNum / GOD.DATA_BLOCKS_PER_PART);
        long dataBlockInFile = blockNum % GOD.DATA_BLOCKS_PER_PART;
        uint shtIndex = (uint)(dataBlockInFile / GOD.DATA_BLOCKS_PER_SHT);

        long newOffset = GOD.BLOCK_SIZE; // master hashtable
        newOffset += ((shtIndex + 1) * GOD.BLOCK_SIZE); // Add subhash table blocks
        newOffset += (dataBlockInFile * GOD.BLOCK_SIZE); // Add data blocks
        newOffset += (sector * XISO.SECTOR_SIZE) % GOD.BLOCK_SIZE; // Add offset within data block

        // Just provide remaining bytes in the current block for simplicity
        // can be revisited later
        long remaining = GOD.BLOCK_SIZE - ((sector * XISO.SECTOR_SIZE) % GOD.BLOCK_SIZE);

        if (fileIndex >= Streams.Count)
            throw new ArgumentOutOfRangeException(
                nameof(sector), 
                "Sector is out of range for the provided files.");
        
        return (Streams[(int)fileIndex], newOffset, remaining);
    }
}
