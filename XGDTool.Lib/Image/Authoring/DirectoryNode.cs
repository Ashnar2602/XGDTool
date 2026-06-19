namespace XGDTool.Lib.Image.Authoring;

public class DirectoryNode(DirectoryEntryExt oldEntry) : Avl.Node<DirectoryNode>
{
    private readonly DirectoryEntryExt _OldEntry = oldEntry;
    
    public DirectoryEntryExt OldEntry => _OldEntry;
    public long StartSector;
    public long FileSize;
    public long DirectoryStart;
    public long DirectoryOffset;
    public string FilePath => OldEntry.FilePath;
    public string FileName => OldEntry.FileName;

    public override bool IsEmptyNode => this is EmptyDirectoryNode;
    public override int CompareTo(DirectoryNode otherNode) => 
        string.Compare(OldEntry.FileName, otherNode.OldEntry.FileName, StringComparison.OrdinalIgnoreCase);
}

public class RootDirectoryNode : DirectoryNode
{
    public override int CompareTo(DirectoryNode otherNode) => -1;
    public RootDirectoryNode() : base(new DirectoryEntryExt()) { }
}

public class EmptyDirectoryNode : DirectoryNode
{
    public override int CompareTo(DirectoryNode otherNode) => -1;
    public EmptyDirectoryNode() : base(new DirectoryEntryExt()) { }
}

public sealed record DirectoryRecord(DirectoryNode Node, long AbsoluteOffset, bool IsDirectoryEntry);
