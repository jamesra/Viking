using AnnotationVizLib;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Live diagnostic: classify why open UntiledLinkedPair DifficultCases decline virtual overlap.
    /// </summary>
    [TestClass]
    public class UntiledLinkedPairClassifyDiagnostic
    {
        private static readonly Uri Endpoint = Viking.Common.ODataEndpointCatalog.EndpointMap[Viking.Common.Endpoint.RPC1];

        [DataTestMethod]
        [TestCategory("LiveData")]
        [Timeout(180000)]
        [DataRow(new ulong[] { 101221, 101247, 364255, 364257, 364258 }, "101247/364255")]
        [DataRow(new ulong[] { 107721, 107735, 108104 }, "108104/107735")]
        [DataRow(new ulong[] { 102557, 102612, 364393 }, "364393/102557")]
        [DataRow(new ulong[] { 365941, 366486 }, "365941/366486")]
        [DataRow(new ulong[] { 98942, 98943, 101061, 101147, 101150, 101220, 101221 }, "hub-98943")]
        public async Task ClassifyVirtualOverlapDecline(ulong[] locations, string label)
        {
            MorphologyGraph morphology = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync(
                [.. locations.Select(id => (long)id)], Endpoint, hops: 1);

            var listener = new CollectingTraceListener();
            Trace.Listeners.Add(listener);
            try
            {
                SliceGraph sliceGraph = await SliceGraph.Create(morphology, ContourSimplifyOptions.Default);
                Assert.IsTrue(sliceGraph.Nodes.Count > 0, $"{label}: no slices");

                foreach (Slice slice in sliceGraph.Nodes.Values)
                {
                    SliceTopology topology = sliceGraph.GetTopology(slice.Key);
                    if (!topology.IsValid || topology.Shapes is null || topology.Shapes.Length < 2)
                        continue;

                    bool[] isUpper = topology.IsUpper;
                    IShape2D[] shapes = [.. topology.Shapes];
                    bool[,] links = CloneLinks(topology);

                    listener.Clear();
                    Vector2[] offsets = SliceTopology.TryTranslateNonOverlappingShapes(shapes, isUpper, links);

                    int forkCount = CountForks(shapes.Length, isUpper, links);
                    int disjointLinked = CountDisjointLinked(topology.Shapes, isUpper, links);

                    Console.WriteLine(
                        $"{label} slice={slice.Key} shapes={shapes.Length} forks={forkCount} " +
                        $"disjointLinked={disjointLinked} vo={(offsets is null ? "null/declined" : "applied")} " +
                        $"types=[{string.Join(",", topology.Shapes.Select(s => s.GetType().Name))}] " +
                        $"trace=[{string.Join(" | ", listener.Lines.Where(l => l.Contains("Virtual overlap") || l.Contains("pair-local")))}]");
                }
            }
            finally
            {
                Trace.Listeners.Remove(listener);
            }
        }

        private static bool[,] CloneLinks(SliceTopology topology)
        {
            int n = topology.Shapes.Length;
            bool[,] links = new bool[n, n];
            for (int i = 0; i < n; i++)
            {
                links[i, i] = true;
                for (int j = i + 1; j < n; j++)
                {
                    //Cross-band only: MayTile always allows same-band pairs.
                    bool linked = topology.IsUpper[i] != topology.IsUpper[j] && topology.IsLinked(i, j);
                    links[i, j] = linked;
                    links[j, i] = linked;
                }
            }

            return links;
        }

        private static int CountForks(int count, bool[] isUpper, bool[,] links)
        {
            int forks = 0;
            for (int i = 0; i < count; i++)
            {
                int partners = 0;
                for (int j = 0; j < count; j++)
                {
                    if (i == j || isUpper[i] == isUpper[j])
                        continue;
                    if (links[i, j])
                        partners++;
                }

                if (partners >= 2)
                    forks++;
            }

            return forks;
        }

        private static int CountDisjointLinked(IShape2D[] shapes, bool[] isUpper, bool[,] links)
        {
            int n = 0;
            for (int i = 0; i < shapes.Length; i++)
            {
                for (int j = i + 1; j < shapes.Length; j++)
                {
                    if (isUpper[i] == isUpper[j] || !links[i, j])
                        continue;
                    if (!shapes[i].Intersects(shapes[j]))
                        n++;
                }
            }

            return n;
        }

        private sealed class CollectingTraceListener : TraceListener
        {
            public List<string> Lines { get; } = [];

            public void Clear() => Lines.Clear();

            public override void Write(string message) { }

            public override void WriteLine(string message)
            {
                if (!string.IsNullOrEmpty(message))
                    Lines.Add(message);
            }
        }
    }
}
