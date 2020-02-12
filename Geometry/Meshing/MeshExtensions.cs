using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry.Meshing
{
    public static class MeshExtensions
    {
        public static TriangulationMesh<IVertex2D> Clone(this TriangulationMesh<IVertex2D> mesh)
        {
            IVertex2D[] vert_clones = mesh.Verticies.Select(v => v.ShallowCopy() as IVertex2D).ToArray();
            TriangulationMesh<IVertex2D> newMesh = new TriangulationMesh<IVertex2D>();
            newMesh.AddVerticies(vert_clones);
            foreach (IEdge key in mesh.Edges.Values)
            {
                newMesh.AddEdge(key.Clone());
            }

            foreach (IFace f in mesh.Faces)
            {
                newMesh.AddFace(f.Clone());
            }

            return newMesh;
        }

        /// <summary>
        /// Create a mesh from a set of triangles
        /// </summary>
        /// <param name="triangles"></param>
        /// <returns></returns>
        public static Mesh3D ToDynamicRenderMesh(this ICollection<GridTriangle> triangles)
        {
            Mesh3D mesh = new Meshing.Mesh3D();
            Dictionary<GridVector2, int> PointToVertexIndex = new Dictionary<GridVector2, int>();

            foreach (GridVector2 v in triangles.SelectMany(tri => tri.Points).Distinct())
            {
                int index = mesh.AddVertex(new Vertex3D(v.ToGridVector3(0)));
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
