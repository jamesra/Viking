using AnnotationVizLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Re-captures the RC1 structure 410 fixtures that <see cref="ChordGenLongPoleHarnessTests"/> and
    /// <see cref="ProcessRegistrationDiagnostic"/> read, from the live RC1 OData service. Runs only when
    /// VIKING_CAPTURE_FIXTURES=1, and writes into this project's source Testdata folder.
    /// </summary>
    [TestClass]
    public class Rc1Structure410FixtureCapture
    {
        [TestMethod]
        [TestCategory("LiveData")]
        [TestCategory("IntegrationTest")]
        [Timeout(600000)]
        public async Task CaptureRc1Structure410Fixtures()
        {
            if (Environment.GetEnvironmentVariable("VIKING_CAPTURE_FIXTURES") != "1")
                Assert.Inconclusive("Set VIKING_CAPTURE_FIXTURES=1 to re-capture the RC1 structure 410 fixtures from live OData.");

            Uri endpoint = Viking.Common.ODataEndpointCatalog.EndpointMap[Viking.Common.Endpoint.RC1];

            MorphologyGraph longPole = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync(
                [.. ChordGenLongPoleHarnessTests.LongPoleLocations.Select(id => (long)id)], endpoint, ChordGenLongPoleHarnessTests.LoadHops);
            Save(longPole, ChordGenLongPoleHarnessTests.FixtureName, endpoint);

            MorphologyGraph chain = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync(
                [.. ProcessRegistrationDiagnostic.Structure410Targets.Select(id => (long)id)], endpoint, ProcessRegistrationDiagnostic.Structure410Hops);
            Save(chain, ProcessRegistrationDiagnostic.Structure410FixtureName, endpoint);
        }

        private static void Save(MorphologyGraph graph, string name, Uri endpoint)
        {
            string path = MorphologyFixture.SourcePath(name);
            MorphologyFixture.Save(graph, path, endpoint.ToString());
            Console.WriteLine($"{name}: {graph.Nodes.Count} nodes, {graph.Edges.Count} links, {new FileInfo(path).Length / 1024} KiB");
        }
    }
}
