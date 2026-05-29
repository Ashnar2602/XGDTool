using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using XGDTool.Format;

namespace XGDTool.Avl
{
    public enum Traversal
    {
        Prefix,
        Infix,
        Postfix
    }

    public class Tree
    {
        private enum Result
        {
            Balanced,
            NoError,
            Error
        }

        public string RootName { get; private set; }
        public Node RootNode { get; private set; } = new EmptyNode();
        public long TotalBytes { get; private set; }
        public long TotalFiles  { get; private set; }
        public long IsoTotalBytes  { get; private set; }

        public Tree(string name, IReadOnlyList<XISO.DirectoryEntry> entries)
        {
            RootName = name;
            RootNode.StartSector = XISO.ROOT_DIRECTORY_SECTOR;
            var entriesList = entries.ToList();
            GenerateFromEntries(entriesList, ref RootNode.Subdirectory);
            ResolveOffsetsAndSizes();
        }

        public Tree(string name, string rootDirectory)
        {
            RootName = name;
            RootNode.StartSector = XISO.ROOT_DIRECTORY_SECTOR;
            GenerateFromDirectory(rootDirectory, ref RootNode.Subdirectory);
            ResolveOffsetsAndSizes();
        }

        public static bool NodeExists([NotNullWhen(true)] Node? node) => ((node != null) && (node is not EmptyNode));

        public static void Traverse<TContext>
        (
            Traversal traversal,
            Node? rootNode,
            int depth,
            Action<Node, int, TContext?> callback,
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

        private void GenerateFromEntries(List<XISO.DirectoryEntry> directoryEntries, ref Node? dirNode)
        {
            
        }

        private void GenerateFromDirectory(string dirPath, ref Node? dirNode)
        {

        }

        private void ResolveOffsetsAndSizes()
        {
             
        }

        private Result InsertNode(ref Node? rootNode, Node node)
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

        private Result LeftGrown(ref Node node)
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

        private Result RightGrown(ref Node node)
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

        private void RotateRight(ref Node node)
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

        private void RotateLeft(ref Node node)
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

        private int CompareKeys(string key1, string key2) => 
            string.Compare(key1, key2, StringComparison.OrdinalIgnoreCase);
    }
}
