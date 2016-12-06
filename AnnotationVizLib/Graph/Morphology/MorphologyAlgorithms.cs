using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlGeometryUtils;
using Geometry;

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
            RTree.RTree<ulong> rtree = new RTree.RTree<ulong>();

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
            List<SortedSet<ulong>> sorted_subgraphs = subgraphs.OrderBy(s => s.Count).ToList();

            //OK find the nearest point between the subgraphs.
            while(sorted_subgraphs.Count > 1)
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
            SortedSet<ulong> subgraphUnion = new SortedSet<ulong>(subgraphs[0]);
            for(int i = 1; i < subgraphs.Count; i++)
            {
                foreach(ulong id in subgraphs[i])
                {
                    subgraphUnion.Add(id);
                }
            }
            
            RTree.RTree<ulong> UnionRTree = this.CreateRTreeForSubgraph(subgraphUnion);

            

            SortedList<ulong, double> distances = new SortedList<ulong, double>();

            ulong nearest_node_id = 0;
            MorphologyEdge best_edge = null;
            double nearest_node_distance = double.MaxValue;
            
            //Check each node in our subgraph to find the nearest node in the subgraphs we want to merge into
            foreach (ulong key in SubgraphToMerge)
            {
                MorphologyNode node = this.Nodes[key];

                SortedSet<ulong> candidates = FindNearestCandidatesFromRTree(UnionRTree, node);

                double min_distance;
                ulong nearest = NearestNode(key, candidates, out min_distance);
                if (min_distance < nearest_node_distance)
                {
                    best_edge = new MorphologyEdge(this, key, nearest);
                    nearest_node_distance = min_distance;
                    nearest_node_id = nearest;
                }
            }

            if (best_edge == null)
                throw new ArgumentException("Unexpected error in MergeSubgraph.  Could not find an edge between subgraphs.");

            this.AddEdge(best_edge);

            //Add the subgraph we merged to the subgraph in the list
            MergeSubgraphs(SubgraphToMerge, nearest_node_id, subgraphs);
        } 

        private void MergeSubgraphs(SortedSet<ulong> SubgraphToMerge, ulong node_to_merge_onto, IList<SortedSet<ulong>> subgraphs)
        {
            foreach (SortedSet<ulong> subgraph in subgraphs)
            {
                if(subgraph.Contains(node_to_merge_onto))
                {
                    foreach(ulong key in SubgraphToMerge)
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
            return NearestNode(shape_to_check, new SortedSet<ulong>(this.Nodes.Keys), out min_distance);
        }

        /// <summary>
        /// Get a list of at least 8 nodes from the RTree that we should check for proximity to the shape_to_check
        /// </summary>
        /// <param name="shape_to_check"></param>
        /// <returns></returns>
        private static SortedSet<ulong> FindNearestCandidatesFromRTree(RTree.RTree<ulong> rtree, IGeometry shape_to_check)
        {
            List<ulong> found_nodes = new List<ulong>();

            double scale_factor = 2.0;
            while(found_nodes.Count < 8 && found_nodes.Count != rtree.Count)
            {
                GridBox bbox = shape_to_check.BoundingBox;
                bbox.Scale(scale_factor);
                found_nodes = rtree.Intersects(bbox.ToRTreeRect());
                scale_factor *= 2.0;
            }

            return new SortedSet<ulong>(found_nodes);
        }

        private ulong NearestNode(IGeometry shape_to_check, SortedSet<ulong> nodes_to_compare, out double min_distance)
        {  
            min_distance = double.MaxValue;
            ulong Nearest = ulong.MaxValue;

            foreach(ulong compare_id in nodes_to_compare)
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

                if(pair_distance_3D < min_distance)
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
            var nodes_to_remove = graph.GetProcess();

            foreach (ulong key in nodes_to_remove)
            {
                graph.RemoveNodePreserveEdges(key);
            }

            //Once in a while, we have a branch attached to a cycle.  This allows the cycle to be removed, and then the branch to be removed in a second-pass if needed
            if (graph.GetProcess().Length > 0)
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
            foreach(MorphologyNode subgraphnode in other.Nodes.Values)
            {
                double node_min_distance;
                ulong id = NearestNode(subgraphnode, out node_min_distance);
                if(node_min_distance < min_distance)
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

            for(int iStart = 0; iStart < path.Count() - 1; iStart++)
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
            if(A.Nodes.Count > B.Nodes.Count)
            {
                MorphologyGraph C = A;
                A = B;
                B = C;
            }

            double minDistance = double.MaxValue;

            foreach (MorphologyNode N in A.Nodes.Values)
            {
                double node_min_distance;
                A.NearestNode(B, out node_min_distance);
                if(node_min_distance < minDistance)
                {
                    minDistance = node_min_distance;
                }
            }

            return minDistance;
        }
    }
}
