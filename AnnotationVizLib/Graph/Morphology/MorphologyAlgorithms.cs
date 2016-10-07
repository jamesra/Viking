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
        /// <summary>
        /// Find isolated subgraphs, find the nearest locations between them and create a fake LocationLink
        /// </summary>
        public void ConnectIsolatedSubgraphs()
        {
            IList<SortedSet<ulong>> subgraphs = MorphologyGraph.IsolatedSubgraphs(this);

            if (subgraphs.Count <= 1)
                return;

            //OK find the nearest point between the subgraphs.
            while(subgraphs.Count > 1)
            {
                //Pop the first subgraph from the list
                SortedSet<ulong> SubgraphToMerge = subgraphs[0];
                subgraphs.RemoveAt(0);


            }
        }

        private RTree.RTree<ulong> CreateRTreeForSubgraph(ICollection<ulong> subgraph)
        {
            RTree.RTree<ulong> rtree = new RTree.RTree<ulong>();

            foreach(ulong key in subgraph)
            {
                MorphologyNode node = Nodes[key];

                RTree.Rectangle bbox = node.Location.VolumeShape.BoundingBox().ToRTreeRect((float)node.Location.VolumePosition.Z);

                rtree.Add(bbox, key);
            }

            return rtree;
        }

        private void MergeSubgraph(SortedSet<ulong> SubgraphToMerge, IList<SortedSet<ulong>> subgraphs)
        {
            double[] Distances = new double[SubgraphToMerge.Count];
            foreach(ulong key in SubgraphToMerge)
            {
                MorphologyNode node = this.Nodes[key];
            }
        }

        
    }
}
