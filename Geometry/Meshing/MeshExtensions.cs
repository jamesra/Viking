using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry.Meshing
{
    public static class MeshExtensions
    {
        /// <summary>
        /// Create a mesh from a set of triangles
        /// </summary>
        /// <param name="triangles"></param>
        /// <returns></returns>
        public static DynamicRenderMesh ToDynamicRenderMesh(this ICollection<GridTriangle> triangles)
        {
            DynamicRenderMesh mesh = new Meshing.DynamicRenderMesh();
            Dictionary<GridVector2, int> PointToVertexIndex = new Dictionary<GridVector2, int>();

            foreach (GridVector2 v in triangles.SelectMany(tri => tri.Points).Distinct())
            {
                int index = mesh.AddVertex(new Vertex(v.ToGridVector3(0)));
                PointToVertexIndex.Add(v, index);
            }

            foreach (GridLineSegment segment in triangles.SelectMany(tri => tri.Segments).Distinct())
            {
                int vertexA = PointToVertexIndex[segment.A];
                int vertexB = PointToVertexIndex[segment.B];
                mesh.AddEdge(vertexA, vertexB);
            }

            foreach (GridTriangle tri in triangles)
            {
                int vertexA = PointToVertexIndex[tri.p1];
                int vertexB = PointToVertexIndex[tri.p2];
                int vertexC = PointToVertexIndex[tri.p3];

                mesh.AddFace(new Face(vertexA, vertexB, vertexC));
            }

            return mesh;
        }

        public static bool IsTriangle(this IFace face)
        {
            return face.iVerts.Length == 3;
        }

        public static bool IsQuad(this IFace face)
        {
            return face.iVerts.Length == 4;
        }
    }
}
