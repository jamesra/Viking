using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnnotationVizLib;
using Geometry;
using Geometry.Meshing;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using TriangleNet;
using TriangleNet.Meshing;
using TriangleNet.Geometry;

namespace MorphologyMesh
{ 
    

    public class TopologyMeshGenerator
    {
        static public int NumPointsAroundCircle = 16;
        /// <summary>
        /// Generate a mesh for a cell
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        public List<DynamicRenderMesh<ulong>> Generate(MorphologyGraph graph)
        {
            List<DynamicRenderMesh<ulong>> listMeshes = new List<DynamicRenderMesh<ulong>>();
            //Adjust the verticies so the models are centered on zero
            GridVector3 translate = -graph.BoundingBox.CenterPoint;

            GraphLib.Graph<ulong, MeshNode, MeshEdge> meshGraph = new GraphLib.Graph<ulong, MeshNode, MeshEdge>();

            foreach (MorphologyNode node in graph.Nodes.Values)
            {
                IShape2D shape = node.Geometry.ToShape2D();

                switch (shape.ShapeType)
                {
                    case ShapeType2D.CIRCLE:
                        listMeshes.Add(ShapeMeshGenerator<ulong>.CreateMeshForDisc(shape as ICircle2D, node.Z, graph.SectionThickness, TopologyMeshGenerator.NumPointsAroundCircle, node.ID, translate));
                        break;
                    case ShapeType2D.POLYGON:
                        listMeshes.Add(ShapeMeshGenerator<ulong>.CreateMeshForPolygonSlab(shape as IPolygon2D, node.Z, graph.SectionThickness, node.ID, translate));
                        break;
                    default:
                        throw new ArgumentException("Unexpected shape type");
                }
            }

            return listMeshes;
        }
    }

}
