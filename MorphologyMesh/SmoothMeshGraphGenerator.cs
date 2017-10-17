using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Geometry.Meshing;
using AnnotationVizLib;
using SqlGeometryUtils;

namespace MorphologyMesh
{
    public class SmoothMeshGraphGenerator
    {
        public static MeshNode CreateNode(MorphologyNode node)
        {
            MeshNode mNode = new MeshNode(node.Key); 
            mNode.Mesh = CreateNodeMesh(node);
            mNode.CapPort = CreatePort(node); //Create a port we can use to cap off the node if needed
            return mNode;
        }

        private static bool IsAdjacentToPolygon(MorphologyNode node)
        {
            return node.Edges.Keys.Select((nodeKey) => node.Graph.Nodes[nodeKey].Location.TypeCode).Any((code) => code != LocationType.CIRCLE);
        }

        /// <summary>
        /// Create a mesh of verticies only for the specified shape
        /// </summary>
        /// <param name="mNode"></param>
        /// <param name="shape"></param>
        /// <param name="Z"></param>
        public static DynamicRenderMesh<ulong> CreateNodeMesh(MorphologyNode node)
        {
            IShape2D shape = node.Geometry.ToShape2D();
            
            double Z = -node.Z;

            return CreateNodeMesh(shape, Z, node.ID, IsAdjacentToPolygon(node)); 
        }

        /// <summary>
        /// Create a mesh of verticies only for the specified shape
        /// </summary>
        /// <param name="mNode"></param>
        /// <param name="shape"></param>
        /// <param name="Z"></param>
        public static DynamicRenderMesh<ulong> CreateNodeMesh(IShape2D shape, double Z, ulong NodeData, bool AdjacentToPolygon = false)
        {
            DynamicRenderMesh<ulong> Mesh = new DynamicRenderMesh<ulong>();

            switch (shape.ShapeType)
            {
                case ShapeType2D.CIRCLE:
                    ICircle2D circle = shape as ICircle2D;
                    //TODO: Check if adjacent mesh nodes are polygons and add more points in a circle if they are.
                    int NumPointsOnCircle = AdjacentToPolygon  ? SmoothMeshGenerator.NumPointsAroundCircleAdjacentToPolygon : SmoothMeshGenerator.NumPointsAroundCircle;
                    Mesh.AddVertex(ShapeMeshGenerator<ulong>.CreateVerticiesForCircle(circle, Z, NumPointsOnCircle, NodeData, GridVector3.Zero));
                    break;
                case ShapeType2D.POLYGON:
                    IPolygon2D poly = shape as IPolygon2D;
                    Mesh.AddVertex(ShapeMeshGenerator<ulong>.CreateVerticiesForPolygon(poly, Z, NodeData, GridVector3.Zero));
                    break;
                case ShapeType2D.POLYLINE:
                    IPolyLine2D polyline = shape as IPolyLine2D;
                    Mesh.AddVertex(ShapeMeshGenerator<ulong>.CreateVerticiesForPolyline(polyline, Z, NodeData, GridVector3.Zero));
                    break;
                case ShapeType2D.POINT:
                    IPoint2D point = shape as IPoint2D;
                    Mesh.AddVertex(ShapeMeshGenerator<ulong>.CreateVerticiesForPoint(point, Z, NodeData, GridVector3.Zero));
                    break;
                default:
                    throw new ArgumentException("Unexpected shape type");
            }

            return Mesh;
        }

        public static ConnectionVerticies CreatePort(MorphologyNode node)
        {
            return CreatePort(node.Geometry.ToShape2D(), IsAdjacentToPolygon(node));
        }

        public static ConnectionVerticies CreatePort(IShape2D shape, bool AdjacentToPolygon = false)
        {  
            switch (shape.ShapeType)
            {
                case ShapeType2D.CIRCLE:
                    ICircle2D circle = shape as ICircle2D;
                    //TODO: Check if adjacent mesh nodes are polygons and add more points in a circle if they are.
                    int NumPointsOnCircle = AdjacentToPolygon ? SmoothMeshGenerator.NumPointsAroundCircleAdjacentToPolygon : SmoothMeshGenerator.NumPointsAroundCircle;
                    return ConnectionVerticies.CreatePort(circle, NumPointsOnCircle);
                case ShapeType2D.POLYGON:
                    IPolygon2D poly = shape as IPolygon2D;
                    return ConnectionVerticies.CreatePort(poly);
                case ShapeType2D.POLYLINE:
                    IPolyLine2D polyline = shape as IPolyLine2D;
                    return ConnectionVerticies.CreatePort(polyline);
                case ShapeType2D.POINT:
                    IPoint2D point = shape as IPoint2D;
                    return ConnectionVerticies.CreatePort(point);
                default:
                    throw new ArgumentException("Unexpected shape type");
            }
        }

        public static MeshEdge CreateEdge(MorphologyNode Source, MorphologyNode Target)
        {
            return new MeshEdge(Source.ID, Target.ID, CreatePort(Source), CreatePort(Target));
        }
    }
}
