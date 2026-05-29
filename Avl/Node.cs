using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDTool.Avl
{
    public class Node
    {
        public long DirectoryStart;
        public long DirectoryOffset;
        public String Filename = "";
        public long FileSize;
        public long StartSector;
        public long OldStartSector;
        public Skew Skew;
        public Node? Subdirectory = null;
        public Node? LeftChild = null;
        public Node? RightChild = null;
        public String Filepath = "";
    }

    public class EmptyNode : Node
    {

    }
}
