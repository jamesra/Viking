using AnnotationVizLib;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Step-0 harness for section-registration investigation on a specific process chain.
    /// Reports centroid hops, SmoothProcesses offsets, node pin/skip classification, and virtual-overlap
    /// translation magnitudes for slices spanning the requested LocationIDs.
    /// </summary>
    [TestClass]
    public class ProcessRegistrationDiagnostic
    {
        private static readonly Uri Endpoint = new("http://websvc.codepharm.net/RC1/OData");

        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(600000)]
        public async Task DiagnoseStructure410Locations201496To201501()
        {
            ulong structureId = 410;
            ulong[] targetLocations = [201496, 201500, 201501];

            await RunDiagnostic(structureId, targetLocations);
        }

        internal static async Task RunDiagnostic(ulong structureId, ulong[] targetLocations)
        {
            MorphologyGraph root = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync(
                [(long)structureId], false, Endpoint);

            if (!root.Subgraphs.TryGetValue(structureId, out MorphologyGraph cell))
                Assert.Fail($"Structure {structureId} not found in OData response.");

            Vector2 origin = cell.NodesBoundingBox.CenterPoint.XY();
            Console.WriteLine($"Structure {structureId}: {cell.Nodes.Count} nodes, origin ({origin.X:F0}, {origin.Y:F0})");

            foreach (ulong id in targetLocations)
            {
                if (!cell.Nodes.ContainsKey(id))
                    Assert.Fail($"Location {id} not in structure {structureId}.");
            }

            ReportPerTargetLocation(cell, targetLocations);

            ulong[] chain = FindProcessChainContaining(cell, targetLocations);
            Console.WriteLine($"\nProcess chain ({chain.Length} nodes): {string.Join(" -> ", chain)}");

            ReportNodeClassification(cell, chain, targetLocations);
            ReportCentroidHops(cell, chain, label: "BEFORE SmoothProcesses");

            HashSet<ulong> reportIds = [.. chain, .. targetLocations];
            Dictionary<ulong, Vector2> centroidsBefore = reportIds.ToDictionary(id => id, id => cell.Nodes[id].Center.XY());
            MorphologyGraph.SmoothProcesses(cell);
            Dictionary<ulong, Vector2> centroidsAfter = reportIds.ToDictionary(id => id, id => cell.Nodes[id].Center.XY());

            ReportSmoothingOffsets(cell, [.. reportIds.OrderBy(id => cell.Nodes[id].Z)], centroidsBefore, centroidsAfter);
            ReportCentroidHops(cell, chain, label: "AFTER SmoothProcesses");

            SliceGraph slices = await SliceGraph.Create(cell, 2.0, origin);
            Console.WriteLine($"\nSliceGraph: {slices.Nodes.Count} slices, failed topology: {slices.FailedTopologySlices.Count}");

            foreach (var kv in slices.FailedTopologySlices)
                Console.WriteLine($"  failed topology slice key {kv.Key}: {kv.Value}");

            ReportVirtualOverlapForChain(slices, cell, chain, targetLocations);
            ReportVirtualOverlapForTargets(slices, cell, targetLocations);

            List<BajajGeneratorMesh> meshes = await BajajMeshGenerator.ConvertToMesh(slices);

            ReportMeshOutcomeForChain(meshes, chain, targetLocations);
            ReportMeshOutcomeForTargets(meshes, cell, targetLocations);
            PrintSummaryTable(cell, chain, centroidsBefore, centroidsAfter, slices, targetLocations);
        }

        private static void ReportVirtualOverlapForTargets(
            SliceGraph slices,
            MorphologyGraph graph,
            ulong[] targetLocations)
        {
            Console.WriteLine("\n=== Virtual overlap at target locations (any slice) ===");
            foreach (ulong id in targetLocations.OrderBy(i => graph.Nodes[i].Z))
            {
                foreach (Slice slice in slices.Nodes.Values.OrderBy(s => s.Key))
                {
                    if (!slice.NodesAbove.Contains(id) && !slice.NodesBelow.Contains(id))
                        continue;

                    SliceTopology topology = slices.GetSliceTopology(slice);
                    Console.WriteLine($"\n  loc {id} in slice {slice.Key}  above=[{string.Join(",", slice.NodesAbove)}] below=[{string.Join(",", slice.NodesBelow)}]");
                    for (int i = 0; i < topology.Shapes.Length; i++)
                    {
                        if (topology.ShapeIndexToMorphNodeIndex[i] != id)
                            continue;
                        Vector2 vo = topology.GetVirtualOverlapOffset(i);
                        Console.WriteLine($"    shape {i} upper={topology.IsUpper[i]} type={topology.Shapes[i].GetType().Name} |VO|={vo.Magnitude:F1} VO=({vo.X:F1},{vo.Y:F1})");
                    }

                    if (slice.NodesAbove.Count == 1 && slice.NodesBelow.Count == 1)
                    {
                        ulong upperId = slice.NodesAbove.First();
                        ulong lowerId = slice.NodesBelow.First();
                        Vector2 delta = graph.Nodes[upperId].Center.XY() - graph.Nodes[lowerId].Center.XY();
                        int iLower = Array.IndexOf(topology.ShapeIndexToMorphNodeIndex, lowerId);
                        Vector2 voLower = iLower >= 0 ? topology.GetVirtualOverlapOffset(iLower) : Vector2.Zero;
                        bool centroidFallback = topology.HasVirtualOverlapTranslation
                            && voLower != Vector2.Zero
                            && Vector2.Distance(voLower, delta) < Math.Max(1.0, delta.Magnitude * 0.01);
                        Console.WriteLine($"    1:1 centroid delta |dXY|={delta.Magnitude:F1} nm  centroid fallback={centroidFallback}");
                    }
                }
            }
        }

        private static void ReportMeshOutcomeForTargets(
            IReadOnlyList<BajajGeneratorMesh> meshes,
            MorphologyGraph graph,
            ulong[] targetLocations)
        {
            Console.WriteLine("\n=== Mesh outcome at target locations ===");
            foreach (ulong id in targetLocations.OrderBy(i => graph.Nodes[i].Z))
            {
                foreach (BajajGeneratorMesh mesh in meshes)
                {
                    if (!mesh.Topology.ShapeIndexToMorphNodeIndex.Contains(id))
                        continue;
                    int crossBand = mesh.MorphFaces.Count(f => IsCrossBand(mesh, f));
                    var report = mesh.ManifoldReport;
                    Console.WriteLine($"  loc {id}: mesh {mesh} crossBand={crossBand} valid={report.IsValidSliceSurface} holes={report.UnexpectedBoundaryEdges} {report}");
                }
            }
        }

        private static void ReportPerTargetLocation(MorphologyGraph graph, ulong[] targetLocations)
        {
            Console.WriteLine("\n=== Per-target location (may be on different branches) ===");
            foreach (ulong id in targetLocations)
            {
                MorphologyNode node = graph.Nodes[id];
                ulong[] above = node.GetEdgesAbove(graph);
                ulong[] below = node.GetEdgesBelow(graph);
                Console.WriteLine($"\nLocation {id}  Z={node.Z:F0}  {ClassifyNode(node, graph)}");
                Console.WriteLine($"  links above: [{string.Join(", ", above)}]");
                Console.WriteLine($"  links below: [{string.Join(", ", below)}]");
                foreach (ulong up in above)
                {
                    Vector2 hop = graph.Nodes[up].Center.XY() - node.Center.XY();
                    Console.WriteLine($"  hop to {up} (Z={graph.Nodes[up].Z:F0}): |dXY|={hop.Magnitude:F1} nm");
                }
                foreach (ulong dn in below)
                {
                    Vector2 hop = node.Center.XY() - graph.Nodes[dn].Center.XY();
                    Console.WriteLine($"  hop from {dn} (Z={graph.Nodes[dn].Z:F0}): |dXY|={hop.Magnitude:F1} nm");
                }
            }
        }

        private static ulong[] FindProcessChainContaining(MorphologyGraph graph, ulong[] targetLocations)
        {
            HashSet<ulong> targets = [.. targetLocations];
            foreach (ulong[] process in graph.Processes())
            {
                if (process.Any(targets.Contains))
                    return process;
            }

            //Targets may sit on pinned endpoints; walk from any target along Z links.
            MorphologyNode seed = graph.Nodes[targetLocations[0]];
            SortedSet<ulong> chain = [seed.Key];
            MorphologyNode cursor = seed;
            while (true)
            {
                ulong[] below = cursor.GetEdgesBelow();
                if (below.Length != 1)
                    break;
                ulong next = below[0];
                if (!chain.Add(next))
                    break;
                cursor = graph.Nodes[next];
            }

            cursor = seed;
            while (true)
            {
                ulong[] above = cursor.GetEdgesAbove();
                if (above.Length != 1)
                    break;
                ulong next = above[0];
                if (!chain.Add(next))
                    break;
                cursor = graph.Nodes[next];
            }

            return [.. chain.OrderBy(id => graph.Nodes[id].Z).ThenBy(id => id)];
        }

        private static void ReportNodeClassification(MorphologyGraph graph, ulong[] chain, ulong[] targetLocations)
        {
            Console.WriteLine("\n=== Node classification ===");
            Console.WriteLine("LocationID   Z        role                          target?");
            foreach (ulong id in chain)
            {
                MorphologyNode node = graph.Nodes[id];
                string role = ClassifyNode(node, graph);
                bool isTarget = targetLocations.Contains(id);
                Console.WriteLine($"{id,11} {node.Z,8:F0}  {role,-28}  {(isTarget ? "YES" : "")}");
            }
        }

        private static string ClassifyNode(MorphologyNode node, MorphologyGraph graph)
        {
            if (node.IsProcessTerminal())
                return "terminal (pinned)";
            if (node.IsSameSectionBranch(graph))
                return "branch/Y-junction (pinned)";
            if (node.IsUnbranchedProcess(graph))
                return "unbranched process (smoothable)";
            return "other (pinned/isolated)";
        }

        private static void ReportCentroidHops(MorphologyGraph graph, ulong[] chain, string label)
        {
            Console.WriteLine($"\n=== Centroid hops — {label} ===");
            Console.WriteLine("lower -> upper   |dXY| nm   dX nm     dY nm    lowerZ   upperZ");
            for (int i = 0; i + 1 < chain.Length; i++)
            {
                MorphologyNode lower = graph.Nodes[chain[i]];
                MorphologyNode upper = graph.Nodes[chain[i + 1]];
                if (upper.Z <= lower.Z)
                    continue;

                Vector2 hop = upper.Center.XY() - lower.Center.XY();
                Console.WriteLine($"{lower.Key,6} -> {upper.Key,6}  {hop.Magnitude,8:F1}  {hop.X,8:F1}  {hop.Y,8:F1}  {lower.Z,7:F0}  {upper.Z,7:F0}");
            }
        }

        private static void ReportSmoothingOffsets(
            MorphologyGraph graph,
            ulong[] chain,
            Dictionary<ulong, Vector2> before,
            Dictionary<ulong, Vector2> after)
        {
            Console.WriteLine("\n=== SmoothProcesses offsets ===");
            Console.WriteLine("LocationID   role                         |offset| nm  cap nm  applied?  reason if skipped");
            foreach (ulong id in chain)
            {
                MorphologyNode node = graph.Nodes[id];
                Vector2 delta = after[id] - before[id];
                double cap = ComputeSmoothCap(node);
                string role = ClassifyNode(node, graph);
                string reason = SmoothSkipReason(node, chain, delta.Magnitude);
                bool applied = delta.Magnitude > Tolerance.Epsilon && reason == "";
                Console.WriteLine($"{id,11}  {role,-28}  {delta.Magnitude,8:F1}  {cap,6:F1}  {(applied ? "yes" : "no"),8}  {reason}");
            }
        }

        private static double ComputeSmoothCap(MorphologyNode node)
        {
            Rectangle bbox = node.Geometry.BoundingBox();
            return Math.Min(
                MorphologyGraph.MaxProcessCentroidOffset,
                MorphologyGraph.MaxProcessCentroidOffsetFractionOfWidth * bbox.Width);
        }

        private static string SmoothSkipReason(MorphologyNode node, ulong[] reportOrder, double offsetMag)
        {
            if (node.IsProcessTerminal())
                return "terminal (Catmull anchor)";
            if (node.IsSameSectionBranch())
                return "branch/Y-junction (pinned)";
            if (!node.IsUnbranchedProcess())
                return "not 1-up-1-down process";
            if (offsetMag <= Tolerance.Epsilon)
                return "fit matched centroid (or clamped to zero)";
            return "";
        }

        private static void ReportVirtualOverlapForChain(
            SliceGraph slices,
            MorphologyGraph graph,
            ulong[] chain,
            ulong[] targetLocations)
        {
            Console.WriteLine("\n=== Virtual overlap per slice (chain locations) ===");
            HashSet<ulong> chainSet = [.. chain];

            foreach (Slice slice in slices.Nodes.Values.OrderBy(s => s.Key))
            {
                ulong[] sliceNodes = [.. slice.NodesAbove.Concat(slice.NodesBelow).Where(chainSet.Contains)];
                if (sliceNodes.Length == 0)
                    continue;

                SliceTopology topology = slices.GetSliceTopology(slice);
                bool isTargetSlice = sliceNodes.Any(targetLocations.Contains);

                Console.WriteLine($"\nSlice {slice.Key}  locations [{string.Join(", ", sliceNodes.OrderBy(n => graph.Nodes[n].Z))}]  {(isTargetSlice ? "[TARGET]" : "")}");
                Console.WriteLine($"  shapes: {topology.Shapes.Length}  virtual overlap: {topology.HasVirtualOverlapTranslation}  tileable morph indices: [{string.Join(", ", topology.ShapeIndexToMorphNodeIndex.Distinct())}]");

                for (int i = 0; i < topology.Shapes.Length; i++)
                {
                    ulong morphId = topology.ShapeIndexToMorphNodeIndex[i];
                    if (!chainSet.Contains(morphId))
                        continue;

                    Vector2 offset = topology.GetVirtualOverlapOffset(i);
                    Vector2 centroid = graph.Nodes[morphId].Center.XY();
                    bool isUpper = topology.IsUpper[i];
                    Console.WriteLine($"  shape {i} loc {morphId} Z={topology.ShapeZ[i]:F0} upper={isUpper} type={topology.Shapes[i].GetType().Name} |VO|={offset.Magnitude:F1} nm  VO=({offset.X:F1},{offset.Y:F1})");
                }

                DescribeOneToOneCentroidFallback(slice, graph, topology);
            }
        }

        /// <summary>
        /// In the historical 1:1 linked pair, virtual overlap moves the partner by the full centroid delta.
        /// </summary>
        private static void DescribeOneToOneCentroidFallback(Slice slice, MorphologyGraph graph, SliceTopology topology)
        {
            if (slice.NodesAbove.Count != 1 || slice.NodesBelow.Count != 1)
                return;

            ulong upperId = slice.NodesAbove.First();
            ulong lowerId = slice.NodesBelow.First();
            int iUpper = Array.IndexOf(topology.ShapeIndexToMorphNodeIndex, upperId);
            int iLower = Array.IndexOf(topology.ShapeIndexToMorphNodeIndex, lowerId);
            if (iUpper < 0 || iLower < 0)
            {
                Console.WriteLine("  1:1 slice but chain locations not in tileable shapes (correspondence-only or filtered).");
                return;
            }

            Vector2 upperCentroid = graph.Nodes[upperId].Center.XY();
            Vector2 lowerCentroid = graph.Nodes[lowerId].Center.XY();
            Vector2 centroidDeltaLowerToUpper = upperCentroid - lowerCentroid;

            Vector2 voUpper = topology.GetVirtualOverlapOffset(iUpper);
            Vector2 voLower = topology.GetVirtualOverlapOffset(iLower);

            //Upper is the fixed frame in the 1:1 degenerate case; lower partner moves.
            Vector2 expectedLowerMove = centroidDeltaLowerToUpper;
            bool lowerMatchesCentroidFallback = topology.HasVirtualOverlapTranslation
                && voLower != Vector2.Zero
                && Vector2.Distance(voLower, expectedLowerMove) < Math.Max(1.0, expectedLowerMove.Magnitude * 0.01);

            Console.WriteLine($"  1:1 pair upper={upperId} lower={lowerId}");
            Console.WriteLine($"    centroid delta (lower->upper): |dXY|={centroidDeltaLowerToUpper.Magnitude:F1} nm  ({centroidDeltaLowerToUpper.X:F1}, {centroidDeltaLowerToUpper.Y:F1})");
            Console.WriteLine($"    virtual offset lower: |VO|={voLower.Magnitude:F1}  upper: |VO|={voUpper.Magnitude:F1}");
            Console.WriteLine($"    centroid fallback on lower: {(lowerMatchesCentroidFallback ? "LIKELY YES" : "no / box placement")}");
        }

        private static void ReportMeshOutcomeForChain(
            IReadOnlyList<BajajGeneratorMesh> meshes,
            ulong[] chain,
            ulong[] targetLocations)
        {
            HashSet<ulong> chainSet = [.. chain];
            Console.WriteLine("\n=== Mesh outcome (slices touching chain) ===");

            foreach (BajajGeneratorMesh mesh in meshes.OrderBy(m => m.Topology.ShapeZ.Min()))
            {
                ulong[] locs = [.. mesh.Topology.ShapeIndexToMorphNodeIndex.Where(chainSet.Contains).Distinct()];
                if (locs.Length == 0)
                    continue;

                bool isTarget = locs.Any(targetLocations.Contains);
                int crossBand = mesh.MorphFaces.Count(f => IsCrossBand(mesh, f));
                var report = mesh.ManifoldReport;
                Console.WriteLine($"\nMesh {mesh} {(isTarget ? "[TARGET]" : "")} locs [{string.Join(", ", locs)}]");
                Console.WriteLine($"  faces={mesh.Faces.Count} crossBand={crossBand} valid={report.IsValidSliceSurface} {report}");
                if (report.UnexpectedBoundaryEdges > 0 || crossBand == 0)
                    Console.WriteLine("  >>> visible gap risk: no cross-band tiling or open boundary edges");
            }
        }

        private static bool IsCrossBand(BajajGeneratorMesh mesh, MorphMeshFace face)
        {
            bool hasUpper = false;
            bool hasLower = false;
            foreach (int iVert in face.iVerts)
            {
                IShapeIndex idx = mesh[iVert].ShapeIndex;
                if (idx is null)
                    continue;
                if (mesh.IsUpperShape[idx.ShapeIndex])
                    hasUpper = true;
                else
                    hasLower = true;
            }

            return hasUpper && hasLower;
        }

        private static void PrintSummaryTable(
            MorphologyGraph graph,
            ulong[] chain,
            Dictionary<ulong, Vector2> before,
            Dictionary<ulong, Vector2> centroidsAfter,
            SliceGraph slices,
            ulong[] targetLocations)
        {
            Console.WriteLine("\n=== SUMMARY TABLE (all targets) ===");
            Console.WriteLine("Location  Z      role                  hopIn nm  smooth nm  cap nm  |VO| nm  centroidFallback?");
            foreach (ulong id in targetLocations.OrderBy(i => graph.Nodes[i].Z))
            {
                MorphologyNode node = graph.Nodes[id];
                double hopIn = HopFromLinkedBelow(graph, id, before);
                double smooth = before.TryGetValue(id, out Vector2 b) && centroidsAfter.TryGetValue(id, out Vector2 a)
                    ? (a - b).Magnitude
                    : 0;
                double cap = ComputeSmoothCap(node);
                (double voMag, bool centroidFallback) = MaxVirtualOverlapForLocation(slices, graph, id);
                Console.WriteLine($"{id,8} {node.Z,6:F0}  {ClassifyNode(node, graph),-20}  {hopIn,8:F1}  {smooth,9:F1}  {cap,6:F1}  {voMag,7:F1}  {(centroidFallback ? "yes" : "no")}");
            }

            ulong[] chainContext = [.. chain.Where(id => !targetLocations.Contains(id))];
            if (chainContext.Length > 0)
            {
                Console.WriteLine("\n(chain context — nodes between targets on shared walk)");
                foreach (ulong id in chainContext)
                {
                    MorphologyNode node = graph.Nodes[id];
                    double hopIn = HopFromBelow(graph, chain, id, before);
                    double smooth = (centroidsAfter[id] - before[id]).Magnitude;
                    double cap = ComputeSmoothCap(node);
                    (double voMag, bool centroidFallback) = MaxVirtualOverlapForLocation(slices, graph, id);
                    Console.WriteLine($"{id,8} {node.Z,6:F0}  {ClassifyNode(node, graph),-20}  {hopIn,8:F1}  {smooth,9:F1}  {cap,6:F1}  {voMag,7:F1}  {(centroidFallback ? "yes" : "no")}");
                }
            }
        }

        private static double HopFromLinkedBelow(MorphologyGraph graph, ulong id, Dictionary<ulong, Vector2> centroids)
        {
            MorphologyNode node = graph.Nodes[id];
            ulong[] below = node.GetEdgesBelow(graph);
            if (below.Length != 1)
                return double.NaN;
            ulong lower = below[0];
            return (centroids[id] - graph.Nodes[lower].Center.XY()).Magnitude;
        }

        private static double HopFromBelow(MorphologyGraph graph, ulong[] chain, ulong id, Dictionary<ulong, Vector2> centroids)
        {
            int i = Array.IndexOf(chain, id);
            if (i <= 0)
                return double.NaN;
            MorphologyNode lower = graph.Nodes[chain[i - 1]];
            MorphologyNode self = graph.Nodes[id];
            if (self.Z <= lower.Z)
                return double.NaN;
            return (centroids[id] - centroids[chain[i - 1]]).Magnitude;
        }

        private static (double voMag, bool centroidFallback) MaxVirtualOverlapForLocation(
            SliceGraph slices,
            MorphologyGraph graph,
            ulong locationId)
        {
            double maxVo = 0;
            bool centroidFallback = false;
            foreach (Slice slice in slices.Nodes.Values)
            {
                if (!slice.NodesAbove.Contains(locationId) && !slice.NodesBelow.Contains(locationId))
                    continue;

                SliceTopology topology = slices.GetSliceTopology(slice);
                for (int i = 0; i < topology.Shapes.Length; i++)
                {
                    if (topology.ShapeIndexToMorphNodeIndex[i] != locationId)
                        continue;
                    Vector2 vo = topology.GetVirtualOverlapOffset(i);
                    maxVo = Math.Max(maxVo, vo.Magnitude);
                }

                if (slice.NodesAbove.Count == 1 && slice.NodesBelow.Count == 1)
                {
                    ulong upperId = slice.NodesAbove.First();
                    ulong lowerId = slice.NodesBelow.First();
                    int iLower = Array.IndexOf(topology.ShapeIndexToMorphNodeIndex, lowerId);
                    if (iLower >= 0)
                    {
                        Vector2 expected = graph.Nodes[upperId].Center.XY() - graph.Nodes[lowerId].Center.XY();
                        Vector2 voLower = topology.GetVirtualOverlapOffset(iLower);
                        if (voLower != Vector2.Zero && Vector2.Distance(voLower, expected) < Math.Max(1.0, expected.Magnitude * 0.01))
                            centroidFallback = true;
                    }
                }
            }

            return (maxVo, centroidFallback);
        }
    }
}
