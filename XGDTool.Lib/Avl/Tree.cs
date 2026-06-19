using System.Diagnostics.CodeAnalysis;

namespace XGDTool.Lib.Avl;

public static class Tree<TNode> where TNode : Node<TNode>
{
    public static InsertResult InsertNode(ref TNode? parentNode, TNode node)
    {
        if (parentNode == null || parentNode.IsEmptyNode)
        {
            parentNode = node;
            return InsertResult.Balanced;
        }

        // var compResult = CompareKeys(node.Name, parentNode.Name);
        var compResult = node.CompareTo(parentNode);

        if (compResult < 0)
        {
            var result = InsertNode(ref parentNode.LeftChild, node);
            return (result == InsertResult.Balanced) ? LeftGrown(ref parentNode) : result;
        }
        else if (compResult > 0)
        {
            var result = InsertNode(ref parentNode.RightChild, node);
            return (result == InsertResult.Balanced) ? RightGrown(ref parentNode) : result;
        }

        return InsertResult.Error;
    }

    private static InsertResult LeftGrown(ref TNode node)
    {
        switch (node.Skew)
        {
            case Skew.Left:
                if (!NodeIsValid(node.LeftChild))
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
                    if (!NodeIsValid(node.LeftChild))
                    {
                        throw new InvalidOperationException(
                            "Left child cannot be null or empty when skew is left.");
                    }
                    else if (!NodeIsValid(node.LeftChild.RightChild))
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
                    RotateLeft(ref node.LeftChild);
                    RotateRight(ref node);
                }
                return InsertResult.NoError;
            case Skew.Right:
                node.Skew = Skew.None;
                return InsertResult.NoError;
            default:
                node.Skew = Skew.Left;
                return InsertResult.Balanced;
        }
    }

    private static InsertResult RightGrown(ref TNode node)
    {
        switch (node.Skew)
        {
            case Skew.Left:
                node.Skew = Skew.None;
                return InsertResult.NoError;

            case Skew.Right:
                {
                    if (!NodeIsValid(node.RightChild))
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
                        if (!NodeIsValid(node.RightChild.LeftChild))
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

                    return InsertResult.NoError;
                }

            default:
                node.Skew = Skew.Right;
                return InsertResult.Balanced;
        }
    }

    private static void RotateLeft(ref TNode node)
    {
        if (node.RightChild == null)
        {
            throw new InvalidOperationException(
                "Right child cannot be null or empty when performing right rotation.");
        }

        TNode tmp = node;
        node = node.RightChild;
        tmp.RightChild = node.LeftChild;
        node.LeftChild = tmp;
    }

    private static void RotateRight(ref TNode node)
    {
        if (node.LeftChild == null)
        {
            throw new InvalidOperationException(
                "Left child cannot be null or empty when performing left rotation.");
        }

        TNode tmp = node;
        node = node.LeftChild;
        tmp.LeftChild = node.RightChild;
        node.RightChild = tmp;
    }

    public static bool NodeIsValid([NotNullWhen(true)] TNode? node) => (node != null) && (!node.IsEmptyNode);
}
