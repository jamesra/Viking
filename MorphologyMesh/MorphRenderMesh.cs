using Geometry;
using Geometry.Meshing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MorphologyMesh
{
    /// <summary>
    /// Return true if a flood fill function can move from the origin face to the candidate face.
    /// </summary>
    /// <param name="mesh">Mesh containing the faces and edges</param>
    /// <param name="face">Face that we are testing to see if it meets criteria</param>
    /// <returns></returns>
    public delegate bool FaceMeetsCriteriaFunction(MorphRenderMesh mesh, MorphMeshFace face);

    /// <summary>
    /// Return true if a flood fill function can move from the origin face to the candidate face.
    /// </summary>
    /// <param name="mesh">Mesh containing the faces and edges</param>
    /// <param name="origin">Face that originated the test</param>
    /// <param name="candidate">Face that we are testing to see if it meets criteria</param>
    /// <param name="edge">The edge connecting the faces that must also meet criteria</param>
    /// <returns></returns>
    public delegate bool EdgeMeetsCriteriaFunc(MorphRenderMesh mesh, MorphMeshFace origin, MorphMeshFace candidate, MorphMeshEdge edge);

    public enum VertexOrigin
    {
        CONTOUR, //The vertex is on the exterior or Interior contour of a polygon
        MEDIALAXIS //The vertex is on the medial axis of the polygon
    }


    /// <summary>
    /// Represents where in an medial axis graph the vertex originated
    /// </summary>
    public readonly struct MedialAxisIndex(MedialAxisGraph graph, MedialAxisVertex v)
    {
        public readonly MedialAxisGraph MedialAxisGraph = graph;
        public readonly MedialAxisVertex Vertex = v;
    }



    /// <summary>
    /// A 3D mesh that records the polygons used to construct the mesh.  Tracks the original polygonal index
    /// of every vertex and the type of edge connecting verticies.
    ///    
    /// The MorphRenderMesh class was originally written to handle polygons at arbitrary Z levels.  However, when generating a full mesh it
    /// is possible, if annotators are trying to be difficult, to have annotations with layouts in Z like this.  That have to be grouped 
    /// into a single mesh in order to branch the mesh correctly.
    /// 
    ///  Z = 1:          A
    ///                 / \ 
    ///  Z = 2:        B   \   D
    ///                     \ /
    ///  Z = 3:              C
    ///
    /// In this case the "upper" polygons are A,D and the "lower" polygons are B,C.  Even though B & D are on the same Z level.
    /// 
    /// I originally only had the Upper and Lower Polygon concept in BajajMesh, but the system should be ported to MorphRenderMesh
    /// </summary>
    /// 
    /// </summary>
    public class MorphRenderMesh : Mesh3D<MorphMeshVertex>
    {
        public virtual IShape2D[] Shapes { get; }

        public virtual double[] ShapeZ { get; }

        public virtual bool[] IsUpperShape { get; }

        /// <summary>
        /// True when any topology shape is a polygon. Polyline-only slices skip region/medial-axis closing.
        /// </summary>
        public bool HasPolygonShapes => Shapes is not null && Shapes.Any(s => s is Polygon);

        /// <summary>
        /// True when two shapes may be joined by a CORRESPONDING edge where their verticies share an XY position.
        ///
        /// Two annotations can coincide in XY without the annotator ever linking them, which happens whenever a
        /// process doubles back through the same column.  Stitching those together produces a surface that jumps
        /// between unrelated parts of the structure, so <see cref="BajajGeneratorMesh"/> overrides this to consult
        /// the slice's LocationLinks.  The base implementation allows everything, preserving the behavior of meshes
        /// built directly from shape arrays.
        /// </summary>
        protected virtual bool CorrespondenceAllowed(int iShapeA, int iShapeB) => true;

        private readonly Dictionary<IShapeIndex, long> ShapeIndexToVertex = [];

        [NonSerialized]
        private double? _avgZ = null; //Cached average Z level of polygons use only for sorting purposes
        public double AverageZ
        {
            get
            {
                if (_avgZ.HasValue == false)
                    _avgZ = ShapeZ.Average();

                return _avgZ.Value;
            }
        }

        /// <summary>
        /// Generates a MorphRenderMesh for a set of polygons and ZLevels.
        /// </summary>
        /// <param name="polygons"></param>
        /// <param name="ZLevels"></param>
        /// <param name="IsUpperPolygon">True indicates the polygon</param>
        public MorphRenderMesh(IReadOnlyList<IShape2D> polygons, IReadOnlyList<double> ZLevels, IReadOnlyList<bool> isUpperPolygon)
        {
            //TODO: I don't add corresponding verticies at overlap points due to how the original MonogameTestbed was written, but I probably should. 
            Debug.Assert(polygons.Count == ZLevels.Count);
            Shapes = [.. polygons];
            ShapeZ = [.. ZLevels];
            IsUpperShape = IsUpperShape;
            this.CreateOffsetEdge = MorphMeshEdge.Duplicate;
            this.CreateOffsetFace = MorphMeshFace.CreateOffsetCopy;

            this.CreateFace = MorphMeshFace.Create;
            this.CreateEdge = MorphMeshEdge.Create;

            //Now that we have polygons organized by Z-level, add any corresponding verticies for polygons on adjacent Z levels.
            //AddCorrespondingVertices(PolygonsByZ);

            PopulateMesh(this);
        }

        /// <summary>
        /// Creates a mesh without faces.  The mesh contains a vertex for every polygon vertex.  It also contains contour edges and corresponding edges for polygon intersection points
        /// </summary>
        /// <param name="mesh"></param>
        private static void PopulateMesh(MorphRenderMesh mesh)
        {
            //Add verticies
            List<IShapeIndex> shapeVerts = new(new ShapeSetVertexEnum(mesh.Shapes));

            //This is used to identify corresponding edges
            //TODO: PositionToIndex does not handle multiple Z Level meshes correctly when generating corresponding edges
            Dictionary<Vector2, int> PositionToIndex = new();

            foreach (var i1 in shapeVerts)
            {
                MorphMeshVertex v = new(i1, i1.Point(mesh.Shapes).ToVector3(mesh.ShapeZ[i1.ShapeIndex]));
                int iV;

                if (PositionToIndex.TryGetValue(v.Position.XY(), out var corresponding_vertex))
                {
                    //This vertex corresponds to where the polygon overlaps another polygon on another level.
                    //Populate the correspoinding field, and ensure the positions are 100% identical 
                    var corresponding = mesh[corresponding_vertex];

                    //Positions are snapped either way so the triangulator sees one site instead of a degenerate
                    //micro-edge, but an unlinked pair gets no CORRESPONDING edge and no Corresponding index, which
                    //keeps CompleteCorrespondingVertexFaces from building a surface across the two.
                    bool allowed = mesh.CorrespondenceAllowed(corresponding.ShapeIndex.ShapeIndex, i1.ShapeIndex);

                    v = new MorphMeshVertex(i1, corresponding.Position.XY().ToVector3(v.Position.Z))
                    {
                        Corresponding = allowed ? corresponding_vertex : null
                    }; //Ensure the position is identical

                    //Add new vert to mesh with matching position and create corresponding edge
                    iV = mesh.AddVertex(v);

                    if (allowed)
                    {
                        corresponding.Corresponding = iV;

                        MorphMeshEdge corresponding_edge = new(EdgeType.CORRESPONDING, iV, corresponding_vertex);
                        mesh.AddEdge(corresponding_edge);
                    }
                }
                else
                {
                    //A new vert, add to mesh
                    corresponding_vertex = mesh.AddVertex(v);
                    PositionToIndex.Add(v.Position.XY(), corresponding_vertex);
                }
            }

            //Add contours. Polygon rings wrap via Next; polyline endpoints return null and stay open.
            foreach (var i1 in shapeVerts)
            {
                IShapeIndex next = i1.Next;
                if (next is null)
                    continue;

                MorphMeshEdge edge = new(EdgeType.CONTOUR, mesh[i1].Index, mesh[next].Index);
                mesh.AddEdge(edge);
            }

            //We need to handle the case where a single vertex is on the other side of the contour boundary.  This
            //creates two corresponding vertices
            //      D
            //     / \
            // A--1-3-2--B
            //   /     \
            //  C       E

            //This creates two corresponding verticies 1 & 2.Then the code to prevent adjacent corresponding verticies adds a third point, 3.
            //todo, fix the case above

        }

        /*
        /// <summary>
        /// Given a vertex, predict where the vertex would be using the two points before and after and a catmullrom fit
        /// </summary>
        /// <param name="corresponding"></param>
        private static Vector2 FitCurveMidpoint(MorphRenderMesh mesh, int index)
        {
            return FitCurveMidpoint(mesh, mesh[index]);
        }

        /// <summary>
        /// Given a vertex, predict where the vertex would be using the two points before and after and a catmullrom fit
        /// </summary>
        /// <param name="corresponding"></param>
        private static Vector2 FitCurveMidpoint(MorphRenderMesh mesh, MorphMeshVertex v)
        {
            PolygonIndex cIndex = v.ShapeIndex;
            return cIndex.PredictPoint(mesh.Shapes);
        }
        */


        /// <summary>
        /// Returns a dictionary mapping points on two Z levels to polygon indicies in the mesh.
        /// PointIndex values will index into the Mesh's full array of polygons, not a subset.
        /// </summary>
        /// <param name="ZLevelA"></param>
        /// <param name="ZLevelB"></param>
        /// <returns></returns>
        public Dictionary<Vector2, List<PolygonIndex>> CreatePointToPolyMap(double ZLevelA, double ZLevelB) => throw new NotImplementedException();

        public new MorphMeshEdge this[IEdgeKey key] => (MorphMeshEdge)this.Edges[key];

        /// <summary>
        /// Returns all of the verticies that match the indicies
        /// </summary>
        /// <param name="vertIndicies"></param>
        /// <returns></returns>
        public new IEnumerable<MorphMeshEdge> this[IEnumerable<IEdgeKey> keys] => keys.Select(e => (MorphMeshEdge)this.Edges[e]);

        public virtual MorphMeshVertex this[IShapeIndex key]
        {
            get => _Verticies[(int)ShapeIndexToVertex[key]];
            set => _Verticies[(int)ShapeIndexToVertex[key]] = value;
        }

        public virtual bool Contains(IShapeIndex key) => ShapeIndexToVertex.ContainsKey(key);


        /// <summary>
        /// Returns true if an edge exists between the two points
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public virtual bool Contains(IShapeIndex A, IShapeIndex B)
        {
            if (!this.Contains(A) || !this.Contains(B))
                return false;

            EdgeKey key = new(this[A].Index, this[B].Index);
            return this.Contains(key);
        }

        public MorphMeshVertex GetVertex(int key) => (MorphMeshVertex)Vertices[key];

        public override int AddVertex(MorphMeshVertex v)
        {
            var iVert = base.AddVertex(v);
            if (v.ShapeIndex is not null)
                ShapeIndexToVertex.Add(v.ShapeIndex, iVert);
            return iVert;
        }

        /// <summary>
        /// Faces refused because one of their edges already carried two faces.  Non-zero after generation means a
        /// pass tried to build a third wall on a chord; the mesh is left with a hole there instead of a fin.
        /// </summary>
        public int FacesRefusedAtEdgeCapacity { get; private set; }

        /// <summary>
        /// A slice surface is a 2-manifold with boundary: no edge may carry more than two faces.  Every face pass
        /// (CloseFaces, IdentifyIncompleteFace, region closing) reaches this method, so the invariant is enforced
        /// here rather than by each caller.  Two triangles on one chord whose apexes are 0.05 nm apart in XY (RPC1
        /// 133336/133341, chord 177-188 with apexes 178 and 189) are the case the per-caller guards missed.
        /// </summary>
        public override void AddFace(IFace face)
        {
            if (!Faces.Contains(face))
            {
                foreach (IEdgeKey key in face.Edges)
                {
                    if (Edges.TryGetValue(key, out IEdge existing) && existing.Faces.Count >= 2)
                    {
                        FacesRefusedAtEdgeCapacity++;
                        if (BajajMeshGenerator.VerboseLogging)
                            Trace.WriteLine($"Face {face} refused: edge {key} already carries {existing.Faces.Count} faces.");
                        return;
                    }
                }
            }

            base.AddFace(face);
        }

        public int AddVerticies(ICollection<MorphMeshVertex> verts)
        {
            //int iStartVert = base.AddVerticies(verts.Select(v => (IVertex3D)v).ToArray());
            var iStartVert = base.AddVerticies([.. verts]);

            foreach (var v in verts)
            {
                if (v.ShapeIndex is null)
                    continue;

                ShapeIndexToVertex.Add(v.ShapeIndex, v.Index);
            }

            return iStartVert;
        }

        public MorphMeshVertex GetOrAddVertex(PolygonIndex pIndex, Vector3 vert3)
        {
            MorphMeshVertex meshVertex;
            if (!this.Contains(pIndex))
            {
                meshVertex = new MorphMeshVertex(pIndex, vert3); //TODO: Add normal here?
                this.AddVertex(meshVertex);
            }
            else
            {
                meshVertex = this[pIndex];
                Debug.Assert(meshVertex.Position == vert3); //The mesh version and the version we expect should be in the same position
            }

            return meshVertex;
        }

        public MorphMeshEdge GetEdge(IEdgeKey key) => (MorphMeshEdge)Edges[key];

        public IEnumerable<MorphMeshFace> MorphFaces
        {
            get
            {
                foreach (IFace face in this.Faces)
                {
                    if (face is MorphMeshFace morphFace)
                        yield return morphFace;
                }
            }
        }

        public IEnumerable<MorphMeshEdge> MorphEdges
        {
            get
            {
                foreach (var edge in this.Edges.Values)
                {
                    yield return (MorphMeshEdge)edge;
                }
            }
        }

        public IEnumerable<MorphMeshVertex> MorphVerticies
        {
            get
            {
                foreach (var v in this.Vertices)
                {
                    yield return (MorphMeshVertex)v;
                }
            }
        }

        /// <summary>
        /// Assign a type to each edge based on the rules specified in EdgeTypeExtensions.        
        /// JA: Safe to run with more than 2 Z levels.
        /// </summary>
        public void ClassifyMeshEdges()
        {
            foreach (var edge in this.MorphEdges.Where(e => e.Type == EdgeType.UNKNOWN).ToArray())
            {
                //if (edge.Type != EdgeType.UNKNOWN)
                //continue;

                var A = this.GetVertex(edge.A);
                var B = this.GetVertex(edge.B);

                if (A.Position.XY() == B.Position.XY())
                {
                    edge.Type = EdgeType.CORRESPONDING;
                    continue;
                }

                edge.Type = this.GetEdgeTypeWithOrientation(A, B);
            }

            return;
        }


        /// <summary>
        /// Remove old face from mesh, Reverse the order of verticies to make a new face. Add new face to mesh. Returns the new face.
        /// </summary>
        public MorphMeshFace ReverseFace(IFace f)
        {
            this.RemoveFace(f);
            MorphMeshFace newFace = new(f.iVerts.Reverse());
            this.AddFace(newFace);
            return newFace;
        }


        /// <summary>
        /// A helper function that ensures all faces have the same Z level
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="face"></param>
        /// <param name="criteria"></param>
        /// <param name="ExpectedZ">If defined all verticies of the face must have the same Z value</param>
        /// <returns></returns>
        private static bool IsInRegion(MorphRenderMesh mesh, MorphMeshFace face, Func<MorphRenderMesh, MorphMeshFace, bool> criteria, double? ExpectedZ)
        {
            if (ExpectedZ.HasValue)
            {
                if (face.AllVertsAtSameZ(mesh, out var FaceZ))
                {
                    if (FaceZ != ExpectedZ)
                        return false;
                }
                else
                {
                    return false;
                }
            }

            return criteria(mesh, face);
        }


        /// <summary>
        /// Assign all incomplete verticies (verts without a full set of faces) to regions based on connectivity
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="IncompleteVerticies"></param>
        /// <returns></returns>
        public static MorphMeshRegionGraph SecondPassRegionDetection(MorphRenderMesh mesh, List<MorphMeshVertex> IncompleteVerticies, TriangulationMesh<IVertex2D<PolygonIndex>>.ProgressUpdate OnProgress = null)
        {
            MorphMeshRegionGraph graph = new();

            //Two incomplete verticies on the same hole can each walk out the same perimeter.  Closing that region
            //twice triangulates the hole twice and leaves the shared edges carrying more faces than a manifold
            //surface allows, so track the faces already claimed by a region.
            HashSet<MorphMeshFace> facesAlreadyInRegions = [];

            SortedSet<MorphMeshVertex> listUnassignedVerticies = [.. IncompleteVerticies];
            while (listUnassignedVerticies.Count > 0)
            {
                var v = listUnassignedVerticies.First();
                listUnassignedVerticies.Remove(v);

                //Identify edges missing faces
                List<IEdge> edges = [.. v.Edges.Select(key => mesh.Edges[key]).Where(e => e.Faces.Count < 2)];

                foreach (var edge in edges)
                {
                    Stack<int> searchHistory = new();
                    searchHistory.Push(v.Index);
                    var Face = mesh.IdentifyIncompleteFace(v);
                    if (Face != null)
                    {
                        listUnassignedVerticies.RemoveWhere(iVert => Face.Contains(iVert.Index));
                        MorphMeshRegion region = null;

                        var listRegionFaces = RegionPerimeterToFaces(mesh, Face, OnProgress);
                        if (listRegionFaces.Count == 0)
                        {
                            //We probably removed corresponding verticies and had no faces left.
                            break;
                        }

                        if (listRegionFaces.All(facesAlreadyInRegions.Contains))
                            break;

                        facesAlreadyInRegions.UnionWith(listRegionFaces);

                        //A perimeter split at a corresponding pair comes back as two loops that share no vertex.
                        //One region holding both has a boundary the perimeter walk cannot order, so closing it
                        //failed and every edge of both loops stayed open (RPC1 82622/82684, 83509/83711).
                        foreach (List<MorphMeshFace> connectedFaces in SplitIntoEdgeConnectedGroups(listRegionFaces))
                        {
                            region = new MorphMeshRegion(mesh, connectedFaces, RegionType.UNTILED);
                            graph.AddNode(region);
                        }

                        break;
                    }

                    //TODO: Remove edges that now have faces or are in a region
                }
            }

            return graph;
        }

        /// <summary>
        /// Partitions faces into groups connected through shared edges.
        /// </summary>
        private static List<List<MorphMeshFace>> SplitIntoEdgeConnectedGroups(List<MorphMeshFace> faces)
        {
            Dictionary<IEdgeKey, List<MorphMeshFace>> facesByEdge = [];
            foreach (MorphMeshFace face in faces)
            {
                foreach (IEdgeKey edge in face.Edges)
                {
                    if (!facesByEdge.TryGetValue(edge, out List<MorphMeshFace> sharing))
                        facesByEdge[edge] = sharing = [];

                    sharing.Add(face);
                }
            }

            List<List<MorphMeshFace>> groups = [];
            HashSet<MorphMeshFace> visited = [];
            foreach (MorphMeshFace seed in faces)
            {
                if (!visited.Add(seed))
                    continue;

                List<MorphMeshFace> group = [];
                Stack<MorphMeshFace> pending = new();
                pending.Push(seed);
                while (pending.Count > 0)
                {
                    MorphMeshFace face = pending.Pop();
                    group.Add(face);
                    foreach (IEdgeKey edge in face.Edges)
                    {
                        foreach (MorphMeshFace neighbour in facesByEdge[edge])
                        {
                            if (visited.Add(neighbour))
                                pending.Push(neighbour);
                        }
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        /// <summary>
        /// Take a list of vertex indicies that describe the closed perimeter of a region without faces in the mesh.  Triangulate the verticies and insert faces based upon the triangulation
        /// </summary>
        public static List<MorphMeshFace> RegionPerimeterToFaces(MorphRenderMesh mesh, List<int> Face, TriangulationMesh<IVertex2D<PolygonIndex>>.ProgressUpdate OnProgress = null)
        {
            if (Face is null)
                return [];

            if (Face.Count == 3)
            {
                //If the region is only 4 points or less just create a face and region
                MorphMeshFace newFace = new(Face);
                return [newFace];
            }
            else if (Face.Count == 4)
            {
                //If the region is only 4 points or less just create a face and region
                MorphMeshFace newFace = new(Face);

                //Check for a corresponding edge, if it exists split on the corresponding edge
                for (var iVert = 0; iVert < Face.Count; iVert++)
                {
                    int iNextVert = (iVert + 1) % Face.Count;
                    var vA = mesh[Face[iVert]];
                    var vB = mesh[Face[iNextVert]];

                    if (!mesh.Contains(vA.Index, vB.Index))
                        continue;

                    //TODO: When not CORRESPONDING, prefer the shortest diagonal instead of always cutting here.
                    var iPrev = iVert - 1 < 0 ? Face.Count - 1 : iVert - 1;
                    var iNext = (iVert + 2) % Face.Count;

                    //Both triangles must share the diagonal Prev-Next1, not the existing edge Vert-Next1: a pair
                    //sharing the perimeter edge put two faces on it and none on Next-Prev (RPC1 145474/145475).
                    List<MorphMeshFace> listFaces =
                    [
                        new([Face[iPrev], Face[iVert], Face[iNextVert]]),
                        new([Face[iPrev], Face[iNextVert], Face[iNext]])
                    ];
                    return listFaces;
                }

                return [newFace];
            }
            else
            {
                // Corresponding verts share XY and break triangulation. Strip adjacent pairs first, then
                // either triangulate a clean perimeter, split non-adjacent duplicates into two loops, or
                // triangulate a half that only duplicates at its endpoints (closed in XY).
                while (RemoveFirstAdjacentCorrespondingVerticies(mesh, ref Face))
                {
                }

                if (Face.Count <= 2)
                    return [];

                if (Face.Count == 3)
                    return [new MorphMeshFace(Face)];

                if (PerimeterHasInteriorDuplicateXY(mesh, Face))
                {
                    if (!TrySplitPerimeterAtDuplicateXY(mesh, Face, out List<int> partA, out List<int> partB))
                    {
                        Trace.WriteLine(
                            $"Skipping region perimeter with corresponding points that could not be split ({Face.Count} verts).");
                        return [];
                    }

                    List<MorphMeshFace> splitFaces = [];
                    splitFaces.AddRange(RegionPerimeterToFaces(mesh, partA, OnProgress));
                    splitFaces.AddRange(RegionPerimeterToFaces(mesh, partB, OnProgress));
                    return splitFaces;
                }

                //Create a polygon for the region. Endpoint-only XY duplicates (a split half closed in XY)
                //are fine: EnsureClosedRing collapses them to a closed 2D ring.
                List<int> closedIndices = [.. Face.EnsureClosedRing()];
                Polygon regionBorder;
                try
                {
                    regionBorder = new([.. closedIndices.Select(iVert => mesh[iVert].Position.XY())]);
                }
                catch (ArgumentException e)
                {
                    //A perimeter half produced by the duplicate-XY split can still self-intersect when the region
                    //pinches a second time (RPC1 102434/102614/364316..., 102803/145352/...).  Leaving that region
                    //untiled costs a hole in one slice; letting the exception out fails the whole slice.
                    Trace.WriteLine($"Skipping region perimeter that is not a valid ring ({Face.Count} verts): {e.Message}");
                    return [];
                }

                var regionMesh = regionBorder.Triangulate(iPoly: 0, OnProgress: OnProgress);

                List<MorphMeshFace> listRegionFaces = new(regionMesh.Faces.Count);

                foreach (var f in regionMesh.Faces)
                {
                    int[] iMeshVerts = [.. f.iVerts.Select(v => closedIndices[v])];
                    MorphMeshFace newFace = new(iMeshVerts);
                    listRegionFaces.Add(newFace);
                }

                return listRegionFaces;
            }
        }

        /// <summary>
        /// True when some XY appears more than once on the perimeter excluding the trivial closed-ring
        /// case where only the first and last entries share XY.
        /// </summary>
        private static bool PerimeterHasInteriorDuplicateXY(MorphRenderMesh mesh, List<int> face)
        {
            if (face is null || face.Count < 2)
                return false;

            Vector2[] xys = [.. face.Select(i => mesh[i].Position.XY())];
            bool endpointsMatch = xys[0] == xys[^1];

            Dictionary<Vector2, int> counts = new(xys.Length);
            for (int i = 0; i < xys.Length; i++)
            {
                counts.TryGetValue(xys[i], out int c);
                counts[xys[i]] = c + 1;
            }

            for (int i = 0; i < xys.Length; i++)
            {
                int count = counts[xys[i]];
                if (count <= 1)
                    continue;

                // First/last only: a split half closed in XY — not an interior pinch.
                if (endpointsMatch && count == 2
                    && (i == 0 || i == xys.Length - 1)
                    && xys[i] == xys[0])
                    continue;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Split a perimeter at a non-adjacent duplicate-XY pair (usually mutual Corresponding partners)
        /// into two closed-in-XY loops that can be triangulated separately.
        /// </summary>
        private static bool TrySplitPerimeterAtDuplicateXY(MorphRenderMesh mesh, List<int> face, out List<int> partA, out List<int> partB)
        {
            partA = null;
            partB = null;
            int n = face.Count;
            if (n < 4)
                return false;

            static bool AreCyclicAdjacent(int i, int j, int count)
            {
                int dist = Math.Abs(i - j);
                return dist == 1 || dist == count - 1;
            }

            // Prefer mutual corresponding partners on the perimeter.
            for (int i = 0; i < n; i++)
            {
                MorphMeshVertex vi = mesh[face[i]];
                if (!vi.Corresponding.HasValue)
                    continue;

                int j = face.IndexOf(vi.Corresponding.Value);
                if (j < 0 || j == i || AreCyclicAdjacent(i, j, n))
                    continue;
                if (BuildSplitParts(face, i, j, out partA, out partB))
                    return true;
            }

            // Fallback: any non-adjacent duplicate XY.
            for (int i = 0; i < n; i++)
            {
                Vector2 xy = mesh[face[i]].Position.XY();
                for (int j = i + 1; j < n; j++)
                {
                    if (mesh[face[j]].Position.XY() != xy)
                        continue;
                    if (AreCyclicAdjacent(i, j, n))
                        continue;
                    if (BuildSplitParts(face, i, j, out partA, out partB))
                        return true;
                }
            }

            return false;
        }

        private static bool BuildSplitParts(List<int> face, int i, int j, out List<int> partA, out List<int> partB)
        {
            partA = null;
            partB = null;
            if (i > j)
                (i, j) = (j, i);

            // Arc i..j inclusive, and the complementary arc j..i inclusive (wrapping).
            partA = face.GetRange(i, j - i + 1);
            partB = [];
            for (int k = j; k < face.Count; k++)
                partB.Add(face[k]);
            for (int k = 0; k <= i; k++)
                partB.Add(face[k]);

            if (partA.Count < 3 || partB.Count < 3)
            {
                partA = null;
                partB = null;
                return false;
            }

            return true;
        }

        /// <summary>
        ///If the corresponding verticies are adjacent in the face then we can add a quad using the index before and after the corresponding verticies
        /// 
        /// Z = 0    <-- A -- B                  <-- A -- B
        ///                   |      becomes         | \  |  with B,C being removed from the face
        /// Z = 0    <-- D -- C                  <-- D -- C
        /// 
        /// This function removes the first instance found from the passed list, removes the corresponding verticies from the Face.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="Face"></param>
        /// <returns>True if an adjacent corresponding vertex pair was found and removed</returns>
        private static bool RemoveFirstAdjacentCorrespondingVerticies(MorphRenderMesh mesh, ref List<int> Face)
        {
            //This test is only possible if we can create a quad.  If we have three verts we return false and the caller should manage to create a triangle face with three verts.
            if (Face.Count <= 3)
                return false;
            /////////////////////////////////////////////////////////////
            //Case 0: 
            //If the corresponding verticies are adjacent in the face then we can add a quad using the index before and after the corresponding verticies
            // 
            // Z = 0    <-- A -- B                  <-- A -- B
            //                   |      becomes         | \  |  with B,C being removed from the face
            // Z = 0    <-- D -- C                  <-- D -- C

            InfiniteIndexSet FaceIndex = new(Face);
            for (var i = Face.Count - 1; i >= 0; i--)
            {
                var index = FaceIndex[i];
                var next_index = FaceIndex[i + 1];

                var v1 = mesh[index];
                MorphMeshVertex v2;

                if (v1.Corresponding.HasValue == false)
                    continue;

                v2 = mesh[next_index];

                if (v2.Corresponding.HasValue == false)
                    continue;

                //Not sure how the next vertex could be corresponding and not corresponding to the index because we add a vertex between corresponding verticies.
                if (v1.Corresponding.Value != next_index)
                    continue;

                //OK, create a quad using the indicies before and after the adjacent corresponding verts.  Then split the quad.
                Face quad = new([FaceIndex[i - 1], FaceIndex[i], FaceIndex[i + 1], FaceIndex[i + 2]]);

                //Either diagonal puts one triangle on the corresponding edge.  Where the contours cross, the wedge
                //faces already own both sides of that edge, and splitting anyway made it a 3-face edge (RPC1
                //83509/83711, 82622/82684).  The quad is then left for the region to close without the pair.
                MorphMeshEdge correspondingEdge = (MorphMeshEdge)mesh[new EdgeKey(index, next_index)];
                if (correspondingEdge.Faces.Count < 2)
                    mesh.SplitFace(quad);
                else
                    Trace.WriteLine($"Region perimeter: corresponding edge {correspondingEdge} already has two faces; quad {quad} not split.");

                //Remove the corresponding verticies we created.
                if (i <= Face.Count - 2)
                    Face.RemoveRange(i, 2);
                else
                {
                    Face.RemoveAt(i);
                    Face.RemoveAt(0);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Identify all adjacent faces which have an invalid edge in the same plane (Z level)
        /// 
        /// I left this function in MorphologyMesh instead of moving to BajajMeshGenerator because
        /// it should work regardless of the number of Z levels in the mesh. 
        /// </summary>
        public static List<MorphMeshRegion> IdentifyRegions(MorphRenderMesh mesh)
        {
            List<MorphMeshRegion> listRegions = [];
            SortedSet<IFace> FacesAssignedToRegions = [];

            foreach (var f in mesh.Faces)
            {
                if (FacesAssignedToRegions.Contains(f))
                {
                    continue;
                }

                MorphMeshFace face = (MorphMeshFace)f;

                var faceVerts = face.iVerts.Select(i => (MorphMeshVertex)mesh.Vertices[i]).ToArray();

                if (face.IsInUntiledRegion(mesh))
                {
                    MorphMeshRegion region = new(mesh, mesh.FloodFillRegion(face, (m, foundFace) => IsInRegion(m, foundFace, MorphMeshFace.IsInUntiledRegion, new double?()), MorphMeshFace.AdjacentFaceDoesNotCrossContour, FacesAssignedToRegions), RegionType.UNTILED);
                    listRegions.Add(region);
                    FacesAssignedToRegions.UnionWith(region.Faces);
                    continue;
                }

                if (!face.AllVertsAtSameZ(mesh, out var FaceZ))
                {
                    //FacesAssignedToRegions.Add(face);
                    continue;
                }

                if (face.IsInExposedRegion(mesh))
                {
                    MorphMeshRegion region = new(mesh, mesh.FloodFillRegion(face,
                        (m, foundFace) => IsInRegion(m, foundFace, MorphMeshFace.IsInExposedRegion, FaceZ.Value),
                        MorphMeshFace.AdjacentFaceDoesNotCrossContour, FacesAssignedToRegions),
                        RegionType.EXPOSED);
                    listRegions.Add(region);
                    FacesAssignedToRegions.UnionWith(region.Faces);
                    continue;
                }

                if (face.IsInHoleRegion(mesh))
                {
                    MorphMeshRegion region = new(mesh, mesh.FloodFillRegion(face, (m, foundFace) =>
                        IsInRegion(m, foundFace, MorphMeshFace.IsInHoleRegion, FaceZ.Value),
                        MorphMeshFace.AdjacentFaceDoesNotCrossContour,
                        FacesAssignedToRegions),
                        RegionType.HOLE);
                    listRegions.Add(region);
                    FacesAssignedToRegions.UnionWith(region.Faces);
                    continue;
                }

                if (face.IsInInvaginatedRegion(mesh))
                {
                    MorphMeshRegion region = new(mesh, mesh.FloodFillRegion(face,
                        (m, foundFace) => IsInRegion(m, foundFace, MorphMeshFace.IsInInvaginatedRegion, FaceZ.Value),
                        MorphMeshFace.AdjacentFaceDoesNotCrossContour, FacesAssignedToRegions),
                        RegionType.INVAGINATION);

                    //Whether or not the region is valid we mark it as checked so we don't repeat the floodfill for every face in the region.
                    FacesAssignedToRegions.UnionWith(region.Faces);


                    //Invaginated regions can sometimes be bridges between two seperate ares of the same cell.  Test if the region is valid by examing the entire region for two open exits.
                    if (MorphMeshRegion.IsValidInvagination(region))
                    {
                        listRegions.Add(region);
                        continue;
                    }
                }


                FacesAssignedToRegions.Add(face);
            }

            return listRegions;
        }


        public List<int> IdentifyIncompleteFace(int iVert)
        {
            IVertex origin = this.Vertices[iVert];
            return IdentifyIncompleteFace(origin);
        }


        /// <summary>
        /// Find all edges that enclose a loop of verticies missing faces
        /// Returns a list of vertex indicies that describe the perimeter of a mesh region without a face, or null if one cannot be found
        /// </summary>
        /// <param name="MaxFaceVerts">Optional param to specify a max path length to shorten searches</param>
        public List<int> IdentifyIncompleteFace(IVertex origin, int? MaxFaceVerts = null)
        {
            //Identify edges missing faces
            List<MorphMeshEdge> edges = [.. origin.Edges.Select(key => (MorphMeshEdge)Edges[key]).Where(e => (e.Type != EdgeType.CONTOUR && e.Faces.Count < 2) ||
                                                                                                         (e.Type == EdgeType.CONTOUR && e.Faces.Count == 0))];

            List<int> ShortestFace = null;
            foreach (var edge in edges)
            {
                var Face = FindAnyCloseableFace(origin.Index, this[edge.OppositeEnd(origin.Index)], edge, MaxPathLength: MaxFaceVerts);
                if (Face != null)
                {
                    if (ShortestFace is null)
                    {
                        ShortestFace = Face;
                    }
                    else
                    {
                        if (ShortestFace.Count > Face.Count)
                        {
                            ShortestFace = Face;
                        }
                        else if (ShortestFace.Count == Face.Count)
                        {
                            //In this case use the face with the smallest perimeter     
                            ShortestFace = this.PathDistance(ShortestFace) < this.PathDistance(Face) ? ShortestFace : Face;
                        }
                    }
                }
            }

            if (ShortestFace != null)
            {
                return ShortestFace;
            }

            return null;
            //Face should be a loop of verticies that connect to our origin point
        }

        /// <summary>
        /// Identify if there are faces that could be created using the specified edge
        /// </summary>
        /// <param name="TargetVert"></param>
        /// <param name="current"></param>
        /// <param name="testEdge"></param>
        /// <param name="CheckedEdges"></param>
        /// <param name="Path"></param>
        /// <param name="MaxPathLength">Maximum length of the path.  If a potential path exceeds this length it is abandoned.</param>
        /// <param name="EdgeCriteriaFunc">If not null, edgekeys passes to this function must return true to be included in the path</param>
        /// <returns></returns>
        public List<int> FindAnyCloseableFace(int TargetVert,
                                                IVertex current,
                                                IEdge testEdge,
                                                SortedSet<IEdgeKey> CheckedEdges = null,
                                                Stack<int> Path = null,
                                                int? MaxPathLength = null,
                                                Func<Stack<int>, SortedSet<IEdgeKey>, IVertex, IEdgeKey, bool> EdgeCriteriaFunc = null)
        {
            CheckedEdges ??= [];

            if (Path is null)
            {
                Path = new Stack<int>();
                Path.Push(TargetVert);
            }

            /////////////////////////////////////////////////////////////

            CheckedEdges.Add(testEdge.Key);
            //if (Path.Count > 4) //We must return only triangles or quads, and we return closed loops
            //return null;

            if (current.Index == TargetVert)
            {
                //Destination found
                return [.. Path];
            }
            else if (Path.Contains(current.Index))
            {
                //We've looped into our own stack
                return null;
            }
            else if (MaxPathLength.HasValue && Path.Count >= MaxPathLength.Value)
            {
                //This path is too long, move on
                return null;
            }
            else
            {
                //Make sure the face formed by the top three entries in the path is not already present in the mesh

                var FaceTest = StackExtensions<int>.Peek(Path, 2);
                if (FaceTest.Count == 2)
                {
                    FaceTest.Insert(0, current.Index);
                    if (this.Contains(new Face(FaceTest)))
                    {
                        return null;
                    }
                }

                //If we aren't an existing face then add to the path and continue the search
                Path.Push(current.Index);
            }

            List<MorphMeshEdge> EdgesToCheck = [];
            if (EdgeCriteriaFunc is null)
            {
                foreach (var edgekey in current.Edges.Where(e => !CheckedEdges.Contains(e)))
                {
                    MorphMeshEdge edge = this.Edges[edgekey] as MorphMeshEdge;
                    if (edge.Type == EdgeType.CONTOUR)
                    {
                        //Contour edges only need one face to be complete
                        if (edge.Faces.Count == 0)
                        {
                            EdgesToCheck.Add(edge);
                        }
                    }
                    else
                    {
                        if (edge.Faces.Count < 2)
                        {
                            EdgesToCheck.Add(edge);
                        }
                    }
                }
            }
            else
            {
                EdgesToCheck = [.. current.Edges.Where(e => EdgeCriteriaFunc(Path, CheckedEdges, current, e)).Select(key => this.Edges[key] as MorphMeshEdge)];
            }

            List<int> ShortestFace = null;
            if (EdgesToCheck.Count == 1)
            {
                var edge = EdgesToCheck.First();
                return FindAnyCloseableFace(TargetVert, this[edge.OppositeEnd(current.Index)], edge, CheckedEdges, Path, MaxPathLength, EdgeCriteriaFunc);
            }
            else if (EdgesToCheck.Count > 1)
            {
                //Test all of the edges we have not examined yet who do not have two faces already
                //Search the corresponding edges first since they can short-circuit a path

                foreach (var edge in EdgesToCheck.OrderBy(e => e.Type != EdgeType.CORRESPONDING))
                {
                    var Face = FindAnyCloseableFace(TargetVert, this[edge.OppositeEnd(current.Index)], edge, [.. CheckedEdges], new Stack<int>(Path.Reverse()), MaxPathLength, EdgeCriteriaFunc);

                    if (Face != null)
                    {
                        if (ShortestFace is null)
                        {
                            ShortestFace = Face;
                        }
                        else
                        {
                            if (ShortestFace.Count > Face.Count)
                            {
                                ShortestFace = Face;
                            }
                            else if (ShortestFace.Count == Face.Count)
                            {
                                //In this case use the face with the smallest perimeter     
                                ShortestFace = this.PathDistance(ShortestFace) < this.PathDistance(Face) ? ShortestFace : Face;
                            }
                        }
                    }
                }
            }

            if (ShortestFace != null)
            {
                return ShortestFace;
            }

            //Take this index off the stack since we did not locate a path
            Path.Pop();

            return null;
        }


        /// <summary>
        /// Build an RTree using SliceChords in the mesh.  
        /// Note that slice-chords cross Z levels so CONTOUR and ARTIFICIAL edges are not included
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public SliceChordRTree CreateChordTree(ICollection<double> ZLevels)
        {
            SliceChordRTree rTree = new();

            //double MinZ = ZLevels.Min();
            //double MaxZ = ZLevels.Max();

            ///Create a list of all slice chords.  Contours are valid but are not slice chords since they don't cross sections
            foreach (var e in this.Edges.Values.Where(e => //this[e.A].Position.Z >= MinZ && this[e.A].Position.Z <= MaxZ &&
                                                           //this[e.B].Position.Z >= MinZ && this[e.B].Position.Z <= MaxZ &&
                                                                     (((MorphMeshEdge)e).Type != EdgeType.CONTOUR) &&
                                                                     (((MorphMeshEdge)e).Type != EdgeType.ARTIFICIAL) &&
                                                                     (((MorphMeshEdge)e).Type != EdgeType.CORRESPONDING)))
            {
                //Two verticies at one XY that were not paired as CORRESPONDING (a vertex a shape repeats, or two
                //shapes on one band touching at a vertex) leave a zero-length chord.  It cannot intersect anything
                //and LineSegment refuses to build it, which used to abort the whole slice (RPC1 365345/365348).
                if (this[e.A].Position.XY() == this[e.B].Position.XY())
                {
                    Trace.WriteLine($"CreateChordTree: skipping zero-length {((MorphMeshEdge)e).Type} edge {e.A}[{this[e.A].ShapeIndex}] - {e.B}[{this[e.B].ShapeIndex}] at {this[e.A].Position}");
                    continue;
                }

                var bbox = this.ToSegment(e).BoundingBox.ToRTreeRect(0);
                if (!(this[e.A].ShapeIndex is null || this[e.B].ShapeIndex is null))
                {
                    SliceChord chord = new(this[e.A].ShapeIndex, this[e.B].ShapeIndex, this.Shapes);
                    var AZ = this.Vertices[e.A].Position.Z;
                    var BZ = this.Vertices[e.B].Position.Z;
                    rTree.Add(bbox, chord); //(MinZ: Math.Min(AZ,BZ), MaxZ: Math.Max(AZ,BZ)), e);
                }
                else
                {
                    MeshChord chord = new(this, e.A, e.B);
                    rTree.Add(bbox, chord);
                }
            }

            return rTree;
        }

        /// <summary>
        /// Returns the region, a set of faces, which are connected to the passed face and meet the criteria function
        /// </summary>
        /// <param name="f"></param>
        /// <param name="MeetsCriteriaFunc"></param>
        /// <param name="CheckedFaces"></param>
        /// <returns></returns>
        public SortedSet<MorphMeshFace> FloodFillRegion(MorphMeshFace f, FaceMeetsCriteriaFunction faceMeetsCriteriaFunc, EdgeMeetsCriteriaFunc EdgeMeetsCriteriaFunc, IEnumerable<IFace> CheckedFaces)
        {
            SortedSet<IFace> checkedRegionFaces = [.. CheckedFaces];

            return FloodFillRegionRecurse(f, faceMeetsCriteriaFunc, EdgeMeetsCriteriaFunc, ref checkedRegionFaces);
        }

        /// <summary>
        /// Performs a flood fill that includes all faces that pass the criteria function
        /// </summary>
        /// <param name="f"></param>
        /// <param name="MeetsCriteriaFunc"></param>
        /// <param name="CheckedFaces"></param>
        /// <returns></returns>
        private SortedSet<MorphMeshFace> FloodFillRegionRecurse(MorphMeshFace f, FaceMeetsCriteriaFunction faceMeetsCriteriaFunc, EdgeMeetsCriteriaFunc EdgeMeetsCriteriaFunc, ref SortedSet<IFace> CheckedFaces)
        {
            SortedSet<MorphMeshFace> region =
            [
                f
            ];
            CheckedFaces.Add(f);

            foreach (var adjacent in f.AdjacentFaces(this, EdgeMeetsCriteriaFunc))
            {
                if (CheckedFaces.Contains(adjacent))
                    continue;

                if (faceMeetsCriteriaFunc != null && false == faceMeetsCriteriaFunc(this, adjacent))
                {
                    CheckedFaces.Add(adjacent);
                    continue;
                }

                region.UnionWith(FloodFillRegionRecurse(adjacent, faceMeetsCriteriaFunc, EdgeMeetsCriteriaFunc, ref CheckedFaces));
            }

            return region;
        }

        /// <summary>
        /// Remove every edge whose type affirmatively rules it off the final surface.  Unclassified (UNKNOWN) edges
        /// are deliberately left alone and reported instead: edges minted implicitly by AddFace carry UNKNOWN, and
        /// ClassifyMeshEdges only runs once inside AddDelaunayEdges, so deleting them would drop tiling that later
        /// passes added and nothing ever rejected.
        /// </summary>
        public static void RemoveInvalidEdges(MorphRenderMesh mesh)
        {
            foreach (var e in mesh.Edges.Values.Where(e => ((MorphMeshEdge)e).Type.IsAffirmativelyInvalid()).ToArray())
            {
                mesh.RemoveEdge(e);
            }

            ReportUnclassifiedEdges(mesh);
        }

        public void RemoveInvalidEdges() => RemoveInvalidEdges(this);

        /// <summary>
        /// An edge still typed UNKNOWN after a removal pass means classification never reached it, which is not fatal
        /// and is expected whenever the pass runs after faces have been added.  The count is surfaced so a mesh full
        /// of unclassified edges is visible in the BajajTest and BajajMultiTest logs instead of passing unnoticed.
        /// </summary>
        private static void ReportUnclassifiedEdges(MorphRenderMesh mesh)
        {
            int unclassified = mesh.MorphEdges.Count(e => e.Type == EdgeType.UNKNOWN);
            if (unclassified == 0)
                return;

            //Deliberately traced rather than asserted.  A whole-cell run of structure 180 reports zero unclassified
            //edges, but only because RemoveInvalidEdges is called before the face-generating passes; calling it after
            //them leaves unclassified edges as a matter of course (a 3-segment ribbon leaves 3, stacked squares 4).
            //An assert would therefore fire on exactly the ordering this method was changed to tolerate.
            Trace.WriteLine($"RemoveInvalidEdges: {unclassified} of {mesh.Edges.Count} edges are still unclassified (UNKNOWN) and were left in place. {mesh}");
        }
    }
}
