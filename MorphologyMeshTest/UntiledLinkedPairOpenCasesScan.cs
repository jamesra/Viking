using AnnotationVizLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using MorphologyMeshTest.DifficultCases;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Live scan: which open UntiledLinkedPair DifficultCases still lack spanning faces after pair-local VO.
    /// </summary>
    [TestClass]
    public class UntiledLinkedPairOpenCasesScan
    {
        //Match MullerCells-20-10 import that produced the open ULP backlog.
        private static readonly ContourSimplifyOptions MullerSimplify = new(20.0, 10.0, adaptiveHullSpacing: false);

        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(1800000)]
        public async Task ScanOpenUntiledLinkedPairCases()
        {
            var open = DifficultCaseList.Load()
                .Where(c => c.Open && string.Equals(c.FailureKind, "UntiledLinkedPair", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.IsTrue(open.Count > 0, "Expected open UntiledLinkedPair cases");
            int fixedCount = 0;
            int stillFail = 0;
            int otherFail = 0;

            foreach (DifficultCase difficultCase in open)
            {
                try
                {
                    MorphologyGraph morphology = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync(
                        [.. difficultCase.Locations.Select(id => (long)id)], difficultCase.Endpoint, hops: 1);

                    SliceGraph slices = await SliceGraph.Create(morphology, MullerSimplify);
                    Slice target = slices.Nodes.Values.FirstOrDefault(s => difficultCase.Locations.All(id => s.AllNodes.Contains(id)));
                    if (target is null)
                    {
                        otherFail++;
                        Console.WriteLine($"OTHER {difficultCase.Key}: no slice contains all locations");
                        continue;
                    }

                    if (slices.FailedTopologySlices.ContainsKey(target.Key))
                    {
                        otherFail++;
                        Console.WriteLine($"OTHER {difficultCase.Key}: topology failed");
                        continue;
                    }

                    SliceTopology topology = slices.GetTopology(target);
                    BajajGeneratorMesh mesh = new(topology, target);
                    BajajMeshGenerator.GenerateFaces(mesh);

                    if (mesh.HasUntiledLinkedPairs)
                    {
                        stillFail++;
                        string ids = string.Join(",", mesh.UntiledLinkedShapeIndices);
                        Console.WriteLine($"STILL {difficultCase.Key}: untiled shape indices [{ids}]");
                    }
                    else
                    {
                        fixedCount++;
                        Console.WriteLine($"FIXED {difficultCase.Key}");
                    }
                }
                catch (Exception ex)
                {
                    otherFail++;
                    Console.WriteLine($"OTHER {difficultCase.Key}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            Console.WriteLine($"Summary: fixed={fixedCount} stillUntiled={stillFail} other={otherFail} total={open.Count}");
            Assert.IsTrue(fixedCount > 0, "Pair-local VO should clear at least one open UntiledLinkedPair case");
        }
    }
}
