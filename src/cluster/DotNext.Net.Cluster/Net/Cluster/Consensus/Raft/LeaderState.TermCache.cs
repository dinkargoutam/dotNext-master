using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

internal partial class LeaderState<TMember>
{
    // Splay Tree is an ideal candidate for caching terms because
    // 1. When all members are online, their indexes within WAL is in sync, so the necessary term is the root of the tree
    // with O(1) access
    // 2. Removing obsolete indexes is O(1)
    // 3. Cache cleanup is O(1)
    [StructLayout(LayoutKind.Auto)]
#if DEBUG
    internal
#else
    private
#endif
    struct TermCache
    {
        private TermCacheNode? root;
        private int addCount;

        internal readonly int ApproximatedCount => addCount;

        internal void Add(long index, long term)
        {
            TermCacheNode? parent = null;

            for (var x = root; x is not null; x = index < x.Index ? x.Left : x.Right)
            {
                parent = x;
            }

            TermCacheNode node;
            switch (parent?.Index.CompareTo(index))
            {
                case null:
                    root = node = new(index, term);
                    addCount = 1;
                    break;
                case > 0:
                    parent.Left = node = new(index, term) { Parent = parent };
                    addCount += 1;
                    break;
                case < 0:
                    parent.Right = node = new(index, term) { Parent = parent };
                    addCount += 1;
                    break;
                case 0:
                    Debug.Assert(term == parent.Term);
                    node = parent;
                    break;
            }

            Splay(node);
        }

        internal bool TryGet(long index, out long term)
        {
            var node = FindNode(index);
            if (node is not null)
            {
                term = node.Term;
                return true;
            }

            term = default;
            return false;
        }

        internal void RemovePriorTo(long index)
        {
            var node = FindNode(index);

            if (node is not null)
            {
                node.Left = null;

                if (node.Right is null)
                    addCount = 1;
            }
        }

        internal void Clear() => this = default;

        private TermCacheNode? FindNode(long index)
        {
            var result = root;
            while (result is not null)
            {
                switch (index.CompareTo(result.Index))
                {
                    case < 0:
                        result = result.Left;
                        continue;
                    case > 0:
                        result = result.Right;
                        continue;
                    case 0:
                        Splay(result);
                        goto exit;
                }
            }

        exit:
            return result;
        }

        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1013", Justification = "False positive")]
        private void RotateRight(TermCacheNode node)
        {
            Debug.Assert(node.Left is not null);

            var y = node.Left;
            node.Left = y.Right;

            if (y.Right is not null)
                y.Right.Parent = node;

            y.Parent = node.Parent;

            switch (node)
            {
                case { Parent: null }:
                    root = y;
                    break;
                case { IsRight: true }:
                    node.Parent.Right = y;
                    break;
                default:
                    node.Parent.Left = y;
                    break;
            }

            y.Right = node;
            node.Parent = y;
        }

        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1013", Justification = "False positive")]
        private void RotateLeft(TermCacheNode node)
        {
            Debug.Assert(node.Right is not null);

            var y = node.Right;
            node.Right = y.Left;

            if (y.Left is not null)
                y.Left.Parent = node;

            y.Parent = node.Parent;

            switch (node)
            {
                case { Parent: null }:
                    root = y;
                    break;
                case { IsLeft: true }:
                    node.Parent.Left = y;
                    break;
                default:
                    node.Parent.Right = y;
                    break;
            }

            y.Left = node;
            node.Parent = y;
        }

        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1013", Justification = "False positive")]
        private void Splay(TermCacheNode node)
        {
            while (node.Parent is not null)
            {
                switch (node)
                {
                    case { IsLeft: true, Parent: { IsRoot: true } }:
                        RotateRight(node.Parent); // zig rotation
                        break;
                    case { IsLeft: false, Parent: { IsRoot: true } }:
                        RotateLeft(node.Parent); // zag rotation
                        break;
                    case { IsLeft: true, Parent: { IsLeft: true } }:
                        // zig-zig rotation
                        RotateRight(node.Parent.Parent);
                        RotateRight(node.Parent);
                        break;
                    case { IsRight: true, Parent: { IsRight: true } }:
                        // zag-zag rotation
                        RotateLeft(node.Parent.Parent);
                        RotateLeft(node.Parent);
                        break;
                    case { IsRight: true, Parent: { IsLeft: true } }:
                        // zig-zag rotation
                        RotateLeft(node.Parent);
                        RotateRight(node.Parent);
                        break;
                    default:
                        // zag-zig rotation
                        RotateRight(node.Parent);
                        RotateLeft(node.Parent);
                        break;
                }
            }
        }
    }

    private sealed class TermCacheNode
    {
        internal readonly long Index, Term;
        internal TermCacheNode? Left, Right, Parent;

        internal TermCacheNode(long index, long term)
        {
            Index = index;
            Term = term;
        }

        [MemberNotNullWhen(true, nameof(Parent))]
        internal bool IsLeft => Parent?.Left == this;

        [MemberNotNullWhen(true, nameof(Parent))]
        internal bool IsRight => Parent?.Right == this;

        [MemberNotNullWhen(false, nameof(Parent))]
        internal bool IsRoot => Parent is null;
    }

    private TermCache precedingTermCache;
}