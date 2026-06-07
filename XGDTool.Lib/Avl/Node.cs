using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDTool.Lib.Avl
{
    public class Node
    {
        public long DirectoryStart;
        public long DirectoryOffset;
        public string Filename = "";
        public long FileSize;
        public long StartSector;
        public long OldStartSector;
        public Skew Skew = Skew.None;
        public Node? Subdirectory = null;
        public Node? LeftChild = null;
        public Node? RightChild = null;
        public string Filepath = "";

        public Node(string filename)
        {
            Filename = filename;
        }
    }

    public class EmptyNode : Node
    {
        public EmptyNode() : base(string.Empty) { }
    }
}
