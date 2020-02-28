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

        public static TriangulationMesh<Vertex2D<PointIndex>> Triangulate(this GridPolygon poly, int iPoly = 0, TriangulationMesh<Vertex2D<PointIndex>>.ProgressUpdate OnProgress=null)
        {
            var polyCopy = (GridPolygon)poly.Clone();

            //Center the polygon on 0,0 to reduce floating point error
            var centeredPoly = polyCopy.Translate(-polyCopy.Centroid);

            PolygonVertexEnum vertEnumerator = new PolygonVertexEnum(centeredPoly, iPoly);

            var meshVerts = vertEnumerator.Select(v => new Vertex2D<PointIndex>(v.Point(centeredPoly), v)).ToArray();

            Dictionary<PointIndex, Vertex2D<PointIndex>> IndexToVert = meshVerts.ToDictionary(v => v.Data);

            TriangulationMesh<Vertex2D<PointIndex>> mesh = GenericDelaunayMeshGenerator2D<Vertex2D<PointIndex>>.TriangulateToMesh(meshVerts, OnProgress);

            PointIndex? lastVert = null;

            SortedSet<IEdgeKey> constrainedEdges = new SortedSet<IEdgeKey>();

            //Add constrained edges to the mesh
            while (vertEnumerator.MoveNext() == true)
            {
                PointIndex currentVert = vertEnumerator.Current;
                int A = IndexToVert[currentVert].Index;
                int B = IndexToVert[currentVert.Next].Index;

                Edge e = new Edge(A, B);
                mesh.AddConstrainedEdge(e);
                constrainedEdges.Add(e.Key);
            }

            //Remove edges that are not contained in the polygon, that means any edges that connect points on the same ring which are not constrained edges
            var EdgesToCheck = mesh.Edges.Keys.Where(k => mesh[k.A].Data.AreOnSameRing(mesh[k.B].Data) && constrainedEdges.Contains(k) == false).ToArray();
            foreach(EdgeKey key in EdgesToCheck)
            {
                GridLineSegment line = mesh.ToGridLineSegment(key);

                if(false == centeredPoly.Contains(line.Bisect()))
                {
                    mesh.RemoveEdge(key);
                }
            }

            System.Diagnostics.Debug.Assert(mesh.Faces.Count > 0, "Triangulation of polygon should create at least one face");
            System.Diagnostics.Debug.Assert(constrainedEdges.All(e => mesh[e].Faces.Count == 1), "All constrained edges should have one face");
            return mesh;
        }
    }
}
