using Geometry;
using GraphLib;
using System;

namespace AnnotationVizLib
{
    [Serializable]
    public class MorphologyEdge(MorphologyGraph graph, ulong A, ulong B) : Edge<ulong>(A < B ? A : B, A < B ? B : A, false)
    {
        //Structure this edge is part of
        public MorphologyGraph Graph = graph;

        public override bool Directional => false;

        public MorphologyEdge(MorphologyGraph graph, long A, long B)
            : this(graph, (ulong)A, (ulong)B)
        {
        }

        /// <summary>
        /// Return the other node connected by the edge
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public ulong OtherNode(ulong key)
        {
            if (key != SourceNodeKey && key != TargetNodeKey)
            {
                throw new ArgumentException("Key must match a node ID connected by the edge");
            }

            return SourceNodeKey == key ? TargetNodeKey : SourceNodeKey;
        }

        public override string ToString() => this.SourceNodeKey.ToString() + "-" + this.TargetNodeKey.ToString();

        private double? _DistanceCenterToCenter = new double?(); //Distance between node centers
        /// <summary>
        /// The length between the centers of the two connected nodes.
        /// </summary>
        public double DistanceCenterToCenter
        {
            get
            {
                if (!_DistanceCenterToCenter.HasValue)
                {
                    _DistanceCenterToCenter = Vector3.Distance(Graph.Nodes[this.SourceNodeKey].Center,
                                                                   Graph.Nodes[this.TargetNodeKey].Center);
                }

                return _DistanceCenterToCenter.Value;
            }
        }
    }
}
