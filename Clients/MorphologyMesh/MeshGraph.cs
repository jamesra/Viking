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

    }

    public class MeshEdge : GraphLib.Edge<ulong>
    {
        public MeshEdge(ulong SourceNode, ulong TargetNode) : base(SourceNode, TargetNode, false)
        {            
        }

    }


    public class MeshNode : GraphLib.Node<ulong, MeshEdge>
    {
        public DynamicRenderMesh<ulong> Mesh = null;

        public ConnectionVerticies UpperPort; //Connection point if attaching geometry above this node in Z
        public ConnectionVerticies LowerPort; //Connection point if attaching geometry below this node in Z

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
        /// Return edges conneted to nodes above this node in Z
        /// </summary>
        public ulong[] GetEdgesAbove(MeshGraph graph)
        {
            return this.Edges.Where(e => graph.Nodes[e.Key].Z > this.Z).Select(e => e.Key).ToArray();
        }

        /// <summary>
        /// Return edges conneted to nodes above this node in Z
        /// </summary>
        public ulong[] GetEdgesBelow(MeshGraph graph)
        {
            return this.Edges.Where(e => graph.Nodes[e.Key].Z < this.Z).Select(e => e.Key).ToArray();
        }


        public MeshNode(ulong key) : base(key)
        {
            UpperPort = null;
            LowerPort = null;
        }

        public override string ToString()
        {
            return Key.ToString();
        }
    }
}
