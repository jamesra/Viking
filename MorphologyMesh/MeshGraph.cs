using Geometry;
using Geometry.Meshing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MorphologyMesh
{
    public class MeshGraph : GraphLib.Graph<ulong, MeshNode, MeshEdge>
    {
        public double SectionThickness = 0;

    }

    public class MeshEdge : GraphLib.Edge<ulong>
    {
        public ConnectionVertices SourcePort;
        public ConnectionVertices TargetPort;

        public MeshEdge(ulong SourceNode, ulong TargetNode, ConnectionVertices sourcePort, ConnectionVertices targetPort) : base(SourceNode, TargetNode, false)
        {
            this.SourcePort = sourcePort;
            this.TargetPort = targetPort;
        }

        public MeshEdge(ulong SourceNode, ulong TargetNode) : base(SourceNode, TargetNode, false)
        {
            this.SourcePort = null;
            this.TargetPort = null;
        }

        public ConnectionVertices GetPortForNode(ulong NodeID)
        {
            if (NodeID == this.SourceNodeKey)
            {
                return SourcePort;
            }

            if (NodeID == this.TargetNodeKey)
            {
                return TargetPort;
            }

            throw new ArgumentException("Node ID not part of edge");
        }

        public ConnectionVertices GetOppositePortForNode(ulong NodeID)
        {
            if (NodeID == this.SourceNodeKey)
            {
                return TargetPort;
            }

            if (NodeID == this.TargetNodeKey)
            {
                return SourcePort;
            }

            throw new ArgumentException("Node ID not part of edge");
        }

        public override string ToString() => string.Format("{0}-{1}", SourceNodeKey, TargetNodeKey);
    }


    public class MeshNode(ulong key) : GraphLib.Node<ulong, MeshEdge>(key)
    {
        public Mesh3D<IVertex3D<ulong>> Mesh = null;

        public bool UpperPortCapped = false; //True if faces have been generated
        public bool LowerPortCapped = false; //True if faces have been generated

        public Dictionary<ulong, ConnectionVertices> IDToCrossSection = [];


        private ConnectionVertices _CapPort;
        public ConnectionVertices CapPort
        {
            get => _CapPort;
            set
            {
                _CapPort = value;
                this.IDToCrossSection[this.Key] = value;
            }
        }

        //public ConnectionVertices CapPort;

        public bool AdjacentToPolygon = false;


        //public Vector3 UpperCentroid;
        //public Vector3 LowerCentroid;

        public MeshGraph MeshGraph
        {
            get; set;
        }

        public Box BoundingBox => Mesh.BoundingBox;

        public double Z => BoundingBox.CenterPoint.Z;

        /// <summary>
        /// Z level of the cap port connection
        /// </summary>
        public double CapPortZ;

        /*
        private Polygon _ShapeAsPolygon;

        public Polygon ShapeAsPolygon
        {
            get
            {
                return _ShapeAsPolygon;
            }
        }
        */

        /// <summary>
        /// Return edges conneted to nodes above this node in Z
        /// </summary>
        public ulong[] GetEdgesAbove(MeshGraph graph = null)
        {
            graph ??= this.MeshGraph;

            return [.. this.Edges.Where(e => this.IsNodeAbove(graph.Nodes[e.Key])).Select(e => e.Key)];
        }

        /// <summary>
        /// Return edges conneted to nodes above this node in Z
        /// </summary>
        public ulong[] GetEdgesBelow(MeshGraph graph = null)
        {
            graph ??= this.MeshGraph;

            return [.. this.Edges.Where(e => this.IsNodeBelow(graph.Nodes[e.Key])).Select(e => e.Key)];
        }

        public bool IsNodeAbove(MeshNode other) => other.Z > this.Z;

        public bool IsNodeBelow(MeshNode other) => other.Z < this.Z;

        public override string ToString() => Key.ToString() + " Z: " + Z.ToString();
    }
}
