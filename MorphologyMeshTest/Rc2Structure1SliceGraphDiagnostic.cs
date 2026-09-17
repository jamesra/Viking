using AnnotationVizLib;
using AnnotationVizLib.OData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MorphologyMeshTest
{
    [TestClass]
    public class Rc2Structure1SliceGraphDiagnostic
    {
        static readonly Uri Rc2OData = Viking.Common.ODataEndpointCatalog.EndpointMap[Viking.Common.Endpoint.RC2];

        [TestMethod]
        [Timeout(600000)]
        public async Task Rc2Structure1_SliceGraph_Create_CompletesWithSlices()
        {
            MorphologyGraph graph = await ODataMorphologyFactory.FromODataAsync([1L], include_children: true, Rc2OData);

            int subgraphsWithNodes = CountSubgraphsWithNodes(graph);
            Assert.IsTrue(graph.Nodes.Count > 0 || subgraphsWithNodes > 0,
                "Structure 1 should have morphology nodes on the root graph or child subgraphs.");

            var results = await SliceAllSubgraphsAsync(graph);

            Assert.IsTrue(results.Count > 0, "Expected to build at least one subgraph slice graph.");
            int sliceTotal = results.Sum(r => r.sliceCount);
            int failedTopology = results.Sum(r => r.failedTopology);
            Assert.IsTrue(sliceTotal > 0, $"Expected slices, got {sliceTotal} across {results.Count} subgraph(s).");

            Console.WriteLine($"RC2 structure 1: {results.Count} subgraph(s), {sliceTotal} slice(s), {failedTopology} failed topology.");
        }

        static async Task<List<(ulong structureId, int sliceCount, int failedTopology)>> SliceAllSubgraphsAsync(MorphologyGraph parent)
        {
            List<(ulong, int, int)> results = [];
            await SliceSubgraphsRecursive(parent, results);
            return results;
        }

        static async Task SliceSubgraphsRecursive(MorphologyGraph parent, List<(ulong structureId, int sliceCount, int failedTopology)> results)
        {
            foreach (MorphologyGraph subgraph in parent.Subgraphs.Values)
            {
                if (subgraph.Nodes.Count > 0)
                {
                    SliceGraph slices = await SliceGraph.Create(subgraph, 2.0);
                    results.Add((subgraph.StructureID, slices.Nodes.Count, slices.FailedTopologyCount));
                }

                await SliceSubgraphsRecursive(subgraph, results);
            }
        }

        static int CountSubgraphsWithNodes(MorphologyGraph graph)
        {
            int count = 0;
            void Walk(MorphologyGraph g)
            {
                foreach (MorphologyGraph sub in g.Subgraphs.Values)
                {
                    if (sub.Nodes.Count > 0)
                        count++;
                    Walk(sub);
                }
            }

            Walk(graph);
            return count;
        }
    }
}
