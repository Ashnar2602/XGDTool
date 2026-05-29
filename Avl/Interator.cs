using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDTool.Format;

namespace XGDTool.Avl
{
    public class Interator
    {
        public class Entry
        {
            public readonly long Offset;
            public readonly bool DirectoryEntry;
            public readonly Node Node;

            public Entry(long offset, bool directoryEntry, Node node)
            {
                Offset = offset;
                DirectoryEntry = directoryEntry;
                Node = node;
            }
        }

        public List<Entry> Entries { get; private set; } = new();

        public Interator(Tree tree)
        {
            var nodes = new List<Node>();

            Tree.Traverse(
                Traversal.Prefix, 
                tree.RootNode.Subdirectory, 
                0, 
                CollectNodes, 
                nodes);

            foreach (var node in nodes)
            {
                if (node.Subdirectory == null || node.Subdirectory is not EmptyNode)
                {
                    Entries.Add(new Entry(node.StartSector * XISO.SECTOR_SIZE, false, node));
                }
                else
                {
                    Entries.Add(new Entry(node.DirectoryStart + node.DirectoryOffset, true, node));
                }
            }

            Entries.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        }

        private void CollectNodes(Node? node, int depth, List<Node>? nodes)
        {
            if (node == null || node is EmptyNode)
                return;

            nodes!.Add(node);

            if (node.Subdirectory != null)
            {
                Tree.Traverse(Traversal.Prefix, node.Subdirectory, 0, CollectNodes, nodes);
            }
        }
    }
}
