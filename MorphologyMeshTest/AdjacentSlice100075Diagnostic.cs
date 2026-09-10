using AnnotationVizLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Find slices adjacent to the selected pair 100075/100076 on RPC1 structure 2628
    /// and report which of them fail to produce mesh geometry.
    /// </summary>
    [TestClass]
    public class AdjacentSlice100075Diagnostic
    {
        private static readonly Uri Endpoint = Viking.Common.ODataEndpointCatalog.EndpointMap[Viking.Common.Endpoint.RPC1];

        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(600000)]
        public async Task IdentifyAdjacentSlicesTo100075_100076()
        {
            ulong[] selected = [100075, 100076];

            MorphologyGraph morphology = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync(
                [.. selected.Select(id => (long)id)], Endpoint, hops: 2);

            Console.WriteLine($"Loaded morphology: {morphology.Nodes.Count} nodes, {morphology.Edges.Count} edges");
            foreach (ulong id in selected)
            {
                Assert.IsTrue(morphology.Nodes.ContainsKey(id), $"Location {id} missing from OData fetch.");
                MorphologyNode n = morphology.Nodes[id];
                Console.WriteLine($"  loc {id}: Z={n.Z:F1} section={n.Location.UnscaledZ} neighbors=[{string.Join(",", n.Edges.Keys)}]");
            }

            // Neighbors of either selected location (these are candidates for adjacent slices).
            SortedSet<ulong> neighborIds = [];
            foreach (ulong id in selected)
            {
                foreach (ulong nb in morphology.Nodes[id].Edges.Keys)
                    neighborIds.Add(nb);
            }
            neighborIds.ExceptWith(selected);
            Console.WriteLine($"\nNeighbor locations of selected pair: {string.Join(", ", neighborIds)}");

            SliceGraph slices = await SliceGraph.Create(morphology, 2.0);
            Console.WriteLine($"\nSliceGraph: {slices.Nodes.Count} slices, failed topology: {slices.FailedTopologyCount}");
            foreach (var kv in slices.FailedTopologySlices)
                Console.WriteLine($"  FAILED topology slice {kv.Key}: {kv.Value}");

            Slice selectedSlice = slices.Nodes.Values.FirstOrDefault(s =>
                selected.All(id => s.AllNodes.Contains(id)));
            Assert.IsNotNull(selectedSlice, "No slice contains both 100075 and 100076.");

            Console.WriteLine($"\n=== Selected slice {selectedSlice.Key} ===");
            PrintSlice(slices, selectedSlice);

            // Adjacent slices share exactly one of the selected locations (the shared contour).
            List<Slice> adjacent = [.. slices.Nodes.Values
                .Where(s => s.Key != selectedSlice.Key)
                .Where(s => selected.Any(id => s.AllNodes.Contains(id)))
                .OrderBy(s => s.Key)];

            Console.WriteLine($"\n=== Adjacent slices sharing 100075 or 100076 ({adjacent.Count}) ===");
            foreach (Slice adj in adjacent)
                PrintSlice(slices, adj);

            // Also list slice-graph edges from the selected slice.
            if (slices.Nodes.TryGetValue(selectedSlice.Key, out Slice selNode))
            {
                Console.WriteLine($"\nSlice-graph neighbors of selected slice {selectedSlice.Key}:");
                foreach (ulong otherKey in selNode.Edges.Keys)
                {
                    Slice other = slices.Nodes[otherKey];
                    PrintSlice(slices, other, indent: "  ");
                }
            }

            Console.WriteLine("\n=== ConvertToMesh ===");
            List<BajajGeneratorMesh> meshes = await BajajMeshGenerator.ConvertToMesh(slices);
            Dictionary<ulong, BajajGeneratorMesh> byKey = meshes
                .Where(m => m.Slice != null)
                .ToDictionary(m => m.Slice.Key, m => m);

            Console.WriteLine($"\n=== Mesh outcome: selected + adjacent ===");
            ReportMesh(selectedSlice, byKey, "SELECTED");
            foreach (Slice adj in adjacent)
                ReportMesh(adj, byKey, "ADJACENT");

            // Candidate for BajajTest: adjacent slices that produced no faces or had generation errors.
            List<Slice> bad = [.. adjacent.Where(s =>
            {
                if (!byKey.TryGetValue(s.Key, out BajajGeneratorMesh m))
                    return true;
                return m.GenerationHadErrors || (m.Faces?.Count ?? 0) == 0;
            })];

            Console.WriteLine($"\n=== Bad adjacent slices needing BajajTest ({bad.Count}) ===");
            foreach (Slice s in bad)
            {
                ulong[] locs = [.. s.AllNodes.OrderBy(id => id)];
                Console.WriteLine($"  slice {s.Key}: --repro-locations {string.Join(",", locs)}");
            }

            Assert.IsTrue(adjacent.Count > 0, "Expected at least one adjacent slice sharing 100075 or 100076.");
        }

        static void PrintSlice(SliceGraph slices, Slice s, string indent = "")
        {
            string sections = slices.FormatSectionNumbers(s);
            bool failedTopo = slices.FailedTopologySlices.ContainsKey(s.Key);
            Console.WriteLine(
                $"{indent}slice {s.Key}: {sections} above=[{string.Join(",", s.NodesAbove)}] below=[{string.Join(",", s.NodesBelow)}] " +
                $"HasAbove={s.HasSliceAbove} HasBelow={s.HasSliceBelow} failedTopo={failedTopo}");
        }

        static void ReportMesh(Slice s, Dictionary<ulong, BajajGeneratorMesh> byKey, string tag)
        {
            if (!byKey.TryGetValue(s.Key, out BajajGeneratorMesh m))
            {
                Console.WriteLine($"  [{tag}] slice {s.Key}: NO MESH produced  locs=[{string.Join(",", s.AllNodes)}]");
                return;
            }

            MeshManifoldReport report = m.ManifoldReport;
            Console.WriteLine(
                $"  [{tag}] slice {s.Key}: faces={m.Faces?.Count ?? 0} verts={m.Vertices?.Count ?? 0} " +
                $"errors={m.GenerationHadErrors} manifold={report.IsValidSliceSurface} " +
                $"nonManifold={report.NonManifoldEdges} holes={report.UnexpectedBoundaryEdges} " +
                $"locs=[{string.Join(",", s.AllNodes)}]");
        }
    }
}
