using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using TriangleNet;
using TriangleNet.Geometry;
using TriangleNet.Meshing;

namespace VikingXNAGraphics
{

    public static class TriangleNetExtensions
    {
        public static TriangleNet.Geometry.IPolygon CreatePolygon(this ICollection<Geometry.GridVector2> Verticies, ICollection<GridVector2[]> InteriorPolygons = null)
        {
            TriangleNet.Geometry.Polygon poly = new TriangleNet.Geometry.Polygon(Verticies.Count);
            TriangleNet.Geometry.Vertex[] points = Verticies.Select((v, i) => new TriangleNet.Geometry.Vertex(v.X, v.Y, i)).ToArray();

            TriangleNet.Geometry.Contour contour = new TriangleNet.Geometry.Contour(points);

            foreach (TriangleNet.Geometry.Vertex p in points)
            {
                poly.Add(p);
            }

            poly.Add(contour);

            if (InteriorPolygons != null)
            {
                foreach (ICollection<GridVector2> inner_polygon in InteriorPolygons)
                {
                    TriangleNet.Geometry.Contour inner_poly = inner_polygon.CreateContour();
                    poly.Add(inner_poly, true);
                }
            }

            return poly;
        }

        public static TriangleNet.Geometry.IPolygon CreatePolygon(this GridPolygon input)
        {
            return CreatePolygon(input.ExteriorRing, input.InteriorRings);
        }

        public static TriangleNet.Geometry.Contour CreateContour(this ICollection<Geometry.GridVector2> Verticies)
        {
            TriangleNet.Geometry.Vertex[] points = Verticies.Select((v, i) => new TriangleNet.Geometry.Vertex(v.X, v.Y, i)).ToArray();
            TriangleNet.Geometry.Contour contour = new TriangleNet.Geometry.Contour(points);

            return contour;
        }
         
        public static PositionColorMeshModel CreateMeshForPolygon2D(GridVector2[] Verticies, ICollection<GridVector2[]> InteriorPolygons, Color color)
        {
            IPolygon poly = Verticies.CreatePolygon(InteriorPolygons); 
            return poly.CreateMeshForPolygon2D(color);
        }

        public static PositionColorMeshModel CreateMeshForPolygon2D(this GridPolygon input, Color color)
        {
            IPolygon poly = input.CreatePolygon();
            return poly.CreateMeshForPolygon2D(color);
        }

        public static PositionColorMeshModel CreateMeshForPolygon2D(this IPolygon polygon, Color color)
        {
            ConstraintOptions constraints = new ConstraintOptions();
            constraints.ConformingDelaunay = false;
            constraints.Convex = false; 

            QualityOptions quality = new QualityOptions();
            quality.SteinerPoints = (polygon.Points.Count / 2) + 1;

            IMesh mesh = polygon.Triangulate(constraints, quality); 
            return CreateMeshModel(mesh, color);
        }

        public static PositionColorMeshModel CreateMeshModel(this TriangleNet.Meshing.IMesh mesh, Color color)
        {
            PositionColorMeshModel meshModel = new PositionColorMeshModel();
            meshModel.Verticies = mesh.Vertices.Select(v => new VertexPositionColor(new Vector3((float)v.X, (float)v.Y, 0), color)).ToArray();

            List<int> edges = new List<int>(mesh.Vertices.Count * 3);

            foreach (TriangleNet.Topology.Triangle tri in mesh.Triangles)
            {
                edges.Add(tri.GetVertexID(0));
                edges.Add(tri.GetVertexID(1));
                edges.Add(tri.GetVertexID(2));
            }

            meshModel.Edges = edges.ToArray(); 
            return meshModel;
        }
    }
}
