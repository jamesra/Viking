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

        public static Geometry.Meshing.Mesh3D ToMesh(this TriangleNet.Meshing.IMesh mesh)
        {
            GridVector3[] vertArray = mesh.Vertices.Select(v => v.ToGridVector3(0)).ToArray();

            Geometry.Meshing.Mesh3D output = new Geometry.Meshing.Mesh3D();

            output.AddVerticies(vertArray.Select(v => new Geometry.Meshing.Vertex3D(v)).ToArray());

            List<int> edges = new List<int>(mesh.Vertices.Count * 3);

            foreach (TriangleNet.Topology.Triangle tri in mesh.Triangles)
            {
                int[] face = new int[] { tri.GetVertexID(0), tri.GetVertexID(1), tri.GetVertexID(2) };

                GridVector2[] verts = face.Select(f => vertArray[f].XY()).ToArray();

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
            PositionColorMeshModel meshModel = new PositionColorMeshModel();
            VertexPositionColor[] vertArray = mesh.Vertices.Select(v => new VertexPositionColor(new Vector3((float)v.X, (float)v.Y, 0), color)).ToArray();
            meshModel.Verticies = vertArray;

            List<int> edges = new List<int>(mesh.Vertices.Count * 3);

            foreach (TriangleNet.Topology.Triangle tri in mesh.Triangles)
            {
                GridVector2[] verts = new GridVector2[] { vertArray[tri.GetVertexID(0)].Position.ToGridVector3().XY(),
                                                  vertArray[tri.GetVertexID(1)].Position.ToGridVector3().XY(),
                                                  vertArray[tri.GetVertexID(2)].Position.ToGridVector3().XY()};

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

            meshModel.Edges = edges.ToArray(); 
            return meshModel;
        }
    }
}
