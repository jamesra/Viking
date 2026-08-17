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
        /// Ensure every face has consistent, outward-facing winding so backface culling does not punch holes
        /// in the surface.  Faces are grouped into connected components (linked by shared edges).  Within each
        /// component the winding is propagated from a trusted seed so adjacent faces traverse their shared edge
        /// in opposite directions (a manifold-consistent orientation).  Trusted seeds are faces whose orientation
        /// is already known correct (medial-axis caps).  Components with no trusted seed are flipped as a unit so
        /// a representative cap face faces outward.
        ///
        /// This replaces the previous per-face heuristic, which decided each face independently and bailed out
        /// ("Not implemented") for the vertical side-wall faces, leaving neighboring faces wound inconsistently.
        /// </summary>
        public void EnsureFacesHaveExternalNormals()
        {
            int totalReversals = 0;
            int anchorConflicts = 0;
            int componentsFlipped = 0;

            HashSet<IFace> visited = [];

            foreach (MorphMeshFace start in this.MorphFaces.ToArray())
            {
                if (visited.Contains(start))
                    continue;

                //1. Discover the connected component (no winding changes yet) and collect its trusted anchors.
                List<MorphMeshFace> componentFaces = CollectConnectedComponent(start, visited, out List<MorphMeshFace> anchors);

                //2. Propagate consistent winding across the component, starting from every anchor at once.
                //Seeding from a single face meant propagation could reach an anchor only after already orienting
                //its neighbors the opposite way, and the anchor (which is never reversed) was then stranded in
                //conflict.  Placing all anchors up front leaves only anchor-versus-anchor disagreements, which are
                //genuinely ambiguous rather than an artifact of traversal order.
                List<MorphMeshFace> seeds = anchors.Count > 0 ? anchors : [componentFaces[0]];
                List<MorphMeshFace> component = PropagateWindingFromSeeds(seeds, ref totalReversals, ref anchorConflicts);

                //3. Flip the whole component if it points inward relative to annotation contours.
                if (OrientComponentOutward(component))
                    componentsFlipped++;
            }

            //4. Propagation cannot fix a component reached through a non-manifold junction, so sweep up whatever
            //pairs of faces still disagree across a shared edge.
            int repaired = MeshWindingReorientation.RepairManifoldConsistency(this);

            foreach (MorphMeshFace f in this.MorphFaces)
                f.NormalIsKnownCorrect = true;

            //An anchor conflict means two trusted faces disagree, so the component keeps inconsistent winding that
            //no later pass will resolve.
            if (anchorConflicts > 0)
                Trace.WriteLine($"{this}: winding propagation reversed {totalReversals} faces, flipped {componentsFlipped} components outward and repaired {repaired} more, but {anchorConflicts} trusted faces disagreed with another anchor.");
        }

        /// <summary>
        /// A face is a trusted orientation anchor if its normal was set correct at creation (e.g. region/cap faces)
        /// or it touches a medial-axis vertex (caps always point up or down and are oriented at creation).
        /// </summary>
        private bool IsAnchorFace(MorphMeshFace f) =>
            f.NormalIsKnownCorrect || this[f.iVerts].Any(v => v.MedialAxisIndex.HasValue);

        /// <summary>
        /// Flood the faces connected to <paramref name="start"/> via shared edges without modifying winding.
        /// </summary>
        /// <param name="anchors">Every trusted anchor face in the component, empty if it has none.</param>
        private List<MorphMeshFace> CollectConnectedComponent(MorphMeshFace start, HashSet<IFace> visited, out List<MorphMeshFace> anchors)
        {
            anchors = [];
            List<MorphMeshFace> component = [];
            Queue<MorphMeshFace> queue = new();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                MorphMeshFace f = queue.Dequeue();
                component.Add(f);
                if (IsAnchorFace(f))
                    anchors.Add(f);

                foreach (IEdgeKey ek in f.Edges)
                {
                    foreach (IFace nf in this.Edges[ek].Faces)
                    {
                        if (nf is not MorphMeshFace neighbor)
                            continue;
                        if (visited.Contains(neighbor))
                            continue;

                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return component;
        }

        /// <summary>
        /// Breadth-first traversal from every seed at once, reversing any neighbor whose winding disagrees with the
        /// placed face across their shared edge.  Trusted anchor faces are never reversed (their orientation wins).
        /// Returns the live face instances of the component after reorientation.
        /// </summary>
        private List<MorphMeshFace> PropagateWindingFromSeeds(List<MorphMeshFace> seeds, ref int totalReversals, ref int anchorConflicts)
        {
            List<MorphMeshFace> component = [];
            HashSet<IFace> placed = [.. seeds.Cast<IFace>()];
            Queue<MorphMeshFace> queue = new();
            foreach (MorphMeshFace seed in seeds)
                queue.Enqueue(seed);

            while (queue.Count > 0)
            {
                MorphMeshFace current = queue.Dequeue();
                component.Add(current);

                foreach (IEdgeKey ek in current.Edges)
                {
                    //Snapshot the edge's faces because ReverseFace mutates the edge to face map mid-loop.
                    IFace[] neighbors = [.. this.Edges[ek].Faces];
                    foreach (IFace nf in neighbors)
                    {
                        if (nf is not MorphMeshFace neighbor)
                            continue;
                        if (current.Equals(neighbor) || placed.Contains(neighbor))
                            continue;

                        bool currentForward = TraversesForward(current.iVerts, ek.A, ek.B);
                        bool neighborForward = TraversesForward(neighbor.iVerts, ek.A, ek.B);

                        //Consistent winding requires the two faces to traverse the shared edge in opposite directions.
                        if (currentForward == neighborForward)
                        {
                            if (IsAnchorFace(neighbor))
                            {
                                anchorConflicts++;
                            }
                            else
                            {
                                neighbor = (MorphMeshFace)this.ReverseFace(neighbor);
                                totalReversals++;
                            }
                        }

                        placed.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return component;
        }

        /// <summary>
        /// Returns true if the closed ring of vertex indicies traverses the directed edge a to b,
        /// false if it traverses b to a.
        /// </summary>
        private static bool TraversesForward(ImmutableArray<int> iVerts, int a, int b)
        {
            for (int i = 0; i < iVerts.Length; i++)
            {
                int x = iVerts[i];
                int y = iVerts[(i + 1) % iVerts.Length];
                if (x == a && y == b)
                    return true;
                if (x == b && y == a)
                    return false;
            }

            return false;
        }

        /// <summary>
        /// Flip an internally-consistent component as a unit so it faces outward.  Uses the up/down cap test on the
        /// most horizontal face (reliable and matching the renderer's CullCounterClockwiseFace convention).  For a
        /// capless/vertical component it falls back to signed volume: outward faces are clockwise-from-exterior under
        /// that convention, which yields negative signed volume, so a positive volume means the component is inverted.
        /// </summary>
        private bool OrientComponentOutward(List<MorphMeshFace> component)
        {
            if (component.Count == 0)
                return false;

            //A closed component encloses a volume, so the sign of that volume settles which way it faces without
            //relying on a representative face.  Outward faces are clockwise viewed from outside under the
            //renderer's culling convention, which gives a negative signed volume, so a positive one is inverted.
            if (IsComponentClosed(component))
            {
                if (ComponentSignedVolume(component) <= 0)
                    return false;

                foreach (MorphMeshFace f in component.ToArray())
                    this.ReverseFace(f);

                return true;
            }

            var ctx = MorphMeshOutwardOrientation.ShapeContext.FromSliceTopology(Topology);
            MorphMeshFace rep = null;
            double bestAbsZ = -1;
            foreach (MorphMeshFace f in component)
            {
                if (f.IsTriangle() == false)
                    continue;

                double absZ = Math.Abs(this.Normal(f).Z);
                if (absZ > bestAbsZ)
                {
                    bestAbsZ = absZ;
                    rep = f;
                }
            }

            IFace faceToTest = rep ?? component[0];
            if (MorphMeshOutwardOrientation.FaceNeedsFlipForOutward(this, faceToTest, ctx) == false)
                return false;

            foreach (MorphMeshFace f in component.ToArray())
                this.ReverseFace(f);
            return true;
        }

        /// <summary>
        /// True when every edge of the component is shared by exactly two of that component's own faces, so the
        /// component encloses a volume and its signed volume is meaningful.
        /// </summary>
        private bool IsComponentClosed(List<MorphMeshFace> component)
        {
            HashSet<IFace> componentFaces = [.. component.Cast<IFace>()];

            foreach (MorphMeshFace f in component)
            {
                foreach (IEdgeKey ek in f.Edges)
                {
                    if (this.Edges[ek].Faces.Count(componentFaces.Contains) != 2)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Signed volume of the component using the divergence theorem, fan-triangulating any non-triangular faces.
        /// </summary>
        private double ComponentSignedVolume(List<MorphMeshFace> component)
        {
            double sixV = 0;
            foreach (MorphMeshFace f in component)
            {
                MorphMeshVertex[] verts = [.. this[f.iVerts]];
                for (int i = 1; i + 1 < verts.Length; i++)
                {
                    Vector3 a = verts[0].Position;
                    Vector3 b = verts[i].Position;
                    Vector3 c = verts[i + 1].Position;
                    sixV += Vector3.Dot(a, Vector3.Cross(b, c));
                }
            }

            return sixV / 6.0;
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
    }
}
