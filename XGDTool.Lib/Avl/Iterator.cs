using XGDTool.Lib.Image.Format;

namespace XGDTool.Lib.Avl;

public class Iterator
{
    public class Entry
    {
        public required long Offset { get; init; }
        public required bool IsDirectoryEntry { get; init; }
        public required Node Node { get; init; }
    }

    private readonly Tree AvlTree;
    private List<Entry>? _Entries;

    public IReadOnlyList<Entry> Entries => _Entries ??= GetEntries();
    
    public Iterator(Tree avlTree)
    {
        AvlTree = avlTree;
    }

    private List<Entry> GetEntries()
    {
        var nodes = new List<Node>();
        var entries = new List<Entry>();    

        Tree.Traverse(
            Traversal.Prefix,
            AvlTree.RootNode.Subdirectory,
            0,
            CollectNodesCb,
            nodes);

        foreach (var node in nodes)
        {
            if (node.Subdirectory == null || node.Subdirectory is not EmptyNode)
            {
                entries.Add(new Entry()
                {
                    Offset = node.StartSector * XISO.SECTOR_SIZE,
                    IsDirectoryEntry = false,
                    Node = node
                });
            }
            else
            {
                entries.Add(new Entry()
                {
                    Offset = node.DirectoryStart + node.DirectoryOffset,
                    IsDirectoryEntry = true,
                    Node = node
                });
            }
        }

        entries.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        return entries;
    }

    private void CollectNodesCb(Node? node, int depth, List<Node>? nodes)
    {
        if (node == null || node is EmptyNode)
            return;

        nodes!.Add(node);

        if (node.Subdirectory != null)
            Tree.Traverse(Traversal.Prefix, node.Subdirectory, 0, CollectNodesCb, nodes);
    }

    public byte[] WriteDirectoriesToBuffer(int startIndex, out int count)
    {
        count = 0;

        int bufferPos = 0;
        var buffer = new byte[Entries.Count * (XISO.DIRECTORY_HEADER_SIZE + 256)];

        for (int i = startIndex; i < Entries.Count; i++)
        {
            var entryBytes = XISO.CreateDirectoryEntry(Entries[i].Node).ToBytes();
            int entryLen = entryBytes.Length;

            if (bufferPos + entryLen > buffer.Length)
                Array.Resize(ref buffer, buffer.Length + entryLen);

            Array.Copy(entryBytes, 0, buffer, bufferPos, entryLen);
            
            count++;
            bufferPos += entryLen;

            if (i == Entries.Count - 1 || Entries[i + 1].Node.DirectoryStart != Entries[i].Node.DirectoryStart)
                break;

            var padlen = (int)Entries[i + 1].Node.DirectoryOffset - bufferPos;

            if (padlen > 0)
                bufferPos += padlen;
        }

        if (bufferPos % XISO.SECTOR_SIZE != 0)
        {
            int padlen = XISO.SECTOR_SIZE - (bufferPos % XISO.SECTOR_SIZE);

            if (bufferPos + padlen > buffer.Length)
                Array.Resize(ref buffer, buffer.Length + padlen);

            Array.Fill(buffer, XISO.PAD_BYTE, bufferPos, padlen);
            bufferPos += padlen;
        }

        Array.Resize(ref buffer, bufferPos);
        return buffer;
    }
}
