using AnnotationVizLib;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Live diagnostic for a structure made of a single CIRCLE annotation (Multitest pick: structure 51309, Z 85400),
    /// which renders as a flat disc.  Reports the circle radius against the cap height and the per-slice mesh so the
    /// shape can be compared with what the viewer shows.
    /// </summary>
    [TestClass]
    public class SingleCircleStructureDiagnostic
    {
        private static readonly Uri Endpoint = Viking.Common.ODataEndpointCatalog.EndpointMap[Viking.Common.Endpoint.RPC1];

        [DataTestMethod]
        [TestCategory("LiveData")]
        [Timeout(300000)]
        [DataRow(51309L)]
        public async Task DescribeSingleCircleStructure(long structureId)
        {
            MorphologyGraph root = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync(
                [structureId], include_children: false, Endpoint);
            MorphologyGraph structure = root.Subgraphs.TryGetValue((ulong)structureId, out MorphologyGraph sub) ? sub : root;

            Console.WriteLine($"Structure {structureId}: nodes={structure.Nodes.Count} edges={structure.Edges.Count} sectionThickness={structure.SectionThickness}");
            foreach (MorphologyNode n in structure.Nodes.Values)
            {
                Console.WriteLine(
                    $"  loc {n.ID}: type={n.Location.TypeCode} Z={n.Z:F1} section={n.Location.UnscaledZ} " +
                    $"bbox={n.BoundingBox} neighbors=[{string.Join(",", n.Edges.Keys)}]");
            }

            SliceGraph slices = await SliceGraph.Create(structure, ContourSimplifyOptions.Default);
            Console.WriteLine($"\nSliceGraph: slices={slices.Nodes.Count} failedTopology={slices.FailedTopologyCount}");

            foreach (Slice slice in slices.Nodes.Values.OrderBy(s => s.Key))
            {
                Console.WriteLine($"\nSlice {slice.Key}: above=[{string.Join(",", slice.NodesAbove)}] below=[{string.Join(",", slice.NodesBelow)}] " +
                                  $"hasSliceAbove={slice.HasSliceAbove} hasSliceBelow={slice.HasSliceBelow}");

                SliceTopology topology = slices.GetTopology(slice);
                if (topology.IsValid == false)
                {
                    Console.WriteLine("  topology invalid");
                    continue;
                }

                Console.WriteLine($"  sliceThickness={topology.SliceThickness} centerZ={topology.SliceCenterZ}");
                for (int i = 0; i < topology.Shapes.Length; i++)
                {
                    IShape2D shape = topology.Shapes[i];
                    string extent = shape is Polygon pg
                        ? $"ring={pg.ExteriorRing.Length} bbox={pg.BoundingBox} area={pg.Area:F0}"
                        : shape.GetType().Name;
                    Circle c = topology.ShapeCircles?[i] ?? default;
                    Console.WriteLine($"  shape[{i}] morph={topology.ShapeIndexToMorphNodeIndex[i]} upper={topology.IsUpper[i]} Z={topology.ShapeZ[i]:F1} " +
                                      $"type={topology.ShapeLocationTypes[i]} circleR={c.Radius:F1} {extent}");
                }

                BajajGeneratorMesh mesh = new(topology, slice);
                BajajMeshGenerator.GenerateFaces(mesh);

                double minZ = mesh.Vertices.Min(v => v.Position.Z);
                double maxZ = mesh.Vertices.Max(v => v.Position.Z);
                int capVerts = mesh.Vertices.Count(v => v.ShapeIndex is null);
                Console.WriteLine($"  mesh: verts={mesh.Vertices.Count} (cap={capVerts}) faces={mesh.Faces.Count} Z=[{minZ:F1},{maxZ:F1}] height={maxZ - minZ:F1}");
                Console.WriteLine($"  report: {mesh.ManifoldReport}");

                DescribeNormals(mesh);
            }
        }

        private static void DescribeNormals(BajajGeneratorMesh mesh)
        {
            var normals = mesh.Faces.Select(f => mesh.Normal([.. f.iVerts])).ToArray();
            Console.WriteLine($"  face normals: up={normals.Count(n => n.Z > 0)} down={normals.Count(n => n.Z < 0)} flat={normals.Count(n => n.Z == 0)}");
            Console.WriteLine($"  vertex normals: up={mesh.Vertices.Count(v => v.Normal.Z > 0.5)} down={mesh.Vertices.Count(v => v.Normal.Z < -0.5)} sideways={mesh.Vertices.Count(v => Math.Abs(v.Normal.Z) <= 0.5)}");

            //Caps occupy the half section beyond the outermost contours; report their winding separately so an
            //inverted cap stands out from the band.
            double halfThickness = mesh.SliceThickness / 2.0;
            double topFloor = mesh.Vertices.Max(v => v.Position.Z) - halfThickness - 1e-3;
            double bottomCeiling = mesh.Vertices.Min(v => v.Position.Z) + halfThickness + 1e-3;
            var topNormals = mesh.Faces.Where(f => f.iVerts.All(i => mesh[i].Position.Z >= topFloor)).Select(f => mesh.Normal([.. f.iVerts])).ToArray();
            var bottomNormals = mesh.Faces.Where(f => f.iVerts.All(i => mesh[i].Position.Z <= bottomCeiling)).Select(f => mesh.Normal([.. f.iVerts])).ToArray();
            Console.WriteLine($"  top-cap faces (Z >= {topFloor:F1}): {topNormals.Length} up={topNormals.Count(n => n.Z > 0)} down={topNormals.Count(n => n.Z < 0)}");
            Console.WriteLine($"  bottom-cap faces (Z <= {bottomCeiling:F1}): {bottomNormals.Length} up={bottomNormals.Count(n => n.Z > 0)} down={bottomNormals.Count(n => n.Z < 0)}");
        }

        /// <summary>
        /// Reference: a two-circle structure whose caps render correctly, to compare cap winding against the
        /// isolated circle above.
        /// </summary>
        [DataTestMethod]
        [TestCategory("LiveData")]
        [Timeout(300000)]
        [DataRow(368453L, 368452L)]
        public async Task DescribeCirclePairCaps(long upperId, long lowerId)
        {
            MorphologyGraph graph = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync([upperId, lowerId], Endpoint, hops: 1);
            SliceGraph slices = await SliceGraph.Create(graph, ContourSimplifyOptions.Default);
            foreach (Slice slice in slices.Nodes.Values.OrderBy(s => s.Key))
            {
                if (slice.AllNodes.Contains((ulong)upperId) == false || slice.AllNodes.Contains((ulong)lowerId) == false)
                    continue;

                SliceTopology topology = slices.GetTopology(slice);
                Console.WriteLine($"Slice {slice.Key}: above=[{string.Join(",", slice.NodesAbove)}] below=[{string.Join(",", slice.NodesBelow)}] hasSliceAbove={slice.HasSliceAbove} hasSliceBelow={slice.HasSliceBelow} thickness={topology.SliceThickness}");
                BajajGeneratorMesh mesh = new(topology, slice);
                BajajMeshGenerator.GenerateFaces(mesh);
                Console.WriteLine($"  mesh: verts={mesh.Vertices.Count} faces={mesh.Faces.Count} Z=[{mesh.Vertices.Min(v => v.Position.Z):F1},{mesh.Vertices.Max(v => v.Position.Z):F1}]");
                DescribeNormals(mesh);
            }
        }
    }
}
