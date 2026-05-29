using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

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
        public Node? RootNode { get; private set; }
        public long TotalBytes { get; private set; }
        public long TotalFiles  { get; private set; }
        public long OutIsoTotalBytes  { get; private set; }

        public void Traverse<TContext>
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
    }
}
