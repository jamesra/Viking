using AnnotationVizLib;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using SqlGeometryUtils;
using System;
using System.Linq;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Live diagnostic for Multitest pick Structure 53730 / locations 381857,381868 (dropped-topology example).
    /// </summary>
    [TestClass]
    public class DroppedSlice381857Diagnostic
    {
        private static readonly Uri Endpoint = Viking.Common.ODataEndpointCatalog.EndpointMap[Viking.Common.Endpoint.RPC1];

        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(300000)]
        public async Task DiagnoseDroppedSlice_381857_381868()
        {
            ulong[] locations = [381857, 381868];

            MorphologyGraph morphology = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync(
                [.. locations.Select(id => (long)id)], Endpoint, hops: 1);

            Console.WriteLine($"Nodes={morphology.Nodes.Count} Edges={morphology.Edges.Count}");
            foreach (ulong id in locations)
            {
                Assert.IsTrue(morphology.Nodes.ContainsKey(id), $"Missing location {id}");
                MorphologyNode n = morphology.Nodes[id];
                Console.WriteLine(
                    $"  loc {id}: type={n.Location.TypeCode} Z={n.Z:F1} section={n.Location.UnscaledZ} " +
                    $"neighbors=[{string.Join(",", n.Edges.Keys)}]");
            }

            SliceGraph slices = await SliceGraph.Create(morphology, ContourSimplifyOptions.Default);
            Console.WriteLine($"\nSliceGraph: slices={slices.Nodes.Count} failedTopology={slices.FailedTopologyCount}");
            foreach (var kv in slices.FailedTopologySlices)
                Console.WriteLine($"  FAILED slice {kv.Key}: {kv.Value}");

            Slice target = slices.Nodes.Values.FirstOrDefault(s => locations.All(id => s.AllNodes.Contains(id)));
            Assert.IsNotNull(target, "No slice contains both 381857 and 381868.");

            Console.WriteLine($"\nTarget slice {target.Key}: {slices.FormatSectionNumbers(target)}");
            Console.WriteLine($"  above=[{string.Join(",", target.NodesAbove)}] below=[{string.Join(",", target.NodesBelow)}]");

            SliceTopology topology = slices.GetTopology(target);
            Console.WriteLine($"  topology.IsValid={topology.IsValid} shapes={topology.Shapes?.Length ?? -1}");
            if (topology.IsValid)
            {
                for (int i = 0; i < topology.Shapes.Length; i++)
                {
                    IShape2D shape = topology.Shapes[i];
                    string kind = shape switch
                    {
                        Polygon => "Polygon",
                        Polyline => "Polyline",
                        _ => shape?.GetType().Name ?? "null"
                    };
                    LocationType locType = topology.ShapeLocationTypes?[i] ?? LocationType.POINT;
                    Console.WriteLine(
                        $"    shape[{i}] morph={topology.ShapeIndexToMorphNodeIndex[i]} upper={topology.IsUpper[i]} " +
                        $"Z={topology.ShapeZ[i]:F1} type={locType} kind={kind} " +
                        $"pts={(shape is Polyline pl ? pl.PointCount : shape is Polygon pg ? pg.ExteriorRing.Length : -1)}");
                }
            }

            Console.WriteLine($"\n=== Geometry (translated to cell center) ===");
            foreach (ulong id in locations)
            {
                MorphologyNode n = morphology.Nodes[id];
                if (n.Geometry is null)
                {
                    Console.WriteLine($"  loc {id}: null geometry");
                    continue;
                }

                Polyline line = n.Geometry.ToPolyLine(0);
                Console.WriteLine($"  loc {id}: {line.PointCount} pts");
                for (int i = 0; i < line.PointCount; i++)
                {
                    IPoint2D p = line.Points[i];
                    Console.WriteLine($"    [{i}] ({p.X.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {p.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
                }
            }

            Console.WriteLine($"\n=== Bounding boxes / intersection ===");
            {
                Polyline a = morphology.Nodes[381857].Geometry.ToPolyLine(0);
                Polyline b = morphology.Nodes[381868].Geometry.ToPolyLine(0);
                Console.WriteLine($"  381857 bbox={a.BoundingBox} 381868 bbox={b.BoundingBox}");
                Console.WriteLine($"  Intersects={a.Intersects(b)}");
            }

            // Full child structure 53730 — how many drops when meshed alone?
            MorphologyGraph root = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync(
                [53730L], include_children: false, Endpoint);
            MorphologyGraph child = root.Subgraphs.TryGetValue(53730, out MorphologyGraph sub) ? sub : root;
            if (child.Nodes.Count > 0)
            {
                SliceGraph childSlices = await SliceGraph.Create(child, ContourSimplifyOptions.Default);
                Console.WriteLine($"\nStructure 53730 alone: nodes={child.Nodes.Count} slices={childSlices.Nodes.Count} failedTopology={childSlices.FailedTopologyCount}");
                foreach (var kv in childSlices.FailedTopologySlices.Take(20))
                    Console.WriteLine($"  FAILED {kv.Key}: {kv.Value}");
            }

            if (topology.IsValid)
            {
                BajajGeneratorMesh mesh = new(topology, target);
                BajajMeshGenerator.GenerateFaces(mesh);
                MeshManifoldReport report = mesh.ManifoldReport;
                Console.WriteLine(
                    $"\nGenerateFaces: faces={mesh.Faces?.Count ?? 0} verts={mesh.Vertices?.Count ?? 0} " +
                    $"errors={mesh.GenerationHadErrors} manifold={report.IsValidSliceSurface} " +
                    $"nonManifold={report.NonManifoldEdges} holes={report.UnexpectedBoundaryEdges}");
                Console.WriteLine($"  report: {report}");
            }
        }
    }
}
