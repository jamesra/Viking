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
        /// Find isolated subgraphs, find the nearest locations between them and create a fake LocationLink
        /// </summary>
        protected static void ConnectIsolatedSubgraphs(MorphologyGraph graph)
        {
            IList<SortedSet<ulong>> subgraphs = MorphologyGraph.IsolatedSubgraphs(graph);

            if (subgraphs.Count <= 1)
                return;

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

        private RTree.RTree<ulong> CreateRTreeForSubgraph(ICollection<ulong> subgraph)
        {
            RTree.RTree<ulong> rtree = new RTree.RTree<ulong>();

            foreach(ulong key in subgraph)
            {
                MorphologyNode node = Nodes[key];

                RTree.Rectangle bbox = node.BoundingBox.ToRTreeRect();

                rtree.Add(bbox, key);
            }

            return rtree;
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
            
            //RTree.RTree<ulong> UnionRTree = this.CreateRTreeForSubgraph(subgraphUnion);

            SortedList<ulong, double> distances = new SortedList<ulong, double>();

            ulong nearest_node_id = 0;
            MorphologyEdge best_edge = null;
            double nearest_node_distance = double.MaxValue;

            foreach (ulong key in SubgraphToMerge)
            {
                MorphologyNode node = this.Nodes[key];

                double min_distance;
                ulong nearest = NearestNode(key, subgraphUnion, out min_distance);
                if (min_distance < nearest_node_distance)
                {
                    best_edge = new MorphologyEdge(key, nearest);
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

            min_distance = double.MaxValue;
            ulong Nearest = ulong.MaxValue;

            foreach(ulong compare_id in nodes_to_compare)
            {
                MorphologyNode compare_node = this.Nodes[compare_id];
                double z_distance = Math.Abs(node.Z - compare_node.Z);

                //Don't bother with the expensive geometry check if the Z distance puts us out of contending for minimum distance
                if (z_distance > min_distance)
                    continue;

                double pair_distance = node.VolumeShape.STDistance(compare_node.VolumeShape).Value;
                
                double pair_distance_3D = (pair_distance * pair_distance) + (z_distance * z_distance);
                pair_distance_3D = Math.Sqrt(pair_distance_3D);

                if(pair_distance_3D < min_distance)
                {
                    min_distance = pair_distance;
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
        }
    }
}
