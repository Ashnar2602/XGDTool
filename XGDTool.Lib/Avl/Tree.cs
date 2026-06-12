using System.Diagnostics.CodeAnalysis;
using System.Text;
using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.MockFileSystem;

namespace XGDTool.Lib.Avl;

public class Tree(string name = "Root")
{
    private enum Result
    {
        Balanced,
        NoError,
        Error
    }

    private sealed class AssignOffsetsContext
    {
        public long DirectoryStart;
        public long CurrentSector;
    }

    private delegate void PrivateTraversalCb<TContext>(ref Node node, int depth, ref TContext context);

    public delegate void TraversalCallback<TContext>(Node? node, int depth, ref TContext context);

    private Node? _RootNode = null;
    private const int MAX_RECURSE_DEPTH = 200;

    public Node RootNode => _RootNode ?? throw new InvalidOperationException("Root node has not been initialized.");
    public string RootName { get; init; } = name;
    public long TotalBytes { get; private set; }
    public long TotalFiles  { get; private set; }

    public void BuildTree(IReadOnlyList<Image.Reader.DirectoryEntry> entries)
    {
        _RootNode = new Node(RootName);
        _RootNode.StartSector = XISO.ROOT_DIRECTORY_SECTOR;
        TotalBytes = 0;
        TotalFiles = 0;
        GenerateFromEntries(entries);
        ResolveOffsets();
    }

    public void BuildTree<T>(Entry<T, Image.Reader.DirectoryEntry> root) 
        where T : Entry<T, Image.Reader.DirectoryEntry>
    {
        _RootNode = new Node(RootName);
        _RootNode.StartSector = XISO.ROOT_DIRECTORY_SECTOR;
        TotalBytes = 0;
        TotalFiles = 0;
        GenerateFromMockFileSystem(root);
        ResolveOffsets();
    }

    public void BuildTree(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The specified root directory '{rootDirectory}' does not exist.");
        }
        _RootNode = new Node(RootName);
        _RootNode.StartSector = XISO.ROOT_DIRECTORY_SECTOR;
        TotalBytes = 0;
        TotalFiles = 0;
        GenerateFromDirectory(rootDirectory);
        ResolveOffsets();
    }

    public static bool NodeExists([NotNullWhen(true)] Node? node) => 
        ((node != null) && (node is not EmptySubdirectoryNode));

    public static void Traverse<TContext>
    (
        Traversal traversal,
        Node? node,
        int depth,
        TraversalCallback<TContext> callback,
        ref TContext context
    )
    {
        if (node == null || node is EmptySubdirectoryNode)
            return;

        switch (traversal)
        {
            case Traversal.Prefix:
                callback(node, depth, ref context);
                Traverse(traversal, node.LeftChild, depth + 1, callback, ref context);
                Traverse(traversal, node.RightChild, depth + 1, callback, ref context);
                break;
            case Traversal.Infix:
                Traverse(traversal, node.LeftChild, depth + 1, callback, ref context);
                callback(node, depth, ref context);
                Traverse(traversal, node.RightChild, depth + 1, callback, ref context);
                break;
            case Traversal.Postfix:
                Traverse(traversal, node.LeftChild, depth + 1, callback, ref context);
                Traverse(traversal, node.RightChild, depth + 1, callback, ref context);
                callback(node, depth, ref context);
                break;
        }
    }

    private void GenerateFromEntries(IReadOnlyList<Image.Reader.DirectoryEntry> entries)
    {
        var queue = new Queue<Image.Reader.DirectoryEntry>(entries);
        RecurseEntries(ref queue, ref RootNode.Subdirectory, 0);
    }

    private void GenerateFromMockFileSystem<T>(Entry<T, Image.Reader.DirectoryEntry> root) 
        where T : Entry<T, Image.Reader.DirectoryEntry> => 
            RecurseMockFileSystem(root, ref RootNode.Subdirectory, 0);

    private void GenerateFromDirectory(string dirPath) => 
        RecurseDirectories(dirPath, dirPath, ref RootNode.Subdirectory, 0);

    private void RecurseMockFileSystem<T>(Entry<T, Image.Reader.DirectoryEntry> parent, ref Node? dirNode, int depth) 
        where T : Entry<T, Image.Reader.DirectoryEntry>
    {
        if (depth > MAX_RECURSE_DEPTH)
        {
            throw new InvalidOperationException(
                $"Maximum recursion depth of {MAX_RECURSE_DEPTH} exceeded while processing mock filesystem entries.");
        }

        foreach (var entry in parent.SubEntries)
        {
            var node = new Node(entry.FileName)
            {
                FilePath = entry.GetRelativePath(),
                SystemPath = entry.GetFullPath()
            };

            if (entry.IsDirectory)
            {
                RecurseMockFileSystem(entry, ref node.Subdirectory, depth + 1);
                node.Subdirectory ??= new EmptySubdirectoryNode();
            }
            else
            {
                var context = entry.Context;
                if (context == null || context.Header.FileSize == 0)
                    continue;

                node.FileSize = context.Header.FileSize;
                node.OldStartSector = context.Header.StartSector;
                node.FilePath = context.FilePath;
                TotalBytes += context.Header.FileSize;
                TotalFiles++;
            }

            if (InsertNode(ref dirNode, node) == Result.Error)
            {
                throw new InvalidOperationException(
                    $"Failed to insert node with filename '{node.Filename}' into the AVL tree.");
            }
        }
    }

    private void RecurseEntries(ref Queue<Image.Reader.DirectoryEntry> queue, ref Node? dirNode, int depth)
    {
        if (depth > MAX_RECURSE_DEPTH)
        {
            throw new InvalidOperationException(
                $"Maximum recursion depth of {MAX_RECURSE_DEPTH} exceeded while processing directory entries.");
        }
        while (queue.Count > 0)
        {
            var entry = queue.Dequeue();
            var node = new Node(entry.GetName()) 
            {
                OldStartSector = entry.Header.StartSector,
                FileSize = entry.Header.FileSize,
                FilePath = entry.FilePath
            };

            if (entry.Header.Attributes.HasFlag(XISO.DirAttribute.Directory))
            {
                var subEntries = new Queue<Image.Reader.DirectoryEntry>();
                var remainingQueue = new Queue<Image.Reader.DirectoryEntry>();
                var currentPath = entry.FilePath;

                while (queue.Count > 0)
                {
                    var item = queue.Dequeue();
                    if (item.FilePath.StartsWith(currentPath, StringComparison.OrdinalIgnoreCase) &&
                        !currentPath.Equals(item.FilePath))
                    {
                        subEntries.Enqueue(item);
                    }
                    else
                    {
                        remainingQueue.Enqueue(item);
                    }
                }

                queue = remainingQueue;

                if (subEntries.Count > 0)
                    RecurseEntries(ref subEntries, ref node.Subdirectory, depth + 1);

                node.Subdirectory ??= new EmptySubdirectoryNode();
            }
            else if (entry.Header.FileSize > 0)
            {
                TotalBytes += entry.Header.FileSize;
                TotalFiles++;
            }
            else
            {
                continue;
            }

            if (InsertNode(ref dirNode, node) == Result.Error)
            {
                throw new InvalidOperationException(
                    $"Failed to insert node with filename '{node.Filename}' into the AVL tree.");
            }
        }
    }

    private void RecurseDirectories(string rootPath, string dirPath, ref Node? dirNode, int depth)
    {
        if (depth > MAX_RECURSE_DEPTH)
        {
            throw new InvalidOperationException(
                $"Maximum recursion depth of {MAX_RECURSE_DEPTH} exceeded while processing directory '{dirPath}'.");
        }

        if (!Directory.Exists(dirPath))
        {
            throw new DirectoryNotFoundException(
                $"The specified directory '{dirPath}' does not exist.");
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(dirPath))
        {
            var dirInfo = new DirectoryInfo(entry);
            var node = new Node(dirInfo.Name);

            node.FilePath = dirInfo.FullName
                .Substring(rootPath.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            node.SystemPath = dirInfo.FullName;

            if (Directory.Exists(entry))
            {
                RecurseDirectories(rootPath, entry, ref node.Subdirectory, depth + 1);
                node.Subdirectory ??= new EmptySubdirectoryNode();
            }
            else if (File.Exists(entry))
            {
                var fileInfo = new FileInfo(entry);

                if (fileInfo.Length > uint.MaxValue)
                {
                    throw new InvalidOperationException(
                        $"File '{fileInfo.FullName}' exceeds the maximum allowed size of {uint.MaxValue} bytes for ISO files.");
                }

                node.FileSize = fileInfo.Length;
                TotalBytes += fileInfo.Length;
                TotalFiles++;
            }
            else
            {
                continue;
            }

            if (InsertNode(ref dirNode, node) == Result.Error)
            {
                throw new InvalidOperationException(
                    $"Failed to insert node with filename '{node.Filename}' into the AVL tree.");
            }
        }
    }

    private void Traverse<TContext>
    (
        Traversal traversal,
        ref Node? node,
        int depth,
        PrivateTraversalCb<TContext> callback,
        ref TContext context
    )
    {
        if (node == null || node is EmptySubdirectoryNode)
            return;

        switch (traversal)
        {
            case Traversal.Prefix:
                callback(ref node, depth, ref context);
                Traverse(traversal, ref node.LeftChild, depth + 1, callback, ref context);
                Traverse(traversal, ref node.RightChild, depth + 1, callback, ref context);
                break;
            case Traversal.Infix:
                Traverse(traversal, ref node.LeftChild, depth + 1, callback, ref context);
                callback(ref node, depth, ref context);
                Traverse(traversal, ref node.RightChild, depth + 1, callback, ref context);
                break;
            case Traversal.Postfix:
                Traverse(traversal, ref node.LeftChild, depth + 1, callback, ref context);
                Traverse(traversal, ref node.RightChild, depth + 1, callback, ref context);
                callback(ref node, depth, ref context);
                break;
        }
    }

    private static long NumSectors(long size) => (size / XISO.SECTOR_SIZE + ((size % XISO.SECTOR_SIZE) > 0 ? 1 : 0));

    private void ResolveOffsets()
    {
        long startSector = RootNode.StartSector;
        int _ = 0;

        Traverse(Traversal.Prefix, ref _RootNode, 0, CalculateDirectoryRequirementsCb, ref _);
        Traverse(Traversal.Prefix, ref _RootNode, 0, CalculateDirectoryOffsetsCb, ref startSector);
        Traverse(Traversal.Prefix, ref _RootNode, 0, VerifyTreeCb, ref _);
    }

    private void CalculateDirectoryRequirementsCb(ref Node node, int depth, ref int _)
    { 
        if (node.Subdirectory == null && node.Subdirectory is not EmptySubdirectoryNode)
            return;

        if (node.Subdirectory is not EmptySubdirectoryNode)
        {
            Traverse(Traversal.Prefix, ref node.Subdirectory, 0, CalculateDirectorySizeCb, ref node.FileSize);
            Traverse(Traversal.Prefix, ref node.Subdirectory, 0, CalculateDirectoryRequirementsCb, ref _);
        }
        else
        {
            node.FileSize = XISO.SECTOR_SIZE;
        }
    }

    private void CalculateDirectorySizeCb(ref Node node, int depth, ref long outSize)
    {
        if (depth == 0)
            outSize = 0;

        var nameLength = Encoding.UTF8.GetByteCount(node.Filename);
        var length = XISO.DIRECTORY_HEADER_SIZE + nameLength;
        length += ((4 - (length % 4)) % 4);

        if (NumSectors(outSize + length) > NumSectors(outSize))
            outSize += (XISO.SECTOR_SIZE - (outSize % XISO.SECTOR_SIZE)) % XISO.SECTOR_SIZE;

        node.DirectoryOffset = outSize;
        outSize += length;
    }

    private void CalculateDirectoryOffsetsCb(ref Node node, int depth, ref long currentSector)
    {
        if (node.Subdirectory is EmptySubdirectoryNode)
        {
            node.StartSector = currentSector;
            currentSector++;
        }
        else if (node.Subdirectory != null)
        {
            node.StartSector = currentSector;
            currentSector += NumSectors(node.FileSize);

            var aoContext = new AssignOffsetsContext();
            aoContext.DirectoryStart = node.StartSector * XISO.SECTOR_SIZE;
            aoContext.CurrentSector = currentSector;

            Traverse(Traversal.Prefix, ref node.Subdirectory, 0, AssignOffsetsCb, ref aoContext);
            currentSector = aoContext.CurrentSector;

            Traverse(Traversal.Prefix, ref node.Subdirectory, 0, CalculateDirectoryOffsetsCb, ref currentSector);
        }
    }

    private void AssignOffsetsCb(ref Node node, int depth, ref AssignOffsetsContext context)
    {
        node.DirectoryStart = context.DirectoryStart;

        if (node.Subdirectory == null)
        {
            node.StartSector = context.CurrentSector;
            context.CurrentSector += NumSectors(node.FileSize);
        }
    }

    private void VerifyTreeCb(ref Node node, int depth, ref int _)
    {
        if (node is EmptySubdirectoryNode)
            return;

        if (node.FileSize > 0xFFFFFFFFu)
        {
            throw new InvalidOperationException(
                $"File '{node.Filename}' exceeds the maximum allowed size of 4GB for ISO files.");
        }
        if (node.StartSector > 0xFFFFFFFFu)
        {
            throw new InvalidOperationException(
                $"Node '{node.Filename}' has a starting sector that exceeds the maximum allowed value of 4GB / 2048 bytes per sector for ISO files.");
        }

        if (node.Subdirectory != null)
            Traverse(Traversal.Prefix, ref node.Subdirectory, 0, VerifyTreeCb, ref _);
    }

    private static Result InsertNode(ref Node? rootNode, Node node)
    {
        if (rootNode == null || rootNode is EmptySubdirectoryNode)
        {
            rootNode = node;
            return Result.Balanced;
        }

        var compResult = CompareKeys(node.Filename, rootNode.Filename);

        if (compResult < 0)
        {
            var result = InsertNode(ref rootNode.LeftChild, node);
            return (result == Result.Balanced) ? LeftGrown(ref rootNode) : result;
        }
        else if (compResult > 0)
        {
            var result = InsertNode(ref rootNode.RightChild, node);
            return (result == Result.Balanced) ? RightGrown(ref rootNode) : result;
        }

        return Result.Error;
    }

    private static Result LeftGrown(ref Node node)
    {
        switch (node.Skew)
        {
            case Skew.Left:
                if (!NodeExists(node.LeftChild))
                {
                    throw new InvalidOperationException(
                        "Left child cannot be null or empty when skew is left.");
                }
                if (node.LeftChild.Skew == Skew.Left)
                {
                    node.Skew = node.LeftChild.Skew = Skew.None;
                    RotateRight(ref node);
                }
                else
                {
                    if (!NodeExists(node.LeftChild))
                    {
                        throw new InvalidOperationException(
                            "Left child cannot be null or empty when skew is left.");
                    }
                    else if (!NodeExists(node.LeftChild.RightChild))
                    {
                        throw new InvalidOperationException(
                            "Right child of left child cannot be null or empty when skew is left.");
                    }

                    switch (node.LeftChild.RightChild.Skew)
                    {
                        case Skew.Left:
                            node.Skew = Skew.Right;
                            node.LeftChild.Skew = Skew.None;
                            break;
                        case Skew.Right:
                            node.Skew = Skew.None;
                            node.LeftChild.Skew = Skew.Left;
                            break;
                        default:
                            node.Skew = Skew.None;
                            node.LeftChild.Skew = Skew.None;
                            break;
                    }

                    node.LeftChild.RightChild.Skew = Skew.None;
                    RotateRight(ref node.LeftChild);
                    RotateLeft(ref node);
                }
                return Result.NoError;
            case Skew.Right:
                node.Skew = Skew.None;
                return Result.NoError;
            default:
                node.Skew = Skew.Left;
                return Result.Balanced;
        }
    }

    private static Result RightGrown(ref Node node)
    {
        switch (node.Skew)
        {
            case Skew.Left:
                node.Skew = Skew.None;
                return Result.NoError;

            case Skew.Right:
                {
                    if (!NodeExists(node.RightChild))
                    {
                        throw new InvalidOperationException(
                            "Right child cannot be null or empty when skew is right.");
                    }

                    if (node.RightChild.Skew == Skew.Right)
                    {
                        node.Skew = Skew.None;
                        node.RightChild.Skew = Skew.None;
                        RotateLeft(ref node);
                    }
                    else
                    {
                        if (!NodeExists(node.RightChild.LeftChild))
                        {
                            throw new InvalidOperationException(
                                "Left child of right child cannot be null or empty when skew is right.");
                        }

                        switch (node.RightChild.LeftChild.Skew)
                        {
                            case Skew.Left:
                                node.Skew = Skew.None;
                                node.RightChild.Skew = Skew.Right;
                                break;

                            case Skew.Right:
                                node.Skew = Skew.Left;
                                node.RightChild.Skew = Skew.None;
                                break;

                            default:
                                node.Skew = Skew.None;
                                node.RightChild.Skew = Skew.None;
                                break;
                        }

                        node.RightChild.LeftChild.Skew = Skew.None;

                        RotateRight(ref node.RightChild);
                        RotateLeft(ref node);
                    }

                    return Result.NoError;
                }

            default:
                node.Skew = Skew.Right;
                return Result.Balanced;
        }
    }

    private static void RotateLeft(ref Node node)
    {
        if (node.RightChild == null)
        {
            throw new InvalidOperationException(
                "Right child cannot be null or empty when performing right rotation.");
        }

        Node tmp = node;
        node = node.RightChild;
        tmp.RightChild = node.LeftChild;
        node.LeftChild = tmp;
    }

    private static void RotateRight(ref Node node)
    {
        if (node.LeftChild == null)
        {
            throw new InvalidOperationException(
                "Left child cannot be null or empty when performing left rotation.");
        }

        Node tmp = node;
        node = node.LeftChild;
        tmp.LeftChild = node.RightChild;
        node.RightChild = tmp;
    }

    private static int CompareKeys(string key1, string key2) => 
        string.Compare(key1, key2, StringComparison.OrdinalIgnoreCase);
}
