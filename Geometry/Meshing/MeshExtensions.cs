//#define TRACEMESH

using System;
using System.Collections.Generic;
using System.Linq;

namespace Geometry.Meshing
{
    public static class MeshExtensions
    {
        /// <summary>
        /// Creates a copy of the input that ensures the first and last index value are identical
        /// </summary>
        public static IReadOnlyList<int> EnsureClosedRing(this IEnumerable<int> iVerts) => iVerts.ToList().EnsureClosedRing();

        /// <summary>
        /// Creates a copy of the input that ensures the first and last index value are identical
        /// </summary>
        /// <param name="iVerts"></param>
        /// <returns></returns>
        public static IReadOnlyList<int> EnsureClosedRing(this List<int> iVerts)
        {
            List<int> iClosedRing = [.. iVerts];

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
        public static bool IsClosedRing(this IEnumerable<int> iVerts) => iVerts.First() == iVerts.Last();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="iVerts"></param>
        /// <returns>True if the first and last index are identical</returns>
        public static bool IsClosedRing(this IReadOnlyList<int> iVerts) => iVerts[0] == iVerts[iVerts.Count - 1];

        public static bool IsValidClosedRing(this IEnumerable<int> iVerts) => iVerts.ToArray().IsValidClosedRing(out string Reason);

        public static bool IsValidClosedRing(this IEnumerable<int> iVerts, out string Reason) => iVerts.ToArray().IsValidClosedRing(out Reason);

        public static bool IsValidClosedRing(this IReadOnlyList<int> iVerts) => iVerts.IsValidClosedRing(out string Reason);

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
            IVertex2D[] vert_clones = [.. mesh.Verticies.Select(v => v.ShallowCopy() as IVertex2D)];
            TriangulationMesh<IVertex2D> newMesh = new();
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
            RTree.RTree<IEdge> rTree = new();
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
            Mesh2D mesh = new();
            QuadTreeWithUniqueValues<int> PointToVertexIndex = new();

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

        public static bool IsTriangle(this IFace face) => face.iVerts.Length == 3;

        public static bool IsQuad(this IFace face) => face.iVerts.Length == 4;

        public static TriangulationMesh<IVertex2D<PolygonIndex>> Triangulate(this IReadOnlyList<GridPolygon> polys, TriangulationMesh<IVertex2D<PolygonIndex>>.ProgressUpdate OnProgress = null) => throw new NotImplementedException();

        public static TriangulationMesh<IVertex2D<PolygonIndex>> Triangulate(this GridPolygon poly, int iPoly = 0, TriangulationMesh<IVertex2D<PolygonIndex>>.ProgressUpdate OnProgress = null)
        {
            //var polyCopy = (GridPolygon)poly.Clone();

            //Center the polygon on 0,0 to reduce floating point error
            var centeredPoly = poly.Translate(-poly.Centroid);

            PolygonVertexEnum vertEnumerator = new(centeredPoly, iPoly);

            var meshVerts = vertEnumerator.Select(v => new Vertex2D<PolygonIndex>(v.Point(centeredPoly), v)).ToArray();

            Dictionary<PolygonIndex, Vertex2D<PolygonIndex>> IndexToVert = meshVerts.ToDictionary(v => v.Data);

            TriangulationMesh<IVertex2D<PolygonIndex>> mesh = GenericDelaunayMeshGenerator2D<IVertex2D<PolygonIndex>>.TriangulateToMesh(meshVerts, OnProgress);

            SortedSet<IEdgeKey> constrainedEdges = [];

            //Add constrained edges to the mesh
            PolygonIndex[] pIndicies = vertEnumerator.ToArray();

            Dictionary<PolygonIndex, Edge> edgeFacesToCheck = [];

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

                if (ShapeRelation.NONE == centeredPoly.GetRelation(line.Bisect()))
                {
                    mesh.RemoveEdge(key);

                    OnProgress?.Invoke(mesh);
                }
            }

            //If there are three constrained edges that form an interior polygon that is a triangle the face won't be removed.  This results
            //in a constrained edge with two faces.  For this case remove the interior face
            foreach (var innerPolyGroup in edgeFacesToCheck.GroupBy(i => i.Key.iInnerPoly))
            {
                GridPolygon innerPolygon = poly.InteriorPolygons[innerPolyGroup.Key.Value];
                GridVector2 Centroid = innerPolygon.Centroid;

                //Figure out the inner polygon vertex numbers in the mesh
                SortedSet<int> innerPolyVerts = [.. innerPolyGroup.SelectMany(g => new int[] { g.Value.A, g.Value.B })];
                IFace[] allFaces = [.. innerPolyGroup.SelectMany(g => g.Value.Faces).Distinct()];

                IFace[] InteriorFaces = [.. allFaces.Where(f => f.iVerts.All(iVert => innerPolyVerts.Contains(iVert)))];

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
        /// Removes degenerate input before triangulating a region: perimeter vertices that sit within
        /// <see cref="Global.Epsilon"/> of the previously kept perimeter vertex (or the ring start), perimeter
        /// vertices that are colinear with their neighbors (redundant midpoints), and interior points that
        /// coincide with a kept perimeter vertex or a previously kept interior point.
        /// The original vertex <see cref="IVertex.Index"/> values are preserved so callers can map the
        /// triangulation result back to their source mesh.  Without this the divide-and-conquer Delaunay
        /// generator throws on coincident or near-colinear points (e.g. "Can't create line with two identical points").
        /// </summary>
        /// <param name="perimeter">Ordered perimeter ring of the region.</param>
        /// <param name="interior">Interior (e.g. medial-axis) points that must lie inside the region.</param>
        public static (IVertex2D[] Perimeter, IVertex2D[] Interior) CleanRegionTriangulationInput(IReadOnlyList<IVertex2D> perimeter, IReadOnlyList<IVertex2D> interior)
        {
            List<IVertex2D> cleanedPerimeter = new(perimeter.Count);
            foreach (IVertex2D v in perimeter)
            {
                if (cleanedPerimeter.Count > 0 && GridVector2.Equals(cleanedPerimeter[cleanedPerimeter.Count - 1].Position, v.Position))
                    continue; //Skip a point that duplicates the previous perimeter point

                cleanedPerimeter.Add(v);
            }

            //Drop a trailing point that closes the ring back onto the first point
            while (cleanedPerimeter.Count > 1 && GridVector2.Equals(cleanedPerimeter[0].Position, cleanedPerimeter[cleanedPerimeter.Count - 1].Position))
                cleanedPerimeter.RemoveAt(cleanedPerimeter.Count - 1);

            //Remove redundant colinear midpoints.  Keep removing while the middle of a triplet lies on the line
            //connecting its neighbors, but never reduce the ring below a triangle.
            bool removed = true;
            while (removed && cleanedPerimeter.Count > 3)
            {
                removed = false;
                for (int i = 0; i < cleanedPerimeter.Count; i++)
                {
                    GridVector2 prev = cleanedPerimeter[(i - 1 + cleanedPerimeter.Count) % cleanedPerimeter.Count].Position;
                    GridVector2 curr = cleanedPerimeter[i].Position;
                    GridVector2 next = cleanedPerimeter[(i + 1) % cleanedPerimeter.Count].Position;

                    if (prev.Winding(curr, next) == RotationDirection.COLINEAR)
                    {
                        cleanedPerimeter.RemoveAt(i);
                        removed = true;
                        break;
                    }
                }
            }

            List<IVertex2D> cleanedInterior = new(interior?.Count ?? 0);
            if (interior != null)
            {
                foreach (IVertex2D v in interior)
                {
                    bool duplicate = cleanedPerimeter.Any(p => GridVector2.Equals(p.Position, v.Position))
                                  || cleanedInterior.Any(p => GridVector2.Equals(p.Position, v.Position));
                    if (duplicate)
                        continue;

                    cleanedInterior.Add(v);
                }
            }

            return ([.. cleanedPerimeter], [.. cleanedInterior]);
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
                verts = [.. faceList];
            }

            GridVector2 shapeCenter = verts.Select(v => v.Position).ToArray().Average();

            if (shapeCenter.Magnitude < 100)
            {
                shapeCenter = GridVector2.Zero; //Don't nudge if we are close to origin, prevents errors in our tests.
            }

            //Center the verts on 0,0 to reduce floating point error
            //Assign the index to the new vertex to match the index into the faceVerts and interiorVerts arrays
            var faceVerts = verts.Select((v, i) => new Vertex2D<int>(i, v.Position - shapeCenter, v.Index)).ToArray();
            var interiorVerts = InteriorPoints is null ? System.Array.Empty<Vertex2D<int>>() : [.. InteriorPoints.Select((v, i) => new Vertex2D<int>(i + faceVerts.Length, v.Position - shapeCenter, v.Index))];

            GridPolygon centeredPoly = new(faceVerts.Select(v => v.Position).ToArray().EnsureClosedRing());
            System.Diagnostics.Debug.Assert(interiorVerts.All(v => centeredPoly.Contains(v.Position)), "Interior points must be inside Face");

            var tri_mesh_verts = faceVerts.Union(interiorVerts).ToArray();

            TriangulationMesh<IVertex2D<int>> tri_mesh = GenericDelaunayMeshGenerator2D<IVertex2D<int>>.TriangulateToMesh(tri_mesh_verts, OnProgress);

            OnProgress?.Invoke(tri_mesh);

            SortedSet<IEdgeKey> expectedConstrainedEdges = [];

            //Add constrained edges to the mesh
            SortedSet<int> faceIndicies = [.. faceVerts.Select(f => f.Index)];

            InfiniteSequentialIndexSet FaceIndexer = new(0, faceVerts.Length, 0);
            for (int i = 0; i < faceVerts.Length; i++)
            {
                int A = faceVerts[FaceIndexer[i]].Index;
                int B = faceVerts[FaceIndexer[i + 1]].Index;

                Edge e = new ConstrainedEdge(A, B);
                if (tri_mesh.Contains(e))
                {
                    //Replace the standard edge with a constrained edge
                    if (tri_mesh[e] as ConstrainedEdge is null)
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

            //Remove edges that are not contained inside the polygon, that means any edges that connect points on the same ring which are not constrained edges. 
            //This removes edges from concave regions and interior holes
            var EdgesToCheck = tri_mesh.Edges.Keys.Where(k => faceIndicies.Contains(k.A) && faceIndicies.Contains(k.B) && expectedConstrainedEdges.Contains(k) == false).ToArray();
            foreach (IEdgeKey key in EdgesToCheck)
            {
                GridLineSegment line = new(tri_mesh_verts[key.A].Position, tri_mesh_verts[key.B].Position);// tri_mesh.ToGridLineSegment(key);

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
            bool[] constrainedEdgeInMesh = [.. expectedConstrainedEdges.Select(e => tri_mesh.Contains(e))];
            int[] constrainedEdgeFaces = [.. expectedConstrainedEdges.Where(e => tri_mesh.Contains(e)).Select(e => tri_mesh[e].Faces.Count)];

            System.Diagnostics.Debug.Assert(constrainedEdgeInMesh.All(hasEdge => hasEdge), "Triangulation of polygon should create at least one face");
            System.Diagnostics.Debug.Assert(tri_mesh.Faces.Count > 0, "Triangulation of polygon should create at least one face");
            System.Diagnostics.Debug.Assert(constrainedEdgeFaces.All(facecount => facecount == 1), "All constrained edges should have one face");
#endif
            return tri_mesh;
        }
    }
}
