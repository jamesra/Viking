using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnnotationVizLib;
using Geometry;
using Geometry.Meshing;

namespace MorphologyMesh
{
    public class MeshGraph : GraphLib.Graph<ulong, MeshNode, MeshEdge>
    {
        public double SectionThickness = 0;

    }

    public class MeshEdge : GraphLib.Edge<ulong>
    {
        public ConnectionVerticies SourcePort;
        public ConnectionVerticies TargetPort; 

        public MeshEdge(ulong SourceNode, ulong TargetNode, ConnectionVerticies sourcePort, ConnectionVerticies targetPort) : base(SourceNode, TargetNode, false)
        {
            this.SourcePort = sourcePort;
            this.TargetPort = targetPort;
        }

        public MeshEdge(ulong SourceNode, ulong TargetNode) : base(SourceNode, TargetNode, false)
        {
            this.SourcePort = null;
            this.TargetPort = null;
        }

        public ConnectionVerticies GetPortForNode(ulong NodeID)
        {
            if(NodeID == this.SourceNodeKey)
            {
                return SourcePort;
            }

            if(NodeID == this.TargetNodeKey)
            {
                return TargetPort; 
            }

            throw new ArgumentException("Node ID not part of edge");
        }

        public ConnectionVerticies GetOppositePortForNode(ulong NodeID)
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

        public override string ToString()
        {
            return string.Format("{0}-{1}", SourceNodeKey, TargetNodeKey);
        }
    }


    public class MeshNode : GraphLib.Node<ulong, MeshEdge>
    {
        public Mesh3D<IVertex3D<ulong>> Mesh = null; 

        public bool UpperPortCapped = false; //True if faces have been generated
        public bool LowerPortCapped = false; //True if faces have been generated

        public Dictionary<ulong, ConnectionVerticies> IDToCrossSection = new Dictionary<ulong, ConnectionVerticies>();

        
        private ConnectionVerticies _CapPort;
        public ConnectionVerticies CapPort
        {
            get
            {
                return _CapPort;
            }
            set
            {
                _CapPort = value;
                this.IDToCrossSection[this.Key] = value;
            }
        }
        
        //public ConnectionVerticies CapPort;

        public bool AdjacentToPolygon = false; 


        //public GridVector3 UpperCentroid;
        //public GridVector3 LowerCentroid;

        public MeshGraph MeshGraph
        {
            get; set;
        }

        public GridBox BoundingBox
        {
            get
            {
                return Mesh.BoundingBox;
            }
        }

        public double Z
        {
            get
            {
                return BoundingBox.CenterPoint.Z;
            }
        }

        /// <summary>
        /// Z level of the cap port connection
        /// </summary>
        public double CapPortZ;

        /*
        private GridPolygon _ShapeAsPolygon;

        public GridPolygon ShapeAsPolygon
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
            if(graph == null)
            {
                graph = this.MeshGraph;
            }

            return this.Edges.Where(e => this.IsNodeAbove(graph.Nodes[e.Key])).Select(e => e.Key).ToArray();
        }

        /// <summary>
        /// Return edges conneted to nodes above this node in Z
        /// </summary>
        public ulong[] GetEdgesBelow(MeshGraph graph = null)
        {
            if (graph == null)
            {
                graph = this.MeshGraph;
            }

            return this.Edges.Where(e => this.IsNodeBelow(graph.Nodes[e.Key])).Select(e => e.Key).ToArray();
        }

        public bool IsNodeAbove(MeshNode other)
        {
            return other.Z > this.Z;
        }

        public bool IsNodeBelow(MeshNode other)
        {
            return other.Z < this.Z;
        }


        public MeshNode(ulong key) : base(key)
        {
        }

        public override string ToString()
        {
            return Key.ToString() + " Z: " + Z.ToString();
        }
    }
}
