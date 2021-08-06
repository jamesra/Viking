//#define TRACEMESH

using System.Collections.Generic;
using System.Linq;

namespace Geometry.Meshing
{
    public static class MeshExtensions
    {
        /// <summary>
        /// Creates a copy of the input that ensures the first and last index value are identical
        /// </summary>
        public static IReadOnlyList<int> EnsureClosedRing(this IEnumerable<int> iVerts)
        {
            return iVerts.ToList().EnsureClosedRing();
        }

        /// <summary>
        /// Creates a copy of the input that ensures the first and last index value are identical
        /// </summary>
        /// <param name="iVerts"></param>
        /// <returns></returns>
        public static IReadOnlyList<int> EnsureClosedRing(this List<int> iVerts)
        {
            List<int> iClosedRing = iVerts.ToList();

            if (iClosedRing[0] == iClosedRing.Last())
                return iClosedRing;

            iClosedRing.Add(iClosedRing[0]);
            return iClosedRing;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="iVerts"></param>
        /// <returns>True if the first and last index are identical</returns>
        public static bool IsClosedRing(this IEnumerable<int> iVerts)
        {
            return iVerts.First() == iVerts.Last();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="iVerts"></param>
        /// <returns>True if the first and last index are identical</returns>
        public static bool IsClosedRing(this IReadOnlyList<int> iVerts)
        {
            return iVerts[0] == iVerts[iVerts.Count - 1];
        }

        public static bool IsValidClosedRing(this IEnumerable<int> iVerts)
        {
            return iVerts.ToArray().IsValidClosedRing(out string Reason);
        }

        public static bool IsValidClosedRing(this IEnumerable<int> iVerts, out string Reason)
        {
            return iVerts.ToArray().IsValidClosedRing(out Reason);
        }

        public static bool IsValidClosedRing(this IReadOnlyList<int> iVerts)
        {
            return iVerts.IsValidClosedRing(out string Reason);
        }

        public static bool IsValidClosedRing(this IReadOnlyList<int> iVerts, out string Reason)
        {
            if (iVerts.IsClosedRing() == false)
            {
                Reason = "Input is not a closed ring";
                return false;
            }

            if (iVerts.Distinct().Count() == iVerts.Count - 1)
            {
                Reason = null;
                return true;
            }
            else
            {
                Reason = "Input contains duplicate indicies that are not the head and tail of the ring";
                return false;
            }
        }

        public static EdgeKey[] ToEdgeKeys(this IEnumerable<int> iVerts)
        {
            IReadOnlyList<int> ring = iVerts.EnsureClosedRing();
            EdgeKey[] keys = new EdgeKey[ring.Count - 1];
            for (int i = 0; i < ring.Count - 1; i++)
            {
                keys[i] = new EdgeKey(ring[i], ring[i + 1]);
            }

            return keys;
        }

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
        /// A function provided to help debug.  Returns true if any edges intersect, other than at endpoints of course
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static bool AnyMeshEdgesIntersect(this IReadOnlyMesh2D<IVertex2D> mesh)
        {
            RTree.RTree<IEdge> rTree = mesh.GenerateEdgeRTree();

            foreach (var e in mesh.Edges.Keys)
            {
                GridLineSegment seg = mesh.ToGridLineSegment(e);
                foreach (var intersection in rTree.IntersectionGenerator(seg.BoundingBox))
                {
                    if (intersection.Equals(e)) //Don't test for intersecting with ourselves
                        continue;

                    GridLineSegment testLine = mesh.ToGridLineSegment(intersection);
                    if (seg.Intersects(in testLine, intersection.A == e.A || intersection.B == e.A || intersection.A == e.B || intersection.B == e.B))
                    {
                        System.Diagnostics.Trace.WriteLine(string.Format("{0} intersects {1}", e, intersection));
                        return true;
                    }
                }
            }

            return false;
        }


        public static RTree.RTree<IEdge> GenerateEdgeRTree(this IReadOnlyMesh2D<IVertex2D> mesh)
        {
            RTree.RTree<IEdge> rTree = new RTree.RTree<IEdge>();
            foreach (var e in mesh.Edges.Values)
            {
                GridLineSegment seg = mesh.ToGridLineSegment(e);
                rTree.Add(seg.BoundingBox, e);
            }

            return rTree;
        }

        /// <summary>
        /// Create a mesh from a set of triangles
        /// </summary>
        /// <param name="triangles"></param>
        /// <returns></returns>
        public static Mesh2D ToDynamicRenderMesh(this ICollection<GridTriangle> triangles)
        {
            Mesh2D mesh = new Meshing.Mesh2D();
            Dictionary<GridVector2, int> PointToVertexIndex = new Dictionary<GridVector2, int>();

            foreach (GridVector2 v in triangles.SelectMany(tri => tri.Points).Distinct())
            {
                int index = mesh.AddVertex(new Vertex2D(v));
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

        public static TriangulationMesh<IVertex2D<PolygonIndex>> Triangulate(this GridPolygon poly, int iPoly = 0, TriangulationMesh<IVertex2D<PolygonIndex>>.ProgressUpdate OnProgress = null)
        {
            //var polyCopy = (GridPolygon)poly.Clone();

            //Center the polygon on 0,0 to reduce floating point error
            var centeredPoly = poly.Translate(-poly.Centroid);

            PolygonVertexEnum vertEnumerator = new PolygonVertexEnum(centeredPoly, iPoly);

            var meshVerts = vertEnumerator.Select(v => new Vertex2D<PolygonIndex>(v.Point(centeredPoly), v)).ToArray();

            Dictionary<PolygonIndex, Vertex2D<PolygonIndex>> IndexToVert = meshVerts.ToDictionary(v => v.Data);

            TriangulationMesh<IVertex2D<PolygonIndex>> mesh = GenericDelaunayMeshGenerator2D<IVertex2D<PolygonIndex>>.TriangulateToMesh(meshVerts, OnProgress);

            SortedSet<IEdgeKey> constrainedEdges = new SortedSet<IEdgeKey>();

            //Add constrained edges to the mesh
            PolygonIndex[] pIndicies = vertEnumerator.ToArray();

            Dictionary<PolygonIndex, Edge> edgeFacesToCheck = new Dictionary<PolygonIndex, Edge>();

            //while (vertEnumerator.MoveNext() == true)
            foreach (PolygonIndex currentVert in pIndicies)
            {
                //PointIndex currentVert = vertEnumerator.Current;
                int A = IndexToVert[currentVert].Index;
                int B = IndexToVert[currentVert.Next].Index;

                Edge e = new ConstrainedEdge(A, B);
                mesh.AddConstrainedEdge(e, OnProgress);
                constrainedEdges.Add(e.Key);

                //If there are three constrained edges that form an interior polygon that is a triangle the face wont be removed.  This results
                //in a constrained edge with two faces.  For this case remove the interior face after all constrained edges are added
                if (currentVert.IsInner && currentVert.NumUniqueInRing == 3)
                {
                    edgeFacesToCheck.Add(currentVert, e);
                }
            }

            //Remove edges that are not contained in the polygon, that means any edges that connect points on the same ring which are not constrained edges
            var EdgesToCheck = mesh.Edges.Keys.Where(k => mesh[k.A].Data.AreOnSameRing(mesh[k.B].Data) && constrainedEdges.Contains(k) == false).ToArray();
            foreach (IEdgeKey key in EdgesToCheck)
            {
                GridLineSegment line = mesh.ToGridLineSegment(key);

                if (OverlapType.NONE == centeredPoly.ContainsExt(line.Bisect()))
                {
                    mesh.RemoveEdge(key);

                    OnProgress?.Invoke(mesh);
                }
            }

            //If there are three constrained edges that form an interior polygon that is a triangle the face wont be removed.  This results
            //in a constrained edge with two faces.  For this case remove the interior face
            foreach (var innerPolyGroup in edgeFacesToCheck.GroupBy(i => i.Key.iInnerPoly))
            {
                GridPolygon innerPolygon = poly.InteriorPolygons[innerPolyGroup.Key.Value];
                GridVector2 Centroid = innerPolygon.Centroid;

                //Figure out the inner polygon vertex numbers in the mesh
                SortedSet<int> innerPolyVerts = new SortedSet<int>(innerPolyGroup.SelectMany(g => new int[] { g.Value.A, g.Value.B }));
                IFace[] allFaces = innerPolyGroup.SelectMany(g => g.Value.Faces).Distinct().ToArray();

                IFace[] InteriorFaces = allFaces.Where(f => f.iVerts.All(iVert => innerPolyVerts.Contains(iVert))).ToArray();

                //Should only ever be one interior face for a 3 vert interior polygon, unless someone adds interior polygons to interior polygons later <shudder/>
                foreach (IFace f in InteriorFaces)
                {
                    mesh.RemoveFace(f);

                    OnProgress?.Invoke(mesh);
                }
            }


            //System.Diagnostics.Debug.Assert(mesh.Faces.Count > 0, "Triangulation of polygon should create at least one face");
            //System.Diagnostics.Debug.Assert(constrainedEdges.All(e => mesh[e].Faces.Count == 1), "All constrained edges should have one face");
            return mesh;
        }

        /// <summary>
        /// Triangulate a set of points on a face, that include a set of points inside the faces.
        /// </summary>
        /// <param name="verts">Exterior ring of a polygon</param>
        /// <param name="InteriorPoints">These points must be contained by the polygon defined by face</param>
        /// <param name="OnProgress"></param>
        /// <returns></returns>
        public static TriangulationMesh<IVertex2D<int>> Triangulate(IVertex2D[] verts, IVertex2D[] InteriorPoints = null, TriangulationMesh<IVertex2D<int>>.ProgressUpdate OnProgress = null)
        {
            if (verts.Last() == verts.First())
            {
                var faceList = verts.ToList();
                faceList.RemoveAt(faceList.Count - 1);
                verts = faceList.ToArray();
            }

            GridVector2 faceCenter = verts.Select(v => v.Position).ToArray().Average();

            if (GridVector2.Magnitude(faceCenter) < 100)
            {
                faceCenter = GridVector2.Zero; //Don't nudge if we are close to origin, prevents errors in our tests.
            }

            //Center the verts on 0,0 to reduce floating point error
            var faceVerts = verts.Select(v => new Vertex2D<int>(v.Position - faceCenter, v.Index)).ToArray();
            var interiorVerts = InteriorPoints == null ? System.Array.Empty<Vertex2D<int>>() : InteriorPoints.Select(v => new Vertex2D<int>(v.Position - faceCenter, v.Index)).ToArray();

            GridPolygon centeredPoly = new GridPolygon(faceVerts.Select(v => v.Position).ToArray().EnsureClosedRing());
            System.Diagnostics.Debug.Assert(interiorVerts.All(v => centeredPoly.Contains(v.Position)), "Interior points must be inside Face");

            var tri_mesh_verts = faceVerts.Union(interiorVerts).ToArray();

            TriangulationMesh<IVertex2D<int>> tri_mesh = GenericDelaunayMeshGenerator2D<IVertex2D<int>>.TriangulateToMesh(tri_mesh_verts, OnProgress);

            OnProgress?.Invoke(tri_mesh);

            SortedSet<IEdgeKey> expectedConstrainedEdges = new SortedSet<IEdgeKey>();

            //Add constrained edges to the mesh
            SortedSet<int> FaceIndicies = new SortedSet<int>(faceVerts.Select(f => f.Index));

            InfiniteSequentialIndexSet FaceIndexer = new InfiniteSequentialIndexSet(0, faceVerts.Length, 0);
            for (int i = 0; i < faceVerts.Length; i++)
            {
                int A = faceVerts[FaceIndexer[i]].Index;
                int B = faceVerts[FaceIndexer[i + 1]].Index;

                Edge e = new ConstrainedEdge(A, B);
                if (tri_mesh.Contains(e))
                {
                    if (tri_mesh[e] as ConstrainedEdge == null)
                    {
                        var existing_faces = tri_mesh[e].Faces;
                        tri_mesh.RemoveEdge(e.Key);
                        tri_mesh.AddEdge(e);
                        tri_mesh.AddFaces(existing_faces);

                        OnProgress?.Invoke(tri_mesh);
                    }
                }

                var added_constrained_edges = tri_mesh.AddConstrainedEdge(e, OnProgress);
                expectedConstrainedEdges.UnionWith(added_constrained_edges.Select(ce => ce.Key));
            }

            //Remove edges that are not contained in the polygon, that means any edges that connect points on the same ring which are not constrained edges
            var EdgesToCheck = tri_mesh.Edges.Keys.Where(k => FaceIndicies.Contains(k.A) && FaceIndicies.Contains(k.B) && expectedConstrainedEdges.Contains(k) == false).ToArray();
            foreach (IEdgeKey key in EdgesToCheck)
            {
                GridLineSegment line = new GridLineSegment(tri_mesh_verts[key.A].Position, tri_mesh_verts[key.B].Position);// tri_mesh.ToGridLineSegment(key);

                if (false == centeredPoly.Contains(line.Bisect()))
                {
#if TRACEMESH
                    Trace.WriteLine(string.Format("{0} exterior to poly", key));
#endif 
                    tri_mesh.RemoveEdge(key);

                    OnProgress?.Invoke(tri_mesh);
                }
            }

#if DEBUG
            bool[] constrainedEdgeInMesh = expectedConstrainedEdges.Select(e => tri_mesh.Contains(e)).ToArray();
            int[] constrainedEdgeFaces = expectedConstrainedEdges.Where(e => tri_mesh.Contains(e)).Select(e => tri_mesh[e].Faces.Count).ToArray();

            System.Diagnostics.Debug.Assert(constrainedEdgeInMesh.All(hasEdge => hasEdge), "Triangulation of polygon should create at least one face");
            System.Diagnostics.Debug.Assert(tri_mesh.Faces.Count > 0, "Triangulation of polygon should create at least one face");
            System.Diagnostics.Debug.Assert(constrainedEdgeFaces.All(facecount => facecount == 1), "All constrained edges should have one face");
#endif
            return tri_mesh;
        }
    }
}
