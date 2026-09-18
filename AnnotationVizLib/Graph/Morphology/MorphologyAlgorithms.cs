using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AnnotationVizLib
{
    partial class MorphologyGraph
    {
        public void ConnectIsolatedSubgraphs()
        {
            ConnectIsolatedSubgraphs(this);

            foreach (MorphologyGraph subgraph in this.Subgraphs.Values)
            {
                subgraph.ConnectIsolatedSubgraphs();
            }
        }

        /// <summary>
        /// Create an RTree for some, but not all of the nodes in a graph
        /// </summary>
        /// <param name="subgraph"></param>
        /// <returns></returns>
        private RTree.RTree<ulong> CreateRTreeForSubgraph(ICollection<ulong> subgraph)
        {
            RTree.RTree<ulong> rtree = new();

            foreach (ulong key in subgraph)
            {
                MorphologyNode node = Nodes[key];

                RTree.Rectangle bbox = node.BoundingBox.ToRTreeRect();

                rtree.Add(bbox, key);
            }

            return rtree;
        }

        /// <summary>
        /// Find isolated subgraphs, find the nearest locations between them and create a fake LocationLink
        /// </summary>
        protected static void ConnectIsolatedSubgraphs(MorphologyGraph graph)
        {
            IList<SortedSet<ulong>> subgraphs = MorphologyGraph.IsolatedSubgraphs(graph);

            if (subgraphs.Count <= 1)
                return;

            //Sort the subgraphs from smallest to largest
            List<SortedSet<ulong>> sorted_subgraphs = [.. subgraphs.OrderBy(s => s.Count)];

            //OK find the nearest point between the subgraphs.
            while (sorted_subgraphs.Count > 1)
            {
                //Pop the first subgraph from the list
                SortedSet<ulong> SubgraphToMerge = sorted_subgraphs[0];
                sorted_subgraphs.RemoveAt(0);

                graph.MergeSubgraph(SubgraphToMerge, sorted_subgraphs);
            }
        }

        private void MergeSubgraph(SortedSet<ulong> SubgraphToMerge, IList<SortedSet<ulong>> subgraphs)
        {
            double[] Distances = new double[SubgraphToMerge.Count];

            //Create a single graph of the subgraphs we want to merge into
            SortedSet<ulong> subgraphUnion = [.. subgraphs[0]];
            for (int i = 1; i < subgraphs.Count; i++)
            {
                foreach (ulong id in subgraphs[i])
                {
                    subgraphUnion.Add(id);
                }
            }

            RTree.RTree<ulong> UnionRTree = this.CreateRTreeForSubgraph(subgraphUnion);



            SortedList<ulong, double> distances = [];

            ulong nearest_node_id = 0;
            MorphologyEdge best_edge = null;
            double nearest_node_distance = double.MaxValue;

            //Check each node in our subgraph to find the nearest node in the subgraphs we want to merge into
            foreach (ulong key in SubgraphToMerge)
            {
                MorphologyNode node = this.Nodes[key];

                SortedSet<ulong> candidates = FindNearestCandidatesFromRTree(UnionRTree, node);

                ulong nearest = NearestNode(key, candidates, out double min_distance);
                if (min_distance < nearest_node_distance)
                {
                    best_edge = new MorphologyEdge(this, key, nearest);
                    nearest_node_distance = min_distance;
                    nearest_node_id = nearest;
                }
            }

            if (best_edge is null)
                throw new ArgumentException("Unexpected error in MergeSubgraph.  Could not find an edge between subgraphs.");

            this.AddEdge(best_edge);

            //Add the subgraph we merged to the subgraph in the list
            MorphologyGraph.MergeSubgraphs(SubgraphToMerge, nearest_node_id, subgraphs);
        }

        private static void MergeSubgraphs(SortedSet<ulong> SubgraphToMerge, ulong node_to_merge_onto, IList<SortedSet<ulong>> subgraphs)
        {
            foreach (SortedSet<ulong> subgraph in subgraphs)
            {
                if (subgraph.Contains(node_to_merge_onto))
                {
                    foreach (ulong key in SubgraphToMerge)
                    {
                        subgraph.Add(key);
                    }

                    return;
                }
            }

            throw new ArgumentException("Merging subgraph using key that does not exist " + node_to_merge_onto.ToString());
        }

        private ulong NearestNode(ulong node_to_check, SortedSet<ulong> nodes_to_compare, out double min_distance)
        {
            MorphologyNode node = this.Nodes[node_to_check];

            return NearestNode(node, nodes_to_compare, out min_distance);
        }

        private ulong NearestNode(IGeometry shape_to_check, out double min_distance)
        {
            //Use the RTree to estimate which nodes to check
            SortedSet<ulong> candidates = FindNearestCandidatesFromRTree(this.RTree, shape_to_check);
            return NearestNode(shape_to_check, [.. this.Nodes.Keys], out min_distance);
        }

        /// <summary>
        /// Get a list of at least 8 nodes from the RTree that we should check for proximity to the shape_to_check
        /// </summary>
        /// <param name="shape_to_check"></param>
        /// <returns></returns>
        private static SortedSet<ulong> FindNearestCandidatesFromRTree(RTree.RTree<ulong> rtree, IGeometry shape_to_check)
        {
            List<ulong> found_nodes = [];

            double scale_factor = 2.0;
            while (found_nodes.Count < 8 && found_nodes.Count != rtree.Count)
            {
                Box bbox = shape_to_check.BoundingBox;
                bbox = bbox.Scale(scale_factor);
                found_nodes = [.. rtree.Intersects(bbox.ToRTreeRect())];
                scale_factor *= 2.0;
            }

            return [.. found_nodes];
        }

        private ulong NearestNode(IGeometry shape_to_check, SortedSet<ulong> nodes_to_compare, out double min_distance)
        {
            min_distance = double.MaxValue;
            ulong Nearest = ulong.MaxValue;

            foreach (ulong compare_id in nodes_to_compare)
            {
                MorphologyNode compare_node = this.Nodes[compare_id];
                double z_distance = Math.Abs(shape_to_check.Z - compare_node.Z);

                //Don't bother with the expensive geometry check if the Z distance puts us out of contending for minimum distance
                if (z_distance > min_distance)
                    continue;

                double pair_distance = shape_to_check.Geometry.STDistance(compare_node.Geometry).Value;
                if (pair_distance > min_distance)
                    continue;

                double pair_distance_3D = (pair_distance * pair_distance) + (z_distance * z_distance);
                pair_distance_3D = Math.Sqrt(pair_distance_3D);

                if (pair_distance_3D < min_distance)
                {
                    min_distance = pair_distance_3D;
                    Nearest = compare_id;
                }
            }

            return Nearest;
        }

        /// <summary>
        /// Convert a graph to contain only branch points and terminals
        /// </summary>
        /// <param name="graph"></param>
        public void ToStickFigure()
        {
            ToStickFigure(this);

            foreach (MorphologyGraph subgraph in this.Subgraphs.Values)
            {
                ToStickFigure(subgraph);
            }
        }

        private static void ToStickFigure(MorphologyGraph graph)
        {
            var nodes_to_remove = graph.GetProcessIDs();

            foreach (ulong key in nodes_to_remove)
            {
                graph.RemoveNodePreserveEdges(key);
            }

            //Once in a while, we have a branch attached to a cycle.  This allows the cycle to be removed, and then the branch to be removed in a second-pass if needed
            if (graph.GetProcessIDs().Length > 0)
                ToStickFigure(graph);
        }

        /// <summary>
        /// Find the nearest node on our graph to another morphology graph
        /// </summary>
        /// <param name="subgraph"></param>
        /// <returns></returns>
        public ulong NearestNode(MorphologyGraph other, out double min_distance)
        {
            min_distance = double.MaxValue;
            ulong nearest_node = ulong.MaxValue;
            //Get the bounding box for the graph, 
            foreach (MorphologyNode subgraphnode in other.Nodes.Values)
            {
                ulong id = NearestNode(subgraphnode, out double node_min_distance);
                if (node_min_distance < min_distance)
                {
                    min_distance = node_min_distance;
                    nearest_node = id;
                }
            }

            return nearest_node;
        }

        /// <summary>
        /// Returns the length of a path, measured from the center of each location.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public double PathLength(IList<ulong> path)
        {
            //Need at least two nodes to measure a path
            if (path.Count <= 1)
                return 0.0;

            double TotalDistance = 0.0;

            for (int iStart = 0; iStart < path.Count - 1; iStart++)
            {
                int iEnd = iStart + 1;

                ulong KeyA = path[iStart];
                ulong KeyB = path[iEnd];

                MorphologyNode start = Nodes[KeyA];

                MorphologyEdge edge = start.Edges[KeyB].First();

                TotalDistance += edge.DistanceCenterToCenter;
            }

            return TotalDistance;
        }

        /// <summary>
        /// The length of the shortest line between two morphology graphs
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static double GraphDistance(MorphologyGraph A, MorphologyGraph B)
        {
            //Find the smaller graph
            if (A.Nodes.Count > B.Nodes.Count)
            {
                (B, A) = (A, B);
            }

            double minDistance = double.MaxValue;

            foreach (MorphologyNode N in A.Nodes.Values)
            {
                A.NearestNode(B, out double node_min_distance);
                if (node_min_distance < minDistance)
                {
                    minDistance = node_min_distance;
                }
            }

            return minDistance;
        }


        /// <summary>
        /// Return the distance between all subgraphs of the indicated types
        /// </summary>
        /// <param name="cell_graph"></param>
        /// <param name="SourceTypeIDs"></param>
        /// <param name="TargetTypeIDs"></param>
        /// <returns></returns>
        public static PathData[] DistancesBetweenSubgraphsByType(MorphologyGraph cell_graph, SortedSet<ulong> SourceTypeIDs, SortedSet<ulong> TargetTypeIDs)
        {
            List<ulong> source_ids = [.. cell_graph.Subgraphs.Where(sg => SourceTypeIDs.Contains(sg.Value.structureType.ID)).Select(sg => sg.Key)];
            //Assert.IsTrue(desmosome_ids.Count > 0);
            if (source_ids.Count == 0)
                return [];

            var nodes_with_sourceType_subgraphs = source_ids.Select(id => new { Node = cell_graph.NearestNodeToSubgraph[id], StructureID = id }).ToList();

            SortedDictionary<ulong, PathData> paths_between_types = [];

            //Find the nearest synapse
            foreach (var node_with_sourceType in nodes_with_sourceType_subgraphs)
            {
                IList<ulong> path_to_targetType = MorphologyGraph.ShortestPath(cell_graph, node_with_sourceType.Node, (n) => n.NodeContainsStructureOfType(TargetTypeIDs));
                if (path_to_targetType is null)
                    continue;

                //Find the substructure on the final node of the path
                MorphologyNode destination = cell_graph.Nodes[path_to_targetType.Last()];
                ulong TargetStructureID = destination.Subgraphs.Where(s => TargetTypeIDs.Contains(s.structureType.ID)).Select(s => s.StructureID).First();

                paths_between_types[node_with_sourceType.Node] = new PathData
                {
                    Path = path_to_targetType,
                    SourceStructureID = node_with_sourceType.StructureID,
                    TargetStructureID = TargetStructureID,
                    NearestNodeToSource = cell_graph.Nodes[node_with_sourceType.Node],
                    NearestNodeToTarget = destination
                };
            }

            /*
            if (paths_between_types.Count > 0)
            {
                //int[] hops = paths_for_desmosomes.Select(p => p.Value.Path.Count).ToArray();
                //double avg_hops = paths_for_desmosomes.Select(p => p.Value.Path.Count).Average();
                //Console.WriteLine("Avg number of hops to synapse component: {0}", avg_hops);
            }
            */

            //Precalculate the distance between the substructures using the path
            foreach (ulong ID in paths_between_types.Keys)
            {
                PathData p = paths_between_types[ID];
                p.Distance = DistanceBetweenSubstructures(cell_graph, p.Path, p.SourceStructureID, p.TargetStructureID);
            }

            return [.. paths_between_types.Values];
        }


        /// <summary>
        /// The distance between two substructures in a cell
        /// </summary>
        /// <param name="path_between"></param>
        /// <param name="SourceStructureID"></param>
        /// <param name="TargetStructureID"></param>
        /// <returns></returns>
        internal static double DistanceBetweenSubstructures(MorphologyGraph graph, IList<ulong> path_between, ulong SourceStructureID, ulong TargetStructureID)
        {
            if (path_between.Count <= 2)
            {
                //Measure the direct distance between the structures because there is a direct line between the two
                MorphologyGraph source = graph.Subgraphs[SourceStructureID];
                MorphologyGraph target = graph.Subgraphs[TargetStructureID];

                return MorphologyGraph.GraphDistance(source, target);
            }

            double path_distance = graph.PathLength(path_between);

            ulong nearest_node_to_source = graph.NearestNode(graph.Subgraphs[SourceStructureID], out double SourceToPathDistance);
            ulong nearest_node_to_target = graph.NearestNode(graph.Subgraphs[TargetStructureID], out double TargetToPathDistance);

            return path_distance + SourceToPathDistance + TargetToPathDistance;
        }

        /// <summary>
        /// Cap on XY translation in volume units (nm after scale) when an explicit max-offset is requested.
        /// </summary>
        public const double MaxProcessCentroidOffset = 80.0;

        /// <summary>Default leave-one-out half-window (±N process steps) for <see cref="CurveFitProcesses"/>.</summary>
        public const int DefaultCurveFitHalfWindow = 7;

        /// <summary>
        /// Options for residual-ordered leave-one-out Catmull-Rom process registration correction.
        /// </summary>
        public readonly struct CurveFitOptions
        {
            public static CurveFitOptions Default => new(DefaultCurveFitHalfWindow, null, null);

            public CurveFitOptions(int halfWindow, double? maxOffsetNm, ISet<ulong> onlyLocationIds)
            {
                HalfWindow = halfWindow < 1 ? 1 : halfWindow;
                MaxOffsetNm = maxOffsetNm is > 0 ? maxOffsetNm : null;
                OnlyLocationIds = onlyLocationIds;
            }

            public int HalfWindow { get; }
            public double? MaxOffsetNm { get; }
            public ISet<ulong> OnlyLocationIds { get; }
        }

        /// <summary>
        /// Legacy entry point: residual-ordered curvefit with default window, no max-offset clamp, moving
        /// processes and terminals. Prefer <see cref="CurveFitProcesses(MorphologyGraph, CurveFitOptions)"/>.
        /// </summary>
        public static void SmoothProcesses(MorphologyGraph graph) =>
            CurveFitProcesses(graph, CurveFitOptions.Default);

        /// <summary>
        /// Residual-ordered leave-one-out Catmull-Rom correction of process and terminal centroids.
        /// Branch points stay fixed as curve anchors. Child subgraphs co-move with their parent location.
        /// A move is refused when it would increase the sum of XY lengths of the node's LocationLinks.
        /// Call before <c>SliceGraph.Create</c>. Mutates <see cref="MorphologyNode.Geometry"/> in place.
        /// </summary>
        public static void CurveFitProcesses(MorphologyGraph graph, CurveFitOptions options = default)
        {
            if (graph is null)
                return;

            if (options.HalfWindow < 1 && options.MaxOffsetNm is null && options.OnlyLocationIds is null)
                options = CurveFitOptions.Default;

            Dictionary<int, List<MorphologyNode>> nodesBySection = BuildSameSectionIndex(graph);
            foreach (ulong[] process in graph.Processes())
                CurveFitProcessChain(graph, process, nodesBySection, options);

            graph._RTree = null;
            graph.ResetCachedMeasurements();

            foreach (MorphologyGraph subgraph in graph.Subgraphs.Values)
                CurveFitProcesses(subgraph, options);
        }

        /// <summary>
        /// Sum of XY lengths of every LocationLink in <paramref name="graph"/> (each undirected edge once).
        /// <see cref="CurveFitProcesses"/> must not increase this; the per-move gate and FSCheck rely on it.
        /// </summary>
        public static double SumLocationLinkLengths(MorphologyGraph graph)
        {
            if (graph is null)
                return 0;

            double sum = 0;
            foreach (MorphologyEdge edge in graph.Edges.Values)
            {
                Vector2 a = graph.Nodes[edge.SourceNodeKey].Center.XY();
                Vector2 b = graph.Nodes[edge.TargetNodeKey].Center.XY();
                sum += (a - b).Magnitude;
            }

            return sum;
        }

        /// <summary>
        /// True when curvefit may translate this node: unbranched process shaft or process terminal.
        /// Same-section / multi-edge branches are anchors only.
        /// </summary>
        public static bool IsCurveFitMovable(MorphologyNode node, MorphologyGraph graph = null)
        {
            graph ??= node.Graph;
            if (node is null)
                return false;
            if (node.IsSameSectionBranch(graph) || node.Edges.Count > 2)
                return false;
            return node.IsUnbranchedProcess(graph) || node.IsProcessTerminal();
        }

        /// <summary>
        /// One pass over the graph so same-section overlap checks do not rescan every node for every smoothed contour.
        /// </summary>
        private static Dictionary<int, List<MorphologyNode>> BuildSameSectionIndex(MorphologyGraph graph)
        {
            Dictionary<int, List<MorphologyNode>> bySection = new();
            foreach (MorphologyNode node in graph.Nodes.Values)
            {
                int section = (int)Math.Round(node.UnscaledZ);
                if (!bySection.TryGetValue(section, out List<MorphologyNode> list))
                {
                    list = [];
                    bySection[section] = list;
                }

                list.Add(node);
            }

            return bySection;
        }

        private static void CurveFitProcessChain(
            MorphologyGraph graph,
            ulong[] process,
            Dictionary<int, List<MorphologyNode>> nodesBySection,
            CurveFitOptions options)
        {
            if (process.Length < 3)
                return;

            double chainLengthBefore = ProcessChainLinkLength(graph, process);

            MorphologyNode[] nodes = [.. process.Select(id => graph.Nodes[id])];
            Vector2[] centroids = [.. nodes.Select(n => n.Center.XY())];
            double[] z = [.. nodes.Select(n => n.Z)];

            List<(int Index, double Residual)> movable = [];
            for (int i = 0; i < nodes.Length; i++)
            {
                MorphologyNode node = nodes[i];
                if (!IsCurveFitMovable(node, graph))
                    continue;
                if (options.OnlyLocationIds is not null && !options.OnlyLocationIds.Contains(node.Key))
                    continue;

                Vector2 fitted = EvaluateLeaveOneOutCentroid(centroids, z, i, options.HalfWindow);
                double residual = Vector2.Distance(centroids[i], fitted);
                movable.Add((i, residual));
            }

            if (movable.Count == 0)
                return;

            movable.Sort((a, b) => b.Residual.CompareTo(a.Residual));

            foreach ((int i, _) in movable)
            {
                MorphologyNode node = nodes[i];
                Vector2 fitted = EvaluateLeaveOneOutCentroid(centroids, z, i, options.HalfWindow);
                Vector2 offset = fitted - centroids[i];
                offset = ClampProcessOffset(node, offset, options.MaxOffsetNm);
                if (offset.Magnitude <= Tolerance.Epsilon)
                    continue;

                offset = LimitOffsetToAvoidSameSectionOverlap(node, offset, nodesBySection);
                if (offset.Magnitude <= Tolerance.Epsilon)
                    continue;

                offset = LimitOffsetToAvoidLengtheningLinks(node, offset);
                if (offset.Magnitude <= Tolerance.Epsilon)
                    continue;

                TranslateNodeAndAttachedSubgraphs(graph, node, offset);
                centroids[i] = node.Center.XY();
            }

            double chainLengthAfter = ProcessChainLinkLength(graph, process);
            Debug.Assert(chainLengthAfter <= chainLengthBefore + 1e-3,
                $"CurveFitProcesses lengthened process [{string.Join(",", process)}] from {chainLengthBefore} to {chainLengthAfter}.");
        }

        static double ProcessChainLinkLength(MorphologyGraph graph, ulong[] process)
        {
            double sum = 0;
            for (int i = 0; i + 1 < process.Length; i++)
            {
                Vector2 a = graph.Nodes[process[i]].Center.XY();
                Vector2 b = graph.Nodes[process[i + 1]].Center.XY();
                sum += (a - b).Magnitude;
            }

            return sum;
        }

        static double IncidentLinkLengthSum(MorphologyNode node, Vector2 centroid)
        {
            double sum = 0;
            foreach (ulong otherId in node.Edges.Keys)
            {
                Vector2 other = node.Graph.Nodes[otherId].Center.XY();
                sum += (other - centroid).Magnitude;
            }

            return sum;
        }

        /// <summary>
        /// Refuse (or shrink) a translation that would increase the sum of this node's LocationLink XY lengths.
        /// Leave-one-out Catmull-Rom can pull a shaft node off the polyline between its neighbours; that
        /// lengthens the involved links and is not a valid registration correction.
        /// </summary>
        static Vector2 LimitOffsetToAvoidLengtheningLinks(MorphologyNode node, Vector2 offset)
        {
            Vector2 origin = node.Center.XY();
            double before = IncidentLinkLengthSum(node, origin);
            Vector2 candidate = offset;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                double after = IncidentLinkLengthSum(node, origin + candidate);
                if (after <= before + Tolerance.Epsilon)
                    return candidate;

                candidate *= 0.5;
            }

            Trace.WriteLine(
                $"CurveFitProcesses: location {node.Key} left in place; proposed offset ({offset.Magnitude:G4}) " +
                $"would lengthen incident LocationLinks from {before:G6} to {IncidentLinkLengthSum(node, origin + offset):G6}.");
            return Vector2.Zero;
        }

        /// <summary>
        /// Admission and window caps for residual-field leave-one-out sampling.
        /// Shorter both-sided windows than <see cref="DefaultCurveFitHalfWindow"/> so horizontal travel stays in the curve.
        /// </summary>
        public readonly struct ResidualWindowOptions
        {
            public static ResidualWindowOptions Default => new(2, 3, 5, 7);

            public ResidualWindowOptions(int minBoth, int maxBoth, int minOne, int maxOne)
            {
                MinBoth = minBoth < 1 ? 1 : minBoth;
                MaxBoth = maxBoth < MinBoth ? MinBoth : maxBoth;
                MinOne = minOne < 1 ? 1 : minOne;
                MaxOne = maxOne < MinOne ? MinOne : maxOne;
            }

            public int MinBoth { get; }
            public int MaxBoth { get; }
            public int MinOne { get; }
            public int MaxOne { get; }
        }

        /// <summary>
        /// Catmull-Rom evaluation at <paramref name="i"/> using up to <paramref name="halfWindow"/> neighbors
        /// on each side, excluding the node itself so its jitter does not enter the fit.
        /// Used by <see cref="CurveFitProcesses"/>; always returns a prediction (may copy a lone neighbor).
        /// </summary>
        internal static Vector2 EvaluateLeaveOneOutCentroid(Vector2[] centroids, double[] z, int i, int halfWindow)
        {
            List<int> controlIdx = [];
            int lo = Math.Max(0, i - halfWindow);
            int hi = Math.Min(centroids.Length - 1, i + halfWindow);
            for (int j = lo; j <= hi; j++)
            {
                if (j == i)
                    continue;
                controlIdx.Add(j);
            }

            return FitLeaveOneOutAtZ(centroids, z, i, controlIdx) ?? centroids[i];
        }

        /// <summary>
        /// Leave-one-out Catmull-Rom with asymmetric admission for residual-field samples.
        /// Both sides require <see cref="ResidualWindowOptions.MinBoth"/> each; one-sided requires
        /// <see cref="ResidualWindowOptions.MinOne"/>. Returns null when the sample is not admitted
        /// (unlike <see cref="EvaluateLeaveOneOutCentroid"/>, which always returns a value).
        /// </summary>
        public static Vector2? TryEvaluateAsymmetricLeaveOneOut(
            Vector2[] centroids,
            double[] z,
            int i,
            ResidualWindowOptions options = default)
        {
            if (centroids is null || z is null || centroids.Length != z.Length)
                return null;
            if (i < 0 || i >= centroids.Length)
                return null;

            if (options.MinBoth < 1 && options.MaxBoth < 1 && options.MinOne < 1 && options.MaxOne < 1)
                options = ResidualWindowOptions.Default;

            int belowAvailable = i;
            int aboveAvailable = centroids.Length - 1 - i;

            List<int> controlIdx = [];
            if (belowAvailable >= 1 && aboveAvailable >= 1)
            {
                if (belowAvailable < options.MinBoth || aboveAvailable < options.MinBoth)
                    return null;

                int takeBelow = Math.Min(belowAvailable, options.MaxBoth);
                int takeAbove = Math.Min(aboveAvailable, options.MaxBoth);
                for (int j = i - takeBelow; j < i; j++)
                    controlIdx.Add(j);
                for (int j = i + 1; j <= i + takeAbove; j++)
                    controlIdx.Add(j);
            }
            else if (belowAvailable == 0 && aboveAvailable >= options.MinOne)
            {
                int takeAbove = Math.Min(aboveAvailable, options.MaxOne);
                for (int j = i + 1; j <= i + takeAbove; j++)
                    controlIdx.Add(j);
            }
            else if (aboveAvailable == 0 && belowAvailable >= options.MinOne)
            {
                int takeBelow = Math.Min(belowAvailable, options.MaxOne);
                for (int j = i - takeBelow; j < i; j++)
                    controlIdx.Add(j);
            }
            else
            {
                return null;
            }

            // Need at least two controls for a real curve (not a point copy).
            if (controlIdx.Count < 2)
                return null;

            return FitLeaveOneOutAtZ(centroids, z, i, controlIdx);
        }

        /// <summary>
        /// Catmull-Rom (or linear fallback) at <paramref name="i"/>'s Z using the given control indices.
        /// Null when controls cannot produce a prediction.
        /// </summary>
        static Vector2? FitLeaveOneOutAtZ(Vector2[] centroids, double[] z, int i, List<int> controlIdx)
        {
            if (controlIdx is null || controlIdx.Count == 0)
                return null;
            if (controlIdx.Count == 1)
                return centroids[controlIdx[0]];

            double queryZ = z[i];
            int seg = 0;
            while (seg + 1 < controlIdx.Count && z[controlIdx[seg + 1]] < queryZ)
                seg++;

            if (seg + 1 >= controlIdx.Count)
                return centroids[controlIdx[controlIdx.Count - 1]];

            int i1 = controlIdx[seg];
            int i2 = controlIdx[seg + 1];
            double dz = z[i2] - z[i1];
            if (Math.Abs(dz) < Tolerance.Epsilon)
                return centroids[i1];

            if (Vector2.DistanceSquared(centroids[i1], centroids[i2]) <= Tolerance.EpsilonSquared)
                return centroids[i1];

            double t = (queryZ - z[i1]) / dz;
            if (t < 0)
                t = 0;
            else if (t > 1)
                t = 1;

            int i0 = seg > 0 ? controlIdx[seg - 1] : i1;
            int i3 = seg + 2 < controlIdx.Count ? controlIdx[seg + 2] : i2;

            Vector2[] fitted = CatmullRom.FitCurveSegment(
                centroids[i0], centroids[i1], centroids[i2], centroids[i3], [t]);
            if (fitted is null || fitted.Length == 0 || double.IsNaN(fitted[0].X) || double.IsNaN(fitted[0].Y))
                return centroids[i1] + ((centroids[i2] - centroids[i1]) * t);

            return fitted[0];
        }

        /// <summary>
        /// Shrink a smoothing translation until the moved contour no longer intersects another contour of the same
        /// structure on the same section that it did not already intersect.  Two processes of one cell running side
        /// by side are often closer than a large registration hop; pushing one into the other creates a
        /// same-section overlap the Bajaj tiler cannot handle (RPC1 108506/108520: clean raw, inconsistent after
        /// smoothing), and that was the largest remaining failure class in the Muller glia after the generator fixes.
        /// Halving is tried three times before the node is left where it was annotated.
        /// </summary>
        private static Vector2 LimitOffsetToAvoidSameSectionOverlap(
            MorphologyNode node,
            Vector2 offset,
            Dictionary<int, List<MorphologyNode>> nodesBySection)
        {
            int section = (int)Math.Round(node.UnscaledZ);
            if (!nodesBySection.TryGetValue(section, out List<MorphologyNode> sectionNodes))
                return offset;

            Rectangle reach = node.Geometry.BoundingBox();
            double pad = offset.Magnitude;
            List<MorphologyNode> neighbours = [];
            foreach (MorphologyNode other in sectionNodes)
            {
                if (other.Key == node.Key)
                    continue;

                Rectangle otherBox = other.Geometry.BoundingBox();
                if (otherBox.Left > reach.Right + pad || otherBox.Right < reach.Left - pad
                    || otherBox.Bottom > reach.Top + pad || otherBox.Top < reach.Bottom - pad)
                    continue;

                if (node.Geometry.STIntersects(other.Geometry).IsTrue)
                    continue;

                neighbours.Add(other);
            }

            if (neighbours.Count == 0)
                return offset;

            Vector2 candidate = offset;
            List<ulong> blockers = null;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                SqlGeometry moved = node.Geometry.Translate(candidate);
                blockers = null;
                foreach (MorphologyNode other in neighbours)
                {
                    if (!moved.STIntersects(other.Geometry).IsTrue)
                        continue;
                    blockers ??= [];
                    blockers.Add(other.Key);
                }

                if (blockers is null)
                    return candidate;

                candidate *= 0.5;
            }

            // blockers are from the last (smallest) attempted offset — 1/8 of the proposed move.
            Trace.WriteLine(
                $"CurveFitProcesses: location {node.Key} left in place on section {section}; " +
                $"even 1/8 of the proposed offset ({offset.Magnitude:G4}) overlaps same-section neighbour(s) [{string.Join(", ", blockers)}].");
            return Vector2.Zero;
        }

        /// <summary>
        /// When <paramref name="maxOffsetNm"/> is null, returns <paramref name="offset"/> unchanged.
        /// When set, caps magnitude to that many nanometres.
        /// </summary>
        private static Vector2 ClampProcessOffset(MorphologyNode node, Vector2 offset, double? maxOffsetNm)
        {
            double length = offset.Magnitude;
            if (length <= Tolerance.Epsilon)
                return Vector2.Zero;

            if (maxOffsetNm is null || maxOffsetNm.Value <= Tolerance.Epsilon)
                return offset;

            double maxOffset = maxOffsetNm.Value;
            if (length <= maxOffset)
                return offset;

            return offset * (maxOffset / length);
        }

        /// <summary>
        /// Rigidly translate a process node and every child subgraph whose nearest parent location is that node.
        /// </summary>
        internal static void TranslateNodeAndAttachedSubgraphs(MorphologyGraph graph, MorphologyNode node, Vector2 offset)
        {
            node.Geometry = node.Geometry.Translate(offset);

            foreach (KeyValuePair<ulong, ulong> pair in graph.NearestNodeToSubgraph)
            {
                if (pair.Value != node.Key)
                    continue;
                if (!graph.Subgraphs.TryGetValue(pair.Key, out MorphologyGraph child))
                    continue;
                TranslateSubgraphGeometry(child, offset);
            }
        }

        private static void TranslateSubgraphGeometry(MorphologyGraph subgraph, Vector2 offset)
        {
            foreach (MorphologyNode n in subgraph.Nodes.Values)
                n.Geometry = n.Geometry.Translate(offset);

            subgraph._RTree = null;
            subgraph.ResetCachedMeasurements();

            foreach (MorphologyGraph nested in subgraph.Subgraphs.Values)
                TranslateSubgraphGeometry(nested, offset);
        }
    }
}
