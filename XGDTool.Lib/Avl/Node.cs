namespace XGDTool.Lib.Avl;

public class Node(string filename)
{
    public long DirectoryStart;
    public long DirectoryOffset;
    public string Filename = filename;
    public long FileSize;
    public long StartSector;
    public long OldStartSector;
    public Skew Skew = Skew.None;
    public Node? Subdirectory = null;
    public Node? LeftChild = null;
    public Node? RightChild = null;
    public string FilePath = "";
    public string SystemPath = "";
}

public class EmptySubdirectoryNode : Node
{
    public EmptySubdirectoryNode() : base(string.Empty) { }
}
