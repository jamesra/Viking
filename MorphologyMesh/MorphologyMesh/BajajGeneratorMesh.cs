using Geometry;
using Geometry.Meshing;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace MorphologyMesh
{

    /// <summary>
    /// 
    /// Used by the Bajaj generator to represent two sets of polygons, upper and lower, regardless of actual Z levels.
    /// 
    /// MorphRenderMesh class was originally written to handle polygons at arbitrary Z levels.  However, when generating a full mesh it
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
    /// </summary>
    public class BajajGeneratorMesh(SliceTopology topology, Slice slice = null) : MorphRenderMesh(topology.Shapes, topology.ShapeZ, topology.IsUpper)
    {
        public override bool[] IsUpperShape => Topology.IsUpper;

        /// <summary>
        /// Vertex indicies that belong to an upper polygon
        /// </summary>
        public ImmutableSortedSet<int> UpperShapeIndicies => Topology.UpperShapeIndicies;
        /// <summary>
        /// Vertex indicies that belong to a lower polygon
        /// </summary>
        public ImmutableSortedSet<int> LowerShapeIndicies => Topology.LowerShapeIndicies;

        internal IShape2D[] UpperShapes => Topology.UpperShapes;
        internal IShape2D[] LowerShapes => Topology.LowerShapes;

        //private readonly List<MorphMeshRegion> _Regions = new List<MorphMeshRegion>();

        public List<MorphMeshRegion> Regions { get; private set; }

        /// <summary>
        /// An optional field that allows tracking of which annotations compose the mesh
        /// </summary>
        public readonly SliceTopology Topology = topology;

        /// <summary>
        /// An optional field that allows tracking of which annotations compose the mesh
        /// </summary>
        public readonly Slice Slice = slice;

        /// <summary>
        /// How thick the slice is along the Z axis
        /// </summary>
        public double SliceThickness => Topology.SliceThickness;

        /// <summary>
        /// Where the center of the slice is along the Z axis
        /// </summary>
        public double SliceCenterZ => Topology.SliceCenterZ;

        /// <summary>
        /// A cache for failures when finding slice chords
        /// </summary>
        internal SliceChordsTestResultsCache SliceChordCandidateCache = new();

        private Func<int, int, bool> _shapeLinkPredicate;

        /// <summary>
        /// Delegate form of <see cref="SliceTopology.MayTile"/> for the slice chord tests, or null when this mesh
        /// was built without link data so the ShapeLink test is skipped instead of rejecting everything.
        /// Cached because it is queried once per chord candidate.
        /// </summary>
        internal Func<int, int, bool> ShapeLinkPredicate
        {
            get
            {
                if (Topology.HasLinkData == false)
                    return null;

                return _shapeLinkPredicate ??= Topology.MayTile;
            }
        }

        /// <summary>
        /// Vertex ranges allocated to each partner where a polyline forks across this slice, or null when nothing forks.
        /// </summary>
        internal PolylineForkPartition ForkPartition => Topology.ForkPartition;

        /// <summary>
        /// Convenience for callers holding only a <see cref="MorphRenderMesh"/>.
        /// </summary>
        internal static Func<int, int, bool> LinkPredicateFor(MorphRenderMesh mesh) =>
            (mesh as BajajGeneratorMesh)?.ShapeLinkPredicate;

        /// <inheritdoc cref="LinkPredicateFor"/>
        internal static PolylineForkPartition ForkPartitionFor(MorphRenderMesh mesh) =>
            (mesh as BajajGeneratorMesh)?.ForkPartition;

        /// <summary>
        /// True when a single-face edge borders a deliberate fork gap rather than a hole.
        ///
        /// The test is deliberately narrow: both endpoints must be verticies of a polyline, and at least one must be
        /// a vertex the partitioner designated as a fork boundary.  A real tear one segment away from the fork is
        /// still reported as a hole, which is the point; a broad exemption would hide exactly the defects the
        /// manifold report exists to surface.
        /// </summary>
        public bool IsForkGapBoundaryEdge(IEdgeKey edge)
        {
            if (ForkPartition is null)
                return false;

            IShapeIndex indexA = this[edge.A].ShapeIndex;
            IShapeIndex indexB = this[edge.B].ShapeIndex;
            if (indexA is null || indexB is null)
                return false;

            if (Shapes[indexA.ShapeIndex] is not Polyline || Shapes[indexB.ShapeIndex] is not Polyline)
                return false;

            return ForkPartition.IsForkBoundaryVertex(indexA.ShapeIndex, indexA.VertexIndex)
                || ForkPartition.IsForkBoundaryVertex(indexB.ShapeIndex, indexB.VertexIndex);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Called from the base constructor via PopulateMesh.  C# runs a derived class's field initializers before
        /// the base constructor, so <see cref="Topology"/> is already assigned by the time this is reached; the
        /// assert guards that ordering, because silently seeing a default topology here would disable the link gate
        /// on every mesh without any other symptom.
        /// </remarks>
        protected override bool CorrespondenceAllowed(int iShapeA, int iShapeB)
        {
            Debug.Assert(Topology.IsValid, "Topology must be assigned before PopulateMesh queries correspondence.");

            return Topology.MayTile(iShapeA, iShapeB);
        }

        /// <summary>
        /// Set to true if a non-fatal error occurred during face generation (e.g. a region or pass could
        /// not be completed).  The mesh may still be partially generated, but callers should treat it as
        /// suspect rather than a fully successful reconstruction.
        /// </summary>
        public bool GenerationHadErrors { get; set; } = false;

        /// <summary>
        /// The manifold state measured at the end of face generation.  Lets callers and tests inspect why a
        /// slice was flagged rather than only knowing that something went wrong.
        /// </summary>
        public MeshManifoldReport ManifoldReport { get; set; }

        public override string ToString()
        {
            StringBuilder output = new();
            if (Slice != null)
            {
                output.Append($"{Slice}:\n\t");
            }

            output.Append(base.ToString());
            return output.ToString();
        }

        public BajajGeneratorMesh(IReadOnlyList<IShape2D> shapes, IReadOnlyList<double> ZLevels, IReadOnlyList<bool> IsUpperShape) :
            this(new SliceTopology(shapes, IsUpperShape, ZLevels))
        {

        }

        public IShape2D[] GetSameLevelShapes(in IShapeIndex key) => IsUpperShape[key.ShapeIndex] ? UpperShapes : LowerShapes;

        public IShape2D[] GetAdjacentLevelShapes(in IShapeIndex key) => IsUpperShape[key.ShapeIndex] ? LowerShapes : UpperShapes;

        public IShape2D[] GetSameLevelShapes(in SliceChord sc) => IsUpperShape[sc.Origin.ShapeIndex] ? UpperShapes : LowerShapes;

        public IShape2D[] GetAdjacentLevelShapes(in SliceChord sc) => IsUpperShape[sc.Origin.ShapeIndex] ? LowerShapes : UpperShapes;


        public void IdentifyRegionsViaFaces() => this.Regions = IdentifyRegions(this);

        public MorphMeshRegionGraph IdentifyRegionsViaVerticies(List<MorphMeshVertex> IncompleteVerticies) => SecondPassRegionDetection(this, IncompleteVerticies);

        public Vector2 CalculateAverageVertexPositionXY()
        {
            List<Vector2> points = new(this.Vertices.Count);

            var groups = this.Vertices.GroupBy(v => v.Corresponding.HasValue);
            foreach (var g in groups)
            {
                if (g.Key == true)
                {
                    var uniquePoints = g.Select(v => v.Position.XY()).Distinct();
                    points.AddRange(uniquePoints);
                }
                else
                {
                    points.AddRange(g.Select(v => v.Position.XY()));
                }
            }

            return points.Average();
        }

        /// <summary>
        /// For each vertex, 
        /// find all paths along edges without faces that can return to the that enclose triangles or quads and create faces if they don't exist
        /// </summary>
        public void CloseFaces(IEnumerable<IVertex> VertsToClose = null)
        {
            VertsToClose ??= this.Vertices;

            foreach (var v in VertsToClose)
            {
                this.CloseFaces(v);
            }
        }

        /// <summary>
        /// For the passed vertex, identify any connected edges without two faces.  Determine if a path can be walked along edges with missing faces
        /// back to the passed vertex.  If a path exists with a length of 3 or 4 add it to the mesh.
        /// </summary>
        public void CloseFaces(IVertex vertexToClose)
        {
            //Identify edges missing faces, COUNTOUR edges only have one face to be considered complete
            List<IEdge> edges = [.. vertexToClose.Edges.Select(key => Edges[key]).Where(e => ((MorphMeshEdge)e).FacesComplete == false)];

            foreach (var edge in edges)
            {
                List<int> Face = FindCloseableFace(vertexToClose.Index, this[edge.OppositeEnd(vertexToClose.Index)], edge);
                if (Face != null)
                {
                    Debug.Assert(Face.Count == 3 || Face.Count == 4);
                    if (Face.Count == 4)
                        continue;

                    IFace f = this.CreateFace(Face);

                    if (this.Faces.Contains(f) == false)
                        this.AddFace(f);

                    if (f.iVerts.Length == 4)
                        this.SplitFace(f);
                }
            }
        }

        /// <summary>
        /// Identify if there are faces that could be created using the specified verticies
        /// </summary>
        /// <param name="targetVert"></param>
        /// <param name="current"></param>
        /// <param name="testEdge"></param>
        /// <param name="checkedEdges"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        private List<int> FindCloseableFace(int targetVert, IVertex current, IEdge testEdge, SortedSet<IEdgeKey> checkedEdges = null, Stack<int> path = null)
        {
            checkedEdges ??= [];

            if (path is null)
            {
                path = new Stack<int>();
                path.Push(targetVert);
            }

            //Make sure the face formed by the top three entries in the path is not already present in the mesh

            List<int> faceTest = StackExtensions<int>.Peek(path, 3);
            if (faceTest.Count == 3)
            {
                if (this.Contains(new Face(faceTest)))
                    return null;
            }

            /////////////////////////////////////////////////////////////

            checkedEdges.Add(testEdge.Key);
            if (path.Count > 4) //We must return only triangles or quads, and we return closed loops
                return null;

            if (current.Index == targetVert)
            {
                return [.. path];
            }
            else
            {
                path.Push(current.Index);
            }

            //Test all of the edges we have not examined yet who do not have two faces already
            List<int> shortestFace = null;
            foreach (IEdge edge in current.Edges.Where(e => !checkedEdges.Contains(e)).Select(e => this.Edges[e]).Where(e => ((MorphMeshEdge)e).FacesComplete == false))
            {
                List<int> Face = FindCloseableFace(targetVert, this[edge.OppositeEnd(current.Index)], edge, [.. checkedEdges], new Stack<int>(path));

                if (Face != null)
                {
                    if (shortestFace is null)
                    {
                        shortestFace = Face;
                    }
                    else
                    {
                        if (shortestFace.Count > Face.Count)
                        {
                            shortestFace = Face;
                        }
                    }
                }
            }

            if (shortestFace != null)
            {
                return shortestFace;
            }

            //Take this index off the stack since we did not locate a path
            path.Pop();

            return null;
        }

        /// <summary>
        /// Ensure every 2-manifold patch has consistent, outward-facing winding so backface culling does not
        /// punch holes in the surface. Delegates patch BFS to <see cref="MeshWindingReorientation"/> (only
        /// edges with exactly two faces) then majority-vote outward vs contours. Greedy repair is skipped when
        /// any edge still has three faces; that pass oscillates on non-manifold junctions.
        /// </summary>
        public void EnsureFacesHaveExternalNormals()
        {
            var options = new MeshWindingReorientation.Options
            {
                RespectAnchorFaces = true,
                AlwaysOrientOutward = false,
                RunRepairPass = false
            };
            MeshWindingReorientation.Reorient(this, options);

            if (HasPolygonShapes)
            {
                var ctx = MorphMeshOutwardOrientation.ShapeContext.FromSliceTopology(Topology);
                MorphMeshOutwardOrientation.OrientComponentsOutward(this, ctx);
            }

            var after = MeshWindingDiagnostics.Analyze(this);
            if (after.NonManifoldEdges == 0)
                MeshWindingReorientation.RepairManifoldConsistency(this);

            foreach (MorphMeshFace f in this.MorphFaces)
                f.NormalIsKnownCorrect = true;
        }

        /// <summary>
        /// Return true if the face has CCW winding when viewed from the exterior of the mesh
        /// </summary>
        public bool FaceHasCCWWinding(IFace f)
        {
            MorphMeshVertex[] verts = [.. this[f.iVerts]];

            Vector3 n = this.Normal(f);
            Vector2 face_center;

            bool CheckAgainstUpperPolygons; //True if we check if the centroid is contained in upper polygons, false if centroid needs to be checked against lower polygons
            //Check if the normal is oriented up or down.  If it is up, then check that the face centroid is not contained within the upper polygons, and vice versa.
            if (Math.Abs(n.Z) < Global.Epsilon)
            {
                if (f.IsTriangle() == false)
                    return true;

                //First find the vertex that is not part of the corresponding pair that created this face.  Note that corresponding verts can be adjacent within a polygon,
                //so if the vertex is corresponding it could stil be the extra vertex of the triangle if its corresponding vertex is not part of the face.
                MorphMeshVertex noncorresponding = verts.Where(v => v.Corresponding.HasValue == false || f.iVerts.Contains(v.Corresponding.Value) == false).First();
                if (noncorresponding.ShapeIndex is null)
                    return true;

                int iNonCorresponding = Array.IndexOf(verts, noncorresponding);
                bool NonCorrespondingIsUpper = IsUpperShape[noncorresponding.ShapeIndex.ShapeIndex];

                    InfiniteSequentialIndexSet faceIndexer = new(0, f.iVerts.Length, 0);

                    MorphMeshVertex nextVert = verts[faceIndexer[iNonCorresponding + 1]];
                    MorphMeshVertex prevVert = verts[faceIndexer[iNonCorresponding - 1]];
                    bool output;
                    if (nextVert.ShapeIndex == noncorresponding.ShapeIndex.Next)
                    {
                        output = NonCorrespondingIsUpper == false;
                        //seg = new LineSegment(noncorresponding.Position.XY(), verts[faceIndexer[iNonCorresponding + 1]].Position.XY());
                    }
                    else if (nextVert.ShapeIndex == noncorresponding.ShapeIndex.Previous)
                    {
                        output = NonCorrespondingIsUpper;
                    }
                    else
                    {
                        output = prevVert.ShapeIndex == noncorresponding.ShapeIndex.Previous ? NonCorrespondingIsUpper == false : NonCorrespondingIsUpper;
                    }

                    return noncorresponding.ShapeIndex.IsInner ? !output : output;
            }
            else if (n.Z < 0)
            {
                CheckAgainstUpperPolygons = false;
                face_center = GetCentroid(f);
            }
            else //n.Z > 0
            {
                CheckAgainstUpperPolygons = true;
                face_center = GetCentroid(f);
            }

            if (CheckAgainstUpperPolygons == false)
            {
                if (this.LowerShapes.Any(p => p.GetRelation((IPoint2D)face_center) == ShapeRelation.Contained))
                    return false;

                return true;
            }
            else
            {
                if (this.UpperShapes.Any(p => p.GetRelation((IPoint2D)face_center) == ShapeRelation.Contained))
                    return false;

                return true;
            }
            /*
            MorphMeshVertex[] verts = this[f.iVerts].ToArray();

            //Vector2 face_center = GetCentroid(f);
            Vector2[] positions = verts.Select(v => v.Position.XY()).Distinct().ToArray();

            if (positions.Length < 3)
                return true; //Not implemented

            return positions.AreClockwise() == false;
            */
        }

        /// <summary>
        /// After tiling in the overlapped XY frame, move the verticies of every translated shape back so the mesh
        /// spans the annotators' original positions instead of the frame Bajaj needed.
        /// </summary>
        internal void RestoreVirtualOverlapTranslation()
        {
            Vector2[] offsets = Topology.VirtualOverlapOffsets;
            if (offsets is null)
                return;

            Vector2[] vertexOffsets = AttributeVirtualOverlapOffsetsToVerticies(offsets);

            foreach (MorphMeshVertex v in MorphVerticies)
            {
                Vector2 back = -vertexOffsets[v.Index];
                if (back == Vector2.Zero)
                    continue;

                v.Position = new Vector3(v.Position.X + back.X, v.Position.Y + back.Y, v.Position.Z);
            }

            for (int i = 0; i < offsets.Length; i++)
            {
                if (offsets[i] == Vector2.Zero)
                    continue;

                IShape2D restored = Shapes[i].Translate(-offsets[i]);
                Shapes[i] = restored;
                Topology.Shapes[i] = restored;
            }

            RestoreBandShapes(Topology.UpperShapes, UpperShapeIndicies, offsets);
            RestoreBandShapes(Topology.LowerShapes, LowerShapeIndicies, offsets);
        }

        /// <summary>
        /// UpperShapes and LowerShapes hold their own references to the shapes, so they have to be restored
        /// alongside the shape array they were copied from.
        /// </summary>
        private static void RestoreBandShapes(IShape2D[] bandShapes, ImmutableSortedSet<int> bandIndicies, Vector2[] offsets)
        {
            int[] shapeIndex = [.. bandIndicies];
            for (int j = 0; j < bandShapes.Length && j < shapeIndex.Length; j++)
            {
                Vector2 offset = offsets[shapeIndex[j]];
                if (offset != Vector2.Zero)
                    bandShapes[j] = bandShapes[j].Translate(-offset);
            }
        }

        /// <summary>
        /// Maps each vertex to the offset that must be undone.  Contour verticies carry the shape they came from,
        /// but cap and medial-axis verticies have no ShapeIndex, so they inherit the offset of the shape verticies
        /// they share a face with.  Inheritance is repeated until it stops spreading, because a medial-axis vertex
        /// deep inside a cap can be several faces away from any contour vertex.  Anything still unattributed stays
        /// where it is: leaving an interior vertex in the tiling frame is a lesser defect than pulling it toward a
        /// shape it does not belong to.
        /// </summary>
        private Vector2[] AttributeVirtualOverlapOffsetsToVerticies(Vector2[] offsets)
        {
            Vector2[] vertexOffsets = new Vector2[Vertices.Count];
            bool[] attributed = new bool[Vertices.Count];

            foreach (MorphMeshVertex v in MorphVerticies)
            {
                if (v.ShapeIndex is null)
                    continue;

                vertexOffsets[v.Index] = offsets[v.ShapeIndex.ShapeIndex];
                attributed[v.Index] = true;
            }

            bool spread = true;
            while (spread)
            {
                spread = false;

                //Collected for the whole sweep before anything is assigned, and resolved by lowest source vertex
                //index, so a vertex reachable from two shapes does not depend on face enumeration order.
                int[] source = new int[Vertices.Count];
                Array.Fill(source, -1);

                foreach (IFace f in Faces)
                {
                    int iSource = -1;
                    foreach (int iVert in f.iVerts)
                    {
                        if (attributed[iVert] && (iSource < 0 || iVert < iSource))
                            iSource = iVert;
                    }

                    if (iSource < 0)
                        continue;

                    foreach (int iVert in f.iVerts)
                    {
                        if (attributed[iVert] == false && (source[iVert] < 0 || iSource < source[iVert]))
                            source[iVert] = iSource;
                    }
                }

                for (int iVert = 0; iVert < source.Length; iVert++)
                {
                    if (source[iVert] < 0)
                        continue;

                    vertexOffsets[iVert] = vertexOffsets[source[iVert]];
                    attributed[iVert] = true;
                    spread = true;
                }
            }

            return vertexOffsets;
        }
    }
}
