namespace XGDTool.Lib.Avl;

public abstract class Node<TSelf> where TSelf : Node<TSelf>
{
    public Skew Skew = Skew.None;
    public TSelf? SubDirectory = null;
    public TSelf? LeftChild = null;
    public TSelf? RightChild = null;
    public abstract bool IsEmptyNode { get; }
    public abstract int CompareTo(TSelf otherNode);
}