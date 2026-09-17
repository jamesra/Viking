using AnnotationVizLib;
using Geometry;
using Geometry.Meshing;
using GraphLib;
using SqlGeometryUtils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace MorphologyMesh
{
    public class SliceChordRTree : RTree.RTree<MorphologyMesh.ISliceChord>
    {
        /// <summary>
        /// Advances each time a chord is inserted. Used to skip re-validating pairs that were already
        /// accepted under the same geometric tests while the tree was unchanged.
        /// </summary>
        public int Generation { get; private set; }

        public new void Add(RTree.Rectangle r, ISliceChord item)
        {
            base.Add(r, item);
            Generation++;
        }
    }

    /// <summary>
    /// Remembers the last successful <see cref="BajajMeshGenerator.IsSliceChordValid"/> result for a pair
    /// so <c>TryAddSliceChord</c> can avoid repeating permanent tests when only the RTree may have grown.
    /// </summary>
    sealed class LastValidChordCache
    {
        readonly Dictionary<(int Origin, int Target), (SliceChordTestType Tests, int Generation)> _valid = [];

        public void Record(int origin, int target, SliceChordTestType tests, int generation) =>
            _valid[(origin, target)] = (tests, generation);

        public bool TryGet(int origin, int target, out SliceChordTestType tests, out int generation)
        {
            if (_valid.TryGetValue((origin, target), out var entry))
            {
                tests = entry.Tests;
                generation = entry.Generation;
                return true;
            }

            tests = SliceChordTestType.None;
            generation = -1;
            return false;
        }

        public void Clear() => _valid.Clear();
    }

    /// <summary>
    /// Per-origin expanding k-NN ranking so OTV search can resume past known-bad targets instead of
    /// restarting at batch size 1 on every pass.
    /// </summary>
    sealed class NearestRankingCache
    {
        public sealed class Entry
        {
            public List<DistanceToPoint<MorphMeshVertex>> List;
            public int NextIndex;
            public int BatchSize;
            public int OppositeTreeCount;
            public int RTreeGeneration;
        }

        readonly Dictionary<int, Entry> _byOrigin = [];

        public bool TryGet(int originIndex, int oppositeCount, int rTreeGeneration, out Entry entry)
        {
            if (_byOrigin.TryGetValue(originIndex, out entry)
                && entry.OppositeTreeCount == oppositeCount
                && entry.RTreeGeneration == rTreeGeneration
                && entry.List is not null)
                return true;

            entry = null;
            return false;
        }

        public Entry GetOrCreate(int originIndex) 
        {
            if (!_byOrigin.TryGetValue(originIndex, out Entry entry))
            {
                entry = new Entry();
                _byOrigin[originIndex] = entry;
            }

            return entry;
        }

        public void InvalidateAll() => _byOrigin.Clear();

        public void InvalidateOrigin(int originIndex) => _byOrigin.Remove(originIndex);
    }

    /// <summary>
    /// Represents a quad tree for points in the above or below shape set for a mesh group
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct SliceTopologyQuadTrees<T>
    {
        public QuadTreeWithUniqueValues<T> Above;
        public QuadTreeWithUniqueValues<T> Below;

        public ImmutableArray<int> UpperPolyIndicies;
        public ImmutableArray<int> LowerPolyIndicies;

        public SliceTopologyQuadTrees(QuadTreeWithUniqueValues<T> aboveQuad, QuadTreeWithUniqueValues<T> belowQuad, IEnumerable<int> upperPolyIndicies, IEnumerable<int> lowerPolyIndicies)
        {
            Above = aboveQuad;
            Below = belowQuad;

            UpperPolyIndicies = [.. upperPolyIndicies];
            LowerPolyIndicies = [.. lowerPolyIndicies];
        }

        public SliceTopologyQuadTrees(QuadTreeWithUniqueValues<T> aboveQuad, QuadTreeWithUniqueValues<T> belowQuad, ImmutableArray<int> upperPolyIndicies, ImmutableArray<int> lowerPolyIndicies)
        {
            Above = aboveQuad;
            Below = belowQuad;

            UpperPolyIndicies = upperPolyIndicies;
            LowerPolyIndicies = lowerPolyIndicies;
        }

        /// <summary>
        /// Return the QuadTreeWithUniqueValues for the points on the opposite side of the polygon
        /// </summary>
        /// <param name="iPoly"></param>
        /// <returns></returns>
        public readonly QuadTreeWithUniqueValues<T> GetOppositeSide(int iPoly) => UpperPolyIndicies.Contains(iPoly) ? Below : Above;

    }


    public class OTVTable : System.Collections.Concurrent.ConcurrentDictionary<Geometry.IShapeIndex, Geometry.IShapeIndex> { }

    public enum CONTOUR_RELATION
    {
        Disjoint,
        Enclosure,
        Intersects
    }

    public enum ZDirection
    {
        Increasing,
        Decreasing
    }

    /// <summary>
    /// Flag enumeration indicating a set of tests.
    /// </summary>
    [Flags]
    public enum SliceChordTestType
    {
        /// <summary>
        /// No test flags set
        /// </summary>
        None = 0,
        /// <summary>
        /// Allow the chord if the endpoints share an X,Y position
        /// </summary>
        Correspondance = 1,
        /// <summary>
        /// Allow the chord if it does not intersect an existing chord
        /// </summary>
        ChordIntersection = 2,
        /// <summary>
        /// Allow the chord if the endpoints are on the correct side of the contours
        /// </summary>
        Theorem2 = 4,
        /// <summary>
        /// Allow if the chord is only entirely inside or outside the polygons but not both
        /// </summary>
        Theorem4 = 8,
        /// <summary>
        /// Allow the chord if the contours are not more than 90 degrees different in orientation
        /// </summary>
        LineOrientation = 16,
        /// <summary>
        /// Allow the chord if the edge is considered valid according to EdgeType criteria
        /// </summary>
        EdgeType = 32,
        /// <summary>
        /// Allow the chord if the chord will not intersect an existing face
        /// </summary>
        Face = 64,
        /// <summary>
        /// Allow the chord only if the annotator joined the two shapes with a LocationLink.
        /// Unlike the geometric tests this is never relaxed on a later pass: an unlinked pair is a fact about
        /// the annotation, not a heuristic that a looser pass might legitimately overrule.
        /// </summary>
        ShapeLink = 128,
        /// <summary>
        /// Allow the chord only if the vertex falls inside the range its forking polyline allocated to that partner.
        /// Separate from <see cref="ShapeLink"/> so the allocation can be relaxed while tuning without also
        /// re-enabling chords across pairs the annotator never linked.
        /// </summary>
        ForkPartition = 256
    }

    [Flags]
    public enum SliceChordPriority
    {
        Distance = 1, //Add chords shortest to longest
        Orientation = 2, //Add chords with the closest orientation of contours first
    }

    /// <summary>
    /// Stores the results for a single origin and all tested candidates
    /// </summary>
    public class SliceChordOriginTestResultsCache
    {
        readonly Dictionary<int, SliceChordTestType> KnownCandidateFailures = [];

        public SliceChordOriginTestResultsCache()
        {

        }

        public SliceChordTestType GetFailures(int Target, SliceChordTestType requested)
        {
            if (KnownCandidateFailures.TryGetValue(Target, out SliceChordTestType knownFailures))
            {
                return knownFailures & requested;
            }

            return SliceChordTestType.None;
        }

        public void RecordFailure(int Target, SliceChordTestType failures)
        {
            //Don't bother if nothing failed
            if (failures == SliceChordTestType.None)
            {
                return;
            }

            if (KnownCandidateFailures.TryGetValue(Target, out SliceChordTestType knownFailures))
                KnownCandidateFailures[Target] = failures | knownFailures;
            else
                KnownCandidateFailures.Add(Target, failures);
        }

        /// <summary>
        /// Removes a target vertex. 
        /// </summary>
        /// <param name="Origin"></param>
        public bool Remove(int Target) => KnownCandidateFailures.Remove(Target);

        /// <summary>
        /// Clear all results
        /// </summary>
        public void Clear() => KnownCandidateFailures.Clear();
    }

    public class SliceChordsTestResultsCache
    {
        /// <summary>
        /// Record failures.  First level is the origin, then the targets for that origin.
        /// </summary>
        private readonly Dictionary<int, SliceChordOriginTestResultsCache> Failures;

        public SliceChordsTestResultsCache()
        {
            Failures = [];
        }

        /// <summary>
        /// Given a set of tests, returns which tests are known to have failed
        /// </summary>
        /// <param name="candidate"></param>
        /// <param name="requested"></param>
        /// <returns></returns>
        public SliceChordOriginTestResultsCache GetFailuresForOrigin(int Origin)
        {
            if (Failures.TryGetValue(Origin, out SliceChordOriginTestResultsCache knownCandidates))
            {
                return knownCandidates;
            }
            else
            {
                SliceChordOriginTestResultsCache Obj = new();
                Failures.Add(Origin, Obj);
                return Obj;
            }
        }

        /// <summary>
        /// Given a set of tests, returns which tests are known to have failed
        /// </summary>
        /// <param name="candidate"></param>
        /// <param name="requested"></param>
        /// <returns></returns>
        public SliceChordTestType GetFailures(int Origin, int Target, SliceChordTestType requested) => Failures.TryGetValue(Origin, out var knownCandidates) == false ? SliceChordTestType.None : knownCandidates.GetFailures(Target, requested);

        /// <summary>
        /// Add the failure flag to the candidate slice chord between Origin and Target.  This can prevent retesting the same chord later.
        /// </summary>
        /// <param name="Origin"></param>
        /// <param name="Target"></param>
        /// <param name="failures"></param>
        public void RecordFailure(int Origin, int Target, SliceChordTestType failures)
        {
            //Don't bother if nothing failed
            if (failures == SliceChordTestType.None)
            {
                return;
            }


            if (false == Failures.TryGetValue(Origin, out SliceChordOriginTestResultsCache knownCandidates))
            {
                knownCandidates = new SliceChordOriginTestResultsCache();
                Failures.Add(Origin, knownCandidates);
            }

            knownCandidates.RecordFailure(Target, failures);
        }

        /// <summary>
        /// Removes a vertex.  This is done when we know the vertex is complete and no longer under consideration
        /// </summary>
        /// <param name="Origin"></param>
        public void Remove(int Origin) => Failures.Remove(Origin);

        /// <summary>
        /// Clear all results
        /// </summary>
        public void Clear() => Failures.Clear();
    }



    public static class BajajMeshGenerator
    {
        /// <summary>
        /// XY distance at which distinct mesh vertices become one Delaunay site.
        /// <see cref="Vector2.Equals(Vector2, Vector2)"/> / <see cref="Global.Epsilon"/> (0.001) is too tight:
        /// correspondence and midpoint insertion can leave a colinear triplet ~0.2 apart (span ~0.4), and
        /// divide-and-conquer Delaunay then adds a long edge through the middle vertex that intersects the
        /// tiny outer segment (<c>EdgesIntersectTriangulationException</c>, slice 620). Real contour spacing
        /// is much larger (pixels after volume scale). <c>Epsilon * 100</c> (medial-axis dedup) is still
        /// below a 0.2 step, so this is <c>Epsilon * 500</c>.
        /// </summary>
        internal const double DelaunayXyClusterDistance = Global.Epsilon * 500.0;

        /// <summary>
        /// Raised once for every slice in the graph.  <paramref name="mesh"/> is null when the slice produced no
        /// mesh at all.  Every slice reports, successfully or not, so a consumer assembling slices knows when it
        /// has heard from all of them.
        /// </summary>
        public delegate void OnMeshGeneratedEventHandler(Slice slice, BajajGeneratorMesh mesh, bool Success);

        /// <summary>
        /// Slice and mesh a morphology graph. Callers own registration correction (<c>--correction</c>); this
        /// path does not run curvefit/neighbor. Correspondence requires identical XY after any prior correction.
        /// </summary>
        public static async Task<List<BajajGeneratorMesh>> ConvertToMesh(MorphologyGraph graph, OnMeshGeneratedEventHandler OnMeshGenerated = null, bool smoothProcesses = false)
        {
            if (smoothProcesses)
                MorphologyGraph.CurveFitProcesses(graph);

            Trace.WriteLine("Begin Slice graph construction");
            SliceGraph sliceGraph = await SliceGraph.Create(graph, 2.0);
            Trace.WriteLine("End Slice graph construction");

            return await ConvertToMesh(sliceGraph, OnMeshGenerated);
        }

        /*
        /// <summary>
        /// Convert a morphology graph to an unprocessed mesh graph
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        public static List<BajajGeneratorMesh> ConvertToMesh(MorphologyGraph graph, OnMeshGeneratedEventHandler OnMeshGenerated = null)
        {
            Trace.WriteLine("Begin Slice graph construction");
            SliceGraph sliceGraph = SliceGraph.Create(graph, 2.0).Result;
            Trace.WriteLine("End Slice graph construction");

            return ConvertToMesh(sliceGraph, OnMeshGenerated);
        }
        */

        /// <summary>
        /// When true, face-generation workers emit Trace lines for routine per-slice progress. Failures always trace.
        /// </summary>
        public static bool VerboseLogging { get; set; }

        /// <summary>
        /// Convert every slice in <paramref name="sliceGraph"/> to a mesh. Topology must already be cached.
        /// Workers are bounded by <see cref="MeshParallelism.DegreeOfParallelism"/> so the thread pool is never
        /// parked behind a semaphore (the previous per-slice <c>Task.Run</c> + <c>Wait</c> injected thousands of
        /// blocked threads on a large cell).
        /// </summary>
        /// <param name="retainMeshes">
        /// When true, return every generated mesh sorted by Z. Whole-cell live assembly only needs
        /// <paramref name="OnMeshGenerated"/> and should leave this false so finished meshes are not pinned for
        /// the duration of the run.
        /// </param>
        public static async Task<List<BajajGeneratorMesh>> ConvertToMesh(
            SliceGraph sliceGraph,
            OnMeshGeneratedEventHandler OnMeshGenerated = null,
            bool retainMeshes = true)
        {
            using var _wall = MeshPhaseTimings.Measure(MeshPhase.FaceGenerationWall, sliceGraph.Nodes.Count);

            List<BajajGeneratorMesh> listBajajMeshGenerators = retainMeshes ? new(sliceGraph.Nodes.Count) : null;
            object listLock = retainMeshes ? new() : null;

            ParallelOptions options = new()
            {
                //Allow more scheduled partitions than slots; FaceSlots caps the actual concurrent GenerateFaces work
                //across every in-flight structure pipeline.
                MaxDegreeOfParallelism = MeshParallelism.DegreeOfParallelism * 2
            };

            //Longest-job-first: expensive contour pairs claim FaceSlots first so the serial tail is short cheap work.
            //Parallel.ForEachAsync drains the source in order into waiting workers; sort descending by topology verts.
            List<Slice> slicesByComplexity = [.. sliceGraph.Nodes.Values
                .OrderByDescending(s => EstimateSliceContourVertices(sliceGraph, s))];

            await Parallel.ForEachAsync(slicesByComplexity, options, async (capturedSlice, ct) =>
            {
                BajajGeneratorMesh mesh = null;
                bool success = false;
                Exception error = null;

                await MeshParallelism.RunWithFaceSlotAsync(_ =>
                {
                    try
                    {
                        SliceTopology topology = sliceGraph.GetTopology(capturedSlice);
                        if (topology.IsValid == false)
                        {
                            string sectionText = sliceGraph.FormatSectionNumbers(capturedSlice);
                            Trace.WriteLine($"Slice {capturedSlice.Key} produced no mesh: topology initialisation failed ({sectionText}).");
                            return ValueTask.CompletedTask;
                        }

                        mesh = new(topology, capturedSlice);
                        if (listBajajMeshGenerators is not null)
                        {
                            lock (listLock)
                                listBajajMeshGenerators.Add(mesh);
                        }

                        GenerateFaces(mesh);
                        success = !mesh.GenerationHadErrors;
                    }
                    catch (Exception e)
                    {
                        error = e;
                        Trace.WriteLine($"Slice {capturedSlice} produced no mesh:\n{e}");
                        sliceGraph.RecordFaceGenerationError(capturedSlice.Key, e);
                    }

                    return ValueTask.CompletedTask;
                }, ct).ConfigureAwait(false);

                //Notify after releasing the face slot so assembly merges do not hold a generation permit.
                if (error is not null || mesh is null)
                    OnMeshGenerated?.Invoke(capturedSlice, null, false);
                else
                    OnMeshGenerated?.Invoke(mesh.Slice, mesh, success);
            }).ConfigureAwait(false);

            if (listBajajMeshGenerators is null)
                return [];

            listBajajMeshGenerators.Sort(Comparer<BajajGeneratorMesh>.Create((a, b) => a.AverageZ.CompareTo(b.AverageZ)));
            return listBajajMeshGenerators;
        }

        /// <summary>
        /// Contour-vertex estimate used to schedule face generation longest-job-first. Uses cached topology after
        /// <see cref="SliceGraph.InitializeTopologyAsync"/>; invalid/missing topology sorts last (0).
        /// </summary>
        internal static int EstimateSliceContourVertices(SliceGraph sliceGraph, Slice slice)
        {
            SliceTopology topology = sliceGraph.GetTopology(slice);
            if (!topology.IsValid || topology.Shapes is null)
                return 0;

            int total = 0;
            foreach (IShape2D shape in topology.Shapes)
            {
                total += shape switch
                {
                    Polygon poly => poly.TotalUniqueVertices,
                    Polyline line => line.NumUniqueVertices,
                    IHasControlPoints pts => pts.ControlPoints.Count,
                    _ => 0
                };
            }

            return total;
        }

        /// <summary>
        /// Tile one slice.  An exclusive linked CIRCLE pair lofts matching samples; a slice with any other polygon
        /// takes the Bajaj polygon path (regions, medial-axis closing); a polyline-only slice is a ruled ribbon.
        /// All three end in <see cref="FinishSliceMesh"/> for virtual-overlap restore, caps, normals, and validation.
        /// </summary>
        public static void GenerateFaces(BajajGeneratorMesh mesh)
        {
            long t0 = MeshPhaseTimings.Enabled ? Stopwatch.GetTimestamp() : 0;
            int chordPassCalls = 0;
            int chordsAdded = 0;
            using var _phase = MeshPhaseTimings.Measure(MeshPhase.FaceGeneration, mesh.Vertices.Count);

            int singleTrianglePolylinePairs = 0;
            if (mesh.UpperShapeIndicies.Count == 0 || mesh.LowerShapeIndicies.Count == 0)
            {
                //An isolated annotation is split into a slice below it and a slice above it, each holding the one
                //contour on a single band.  There is nothing to tile to, and running the polygon path anyway treated
                //the lone contour as an untiled region and filled it flat at the contour Z, so a single circle came
                //out as a zero-thickness disc with the two slices' fills lying back to back.  Only the cap applies.
            }
            else if (CirclePairMeshGenerator.TryGenerateFaces(mesh))
            {
            }
            else if (mesh.HasPolygonShapes)
            {
                GeneratePolygonFaces(mesh, out chordPassCalls, out chordsAdded);
            }
            else
            {
                singleTrianglePolylinePairs = PolylineRibbonMeshGenerator.GenerateRibbonFaces(mesh);
            }

            FinishSliceMesh(mesh, singleTrianglePolylinePairs);

            if (t0 == 0)
                return;

            double seconds = (Stopwatch.GetTimestamp() - t0) / (double)Stopwatch.Frequency;
            if (seconds < MeshPhaseTimings.SlowSliceThresholdSeconds)
                return;

            string chordStats = ChordGenStats.Enabled ? $" {ChordGenStats.Format()}" : "";
            MeshPhaseTimings.RecordSlowSlice(
                $"slow FaceGeneration {seconds:F1}s verts={mesh.Vertices.Count} faces={mesh.Faces.Count} chordPassCalls={chordPassCalls} chordsAdded={chordsAdded} {DescribeSlice(mesh)}{chordStats}");
        }

        static string DescribeSlice(BajajGeneratorMesh mesh)
        {
            if (mesh.Slice is null)
                return "slice=(none)";

            return $"key={mesh.Slice.Key} locations={string.Join(",", mesh.Slice.AllNodes)}";
        }

        /// <summary>
        /// Bajaj 1996 / Edwards 2011 tiling for closed contours: Delaunay, region graph, untiled-region closing via
        /// the medial axis, then OTV slice chords and face generation in two passes.  Polylines never reach here as
        /// tileable shapes; SliceGraph keeps them correspondence-only when the slice has a polygon.
        /// </summary>
        private static void GeneratePolygonFaces(BajajGeneratorMesh mesh, out int chordPassCalls, out int chordsAdded)
        {
            int vertCount = mesh.Vertices.Count;
            chordPassCalls = 0;
            chordsAdded = 0;

            using (MeshPhaseTimings.Measure(MeshPhase.Delaunay, vertCount))
                AddDelaunayEdges(mesh);

            MorphMeshRegionGraph RegionPairingGraph;
            using (MeshPhaseTimings.Measure(MeshPhase.RegionGraphBuild, vertCount))
            {
                RegionPairingGraph = GenerateRegionGraph(mesh);
                mesh.RemoveInvalidEdges();
                CompleteCorrespondingVertexFaces(mesh);
            }

            SliceChordRTree rTree = mesh.CreateChordTree(mesh.ShapeZ);
            using (MeshPhaseTimings.Measure(MeshPhase.RegionClosing, vertCount))
            {
                List<OTVTable> listOTVTables = RegionPairingGraph.MergeAndCloseRegionsPass(mesh, rTree);
            }

            var IncompleteVerticies = IdentifyIncompleteVerticies(mesh);

            List<MorphMeshVertex> FirstPassIncompleteVerticies;
            using (MeshPhaseTimings.Measure(MeshPhase.ChordGeneration, vertCount))
                FirstPassIncompleteVerticies = FirstPassSliceChordGeneration(mesh, mesh.ShapeZ, out chordPassCalls, out chordsAdded, rTree);

            using (MeshPhaseTimings.Measure(MeshPhase.FaceClosing, vertCount))
                BajajMeshGenerator.FirstPassFaceGeneration(mesh);

            try
            {
                MorphMeshRegionGraph SecondPassRegions;
                using (MeshPhaseTimings.Measure(MeshPhase.SecondPassRegionDetection, vertCount))
                    SecondPassRegions = MorphRenderMesh.SecondPassRegionDetection(mesh, FirstPassIncompleteVerticies);

                using (MeshPhaseTimings.Measure(MeshPhase.RegionClosing, vertCount))
                    SecondPassRegions.MergeAndCloseRegionsPass(mesh, rTree);
            }
            catch (Exception e)
            {
                mesh.RecordGenerationError($"second pass region closing threw {e.GetType().Name}: {e.Message}");
                Trace.WriteLine(string.Format("Exception building mesh {0}\n{1}", mesh.ToString(), e));
            }

            using (MeshPhaseTimings.Measure(MeshPhase.FaceClosing, vertCount))
                BajajMeshGenerator.FirstPassFaceGeneration(mesh);
        }

        /// <summary>
        /// Steps shared by the polygon, polyline, and circle-pair paths once the band between sections has faces.
        /// Virtual overlap is undone before caps so <c>CapCircleEnd</c> rays from the annotation-space centre through
        /// contour verts that already sit there, rather than through a stacked tiling frame.
        /// </summary>
        /// <param name="singleTrianglePolylinePairs">Sliver count from the ribbon path; zero for polygons and circle pairs.</param>
        internal static void FinishSliceMesh(BajajGeneratorMesh mesh, int singleTrianglePolylinePairs)
        {
            mesh.RestoreVirtualOverlapTranslation();

            if (mesh.Slice != null)
            {

                if (mesh.Slice.HasSliceAbove == false)
                    mesh.CapMeshEnd(true);

                if (mesh.Slice.HasSliceBelow == false)
                    mesh.CapMeshEnd(false);

            }

            mesh.EnsureFacesHaveExternalNormals();

            mesh.RecalculateNormals();

            using (MeshPhaseTimings.Measure(MeshPhase.ManifoldValidate, mesh.Faces.Count))
                mesh.ManifoldReport = MeshManifoldValidator.Validate(mesh, mesh.IsForkGapBoundaryEdge, singleTrianglePolylinePairs, mesh.IsRibbonBoundaryEdge);

            if (mesh.ManifoldReport.IsValidSliceSurface == false)
            {
                //The report itself is the reason here; the assembly planner already prints it for the slice.
                mesh.GenerationHadErrors = true;
                if (VerboseLogging)
                    Trace.WriteLine($"Mesh {mesh} is not a valid slice surface: {mesh.ManifoldReport}");
                else
                    Trace.WriteLine($"Mesh slice {mesh.Slice?.Key} is not a valid slice surface: {mesh.ManifoldReport}");
            }

            //LocationLinked cross-band pairs with no spanning face are a failure even when the manifold report is clean.
            mesh.CheckLinkedPairsHaveFaces();
            if (mesh.HasUntiledLinkedPairs)
            {
                if (VerboseLogging)
                    Trace.WriteLine($"Mesh {mesh} has untiled linked pair(s): {string.Join("; ", mesh.GenerationErrors.Where(e => e.StartsWith("untiled linked")))}");
                else
                    Trace.WriteLine($"Mesh slice {mesh.Slice?.Key} has untiled linked pair(s).");
            }
        }

        /// <summary>
        /// Groups mesh vertices that share an XY Delaunay site. Corresponding vertices (identical XY, different Z)
        /// collapse via <see cref="Vector2"/> equality; remaining near-duplicates within
        /// <see cref="DelaunayXyClusterDistance"/> are clustered so the triangulator does not see a degenerate
        /// colinear micro-edge.
        /// </summary>
        internal static Dictionary<Vector2, List<int>> CreatePointToIndexMap(BajajGeneratorMesh mesh)
        {
            Dictionary<Vector2, List<int>> exact = new(mesh.Vertices.Count);
            foreach (MorphMeshVertex v in mesh.Vertices)
            {
                Vector2 p = v.Position.XY();
                if (exact.TryGetValue(p, out List<int> list))
                    list.Add(v.Index);
                else
                    exact.Add(p, [v.Index]);
            }

            return ClusterNearDuplicateXySites(exact, DelaunayXyClusterDistance);
        }

        /// <summary>
        /// Union-find merge of XY keys closer than <paramref name="mergeDistance"/>. Each cluster keeps the
        /// first site's coordinates and concatenates mesh-index lists (same representation as corresponding verts).
        /// </summary>
        internal static Dictionary<Vector2, List<int>> ClusterNearDuplicateXySites(
            Dictionary<Vector2, List<int>> exactSites, double mergeDistance)
        {
            if (exactSites.Count < 2 || mergeDistance <= 0)
                return exactSites;

            Vector2[] sites = [.. exactSites.Keys];
            int n = sites.Length;
            double mergeDistSq = mergeDistance * mergeDistance;
            int[] parent = new int[n];
            for (int i = 0; i < n; i++)
                parent[i] = i;

            int Find(int i)
            {
                while (parent[i] != i)
                {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }

                return i;
            }

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (Vector2.DistanceSquared(in sites[i], in sites[j]) > mergeDistSq)
                        continue;

                    int a = Find(i);
                    int b = Find(j);
                    if (a != b)
                        parent[b] = a;
                }
            }

            Dictionary<int, List<int>> merged = [];
            for (int i = 0; i < n; i++)
            {
                int root = Find(i);
                if (!merged.TryGetValue(root, out List<int> list))
                {
                    list = [];
                    merged[root] = list;
                }

                list.AddRange(exactSites[sites[i]]);
            }

            Dictionary<Vector2, List<int>> result = new(merged.Count);
            foreach (KeyValuePair<int, List<int>> kvp in merged)
                result.Add(sites[kvp.Key], kvp.Value);

            return result;
        }

        public static void AddDelaunayEdges(BajajGeneratorMesh mesh, TriangulationMesh<Vertex2D<List<int>>>.ProgressUpdate OnProgress = null)
        {
            Geometry.Meshing.TriangulationMesh<Vertex2D<List<int>>> triMesh = null;

            //Create a map of the verticies present at each point, we expect one vertex usually, but two verticies for corresponding verticies
            //Then use the keys of the dictionary to create a Vertex2D array that we'll triangulate.  Once the triangulation is done
            //feed the existing contour edges into that triangulation as constraints.  Then add the faces to the passed mesh and classify the 
            //edges created for those faces.

            Dictionary<Vector2, List<int>> pointToIndexMap = CreatePointToIndexMap(mesh);

            Dictionary<int, int> MeshToTriMesh = new(mesh.Vertices.Count);
            Dictionary<int, List<int>> TriMeshToMesh = new(mesh.Vertices.Count);

            int n = pointToIndexMap.Count;
            Vector2[] points = new Vector2[n];
            Vector2[] translated_points = new Vector2[n];
            int iPoint = 0;
            Vector2 sum = default;
            foreach (Vector2 p in pointToIndexMap.Keys)
            {
                points[iPoint++] = p;
                sum += p;
            }

            Vector2 avg = n > 0 ? sum / n : default;
            for (int i = 0; i < n; i++)
                translated_points[i] = points[i] - avg;

            var verts = TriangulateSitesWithRetry(translated_points, points, pointToIndexMap, OnProgress, out triMesh);

            foreach (var v in verts)
            {
                List<int> listIndicies = v.Data;//pointToIndexMap[v.Position];
                foreach (int i in listIndicies)
                {
                    MeshToTriMesh[i] = v.Index; //Map the mesh vertex ID to the vertex ID in the triangulation.  This can be a many to one mapping for corresponding verticies.
                }

                TriMeshToMesh[v.Index] = listIndicies; //Map the triangulations vertex ID to the mesh verticies.  This is a one to many mapping for corresponding verticies
            }

            var ContourEdges = mesh.MorphEdges.Where(e => e.Type == EdgeType.CONTOUR);
            foreach (var edge in ContourEdges)
            {
                int A = MeshToTriMesh[edge.A];
                int B = MeshToTriMesh[edge.B];
                if (A == B)
                    continue;

                triMesh.AddConstrainedEdge(new Geometry.Meshing.ConstrainedEdge(A, B), OnProgress);
            }

            foreach (IFace f in triMesh.Faces)
            {
                List<int> A_List = TriMeshToMesh[f.iVerts[0]];
                List<int> B_List = TriMeshToMesh[f.iVerts[1]];
                List<int> C_List = TriMeshToMesh[f.iVerts[2]];

                if (A_List.Count == 1 && B_List.Count == 1 && C_List.Count == 1)
                {
                    MorphMeshFace mesh_face = new(A_List[0], B_List[0], C_List[0]);

                    //The triangulator only sees XY, so it happily spans shapes the annotator never linked.  These
                    //faces are committed before any edge classification runs, so this is the only place to stop
                    //them; a rejected triangle leaves the region open for the normal chord and region passes.
                    if (FaceRespectsShapeLinks(mesh, mesh_face))
                        mesh.AddFace(mesh_face);
                }
                else
                {
                    /* Adding edges instead of faces seems like a good idea, but it causes a lot of issues with extra, incorrect, and missing faces on corresponding verticies even though it solves some cases*/
                    /*
                    //Add the edges, but not the face
                    foreach(var combo in A_List.CombinationPairs(B_List))
                    {
                        if(mesh.Contains(combo.A, combo.B) == false)
                        {
                            MorphMeshEdge edge = new MorphMeshEdge(EdgeType.UNKNOWN, combo.A, combo.B);
                            mesh.AddEdge(edge);
                        }
                    }

                    foreach (var combo in B_List.CombinationPairs(C_List))
                    {
                        if (mesh.Contains(combo.A, combo.B) == false)
                        {
                            MorphMeshEdge edge = new MorphMeshEdge(EdgeType.UNKNOWN, combo.A, combo.B);
                            mesh.AddEdge(edge);
                        }
                    }

                    foreach (var combo in C_List.CombinationPairs(A_List))
                    {
                        if (mesh.Contains(combo.A, combo.B) == false)
                        {
                            MorphMeshEdge edge = new MorphMeshEdge(EdgeType.UNKNOWN, combo.A, combo.B);
                            mesh.AddEdge(edge);
                        }
                    }
                    */
                }
            }

            //For corresponding verticies, we'll create edges where
            //int[] triMeshCorrespondingVerts = TriMeshToMesh.Where(item => item.Value.Count > 1).Select(item => item.Key).ToArray();

            mesh.ClassifyMeshEdges();
            //BajajGeneratorMesh.AddTriangulationEdgesToMesh(triMesh, mesh);
        }

        /// <summary>
        /// Largest perturbation, in nm, applied to triangulation sites when the divide-and-conquer Delaunay pass
        /// fails on degenerate input.  Far below annotation precision, so the returned connectivity is still a
        /// valid triangulation of the real contour positions.
        /// </summary>
        private const double DelaunayRetryJitter = 1e-3;

        private const int DelaunayRetryAttempts = 4;

        /// <summary>
        /// Triangulates the site set, retrying with a small deterministic offset on each site when the
        /// divide-and-conquer merge fails.  Merge failures come from near-colinear triples that the contour
        /// simplifier left behind (RPC1 365043/365415: three contour vertices within 0.005 nm of one line over a
        /// 50 nm span), where the circumcircle test cannot separate the candidates.  Nudging the sites off that
        /// line breaks the tie; the mesh keeps its unperturbed vertex positions because only face connectivity is
        /// read back from the triangulation.
        /// </summary>
        private static Vertex2D<List<int>>[] TriangulateSitesWithRetry(Vector2[] translatedPoints, Vector2[] originalPoints,
            Dictionary<Vector2, List<int>> pointToIndexMap, TriangulationMesh<Vertex2D<List<int>>>.ProgressUpdate OnProgress,
            out TriangulationMesh<Vertex2D<List<int>>> triMesh)
        {
            int n = translatedPoints.Length;
            Vertex2D<List<int>>[] verts = new Vertex2D<List<int>>[n];
            Vector2[] jittered = null;
            for (int attempt = 0; ; attempt++)
            {
                Vector2[] sites = translatedPoints;
                if (attempt > 0)
                {
                    //The offset pattern is a function of index only so a slice meshes identically from run to run.
                    jittered ??= new Vector2[n];
                    double scale = DelaunayRetryJitter * attempt;
                    for (int i = 0; i < n; i++)
                    {
                        double phase = (i * 0.6180339887498949) % 1.0 * Math.PI * 2.0;
                        jittered[i] = translatedPoints[i] + new Vector2(Math.Cos(phase) * scale, Math.Sin(phase) * scale);
                    }

                    sites = jittered;
                }

                for (int i = 0; i < n; i++)
                    verts[i] = new Vertex2D<List<int>>(sites[i], pointToIndexMap[originalPoints[i]]);
                try
                {
                    triMesh = Geometry.GenericDelaunayMeshGenerator2D<Vertex2D<List<int>>>.TriangulateToMesh(verts, OnProgress);
                    if (attempt > 0)
                        Trace.WriteLine($"AddDelaunayEdges: triangulation succeeded on retry {attempt} after perturbing sites by {DelaunayRetryJitter * attempt:F4} nm");

                    return verts;
                }
                //ArgumentException is the triangulator's own report of a degenerate flip ("Edge cannot flip unless it
                //has two triangular faces", RPC1 133586/133601 in the full-cell run); it is the same near-colinear
                //input as the typed exceptions and responds to the same nudge.
                catch (Exception e) when (attempt < DelaunayRetryAttempts && (e is GeometryMeshExceptionBase || e is ArgumentException))
                {
                    Trace.WriteLine($"AddDelaunayEdges: triangulation attempt {attempt + 1} failed ({e.GetType().Name}: {e.Message}); retrying with perturbed sites");
                }
            }
        }

        /// <summary>
        /// True when the face respects the annotation topology: every pair of shapes it touches is joined by a
        /// LocationLink, and no pair crosses a fork gap.  A mesh with neither link data nor a fork partition allows
        /// everything, which keeps hand-built topologies working.
        ///
        /// Always rejects Delaunay faces that lie entirely on a single <see cref="Polyline"/>: contour
        /// triangulation may fill polygons, but polyline interiors (including accidentally closed LINESTRINGs
        /// and CLOSEDCURVE rings) are not part of the shape.
        /// </summary>
        private static bool FaceRespectsShapeLinks(BajajGeneratorMesh mesh, MorphMeshFace face)
        {
            if (IsSamePolylineFace(mesh, face))
                return false;

            Func<int, int, bool> isLinked = mesh.ShapeLinkPredicate;
            PolylineForkPartition forkPartition = mesh.ForkPartition;
            if (isLinked is null && forkPartition is null)
                return true;

            ImmutableArray<int> iVerts = face.iVerts;
            for (int a = 0; a < iVerts.Length; a++)
            {
                IShapeIndex indexA = mesh[iVerts[a]].ShapeIndex;
                if (indexA is null)
                    continue;

                for (int b = a + 1; b < iVerts.Length; b++)
                {
                    IShapeIndex indexB = mesh[iVerts[b]].ShapeIndex;
                    if (indexB is null)
                        continue;

                    if (indexA.ShapeIndex == indexB.ShapeIndex)
                        continue;

                    if (isLinked is not null && isLinked(indexA.ShapeIndex, indexB.ShapeIndex) == false)
                        return false;

                    if (forkPartition is not null
                        && (forkPartition.AllowsChord(indexA.ShapeIndex, indexA.VertexIndex, indexB.ShapeIndex) == false
                         || forkPartition.AllowsChord(indexB.ShapeIndex, indexB.VertexIndex, indexA.ShapeIndex) == false))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// True when every annotated vertex of the face belongs to the same polyline shape.
        /// </summary>
        private static bool IsSamePolylineFace(BajajGeneratorMesh mesh, MorphMeshFace face)
        {
            ImmutableArray<int> iVerts = face.iVerts;
            int? shapeId = null;
            for (int i = 0; i < iVerts.Length; i++)
            {
                IShapeIndex index = mesh[iVerts[i]].ShapeIndex;
                if (index is null)
                    continue;

                if (shapeId is null)
                    shapeId = index.ShapeIndex;
                else if (shapeId.Value != index.ShapeIndex)
                    return false;
            }

            if (shapeId is null)
                return false;

            int id = shapeId.Value;
            return id >= 0 && id < mesh.Shapes.Length && mesh.Shapes[id] is Polyline;
        }

        /*
        /// <summary>
        /// Add all edges from a delaunay triangulation to the mesh which are valid
        /// </summary>
        /// <param name="mesh"></param>
        public static void AddDelaunayEdges(BajajGeneratorMesh mesh)
        {
            IMesh triMesh = mesh.Polygons.Triangulate();

            BajajMeshGenerator.AddTriangulationEdgesToMesh(triMesh, mesh);

            mesh.ClassifyMeshEdges();
        }
        */

        public static MorphMeshRegionGraph GenerateRegionGraph(BajajGeneratorMesh mesh)
        {
            //Identify our trouble areas. 
            mesh.IdentifyRegionsViaFaces();

            //Identify probable mappings between regions
            MorphMeshRegionGraph RegionPairingGraph = GenerateRegionConnectionGraph(mesh);

            //Remove invalid edges
            //RemoveInvalidEdges(mesh);

            //Close the nodes with no edges
            //CloseRegionsFirstPass(mesh, RegionPairingGraph.Nodes.Values.Where(v => v.Edges.Count == 0).Select(v => v.Key).ToList());
            /*
            List<MorphMeshRegion> regions = RegionPairingGraph.Nodes.Where(n => n.Value.Edges.Count == 0).Select(n => n.Key).ToList();
            foreach(MorphMeshRegion unconnectedRegion in regions)
            {
                RegionPairingGraph.RemoveNode(unconnectedRegion);
            }
            */

            return RegionPairingGraph;
        }

        /// <summary>
        /// Create edges in our mesh based on a triangulation.  These edges will be categorized later and some discarded.
        /// </summary>
        /// <param name="triMesh"></param>
        /// <param name="output"></param>
        /*
        public static void AddTriangulationEdgesToMesh(IMesh2D<IVertex2D> triMesh, MorphRenderMesh output)
        {
            var pointToPoly = Polygon.CreatePointToPolyMap(output.Shapes.Select(p => p as Polygon).ToArray());

            Vector2[] vertArray = triMesh.Vertices.Select(v => new Vector2(v.Position.X, v.Position.Y)).ToArray();
            Dictionary<int, int[]> TriIndexToMeshIndex = new Dictionary<int, int[]>();

            //SortedList<MorphMeshVertex, MorphMeshVertex> CorrespondingVerticies = new SortedList<MorphMeshVertex, MorphMeshVertex>();

            double[] PolyZ = output.ShapeZ;
            
            */

        /*Ensure all triangulation points are in the mesh*/

        /*
        for (int iVert = 0; iVert < vertArray.Length; iVert++)
        {
            Vector2 vert = vertArray[iVert];
            List<PolygonIndex> listPointIndicies = pointToPoly[vert];

            double[] PointZs = listPointIndicies.Select(p => PolyZ[p.ShapeIndex]).ToArray();

            PolygonIndex pIndex = listPointIndicies[0];
            Vector3 vert3 = vert.ToVector3(PolyZ[pIndex.ShapeIndex]);

            MorphMeshVertex meshVertex = output.GetOrAddVertex(pIndex, vert3);

            TriIndexToMeshIndex[iVert] = new int[] { meshVertex.Index };

            if (listPointIndicies.Count > 1)
            {
                //We have a CORRESPONDING pair on two sections
                //We need to add these later or they mess up our indexing for faces
                List<int> meshIndicies = new List<int>
                {
                    meshVertex.Index
                };
                for (int i = 1; i < listPointIndicies.Count; i++)
                {
                    PolygonIndex pOtherIndex = listPointIndicies[i];
                    if (pIndex.ShapeIndex == pOtherIndex.ShapeIndex)
                        continue;

                    Vector3 otherVert3 = vert.ToVector3(PolyZ[pOtherIndex.ShapeIndex]);
                    MorphMeshVertex correspondingVertex = output.GetOrAddVertex(pOtherIndex, otherVert3);
                    //CorrespondingVerticies[meshVertex] = correspondingVertex;
                    meshIndicies.Add(correspondingVertex.Index);
                }

                TriIndexToMeshIndex[iVert] = meshIndicies.ToArray();
            }
        }

        //Because we took verticies from mesh the indicies should line up
        foreach (TriangleNet.Topology.Triangle tri in triMesh.Triangles)
        {
            int[] tri_face = new int[] { tri.GetVertexID(0), tri.GetVertexID(1), tri.GetVertexID(2) };
            int[] face = tri_face.SelectMany(f => TriIndexToMeshIndex[f]).ToArray();

            //Here we need to check for a corresponding edge being involved.  If we don't we can get an edge that should not exist in the mesh that face generation can follow to produce an incorrect mesh
            //A corresponding edge will have two vertex entries in the table, so we check for four or more verticies in the face to go down this special path
            if (face.Length > 4)
            {
                continue;
                //throw new NotImplementedException("Unexpected number of faces for Delaunay Triangulation conversion to mesh.  Expected each face to have three edges.");
            }
            else if (face.Length == 4)
            {
                */
        /*
        This code does generate faces around a corresponding vertex.  However the bajaj code that executes later produces smoother faces around corresponding points so I
        do not generate faces for triangles that contain corresponding verticies.
        */
        /***************

        //We need to make sure the face isn't twisted
        List<int> sortedFace = new List<int>(4);
        int[] correspondingEdge = tri_face.Where(f => TriIndexToMeshIndex[f].Length > 1).SelectMany(f => TriIndexToMeshIndex[f]).ToArray();
        System.Diagnostics.Debug.Assert(correspondingEdge.Length == 2); //I only wrote this for the case of a single corresponding edge.  While possible in theory, the multiple case should not occur in practice

        EdgeKey correspondingEdgeKey = new EdgeKey(correspondingEdge[0], correspondingEdge[1]);

        //Once we add two faces to the edge we are done
        if (output[correspondingEdgeKey].Faces.Count == 2)
            continue;

        MorphMeshVertex[] CorrespondingVerts = new MorphMeshVertex[] { output.GetVertex(correspondingEdge[0]), output.GetVertex(correspondingEdge[1]) }.OrderBy(v => v.Position.Z).ToArray();
        MorphMeshVertex[] OtherVerts = face.Where(f => f != correspondingEdgeKey.A && f != correspondingEdgeKey.B).Select(f => output.GetVertex(f)).OrderBy(f => f.Position.Z).ToArray();

        int[] vertsA = new int[] { CorrespondingVerts[0].Index, CorrespondingVerts[1].Index, OtherVerts[0].Index };
        int[] vertsB = new int[] { OtherVerts[0].Index, CorrespondingVerts[1].Index, OtherVerts[1].Index };

        MorphMeshFace FaceA = new MorphMeshFace(vertsA);
        MorphMeshFace FaceB = new MorphMeshFace(vertsB);

        //output.SplitFace(quadFace);
        output.AddFace(FaceA);
        output.AddFace(FaceB);
        *******************/

        /*

    }
    else
    {
        Vector2[] verts = tri_face.Select(f => vertArray[f]).ToArray();

        if (verts.AreClockwise())
        {
            output.AddFace(new MorphMeshFace(face[1], face[0], face[2]));
        }
        else
        {
            output.AddFace(new MorphMeshFace(face));
        }
    }
}

return;
}
*/

        /*
        /// <summary>
        /// This is a specialized criteria function that quickly checks for faces for corresponding verticies.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="checkedEdges"></param>
        /// <param name="current"></param>
        /// <param name="candidate"></param>
        /// <returns></returns>
        private static bool CorrespondingVertexCloseableFaceCriteriaFunction(Stack<int> path, SortedSet<IEdgeKey> checkedEdges, IVertex current, IEdgeKey candidate)
        {

        }
        */

        /// <summary>
        /// We need to handle the case where a single vertex is on the other side of the contour boundary and creates
        /// two corresponding vertices which are tightly grouped.
        /// 
        //       3
        ///     / \
        /// A--2-B-4--C
        ///   /     \
        ///  1       5
        ///  
        /// This should only be called after the mesh is created when we know there are no faces for edges
        /// </summary>
        public static void CompleteAdjacentCorrespondingVertexFaces(MorphRenderMesh mesh)
        {
            //Identify any verticies who have a corresponding vertex previous and after thier position
            /*
            var polyEnum = new PolySetVertexEnum(mesh.Shapes);
            foreach (PolygonIndex pIndex in polyEnum)
            {
                MorphMeshVertex vert = mesh[pIndex];

                if (vert.Corresponding.HasValue)
                    continue;
            }
            */
        }

        public static void CompleteCorrespondingVertexFaces(MorphRenderMesh mesh)
        {
            //Corresponding edges should have two faces if they are complete

            MorphMeshEdge[] edges = [.. mesh.MorphEdges.Where(e => e.Type == EdgeType.CORRESPONDING && e.Faces.Count < 2)];

            foreach (MorphMeshEdge edge in edges)
            {
                MorphMeshVertex vA = mesh[edge.A];
                MorphMeshVertex vB = mesh[edge.B];

                //MorphMeshVertex vUpper = mesh.IsUpperShape[vA.PolyIndex.Value.ShapeIndex] ? vA : vB;
                //MorphMeshVertex vLower = vUpper == vA ? vB : vA;

                List<MorphMeshVertex> VertsToCheck = [vA, vB];

                //TODO: I probably don't need the where statement below because I know the vertex is not face complete because the attached corresponding edge is not complete
                //I also should probably collect all of the possible faces, then select the option with the smallest perimeter. 
                foreach (MorphMeshVertex v in VertsToCheck.Where(vT => !vT.IsFaceSurfaceComplete(mesh)))
                {
                    if (edge.Faces.Count == 2)
                        break;

                    List<int> Face = null;
                    Face = mesh.FindAnyCloseableFace(vA.Index, vB, edge, MaxPathLength: 4);

                    //Check for an existing pathway for a face, if it exists, use it to be consistent with the model
                    if (Face?.Count <= 4)
                    {
                        MorphMeshFace face = new(Face);
                        //mesh.AddFace(face);  //Split face will add the faces, so there is no need to add before we split

                        if (Face.Count == 4)
                        {
                            mesh.SplitFace(face);
                        }
                    }
                    else //Face is null, so there isn't an obvious mapping
                    {
                        //We cannot count on the order of the verticies returned in Face. 
                        //If we want to get correct CCW winding it takes extra work
                        int iVA = v.Index;
                        int iVB = v.Corresponding.Value;

                        //int iVLower = mesh.IsUpperShape[vA.PolyIndex.Value.ShapeIndex] ? iVB : iVA;
                        //int iVUpper = iVLower == iVB ? iVA : iVB;

                        if (v.ShapeIndex is null || mesh[iVB].ShapeIndex is null)
                            break;

                        int nFacesFound = TryAddCorrespondingNeighborFaces(mesh, v.ShapeIndex, mesh[iVB].ShapeIndex, iVA, iVB);

                        //Once in a while there are not two valid edges to complete the face.
                        //TODO: This case would be better handled by triangulating verticies contained in the face.  It would solve some of the known failures in mesh generation.
                        if (nFacesFound == 1)
                        {
                            break; //This prevents overlapping faces from check the corresponding face
                        }


                        /*

                        bool NextContains = oppositePolygon.Covers(vPolyIndex.Next.Point(mesh.Polygons));
                        bool PrevContains = oppositePolygon.Covers(vPolyIndex.Previous.Point(mesh.Polygons));

                        bool FlipContainsTest = vCorrespondingIndex.IsInner; // false;// vPolyIndex.IsInner ^ vCorrespondingIndex.IsInner;

                        if(FlipContainsTest)
                        {
                            NextContains = !NextContains;
                            PrevContains = !PrevContains;
                        }

                        if (NextContains == false)
                        {
                            int iOther = mesh[vPolyIndex.Next].Index;
                            int[] TriFace = new int[] { iOther, iVA, iVB };
                            MorphMeshFace face = new MorphMeshFace(TriFace);
                            mesh.AddFace(face);
                        }

                        if(PrevContains == false)
                        {
                            int iOther = mesh[vPolyIndex.Previous].Index;
                            int[] TriFace = new int[] { iOther, iVA, iVB };
                            MorphMeshFace face = new MorphMeshFace(TriFace);
                            mesh.AddFace(face);
                        }

                        */

                        /*
                        Debug.Assert(Math.Abs(iVLower - iVUpper) == 1 || (Math.Abs(iVLower - iVUpper) == Face.Count - 1));

                        int iOther = iVLower - 1;
                        //bool CounterClockwise = true;
                        if (iOther < 0 || iOther == iVUpper)
                        {
                            iOther = iVLower + 1;
                            //CounterClockwise = false;
                            if (iOther >= Face.Count || iOther == iVUpper)
                            {
                                iOther = iVUpper - 1;
                              //  CounterClockwise = true;
                                if (iOther < 0 || iOther == iVLower)
                                {
                                    iOther = iVUpper + 1;
                                //    CounterClockwise = false;
                                    if (iOther < 0 || iOther == iVLower)
                                    {
                                        throw new ArgumentException("Can't find third vertex to create face for corresponding edge");
                                    }
                                }
                            }
                        }
                        */

                        //I used to try to get winding correct, the implementation wasn't correct.  Now I handle it at the end of mesh generation.
                        //int[] TriFace = CounterClockwise ? new int[] { iOther, iVLower, iVUpper } : new int[] { iOther, iVUpper, iVLower};

                        /*
                        int[] TriFace = new int[] { iOther, iVLower, iVUpper };
                        MorphMeshFace face = new MorphMeshFace(TriFace.Select(i => Face[i])); 
                        mesh.AddFace(face);
                        */
                    }
                }
            }
        }

        /// <summary>
        /// Close corresponding-vertex faces by walking Next/Previous on each contour. Null neighbors
        /// (polyline endpoints) are skipped so open chains can still form a ribbon.
        /// </summary>
        private static int TryAddCorrespondingNeighborFaces(MorphRenderMesh mesh, IShapeIndex origin, IShapeIndex corresponding, int iVA, int iVB)
        {
            int nFacesFound = 0;

            //Two open polylines crossing in XY make a twisted sheet, not a union of interiors, so the polygon
            //orientation test cannot pick the neighbours to join.  The ribbon pairs verticies by travel direction:
            //when the lines run the same way at the crossing, the part before it on one line meets the part before
            //it on the other; when they run opposite ways, before meets after.  Trying every combination here
            //(which is what the polygon path does, relying on GetContourEdgeTypeWithOrientation to veto the wrong
            //ones) joins before-to-after on both sides of the crossing, folding the ribbon back on itself.
            if (origin is PolylineIndex && corresponding is PolylineIndex)
            {
                bool sameDirection = PolylinesRunSameDirection(mesh, origin, corresponding);
                nFacesFound += TryAddCorrespondingNeighborPair(mesh, origin.Next, sameDirection ? corresponding.Next : corresponding.Previous, iVA, iVB);
                nFacesFound += TryAddCorrespondingNeighborPair(mesh, origin.Previous, sameDirection ? corresponding.Previous : corresponding.Next, iVA, iVB);
                return nFacesFound;
            }

            nFacesFound += TryAddCorrespondingNeighborPair(mesh, origin.Next, corresponding.Next, iVA, iVB);
            nFacesFound += TryAddCorrespondingNeighborPair(mesh, origin.Next, corresponding.Previous, iVA, iVB);
            nFacesFound += TryAddCorrespondingNeighborPair(mesh, origin.Previous, corresponding.Previous, iVA, iVB);
            nFacesFound += TryAddCorrespondingNeighborPair(mesh, origin.Previous, corresponding.Next, iVA, iVB);
            return nFacesFound;
        }

        /// <summary>
        /// True when the XY tangents of two polylines at the given verticies point the same way.  An endpoint uses
        /// its single neighbour; a vertex with neither neighbour in the mesh counts as same-direction so the
        /// caller still attempts the natural pairing.
        /// </summary>
        private static bool PolylinesRunSameDirection(MorphRenderMesh mesh, IShapeIndex a, IShapeIndex b)
        {
            Vector2? ta = TangentXY(mesh, a);
            Vector2? tb = TangentXY(mesh, b);
            if (ta is null || tb is null)
                return true;

            return Vector2.Dot(ta.Value, tb.Value) >= 0;
        }

        private static Vector2? TangentXY(MorphRenderMesh mesh, IShapeIndex index)
        {
            Vector2 here = mesh[index].Position.XY();
            IShapeIndex next = index.Next;
            IShapeIndex prev = index.Previous;
            bool hasNext = next is not null && mesh.Contains(next);
            bool hasPrev = prev is not null && mesh.Contains(prev);

            if (hasNext && hasPrev)
                return mesh[next].Position.XY() - mesh[prev].Position.XY();
            if (hasNext)
                return mesh[next].Position.XY() - here;
            if (hasPrev)
                return here - mesh[prev].Position.XY();

            return null;
        }

        private static int TryAddCorrespondingNeighborPair(MorphRenderMesh mesh, IShapeIndex originNeighbor, IShapeIndex correspondingNeighbor, int iVA, int iVB)
        {
            if (originNeighbor is null || correspondingNeighbor is null)
                return 0;
            if (mesh.Contains(originNeighbor) == false || mesh.Contains(correspondingNeighbor) == false)
                return 0;

            EdgeType type = mesh.GetContourEdgeTypeWithOrientation(originNeighbor, correspondingNeighbor);
            if (type.IsValid() == false && type != EdgeType.FLIPPED_DIRECTION)
                return 0;

            MorphMeshFace first = new([mesh[originNeighbor].Index, iVA, iVB]);
            MorphMeshFace second = new([mesh[correspondingNeighbor].Index, mesh[originNeighbor].Index, iVB]);

            //Every neighbour pairing used to commit the wedge triangle unconditionally, so a vertex whose quad
            //was vetoed collected one wedge per pairing on the same CORRESPONDING edge.  Those 3-face edges and
            //the holes beside them were the largest failure class in the glia slices.  The wedge is still allowed
            //on its own - a fork whose partner contour crosses the trunk needs it, and the chord passes finish
            //the quad later - but only while every edge it touches still has room for a face.
            if (mesh.Contains(first) || FaceContainsVerticies(mesh, first, out _) || AnyEdgeAtFaceCapacity(mesh, first))
                return 0;

            mesh.AddFace(first);

            if (mesh.Contains(second) == false
                && FaceContainsVerticies(mesh, second, out _) == false
                && AnyEdgeAtFaceCapacity(mesh, second) == false)
            {
                mesh.AddFace(second);
            }

            return 1;
        }

        /// <summary>
        /// True when adding <paramref name="face"/> would push one of its edges past the face count a slice surface
        /// allows: a contour edge is the seam with the adjacent slice and carries exactly one face here, any other
        /// edge two.  Only edges already in the mesh are checked; a face may introduce a new chord.
        /// </summary>
        private static bool AnyEdgeAtFaceCapacity(MorphRenderMesh mesh, MorphMeshFace face)
        {
            foreach (IEdgeKey key in face.Edges)
            {
                if (!mesh.Contains(key))
                    continue;

                IEdge edge = mesh[key];
                int capacity = edge is MorphMeshEdge morphEdge && morphEdge.Type == EdgeType.CONTOUR ? 1 : 2;
                if (edge.Faces.Count >= capacity)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns any verticies that are inside the XY projection of a given face.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="face"></param>
        /// <param name="contained_verts"></param>
        /// <returns>True if the face contains verticies</returns>
        private static bool FaceContainsVerticies(MorphRenderMesh mesh, MorphMeshFace face, out MorphMeshVertex[] contained_verts)
        {
            Triangle tri;
            try
            {
                tri = new Triangle([.. face.iVerts.Select(i => mesh.Vertices[i].Position.XY())]);
            }
            catch (ArgumentException)
            {
                //A zero size triangle means it cannot contain verticies
                contained_verts = [];
                return false;
            }

            contained_verts = [.. mesh.Vertices.Where(v => face.iVerts.Contains(v.Index) == false && tri.Covers(v.Position.XY()))];
            return contained_verts.Length > 0;
        }

        public static MorphMeshRegionGraph GenerateRegionConnectionGraph(BajajGeneratorMesh mesh)
        {
            MorphMeshRegionGraph graph = new();

            ///----------- Create data structures ---------- 
            SortedDictionary<int, MorphMeshRegion> VertToRegion = [];
            SortedSet<int> AllRegionVerts = [];
            Dictionary<MorphMeshRegion, SortedSet<MorphMeshEdge>> RegionToEdges = [];

            foreach (MorphMeshRegion region in mesh.Regions)
            {
                foreach (int vert in region.Vertices)
                {
                    //TODO: How to handle a vertex shared by two regions?
                    if (!VertToRegion.ContainsKey(vert))
                        VertToRegion.Add(vert, region);
                }

                AllRegionVerts.UnionWith(region.Vertices);
                graph.AddNode(new Node<MorphMeshRegion, MorphMeshRegionGraphEdge>(region));
                RegionToEdges.Add(region, []);
            }

            //-------------------------------------------------
            //Find all edges that connect regions
            IEdgeKey[] EdgesConnectingRegions = [.. mesh.Edges.Keys.Where(e => AllRegionVerts.Contains(e.A) && AllRegionVerts.Contains(e.B))];

            //Create edges in the graph
            foreach (IEdgeKey edge in EdgesConnectingRegions)
            {
                var RegionA = VertToRegion[edge.A];
                var RegionB = VertToRegion[edge.B];

                if (RegionA == RegionB)
                    continue;

                if (!RegionA.Type.IsValidPair(RegionB.Type))
                    continue;

                if (RegionA.ZLevel.SetEquals(RegionB.ZLevel))
                    continue;

                MorphMeshRegionGraphEdge graphEdge = new(RegionA, RegionB);
                if (!graph.Edges.ContainsKey(graphEdge))
                {
                    graph.AddEdge(graphEdge);
                }

                MorphMeshEdge mme = mesh[edge];
                RegionToEdges[RegionA].Add(mme);
                RegionToEdges[RegionB].Add(mme);
            }

            //----------------------------------------------------

            //Add weights to the edges based on the average distance between the edges
            foreach (MorphMeshRegionGraphEdge edge in graph.Edges.Values)
            {
                var AllAEdges = RegionToEdges[edge.SourceNodeKey];
                var AllBEdges = RegionToEdges[edge.TargetNodeKey];

                SortedSet<MorphMeshEdge> EdgeSet = [.. AllAEdges];
                EdgeSet.IntersectWith(AllBEdges);

                //The weight is the mean length of all edges
                Debug.Assert(EdgeSet.Count > 0); //How are we an edge in the graph if there are no edges in the mesh?
                double avgLength = EdgeSet.Average(e => mesh.ToSegment(e.Key).Length);

                edge.Weight = avgLength;
            }

            return graph;
        }

        /// <summary>
        /// Identify verticies that do not have a complete set of faces between contour edges
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static List<MorphMeshVertex> IdentifyIncompleteVerticies(this MorphRenderMesh mesh)
        {
            return [.. mesh.Vertices.Where(v => v as MorphMeshVertex != null &&
                                        !((MorphMeshVertex)v).IsFaceSurfaceComplete(mesh))
                                        .Select(v => (MorphMeshVertex)v)];
        }

        #region SliceChordGeneration

        /// <summary>
        /// Try to add the slice chord unless it crosses an existing chord or forms an invalid EdgeType
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="sc"></param>
        /// <param name="ChordRTree"></param>
        /// <returns></returns>
        private static bool TryAddSliceChord(BajajGeneratorMesh mesh, SliceChord sc, SliceChordRTree ChordRTree, SliceChordTestType Tests,
                                             LastValidChordCache lastValid)
        {
            int originIdx = mesh[sc.Origin].Index;
            int targetIdx = mesh[sc.Target].Index;
            SliceChordTestType failures;

            bool alreadyValid = false;
            if (lastValid is not null && lastValid.TryGet(originIdx, targetIdx, out SliceChordTestType cachedTests, out int cachedGen)
                && (cachedTests & Tests) == Tests)
            {
                if (cachedGen == ChordRTree.Generation)
                {
                    alreadyValid = true;
                    ChordGenStats.IncTryAddSkippedValid();
                }
                else
                {
                    // Permanent tests already passed; only ChordIntersection can flip when the tree grows.
                    SliceChordTestType intersectionOnly = Tests & SliceChordTestType.ChordIntersection;
                    if (intersectionOnly == SliceChordTestType.None
                        || IsSliceChordValid(sc.Origin, mesh.Shapes, mesh.GetSameLevelShapes(sc), mesh.GetAdjacentLevelShapes(sc), sc.Target, ChordRTree, intersectionOnly, out failures,
                                             mesh.ShapeLinkPredicate, mesh.ForkPartition))
                    {
                        alreadyValid = true;
                        lastValid.Record(originIdx, targetIdx, Tests, ChordRTree.Generation);
                        ChordGenStats.IncTryAddSkippedValid();
                    }
                    else
                    {
                        mesh.SliceChordCandidateCache.RecordFailure(originIdx, targetIdx, failures);
                        return false;
                    }
                }
            }

            if (!alreadyValid)
            {
                if (!BajajMeshGenerator.IsSliceChordValid(sc.Origin, mesh.Shapes, mesh.GetSameLevelShapes(sc), mesh.GetAdjacentLevelShapes(sc), sc.Target, ChordRTree, Tests, out failures,
                                                        mesh.ShapeLinkPredicate, mesh.ForkPartition))
                {
                    mesh.SliceChordCandidateCache.RecordFailure(originIdx, targetIdx, failures);
                    return false;
                }

                lastValid?.Record(originIdx, targetIdx, Tests, ChordRTree.Generation);
            }

            //The shape-only GetEdgeType overload cannot type a chord touching a polyline: given two shapes and a
            //midpoint it has no vertex indices, so it can neither rebuild the chord nor test whether the chord
            //crosses a third shape, and a midpoint test is meaningless against a shape with no interior.  It used
            //to answer FLYING, a type outside IsValid()'s mask, disagreeing with the SURFACE that IsSliceChordValid
            //had just accepted the chord on.  Route polyline chords through the same index-aware overload as the
            //gate so the label cannot contradict the decision.  Polygon pairs keep the midpoint overload: the
            //index-aware overload also reports INTERNAL / UNTILED / INVAGINATION / HOLE / FLAT, which drive region
            //classification, so switching them is a behaviour change well beyond naming an accepted chord.
            EdgeType chordType = sc.Origin is PolylineIndex || sc.Target is PolylineIndex
                ? EdgeTypeExtensions.GetEdgeType(sc.Origin, sc.Target, mesh.Shapes, sc.Line.PointAlongLine(0.5))
                : EdgeTypeExtensions.GetEdgeType(sc.Line, mesh.Shapes[sc.Origin.ShapeIndex], mesh.Shapes[sc.Target.ShapeIndex]);

            MorphMeshEdge edge = new(chordType, originIdx, targetIdx);
            if (mesh.Contains(edge))
                return false;

            mesh.AddEdge(edge);
            ChordRTree.Add(sc.Line.BoundingBox.ToRTreeRect(0), sc);

            return true;
        }

        /// <summary>
        /// Generate slice chords for the remaining unknown chords.  Returns a list of incomplete verticies.
        /// </summary>
        /// <param name="mesh">The mesh, which may contain edges we cannot cross</param>
        public static List<MorphMeshVertex> FirstPassSliceChordGeneration(BajajGeneratorMesh mesh, ICollection<double> ZLevels) =>
            FirstPassSliceChordGeneration(mesh, ZLevels, out _, out _, existingChordTree: null);

        public static List<MorphMeshVertex> FirstPassSliceChordGeneration(BajajGeneratorMesh mesh, ICollection<double> ZLevels, out int chordPassCalls, out int chordsAdded) =>
            FirstPassSliceChordGeneration(mesh, ZLevels, out chordPassCalls, out chordsAdded, existingChordTree: null);

        public static List<MorphMeshVertex> FirstPassSliceChordGeneration(BajajGeneratorMesh mesh, ICollection<double> ZLevels, out int chordPassCalls, out int chordsAdded, SliceChordRTree existingChordTree)
        {
            // Reuse the region-closing RTree when the caller still holds it: CreateChordTree is O(edges) and the
            // closing pass already inserted every chord we must not cross.
            SliceChordRTree rTree = existingChordTree ?? mesh.CreateChordTree(ZLevels);

            mesh.CloseFaces();
            List<MorphMeshVertex> IncompleteVerticies = [.. mesh.MorphVerticies.Where(v => false == v.IsFaceSurfaceComplete(mesh))];
            Dictionary<int, List<MorphMeshVertex>> incompleteByShape = GroupIncompleteVerticesByShape(IncompleteVerticies);

            //Each pass relaxes a geometric heuristic that the previous pass may have been too strict about.
            //ShapeLink is present in every pass because it is not a heuristic: a pair the annotator never joined
            //stays unjoined no matter how badly the slice needs a chord.  ForkPartition rides along with it so a
            //fork's allocation is not quietly undone by the loosest pass.
            const SliceChordTestType AlwaysApplied = SliceChordTestType.Correspondance | SliceChordTestType.ChordIntersection | SliceChordTestType.EdgeType | SliceChordTestType.Theorem4
                                                   | SliceChordTestType.ShapeLink | SliceChordTestType.ForkPartition;

            SliceChordTestType[] PassCriteria =
            [
                AlwaysApplied | SliceChordTestType.Theorem2 | SliceChordTestType.LineOrientation,
                AlwaysApplied | SliceChordTestType.Theorem2,
                AlwaysApplied | SliceChordTestType.LineOrientation,
                AlwaysApplied,
            ];

            var VertexQuadTrees = mesh.CreateQuadTreesForContours();
            LastValidChordCache lastValid = new();
            NearestRankingCache nearestRanking = new();
            Dictionary<MorphMeshVertex, MorphMeshVertex> previousOtv = null;
            HashSet<MorphMeshVertex> dirtySeed = null;

            chordPassCalls = 0;
            chordsAdded = 0;

            foreach (SliceChordTestType passTestCriteria in PassCriteria)
            {
                // Looser criteria can only accept more partners; keep sticky OTV. Nearest rankings may need to
                // resume past targets that failed only on bits that are no longer requested.
                nearestRanking.InvalidateAll();

                while (true)
                {
                    chordPassCalls++;
                    ChordGenStats.IncChordPassCalls();
                    int addedThisPass = SliceChordGenerationPass(mesh, rTree, incompleteByShape, passTestCriteria, VertexQuadTrees, lastValid, nearestRanking, ref previousOtv, ref dirtySeed);
                    chordsAdded += addedThisPass;
                    ChordGenStats.AddChords(addedThisPass);
                    if (addedThisPass == 0)
                        break;

                    mesh.CloseFaces(IncompleteVerticies.Cast<Geometry.Meshing.IVertex>());
                    RemoveCompletedIncompleteVertices(mesh, IncompleteVerticies, incompleteByShape, VertexQuadTrees);
                    nearestRanking.InvalidateAll();
                }
            }

            mesh.SliceChordCandidateCache.Clear();
            lastValid.Clear();

            mesh.CloseFaces(IncompleteVerticies.Cast<Geometry.Meshing.IVertex>());
            RemoveCompletedIncompleteVertices(mesh, IncompleteVerticies, incompleteByShape, VertexQuadTrees);
            return IncompleteVerticies;
        }

        static Dictionary<int, List<MorphMeshVertex>> GroupIncompleteVerticesByShape(List<MorphMeshVertex> verts)
        {
            Dictionary<int, List<MorphMeshVertex>> byShape = [];
            foreach (MorphMeshVertex v in verts)
            {
                if (v.ShapeIndex is null)
                    continue;

                int iPoly = v.ShapeIndex.ShapeIndex;
                if (!byShape.TryGetValue(iPoly, out List<MorphMeshVertex> list))
                {
                    list = [];
                    byShape.Add(iPoly, list);
                }

                list.Add(v);
            }

            return byShape;
        }

        static void RemoveCompletedIncompleteVertices(BajajGeneratorMesh mesh, List<MorphMeshVertex> incomplete, Dictionary<int, List<MorphMeshVertex>> byShape,
                                                      SliceTopologyQuadTrees<MorphMeshVertex>? levelTrees = null)
        {
            List<MorphMeshVertex> completed = null;
            if (levelTrees.HasValue)
            {
                completed = [];
                foreach (MorphMeshVertex v in incomplete)
                {
                    if (v.IsFaceSurfaceComplete(mesh))
                        completed.Add(v);
                }
            }

            incomplete.RemoveAll(v => v.IsFaceSurfaceComplete(mesh));
            foreach (List<MorphMeshVertex> list in byShape.Values)
                list.RemoveAll(v => v.IsFaceSurfaceComplete(mesh));

            if (completed is null || completed.Count == 0)
                return;

            int evicted = 0;
            SliceTopologyQuadTrees<MorphMeshVertex> trees = levelTrees.Value;
            foreach (MorphMeshVertex v in completed)
            {
                mesh.SliceChordCandidateCache.Remove(v.Index);
                if (trees.Above.TryRemove(v, out _))
                    evicted++;
                if (trees.Below.TryRemove(v, out _))
                    evicted++;
            }

            ChordGenStats.AddCompletedVertsEvicted(evicted);
        }

        /// <summary>
        /// Generate slice chords for the remaining unknown chords, returns the number of chords added this pass.
        /// </summary>
        /// <param name="mesh">The mesh, which may contain edges we cannot cross</param>
        /// <param name="LevelTree">An optional parameter containing quadtrees for verticies on the upper and lower polygon sets.  It can be calculated once and passed as this parameter or left null and the function will build it.</param>
        private static int SliceChordGenerationPass(BajajGeneratorMesh mesh, SliceChordRTree rTree, Dictionary<int, List<MorphMeshVertex>> incompleteByShape, SliceChordTestType TestSuite,
                                                    SliceTopologyQuadTrees<MorphMeshVertex> LevelTree, LastValidChordCache lastValid, NearestRankingCache nearestRanking,
                                                    ref Dictionary<MorphMeshVertex, MorphMeshVertex> previousOtv, ref HashSet<MorphMeshVertex> dirtySeed)
        {
            Dictionary<MorphMeshVertex, MorphMeshVertex> OTVTable;
            CreateOptimalTilingVertexTableIncremental(mesh, incompleteByShape, LevelTree, TestSuite, rTree, lastValid, nearestRanking, ref previousOtv, ref dirtySeed, out OTVTable);

            List<SliceChord> CandidateChords = CreateChordCandidateList(mesh, OTVTable);

            ///Starting with the shortest chord, add all of the slice chords that do not intersect an existing chord
            CandidateChords = [.. CandidateChords.OrderBy(sc => sc.Line.Length)];

            int numAdded = 0;
            HashSet<MorphMeshVertex> addedEndpoints = [];
            foreach (SliceChord sc in CandidateChords)
            {
                if (TryAddSliceChord(mesh, sc, rTree, TestSuite, lastValid))
                {
                    numAdded += 1;
                    addedEndpoints.Add(mesh[sc.Origin]);
                    addedEndpoints.Add(mesh[sc.Target]);
                }
            }

            if (numAdded > 0 && previousOtv is not null)
            {
                foreach (KeyValuePair<MorphMeshVertex, MorphMeshVertex> kv in previousOtv)
                {
                    if (addedEndpoints.Contains(kv.Value))
                        addedEndpoints.Add(kv.Key);
                }
            }

            dirtySeed = numAdded > 0 ? addedEndpoints : null;
            previousOtv = OTVTable;
            return numAdded;
        }

        /// <summary>
        /// Using the existing slice chords determine if any faces can be added using existing edges
        /// </summary>
        public static void FirstPassFaceGeneration(MorphRenderMesh mesh, List<MorphMeshVertex> incompleteVerts = null)
        {
            //We know that all faces have a contour as part of the triangle
            incompleteVerts ??= [.. IdentifyIncompleteVerticies(mesh)];

            while (incompleteVerts.Count > 0)
            {
                MorphMeshVertex v = incompleteVerts[0];
                incompleteVerts.RemoveAt(0);

                List<int> face_path = mesh.IdentifyIncompleteFace(v, MaxFaceVerts: 4);
                int facesBefore = mesh.Faces.Count;
                if (face_path != null && face_path.Count <= 4)
                {
                    MorphMeshFace face = new(face_path);
                    if (face.IsTriangle)
                    {
                        mesh.AddFace(face);
                    }
                    else if (face.IsQuad)
                    {
                        var verts = mesh[face_path].ToArray();
                        double[] VertZLevels = [.. verts.Select(vert => vert.Position.Z).Distinct()];

                        //This was changed just before I quit for the night
                        //int NumVertZLevels = verts.Where(vert => vert.Position.Z == VertZLevels[0]).Count();
                        int NumVertZLevels = VertZLevels.Distinct().Count();
                        if (NumVertZLevels == 2)
                        {
                            mesh.AddFace(face);
                            mesh.SplitFace(face);
                        }
                        else if (NumVertZLevels == 1 || NumVertZLevels == (verts.Length - 1))
                        {
                            //Only one of the verts is on a particular Z Level   
                            var LevelA = verts.Where(vert => vert.Position.Z == VertZLevels[0]).ToArray();
                            var LevelB = verts.Where(vert => vert.Position.Z != VertZLevels[0]).ToArray();

                            Geometry.Meshing.IVertex anchor;
                            //Geometry.Meshing.IVertex[] opposite_verts;
                            if (LevelA.Length == 1)
                            {
                                anchor = LevelA[0];
                                //opposite_verts = LevelB;
                            }
                            else
                            {
                                anchor = LevelB.Length == 1 ? LevelB[0] : LevelA.Any() ? LevelA[0] : LevelB[0];
                                //opposite_verts = LevelA;
                            }

                            int iFaceAnchor = face_path.IndexOf(anchor.Index);

                            int iA = iFaceAnchor + 1;
                            int iB = iFaceAnchor + 2;
                            int iC = iFaceAnchor + 3;

                            if (iA >= face_path.Count)
                                iA -= face_path.Count;

                            if (iB >= face_path.Count)
                                iB -= face_path.Count;

                            if (iC >= face_path.Count)
                                iC -= face_path.Count;

                            int O = face_path[iFaceAnchor];
                            int A = face_path[iA];
                            int B = face_path[iB];
                            int C = face_path[iC];

                            MorphMeshFace XAB = new(O, A, B);
                            MorphMeshFace XBC = new(O, B, C);

                            mesh.AddFace(XAB);
                            mesh.AddFace(XBC);
                        }

                    }
                }
                else
                {
                    continue; //Skip this vertex since we could not make a face
                }

                //Check to see if we can add another face if the vertex is not complete yet and we just added a face successfully.
                //A face the mesh refused at edge capacity would be found again on the next pass, so the vertex is
                //only revisited when the face count actually grew.
                if (mesh.Faces.Count > facesBefore && v.IsFaceSurfaceComplete(mesh) == false)
                {
                    incompleteVerts.Insert(0, v);
                }
            }
            //mesh.CloseFaces();
        }


        /// <summary>
        /// Convert the OTV table into a set of slice chord candidates
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="OTVTable"></param>
        /// <returns></returns>
        public static List<SliceChord> CreateChordCandidateList(MorphRenderMesh mesh, OTVTable OTVTable)
        {
            List<SliceChord> CandidateChords = [];

            //Ordered because OTVTable is a ConcurrentDictionary: its enumeration order varies between runs, and this
            //loop both builds the candidate list and adds CORRESPONDING edges as it goes.
            foreach (IShapeIndex i1 in OTVTable.Keys.OrderBy(k => k))
            {
                if (OTVTable.TryGetValue(i1, out IShapeIndex i2))
                {
                    Vector2 p1 = i1.Point(mesh.Shapes);
                    Vector2 p2 = i2.Point(mesh.Shapes);

                    if (p1 != p2)
                    {
                        SliceChord sc = new(i1, i2, mesh.Shapes);
                        CandidateChords.Add(sc);
                    }
                    else
                    {
                        //This is a corresponding contour, both at the same X,Y position, add it to our list.
                        MorphMeshEdge edge = new(EdgeType.CORRESPONDING, mesh[i1].Index, mesh[i2].Index);
                        mesh.AddEdge(edge);
                    }
                }
            }

            return CandidateChords;
        }


        /// <summary>
        /// Convert the OTV table into a set of slice chord candidates
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="OTVTable"></param>
        /// <returns></returns>
        private static List<SliceChord> CreateChordCandidateList(MorphRenderMesh mesh, Dictionary<MorphMeshVertex, MorphMeshVertex> OTVTable)
        {
            List<SliceChord> CandidateChords = [];

            //Ordered by mesh vertex index so acceptance (which refuses chords that cross one already placed)
            //does not inherit dictionary enumeration order.
            foreach (MorphMeshVertex i1 in OTVTable.Keys.OrderBy(v => v.Index))
            {
                if (OTVTable.TryGetValue(i1, out MorphMeshVertex i2))
                {
                    Vector2 p1 = i1.Position.XY();
                    Vector2 p2 = i2.Position.XY();

                    if (p1 != p2)
                    {
                        SliceChord sc = new(i1.ShapeIndex, i2.ShapeIndex, mesh.Shapes);
                        CandidateChords.Add(sc);
                    }
                    else
                    {
                        //This is a corresponding contour, both at the same X,Y position, add it to our list.
                        MorphMeshEdge edge = new(EdgeType.CORRESPONDING, i1.Index, i2.Index);
                        mesh.AddEdge(edge);
                    }
                }
            }

            return CandidateChords;
        }

        /// <summary>
        /// Attempts to add each SliceChord in the OTV table to our mesh.  
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="OTVTable"></param>
        /// <param name="rTree"></param>
        /// <param name="Tests">A set of flags indicating tests.  Chords must pass the flagged tests before being added.</param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public static int TryAddOTVTable(BajajGeneratorMesh mesh, OTVTable OTVTable, SliceChordRTree rTree, SliceChordTestType Tests, SliceChordPriority priority)
        {
            List<SliceChord> CandidateChords = CreateChordCandidateList(mesh, OTVTable);

            //Ties are broken on the endpoint indices to keep the order total.  Chords arrive here in the
            //enumeration order of a ConcurrentDictionary, which varies between runs, and OrderBy is a stable sort,
            //so equal-priority chords used to inherit that arbitrary order.  Acceptance is order-dependent -
            //TryAddSliceChord refuses a chord that crosses one already placed - so the tie order decided which of
            //two competing chords won, and the same cell meshed to a different vertex and triangle count on
            //every run.
            CandidateChords = priority switch
            {
                SliceChordPriority.Distance =>
                    [.. CandidateChords
                        .OrderBy(sc => sc.Line.Length)
                        .ThenBy(sc => sc.Origin)
                        .ThenBy(sc => sc.Target)],
                SliceChordPriority.Orientation =>
                    [.. CandidateChords
                        .OrderBy(sc => EdgeTypeExtensions.Orientation(sc.Origin, sc.Target, mesh.Shapes))
                        .ThenBy(sc => sc.Origin)
                        .ThenBy(sc => sc.Target)],
                _ => throw new ArgumentException("Unexpected slice chord priority"),
            };

            //List<SliceChord> NovelCandidateChords = CandidateChords.Where(sc => !mesh.IsAnEdge(mesh[sc.Origin].Index, mesh[sc.Target].Index)).ToList();

            int count = 0;
            foreach (SliceChord sc in CandidateChords)
            {
                //TODO: Probably need to check that the chords are all created
                count += TryAddSliceChord(mesh, sc, rTree, Tests, lastValid: null) ? 1 : 0;
            }

            return count;
        }


        #endregion

        private static void AddIndexSetToMeshIndexMap(Dictionary<Vector3, long> map, Geometry.Meshing.Mesh3D<IVertex3D<ulong>> mesh, Geometry.IIndexSet set)
        {
            Geometry.Meshing.IVertex3D[] verts = [.. mesh[set]];
            long[] mesh_indicies = [.. set];

            for (int iVert = 0; iVert < mesh_indicies.Length; iVert++)
            {
                map.Add(verts[iVert].Position, mesh_indicies[iVert]);
            }
        }

        /// <summary>
        /// Build a map so we can navigate from a vertex back to a mesh index from a port
        /// </summary>
        /// <param name="mesh">The mesh all ports in Nodes should index into</param>
        /// <param name="Nodes">All nodes containing cap ports that index into the mesh</param>
        /// <returns></returns>
        private static Dictionary<Vector3, long> CreateVertexToMeshIndexMap(Geometry.Meshing.Mesh3D<IVertex3D<ulong>> mesh, IEnumerable<ConnectionVertices> ports)
        {
            Dictionary<Vector3, long> map = [];

            foreach (ConnectionVertices port in ports)
            {
                AddIndexSetToMeshIndexMap(map, mesh, port.ExternalBorder);

                foreach (var innerBorder in port.InternalBorders)
                {
                    AddIndexSetToMeshIndexMap(map, mesh, innerBorder);
                }
            }

            return map;
        }

        public static bool Theorem1() => throw new NotImplementedException();

        /// <summary>
        /// Theorem2 requires that the orientation of the contours connected by the slice chord match. 
        /// </summary>
        /// <param name="polygons">Contours on projection slice</param>
        /// <param name="NearestContour">Nearest vertex on projection slice</param>
        /// <param name="p">Point projected</param>
        /// <returns></returns>
        public static bool Theorem2(IReadOnlyList<IShape2D> Polygons, IShapeIndex vertex, IShapeIndex NearestContour)
        {
            if (!(vertex is PolygonIndex v && NearestContour is PolygonIndex nc))
                return true; //Polylines do not have an orientation since they are visible from both sides

            //return EdgeTypeExtensions.OrientationsAreMatched(vertex, NearestContour, Polygons);

            Vector2 p1 = vertex.Point(Polygons);
            Vector2 p2 = NearestContour.Point(Polygons);

            if (p1 == p2) //Overlapping vertex always goes in the OTV table
            {
                return true;
            }
            else
            {
                LineSegment SliceChord = new(p1, p2);

                bool MatchingOrientations = vertex.IsInner == NearestContour.IsInner;
                /*
                if (!MatchingOrientations && (vertex.IsInner ^ NearestContour.IsInner))
                {
                    Polygon pA = Polygons[vertex.ShapeIndex];
                    Polygon pB = Polygons[NearestContour.ShapeIndex];

                    bool ExternalContourVertexInsideHole = pA.InteriorPolygonContains(p2) || pB.InteriorPolygonContains(p1);
                    if(ExternalContourVertexInsideHole)
                    {
                        if(!pA.IsVertex(p2) && !pB.IsVertex(p1))
                        {
                            MatchingOrientations = !MatchingOrientations;
                        }
                        
                    }
                }*/

                Vector2[] adjacent1 = nc.ConnectedVertices(Polygons);
                Vector2[] pqr = [adjacent1[0], p2, adjacent1[1]];

                Vector2[] adjacent2 = v.ConnectedVertices(Polygons);
                Vector2[] mno = [adjacent2[0], p1, adjacent2[1]];

                bool IsCorrectSide = p1.IsLeftSide(pqr) != p2.IsLeftSide(mno);

                if (!MatchingOrientations)
                {
                    return !IsCorrectSide;
                }

                return IsCorrectSide;
            }

        }

        public static bool Theorem4(IReadOnlyList<IShape2D> sliceShapes, IShapeIndex NearestContour, Vector2 p1)
        {
            Vector2 p2 = NearestContour.Point(sliceShapes);

            LineSegment ContourLine = new(p1, p2);

            foreach (IShape2D poly in sliceShapes)
            {
                if (!Theorem4(poly, ContourLine))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Theorem 4 requries that a line segment does not occupy space both internal and external to the polygon.
        /// Lines that fall over a polygon segment are acceptable as long as the rest of the line qualifies.
        /// </summary>
        /// <param name="poly"></param>
        /// <param name="line"></param>
        /// <returns></returns>
        public static bool Theorem4(IReadOnlyList<IShape2D> shapes, LineSegment line)
        {
            foreach (IShape2D shape in shapes)
            {
                if (!Theorem4(shape, line))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Theorem 4 requries that a line segment does not occupy space both internal and external to the polygon.
        /// Lines that fall over a polygon segment are acceptable as long as the rest of the line qualifies.
        /// </summary>
        /// <param name="poly"></param>
        /// <param name="line"></param>
        /// <returns></returns>
        public static bool Theorem4(IShape2D shape, LineSegment line)
        {
            //Chord vs shape AABB is cheap; most same-slice shapes never near the candidate chord.
            if (!shape.BoundingBox.Intersects(line.BoundingBox))
                return true;

            if (shape is Polygon poly)
                return !line.Intersects(poly, true, out List<Vector2> intersections);

            if (shape is Polyline polyline)
                return !polyline.Intersects(line);

            throw new NotImplementedException();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="vertex"></param>
        /// <param name="Shapes"></param>
        /// <param name="SameLevelShapes"></param>
        /// <param name="AdjacentLevelShapes"></param>
        /// <param name="candidate"></param>
        /// <param name="chordTree"></param>
        /// <param name="TestsToRun"></param>
        /// <param name="results">Flags are set for any failing tests, though other tests may also fail but were not run due to short circuiting.</param>
        /// <param name="isLinked">Answers whether two shape indices are joined by a LocationLink.  Null skips the
        /// ShapeLink test, which is what callers without slice topology (debug views, hand-built shape arrays) want.</param>
        /// <param name="forkPartition">Vertex ranges allocated to each partner of a forking polyline.  Null skips
        /// the ForkPartition test.</param>
        /// <returns></returns>
        public static bool IsSliceChordValid(IShapeIndex vertex, IShape2D[] Shapes, in IReadOnlyList<IShape2D> SameLevelShapes, in IReadOnlyList<IShape2D> AdjacentLevelShapes,
                                                       IShapeIndex candidate, SliceChordRTree chordTree, SliceChordTestType TestsToRun, out SliceChordTestType results,
                                                       Func<int, int, bool> isLinked = null, PolylineForkPartition forkPartition = null)
        {
            ChordGenStats.IncIsSliceChordValid();
            results = SliceChordTestType.None;

            //Checked before the geometry because it is a dictionary lookup and it rejects the pairs whose geometry
            //is most expensive to evaluate: distant shapes that only ended up in the same slice via a doubled-back
            //Z chain.  Applies to corresponding verticies too, since two unlinked annotations that merely coincide
            //in XY must not be stitched together either.
            if ((TestsToRun & SliceChordTestType.ShapeLink) > 0 && isLinked is not null)
            {
                if (isLinked(vertex.ShapeIndex, candidate.ShapeIndex) == false)
                {
                    results |= SliceChordTestType.ShapeLink;
                    return false;
                }
            }

            //Both directions matter: either end of the chord may belong to a forking polyline, and each fork
            //allocates its own verticies independently of what its partner allocated.
            if ((TestsToRun & SliceChordTestType.ForkPartition) > 0 && forkPartition is not null)
            {
                if (forkPartition.AllowsChord(vertex.ShapeIndex, vertex.VertexIndex, candidate.ShapeIndex) == false
                 || forkPartition.AllowsChord(candidate.ShapeIndex, candidate.VertexIndex, vertex.ShapeIndex) == false)
                {
                    results |= SliceChordTestType.ForkPartition;
                    return false;
                }
            }

            Vector2 p1 = vertex.Point(Shapes);
            Vector2 p2 = candidate.Point(Shapes);
            if (p1 == p2)
            {
                //Corresponding verticies: the same X,Y appears on both contours.  A zero-length chord cannot
                //intersect anything and has no orientation, so the geometric theorems are vacuous here and only the
                //Correspondance flag decides whether the pairing is allowed.  That flag was ignored, so a caller
                //that deliberately left it out still had its corresponding chords accepted.
                if ((TestsToRun & SliceChordTestType.Correspondance) == 0)
                {
                    results |= SliceChordTestType.Correspondance;
                    return false;
                }

                return true;
            }

            LineSegment ChordLine = new(p1, p2);

            if ((TestsToRun & SliceChordTestType.ChordIntersection) > 0)
            {
                //IEnumerable<ISliceChord> existingChords = chordTree.Intersects(ChordLine.BoundingBox.ToRTreeRect(0));
                if (chordTree.IntersectionGenerator(ChordLine.BoundingBox.ToRTreeRect(0)).Any(c => c.Line.Intersects(ChordLine, true)))
                {
                    results |= SliceChordTestType.ChordIntersection;
                    return false;
                }
            }

            if ((TestsToRun & SliceChordTestType.EdgeType) > 0)
            {
                EdgeType edgeType = EdgeTypeExtensions.GetEdgeType(vertex, candidate, Shapes, ChordLine.PointAlongLine(0.5));
                if (!edgeType.IsValid())
                {
                    results |= SliceChordTestType.EdgeType;
                    return false;
                }
            }

            bool AngleOrientation = true;
            bool T2 = true;
            bool T4 = true;
            bool T4Opp = true;

            if ((TestsToRun & SliceChordTestType.LineOrientation) > 0
                && vertex is PolygonIndex && candidate is PolygonIndex)
            {
                AngleOrientation = EdgeTypeExtensions.OrientationsAreMatched(vertex, candidate, Shapes);
                if (!AngleOrientation)
                {
                    results |= SliceChordTestType.LineOrientation;
                    return false;
                }
            }

            if ((TestsToRun & SliceChordTestType.Theorem2) > 0)
            {
                //Theorem 2 is symmetric in its two verticies: swapping them exchanges both the points and the
                //adjacency rings, and the side comparison is an inequality between the two, so one evaluation
                //covers the chord in both directions.
                T2 = Theorem2(Shapes, vertex, candidate);
                if (!T2)
                {
                    results |= SliceChordTestType.Theorem2;
                    return false;
                }

            }

            if ((TestsToRun & SliceChordTestType.Theorem4) > 0)
            {
                T4Opp = Theorem4(AdjacentLevelShapes, ChordLine);
                if (!T4Opp)
                {
                    results |= SliceChordTestType.Theorem4;
                    return false;
                }

                T4 = Theorem4(SameLevelShapes, ChordLine);
                if (!T4)
                {
                    results |= SliceChordTestType.Theorem4;
                    return false;
                }
            }

            return AngleOrientation && T2 && T4 && T4Opp;
            //return Theorem2(OppositeContours, candidate, p) && Theorem4(OppositeContours, ContourLine) && Theorem4(Contours, ContourLine);
        }

        public static bool IsSliceChordValid(MorphRenderMesh mesh, MorphMeshVertex vertex, IReadOnlyList<IShape2D> SameLevelShapes, IReadOnlyList<IShape2D> AdjacentLevelShapes,
                                                       MorphMeshVertex candidate, SliceChordRTree chordTree, SliceChordTestType TestsToRun, out SliceChordTestType failures)
        {
            failures = SliceChordTestType.None;
            if (candidate.FacesAreComplete)
                return false;

            return IsSliceChordValid(vertex.ShapeIndex, mesh.Shapes, SameLevelShapes, AdjacentLevelShapes, candidate.ShapeIndex, chordTree, TestsToRun, out failures,
                                     BajajGeneratorMesh.LinkPredicateFor(mesh), BajajGeneratorMesh.ForkPartitionFor(mesh));

            /*
            Vector2 p1 = vertex.Position.XY();
            Vector2 p2 = candidate.Position.XY();
            if (p1 == p2)
                return true;

            if (candidate.FacesAreComplete)
                return false; 

            LineSegment ChordLine = new LineSegment(p1, p2);

            if ((TestsToRun & SliceChordTestType.ChordIntersection) > 0)
            {
                List<ISliceChord> existingChords = chordTree.Intersects(ChordLine.BoundingBox.ToRTreeRect(0));
                if (existingChords.Any(c => c.Line.Intersects(ChordLine, true)))
                    return false;
            }

            if ((TestsToRun & SliceChordTestType.EdgeType) > 0)
            {
                EdgeType edgeType = EdgeTypeExtensions.GetEdgeType(vertex.PolyIndex.Value, candidate.PolyIndex.Value, mesh.Polygons, ChordLine.PointAlongLine(0.5));
                if (!edgeType.IsValid())
                    return false;
            }

            bool AngleOrientation = true;
            bool T2 = true;
            bool T2Opp = true;
            bool T4 = true;
            bool T4Opp = true;

            if ((TestsToRun & SliceChordTestType.LineOrientation) > 0)
            {
                AngleOrientation = EdgeTypeExtensions.OrientationsAreMatched(vertex.PolyIndex.Value, candidate.PolyIndex.Value, mesh.Polygons);
                if (!AngleOrientation)
                    return false;
            }

            if ((TestsToRun & SliceChordTestType.Theorem2) > 0)
            {
                T2 = Theorem2(mesh.Polygons, vertex.PolyIndex.Value, candidate.PolyIndex.Value);
                if (!T2)
                    return false;
            }

            //bool T2 = true;

            if ((TestsToRun & SliceChordTestType.Theorem4) > 0)
            {
                T4Opp = Theorem4(AdjacentLevelPolys, ChordLine);
                if (!T4Opp)
                    return false;

                T4 = Theorem4(SameLevelPolys, ChordLine);
                if (!T4)
                    return false;

            }

            return AngleOrientation && T2 && T2Opp && T4 && T4Opp;
            //return Theorem2(OppositeContours, candidate, p) && Theorem4(OppositeContours, ContourLine) && Theorem4(Contours, ContourLine);
            */
        }

        /// <summary>
        /// Locate the best slice chord partner for a given vertex
        /// </summary>
        /// <param name="vertex">Vertex we are testing</param>
        /// <param name="Polygons">Polygon array verticies refer to</param>
        /// <param name="SameLevelPolys">Polygons in the array at the same Z level as the vertex</param>
        /// <param name="AdjacentLevelPolys">Polygons in the array at a different Z level as the vertex</param>
        /// <param name="OppositeVertexTree">Lookup data structure for verticies on different Z levels</param>
        /// <param name="chordTree">Lookup data structure for existing slice chords</param>
        /// <returns></returns>
        public static List<SliceChord> FindAllSliceChords(PolygonIndex vertex, PolygonIndex[] OppositeVerticies, Polygon[] Polygons, IReadOnlyList<Polygon> SameLevelPolys, IReadOnlyList<Polygon> AdjacentLevelPolys,
                                                              SliceChordRTree chordTree, SliceChordTestType TestsToRun)
        {
            Vector2 p = vertex.Point(Polygons);

            List<SliceChord> listValid = [];

            foreach (PolygonIndex opposite in OppositeVerticies)
            {
                if (IsSliceChordValid(vertex, Polygons, SameLevelPolys, AdjacentLevelPolys, opposite, chordTree, TestsToRun, out SliceChordTestType failures))
                {
                    SliceChord sc = new(vertex, opposite, Polygons);
                    listValid.Add(sc);
                }
            }

            return listValid;
        }

        /// <summary>
        /// Locate the best slice chord partner for a given vertex
        /// </summary>
        /// <param name="vertex">Vertex we are testing</param>
        /// <param name="Polygons">Polygon array verticies refer to</param>
        /// <param name="SameLevelPolys">Polygons in the array at the same Z level as the vertex</param>
        /// <param name="AdjacentLevelShapes">Polygons in the array at a different Z level as the vertex</param>
        /// <param name="oppositeVertexTreeWithUniqueValues">Lookup data structure for verticies on different Z levels</param>
        /// <param name="chordTree">Lookup data structure for existing slice chords</param>
        /// <returns></returns>
        private static IShapeIndex FindOptimalTilingForVertexByDistance(IShapeIndex vertex, IShape2D[] Polygons, IReadOnlyList<IShape2D> SameLevelPolys, IReadOnlyList<IShape2D> AdjacentLevelShapes,
                                                              QuadTreeWithUniqueValues<IShapeIndex> oppositeVertexTreeWithUniqueValues, SliceChordRTree chordTree, SliceChordTestType TestsToRun,
                                                              Func<int, int, bool> isLinked = null, PolylineForkPartition forkPartition = null)
        {
            Vector2 p = vertex.Point(Polygons);
            if (oppositeVertexTreeWithUniqueValues.TryFindNearest(p, out var NearestPoint, out double distance) == false)
                return default;

            if (IsSliceChordValid(vertex, Polygons, SameLevelPolys, AdjacentLevelShapes, NearestPoint, chordTree, TestsToRun, out SliceChordTestType failures, isLinked, forkPartition))
            {
                return NearestPoint;
            }

            //OK, the closest point is not a match.  Expand the search. Multiply before the query so the first
            //expand is 10 (avoid a wasted FindNearestPoints(1) that duplicates TryFindNearest).
            int iNextTest = 1;
            int BatchSize = 1;
            int BatchMultiple = 10;
            List<DistanceToPoint<IShapeIndex>> NearestList = null;

            while (true)
            {
                if (iNextTest >= oppositeVertexTreeWithUniqueValues.Count)
                    return new PolygonIndex?();

                if ((NearestList is null || iNextTest >= NearestList.Count))
                {
                    BatchSize *= BatchMultiple;
                    ChordGenStats.IncFindNearest();
                    NearestList = oppositeVertexTreeWithUniqueValues.FindNearestPoints(p, BatchSize);

                    if (NearestList.Count < BatchSize && iNextTest >= NearestList.Count)
                    {
                        return new PolygonIndex?();
                    }
                }

                if (iNextTest < NearestList.Count)
                {
                    IShapeIndex testPoint = NearestList[iNextTest].Value;

                    if (IsSliceChordValid(vertex, Polygons, SameLevelPolys, AdjacentLevelShapes, testPoint, chordTree, TestsToRun, out failures, isLinked, forkPartition))
                        return testPoint;
                }

                iNextTest++;
            }
        }

        /// <summary>
        /// Locate the best slice chord partner for a given vertex
        /// </summary>
        /// <param name="vertex">Vertex we are testing</param>
        /// <param name="Polygons">Polygon array verticies refer to</param>
        /// <param name="SameLevelShapes">Polygons in the array at the same Z level as the vertex</param>
        /// <param name="AdjacentLevelShapes">Polygons in the array at a different Z level as the vertex</param>
        /// <param name="oppositeVertexTreeWithUniqueValues">Lookup data structure for verticies on different Z levels</param>
        /// <param name="chordTree">Lookup data structure for existing slice chords</param>
        /// <returns></returns>
        private static MorphMeshVertex FindOptimalTilingForVertexByDistance(this MorphRenderMesh mesh, MorphMeshVertex vertex, IReadOnlyList<IShape2D> SameLevelShapes, IReadOnlyList<IShape2D> AdjacentLevelShapes,
                                                              QuadTreeWithUniqueValues<MorphMeshVertex> oppositeVertexTreeWithUniqueValues, SliceChordRTree chordTree, SliceChordTestType TestsToRun,
                                                              LastValidChordCache lastValid = null, NearestRankingCache nearestRanking = null)
        {
            Vector2 p = vertex.Position.XY();
            BajajGeneratorMesh bajajMesh = mesh as BajajGeneratorMesh;
            SliceChordOriginTestResultsCache KnownCandidateFailures = bajajMesh?.SliceChordCandidateCache.GetFailuresForOrigin(vertex.Index);
            SliceChordTestType failures;

            bool TryAccept(MorphMeshVertex candidate)
            {
                if (candidate is null || candidate.FacesAreComplete)
                    return false;

                if (KnownCandidateFailures != null)
                {
                    SliceChordTestType known = KnownCandidateFailures.GetFailures(candidate.Index, TestsToRun);
                    if ((known & ChordGenStats.PermanentFailureMask) != SliceChordTestType.None)
                    {
                        ChordGenStats.IncPermanentCacheHit();
                        return false;
                    }

                    // ChordIntersection failures stay sticky: more chords cannot un-cross a pair.
                    if ((known & SliceChordTestType.ChordIntersection) != SliceChordTestType.None
                        && (TestsToRun & SliceChordTestType.ChordIntersection) != SliceChordTestType.None)
                    {
                        ChordGenStats.IncPermanentCacheHit();
                        return false;
                    }

                    if (known != SliceChordTestType.None)
                        return false;
                }

                if (IsSliceChordValid(mesh, vertex, SameLevelShapes, AdjacentLevelShapes, candidate, chordTree, TestsToRun, out failures))
                {
                    lastValid?.Record(vertex.Index, candidate.Index, TestsToRun, chordTree.Generation);
                    return true;
                }

                KnownCandidateFailures?.RecordFailure(candidate.Index, failures);
                return false;
            }

            NearestRankingCache.Entry ranking = null;
            int oppositeCount = oppositeVertexTreeWithUniqueValues.Count;
            int rTreeGen = chordTree.Generation;
            if (nearestRanking is not null && nearestRanking.TryGet(vertex.Index, oppositeCount, rTreeGen, out ranking)
                && ranking.List is not null)
            {
                while (ranking.NextIndex < ranking.List.Count)
                {
                    MorphMeshVertex resumeCandidate = ranking.List[ranking.NextIndex].Value;
                    ranking.NextIndex++;
                    if (TryAccept(resumeCandidate))
                        return resumeCandidate;
                }
            }
            else
            {
                if (false == oppositeVertexTreeWithUniqueValues.TryFindNearest(p, out var NearestPoint, out double distance))
                    return null;

                if (TryAccept(NearestPoint))
                    return NearestPoint;

                ranking = nearestRanking?.GetOrCreate(vertex.Index) ?? new NearestRankingCache.Entry();
                ranking.List = null;
                ranking.NextIndex = 1;
                ranking.BatchSize = 1;
                ranking.OppositeTreeCount = oppositeCount;
                ranking.RTreeGeneration = rTreeGen;
            }

            // Expand the search. Multiply batch size before the query so the first expand is 10 (not a wasted
            // FindNearestPoints(1) that duplicates TryFindNearest).
            const int BatchMultiple = 10;
            while (true)
            {
                if (ranking.NextIndex >= oppositeVertexTreeWithUniqueValues.Count)
                    return null;

                if (ranking.List is null || ranking.NextIndex >= ranking.List.Count)
                {
                    ranking.BatchSize = ranking.BatchSize <= 1 ? BatchMultiple : ranking.BatchSize * BatchMultiple;
                    ChordGenStats.IncFindNearest();
                    ranking.List = oppositeVertexTreeWithUniqueValues.FindNearestPoints(p, ranking.BatchSize);
                    ranking.OppositeTreeCount = oppositeCount;
                    ranking.RTreeGeneration = rTreeGen;

                    if (ranking.List.Count < ranking.BatchSize && ranking.NextIndex >= ranking.List.Count)
                        return null;
                }

                if (ranking.NextIndex < ranking.List.Count)
                {
                    MorphMeshVertex testPoint = ranking.List[ranking.NextIndex].Value;
                    ranking.NextIndex++;
                    if (TryAccept(testPoint))
                        return testPoint;
                }
                else
                {
                    ranking.NextIndex++;
                }
            }
        }

        private static void CreateOptimalTilingVertexTableIncremental(
            BajajGeneratorMesh mesh,
            Dictionary<int, List<MorphMeshVertex>> incompleteByShape,
            SliceTopologyQuadTrees<MorphMeshVertex> CandidateTreeByLevel,
            SliceChordTestType TestsToRun,
            SliceChordRTree chordTree,
            LastValidChordCache lastValid,
            NearestRankingCache nearestRanking,
            ref Dictionary<MorphMeshVertex, MorphMeshVertex> previousOtv,
            ref HashSet<MorphMeshVertex> dirtySeed,
            out Dictionary<MorphMeshVertex, MorphMeshVertex> OTVTable)
        {
            ChordGenStats.IncOtvRebuild();
            OTVTable = new Dictionary<MorphMeshVertex, MorphMeshVertex>();

            int incompleteCount = 0;
            foreach (List<MorphMeshVertex> list in incompleteByShape.Values)
                incompleteCount += list.Count;

            if (previousOtv is null || incompleteCount == 0)
            {
                ChordGenStats.IncOtvFullRebuild();
                FillOtvTable(mesh, incompleteByShape, CandidateTreeByLevel, TestsToRun, chordTree, lastValid, nearestRanking, sticky: null, dirtyFilter: null, OTVTable);
                previousOtv = new Dictionary<MorphMeshVertex, MorphMeshVertex>(OTVTable);
                dirtySeed = null;
                return;
            }

            HashSet<MorphMeshVertex> dirty = dirtySeed is null ? [] : [.. dirtySeed];
            foreach (KeyValuePair<int, List<MorphMeshVertex>> polygroup in incompleteByShape)
            {
                foreach (MorphMeshVertex v in polygroup.Value)
                {
                    if (v.FacesAreComplete)
                        continue;
                    if (!previousOtv.TryGetValue(v, out MorphMeshVertex partner) || partner.FacesAreComplete)
                        dirty.Add(v);
                }
            }

            if (dirty.Count >= incompleteCount)
            {
                ChordGenStats.IncOtvFullRebuild();
                FillOtvTable(mesh, incompleteByShape, CandidateTreeByLevel, TestsToRun, chordTree, lastValid, nearestRanking, sticky: previousOtv, dirtyFilter: null, OTVTable);
                previousOtv = new Dictionary<MorphMeshVertex, MorphMeshVertex>(OTVTable);
                dirtySeed = null;
                return;
            }

            ChordGenStats.IncOtvDirtyRebuild();
            int unchanged = 0;
            HashSet<MorphMeshVertex> mustRecompute = [.. dirty];
            foreach (KeyValuePair<int, List<MorphMeshVertex>> polygroup in incompleteByShape)
            {
                int iPoly = polygroup.Key;
                bool IsUpperShape = mesh.UpperShapeIndicies.Contains(iPoly);
                IShape2D[] SameLevelShapes = IsUpperShape ? mesh.UpperShapes : mesh.LowerShapes;
                IShape2D[] AdjacentLevelShapes = IsUpperShape ? mesh.LowerShapes : mesh.UpperShapes;

                foreach (MorphMeshVertex v in polygroup.Value)
                {
                    if (v.FacesAreComplete || mustRecompute.Contains(v))
                        continue;
                    if (!previousOtv.TryGetValue(v, out MorphMeshVertex partner))
                    {
                        mustRecompute.Add(v);
                        continue;
                    }

                    // After chords were inserted, only ChordIntersection can invalidate a previously sticky partner.
                    if (dirtySeed is not null
                        && !IsSliceChordValid(mesh, v, SameLevelShapes, AdjacentLevelShapes, partner, chordTree, SliceChordTestType.ChordIntersection, out _))
                    {
                        mustRecompute.Add(v);
                        continue;
                    }

                    OTVTable[v] = partner;
                    unchanged++;
                    ChordGenStats.IncOtvStickyHit();
                    lastValid?.Record(v.Index, partner.Index, TestsToRun, chordTree.Generation);
                }
            }

            ChordGenStats.AddUnchangedOtvEntries(unchanged);
            if (mustRecompute.Count >= incompleteCount)
            {
                ChordGenStats.IncOtvFullRebuild();
                OTVTable.Clear();
                FillOtvTable(mesh, incompleteByShape, CandidateTreeByLevel, TestsToRun, chordTree, lastValid, nearestRanking, sticky: previousOtv, dirtyFilter: null, OTVTable);
            }
            else
            {
                FillOtvTable(mesh, incompleteByShape, CandidateTreeByLevel, TestsToRun, chordTree, lastValid, nearestRanking, sticky: previousOtv, dirtyFilter: mustRecompute, OTVTable);
            }

            previousOtv = new Dictionary<MorphMeshVertex, MorphMeshVertex>(OTVTable);
            dirtySeed = null;
        }

        static void FillOtvTable(
            BajajGeneratorMesh mesh,
            Dictionary<int, List<MorphMeshVertex>> incompleteByShape,
            SliceTopologyQuadTrees<MorphMeshVertex> CandidateTreeByLevel,
            SliceChordTestType TestsToRun,
            SliceChordRTree chordTree,
            LastValidChordCache lastValid,
            NearestRankingCache nearestRanking,
            Dictionary<MorphMeshVertex, MorphMeshVertex> sticky,
            HashSet<MorphMeshVertex> dirtyFilter,
            Dictionary<MorphMeshVertex, MorphMeshVertex> OTVTable)
        {
            foreach (KeyValuePair<int, List<MorphMeshVertex>> polygroup in incompleteByShape)
            {
                if (polygroup.Value.Count == 0)
                    continue;

                int iPoly = polygroup.Key;
                QuadTreeWithUniqueValues<MorphMeshVertex> treeWithUniqueValues = CandidateTreeByLevel.GetOppositeSide(iPoly);

                bool IsUpperShape = mesh.UpperShapeIndicies.Contains(iPoly);
                IShape2D[] SameLevelShapes = IsUpperShape ? mesh.UpperShapes : mesh.LowerShapes;
                IShape2D[] AdjacentLevelShapes = IsUpperShape ? mesh.LowerShapes : mesh.UpperShapes;

                foreach (MorphMeshVertex v in polygroup.Value)
                {
                    if (v.FacesAreComplete)
                        continue;
                    if (dirtyFilter is not null && !dirtyFilter.Contains(v))
                        continue;
                    if (OTVTable.ContainsKey(v))
                        continue;

                    ChordGenStats.IncOtvVertexEval();

                    if (sticky is not null
                        && sticky.TryGetValue(v, out MorphMeshVertex stickyPartner)
                        && !stickyPartner.FacesAreComplete
                        && IsSliceChordValid(mesh, v, SameLevelShapes, AdjacentLevelShapes, stickyPartner, chordTree, TestsToRun, out _))
                    {
                        OTVTable.TryAdd(v, stickyPartner);
                        lastValid?.Record(v.Index, stickyPartner.Index, TestsToRun, chordTree.Generation);
                        ChordGenStats.IncOtvStickyHit();
                        continue;
                    }

                    MorphMeshVertex NearestOnOtherLevel = mesh.FindOptimalTilingForVertexByDistance(v, SameLevelShapes, AdjacentLevelShapes, treeWithUniqueValues, chordTree, TestsToRun, lastValid, nearestRanking);
                    if (NearestOnOtherLevel != null)
                        OTVTable.TryAdd(v, NearestOnOtherLevel);
                }
            }
        }

        /// <summary>
        /// Return a SortedList<int, List<Polygon>> using Z level as the key and lists all polygons for that Z level.
        /// </summary>
        /// <param name="polys"></param>
        /// <param name="PolyZ"></param>
        /// <returns></returns>
        private static SortedList<int, List<IShape2D>> ShapeByLevel(IShape2D[] polys, double[] ShapeZ)
        {
            SortedList<int, List<IShape2D>> levels = [];

            List<int> ZLevels = [.. ShapeZ.Distinct().Select(z => (int)z)];

            foreach (int Z in ZLevels)
            {
                List<IShape2D> level = [.. polys.Where((p, i) => ShapeZ[i] == Z)];
                levels.Add(Z, level);
            }

            return levels;
        }

        private static SortedList<int, List<IShape2D>> ShapeByLevel(this MorphRenderMesh mesh)
        {
            //TODO:  MorphRenderMesh should simply organize the Polygons as a hash table keyed on Z with a list of polygons for each Z value
            SortedList<int, List<IShape2D>> levels = [];

            List<int> ZLevels = [.. mesh.ShapeZ.Distinct().Select(z => (int)z)];

            foreach (int Z in ZLevels)
            {
                List<IShape2D> level = [.. mesh.Shapes.Where((p, i) => mesh.ShapeZ[i] == Z)];
                levels.Add(Z, level);
            }

            return levels;
        }

        /*
        public static void CreateOptimalTilingVertexTable(Polygon[] polygons, bool[] IsPolyAbove, SliceChordTestType TestsToRun, out OTVTable OTVTable)
        { 
            SliceChordRTree chordTree = new SliceChordRTree();
            CreateOptimalTilingVertexTable(new PolySetVertexEnum(polygons), polygons, IsPolyAbove, TestsToRun, out OTVTable, ref chordTree);
        }

        public static void CreateOptimalTilingVertexTable(IEnumerable<PointIndex> VerticiesToMap, IEnumerable<PointIndex> CandidateVerticies, Polygon[] polygons, bool[] IsPolyAbove, SliceChordTestType TestsToRun, out OTVTable Table, ref SliceChordRTree chordTree)
        { 
            SliceTopologyQuadTrees<PointIndex> LevelTree = CreateQuadTreesForVerticies(CandidateVerticies, polygons, IsPolyAbove);

            ////////////////////////////////////////////////////
            CreateOptimalTilingVertexTable(VerticiesToMap, polygons, IsPolyAbove, LevelTree, TestsToRun, out Table, ref chordTree);
        }
        
        public static ConcurrentDictionary<PointIndex, List<SliceChord>> CreateFullOptimalTilingVertexTable(IEnumerable<PointIndex> VerticiesToMap, IEnumerable<PointIndex> MatchCandidates, Polygon[] polygons, bool[] PolyZ, SortedList<int, QuadTreeWithUniqueValues<PointIndex>> CandidateTreeByLevel, SliceChordTestType TestsToRun,
                                                         ref SliceChordRTree chordTree)
        {
            SortedList<int, List<Polygon>> levels = PolyByLevel(polygons, PolyZ);
            Debug.Assert(levels.Keys.Count == 2);

            ConcurrentDictionary<PointIndex, List<SliceChord>> OTVTable = new ConcurrentDictionary<PointIndex, List<SliceChord>>();

            SortedList<double, PointIndex[]> CandidatesByLevel = new SortedList<double, PointIndex[]>();

            foreach (var ZLevel in MatchCandidates.GroupBy(v => PolyZ[v.ShapeIndex]))
            {
                CandidatesByLevel.Add(ZLevel.Key, MatchCandidates.ToArray());
            }

            foreach (var polygroup in VerticiesToMap.GroupBy(v => v.ShapeIndex))
            {
                int iPoly = polygroup.Key;
                Polygon poly = polygons[iPoly];
                int Z = (int)PolyZ[iPoly];
                int AdjacentZ = (int)PolyZ.Where(adjz => adjz != Z).First();

                //QuadTreeWithUniqueValues<PointIndex> treeWithUniqueValues = CandidateTreeByLevel[AdjacentZ];

                List<Polygon> SameLevelPolys = levels[Z];
                List<Polygon> AdjacentLevelPolys = levels[AdjacentZ];

                foreach (PointIndex i in polygroup)
                {
                    Vector2 p1 = i.Point(poly);
                    List<SliceChord> listChords = FindAllSliceChords(i, CandidatesByLevel[AdjacentZ], polygons, SameLevelPolys, AdjacentLevelPolys, chordTree, TestsToRun);
                    if (listChords.Count > 0)
                    {
                        OTVTable.TryAdd(i, listChords);
                    }
                }
            }

            return OTVTable;
        }
        */
        /// <summary>
        /// Find the optimal tiling vertex for the passed verticies
        /// </summary>
        /// <param name="VerticiesToMap"></param>
        /// <param name="polygons"></param>
        /// <param name="PolyZ"></param>
        /// <param name="OTVTable"></param>
        public static void CreateOptimalTilingVertexTable(IEnumerable<IShapeIndex> VerticiesToMap, IShape2D[] shapes, bool[] IsUpperShape, SliceChordTestType TestsToRun, out OTVTable OTVTable, ref SliceChordRTree chordTree,
                                                          Func<int, int, bool> isLinked = null, PolylineForkPartition forkPartition = null)
        {
            SliceTopologyQuadTrees<IShapeIndex> LevelTree = CreateQuadTreesForShapes(shapes, IsUpperShape);

            ////////////////////////////////////////////////////
            CreateOptimalTilingVertexTable(VerticiesToMap, shapes, IsUpperShape, LevelTree, TestsToRun, out OTVTable, ref chordTree, isLinked, forkPartition);
        }

        public static void CreateOptimalTilingVertexTable(IEnumerable<IShapeIndex> VerticiesToMap, IShape2D[] polygons, bool[] IsUpperShape, SliceTopologyQuadTrees<IShapeIndex> CandidateTreeByLevel, SliceChordTestType TestsToRun,
                                                          out OTVTable Table, ref SliceChordRTree chordTree, Func<int, int, bool> isLinked = null, PolylineForkPartition forkPartition = null)
        {
            Table = new OTVTable();

            List<IShape2D> UpperPolygons = [.. polygons.Where((poly, i) => IsUpperShape[i])];
            List<IShape2D> LowerPolygons = [.. polygons.Where((poly, i) => false == IsUpperShape[i])];

            foreach (var shapeGroup in VerticiesToMap.GroupBy(v => v.ShapeIndex))
            {
                int iPoly = shapeGroup.Key;
                IShape2D shape = polygons[iPoly];

                QuadTreeWithUniqueValues<IShapeIndex> oppositeTreeWithUniqueValues = CandidateTreeByLevel.GetOppositeSide(iPoly);

                bool IsUpper = IsUpperShape[iPoly];
                List<IShape2D> sameLevelShapes = IsUpper ? UpperPolygons : LowerPolygons;
                List<IShape2D> adjacentLevelShapes = IsUpper ? LowerPolygons : UpperPolygons;

                foreach (IShapeIndex i in shapeGroup)
                {
                    Vector2 p1 = i.Point(shape);
                    IShapeIndex NearestOnOtherLevel = FindOptimalTilingForVertexByDistance(i, polygons, sameLevelShapes, adjacentLevelShapes, oppositeTreeWithUniqueValues, chordTree, TestsToRun, isLinked, forkPartition);
                    if (NearestOnOtherLevel is not null)
                    {
                        Table.TryAdd(i, NearestOnOtherLevel);
                    }
                }
            }
        }



        /// <summary>
        /// Find the optimal tiling vertex for the passed verticies
        /// </summary>
        /// <param name="VerticiesToMap"></param>
        /// <param name="polygons"></param>
        /// <param name="PolyZ"></param>
        /// <param name="OTVTable"></param>
        public static void CreateOptimalTilingVertexTable(this BajajGeneratorMesh mesh, IEnumerable<MorphMeshVertex> VerticiesToMap, SliceChordTestType TestsToRun, out Dictionary<MorphMeshVertex, MorphMeshVertex> OTVTable, ref SliceChordRTree chordTree)
        {
            var LevelTree = mesh.CreateQuadTreesForContours();

            ////////////////////////////////////////////////////
            CreateOptimalTilingVertexTable(mesh, GroupIncompleteVerticesByShape([.. VerticiesToMap]), LevelTree, TestsToRun, out OTVTable, ref chordTree);
        }

        public static void CreateOptimalTilingVertexTable(this BajajGeneratorMesh mesh, IEnumerable<MorphMeshVertex> VerticiesToMap, SliceTopologyQuadTrees<MorphMeshVertex> CandidateTreeByLevel, SliceChordTestType TestsToRun,
                                                          out Dictionary<MorphMeshVertex, MorphMeshVertex> OTVTable, ref SliceChordRTree chordTree)
        {
            CreateOptimalTilingVertexTable(mesh, GroupIncompleteVerticesByShape([.. VerticiesToMap]), CandidateTreeByLevel, TestsToRun, out OTVTable, ref chordTree);
        }

        public static void CreateOptimalTilingVertexTable(this BajajGeneratorMesh mesh, Dictionary<int, List<MorphMeshVertex>> incompleteByShape, SliceTopologyQuadTrees<MorphMeshVertex> CandidateTreeByLevel, SliceChordTestType TestsToRun,
                                                          out Dictionary<MorphMeshVertex, MorphMeshVertex> OTVTable, ref SliceChordRTree chordTree)
        {
            OTVTable = new Dictionary<MorphMeshVertex, MorphMeshVertex>();

            foreach (KeyValuePair<int, List<MorphMeshVertex>> polygroup in incompleteByShape)
            {
                if (polygroup.Value.Count == 0)
                    continue;

                int iPoly = polygroup.Key;
                QuadTreeWithUniqueValues<MorphMeshVertex> treeWithUniqueValues = CandidateTreeByLevel.GetOppositeSide(iPoly);

                bool IsUpperShape = mesh.UpperShapeIndicies.Contains(iPoly);
                IShape2D[] SameLevelShapes = IsUpperShape ? mesh.UpperShapes : mesh.LowerShapes;
                IShape2D[] AdjacentLevelShapes = IsUpperShape ? mesh.LowerShapes : mesh.UpperShapes;

                foreach (MorphMeshVertex v in polygroup.Value)
                {
                    if (v.FacesAreComplete)
                        continue;

                    MorphMeshVertex NearestOnOtherLevel = mesh.FindOptimalTilingForVertexByDistance(v, SameLevelShapes, AdjacentLevelShapes, treeWithUniqueValues, chordTree, TestsToRun);
                    if (NearestOnOtherLevel != null)
                        OTVTable.TryAdd(v, NearestOnOtherLevel);
                }
            }
        }
        /*
        public static SortedList<int, QuadTreeWithUniqueValues<MorphMeshVertex>> CreateQuadTreesForContours(this MorphRenderMesh mesh)
        {
            SortedList<int, QuadTreeWithUniqueValues<MorphMeshVertex>> LevelTree = new SortedList<int, QuadTreeWithUniqueValues<MorphMeshVertex>>();

            //Build a quad treeWithUniqueValues of all points at a given level
            foreach (double Z in mesh.PolyZ.Distinct())
            {
                Polygon[] ShapesOnLevel = mesh.Polygons.Where((p, i) => mesh.PolyZ[i] == Z).ToArray();
                Rectangle bbox = ShapesOnLevel.BoundingBox();
                bbox.Scale(1.05);
                LevelTree.Add((int)Z, new QuadTreeWithUniqueValues<MorphMeshVertex>(bbox));
            }

            var VertsByZLevel = mesh.MorphVerticies.Where(v => v.Type == VertexOrigin.CONTOUR).GroupBy(v => Math.Round(v.Position.Z));
            foreach(var ZLevel in VertsByZLevel)
            {
                double Z = (int)ZLevel.Key;
                QuadTreeWithUniqueValues<MorphMeshVertex> treeWithUniqueValues = LevelTree[(int)Z];
                foreach(var vertex in ZLevel)
                {
                    treeWithUniqueValues.Add(vertex.Position.XY(), vertex);
                }
            }

            return LevelTree;
        }
        */

        public static SliceTopologyQuadTrees<MorphMeshVertex> CreateQuadTreesForContours(this BajajGeneratorMesh mesh)
        {
            QuadTreeWithUniqueValues<MorphMeshVertex> Above = BuildQuadTreeForPolyGroup(mesh, mesh.UpperShapeIndicies);
            QuadTreeWithUniqueValues<MorphMeshVertex> Below = BuildQuadTreeForPolyGroup(mesh, mesh.LowerShapeIndicies);

            return new SliceTopologyQuadTrees<MorphMeshVertex>(Above, Below, mesh.UpperShapeIndicies, mesh.LowerShapeIndicies);
        }

        private static QuadTreeWithUniqueValues<MorphMeshVertex> BuildQuadTreeForPolyGroup(BajajGeneratorMesh mesh, IReadOnlyList<int> polyset)
        {
            if (polyset.Count == 0)
            {
                return new QuadTreeWithUniqueValues<MorphMeshVertex>();
            }

            var ShapesOnLevel = polyset.Select(iPoly => mesh.Shapes[iPoly]);
            Rectangle bbox = ShapesOnLevel.BoundingBox();
            bbox = Rectangle.Scale(bbox, 1.05);
            QuadTreeWithUniqueValues<MorphMeshVertex> quadTreeWithUniqueValues = new(bbox);

            var Verts = mesh.MorphVerticies.Where(v => v.Type == VertexOrigin.CONTOUR && v.ShapeIndex is not null && polyset.Contains(v.ShapeIndex.ShapeIndex));
            foreach (var vertex in Verts)
            {
                quadTreeWithUniqueValues.TryAdd(vertex.Position.XY(), vertex);
            }

            return quadTreeWithUniqueValues;
        }


        /// <summary>
        /// Build a QuadTreeWithUniqueValues for each Z level containing all points in the polygons on that level
        /// </summary>
        /// <param name="polygons"></param>
        /// <param name="PolyZ"></param>
        /// <returns></returns>
        public static SliceTopologyQuadTrees<IShapeIndex> CreateQuadTreesForShapes(in IReadOnlyList<IShape2D> shapes, bool[] IsUpperShape)
        {
            var shapedata = shapes.Select((shape, i) => new { shape = shape, index = i }).ToArray();
            var upper_lower_groups = shapedata.GroupBy(data => IsUpperShape[data.index]);

            var UpperPolyData = upper_lower_groups.First(group => group.Key == true).Select(group => group);
            var LowerPolyData = upper_lower_groups.First(group => group.Key == false).Select(group => group);

            ImmutableArray<int> UpperPolyIndicies = [.. UpperPolyData.Select(data => data.index)];
            ImmutableArray<int> LowerPolyIndicies = [.. LowerPolyData.Select(data => data.index)];

            QuadTreeWithUniqueValues<IShapeIndex> Above = BuildQuadTreeForPolyGroup([.. UpperPolyData.Select(data => data.shape)],
                                                                                    UpperPolyIndicies);

            QuadTreeWithUniqueValues<IShapeIndex> Below = BuildQuadTreeForPolyGroup([.. LowerPolyData.Select(data => data.shape)],
                                                                                    LowerPolyIndicies);

            return new SliceTopologyQuadTrees<IShapeIndex>(Above, Below, UpperPolyIndicies, LowerPolyIndicies);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ShapesOnLevel"></param>
        /// <param name="iPolyLookup">Index of polygon we should use for PointIndex creation</param>
        /// <returns></returns>
        private static QuadTreeWithUniqueValues<IShapeIndex> BuildQuadTreeForPolyGroup(IShape2D[] ShapesOnLevel, IReadOnlyList<int> iPolyLookup)
        {
            Rectangle bbox = ShapesOnLevel.BoundingBox();
            bbox = Rectangle.Scale(bbox, 1.05);
            QuadTreeWithUniqueValues<IShapeIndex> quadTreeWithUniqueValues = new(bbox);

            for (int i = 0; i < ShapesOnLevel.Length; i++)
            {
                int iShape = iPolyLookup[i];
                IShape2D shape = ShapesOnLevel[i];

                if (shape is Polygon poly)
                {
                    foreach (PolygonIndex pIndex in new PolygonVertexEnum(poly, iShape))
                    {
                        Vector2 p1 = pIndex.Point(poly);
                        quadTreeWithUniqueValues.Add(p1, pIndex);
                    }
                }
                else if (shape is Polyline line)
                {
                    foreach (PolylineIndex pIndex in new PolylineVertexEnum(line, iShape))
                    {
                        Vector2 p1 = pIndex.Point(line);
                        quadTreeWithUniqueValues.Add(p1, pIndex);
                    }
                }
            }

            return quadTreeWithUniqueValues;
        }

        /*
        /// <summary>
        /// Build a QuadTreeWithUniqueValues for each Z level containing all points in the polygons on that level
        /// </summary>
        /// <param name="polygons"></param>
        /// <param name="PolyZ"></param>
        /// <returns></returns>
        public static SortedList<int, QuadTreeWithUniqueValues<PointIndex>> CreateQuadTreesForShapes(IReadOnlyList<Polygon> polygons, double[] PolyZ)
        {
            SortedList<int, QuadTreeWithUniqueValues<PointIndex>> LevelTree = new SortedList<int, QuadTreeWithUniqueValues<PointIndex>>();

            //Build a quad treeWithUniqueValues of all points at a given level
            foreach (double Z in PolyZ.Distinct())
            {
                LevelTree.Add((int)Z, new QuadTreeWithUniqueValues<PointIndex>(polygons.Where((p,i) => PolyZ[i] == Z).ToArray().BoundingBox()));
            }

            for (int iPoly = 0; iPoly < polygons.Count; iPoly++)
            {
                Polygon poly = polygons[iPoly];
                int Z = (int)PolyZ[iPoly];
                if (PolyZ.Contains(Z) == false)
                    continue; 

                QuadTreeWithUniqueValues<PointIndex> treeWithUniqueValues = LevelTree[Z];
                foreach (PointIndex i in new PolygonVertexEnum(poly, iPoly))
                {
                    Vector2 p1 = i.Point(poly);
                    treeWithUniqueValues.Add(p1, i);
                }
            }

            return LevelTree;
        }
        */

        /// <summary>
        /// 
        /// </summary>
        /// <param name="PolysOnLevel"></param>
        /// <param name="iPolyLookup">Index of polygon we should use for PointIndex creation</param>
        /// <returns></returns>
        private static QuadTreeWithUniqueValues<PolygonIndex> BuildQuadTreeForPolyGroup(IEnumerable<PolygonIndex> Candidates, IReadOnlyList<Polygon> PointIndexablePolygons, Polygon[] PolysOnLevel)
        {
            Rectangle bbox = PolysOnLevel.BoundingBox();
            bbox = Rectangle.Scale(bbox, 1.05);
            QuadTreeWithUniqueValues<PolygonIndex> quadTreeWithUniqueValues = new(bbox);

            foreach (var VertGroup in Candidates.GroupBy(p => p.ShapeIndex))
            {
                int iPoly = VertGroup.Key;
                Polygon poly = PointIndexablePolygons[iPoly];

                foreach (PolygonIndex i in VertGroup)
                {
                    Vector2 p1 = i.Point(poly);
                    quadTreeWithUniqueValues.Add(p1, i);
                }
            }

            return quadTreeWithUniqueValues;
        }
    }
}
