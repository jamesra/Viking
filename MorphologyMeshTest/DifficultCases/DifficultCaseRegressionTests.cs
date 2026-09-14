using AnnotationVizLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MorphologyMeshTest.DifficultCases
{
    /// <summary>
    /// Every slice in <c>difficult-cases.json</c> must still mesh to a complete surface, both on the raw annotations
    /// and after the curvefit BajajMultiTest applies under <c>--correction all</c>, because a fix for one difficult
    /// slice has repeatedly re-broken another.  Add a line to the file when a new slice is fixed; never remove one.
    /// Whether the mesh still <em>looks</em> right is checked separately against baseline images by the
    /// mesh-difficult-cases skill.
    /// </summary>
    [TestClass]
    public class DifficultCaseRegressionTests
    {
        //Curvefit reads up to ±7 neighbours along each process; three hops loads enough of the shaft that the
        //fixture matches the whole-cell result BajajMultiTest sees.
        private const int LoadHops = 3;

        public static IEnumerable<object[]> Cases()
        {
            foreach (DifficultCase c in DifficultCaseList.Load())
            {
                // Open entries are failures still under investigation; asserting a complete surface would
                // fail the whole suite until each is fixed.  They stay in difficult-cases.json for tracking.
                if (c.Open)
                    continue;

                yield return [c, false];
                yield return [c, true];
            }
        }

        public static string CaseDisplayName(System.Reflection.MethodInfo _, object[] data) =>
            $"{data[0]} [{((bool)data[1] ? "curvefit" : "raw")}]";

        [DataTestMethod]
        [TestCategory("LiveData")]
        [TestCategory("DifficultCases")]
        [Timeout(300000)]
        [DynamicData(nameof(Cases), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(CaseDisplayName))]
        public async Task DifficultSliceMeshesToCompleteSurface(DifficultCase difficultCase, bool curveFit)
        {
            Console.WriteLine($"{difficultCase}: {difficultCase.Description}");

            MorphologyGraph morphology = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync(
                [.. difficultCase.Locations.Select(id => (long)id)], difficultCase.Endpoint, LoadHops);
            foreach (ulong id in difficultCase.Locations)
                Assert.IsTrue(morphology.Nodes.ContainsKey(id), $"Location {id} was not returned by {difficultCase.Endpoint}");

            if (curveFit)
                MorphologyGraph.CurveFitProcesses(morphology);

            SliceGraph slices = await SliceGraph.Create(morphology, ContourSimplifyOptions.Default);
            Slice target = slices.Nodes.Values.FirstOrDefault(s => difficultCase.Locations.All(id => s.AllNodes.Contains(id)));
            Assert.IsNotNull(target, $"No slice contains all of {string.Join(",", difficultCase.Locations)}");

            if (slices.FailedTopologySlices.TryGetValue(target.Key, out var topologyFailure))
                Assert.Fail($"Slice topology failed: {topologyFailure}");

            SliceTopology topology = slices.GetTopology(target);
            Assert.IsTrue(topology.IsValid, "Slice topology is not valid");

            BajajGeneratorMesh mesh = new(topology, target);
            try
            {
                BajajMeshGenerator.GenerateFaces(mesh);
            }
            catch (Exception e)
            {
                Assert.Fail($"GenerateFaces threw {e.GetType().Name}: {e.Message}");
            }

            MeshManifoldReport report = mesh.ManifoldReport;
            Console.WriteLine($"faces={mesh.Faces.Count} verts={mesh.Vertices.Count} report: {report}");
            foreach (var kv in mesh.Edges.Where(k => k.Value.Faces.Count is 0 or > 2 || (k.Value.Faces.Count == 1 && !mesh.IsRibbonBoundaryEdge(k.Key))))
            {
                MorphMeshEdge edge = (MorphMeshEdge)kv.Value;
                Console.WriteLine($"  defect {kv.Key.A}[{mesh[kv.Key.A].ShapeIndex}]-{kv.Key.B}[{mesh[kv.Key.B].ShapeIndex}] type={edge.Type} faces={edge.Faces.Count}");
            }

            Assert.IsFalse(mesh.GenerationHadErrors, $"Face generation reported errors: {report}");
            Assert.IsTrue(report.IsValidSliceSurface, $"Mesh is not a complete surface: {report}");
        }

        [TestMethod]
        public void DifficultCaseListParses()
        {
            IReadOnlyList<DifficultCase> cases = DifficultCaseList.Load();
            Assert.IsTrue(cases.Count > 0, "difficult-cases.json has no cases");
            int open = 0;
            foreach (DifficultCase c in cases)
            {
                Assert.IsNotNull(c.Endpoint, $"{c} has no resolvable endpoint");
                Assert.IsTrue(c.Locations.Length >= 1, $"{c} has no locations");
                if (c.Open)
                    open++;
            }

            Assert.AreEqual(cases.Count, cases.Select(c => c.Key).Distinct().Count(), "Duplicate cases in difficult-cases.json");
            Assert.IsTrue(cases.Count > open, "Expected at least one closed (regression-tested) case");
            Console.WriteLine($"difficult-cases: total={cases.Count} closed={cases.Count - open} open={open}");
        }
    }
}
