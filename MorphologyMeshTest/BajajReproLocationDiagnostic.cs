using AnnotationVizLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MorphologyMeshTest
{
    [TestClass]
    public class BajajReproLocationDiagnostic
    {
        private static readonly Uri Rc1 = Viking.Common.ODataEndpointCatalog.EndpointMap[Viking.Common.Endpoint.RC1];

        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(120000)]
        public async Task DiagnoseReproLocations8592_8593()
        {
            ulong[] locations = [8592, 8593];
            await RunReproDiagnostic(locations, Rc1);
        }

        internal static async Task RunReproDiagnostic(ulong[] locations, Uri endpoint)
        {
            const string logPath = @"d:\src\git\VikingLegacy\debug-be41e5.log";
            void Log(string hypothesisId, string location, string message, object data = null)
            {
                string line = JsonSerializer.Serialize(new
                {
                    sessionId = "be41e5",
                    runId = "diagnostic",
                    hypothesisId,
                    location,
                    message,
                    data,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
                File.AppendAllText(logPath, line + Environment.NewLine);
            }

            Log("A", "RunReproDiagnostic", "start", new { locations, endpoint = endpoint.ToString() });

            MorphologyGraph morphology = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync(
                [.. locations.Select(id => (long)id)], endpoint, hops: 1);

            Log("D", "RunReproDiagnostic", "morphology loaded", new
            {
                nodeCount = morphology.Nodes.Count,
                found = locations.Select(id => morphology.Nodes.ContainsKey(id)).ToArray()
            });

            SliceGraph graph = await SliceGraph.Create(morphology, 1.0);
            Log("A", "RunReproDiagnostic", "slice graph created", new { sliceCount = graph.Nodes.Count });

            var matching = graph.Nodes
                .Where(n => locations.All(id => n.Value.AllNodes.Contains(id)))
                .Select(n => new { n.Key, above = n.Value.NodesAbove.ToArray(), below = n.Value.NodesBelow.ToArray() })
                .ToList();

            Log("A", "RunReproDiagnostic", "matching slices", new { count = matching.Count, slices = matching });

            foreach (ulong id in locations)
            {
                var slicesForLoc = graph.Nodes
                    .Where(n => n.Value.AllNodes.Contains(id))
                    .Select(n => n.Key)
                    .ToList();
                Log("A", "RunReproDiagnostic", $"slices containing loc {id}", new { slicesForLoc });
            }

            if (matching.Count == 0)
                Assert.Fail($"No slice contains all locations [{string.Join(", ", locations)}]. BajajTest GetSlice Debug.Assert would fire.");

            Slice slice = graph.Nodes.First(n => locations.All(id => n.Value.AllNodes.Contains(id))).Value;
            Log("C", "RunReproDiagnostic", "target slice adjacency", new
            {
                slice.Key,
                slice.HasSliceAbove,
                slice.HasSliceBelow,
                above = slice.NodesAbove.ToArray(),
                below = slice.NodesBelow.ToArray()
            });

            Log("B", "RunReproDiagnostic", "mesh conversion start", new { sliceKey = slice.Key });

            List<BajajGeneratorMesh> meshes;
            try
            {
                meshes = await BajajMeshGenerator.ConvertToMesh(graph);
            }
            catch (Exception ex)
            {
                Log("B", "RunReproDiagnostic", "ConvertToMesh threw", new { type = ex.GetType().FullName, ex.Message, ex.StackTrace });
                throw;
            }

            Log("B", "RunReproDiagnostic", "ConvertToMesh finished", new { meshCount = meshes.Count });

            foreach (BajajGeneratorMesh mesh in meshes)
            {
                MeshManifoldReport report = mesh.ManifoldReport;
                Log("B", "RunReproDiagnostic", "manifold report", new
                {
                    sliceKey = mesh.Slice?.Key,
                    generationHadErrors = mesh.GenerationHadErrors,
                    isValid = report.IsValidSliceSurface,
                    nonManifold = report.NonManifoldEdges,
                    holes = report.UnexpectedBoundaryEdges,
                    faceCount = mesh.Faces?.Count ?? 0
                });
            }

            // BajajTest.GenerateMesh caps both ends unconditionally; production GenerateFaces skips caps when
            // adjacent slices exist.  Simulate the viewer path on the target slice only.
            SliceTopology topology = graph.GetTopology(slice);
            if (topology.IsValid)
            {
                BajajGeneratorMesh viewerMesh = new(topology, slice);
                BajajMeshGenerator.GenerateFaces(viewerMesh);
                int facesBeforeCap = viewerMesh.Faces?.Count ?? 0;
                viewerMesh.CapMeshEnd(true);
                viewerMesh.CapMeshEnd(false);
                Log("C", "RunReproDiagnostic", "unconditional cap (BajajTest path)", new
                {
                    facesBeforeCap,
                    facesAfterCap = viewerMesh.Faces?.Count ?? 0,
                    generationHadErrors = viewerMesh.GenerationHadErrors
                });
            }
        }
    }
}
