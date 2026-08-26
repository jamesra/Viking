using Viking.AnnotationServiceTypes.Interfaces;
using Geometry;
using GraphLib;
using RTree;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnitsAndScale;

namespace AnnotationVizLib
{
    [Serializable]
    public partial class MorphologyGraph : Graph<ulong, MorphologyNode, MorphologyEdge>
    {

        /// <summary>
        /// ID of the structure graph, zero for root or StructureID of structure
        /// </summary>
        public readonly ulong StructureID = 0;

        public readonly IStructureReadOnly structure = null;

        public readonly IScale scale = null;

        public virtual double SectionThickness => scale.Z.Value;

        public IStructureTypeReadOnly structureType => structure.Type;

        [field: NonSerialized()]
        private RTree<ulong> _RTree = null;

        private RTree<ulong> RTree => _RTree ??= CreateRTree(this);

        /// <summary>
        /// Graph this subgraph was added under. Null for a factory root. Not serialized (parent/child cycle).
        /// </summary>
        [field: NonSerialized()]
        public MorphologyGraph Parent { get; private set; }

        /// <summary>
        /// Map the motif label to the arbitrary id used by TLP.  Do not add directly to this collection.  Use Add/Remove Subgraph instead.
        /// </summary>
        public readonly ConcurrentDictionary<ulong, MorphologyGraph> Subgraphs = new();

        internal readonly ConcurrentDictionary<ulong, ulong> NearestNodeToSubgraph = new();

        public MorphologyGraph(ulong subgraph_id, IScale scale)
        {
            this.StructureID = subgraph_id;
            this.structure = null;
            this.scale = scale;
        }

        public MorphologyGraph(ulong subgraph_id, IScale scale, IStructureReadOnly structure)
        {
            this.StructureID = subgraph_id;
            this.structure = structure;
            this.scale = scale;
        }

        //Call this when the graph has changed any spatial qualities that should reset cached measurements
        protected void ResetCachedMeasurements()
        {
            _BoundingBox = default;
            _NodesBoundingBox = default;
        }

        public void AddSubgraph(MorphologyGraph subgraph)
        {
            subgraph.Parent = this;
            Subgraphs.TryAdd(subgraph.StructureID, subgraph);
            ulong nearest_id = NearestNode(subgraph, out double minDistance);
            if (nearest_id != ulong.MaxValue)
            {
                MorphologyNode nearest_node_in_parent = Nodes[nearest_id];
                NearestNodeToSubgraph.TryAdd(subgraph.StructureID, nearest_id);
                Nodes[nearest_id].AddSubgraph(subgraph.StructureID);
            }
        }

        public void RemoveSubgraph(ulong StructureID)
        {
            Subgraphs.TryRemove(StructureID, out MorphologyGraph value);
            if (value != null)
                value.Parent = null;
            if (NearestNodeToSubgraph.TryRemove(StructureID, out ulong nearest_node_id))
            {
                Nodes[nearest_node_id].RemoveSubgraph(StructureID);
            }
        }

        internal static RTree<ulong> CreateRTree(MorphologyGraph graph)
        {
            RTree<ulong> rtree = new();
            foreach (MorphologyNode node in graph.Nodes.Values)
            {
                rtree.Add(node.BoundingBox.ToRTreeRect(), node.Key);
            }

            return rtree;
        }

        public override void AddNode(MorphologyNode node)
        {
            _RTree = null;
            base.AddNode(node);
            ResetCachedMeasurements();
        }

        public override void RemoveNode(ulong key)
        {
            _RTree = null;
            base.RemoveNode(key);
            ResetCachedMeasurements();
        }

        /// <summary>
        /// Remove the node, for any edges create new links between the remaining nodes
        /// </summary>
        /// <param name="key"></param>
        private SortedSet<MorphologyEdge> EdgesForRemovedNode(ulong key)
        {
            //Move all of my edges to the nearest node
            MorphologyNode node_to_remove = Nodes[key];
            SortedSet<ulong> other_nodes = [.. node_to_remove.Edges.Keys];

            ulong nearest_id = NearestNode(key, other_nodes, out double min_distance);

            other_nodes.Remove(nearest_id); //Do not link nearest_node to itself

            SortedSet<MorphologyEdge> new_edges = [];
            foreach (ulong relink_id in other_nodes)
            {
                MorphologyEdge new_edge = new(this, nearest_id, relink_id);
                new_edges.Add(new_edge);
            }

            return new_edges;
        }

        private void RemoveNodePreserveEdges(ulong key)
        {
            SortedSet<MorphologyEdge> new_edges = EdgesForRemovedNode(key);

            RemoveNode(key);

            foreach (MorphologyEdge edge in new_edges)
            {
                if (this.Edges.ContainsKey(edge) == false)
                    this.AddEdge(edge);
            }
        }

        private Box _BoundingBox = default;
        private Box _NodesBoundingBox = default;

        /// <summary>
        /// AABB of this structure's own locations, excluding child subgraphs.
        /// SliceGraph recenters and BajajMultiTest restores volume XY from this origin so a cell mesh is not shifted by synapse bboxes.
        /// </summary>
        public Geometry.Box NodesBoundingBox
        {
            get
            {
                const int ParallelThreshold = 64;
                if (_NodesBoundingBox == default && this.Nodes.Count > 0)
                {
                    IEnumerable<Box> boxes = this.Nodes.Count > ParallelThreshold
                        ? this.Nodes.Values.Select(n => n.BoundingBox).AsParallel()
                        : this.Nodes.Values.Select(n => n.BoundingBox);
                    _NodesBoundingBox = boxes.Aggregate((a, b) => Box.Union(a, b));
                }

                return _NodesBoundingBox;
            }
        }

        public Geometry.Box BoundingBox
        {
            get
            {
                const int ParallelThreshold = 64;
                if (_BoundingBox == default)
                {
                    _BoundingBox = NodesBoundingBox;

                    if (!Subgraphs.IsEmpty)
                    {
                        IEnumerable<Box> subgraphBoxes = this.Subgraphs.Count > ParallelThreshold
                            ? Subgraphs.Values.Select(sg => sg.BoundingBox).AsParallel()
                            : Subgraphs.Values.Select(sg => sg.BoundingBox);
                        Box subgraph_bbox = subgraphBoxes.Aggregate((a, b) => Box.Union(a, b));

                        _BoundingBox = _BoundingBox != default ? Box.Union(_BoundingBox, subgraph_bbox) : subgraph_bbox;
                    }
                }

                Debug.Assert(_BoundingBox != default);
                return _BoundingBox;
            }
        }

        protected SortedDictionary<ulong, SortedSet<ulong>> BuildEdgeLookup()
        {
            SortedDictionary<ulong, SortedSet<ulong>> Links = [];

            foreach (MorphologyEdge edge in Edges.Values)
            {
                if (!Links.ContainsKey(edge.SourceNodeKey))
                {
                    Links[edge.SourceNodeKey] = [edge.TargetNodeKey];
                }
                else
                {
                    Links[edge.SourceNodeKey].Add(edge.TargetNodeKey);
                }

                if (!Links.ContainsKey(edge.TargetNodeKey))
                {
                    Links[edge.TargetNodeKey] = [edge.SourceNodeKey];
                }
                else
                {
                    Links[edge.TargetNodeKey].Add(edge.SourceNodeKey);
                }
            }

            return Links;
        }

        /// <summary>
        /// Locations with 3 or more edges, branch points in a process
        /// </summary>
        /// <returns></returns>
        public ulong[] GetBranchPointIDs() => [.. this.Nodes.Values.Where(n => n.Edges.Count > 2).Select(n => n.Key)];

        /// <summary>
        /// Locations with 1 or fewer links, the tip of a process
        /// </summary>
        /// <returns></returns>
        public ulong[] GetTerminalIDs() => [.. this.Nodes.Values.Where(n => n.Edges.Count == 1 && !n.Location.IsVericosityCap).Select(n => n.Key)];

        /// <summary>
        /// Locations with 2 links, the middle of a process
        /// </summary>
        /// <returns></returns>
        public ulong[] GetProcessIDs() => [.. this.Nodes.Values.Where(n => n.Edges.Count == 2).Select(n => n.Key)];

        /// <summary>
        /// Unbranched 1-up-and-1-down shafts, each including the pinned branch/terminal endpoints used as Catmull-Rom anchors.
        /// Isolated blobs are omitted. A degree-2 node with both links at the same Z is a branch endpoint, not a process.
        /// Called by <see cref="SmoothProcesses"/>; ToStickFigure still uses <see cref="GetProcessIDs"/> (edge count).
        /// </summary>
        public List<ulong[]> Processes()
        {
            SortedSet<ulong> remaining = [.. Nodes.Values.Where(n => n.IsUnbranchedProcess()).Select(n => n.Key)];
            if (remaining.Count == 0)
                return [];

            List<ulong[]> listOutput = [];
            while (remaining.Count > 0)
            {
                ulong[] process = TraverseUnbranchedProcess(Nodes[remaining.First()]);
                listOutput.Add(process);
                remaining.ExceptWith(process.Where(id => Nodes[id].IsUnbranchedProcess()));
            }

            return listOutput;
        }

        /// <summary>
        /// Walks from <paramref name="seed"/> down to the lowest process node, then up, appending the
        /// non-process neighbor at each end so the fit is anchored at branches and terminals.
        /// </summary>
        private ulong[] TraverseUnbranchedProcess(MorphologyNode seed)
        {
            MorphologyGraph graph = seed.Graph;
            MorphologyNode lowest = seed;
            HashSet<ulong> visited = [seed.Key];

            while (true)
            {
                ulong[] below = lowest.GetEdgesBelow();
                if (below.Length != 1)
                    break;

                MorphologyNode neighbor = graph.Nodes[below[0]];
                if (!neighbor.IsUnbranchedProcess() || !visited.Add(neighbor.Key))
                    break;

                lowest = neighbor;
            }

            List<ulong> chain = [];
            ulong[] lowestBelow = lowest.GetEdgesBelow();
            if (lowestBelow.Length == 1)
                chain.Add(lowestBelow[0]);

            MorphologyNode cursor = lowest;
            while (true)
            {
                chain.Add(cursor.Key);
                ulong[] above = cursor.GetEdgesAbove();
                if (above.Length != 1)
                    break;

                MorphologyNode next = graph.Nodes[above[0]];
                if (chain.Contains(next.Key))
                    break;

                if (!next.IsUnbranchedProcess())
                {
                    chain.Add(next.Key);
                    break;
                }

                cursor = next;
            }

            return [.. chain.Distinct().OrderBy(id => graph.Nodes[id].Z).ThenBy(id => id)];
        }
    }
}

