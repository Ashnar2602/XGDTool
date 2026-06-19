using System.Security.Cryptography;
using System.Text;
using ZstdSharp;
using XGDTool.Lib.Util;
using XGDTool.Lib.Exe;
using XGDTool.Lib.Image.Formats;

namespace XGDTool.Lib.Image.Writers;

internal class Zar(IReader reader, IWriterOptions options, Title.Info titleInfo) : IWriter
{
    private sealed class PathNode
    {
        public required string FileName;
        public required string FilePath;
        public bool IsFile;
        public PathNode? Parent;
        public DirectoryEntryExt? Context;
        public List<PathNode> SubEntries = [];
        public uint NameIndex;
        public ulong FileOffset;
        public uint NodeStartIndex;

        public bool IsRoot => Parent == null;

        public void AddSubEntry(PathNode entry)
        {
            entry.Parent = this;
            SubEntries.Add(entry);
        }
    }

    private class Context
    {
        public Converter.Progress ProgData = new();
        public IProgress<Converter.Progress>? Progress;
        public CancellationToken Ct = default;
        public required FileStream Stream;
        public required PathNode RootNode;
        public readonly byte[] ReadBuffer = new byte[512 * XDVDFS.SECTOR_SIZE];
        public readonly byte[] BlockBuffer = new byte[ZAR.COMPRESSED_BLOCK_SIZE];
        public IncrementalHash Sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        public List<ZAR.CompressedOffsetRecord> OffsetRecords = [ new ZAR.CompressedOffsetRecord() ];
        public ZAR.CompressedOffsetRecord CurrentOffsetRecord => OffsetRecords.Last();
        public readonly List<string> NodeNames = [];
        public readonly Dictionary<string, uint> NodeNameLookup = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<uint> NodeNameOffsets = [];
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

        var dirEntries = Reader.DirectoryEntries;
        if (Options.SkipSystemUpdate == true && Reader.Platform == Platform.Xbox360)
        {
            dirEntries.RemoveAll(e => e.FilePath.StartsWith(
                XDVDFS.SYSTEM_UPDATE_DIRECTORY_NAME, 
                StringComparison.OrdinalIgnoreCase));
        }

        var rootNode = BuildTreeFromDirectoryEntries(dirEntries);

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

        context.Footer.CompressedData.Offset = 0;
        context.Footer.CompressedData.Length = (ulong)context.Stream.Position;
        
        WriteOffsetRecords(context);
        WriteNameTable(context);
        WriteFileTree(rootNode, context);
        WriteMetaData(context);
        WriteFooter(context);

        await context.Stream.FlushAsync(ct);
        context.Stream.Dispose();

        return [ OutputPath ];
    }

    private static void AssignNodeData(PathNode rootNode, Context context)
    {
        foreach (var node in EnumerateNodes(rootNode).Skip(1))
        {
            node.NameIndex = GetOrAddName(node.FileName, context);

            if (node.IsFile)
            {
                node.FileOffset = context.CurrentInputOffset;
                context.CurrentInputOffset += (ulong)(node.Context?.FileSize ?? 0);
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
            throw new InvalidOperationException($"File node '{node.FileName}' is missing reader context.");

        long readOffset = Reader.ImageOffset + (node.Context.StartSector * XDVDFS.SECTOR_SIZE);
        long remainingBytes = node.Context.FileSize;

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

        if ((context.WrittenBlockCount % ZAR.CompressedOffsetRecord.ENTRIES_MAX) == 0)
        {
            if (context.WrittenBlockCount > 0)
                context.OffsetRecords.Add(new ZAR.CompressedOffsetRecord());

            context.CurrentOffsetRecord.BaseOffset = (ulong)context.Stream.Position;
        }

        byte[] compressed;
        using (var compressor = new Compressor(6))
            compressed = compressor.Wrap(block.ToArray()).ToArray();

        ReadOnlySpan<byte> outData = compressed.Length >= ZAR.COMPRESSED_BLOCK_SIZE 
            ? block 
            : compressed;

        if (!context.CurrentOffsetRecord.AddSize((ushort)(outData.Length - 1)))
            throw new InvalidOperationException("Failed to add compressed block size to the current ZAR offset record.");

        WriteBytes(outData, context);
        context.WrittenBlockCount++;
    }

    private static void WriteOffsetRecords(Context context)
    {
        context.Footer.OffsetRecords.Offset = (ulong)context.Stream.Position;

        foreach (var record in context.OffsetRecords)
            WriteBytes(record.Serialize(), context);

        context.Footer.OffsetRecords.Length =
            (ulong)context.Stream.Position - context.Footer.OffsetRecords.Offset;
    }

    private static void WriteNameTable(Context context)
    {
        context.Footer.Names.Offset = (ulong)context.Stream.Position;
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
                [
                    (byte)((nameBytes.Length & 0x7F) | 0x80),
                    (byte)(nameBytes.Length >> 7)
                ];
                WriteBytes(header, context);
                currentOffset += 2;
            }
            else
            {
                WriteBytes([ (byte)(nameBytes.Length & 0x7F) ], context);
                currentOffset += 1;
            }

            WriteBytes(nameBytes, context);
            currentOffset += (uint)nameBytes.Length;
        }

        context.Footer.Names.Length =
            (ulong)context.Stream.Position - context.Footer.Names.Offset;
    }

    private static void WriteFileTree(PathNode rootNode, Context context)
    {
        AssignDirectoryNodeRanges(rootNode);

        context.Footer.FileTree.Offset = (ulong)context.Stream.Position;
        foreach (var node in EnumerateNodes(rootNode, sortChildren: true))
        {
            ZAR.PathEntry? entry;
            var nameOffset = node.IsRoot ? 0x7FFFFFFF : context.NodeNameOffsets[(int)node.NameIndex];

            if (node.IsFile)
            {
                entry = new ZAR.FileEntry()
                {
                    NameOffset = nameOffset,
                    FileOffset = node.FileOffset,
                    FileSize = (ulong)(node.Context?.FileSize ?? 0)
                };
            }
            else
            {
                entry = new ZAR.DirectoryEntry()
                {
                    NameOffset = nameOffset,
                    NodeStartIndex = node.NodeStartIndex,
                    NodeCount = (uint)node.SubEntries.Count
                };
            }

            WriteBytes(entry.Serialize(), context);
        }

        context.Footer.FileTree.Length =
            (ulong)context.Stream.Position - context.Footer.FileTree.Offset;
    }

    private static void WriteMetaData(Context context)
    {
        context.Footer.MetaDirectory.Offset = (ulong)context.Stream.Position;
        context.Footer.MetaDirectory.Length = 0;
        context.Footer.MetaData.Offset = (ulong)context.Stream.Position;
        context.Footer.MetaData.Length = 0;
    }

    private static void WriteFooter(Context context)
    {
        context.Footer.TotalSize = (ulong)context.Stream.Position + ZAR.Footer.SIZE;
        context.Footer.Version = ZAR.Footer.VERSION;
        context.Footer.Magic = ZAR.Footer.MAGIC;

        Array.Clear(context.Footer.IntegrityHash);
        var footerZeroHash = context.Footer.Serialize();
        context.Sha256.AppendData(footerZeroHash);
        var hash = context.Sha256.GetHashAndReset();
        Array.Copy(hash, context.Footer.IntegrityHash, context.Footer.IntegrityHash.Length);

        context.Stream.Write(context.Footer.Serialize());
    }

    private static void AlignOutput(Context context, int alignment)
    {
        while ((context.Stream.Position % alignment) != 0)
            WriteBytes([ 0 ], context);
    }

    private static void FlushPendingBlock(Context context)
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
                .OrderBy(n => n.FileName, StringComparer.OrdinalIgnoreCase)
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
                ? node.SubEntries.Cast<PathNode>().OrderBy(n => n.FileName, StringComparer.OrdinalIgnoreCase)
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

    private static PathNode BuildTreeFromDirectoryEntries(IReadOnlyList<DirectoryEntryExt> directoryEntries)
    {
        var root = new PathNode
        {
            FileName = string.Empty,
            FilePath = string.Empty,
            IsFile = false,
            Context = null
        };

        var nodesByPath = new Dictionary<string, PathNode>(StringComparer.OrdinalIgnoreCase)
        {
            [string.Empty] = root
        };

        var eEntries = directoryEntries
            .OrderBy(e => e.FilePath.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar))
            .ThenBy(e => e.FilePath, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in eEntries)
        {
            var normalizedPath = NormalizePath(entry.FilePath);
            if (string.IsNullOrEmpty(normalizedPath))
                continue;

            var isFile = !entry.Attributes.HasFlag(XDVDFS.DirAttributes.Directory);
            if (isFile && entry.FileSize <= 0)
                continue;

            if (nodesByPath.ContainsKey(normalizedPath))
                continue;

            var parentPath = GetParentPath(normalizedPath);
            var parentNode = EnsureDirectoryNode(parentPath, nodesByPath, root);

            var node = new PathNode
            {
                FileName = string.IsNullOrWhiteSpace(entry.FileName) 
                    ? Path.GetFileName(normalizedPath) 
                    : entry.FileName,
                FilePath = normalizedPath,
                IsFile = isFile,
                Context = entry
            };

            parentNode.AddSubEntry(node);
            nodesByPath[normalizedPath] = node;
        }

        return root;
    }

    private static PathNode EnsureDirectoryNode(string path, Dictionary<string, PathNode> nodesByPath, PathNode root)
    {
        var normalized = NormalizePath(path);
        if (nodesByPath.TryGetValue(normalized, out var existing))
            return existing;

        if (string.IsNullOrEmpty(normalized))
            return root;

        var parentPath = GetParentPath(normalized);
        var parent = EnsureDirectoryNode(parentPath, nodesByPath, root);

        var dirName = Path.GetFileName(normalized);
        var syntheticDir = new PathNode
        {
            FileName = dirName,
            FilePath = normalized,
            IsFile = false,
            Context = new DirectoryEntryExt
            {
                FileName = dirName,
                FilePath = normalized,
                Attributes = XDVDFS.DirAttributes.Directory,
                FileSize = 0
            }
        };

        parent.AddSubEntry(syntheticDir);
        nodesByPath[normalized] = syntheticDir;
        return syntheticDir;
    }

    private static string NormalizePath(string path) =>
        path.Replace(
                Path.AltDirectorySeparatorChar, 
                Path.DirectorySeparatorChar)
            .Trim(Path.DirectorySeparatorChar);

    private static string GetParentPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        var parent = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(parent) || parent == ".")
            return string.Empty;

        return NormalizePath(parent);
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
