using System.Text;
using XGDTool.Lib.Image.Authoring;
using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.Util;
using ZstdSharp;

namespace XGDTool.Lib.Image.Readers;

internal class Zar : Base
{
    private sealed class ZFileInfo
    {
        public required ulong FileOffset;
        public required ulong FileSize;
    }

    private class EEntry
    {
        public required uint StartSector;
        public long Offset => StartSector * XDVDFS.SECTOR_SIZE;

        public required long Size;
        public uint NumSectors => XDVDFS.SectorCount(Size);
        public uint EndSector => StartSector + NumSectors - 1;
    }

    private sealed class EFile : EEntry
    {
        public required ZFileInfo FileInfo;
    }

    private sealed class EDirectory : EEntry
    {
        public required byte[] Buffer;
    }

    private readonly List<EEntry> EEntries = [];
    private readonly List<ZAR.CompressedOffsetRecord> OffsetRecords = [];
    private readonly Dictionary<string, ZFileInfo> FilesByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly byte[] CachedBlock = new byte[ZAR.COMPRESSED_BLOCK_SIZE];
    private readonly byte[] CompressedBlockBuffer = new byte[ZAR.COMPRESSED_BLOCK_SIZE];
    private FileStream? Stream;
    private long VirtualSize;
    private ulong? CachedBlockIndex;
    private ulong CompressedDataOffset;
    private ulong CompressedDataSize;

    private string ArchivePath => FilePaths[0];

    public override uint TotalSectors => XDVDFS.SectorCount(VirtualSize);
    public override Format ImageFormat => Format.ZAR;

    public static bool IsValid(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length <= ZAR.Footer.SIZE)
                return false;

            var footer = ReadFooter(stream);
            return footer.Magic == ZAR.Footer.MAGIC &&
                   footer.Version == ZAR.Footer.VERSION &&
                   footer.TotalSize == (ulong)stream.Length;
        }
        catch
        {
            return false;
        }
    }

    public Zar(IReadOnlyList<string> files) : base(files)
    {
        if (!File.Exists(ArchivePath))
            throw new ArgumentException($"The provided path '{ArchivePath}' is not a valid file.");
    }

    protected override Task InitializeType(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        Stream = new FileStream(
            ArchivePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.RandomAccess);

        var entries = ReadArchive(progress, ct);
        var authorer = new XDvdFsAuthorer();
        authorer.CreateTree(entries);

        VirtualSize = authorer.TotalXisoBytes;
        BuildVirtualEntries(authorer, progress, ct);

        return Task.CompletedTask;
    }

    public override void ReadSectors(uint startSector, Span<byte> buffer, CancellationToken ct = default)
    {
        if (!XDVDFS.IsSectorAligned(buffer.Length))
            throw new ArgumentException("Buffer length must be sector aligned.", nameof(buffer));

        var entry = GetEntryForSector(startSector);
        if (entry == null)
        {
            if (startSector >= TotalSectors)
                throw new ArgumentOutOfRangeException(nameof(startSector), "Start sector is beyond the total sectors of the image.");

            var nextEntry = GetNextEntryAfterSector(startSector);
            var clearBytes = buffer.Length;

            if (nextEntry != null)
            {
                clearBytes = Math.Min(
                    buffer.Length,
                    checked((int)((nextEntry.StartSector - startSector) * XDVDFS.SECTOR_SIZE)));
            }

            buffer.Slice(0, clearBytes).Clear();

            if (clearBytes < buffer.Length)
            {
                ReadSectors(
                    startSector + XDVDFS.SectorCount(clearBytes),
                    buffer.Slice(clearBytes), ct);
            }

            return;
        }

        var offsetInEntry = checked((int)(startSector - entry.StartSector)) * XDVDFS.SECTOR_SIZE;
        var remainingLen = buffer.Length;
        var copyLen = Math.Min(remainingLen, entry.Size - offsetInEntry);

        if (entry is EDirectory dir)
        {
            dir.Buffer.AsSpan(offsetInEntry, (int)copyLen).CopyTo(buffer);
            remainingLen -= (int)copyLen;
        }
        else if (entry is EFile file)
        {
            var readLen = (int)Math.Min(copyLen, (long)file.FileInfo.FileSize - offsetInEntry);
            ReadFileBytes(file.FileInfo, offsetInEntry, buffer.Slice(0, readLen));

            if (!XDVDFS.IsSectorAligned(readLen))
            {
                var padLen = checked((int)(XDVDFS.SectorCount(readLen) * XDVDFS.SECTOR_SIZE - readLen));
                buffer.Slice(readLen, padLen).Fill(XDVDFS.PAD_BYTE);
                readLen += padLen;
            }

            copyLen = readLen;
            remainingLen -= readLen;
        }
        else
        {
            throw new InvalidOperationException("Unknown entry type.");
        }

        if (remainingLen > 0)
        {
            ReadSectors(
                startSector + XDVDFS.SectorCount(copyLen),
                buffer.Slice((int)copyLen), ct);
        }
    }

    // public override Task ReadSectorsAsync(uint startSector, Memory<byte> buffer, CancellationToken ct = default)
    // {
    //     ReadSectors(startSector, buffer.Span);
    //     return Task.CompletedTask;
    // }

    private List<DirectoryEntryExt> ReadArchive(IProgress<Converter.Progress>? progress, CancellationToken ct)
    {
        var stream = Stream ?? throw new InvalidOperationException("Archive stream has not been initialized.");
        if (stream.Length <= ZAR.Footer.SIZE)
            throw new InvalidDataException("Archive is too small to be a valid ZAR file.");

        FilesByPath.Clear();
        OffsetRecords.Clear();
        EEntries.Clear();
        CachedBlockIndex = null;

        var footer = ReadFooter(stream);
        ValidateFooter(footer, stream.Length);

        CompressedDataOffset = footer.CompressedData.Offset;
        CompressedDataSize = footer.CompressedData.Length;

        var rawBytes = ReadSection(stream, footer.OffsetRecords.Offset, footer.OffsetRecords.Length);
        if (rawBytes.Length == 0 || (rawBytes.Length % ZAR.CompressedOffsetRecord.SIZE) != 0)
            throw new InvalidDataException("ZAR offset record section has an invalid size.");

        OffsetRecords.Clear();

        for (int offset = 0; offset < rawBytes.Length; offset += ZAR.CompressedOffsetRecord.SIZE)
        {
            OffsetRecords.Add(
                ISerializable.Deserialize<ZAR.CompressedOffsetRecord>(
                    rawBytes.AsSpan(offset, ZAR.CompressedOffsetRecord.SIZE)));
        }
        var nameTable = ReadSection(stream, footer.Names.Offset, footer.Names.Length);
        var fileTree = LoadFileTree(stream, footer);

        var progData = new Converter.Progress
        {
            Stage = Converter.Stage.Initializing,
            Current = 0,
            Total = fileTree.Count
        };
        progress?.Report(progData);

        if (fileTree.Count == 0 || fileTree[0] is not ZAR.DirectoryEntry dEntry)
            throw new InvalidDataException("ZAR file tree is missing a valid root directory entry.");

        if (!string.IsNullOrEmpty(ReadName(nameTable, dEntry.NameOffset)))
            throw new InvalidDataException("ZAR root directory entry must not have a name.");

        var entries = new List<DirectoryEntryExt>(fileTree.Count);
        PopulateFlatEntries(entries, string.Empty, 0, fileTree, nameTable, progress, ref progData, ct);

        progData.Current = progData.Total;
        progress?.Report(progData);

        return entries;
    }

    private void PopulateFlatEntries(
        List<DirectoryEntryExt> output,
        string parentPath,
        int parentIndex,
        IReadOnlyList<ZAR.PathEntry> fileTree,
        ReadOnlySpan<byte> nameTable,
        IProgress<Converter.Progress>? progress,
        ref Converter.Progress progData,
        CancellationToken ct)
    {
        if (fileTree[parentIndex] is not ZAR.DirectoryEntry dEntry)
            throw new InvalidDataException("File entry cannot own child entries.");

        uint startIndex = dEntry.NodeStartIndex;
        uint count = dEntry.NodeCount;

        if (startIndex + count > fileTree.Count)
            throw new InvalidDataException("Directory child range exceeds the ZAR file tree bounds.");

        for (uint i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();

            int childIndex = checked((int)(startIndex + i));
            var childEntry = fileTree[childIndex];
            var childName = ReadName(nameTable, childEntry.NameOffset);

            if (string.IsNullOrEmpty(childName))
                throw new InvalidDataException("ZAR child entry has an empty name.");

            var childPath = string.IsNullOrEmpty(parentPath)
                ? childName
                : Path.Join(parentPath, childName);

            var context = new DirectoryEntryExt
            {
                FileName = childName,
                FilePath = childPath
            };

            if (childEntry is ZAR.FileEntry fEntry)
            {
                if (fEntry.FileSize > uint.MaxValue)
                    throw new InvalidDataException($"File '{childPath}' exceeds the maximum supported size.");

                context.Attributes = XDVDFS.DirAttributes.Normal;
                context.FileSize = (long)fEntry.FileSize;

                FilesByPath[childPath] = new ZFileInfo
                {
                    FileOffset = fEntry.FileOffset,
                    FileSize = fEntry.FileSize
                };
            }
            else
            {
                context.Attributes = XDVDFS.DirAttributes.Directory;
                context.FileSize = 0;
            }

            output.Add(context);

            progData.Current++;
            progress?.Report(progData);

            if (childEntry is ZAR.DirectoryEntry)
                PopulateFlatEntries(output, childPath, childIndex, fileTree, nameTable, progress, ref progData, ct);
        }
    }

    private void BuildVirtualEntries(XDvdFsAuthorer authorer, IProgress<Converter.Progress>? progress, CancellationToken ct)
    {
        var progData = new Converter.Progress
        {
            Stage = Converter.Stage.Initializing,
            Current = 0,
            Total = authorer.EntryRecordList.Count
        };
        progress?.Report(progData);

        {
            var volDesc = new XDVDFS.VolumeDescriptor
            {
                RootDirectoryTableSector = authorer.RootStartSector,
                RootDirectoryTableSize = authorer.RootSize,
            };

            EEntries.Add(new EDirectory
            {
                Buffer = volDesc.Serialize(),
                StartSector = XDVDFS.VOLUME_DESCRIPTOR_SECTOR,
                Size = XDVDFS.VolumeDescriptor.SIZE
            });
        }

        for (int i = 0; i < authorer.EntryRecordList.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var entry = authorer.EntryRecordList[i];

            if (!XDVDFS.IsSectorAligned(entry.AbsoluteOffset))
                throw new InvalidOperationException($"Entry '{entry.Node.FilePath}' is not sector aligned.");

            if (entry.IsDirectoryEntry)
            {
                var dirBuffer = authorer.SerializeDirectoryEntryRange(i, out var count);
                i += count - 1;

                if (!XDVDFS.IsSectorAligned(dirBuffer.Length))
                    throw new InvalidOperationException("Directory buffer is not sector aligned.");

                EEntries.Add(new EDirectory
                {
                    Buffer = dirBuffer,
                    StartSector = XDVDFS.SectorIndex(entry.AbsoluteOffset),
                    Size = dirBuffer.Length
                });
            }
            else if (FilesByPath.TryGetValue(entry.Node.FilePath, out var fileInfo))
            {
                EEntries.Add(new EFile
                {
                    FileInfo = fileInfo,
                    StartSector = XDVDFS.SectorIndex(entry.AbsoluteOffset),
                    Size = entry.Node.FileSize
                });
            }
            else
            {
                throw new InvalidOperationException($"Missing ZAR file data for '{entry.Node.FilePath}'.");
            }

            progData.Current = i + 1;
            progress?.Report(progData);
        }

        progData.Current = progData.Total;
        progress?.Report(progData);
    }

    private void ReadFileBytes(ZFileInfo fileInfo, long fileOffset, Span<byte> buffer)
    {
        if (fileOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(fileOffset));

        long remaining = buffer.Length;
        long outputOffset = 0;
        ulong rawReadOffset = fileInfo.FileOffset + (ulong)fileOffset;

        while (remaining > 0)
        {
            ulong blockIndex = rawReadOffset / ZAR.COMPRESSED_BLOCK_SIZE;
            int blockOffset = (int)(rawReadOffset % ZAR.COMPRESSED_BLOCK_SIZE);
            int stepSize = (int)Math.Min(remaining, ZAR.COMPRESSED_BLOCK_SIZE - blockOffset);

            var block = GetBlock(blockIndex);
            block.AsSpan(blockOffset, stepSize).CopyTo(buffer.Slice((int)outputOffset, stepSize));

            rawReadOffset += (ulong)stepSize;
            outputOffset += stepSize;
            remaining -= stepSize;
        }
    }

    private byte[] GetBlock(ulong blockIndex)
    {
        if (CachedBlockIndex == blockIndex)
            return CachedBlock;

        var stream = Stream ?? throw new InvalidOperationException("Archive stream has not been initialized.");
        var (blockOffset, blockSize) = GetCompressedBlockLocation(blockIndex);

        if (blockSize == ZAR.COMPRESSED_BLOCK_SIZE)
        {
            ReadExactlyAt(stream, CachedBlock, checked((long)blockOffset));
        }
        else
        {
            ReadExactlyAt(stream, CompressedBlockBuffer.AsSpan(0, blockSize), checked((long)blockOffset));

            using var decompressor = new Decompressor();
            var decompressed = decompressor.Unwrap(CompressedBlockBuffer.AsSpan(0, blockSize).ToArray());

            if (decompressed.Length != ZAR.COMPRESSED_BLOCK_SIZE)
                throw new InvalidDataException("Decompressed ZAR block size did not match the expected 64 KiB.");

            decompressed.CopyTo(CachedBlock);
        }

        CachedBlockIndex = blockIndex;
        return CachedBlock;
    }

    private (ulong Offset, int Size) GetCompressedBlockLocation(ulong blockIndex)
    {
        ulong recordIndex = blockIndex / ZAR.CompressedOffsetRecord.ENTRIES_MAX;
        int sizeIndex = (int)(blockIndex % ZAR.CompressedOffsetRecord.ENTRIES_MAX);

        if (recordIndex >= (ulong)OffsetRecords.Count)
            throw new InvalidDataException("ZAR block index exceeds the available offset records.");

        var record = OffsetRecords[(int)recordIndex];
        ulong blockOffset = record.BaseOffset;

        for (int i = 0; i < sizeIndex; i++)
            blockOffset += (ulong)record.SizeTableEntries[i] + 1;

        int blockSize = record.SizeTableEntries[sizeIndex] + 1;
        if (blockSize <= 0)
            throw new InvalidDataException("ZAR block size is invalid.");

        if (blockOffset + (ulong)blockSize > CompressedDataOffset + CompressedDataSize)
            throw new InvalidDataException("ZAR block range exceeds the compressed data section.");

        return (blockOffset, blockSize);
    }

    private static List<ZAR.PathEntry> LoadFileTree(FileStream stream, ZAR.Footer footer)
    {
        var bytes = ReadSection(stream, footer.FileTree.Offset, footer.FileTree.Length);

        if (bytes.Length == 0 || (bytes.Length % ZAR.PathEntry.SIZE) != 0)
            throw new InvalidDataException("ZAR file tree section has an invalid size.");

        var fileTree = new List<ZAR.PathEntry>(bytes.Length / ZAR.PathEntry.SIZE);

        for (int offset = 0; offset < bytes.Length; offset += ZAR.PathEntry.SIZE)
            fileTree.Add(ZAR.PathEntry.DeserializeToType(
                bytes.AsSpan(offset, ZAR.PathEntry.SIZE)));

        return fileTree;
    }

    private static ZAR.Footer ReadFooter(FileStream stream)
    {
        var footerBytes = new byte[ZAR.Footer.SIZE];
        ReadExactlyAt(stream, footerBytes, stream.Length - ZAR.Footer.SIZE);
        return ISerializable.Deserialize<ZAR.Footer>(footerBytes);
    }

    private static byte[] ReadSection(FileStream stream, ulong offset, ulong size)
    {
        if (size > int.MaxValue)
            throw new InvalidDataException("ZAR metadata section is too large to load.");

        var data = new byte[(int)size];
        if (data.Length > 0)
            ReadExactlyAt(stream, data, checked((long)offset));
        return data;
    }

    private static void ValidateFooter(ZAR.Footer footer, long fileSize)
    {
        if (footer.Magic != ZAR.Footer.MAGIC)
            throw new InvalidDataException("ZAR footer magic is invalid.");

        if (footer.Version != ZAR.Footer.VERSION)
            throw new InvalidDataException("Unsupported ZAR footer version.");

        if (footer.TotalSize != (ulong)fileSize)
            throw new InvalidDataException("ZAR footer total size does not match the archive size.");

        if (!IsSectionInRange(footer.CompressedData, (ulong)fileSize) ||
            !IsSectionInRange(footer.OffsetRecords, (ulong)fileSize) ||
            !IsSectionInRange(footer.Names, (ulong)fileSize) ||
            !IsSectionInRange(footer.FileTree, (ulong)fileSize) ||
            !IsSectionInRange(footer.MetaDirectory, (ulong)fileSize) ||
            !IsSectionInRange(footer.MetaData, (ulong)fileSize))
        {
            throw new InvalidDataException("One or more ZAR footer sections are outside the archive bounds.");
        }
    }

    private static bool IsSectionInRange(ZAR.SectionRecord section, ulong fileSize) => 
        section.Offset + section.Length <= fileSize;

    private static string ReadName(ReadOnlySpan<byte> nameTable, uint offset)
    {
        if (offset == 0x7FFFFFFF)
            return string.Empty;

        if (offset >= nameTable.Length)
            throw new InvalidDataException("ZAR name offset is outside the name table.");

        int headerSize = 1;
        int nameLength = nameTable[(int)offset] & 0x7F;

        if ((nameTable[(int)offset] & 0x80) != 0)
        {
            if (offset + 1 >= nameTable.Length)
                throw new InvalidDataException("ZAR extended name header is truncated.");

            headerSize = 2;
            nameLength |= nameTable[(int)offset + 1] << 7;
        }

        int nameOffset = checked((int)offset + headerSize);
        if (nameOffset + nameLength > nameTable.Length)
            throw new InvalidDataException("ZAR name extends beyond the name table.");

        return Encoding.UTF8.GetString(nameTable.Slice(nameOffset, nameLength));
    }

    private EEntry? GetEntryForSector(uint sector) =>
        EEntries.FirstOrDefault(e => sector >= e.StartSector && sector <= e.EndSector);

    private EEntry? GetNextEntryAfterSector(uint sector) =>
        EEntries
            .Where(e => e.StartSector > sector)
            .OrderBy(e => e.StartSector)
            .FirstOrDefault();
}