using AnnotationVizLib;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MorphologyMeshTest
{
    /// <summary>
    /// The composite mesh for cell 476 comes out as 88 separate surfaces even though every contour reaches it and
    /// every shared vertex welds.  A surface can only be split where no slice joins two contours, and a slice only
    /// exists where the morphology graph has an edge, so this asks whether the graph itself is disconnected.
    ///
    /// If the graph has as many components as the mesh, the gaps are missing LocationLinks in the annotation data
    /// rather than a meshing defect.
    /// </summary>
    [TestClass]
    public class MorphologyConnectivityDiagnostic
    {
        private static readonly Uri Endpoint = new("http://websvc.codepharm.net/RC1/OData");

        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(600000)]
        public async Task ReportCell476GraphConnectivity()
        {
            MorphologyGraph root = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync(
                new long[] { 476 }, false, Endpoint);

            MorphologyGraph cell = root.Subgraphs[476];

            int nodes = cell.Nodes.Count;
            int edges = cell.Edges.Count;

            Console.WriteLine($"Cell 476: {nodes} nodes, {edges} edges");
            Console.WriteLine($"A single connected chain would need {nodes - 1} edges, so the deficit is {(nodes - 1) - edges}");

            //Union-find over the annotation links.
            Dictionary<ulong, ulong> parent = [];
            foreach (ulong id in cell.Nodes.Keys)
                parent[id] = id;

            ulong Find(ulong x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }

                return x;
            }

            foreach (var edge in cell.Edges.Values)
            {
                if (parent.ContainsKey(edge.SourceNodeKey) == false || parent.ContainsKey(edge.TargetNodeKey) == false)
                    continue;

                ulong ra = Find(edge.SourceNodeKey), rb = Find(edge.TargetNodeKey);
                if (ra != rb)
                    parent[ra] = rb;
            }

            Dictionary<ulong, int> componentSize = [];
            foreach (ulong id in cell.Nodes.Keys)
            {
                ulong r = Find(id);
                componentSize[r] = componentSize.GetValueOrDefault(r) + 1;
            }

            Console.WriteLine($"Connected components in the annotation graph: {componentSize.Count}");
            Console.WriteLine($"  largest component: {componentSize.Values.Max()} nodes");
            Console.WriteLine($"  components with a single node: {componentSize.Values.Count(v => v == 1)}");
            Console.WriteLine($"  components over 10 nodes: {componentSize.Values.Count(v => v > 10)}");

            Console.WriteLine("\nLargest components by node count:");
            foreach (int size in componentSize.Values.OrderByDescending(v => v).Take(15))
                Console.WriteLine($"    {size}");

            //A slice is built between two adjacent sections.  A link joining two locations that are further apart
            //has no pair of adjacent sections to tile between, so the surface has nothing to bridge the jump.
            Dictionary<long, int> bySectionGap = [];
            foreach (var edge in cell.Edges.Values)
            {
                if (cell.Nodes.TryGetValue(edge.SourceNodeKey, out var a) == false
                    || cell.Nodes.TryGetValue(edge.TargetNodeKey, out var b) == false)
                    continue;

                long gap = Math.Abs(a.Location.UnscaledZ - b.Location.UnscaledZ);
                bySectionGap[gap] = bySectionGap.GetValueOrDefault(gap) + 1;
            }

            Console.WriteLine("\nLinks by how many sections they span:");
            foreach (var kv in bySectionGap.OrderBy(k => k.Key))
                Console.WriteLine($"    {kv.Key} section(s): {kv.Value} links");

            Console.WriteLine($"\nLinks spanning more than one section: {bySectionGap.Where(k => k.Key > 1).Sum(k => k.Value)}");
            Console.WriteLine($"Links within one section (same-section, no band): {bySectionGap.GetValueOrDefault(0)}");

            await ReportSliceGraphConnectivity(cell);
        }

        /// <summary>
        /// The composite surface is joined wherever two slices share a contour, so the surface can only be in as many
        /// pieces as the slice graph is.  Measures that directly: slices as nodes, a shared morphology node as an
        /// edge.  A count far above the annotation graph's own component count means slices that ought to share a
        /// contour do not.
        /// </summary>
        private static async Task ReportSliceGraphConnectivity(MorphologyGraph cell)
        {
            //Slice construction deletes edges from the graph to break cycles, which is a way connectivity present in
            //the annotation can be lost before anything is meshed.
            int edgesBefore = cell.Edges.Count;
            TraceCollector collected = new();
            System.Diagnostics.Trace.Listeners.Add(collected);

            MorphologyMesh.SliceGraph slices;
            try
            {
                slices = await MorphologyMesh.SliceGraph.Create(cell, 2.0, cell.NodesBoundingBox.CenterPoint.XY());
            }
            finally
            {
                System.Diagnostics.Trace.Listeners.Remove(collected);
            }

            Console.WriteLine($"\nSlice graph: {slices.Nodes.Count} slices");
            Console.WriteLine($"  annotation edges before slicing: {edgesBefore}, after: {cell.Edges.Count}, removed: {edgesBefore - cell.Edges.Count}");
            Console.WriteLine($"  trace lines mentioning a cycle: {collected.Lines.Count(l => l.Contains("cycle", StringComparison.OrdinalIgnoreCase))}");
            Console.WriteLine($"  trace lines bailing out of a cross section: {collected.Lines.Count(l => l.Contains("Bailing out", StringComparison.OrdinalIgnoreCase))}");

            //morph node -> slices using it
            Dictionary<ulong, List<ulong>> slicesPerNode = [];
            foreach (var slice in slices.Nodes.Values)
            {
                foreach (ulong node in slice.AllNodes)
                {
                    if (slicesPerNode.TryGetValue(node, out var list) == false)
                        slicesPerNode[node] = list = [];

                    list.Add(slice.Key);
                }
            }

            //A link that never lands in a slice is a connection the surface can never make.
            HashSet<string> coveredEdges = [];
            foreach (var slice in slices.Nodes.Values)
                foreach (var e in slice.InternalEdges)
                    coveredEdges.Add(EdgeKey(e.SourceNodeKey, e.TargetNodeKey));

            var uncovered = cell.Edges.Values
                .Where(e => coveredEdges.Contains(EdgeKey(e.SourceNodeKey, e.TargetNodeKey)) == false)
                .ToList();

            //A slice tiles between the contours it actually holds.  An edge whose endpoint is not in this slice's
            //node sets contributes no geometry for that end, so the surface has nothing to join there even though
            //the link is nominally covered.
            int edgesMissingAnEndpoint = 0;
            int missingEndpointSpanningSections = 0;
            int missingEndpointOnMixedGapNode = 0;

            //A node whose links reach different distances - say one neighbour on the next section and another three
            //sections away - is the case where "above" and "below" stop being a single consistent step.
            Dictionary<ulong, HashSet<long>> gapsPerNode = [];
            foreach (var e in cell.Edges.Values)
            {
                if (cell.Nodes.TryGetValue(e.SourceNodeKey, out var na) == false
                    || cell.Nodes.TryGetValue(e.TargetNodeKey, out var nb) == false)
                    continue;

                long gap = Math.Abs(na.Location.UnscaledZ - nb.Location.UnscaledZ);
                foreach (ulong id in new[] { e.SourceNodeKey, e.TargetNodeKey })
                {
                    if (gapsPerNode.TryGetValue(id, out var set) == false)
                        gapsPerNode[id] = set = [];

                    set.Add(gap);
                }
            }

            int mixedGapNodes = gapsPerNode.Count(kv => kv.Value.Count > 1);
            Console.WriteLine($"  nodes whose links reach more than one section distance: {mixedGapNodes}");

            List<string> samples = [];
            foreach (var slice in slices.Nodes.Values)
            {
                foreach (var e in slice.InternalEdges)
                {
                    if (slice.AllNodes.Contains(e.SourceNodeKey) && slice.AllNodes.Contains(e.TargetNodeKey))
                        continue;

                    edgesMissingAnEndpoint++;

                    cell.Nodes.TryGetValue(e.SourceNodeKey, out var a);
                    cell.Nodes.TryGetValue(e.TargetNodeKey, out var b);
                    if (a is null || b is null)
                        continue;

                    long gap = Math.Abs(a.Location.UnscaledZ - b.Location.UnscaledZ);
                    if (gap > 1)
                        missingEndpointSpanningSections++;

                    if (gapsPerNode.GetValueOrDefault(e.SourceNodeKey)?.Count > 1
                        || gapsPerNode.GetValueOrDefault(e.TargetNodeKey)?.Count > 1)
                        missingEndpointOnMixedGapNode++;

                    if (samples.Count < 8)
                    {
                        samples.Add($"      slice {slice.Key}: {e.SourceNodeKey} (Z{a.Location.UnscaledZ}, gaps [{string.Join(",", gapsPerNode.GetValueOrDefault(e.SourceNodeKey) ?? [])}])" +
                                    $" <-> {e.TargetNodeKey} (Z{b.Location.UnscaledZ}, gaps [{string.Join(",", gapsPerNode.GetValueOrDefault(e.TargetNodeKey) ?? [])}])");
                    }
                }
            }

            Console.WriteLine($"  slice-internal links whose endpoint is not in that slice: {edgesMissingAnEndpoint}");
            Console.WriteLine($"    ...of those, the link itself spans more than one section: {missingEndpointSpanningSections}");
            Console.WriteLine($"    ...of those, an endpoint has links of mixed section distance: {missingEndpointOnMixedGapNode}");
            foreach (string s in samples)
                Console.WriteLine(s);
            Console.WriteLine($"  annotation links covered by a slice: {coveredEdges.Count} of {cell.Edges.Count}");
            Console.WriteLine($"  links never placed in any slice: {uncovered.Count}");
            foreach (var e in uncovered.Take(10))
            {
                cell.Nodes.TryGetValue(e.SourceNodeKey, out var a);
                cell.Nodes.TryGetValue(e.TargetNodeKey, out var b);
                Console.WriteLine($"      {e.SourceNodeKey} (section {a?.Location.UnscaledZ}) <-> {e.TargetNodeKey} (section {b?.Location.UnscaledZ})");
            }

            Console.WriteLine($"  contours used by exactly one slice (an end, needs a cap): {slicesPerNode.Count(kv => kv.Value.Count == 1)}");
            Console.WriteLine($"  contours used by two or more slices (a seam): {slicesPerNode.Count(kv => kv.Value.Count > 1)}");

            Dictionary<ulong, ulong> parent = [];
            foreach (ulong key in slices.Nodes.Keys)
                parent[key] = key;

            ulong Find(ulong x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }

                return x;
            }

            foreach (var kv in slicesPerNode)
            {
                for (int i = 1; i < kv.Value.Count; i++)
                {
                    ulong ra = Find(kv.Value[0]), rb = Find(kv.Value[i]);
                    if (ra != rb)
                        parent[ra] = rb;
                }
            }

            Dictionary<ulong, int> size = [];
            foreach (ulong key in slices.Nodes.Keys)
            {
                ulong r = Find(key);
                size[r] = size.GetValueOrDefault(r) + 1;
            }

            Console.WriteLine($"  connected components in the SLICE graph: {size.Count}");
            Console.WriteLine($"    largest: {size.Values.Max()} slices");
            Console.WriteLine($"    singletons: {size.Values.Count(v => v == 1)}");
        }

        /// <summary>
        /// ConnectIsolatedSubgraphs bridges isolated pieces of an annotation with a synthetic link at the nearest
        /// approach, but only the Collada export path calls it; the BajajMultiTest render path does not.  This
        /// measures how much of the fragmentation that call would actually account for.
        /// </summary>
        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(600000)]
        public async Task ReportEffectOfConnectingIsolatedSubgraphs()
        {
            MorphologyGraph root = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync(
                new long[] { 476 }, false, Endpoint);

            MorphologyGraph cell = root.Subgraphs[476];

            Console.WriteLine($"Before: {cell.Nodes.Count} nodes, {cell.Edges.Count} edges, " +
                              $"{MorphologyGraph.IsolatedSubgraphs(cell).Count} isolated subgraphs");

            cell.ConnectIsolatedSubgraphs();

            Console.WriteLine($"After:  {cell.Nodes.Count} nodes, {cell.Edges.Count} edges, " +
                              $"{MorphologyGraph.IsolatedSubgraphs(cell).Count} isolated subgraphs");

            await ReportSliceGraphConnectivity(cell);
        }

        /// <summary>Order-independent key so a link compares equal whichever way round it is stored.</summary>
        private static string EdgeKey(ulong a, ulong b) => a < b ? $"{a}-{b}" : $"{b}-{a}";

        /// <summary>Captures what slice construction traces so silent edge removal is visible.</summary>
        private sealed class TraceCollector : System.Diagnostics.TraceListener
        {
            public readonly List<string> Lines = [];

            public override void Write(string message) => WriteLine(message);

            public override void WriteLine(string message)
            {
                lock (Lines)
                    Lines.Add(message ?? string.Empty);
            }
        }
    }
}
