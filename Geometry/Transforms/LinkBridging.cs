using System;
using System.Collections.Generic;
using System.Linq;

namespace Geometry.Transforms
{
    /// <summary>A link between two nodes, stored with the smaller id first so a pair has one spelling.</summary>
    public readonly struct NodePair : IEquatable<NodePair>
    {
        public long A { get; }
        public long B { get; }

        public NodePair(long a, long b)
        {
            if (a == b)
                throw new ArgumentException($"A node cannot pair with itself ({a})");
            A = Math.Min(a, b);
            B = Math.Max(a, b);
        }

        public bool Equals(NodePair other) => A == other.A && B == other.B;
        public override bool Equals(object? obj) => obj is NodePair other && Equals(other);
        public override int GetHashCode() => (A.GetHashCode() * 397) ^ B.GetHashCode();
        public override string ToString() => $"{A}-{B}";
    }

    /// <summary>One connected group of removed nodes and the links that replace it.</summary>
    public sealed class BridgedGroup
    {
        /// <summary>The removed nodes, linked to each other through removed nodes only.</summary>
        public IReadOnlyList<long> Removed { get; internal set; } = [];

        /// <summary>The kept nodes any removed node of the group links to.</summary>
        public IReadOnlyList<long> Neighbours { get; internal set; } = [];

        /// <summary>Links to add: every pair of neighbours of one structure that is not linked already.</summary>
        public IReadOnlyList<NodePair> Added { get; internal set; } = [];

        /// <summary>Neighbour pairs not linked because they belong to different structures (or one structure is unknown).</summary>
        public IReadOnlyList<NodePair> NotLinkedDifferentStructure { get; internal set; } = [];

        /// <summary>Neighbour pairs already linked; nothing to add.</summary>
        public IReadOnlyList<NodePair> AlreadyLinked { get; internal set; } = [];
    }

    /// <summary>
    /// Replaces removed nodes of a link graph by direct links between the nodes they joined, so a chain keeps its
    /// connectivity: removing B from A-B-C adds A-C, removing a run B1-B2-B3 from A-B1-B2-B3-C adds A-C, and removing a
    /// branch point joined to A, C and D adds A-C, A-D and C-D. Used by the mosaic remap when a location falls on a tile the
    /// new mosaic dropped (VikingAU <c>remap-from-tiles</c>): Viking location links join one structure's locations, so only
    /// neighbours of one structure are linked.
    /// </summary>
    public static class LinkBridging
    {
        /// <param name="removed">The nodes to remove.</param>
        /// <param name="linksOf">Every node's linked nodes, removed and kept alike; must be symmetric.</param>
        /// <param name="structureOf">A node's structure; null when unknown, which links it to nothing.</param>
        /// <returns>One group per connected set of removed nodes, ordered by its smallest node.</returns>
        public static IReadOnlyList<BridgedGroup> Bridge(IEnumerable<long> removed, Func<long, IEnumerable<long>> linksOf, Func<long, long?> structureOf)
        {
            if (removed is null)
                throw new ArgumentNullException(nameof(removed));
            if (linksOf is null)
                throw new ArgumentNullException(nameof(linksOf));
            if (structureOf is null)
                throw new ArgumentNullException(nameof(structureOf));

            HashSet<long> removedSet = [.. removed];
            HashSet<long> visited = [];
            List<BridgedGroup> groups = [];

            foreach (long start in removedSet.OrderBy(id => id))
            {
                if (!visited.Add(start))
                    continue;

                List<long> members = [];
                SortedSet<long> neighbours = [];
                Stack<long> pending = new([start]);
                while (pending.Count > 0)
                {
                    long node = pending.Pop();
                    members.Add(node);
                    foreach (long peer in linksOf(node) ?? [])
                    {
                        if (peer == node)
                            continue;
                        if (removedSet.Contains(peer))
                        {
                            if (visited.Add(peer))
                                pending.Push(peer);
                        }
                        else
                        {
                            neighbours.Add(peer);
                        }
                    }
                }

                List<NodePair> added = [], different = [], existing = [];
                long[] kept = [.. neighbours];
                for (int i = 0; i < kept.Length; i++)
                {
                    for (int j = i + 1; j < kept.Length; j++)
                    {
                        NodePair pair = new(kept[i], kept[j]);
                        long? si = structureOf(kept[i]), sj = structureOf(kept[j]);
                        if (si is null || si != sj)
                            different.Add(pair);
                        else if ((linksOf(kept[i]) ?? []).Contains(kept[j]))
                            existing.Add(pair);
                        else
                            added.Add(pair);
                    }
                }

                members.Sort();
                groups.Add(new BridgedGroup
                {
                    Removed = members,
                    Neighbours = kept,
                    Added = added,
                    NotLinkedDifferentStructure = different,
                    AlreadyLinked = existing,
                });
            }

            return groups;
        }
    }
}
