using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using XGDTool.Lib.Image.Format;

namespace XGDTool.Lib.Avl
{
    public enum Traversal
    {
        Prefix,
        Infix,
        Postfix
    }

    public delegate void TraversalCallback<TContext>(Node? node, int depth, TContext? context);
    public delegate void RefTraversalCallback<TContext>(ref Node node, int depth, ref TContext context);

    public class Tree
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

        public Node RootNode = new EmptyNode();

        public string RootName { get; init; }
        public long TotalBytes { get; private set; }
        public long TotalFiles  { get; private set; }

        private const int MAX_RECURSE_DEPTH = 100;

        public Tree(string name)
        {
            RootName = name;
        }

        public void BuildTree(IReadOnlyList<Image.Reader.DirectoryEntry> entries)
        {
            RootNode = new Node(RootName);
            RootNode.StartSector = XISO.ROOT_DIRECTORY_SECTOR;
            TotalBytes = 0;
            TotalFiles = 0;
            GenerateFromEntries(entries);
            ResolveOffsets();
        }

        public void BuildTree(string rootDirectory)
        {
            RootNode = new Node(RootName);
            RootNode.StartSector = XISO.ROOT_DIRECTORY_SECTOR;
            TotalBytes = 0;
            TotalFiles = 0;
            GenerateFromDirectory(rootDirectory);
            ResolveOffsets();
        }

        public static bool NodeExists([NotNullWhen(true)] Node? node) => ((node != null) && (node is not EmptyNode));

        public static void Traverse<TContext>
        (
            Traversal traversal,
            Node? rootNode,
            int depth,
            TraversalCallback<TContext> callback,
            TContext? context = default
        )
        {
            if (rootNode == null || rootNode is EmptyNode)
                return;

            switch (traversal)
            {
                case Traversal.Prefix:
                    callback(rootNode, depth, context);
                    Traverse(traversal, rootNode.LeftChild, depth + 1, callback, context);
                    Traverse(traversal, rootNode.RightChild, depth + 1, callback, context);
                    break;
                case Traversal.Infix:
                    Traverse(traversal, rootNode.LeftChild, depth + 1, callback, context);
                    callback(rootNode, depth, context);
                    Traverse(traversal, rootNode.RightChild, depth + 1, callback, context);
                    break;
                case Traversal.Postfix:
                    Traverse(traversal, rootNode.LeftChild, depth + 1, callback, context);
                    Traverse(traversal, rootNode.RightChild, depth + 1, callback, context);
                    callback(rootNode, depth, context);
                    break;
            }
        }

        private void GenerateFromEntries(IReadOnlyList<Image.Reader.DirectoryEntry> entries)
        {
            var queue = new Queue<Image.Reader.DirectoryEntry>(entries);
            RecurseEntries(ref queue, ref RootNode.Subdirectory, 0);
        }

        private void GenerateFromDirectory(string dirPath) => RecurseDirectories(dirPath, ref RootNode.Subdirectory, 0);

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
                var node = new Node(entry.GetName());
                node.OldStartSector = entry.Header.StartSector;
                node.FileSize = entry.Header.FileSize;
                node.Filepath = entry.Filepath;

                if (entry.Header.Attributes.HasFlag(XISO.DirAttribute.Directory))
                {
                    var subEntries = new Queue<Image.Reader.DirectoryEntry>();
                    var remainingQueue = new Queue<Image.Reader.DirectoryEntry>(queue);
                    var currentPath = entry.Filepath;

                    while (queue.Count > 0)
                    {
                        var item = queue.Dequeue();
                        if (item.Filepath.StartsWith(currentPath, StringComparison.OrdinalIgnoreCase) &&
                            !currentPath.Equals(item.Filepath))
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

                    if (node.Subdirectory == null)
                        node.Subdirectory = new EmptyNode();
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

        private void RecurseDirectories(string dirPath, ref Node? dirNode, int depth)
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
                node.Filepath = dirInfo.FullName;

                if (Directory.Exists(entry))
                {
                    RecurseDirectories(entry, ref node.Subdirectory, depth + 1);

                    if (node.Subdirectory == null)
                        node.Subdirectory = new EmptyNode();
                }
                else if (File.Exists(entry))
                {
                    var fileInfo = new FileInfo(entry);

                    if (fileInfo.Length > 0xFFFFFFFF)
                    {
                        throw new InvalidOperationException(
                            $"File '{fileInfo.FullName}' exceeds the maximum allowed size of 4GB for ISO files.");
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

        public static void Traverse<TContext>
        (
            Traversal traversal,
            ref Node? rootNode,
            int depth,
            RefTraversalCallback<TContext> callback,
            ref TContext context
        )
        {
            if (rootNode == null || rootNode is EmptyNode)
                return;

            switch (traversal)
            {
                case Traversal.Prefix:
                    callback(ref rootNode, depth, ref context);
                    Traverse(traversal, ref rootNode.LeftChild, depth + 1, callback, ref context);
                    Traverse(traversal, ref rootNode.RightChild, depth + 1, callback, ref context);
                    break;
                case Traversal.Infix:
                    Traverse(traversal, ref rootNode.LeftChild, depth + 1, callback, ref context);
                    callback(ref rootNode, depth, ref context);
                    Traverse(traversal, ref rootNode.RightChild, depth + 1, callback, ref context);
                    break;
                case Traversal.Postfix:
                    Traverse(traversal, ref rootNode.LeftChild, depth + 1, callback, ref context);
                    Traverse(traversal, ref rootNode.RightChild, depth + 1, callback, ref context);
                    callback(ref rootNode, depth, ref context);
                    break;
            }
        }

        private static long NumSectors(long size) => (size / XISO.SECTOR_SIZE + ((size % XISO.SECTOR_SIZE) > 0 ? 1 : 0));

        private void ResolveOffsets()
        {
            long startSector = RootNode.StartSector;
            int _ = 0;

            Traverse(Traversal.Prefix, ref RootNode, 0, CalculateDirectoryRequirementsCb, ref _);
            Traverse(Traversal.Prefix, ref RootNode, 0, CalculateDirectoryOffsetsCb, ref startSector);
            Traverse(Traversal.Prefix, ref RootNode, 0, VerifyTreeCb, ref _);
        }

        private void CalculateDirectoryRequirementsCb(ref Node node, int depth, ref int _)
        { 
            if (node.Subdirectory == null && node.Subdirectory is not EmptyNode)
                return;

            if (node.Subdirectory is not EmptyNode)
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
            if (node.Subdirectory is EmptyNode)
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
            if (node is EmptyNode)
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
            if (rootNode == null || rootNode is EmptyNode)
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
                        if (!NodeExists(node.RightChild))
                        {
                            throw new InvalidOperationException(
                                "Right child cannot be null or empty when skew is left.");
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
                                node.LeftChild.Skew = Skew.Right;
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
                    if (!NodeExists(node.RightChild))
                    {
                        throw new InvalidOperationException(
                            "Right child cannot be null or empty when skew is right.");
                    }
                    if (node.RightChild.Skew == Skew.Right)
                    {
                        node.Skew = node.RightChild.Skew = Skew.None;
                        RotateLeft(ref node);
                    }
                    else
                    {
                        if (!NodeExists(node.RightChild))
                        {
                            throw new InvalidOperationException(
                                "Right child cannot be null or empty when skew is right.");
                        }
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
                default:
                    node.Skew = Skew.Right;
                    return Result.Balanced;
            }
        }

        private static void RotateRight(ref Node node)
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

        private static void RotateLeft(ref Node node)
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
}
