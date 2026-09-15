using AnnotationVizLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MorphologyMeshTest
{
    /// <summary>
    /// LiveData harness for the RC1 410 ChordGeneration / OTV long pole (locs 200625…).
    /// Prints <see cref="ChordGenStats"/> for H1–H6; asserts a complete surface. Does not assert wall clock.
    /// </summary>
    [TestClass]
    public class ChordGenLongPoleHarnessTests
    {
        private const int LoadHops = 3;
        static readonly ulong[] LongPoleLocations = [200625, 200626, 201467, 1420185];

        [TestMethod]
        [TestCategory("LiveData")]
        [TestCategory("ChordGenHarness")]
        [Timeout(600000)]
        public async Task Rc1_410_LongPole_ChordGenStatsAndManifold()
        {
            Uri endpoint = Viking.Common.ODataEndpointCatalog.EndpointMap[Viking.Common.Endpoint.RC1];
            MorphologyGraph morph = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync(
                [.. LongPoleLocations.Select(id => (long)id)], endpoint, LoadHops);

            SliceGraph slices = await SliceGraph.Create(morph, ContourSimplifyOptions.Default);
            Slice target = slices.Nodes.Values.FirstOrDefault(s => LongPoleLocations.All(id => s.AllNodes.Contains(id)));
            Assert.IsNotNull(target, $"No slice contains all of {string.Join(",", LongPoleLocations)}");
            if (slices.FailedTopologySlices.TryGetValue(target.Key, out var topoFail))
                Assert.Fail($"Slice topology failed: {topoFail}");

            ChordGenStats.Enabled = true;
            ChordGenStats.Reset();
            MeshPhaseTimings.Enabled = true;
            MeshPhaseTimings.Reset();

            BajajGeneratorMesh mesh = new(slices.GetTopology(target), target);
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                BajajMeshGenerator.GenerateFaces(mesh);
            }
            catch (Exception e)
            {
                Assert.Fail($"GenerateFaces threw {e.GetType().Name}: {e.Message}");
            }

            sw.Stop();
            MeshManifoldReport report = mesh.ManifoldReport;
            Console.WriteLine($"wallMs={sw.ElapsedMilliseconds} faces={mesh.Faces.Count} verts={mesh.Vertices.Count} {report}");
            Console.WriteLine(ChordGenStats.Format());
            Console.WriteLine(MeshPhaseTimings.Report());

            // Hypothesis labels for the log (not asserted): H1 otvRebuilds, H2 findNearest, H3 isValid/permCache,
            // H4 unchangedOtv vs otvVertexEvals, H5 (Theorem4 inside isValid), H6 vertsEvicted.
            Assert.IsTrue(ChordGenStats.ChordPassCalls > 0, "Expected at least one chord pass");
            Assert.IsTrue(ChordGenStats.OtvRebuilds > 0, "Expected OTV rebuilds");
            Assert.IsFalse(mesh.GenerationHadErrors, $"Face generation reported errors: {report}");
            Assert.IsTrue(report.IsValidSliceSurface, $"Mesh is not a complete surface: {report}");
        }

        [TestMethod]
        [TestCategory("LiveData")]
        [TestCategory("ChordGenHarness")]
        [Timeout(600000)]
        public async Task Rc1_410_LongPole_ChordSetFingerprint()
        {
            Uri endpoint = Viking.Common.ODataEndpointCatalog.EndpointMap[Viking.Common.Endpoint.RC1];
            MorphologyGraph morph = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync(
                [.. LongPoleLocations.Select(id => (long)id)], endpoint, LoadHops);

            SliceGraph slices = await SliceGraph.Create(morph, ContourSimplifyOptions.Default);
            Slice target = slices.Nodes.Values.FirstOrDefault(s => LongPoleLocations.All(id => s.AllNodes.Contains(id)));
            Assert.IsNotNull(target);

            BajajGeneratorMesh mesh = new(slices.GetTopology(target), target);
            BajajMeshGenerator.GenerateFaces(mesh);

            List<string> chords = [];
            foreach (var kv in mesh.Edges)
            {
                MorphMeshEdge edge = (MorphMeshEdge)kv.Value;
                if (edge.Type == EdgeType.CONTOUR || edge.Type == EdgeType.ARTIFICIAL || edge.Type == EdgeType.CORRESPONDING)
                    continue;
                int a = Math.Min(kv.Key.A, kv.Key.B);
                int b = Math.Max(kv.Key.A, kv.Key.B);
                chords.Add($"{a}-{b}:{edge.Type}");
            }

            chords.Sort(StringComparer.Ordinal);
            Console.WriteLine($"chordCount={chords.Count}");
            Console.WriteLine(string.Join("|", chords));
            Assert.IsTrue(chords.Count > 0);
            Assert.IsTrue(mesh.ManifoldReport.IsValidSliceSurface);
        }
    }
}
