using AnnotationVizLib;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using SqlGeometryUtils;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Live diagnostic for slices BajajMultiTest listed in <c>bajajmultitest_failed_slices.txt</c>.  Classifies each
    /// pair the same way the HUD now does (topology / face-gen exception / invalid surface) and dumps the geometry
    /// so a failing pair can be turned into a synthetic regression test.
    /// </summary>
    [TestClass]
    public class FailedSliceFixturesDiagnostic
    {
        private static readonly Uri Endpoint = Viking.Common.ODataEndpointCatalog.EndpointMap[Viking.Common.Endpoint.RPC1];

        [DataTestMethod]
        [TestCategory("LiveData")]
        [Timeout(300000)]
        [DataRow(368401UL, 368399UL, DisplayName = "368401/368399 face-gen threw: identical points")]
        [DataRow(368453UL, 368452UL, DisplayName = "368453/368452 invalid surface: holes:20")]
        [DataRow(381857UL, 381868UL, DisplayName = "381857/381868 short OPENCURVE pair")]
        public async Task ClassifyFailedSlice(ulong upper, ulong lower)
        {
            ulong[] locations = [upper, lower];

            MorphologyGraph morphology = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync(
                [.. locations.Select(id => (long)id)], Endpoint, hops: 1);

            Console.WriteLine($"Nodes={morphology.Nodes.Count} Edges={morphology.Edges.Count} Structure={morphology.StructureID}");
            foreach (ulong id in locations)
            {
                Assert.IsTrue(morphology.Nodes.ContainsKey(id), $"Missing location {id}");
                MorphologyNode n = morphology.Nodes[id];
                Console.WriteLine(
                    $"  loc {id}: type={n.Location.TypeCode} Z={n.Z:F1} section={n.Location.UnscaledZ} " +
                    $"neighbors=[{string.Join(",", n.Edges.Keys)}]");
            }

            Console.WriteLine("\n=== Volume geometry ===");
            foreach (ulong id in locations)
            {
                MorphologyNode n = morphology.Nodes[id];
                if (n.Geometry is null)
                {
                    Console.WriteLine($"  loc {id}: null geometry");
                    continue;
                }

                if (n.Location.TypeCode is LocationType.CIRCLE)
                {
                    Console.WriteLine($"  loc {id}: circle bbox={n.BoundingBox} geometry={n.Geometry.STGeometryType()}");
                    continue;
                }

                if (n.Location.TypeCode is LocationType.POLYGON or LocationType.CURVEPOLYGON or LocationType.CLOSEDCURVE)
                {
                    Polygon poly = n.Geometry.ToPolygon(0);
                    Console.WriteLine($"  loc {id}: polygon ring={poly.ExteriorRing.Length} pts, {poly.InteriorRings.Count} holes, area={poly.Area:F0}");
                    DumpPoints(poly.ExteriorRing);
                }
                else
                {
                    Polyline line = n.Geometry.ToPolyLine(0);
                    Console.WriteLine($"  loc {id}: polyline {line.PointCount} pts");
                    DumpPoints([.. line.Points.Select(p => new Vector2(p))]);
                }
            }

            SliceGraph slices = await SliceGraph.Create(morphology, ContourSimplifyOptions.Default);
            Console.WriteLine($"\nSliceGraph: slices={slices.Nodes.Count} failedTopology={slices.FailedTopologyCount}");
            foreach (var kv in slices.FailedTopologySlices)
                Console.WriteLine($"  FAILED TOPOLOGY slice {kv.Key}: {kv.Value}");

            Slice target = slices.Nodes.Values.FirstOrDefault(s => locations.All(id => s.AllNodes.Contains(id)));
            Assert.IsNotNull(target, $"No slice contains both {upper} and {lower}.");
            Console.WriteLine($"\nTarget slice {target.Key}: {slices.FormatSectionNumbers(target)}");
            Console.WriteLine($"  above=[{string.Join(",", target.NodesAbove)}] below=[{string.Join(",", target.NodesBelow)}]");

            if (slices.FailedTopologySlices.ContainsKey(target.Key))
            {
                Console.WriteLine("\nCLASSIFICATION: Topology");
                return;
            }

            SliceTopology topology = slices.GetTopology(target);
            Assert.IsTrue(topology.IsValid, "Topology not recorded as failed but IsValid is false.");
            for (int i = 0; i < topology.Shapes.Length; i++)
            {
                IShape2D shape = topology.Shapes[i];
                int pts = shape is Polyline pl ? pl.PointCount : shape is Polygon pg ? pg.ExteriorRing.Length : -1;
                Console.WriteLine(
                    $"    shape[{i}] morph={topology.ShapeIndexToMorphNodeIndex[i]} upper={topology.IsUpper[i]} " +
                    $"Z={topology.ShapeZ[i]:F1} type={topology.ShapeLocationTypes[i]} kind={shape.GetType().Name} pts={pts} " +
                    $"offset={topology.VirtualOverlapOffsets?[i]}");
            }

            BajajGeneratorMesh mesh = new(topology, target);
            try
            {
                BajajMeshGenerator.GenerateFaces(mesh);
            }
            catch (Exception e)
            {
                Console.WriteLine($"\nCLASSIFICATION: FaceGenerationException\n{e}");
                Assert.Fail($"GenerateFaces threw: {e.GetType().Name}: {e.Message}");
                return;
            }

            MeshManifoldReport report = mesh.ManifoldReport;
            Console.WriteLine(
                $"\nGenerateFaces: faces={mesh.Faces.Count} verts={mesh.Vertices.Count} errors={mesh.GenerationHadErrors}");
            Console.WriteLine($"  report: {report}");

            Console.WriteLine("  edges by face count:");
            foreach (var kv in mesh.Edges.OrderBy(k => k.Value.Faces.Count).ThenBy(k => k.Key.A))
            {
                MorphMeshEdge edge = (MorphMeshEdge)kv.Value;
                MorphMeshVertex a = mesh[kv.Key.A];
                MorphMeshVertex b = mesh[kv.Key.B];
                Console.WriteLine(
                    $"    {kv.Key.A}[{a.ShapeIndex}]-{kv.Key.B}[{b.ShapeIndex}] type={edge.Type} faces={edge.Faces.Count} " +
                    $"ribbonEdge={mesh.IsRibbonBoundaryEdge(kv.Key)} " +
                    $"A=({a.Position.X:F1},{a.Position.Y:F1},{a.Position.Z:F0}) B=({b.Position.X:F1},{b.Position.Y:F1},{b.Position.Z:F0})");
            }

            Console.WriteLine("  faces:");
            foreach (var face in mesh.Faces)
                Console.WriteLine($"    [{string.Join(",", face.iVerts)}]");
            Console.WriteLine(report.IsValidSliceSurface && mesh.GenerationHadErrors == false
                ? "\nCLASSIFICATION: OK"
                : "\nCLASSIFICATION: InvalidSurface");
        }

        private static void DumpPoints(Vector2[] pts)
        {
            for (int i = 0; i < pts.Length; i++)
                Console.WriteLine($"    [{i}] ({pts[i].X.ToString("R", CultureInfo.InvariantCulture)}, {pts[i].Y.ToString("R", CultureInfo.InvariantCulture)})");
        }
    }
}
