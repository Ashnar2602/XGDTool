using System.Text;
using XGDTool.Lib.Avl;
using XGDTool.Lib.Util;
using XGDTool.Lib.Image.Formats;

namespace XGDTool.Lib.Image.Authoring;

public class XDvdFsAuthorer()
{
    private sealed class AssignOffsetsContext
    {
        public long DirectoryStart;
        public long CurrentSector;
    }

    private const int MaxRecursionDepth = 100;
    private List<DirectoryRecord> _EntryRecordList = [];
    private DirectoryNode _RootNode = new RootDirectoryNode();
    
    public const uint DefaultRootSector = 0x108;
    public long TotalFileBytes { get; private set; }
    public long TotalFiles { get; private set; }
    public long TotalXisoBytes { get; private set; }
    public uint TotalXisoSectors => XDVDFS.SectorCount(TotalXisoBytes);
    public IReadOnlyList<DirectoryRecord> EntryRecordList => _EntryRecordList;
    public uint RootStartSector => (uint)_RootNode.StartSector;
    public uint RootSize => (uint)_RootNode.FileSize;
    public DirectoryNode RootNode => _RootNode;

    public void CreateTree(IReadOnlyList<DirectoryEntryExt> entries, uint rootSector = DefaultRootSector, int maxDepth = MaxRecursionDepth)
    {
        if (rootSector <= XDVDFS.VOLUME_DESCRIPTOR_SECTOR + 1)
            throw new ArgumentException(
                $"Root sector must be greater than {XDVDFS.VOLUME_DESCRIPTOR_SECTOR + 1} to avoid overwriting volume descriptors.", 
                nameof(rootSector));

        void RecurseEntries(ref Queue<DirectoryEntryExt> queue, ref DirectoryNode? parent, int depth)
        {
            if (depth > maxDepth)
                throw new InvalidOperationException($"Maximum recursion depth of {maxDepth} exceeded.");

            while (queue.Count > 0)
            {
                var entry = queue.Dequeue();
                var newNode = new DirectoryNode(entry.Clone())
                {
                    FileSize = entry.FileSize
                };

                if (entry.Attributes.HasFlag(XDVDFS.DirAttributes.Directory))
                {
                    var subQueue = new Queue<DirectoryEntryExt>();
                    var remainingQueue = new Queue<DirectoryEntryExt>();
                    var currentPath = entry.FilePath;

                    while (queue.Count > 0)
                    {
                        var nextEntry = queue.Dequeue();
                        if (nextEntry.FilePath.StartsWith(currentPath, StringComparison.OrdinalIgnoreCase) && 
                            !currentPath.Equals(nextEntry.FilePath, StringComparison.OrdinalIgnoreCase)) 
                        {
                            subQueue.Enqueue(nextEntry);
                        }
                        else 
                        {
                            remainingQueue.Enqueue(nextEntry);
                        }
                    }

                    queue = remainingQueue;

                    if (subQueue.Count > 0)
                        RecurseEntries(ref subQueue, ref newNode.SubDirectory, depth + 1);

                    newNode.SubDirectory ??= new EmptyDirectoryNode();
                }
                else if (entry.FileSize > 0)
                {
                    TotalFileBytes += entry.FileSize;
                    TotalFiles++;
                }
                else
                {
                    continue;
                }

                if (Tree<DirectoryNode>.InsertNode(ref parent, newNode) == InsertResult.Error)
                    throw new InvalidOperationException(
                        $"Failed to insert node for entry '{entry.FilePath}' into AVL tree.");
            }
        }

        TotalFiles = 0;
        TotalFileBytes = 0;

        _RootNode = new RootDirectoryNode() 
        {
            StartSector = rootSector
        };
        {
            var directoryQueue = new Queue<DirectoryEntryExt>(entries);
            RecurseEntries(ref directoryQueue, ref _RootNode.SubDirectory, 0);
        }

        ResolveOffsets();
        PopulateRecords();

        TotalXisoBytes = CalculateNewImageSize();
    }

    private void ResolveOffsets()
    {
        long startSector = _RootNode.StartSector;
        ref var root = ref _RootNode;
        long _ = 0;

        Traversal<DirectoryNode, long>.Traverse(
            TraverseOrder.Prefix, ref root, CalculateDirectoryRequirementsCb, ref _);

        Traversal<DirectoryNode, long>.Traverse(
            TraverseOrder.Prefix, ref root, CalculateDirectoryOffsetsCb, ref startSector);
    }

    private void CalculateDirectoryRequirementsCb(ref DirectoryNode node, ref long _, int depth)
    { 
        if (node.SubDirectory == null)
            return;

        if (node.SubDirectory is not EmptyDirectoryNode)
        {
            Traversal<DirectoryNode, long>.Traverse(
                TraverseOrder.Prefix, ref node.SubDirectory, CalculateDirectorySizeCb, ref node.FileSize);
            
            Traversal<DirectoryNode, long>.Traverse(
                TraverseOrder.Prefix, ref node.SubDirectory, CalculateDirectoryRequirementsCb, ref _);
        }
        else
        {
            node.FileSize = XDVDFS.SECTOR_SIZE;
        }
    }

    private void CalculateDirectorySizeCb(ref DirectoryNode node, ref long outSize, int depth)
    {
        if (depth == 0)
            outSize = 0;

        var nameLength = Encoding.ASCII.GetByteCount(node.OldEntry.FileName);
        var length = XDVDFS.DirectoryEntry.HEADER_SIZE + nameLength;

        length += (sizeof(uint) - (length % sizeof(uint))) % sizeof(uint);

        if (XDVDFS.SectorCount(outSize + length) > XDVDFS.SectorCount(outSize))
            outSize += (XDVDFS.SECTOR_SIZE - (outSize % XDVDFS.SECTOR_SIZE)) % XDVDFS.SECTOR_SIZE;

        node.DirectoryOffset = outSize;
        outSize += length;
    }

    private void CalculateDirectoryOffsetsCb(ref DirectoryNode node, ref long currentSector, int depth)
    {
        if (node.SubDirectory == null)
            return;

        if (node.SubDirectory is EmptyDirectoryNode)
        {
            node.StartSector = currentSector;
            currentSector++;
        }
        else
        {
            node.StartSector = currentSector;
            currentSector += XDVDFS.SectorCount(node.FileSize);

            var aoContext = new AssignOffsetsContext
            {
                DirectoryStart = node.StartSector * XDVDFS.SECTOR_SIZE,
                CurrentSector = currentSector
            };

            Traversal<DirectoryNode, AssignOffsetsContext>.Traverse(
                TraverseOrder.Prefix, ref node.SubDirectory, AssignOffsetsCb, ref aoContext);
            currentSector = aoContext.CurrentSector;

            Traversal<DirectoryNode, long>.Traverse(
                TraverseOrder.Prefix, ref node.SubDirectory, CalculateDirectoryOffsetsCb, ref currentSector);
        }
    }

    private void AssignOffsetsCb(ref DirectoryNode node, ref AssignOffsetsContext context, int depth)
    {
        node.DirectoryStart = context.DirectoryStart;

        if (node.SubDirectory == null)
        {
            node.StartSector = context.CurrentSector;
            context.CurrentSector += XDVDFS.SectorCount(node.FileSize);
        }
    }

    private void PopulateRecords()
    {
        ref var root = ref _RootNode;

        _EntryRecordList.Clear();

        if (root is null || root.SubDirectory == null)
            throw new InvalidOperationException("Root node has no subdirectory.");

        Traversal<DirectoryNode, List<DirectoryRecord>>.Traverse(
            TraverseOrder.Prefix, ref root.SubDirectory, CollectAndVerifyNodesCb, ref _EntryRecordList);

        _EntryRecordList.Sort((a, b) => a.AbsoluteOffset.CompareTo(b.AbsoluteOffset));
    }

    private void CollectAndVerifyNodesCb(ref DirectoryNode node, ref List<DirectoryRecord> list, int depth)
    {
        if (node is EmptyDirectoryNode)
            return;

        if (node.FileSize > uint.MaxValue)
        {
            throw new InvalidOperationException(
                $"File '{node.OldEntry.FileName}' exceeds the maximum allowed size of 4GB for ISO files.");
        }
        if (node.StartSector > uint.MaxValue)
        {
            throw new InvalidOperationException(
                $"Node '{node.OldEntry.FileName}' has a starting sector that exceeds the maximum allowed value of 4GB / 2048 bytes per sector for ISO files.");
        }

        if (node.SubDirectory == null) 
        {
            list.Add(new DirectoryRecord(
                node, 
                node.StartSector * XDVDFS.SECTOR_SIZE, 
                false));
        }

        list.Add(new DirectoryRecord(
            node, 
            node.DirectoryStart + node.DirectoryOffset, 
            true));

        if (node.SubDirectory != null)
            Traversal<DirectoryNode, List<DirectoryRecord>>.Traverse(
                TraverseOrder.Prefix, ref node.SubDirectory, CollectAndVerifyNodesCb, ref list);
    }

    public static byte[] SerializeDirectoryEntry(DirectoryNode node)
    {
        var entry = new XDVDFS.DirectoryEntry();
        var isEmpty = node.SubDirectory is EmptyDirectoryNode;

        entry.LeftOffset = (node.LeftChild != null)
            ? (ushort)(node.LeftChild.DirectoryOffset / sizeof(uint))
            : (ushort)0;
        entry.RightOffset = (node.RightChild != null)
            ? (ushort)(node.RightChild.DirectoryOffset / sizeof(uint))
            : (ushort)0;
        entry.StartSector = (uint)node.StartSector;
        
        if (node.SubDirectory != null || isEmpty)
        {
            entry.FileSize =
                (uint)node.FileSize +
                (uint)((XDVDFS.SECTOR_SIZE - (node.FileSize % XDVDFS.SECTOR_SIZE)) % XDVDFS.SECTOR_SIZE);
            entry.Attributes = XDVDFS.DirAttributes.Directory;
        }
        else
        {
            entry.FileSize = (uint)node.FileSize;
            entry.Attributes = XDVDFS.DirAttributes.File;
        }

        entry.SetName(node.OldEntry.FileName);
        return entry.Serialize();
    }

    public byte[] SerializeDirectoryEntryRange(int startIndex, out int serializedCount)
    {
        if (startIndex < 0 || startIndex >= EntryRecordList.Count)
            throw new ArgumentOutOfRangeException(nameof(startIndex), "Start index is out of range of the record list.");
        
        serializedCount = 0;

        int bufferPos = 0;
        int initialCount = Math.Min(EntryRecordList.Count - startIndex, 100);
        var buffer = new byte[initialCount * (XDVDFS.DirectoryEntry.HEADER_SIZE + 256)];

        for (int i = startIndex; i < EntryRecordList.Count; i++)
        {
            if (!EntryRecordList[i].IsDirectoryEntry)
                break;

            var entryBytes = SerializeDirectoryEntry(EntryRecordList[i].Node);
            int entryLen = entryBytes.Length;

            if (bufferPos + entryLen > buffer.Length)
                Array.Resize(ref buffer, buffer.Length + entryLen);

            Array.Copy(entryBytes, 0, buffer, bufferPos, entryLen);

            serializedCount++;
            bufferPos += entryLen;

            if (i == (EntryRecordList.Count - 1) || !EntryRecordList[i + 1].IsDirectoryEntry ||
                EntryRecordList[i + 1].Node.DirectoryStart != EntryRecordList[i].Node.DirectoryStart)
                break;

            var padlen = (int)EntryRecordList[i + 1].Node.DirectoryOffset - bufferPos;

            if (padlen > 0)
            {
                if (bufferPos + padlen > buffer.Length)
                    Array.Resize(ref buffer, bufferPos + padlen);

                Array.Fill(buffer, XDVDFS.PAD_BYTE, bufferPos, padlen);
                bufferPos += padlen;
            }
        }

        if (!XDVDFS.IsSectorAligned(bufferPos))
        {
            int padlen = XDVDFS.SECTOR_SIZE - (bufferPos % XDVDFS.SECTOR_SIZE);

            if (bufferPos + padlen > buffer.Length)
                Array.Resize(ref buffer, buffer.Length + padlen);

            Array.Fill(buffer, XDVDFS.PAD_BYTE, bufferPos, padlen);
            bufferPos += padlen;
        }

        if (bufferPos != buffer.Length)
            Array.Resize(ref buffer, bufferPos);

        return buffer;
    }

    private long CalculateNewImageSize()
    {
        if (EntryRecordList.Count == 0)
            throw new InvalidOperationException("No files have been added to the image.");

        var lastRecord = EntryRecordList[^1];
        long entrySize;

        if (lastRecord.IsDirectoryEntry)
        {
            entrySize = 
                XDVDFS.DirectoryEntry.HEADER_SIZE + 
                Encoding.ASCII.GetByteCount(lastRecord.Node.OldEntry.FileName);
        }
        else
        {
            entrySize = lastRecord.Node.FileSize;
        }

        return XDVDFS.SectorCount(lastRecord.AbsoluteOffset + entrySize) * XDVDFS.SECTOR_SIZE;
    }
}
