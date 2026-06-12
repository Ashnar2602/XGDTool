using System.Text;
using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.MockFileSystem;
using XGDTool.Lib.Util;
using ZstdSharp;

namespace XGDTool.Lib.Image.Reader;

internal class Zar : Base
{
    private sealed class ZFileInfo
    {
        public required ulong FileOffset;
        public required ulong FileSize;
    }

    private sealed class PathNode : Entry<PathNode, DirectoryEntry>;

    private class EEntry
    {
        public required uint StartSector;
        public long Offset => StartSector * XISO.SECTOR_SIZE;

        public required long Size;
        public uint NumSectors => XISO.SectorCount(Size);
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

    private readonly List<EEntry> EEntries = new();
    private readonly List<ZAR.CompressionOffsetRecord> OffsetRecords = new();
    private readonly Dictionary<string, ZFileInfo> FilesByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly byte[] CachedBlock = new byte[ZAR.COMPRESSED_BLOCK_SIZE];
    private readonly byte[] CompressedBlockBuffer = new byte[ZAR.COMPRESSED_BLOCK_SIZE];
    private FileStream? Stream;
    private long VirtualSize;
    private ulong? CachedBlockIndex;
    private ulong CompressedDataOffset;
    private ulong CompressedDataSize;

    private string ArchivePath => FilePaths[0];

    public override uint TotalSectors
    {
        get => XISO.SectorCount(VirtualSize);
        protected set => throw new NotSupportedException();
    }

    public override Format ImageFormat => Format.ZAR;

    public static bool IsValid(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length <= ZAR.FOOTER_SIZE)
                return false;

            var footer = ReadFooter(stream);
            return footer.Magic == ZAR.FOOTER_MAGIC &&
                   footer.Version == ZAR.FOOTER_VERSION &&
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

        var rootNode = ReadArchive(progress, ct);
        var avlTree = new Avl.Tree(Path.GetFileNameWithoutExtension(ArchivePath));
        avlTree.BuildTree(rootNode);

        VirtualSize = XISO.CalculateTotalSize(avlTree.RootNode);

        BuildVirtualEntries(avlTree, progress, ct);
        return Task.CompletedTask;
    }

    public override void ReadSectors(uint startSector, Span<byte> buffer, CancellationToken ct = default)
    {
        if (!XISO.IsSectorAligned(buffer.Length))
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
                    checked((int)((nextEntry.StartSector - startSector) * XISO.SECTOR_SIZE)));
            }

            buffer.Slice(0, clearBytes).Clear();

            if (clearBytes < buffer.Length)
            {
                ReadSectors(
                    startSector + XISO.SectorCount(clearBytes),
                    buffer.Slice(clearBytes));
            }

            return;
        }

        var offsetInEntry = (int)(startSector - entry.StartSector) * XISO.SECTOR_SIZE;
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

            if (!XISO.IsSectorAligned(readLen))
            {
                var padLen = checked((int)(XISO.SectorCount(readLen) * XISO.SECTOR_SIZE - readLen));
                buffer.Slice(readLen, padLen).Fill(XISO.PAD_BYTE);
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
                startSector + XISO.SectorCount(copyLen),
                buffer.Slice((int)copyLen));
        }
    }

    // public override Task ReadSectorsAsync(uint startSector, Memory<byte> buffer, CancellationToken ct = default)
    // {
    //     ReadSectors(startSector, buffer.Span);
    //     return Task.CompletedTask;
    // }

    private PathNode ReadArchive(IProgress<Converter.Progress>? progress, CancellationToken ct)
    {
        var stream = Stream ?? throw new InvalidOperationException("Archive stream has not been initialized.");
        if (stream.Length <= ZAR.FOOTER_SIZE)
            throw new InvalidDataException("Archive is too small to be a valid ZAR file.");

        var footer = ReadFooter(stream);
        ValidateFooter(footer, stream.Length);

        CompressedDataOffset = footer.SectionCompressedDataOffset;
        CompressedDataSize = footer.SectionCompressedDataSize;

        LoadOffsetRecords(stream, footer);
        var nameTable = ReadSection(stream, footer.SectionNamesOffset, footer.SectionNamesSize);
        var fileTree = LoadFileTree(stream, footer);

        var progData = new Converter.Progress
        {
            Stage = Converter.Stage.Initializing,
            Current = 0,
            Total = fileTree.Count
        };
        progress?.Report(progData);

        if (fileTree.Count == 0 || fileTree[0].IsFile)
            throw new InvalidDataException("ZAR file tree is missing a valid root directory entry.");

        if (!string.IsNullOrEmpty(ReadName(nameTable, fileTree[0].NameOffset)))
            throw new InvalidDataException("ZAR root directory entry must not have a name.");

        var root = new PathNode { FileName = string.Empty, IsFile = false, Context = null };
        PopulateChildren(root, string.Empty, 0, fileTree, nameTable, progress, ref progData, ct);

        progData.Current = progData.Total;
        progress?.Report(progData);

        return root;
    }

    private void PopulateChildren(
        PathNode parent,
        string parentPath,
        int parentIndex,
        IReadOnlyList<ZAR.FileDirectoryEntry> fileTree,
        ReadOnlySpan<byte> nameTable,
        IProgress<Converter.Progress>? progress,
        ref Converter.Progress progData,
        CancellationToken ct)
    {
        var parentEntry = fileTree[parentIndex];
        if (parentEntry.IsFile)
            throw new InvalidDataException("File entry cannot own child entries.");

        uint startIndex = parentEntry.DirectoryNodeStartIndex;
        uint count = parentEntry.DirectoryNodeCount;

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

            var context = new DirectoryEntry
            {
                FilePath = childPath
            };
            context.SetName(childName);

            if (childEntry.IsFile)
            {
                if (childEntry.FileSize > uint.MaxValue)
                    throw new InvalidDataException($"File '{childPath}' exceeds the maximum supported size.");

                context.Header.Attributes = XISO.DirAttribute.Normal;
                context.Header.FileSize = (uint)childEntry.FileSize;

                FilesByPath[childPath] = new ZFileInfo
                {
                    FileOffset = childEntry.FileOffset,
                    FileSize = childEntry.FileSize
                };
            }
            else
            {
                context.Header.Attributes = XISO.DirAttribute.Directory;
                context.Header.FileSize = 0;
            }

            var childNode = new PathNode
            {
                FileName = childName,
                IsFile = childEntry.IsFile,
                Context = context
            };

            parent.AddSubEntry(childNode);

            progData.Current++;
            progress?.Report(progData);

            if (!childEntry.IsFile)
                PopulateChildren(childNode, childPath, childIndex, fileTree, nameTable, progress, ref progData, ct);
        }
    }

    private void BuildVirtualEntries(Avl.Tree avlTree, IProgress<Converter.Progress>? progress, CancellationToken ct)
    {
        var totalSectors = XISO.SectorCount(VirtualSize);
        var avlIterator = new Avl.Iterator(avlTree);

        var progData = new Converter.Progress
        {
            Stage = Converter.Stage.Initializing,
            Current = 0,
            Total = avlIterator.Entries.Count
        };
        progress?.Report(progData);

        var header = new XISO.FileHeader(
            (uint)avlTree.RootNode.StartSector,
            (uint)avlTree.RootNode.FileSize,
            totalSectors);

        EEntries.Add(new EDirectory
        {
            Buffer = header.ToBytes(),
            StartSector = 0,
            Size = header.Size()
        });

        for (int i = 0; i < avlIterator.Entries.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var entry = avlIterator.Entries[i];

            if (!XISO.IsSectorAligned(entry.Offset))
                throw new InvalidOperationException($"Entry '{entry.Node.FilePath}' is not sector aligned.");

            if (entry.IsDirectoryEntry)
            {
                var dirBuffer = avlIterator.WriteDirectoriesToBuffer(i, out var count);
                i += count - 1;

                EEntries.Add(new EDirectory
                {
                    Buffer = dirBuffer,
                    StartSector = XISO.SectorIndex(entry.Offset),
                    Size = dirBuffer.Length
                });
            }
            else if (FilesByPath.TryGetValue(entry.Node.FilePath, out var fileInfo))
            {
                EEntries.Add(new EFile
                {
                    FileInfo = fileInfo,
                    StartSector = XISO.SectorIndex(entry.Offset),
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
            ulong blockIndex = rawReadOffset / (ulong)ZAR.COMPRESSED_BLOCK_SIZE;
            int blockOffset = (int)(rawReadOffset % (ulong)ZAR.COMPRESSED_BLOCK_SIZE);
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
        ulong recordIndex = blockIndex / (ulong)ZAR.ENTRIES_PER_OFFSETRECORD;
        int sizeIndex = (int)(blockIndex % (ulong)ZAR.ENTRIES_PER_OFFSETRECORD);

        if (recordIndex >= (ulong)OffsetRecords.Count)
            throw new InvalidDataException("ZAR block index exceeds the available offset records.");

        var record = OffsetRecords[(int)recordIndex];
        ulong blockOffset = record.BaseOffset;

        for (int i = 0; i < sizeIndex; i++)
            blockOffset += (ulong)record.ToRaw().GetSize(i) + 1;

        int blockSize = record.ToRaw().GetSize(sizeIndex) + 1;
        if (blockSize <= 0)
            throw new InvalidDataException("ZAR block size is invalid.");

        if (blockOffset + (ulong)blockSize > CompressedDataOffset + CompressedDataSize)
            throw new InvalidDataException("ZAR block range exceeds the compressed data section.");

        return (blockOffset, blockSize);
    }

    private void LoadOffsetRecords(FileStream stream, ZAR.Footer footer)
    {
        var rawBytes = ReadSection(stream, footer.SectionOffsetRecordsOffset, footer.SectionOffsetRecordsSize);
        if (rawBytes.Length == 0 || (rawBytes.Length % ZAR.COMPRESSION_OFFSET_RECORD_SIZE) != 0)
            throw new InvalidDataException("ZAR offset record section has an invalid size.");

        OffsetRecords.Clear();

        for (int offset = 0; offset < rawBytes.Length; offset += ZAR.COMPRESSION_OFFSET_RECORD_SIZE)
        {
            var raw = new ZAR.CompressionOffsetRecordRaw();
            raw.FromBytes(rawBytes.AsSpan(offset, ZAR.COMPRESSION_OFFSET_RECORD_SIZE));
            OffsetRecords.Add(ZAR.CompressionOffsetRecord.FromRaw(raw));
        }
    }

    private static List<ZAR.FileDirectoryEntry> LoadFileTree(FileStream stream, ZAR.Footer footer)
    {
        var rawBytes = ReadSection(stream, footer.SectionFileTreeOffset, footer.SectionFileTreeSize);
        if (rawBytes.Length == 0 || (rawBytes.Length % ZAR.FILE_DIRECTORY_ENTRY_SIZE) != 0)
            throw new InvalidDataException("ZAR file tree section has an invalid size.");

        var fileTree = new List<ZAR.FileDirectoryEntry>(rawBytes.Length / ZAR.FILE_DIRECTORY_ENTRY_SIZE);

        for (int offset = 0; offset < rawBytes.Length; offset += ZAR.FILE_DIRECTORY_ENTRY_SIZE)
        {
            var entry = new ZAR.FileDirectoryEntry();
            entry.FromBytes(rawBytes.AsSpan(offset, ZAR.FILE_DIRECTORY_ENTRY_SIZE));
            fileTree.Add(entry);
        }

        return fileTree;
    }

    private static ZAR.Footer ReadFooter(FileStream stream)
    {
        var footerBytes = new byte[ZAR.FOOTER_SIZE];
        ReadExactlyAt(stream, footerBytes, stream.Length - ZAR.FOOTER_SIZE);
        return Marshalable.FromBytes<ZAR.Footer>(footerBytes);
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
        if (footer.Magic != ZAR.FOOTER_MAGIC)
            throw new InvalidDataException("ZAR footer magic is invalid.");

        if (footer.Version != ZAR.FOOTER_VERSION)
            throw new InvalidDataException("Unsupported ZAR footer version.");

        if (footer.TotalSize != (ulong)fileSize)
            throw new InvalidDataException("ZAR footer total size does not match the archive size.");

        if (!IsSectionInRange(footer.SectionCompressedDataOffset, footer.SectionCompressedDataSize, (ulong)fileSize) ||
            !IsSectionInRange(footer.SectionOffsetRecordsOffset, footer.SectionOffsetRecordsSize, (ulong)fileSize) ||
            !IsSectionInRange(footer.SectionNamesOffset, footer.SectionNamesSize, (ulong)fileSize) ||
            !IsSectionInRange(footer.SectionFileTreeOffset, footer.SectionFileTreeSize, (ulong)fileSize) ||
            !IsSectionInRange(footer.SectionMetaDirectoryOffset, footer.SectionMetaDirectorySize, (ulong)fileSize) ||
            !IsSectionInRange(footer.SectionMetaDataOffset, footer.SectionMetaDataSize, (ulong)fileSize))
        {
            throw new InvalidDataException("One or more ZAR footer sections are outside the archive bounds.");
        }
    }

    private static bool IsSectionInRange(ulong offset, ulong size, ulong fileSize) => offset + size <= fileSize;

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