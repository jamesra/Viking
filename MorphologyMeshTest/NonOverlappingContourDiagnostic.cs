using AnnotationVizLib;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Live-data harness for the non-overlapping contour investigation.  Meshes a real cell and reports, per slice,
    /// whether anything tiled across the gap between the two sections.  A slice with no cross-band faces is a visible
    /// gap in the model, so grouping those by whether the slice's contours overlapped to begin with says whether the
    /// gaps are the virtual overlap pass's problem or somewhere else entirely.
    /// </summary>
    [TestClass]
    public class NonOverlappingContourDiagnostic
    {
        private static readonly Uri Endpoint = new("http://websvc.codepharm.net/RC1/OData");

        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(600000)]
        public async Task MeshCell476AndReportSliceOutcomes()
        {
            MorphologyGraph root = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync(
                new long[] { 476 }, false, Endpoint);

            MorphologyGraph cell = root.Subgraphs[476];
            Vector2 origin = cell.NodesBoundingBox.CenterPoint.XY();
            Console.WriteLine($"Cell 476: {cell.Nodes.Count} nodes");

            SliceGraph slices = await SliceGraph.Create(cell, 2.0, origin);
            Console.WriteLine($"Slices: {slices.Nodes.Count}");

            //The generator swallows per-slice failures and traces them, so the only way to see how often it is
            //failing is to collect what it writes.
            CollectingTraceListener collected = new();
            System.Diagnostics.Trace.Listeners.Add(collected);

            List<BajajGeneratorMesh> meshes;
            try
            {
                meshes = await BajajMeshGenerator.ConvertToMesh(slices);
            }
            finally
            {
                System.Diagnostics.Trace.Listeners.Remove(collected);
            }

            Console.WriteLine($"Meshes: {meshes.Count}, faces {meshes.Sum(m => (long)m.Faces.Count)}");

            ReportTracedFailures(collected.Lines);

            int tiled = 0;
            int gaps = 0;
            Dictionary<string, int> gapsByCategory = [];
            Dictionary<string, int> tiledByCategory = [];
            List<string> gapSliceIds = [];
            List<string> tiledSliceIds = [];

            foreach (BajajGeneratorMesh mesh in meshes)
            {
                string category = Categorize(mesh.Topology);
                bool anyCrossBand = mesh.MorphFaces.Any(f => IsCrossBand(mesh, f));

                if (anyCrossBand)
                {
                    tiled++;
                    tiledByCategory[category] = tiledByCategory.GetValueOrDefault(category) + 1;

                    //A handful of healthy slices, to use as controls when verifying the viewer itself.
                    if (tiledSliceIds.Count < 4 && mesh.Topology.Shapes.Length == 2)
                    {
                        int crossBand = mesh.MorphFaces.Count(f => IsCrossBand(mesh, f));
                        tiledSliceIds.Add($"  {string.Join(",", mesh.Topology.ShapeIndexToMorphNodeIndex.Distinct().OrderBy(n => n))}   [{crossBand} cross-band faces]");
                    }
                }
                else
                {
                    gaps++;
                    gapsByCategory[category] = gapsByCategory.GetValueOrDefault(category) + 1;

                    //Print the LocationIDs so the slice can be replayed in the BAJAJTEST viewer.
                    gapSliceIds.Add($"  {string.Join(",", mesh.Topology.ShapeIndexToMorphNodeIndex.Distinct().OrderBy(n => n))}   [{category}]");
                }
            }

            Console.WriteLine($"\nSlices that tiled across the gap: {tiled}");
            Console.WriteLine($"Slices with NO cross-band faces (visible gaps): {gaps}");

            Console.WriteLine("=== Gap slices, as LocationIDs for --repro-locations ===");
            foreach (string line in gapSliceIds)
                Console.WriteLine(line);

            Console.WriteLine("=== Healthy control slices, as LocationIDs for --repro-locations ===");
            foreach (string line in tiledSliceIds)
                Console.WriteLine(line);

            Console.WriteLine("\nGaps by slice category:");
            foreach (var kv in gapsByCategory.OrderByDescending(k => k.Value))
                Console.WriteLine($"  {kv.Key}: {kv.Value}");

            Console.WriteLine("\nTiled by slice category:");
            foreach (var kv in tiledByCategory.OrderByDescending(k => k.Value))
                Console.WriteLine($"  {kv.Key}: {kv.Value}");

            ReportSurfaceQuality(meshes);
        }

        /// <summary>
        /// Almost every slice tiles, so a visible gap has to be a hole inside a slice that did tile rather than a
        /// slice that produced nothing.  Counts the manifold defects that leave those holes.
        /// </summary>
        private static void ReportSurfaceQuality(IReadOnlyList<BajajGeneratorMesh> meshes)
        {
            long holes = 0;
            long nonManifold = 0;
            long inconsistent = 0;
            long isolated = 0;
            int slicesWithHoles = 0;
            int slicesWithNonManifold = 0;
            int slicesClean = 0;
            Dictionary<int, int> holesPerSlice = [];

            foreach (BajajGeneratorMesh mesh in meshes)
            {
                MeshManifoldReport report = MeshManifoldValidator.Validate(mesh);

                holes += report.UnexpectedBoundaryEdges;
                nonManifold += report.NonManifoldEdges;
                inconsistent += report.InconsistentManifoldEdges;
                isolated += report.IsolatedEdges;

                if (report.UnexpectedBoundaryEdges > 0)
                    slicesWithHoles++;

                if (report.NonManifoldEdges > 0)
                    slicesWithNonManifold++;

                if (report.IsValidSliceSurface)
                    slicesClean++;

                int bucket = Math.Min(report.UnexpectedBoundaryEdges, 10);
                holesPerSlice[bucket] = holesPerSlice.GetValueOrDefault(bucket) + 1;
            }

            //A contour edge takes one face from this slice and one from the adjacent slice.  Two faces here means the
            //slice sealed its own seam, which would wall off the neighbour rather than joining it.
            long contourWithTwoFaces = 0;
            int slicesSealingSeam = 0;
            int sealedAndLegitimatelyCapped = 0;
            int sealedInteriorSlices = 0;
            long sealedEdgesInInteriorSlices = 0;

            foreach (BajajGeneratorMesh mesh in meshes)
            {
                long overFaced = mesh.Edges.Values.Count(e => e is MorphMeshEdge m && m.Type == EdgeType.CONTOUR && e.Faces.Count > 1);
                contourWithTwoFaces += overFaced;
                if (overFaced == 0)
                    continue;

                slicesSealingSeam++;

                //A slice at the end of a branch is capped on purpose, so its seam edges legitimately gain a second
                //face.  A slice with a neighbour on both sides has no such excuse.
                bool capped = mesh.Slice is not null && (mesh.Slice.HasSliceAbove == false || mesh.Slice.HasSliceBelow == false);
                if (capped)
                {
                    sealedAndLegitimatelyCapped++;
                }
                else
                {
                    sealedInteriorSlices++;
                    sealedEdgesInInteriorSlices += overFaced;
                }
            }

            ReportSharedContourAgreement(meshes);

            //A slice that is watertight on its own is a bead: it has no opening for the neighbouring slice to join.
            int closedSurfaces = 0;
            int closedInterior = 0;
            int noOpenSeamAtAll = 0;
            foreach (BajajGeneratorMesh mesh in meshes)
            {
                MeshManifoldReport r = MeshManifoldValidator.Validate(mesh, mesh.IsForkGapBoundaryEdge);
                bool interior = mesh.Slice is not null && mesh.Slice.HasSliceAbove && mesh.Slice.HasSliceBelow;

                if (r.ContourBoundaryEdges == 0)
                {
                    noOpenSeamAtAll++;
                    if (interior)
                        closedInterior++;
                }

                if (r.IsClosed)
                    closedSurfaces++;
            }

            Console.WriteLine($"\n=== Beads: slices with no opening left for a neighbour ===");
            Console.WriteLine($"  fully watertight slice meshes:            {closedSurfaces}");
            Console.WriteLine($"  slices with no open contour seam at all:  {noOpenSeamAtAll}");
            Console.WriteLine($"    of those, INTERIOR slices (beads):      {closedInterior}");

            Console.WriteLine($"\n=== Seam invariant ===");
            Console.WriteLine($"  contour edges carrying 2+ faces:        {contourWithTwoFaces}");
            Console.WriteLine($"  slices sealing their own seam:          {slicesSealingSeam}");
            Console.WriteLine($"    of those, capped branch ends (legit): {sealedAndLegitimatelyCapped}");
            Console.WriteLine($"    of those, INTERIOR slices (wrong):    {sealedInteriorSlices}");
            Console.WriteLine($"    seam edges over-faced in interior:    {sealedEdgesInInteriorSlices}");

            Console.WriteLine($"\n=== Surface quality across {meshes.Count} slice meshes ===");
            Console.WriteLine($"  clean slice surfaces:        {slicesClean}");
            Console.WriteLine($"  slices with holes:           {slicesWithHoles}");
            Console.WriteLine($"  slices with non-manifold:    {slicesWithNonManifold}");
            Console.WriteLine($"  total hole edges:            {holes}");
            Console.WriteLine($"  total non-manifold edges:    {nonManifold}");
            Console.WriteLine($"  total inconsistent windings: {inconsistent}");
            Console.WriteLine($"  total isolated edges:        {isolated}");

            Console.WriteLine("  hole edges per slice (10 = 10 or more):");
            foreach (var kv in holesPerSlice.OrderBy(k => k.Key))
                Console.WriteLine($"    {kv.Key}: {kv.Value} slices");

            //The worst slices are what a viewer notices, so characterise those rather than the average.
            var severe = meshes
                .Select(m => (mesh: m, report: MeshManifoldValidator.Validate(m)))
                .Where(x => x.report.UnexpectedBoundaryEdges >= 10)
                .ToList();

            Console.WriteLine($"\n=== The {severe.Count} worst slices (10+ hole edges) ===");
            Console.WriteLine($"  shape counts: {string.Join(", ", severe.Select(x => x.mesh.Topology.Shapes.Length).GroupBy(c => c).OrderBy(g => g.Key).Select(g => $"{g.Key} shapes x{g.Count()}"))}");
            Console.WriteLine($"  branch slices (>2 shapes): {severe.Count(x => x.mesh.Topology.Shapes.Length > 2)} of {severe.Count}");
            Console.WriteLine($"  had virtual overlap applied: {severe.Count(x => x.mesh.Topology.HasVirtualOverlapTranslation)}");
            Console.WriteLine($"  total hole edges in these:  {severe.Sum(x => x.report.UnexpectedBoundaryEdges)}");

            Console.WriteLine("  region types in the worst slices:");
            Dictionary<string, int> regions = [];
            foreach (var x in severe)
            {
                x.mesh.IdentifyRegionsViaFaces();
                foreach (var r in x.mesh.Regions)
                    regions[r.Type.ToString()] = regions.GetValueOrDefault(r.Type.ToString()) + 1;
            }

            foreach (var kv in regions.OrderByDescending(k => k.Value))
                Console.WriteLine($"    {kv.Key}: {kv.Value}");

            ReportBoundaryLoopShape(severe.Select(x => x.mesh));
        }

        /// <summary>
        /// Decomposes the boundary of each mesh into loops and classifies them.  A loop of purely non-contour edges is
        /// an interior hole that region closing could triangulate.  A loop mixing contour and non-contour edges is a
        /// hole that runs into the contour seam, which cannot be filled without putting a second face on a seam edge
        /// and sealing off the neighbouring slice.  Which of the two dominates decides how the defect has to be fixed.
        /// </summary>
        private static void ReportBoundaryLoopShape(IEnumerable<BajajGeneratorMesh> meshes)
        {
            int pureContour = 0;
            int pureHole = 0;
            int mixed = 0;
            int danglingVerts = 0;

            foreach (BajajGeneratorMesh mesh in meshes)
            {
                //Adjacency over boundary edges only: every vertex on a well formed boundary has exactly two.
                Dictionary<int, List<(int Other, bool IsContour)>> adjacency = [];
                foreach (var kvp in mesh.Edges)
                {
                    if (kvp.Value.Faces.Count != 1)
                        continue;

                    bool isContour = kvp.Value is MorphMeshEdge m && m.Type == EdgeType.CONTOUR;
                    int a = kvp.Key.A;
                    int b = kvp.Key.B;

                    if (adjacency.TryGetValue(a, out var la) == false)
                        adjacency[a] = la = [];
                    if (adjacency.TryGetValue(b, out var lb) == false)
                        adjacency[b] = lb = [];

                    la.Add((b, isContour));
                    lb.Add((a, isContour));
                }

                danglingVerts += adjacency.Count(kv => kv.Value.Count == 1);

                //Walk connected components of the boundary graph and classify by the edge types they contain.
                HashSet<int> visited = [];
                foreach (int start in adjacency.Keys)
                {
                    if (visited.Add(start) == false)
                        continue;

                    Queue<int> queue = new([start]);
                    bool sawContour = false;
                    bool sawNonContour = false;

                    while (queue.Count > 0)
                    {
                        int current = queue.Dequeue();
                        foreach (var (other, isContour) in adjacency[current])
                        {
                            if (isContour)
                                sawContour = true;
                            else
                                sawNonContour = true;

                            if (visited.Add(other))
                                queue.Enqueue(other);
                        }
                    }

                    if (sawContour && sawNonContour)
                        mixed++;
                    else if (sawContour)
                        pureContour++;
                    else
                        pureHole++;
                }
            }

            Console.WriteLine("\n=== Shape of the boundary in the worst slices ===");
            Console.WriteLine($"  loops of contour seam only (expected):        {pureContour}");
            Console.WriteLine($"  loops of non-contour only (fillable holes):   {pureHole}");
            Console.WriteLine($"  loops mixing seam and hole (cannot fill):     {mixed}");
            Console.WriteLine($"  boundary verts with only one boundary edge:    {danglingVerts}");
        }

        /// <summary>
        /// The composite welds slices together keyed on (morph node, vertex index), so every slice that shares a
        /// contour has to agree on that contour's vertex list at the moment its mesh is built.  Measured here from
        /// the topology each finished mesh actually used, which is the state the weld key is derived from.
        /// </summary>
        private static void ReportSharedContourAgreement(IReadOnlyList<BajajGeneratorMesh> meshes)
        {
            //Matching vertex counts do not imply matching vertex lists: two slices can each insert the same number of
            //corresponding points at different places around the ring.  The weld key is the index, so the ordered
            //positions are what has to agree.  Virtual overlap is subtracted out so a moved shape is compared in its
            //canonical position rather than counted as a difference.
            //Expressed relative to the first vertex, so a contour that a slice merely moved for virtual overlap
            //compares equal to its untranslated twin.  That is the right test: the mesh restores the translation
            //afterwards, so welding only needs the two slices to list the same points in the same index order.
            static string Signature(IShape2D s)
            {
                Vector2[] points = s switch
                {
                    Polygon p => p.ExteriorRing,
                    Polyline l => [.. l.Points.Select(pt => new Vector2(pt.X, pt.Y))],
                    _ => []
                };

                if (points.Length == 0)
                    return string.Empty;

                Vector2 origin = points[0];
                return string.Join(";", points.Select(pt => $"{pt.X - origin.X:F2},{pt.Y - origin.Y:F2}"));
            }

            static int VertCount(IShape2D s) => s switch
            {
                Polygon p => p.ExteriorRing.Length,
                Polyline l => l.PointCount,
                _ => -1
            };

            Dictionary<ulong, List<(ulong Slice, int Count, string Sig, bool Translated)>> byNode = [];

            foreach (BajajGeneratorMesh mesh in meshes)
            {
                SliceTopology t = mesh.Topology;
                if (t.Shapes is null || t.ShapeIndexToMorphNodeIndex is null)
                    continue;

                for (int i = 0; i < t.Shapes.Length; i++)
                {
                    ulong node = t.ShapeIndexToMorphNodeIndex[i];
                    Vector2 offset = t.HasVirtualOverlapTranslation ? t.GetVirtualOverlapOffset(i) : Vector2.Zero;

                    if (byNode.TryGetValue(node, out var list) == false)
                        byNode[node] = list = [];

                    list.Add((mesh.Slice?.Key ?? 0, VertCount(t.Shapes[i]), Signature(t.Shapes[i]), offset != Vector2.Zero));
                }
            }

            var shared = byNode.Where(kv => kv.Value.Select(v => v.Slice).Distinct().Count() > 1).ToList();
            var countDisagree = shared.Where(kv => kv.Value.Select(v => v.Count).Distinct().Count() > 1).ToList();
            var listDisagree = shared.Where(kv => kv.Value.Select(v => v.Sig).Distinct().Count() > 1).ToList();
            var sameCountDifferentPoints = listDisagree.Where(kv => kv.Value.Select(v => v.Count).Distinct().Count() == 1).ToList();

            Console.WriteLine($"\n=== Shared contour agreement at mesh build time ===");
            Console.WriteLine($"  contours used by a mesh:                      {byNode.Count}");
            Console.WriteLine($"  ...shared by 2+ slices:                       {shared.Count}");
            Console.WriteLine($"  ...disagree on vertex COUNT:                  {countDisagree.Count}");
            Console.WriteLine($"  ...disagree on the ordered vertex LIST:       {listDisagree.Count}");
            Console.WriteLine($"     of those, same count but different points: {sameCountDifferentPoints.Count}");

            long affectedEdges = listDisagree.Sum(kv => (long)kv.Value.Max(v => v.Count));
            Console.WriteLine($"  contour edges on contours that disagree:       {affectedEdges}");

            Console.WriteLine("\n  sample: same count, different points");
            foreach (var kv in sameCountDifferentPoints.Take(4))
            {
                Console.WriteLine($"    node {kv.Key}:");
                foreach (var v in kv.Value.DistinctBy(v => v.Sig).Take(2))
                    Console.WriteLine($"      slice {v.Slice} ({v.Count} verts{(v.Translated ? ", moved" : "")}): {v.Sig[..Math.Min(150, v.Sig.Length)]}");
            }
        }

        /// <summary>
        /// Buffers everything the mesh generator traces so the harness can count failures it would otherwise swallow.
        /// </summary>
        private sealed class CollectingTraceListener : System.Diagnostics.TraceListener
        {
            private readonly System.Text.StringBuilder _pending = new();

            public List<string> Lines { get; } = [];

            public override void Write(string message) => _pending.Append(message);

            public override void WriteLine(string message)
            {
                lock (Lines)
                {
                    Lines.Add(_pending + message);
                    _pending.Clear();
                }
            }
        }

        /// <summary>
        /// Groups the traced failures by exception type and the frame that threw, so a recurring failure is visible as
        /// one cause with a count rather than thousands of lines in the debug window.
        /// </summary>
        private static void ReportTracedFailures(List<string> lines)
        {
            Dictionary<string, int> byException = [];
            Dictionary<string, int> byOrigin = [];
            int notValidSurface = 0;
            int producedNoMesh = 0;

            foreach (string line in lines)
            {
                if (line.Contains("is not a valid slice surface"))
                    notValidSurface++;

                if (line.Contains("produced no mesh"))
                    producedNoMesh++;

                //Trace writes the whole exception as one entry, so the type and the first frame are both in the text.
                int marker = line.IndexOf("Exception", StringComparison.Ordinal);
                if (marker < 0)
                    continue;

                foreach (string part in line.Split('\n'))
                {
                    string trimmed = part.Trim();
                    if (trimmed.Contains("Exception:") && byExceptionKey(trimmed) is string key)
                        byException[key] = byException.GetValueOrDefault(key) + 1;

                    if (trimmed.StartsWith("at ", StringComparison.Ordinal) && byOrigin.Count < 100000)
                    {
                        string frame = trimmed.Split(" in ")[0];
                        byOrigin[frame] = byOrigin.GetValueOrDefault(frame) + 1;
                        break;
                    }
                }
            }

            Console.WriteLine($"\n=== Traced failures during mesh generation ===");
            Console.WriteLine($"  total traced lines:                  {lines.Count}");
            Console.WriteLine($"  slices reported as invalid surfaces: {notValidSurface}");
            Console.WriteLine($"  slices that produced no mesh:        {producedNoMesh}");

            Console.WriteLine("\n  exceptions by type:");
            foreach (var kv in byException.OrderByDescending(k => k.Value).Take(15))
                Console.WriteLine($"    {kv.Value,6}  {kv.Key}");

            Console.WriteLine("\n  throwing frame:");
            foreach (var kv in byOrigin.OrderByDescending(k => k.Value).Take(15))
                Console.WriteLine($"    {kv.Value,6}  {kv.Key}");

            static string byExceptionKey(string line)
            {
                int end = line.IndexOf("Exception:", StringComparison.Ordinal) + "Exception:".Length;
                return line[..Math.Min(line.Length, end + 90)].Trim();
            }
        }

        /// <summary>
        /// Describes a slice by what the virtual overlap pass had to deal with, so gaps can be attributed.  Overlap is
        /// tested between cross-band pairs only, since that is the relationship a slice chord has to span.
        /// </summary>
        private static string Categorize(SliceTopology topology)
        {
            bool anyDisjoint = false;
            bool anyPair = false;

            for (int i = 0; i < topology.Shapes.Length; i++)
            {
                for (int j = 0; j < topology.Shapes.Length; j++)
                {
                    if (topology.IsUpper[i] == topology.IsUpper[j])
                        continue;

                    anyPair = true;
                    if (topology.Shapes[i].Intersects(topology.Shapes[j]) == false)
                        anyDisjoint = true;
                }
            }

            if (anyPair == false)
                return "no cross-band pair at all";

            if (anyDisjoint == false)
                return "all cross-band pairs already overlap";

            return topology.HasVirtualOverlapTranslation
                ? "had disjoint pairs, virtual overlap APPLIED"
                : "had disjoint pairs, virtual overlap DECLINED";
        }

        private static bool IsCrossBand(BajajGeneratorMesh mesh, MorphMeshFace face)
        {
            SortedSet<int> shapes = [];
            foreach (int iVert in face.iVerts)
            {
                MorphMeshVertex v = mesh[iVert];
                if (v.ShapeIndex is null)
                    return false;

                shapes.Add(v.ShapeIndex.ShapeIndex);
            }

            return shapes.Count >= 2 && shapes.Select(i => mesh.Topology.IsUpper[i]).Distinct().Count() >= 2;
        }
    }
}
