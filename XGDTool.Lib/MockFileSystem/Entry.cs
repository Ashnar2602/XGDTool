namespace XGDTool.Lib.MockFileSystem;

public class Entry<TSelf, TContext> where TSelf : Entry<TSelf, TContext>
{
    private TSelf? Parent;
    private readonly List<TSelf> _SubEntries = new();
    private TSelf Self
    {
        get
        {
            if (this is not TSelf self)
                throw new InvalidOperationException($"Entry of type {GetType().FullName} cannot be cast to {typeof(TSelf).FullName}.");

            return self;
        }
    }
    
    public bool IsRoot => Parent == null;
    public bool IsFile { get; init; }
    public bool IsDirectory => !IsFile;
    public string FileName { get; init; } = "";
    public TContext? Context { get; init; }
    public IReadOnlyList<TSelf> SubEntries => _SubEntries;

    public void AddSubEntry(TSelf entry)
    {
        if (IsFile)
            throw new InvalidOperationException("Cannot add subentry to a file.");

        entry.Parent = Self;
        _SubEntries.Add(entry);
    }

    public void RemoveSubEntry(TSelf entry)
    {
        if (IsFile)
            throw new InvalidOperationException("Cannot remove subentry from a file.");

        if (_SubEntries.Remove(entry))
            entry.Parent = null;
    }

    public string GetFullPath()
    {
        if (Parent == null)
            return FileName;

        return Path.Combine(Parent.GetFullPath(), FileName);
    }

    public string GetRelativePath()
    {
        if (Parent == null)
            return string.Empty;

        var parentRelativePath = Parent.GetRelativePath();
        return parentRelativePath == string.Empty ? FileName : Path.Combine(parentRelativePath, FileName);
    }
}
