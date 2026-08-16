using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using TriangleNet;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace VikingXNAGraphics
{

    public static class TriangleNetExtensions
    {
        public static PositionColorMeshModel CreateMeshForPolygon2D(Geometry.Vector2[] Vertices, ICollection<Geometry.Vector2[]> InteriorPolygons, Color color)
        {
            IPolygon poly = Vertices.CreatePolygon(InteriorPolygons);
            return poly.CreateMeshForPolygon2D(color);
        }

        public static PositionColorMeshModel CreateMeshForPolygon2D(this Geometry.Polygon input, Color color)
        {
            IPolygon poly = input.CreatePolygon();
            return poly.CreateMeshForPolygon2D(color);
        }

        public static PositionColorMeshModel CreateMeshForPolygon2D(this IPolygon polygon, Color color)
        {
            ConstraintOptions constraints = new()
            {
                ConformingDelaunay = false,
                Convex = false

            };

            QualityOptions quality = new()
            {
                SteinerPoints = (polygon.Points.Count / 2) + 1
            };

            IMesh mesh = polygon.Triangulate(constraints, quality);
            return CreateMeshModel(mesh, color);
        }

        public static Geometry.Meshing.Mesh3D ToMesh(this TriangleNet.Meshing.IMesh mesh)
        {
            Geometry.Vector3[] vertArray = [.. mesh.Vertices.Select(v => v.ToVector3(0))];

            Geometry.Meshing.Mesh3D output = new();

            output.AddVerticies([.. vertArray.Select(v => new Geometry.Meshing.Vertex3D(v))]);

            List<int> edges = new(mesh.Vertices.Count * 3);

            foreach (TriangleNet.Topology.Triangle tri in mesh.Triangles)
            {
                int[] face = [tri.GetVertexID(0), tri.GetVertexID(1), tri.GetVertexID(2)];

                Geometry.Vector2[] verts = [.. face.Select(f => vertArray[f].XY())];

                if (verts.AreClockwise())
                {
                    output.AddFace(new Geometry.Meshing.Face(face[1], face[0], face[2]));
                }
                else
                {
                    output.AddFace(new Geometry.Meshing.Face(face));
                }
            }

            return output;
        }

        /// <summary>
        /// Returns a model with counter-clockwise faces
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="color"></param>
        /// <returns></returns>
        public static PositionColorMeshModel CreateMeshModel(this TriangleNet.Meshing.IMesh mesh, Color color)
        {
            PositionColorMeshModel meshModel = new();
            VertexPositionColor[] vertArray = [.. mesh.Vertices.Select(v => new VertexPositionColor(new Vector3((float)v.X, (float)v.Y, 0), color))];
            meshModel.Vertices = vertArray;

            List<int> edges = new(mesh.Vertices.Count * 3);

            foreach (TriangleNet.Topology.Triangle tri in mesh.Triangles)
            {
                Geometry.Vector2[] verts = [ vertArray[tri.GetVertexID(0)].Position.ToVector3().XY(),
                                                  vertArray[tri.GetVertexID(1)].Position.ToVector3().XY(),
                                                  vertArray[tri.GetVertexID(2)].Position.ToVector3().XY()];

                if (verts.AreClockwise())
                {
                    edges.Add(tri.GetVertexID(1));
                    edges.Add(tri.GetVertexID(0));
                    edges.Add(tri.GetVertexID(2));
                }
                else
                {
                    edges.Add(tri.GetVertexID(0));
                    edges.Add(tri.GetVertexID(1));
                    edges.Add(tri.GetVertexID(2));
                }
            }

            meshModel.Edges = [.. edges];
            return meshModel;
        }
    }
}
