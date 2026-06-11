using System.Security.Cryptography;
using System.Text;
using ZstdSharp;
using XGDTool.Lib.Util;
using XGDTool.Lib.Image.Formats;

namespace XGDTool.Lib.Image.Writer;

internal class Zar(IReader reader, IWriterOptions options, Title.Info titleInfo) : IWriter
{
    private class PathNode : MockFileSystem.Entry<PathNode, Reader.DirectoryEntry>
    {
        public uint NameIndex;
        public ulong FileOffset;
        public uint NodeStartIndex;
    }

    private class Context
    {
        public Converter.Progress ProgData = new();
        public IProgress<Converter.Progress>? Progress;
        public CancellationToken Ct = default;
        public required FileStream Stream;
        public required PathNode RootNode;
        public readonly byte[] ReadBuffer = new byte[512 * XISO.SECTOR_SIZE];
        public readonly byte[] BlockBuffer = new byte[ZAR.COMPRESSED_BLOCK_SIZE];
        public IncrementalHash Sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        public List<ZAR.CompressionOffsetRecord> OffsetRecords = new() { new ZAR.CompressionOffsetRecord() };
        public ZAR.CompressionOffsetRecord CurrentOffsetRecord => OffsetRecords.Last();
        public readonly List<string> NodeNames = new();
        public readonly Dictionary<string, uint> NodeNameLookup = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<uint> NodeNameOffsets = new();
        public readonly ZAR.Footer Footer = new();
        public ulong CurrentInputOffset;
        public int CurrentBlockFill;
        public uint WrittenBlockCount;
    }

    private readonly IReader Reader = reader;
    private readonly IWriterOptions Options = options;
    private readonly Title.Info TitleInfo = titleInfo;

    private string OutputPath => Path.Combine(Options.OutputDirectory, TitleInfo.ImageName + ".zar");

    public async Task<IReadOnlyList<string>> Convert(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        if (!Directory.Exists(Options.OutputDirectory))
            Directory.CreateDirectory(Options.OutputDirectory);

        var rootNode = MockFileSystem.Create.FromReader<PathNode>(Reader);

        if (rootNode.SubEntries.Count == 0)
            throw new InvalidOperationException("Cannot create zar from an empty directory.");

        var context = new Context
        {
            ProgData = new Converter.Progress
            {
                Stage = Converter.Stage.WritingData,
                Current = 0,
                Total = Reader.TotalSizeOfFiles
            },
            Progress = progress,
            Ct = ct,
            Stream = new FileStream(
                OutputPath, 
                FileMode.Create, 
                FileAccess.Write, 
                FileShare.None),
            RootNode = rootNode
        };

        AssignNodeData(rootNode, context);
        await WriteFiles(rootNode, context);
        FlushPendingBlock(context);
        AlignOutput(context, 8);

        context.Footer.SectionCompressedDataOffset = 0;
        context.Footer.SectionCompressedDataSize = (ulong)context.Stream.Position;
        
        WriteOffsetRecords(context);
        WriteNameTable(context);
        WriteFileTree(rootNode, context);
        WriteMetaData(context);
        WriteFooter(context);

        await context.Stream.FlushAsync(ct);
        context.Stream.Dispose();

        return new[] { OutputPath };
    }

    private static void AssignNodeData(PathNode rootNode, Context context)
    {
        foreach (var node in EnumerateNodes(rootNode).Skip(1))
        {
            node.NameIndex = GetOrAddName(node.Name, context);

            if (node.IsFile)
            {
                node.FileOffset = context.CurrentInputOffset;
                context.CurrentInputOffset += node.Context?.Header.FileSize ?? 0u;
            }
        }
    }

    private async Task WriteFiles(PathNode rootNode, Context context)
    {
        foreach (var node in EnumerateNodes(rootNode))
        {
            if (!node.IsFile)
                continue;

            await WriteFile(node, context);
        }
    }

    private Task WriteFile(PathNode node, Context context)
    {
        context.Ct.ThrowIfCancellationRequested();

        if (node.Context == null)
            throw new InvalidOperationException($"File node '{node.Name}' is missing reader context.");

        long readOffset = Reader.ImageOffset + ((long)node.Context.Header.StartSector * XISO.SECTOR_SIZE);
        long remainingBytes = node.Context.Header.FileSize;

        while (remainingBytes > 0)
        {
            context.Ct.ThrowIfCancellationRequested();

            int bytesToRead = (int)Math.Min(context.ReadBuffer.Length, remainingBytes);
            Reader.ReadBytes(readOffset, context.ReadBuffer.AsSpan(0, bytesToRead));
            AppendData(context.ReadBuffer.AsSpan(0, bytesToRead), context);

            readOffset += bytesToRead;
            remainingBytes -= bytesToRead;
            context.ProgData.Current += bytesToRead;
            context.Progress?.Report(context.ProgData);
        }

        return Task.CompletedTask;
    }

    private static void AppendData(ReadOnlySpan<byte> data, Context context)
    {
        while (!data.IsEmpty)
        {
            int bytesToCopy = Math.Min(ZAR.COMPRESSED_BLOCK_SIZE - context.CurrentBlockFill, data.Length);

            if (bytesToCopy == ZAR.COMPRESSED_BLOCK_SIZE && context.CurrentBlockFill == 0)
            {
                StoreBlock(data[..ZAR.COMPRESSED_BLOCK_SIZE], context);
                data = data[ZAR.COMPRESSED_BLOCK_SIZE..];
                continue;
            }

            data[..bytesToCopy].CopyTo(context.BlockBuffer.AsSpan(context.CurrentBlockFill));
            context.CurrentBlockFill += bytesToCopy;
            data = data[bytesToCopy..];

            if (context.CurrentBlockFill == ZAR.COMPRESSED_BLOCK_SIZE)
            {
                StoreBlock(context.BlockBuffer, context);
                context.CurrentBlockFill = 0;
            }
        }
    }

    private static void StoreBlock(ReadOnlySpan<byte> block, Context context)
    {
        if (block.Length != ZAR.COMPRESSED_BLOCK_SIZE)
            throw new InvalidOperationException("ZAR blocks must be exactly 64 KiB.");

        if ((context.WrittenBlockCount % ZAR.ENTRIES_PER_OFFSETRECORD) == 0)
        {
            if (context.WrittenBlockCount > 0)
                context.OffsetRecords.Add(new ZAR.CompressionOffsetRecord());

            context.CurrentOffsetRecord.BaseOffset = (ulong)context.Stream.Position;
        }

        byte[] compressed;
        using (var compressor = new Compressor(6))
            compressed = compressor.Wrap(block.ToArray()).ToArray();

        ReadOnlySpan<byte> outData = compressed.Length >= ZAR.COMPRESSED_BLOCK_SIZE ? block : compressed;

        if (!context.CurrentOffsetRecord.AddSize((ushort)(outData.Length - 1)))
            throw new InvalidOperationException("Failed to add compressed block size to the current ZAR offset record.");

        WriteBytes(outData, context);
        context.WrittenBlockCount++;
    }

    private static void WriteOffsetRecords(Context context)
    {
        context.Footer.SectionOffsetRecordsOffset = (ulong)context.Stream.Position;

        foreach (var record in context.OffsetRecords)
            WriteBytes(record.ToRaw().ToBytes(), context);

        context.Footer.SectionOffsetRecordsSize =
            (ulong)context.Stream.Position - context.Footer.SectionOffsetRecordsOffset;
    }

    private static void WriteNameTable(Context context)
    {
        context.Footer.SectionNamesOffset = (ulong)context.Stream.Position;
        context.NodeNameOffsets.Clear();

        uint currentOffset = 0;
        foreach (var name in context.NodeNames)
        {
            context.NodeNameOffsets.Add(currentOffset);

            var nameBytes = Encoding.UTF8.GetBytes(name);
            if (nameBytes.Length > 0x7FFF)
                throw new InvalidOperationException($"Node name '{name}' exceeds ZAR maximum encoded length.");

            if (nameBytes.Length >= 0x80)
            {
                byte[] header =
                {
                    (byte)((nameBytes.Length & 0x7F) | 0x80),
                    (byte)(nameBytes.Length >> 7)
                };
                WriteBytes(header, context);
                currentOffset += 2;
            }
            else
            {
                WriteBytes(new[] { (byte)(nameBytes.Length & 0x7F) }, context);
                currentOffset += 1;
            }

            WriteBytes(nameBytes, context);
            currentOffset += (uint)nameBytes.Length;
        }

        context.Footer.SectionNamesSize =
            (ulong)context.Stream.Position - context.Footer.SectionNamesOffset;
    }

    private static void WriteFileTree(PathNode rootNode, Context context)
    {
        AssignDirectoryNodeRanges(rootNode);

        context.Footer.SectionFileTreeOffset = (ulong)context.Stream.Position;
        foreach (var node in EnumerateNodes(rootNode, sortChildren: true))
        {
            var entry = new ZAR.FileDirectoryEntry();
            entry.IsFile = node.IsFile;
            entry.NameOffset = node.IsRoot ? 0x7FFFFFFF : context.NodeNameOffsets[(int)node.NameIndex];

            if (node.IsFile)
            {
                entry.FileOffset = node.FileOffset;
                entry.FileSize = node.Context?.Header.FileSize ?? 0u;
            }
            else
            {
                entry.DirectoryNodeStartIndex = node.NodeStartIndex;
                entry.DirectoryNodeCount = (uint)node.SubEntries.Count;
                entry.DirectoryReserved = 0;
            }

            WriteBytes(entry.ToBytes(), context);
        }

        context.Footer.SectionFileTreeSize =
            (ulong)context.Stream.Position - context.Footer.SectionFileTreeOffset;
    }

    private static void WriteMetaData(Context context)
    {
        context.Footer.SectionMetaDirectoryOffset = (ulong)context.Stream.Position;
        context.Footer.SectionMetaDirectorySize = 0;
        context.Footer.SectionMetaDataOffset = (ulong)context.Stream.Position;
        context.Footer.SectionMetaDataSize = 0;
    }

    private static void WriteFooter(Context context)
    {
        context.Footer.TotalSize = (ulong)context.Stream.Position + (ulong)context.Footer.Size();
        context.Footer.Version = ZAR.FOOTER_VERSION;
        context.Footer.Magic = ZAR.FOOTER_MAGIC;

        Array.Clear(context.Footer.IntegrityHash);
        var footerZeroHash = context.Footer.ToBytes();
        context.Sha256.AppendData(footerZeroHash);
        var hash = context.Sha256.GetHashAndReset();
        Array.Copy(hash, context.Footer.IntegrityHash, context.Footer.IntegrityHash.Length);

        context.Stream.Write(context.Footer.ToBytes());
    }

    private static void AlignOutput(Context context, int alignment)
    {
        while ((context.Stream.Position % alignment) != 0)
            WriteBytes(new byte[] { 0 }, context);
    }

    private void FlushPendingBlock(Context context)
    {
        if (context.CurrentBlockFill == 0)
            return;

        context.BlockBuffer.AsSpan(context.CurrentBlockFill).Clear();
        StoreBlock(context.BlockBuffer, context);
        context.CurrentBlockFill = 0;
    }

    private static void AssignDirectoryNodeRanges(PathNode rootNode)
    {
        var queue = new Queue<PathNode>();
        queue.Enqueue(rootNode);
        uint currentIndex = 1;

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();

            if (node.IsFile)
            {
                node.NodeStartIndex = 0xFFFFFFFF;
                continue;
            }

            var orderedChildren = node.SubEntries
                .Cast<PathNode>()
                .OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            node.NodeStartIndex = currentIndex;
            currentIndex += (uint)orderedChildren.Count;

            foreach (var child in orderedChildren)
                queue.Enqueue(child);
        }
    }

    private static IEnumerable<PathNode> EnumerateNodes(PathNode rootNode, bool sortChildren = false)
    {
        var queue = new Queue<PathNode>();
        queue.Enqueue(rootNode);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            yield return node;

            var children = sortChildren
                ? node.SubEntries.Cast<PathNode>().OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                : node.SubEntries.Cast<PathNode>();

            foreach (var child in children)
                queue.Enqueue(child);
        }
    }

    private static uint GetOrAddName(string name, Context context)
    {
        if (context.NodeNameLookup.TryGetValue(name, out var existingIndex))
            return existingIndex;

        uint index = (uint)context.NodeNames.Count;
        context.NodeNames.Add(name);
        context.NodeNameLookup.Add(name, index);
        return index;
    }

    private static void WriteBytes(ReadOnlySpan<byte> bytes, Context context)
    {
        context.Stream.Write(bytes);
        context.Sha256.AppendData(bytes);
    }

    public void CleanupCancelled()
    {
        try
        {
            if (File.Exists(OutputPath))
                File.Delete(OutputPath);
        }
        catch
        {
        }
    }
}
