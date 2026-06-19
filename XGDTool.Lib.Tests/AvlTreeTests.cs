using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

using XGDTool.Lib.Avl;

namespace XGDTool.Lib.Tests;

public class AvlTreeTests
{
    private sealed class TestNode : Node<TestNode>
    {
        public int Key;
        public override bool IsEmptyNode => false;
        public override int CompareTo(TestNode other) => Key.CompareTo(other.Key);
    }

    private sealed class CollectContext
    {
        public List<int> Values = [];
    }

    private sealed class MaxDepthContext
    {
        public int MaxDepth;
    }

    [Fact]
    public void InOrderTraversal_IsSorted_AfterRandomInserts()
    {
        var rng = new Random(12345);
        var keys = Enumerable.Range(0, 500).Select(_ => rng.Next(0, 100_000)).Distinct().ToList();

        TestNode? root = null;
        foreach (var key in keys)
        {
            var result = Tree<TestNode>.InsertNode(ref root, new TestNode { Key = key });
            Assert.NotEqual(InsertResult.Error, result);
        }

        var context = new CollectContext();
        Traversal<TestNode, CollectContext>.Traverse(
            TraverseOrder.Infix,
            ref root,
            (ref TestNode node, ref CollectContext ctx, int depth) => ctx.Values.Add(node.Key),
            ref context);

        Assert.Equal(keys.Order(), context.Values);
    }

    [Fact]
    public void InsertingDuplicateKey_ReturnsError()
    {
        TestNode? root = null;
        Tree<TestNode>.InsertNode(ref root, new TestNode { Key = 42 });

        var result = Tree<TestNode>.InsertNode(ref root, new TestNode { Key = 42 });

        Assert.Equal(InsertResult.Error, result);
    }

    [Fact]
    public void Tree_StaysHeightBalanced_AfterManyInserts()
    {
        var rng = new Random(54321);
        const int count = 2000;
        var keys = Enumerable.Range(0, count * 2)
            .Select(_ => rng.Next(0, 10_000_000))
            .Distinct()
            .Take(count)
            .ToList();

        TestNode? root = null;
        foreach (var key in keys)
            Tree<TestNode>.InsertNode(ref root, new TestNode { Key = key });

        var depthContext = new MaxDepthContext();
        Traversal<TestNode, MaxDepthContext>.Traverse(
            TraverseOrder.Prefix,
            ref root,
            (ref TestNode node, ref MaxDepthContext ctx, int depth) => ctx.MaxDepth = Math.Max(ctx.MaxDepth, depth),
            ref depthContext);

        // AVL height bound: height <= 1.44 * log2(n + 2)
        var bound = (int)Math.Ceiling(1.45 * Math.Log2(count + 2));
        Assert.True(depthContext.MaxDepth <= bound,
            $"Tree height {depthContext.MaxDepth} exceeded AVL bound {bound} for {count} nodes.");
    }
}
