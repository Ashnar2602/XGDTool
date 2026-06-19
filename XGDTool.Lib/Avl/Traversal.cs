namespace XGDTool.Lib.Avl;

public enum TraverseOrder
{
    Prefix,
    Infix,
    Postfix
}

public static class Traversal<TNode, TContext> where TNode : Node<TNode>
{
    public delegate void Callback(ref TNode node, ref TContext context, int depth);
    
    public static void Traverse(TraverseOrder order, ref TNode? node, Callback callback, ref TContext context, int depth = 0)
    {
        if (node == null || node.IsEmptyNode)
            return;

        switch (order)
        {
            case TraverseOrder.Prefix:
                callback(ref node, ref context, depth);
                Traverse(order, ref node.LeftChild, callback, ref context, depth + 1);
                Traverse(order, ref node.RightChild, callback, ref context, depth + 1);
                break;
            case TraverseOrder.Infix:
                Traverse(order, ref node.LeftChild, callback, ref context, depth + 1);
                callback(ref node, ref context, depth);
                Traverse(order, ref node.RightChild, callback, ref context, depth + 1);
                break;
            case TraverseOrder.Postfix:
                Traverse(order, ref node.LeftChild, callback, ref context, depth + 1);
                Traverse(order, ref node.RightChild, callback, ref context, depth + 1);
                callback(ref node, ref context, depth);
                break;
        }
    }
}
