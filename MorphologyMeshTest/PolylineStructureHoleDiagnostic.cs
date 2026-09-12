using AnnotationVizLib;
using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Live diagnostic for a hairline hole in a polyline (glial adjacency) structure: Multitest pick was structure
    /// 52432, slice 368197/368198, hole near 368195.  Meshes every slice, reports which are not valid surfaces,
    /// then stitches the slice meshes together by vertex position and lists the edges that end up with a single
    /// face, which is what a hole in the assembled surface looks like.
    /// </summary>
    [TestClass]
    public class PolylineStructureHoleDiagnostic
    {
        private static readonly Uri Endpoint = Viking.Common.ODataEndpointCatalog.EndpointMap[Viking.Common.Endpoint.RPC1];

        [DataTestMethod]
        [TestCategory("LiveData")]
        [Timeout(600000)]
        [DataRow(52432L, 368195UL, true, false)]
        [DataRow(52432L, 368197UL, false, true)]
        public async Task FindHoleNearLocation(long structureId, ulong nearLocation, bool zeroOrigin, bool smoothProcesses)
        {
            MorphologyGraph root = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync([structureId], include_children: false, Endpoint);
            //BajajMultiTest applies CurveFitProcesses under --correction all by default, which can move process
            //and terminal nodes and change which Delaunay triangles the ribbon starts from.
            if (smoothProcesses)
                MorphologyGraph.CurveFitProcesses(root);
            MorphologyGraph structure = root.Subgraphs.TryGetValue((ulong)structureId, out MorphologyGraph sub) ? sub : root;
            Console.WriteLine($"Structure {structureId}: nodes={structure.Nodes.Count} edges={structure.Edges.Count} type={structure.structureType?.Name}");

            MorphologyNode focus = structure.Nodes[nearLocation];
            HashSet<ulong> neighborhood = [nearLocation, .. focus.Edges.Keys];
            foreach (ulong id in neighborhood.OrderBy(i => i))
            {
                MorphologyNode n = structure.Nodes[id];
                int pts = n.Geometry is null ? -1 : n.Geometry.STNumPoints().Value;
                Console.WriteLine($"  loc {id}: type={n.Location.TypeCode} Z={n.Z:F1} section={n.Location.UnscaledZ} pts={pts} neighbors=[{string.Join(",", n.Edges.Keys)}]");
            }

            //Zero origin keeps mesh vertices in volume XY so they can be compared with location bounding boxes; the
            //default origin reproduces BajajMultiTest's coordinates, which can change Delaunay tie-breaking.
            SliceGraph slices = await SliceGraph.Create(structure, ContourSimplifyOptions.Default, zeroOrigin ? Vector2.Zero : null);
            Vector2 origin = slices.XYOrigin;
            Console.WriteLine($"\nSliceGraph: slices={slices.Nodes.Count} failedTopology={slices.FailedTopologyCount}");

            Dictionary<ulong, BajajGeneratorMesh> meshes = [];
            foreach (Slice slice in slices.Nodes.Values.OrderBy(s => s.Key))
            {
                SliceTopology topology = slices.GetTopology(slice);
                if (topology.IsValid == false)
                {
                    Console.WriteLine($"Slice {slice.Key} [{string.Join(",", slice.AllNodes)}]: TOPOLOGY INVALID");
                    continue;
                }

                BajajGeneratorMesh mesh = new(topology, slice);
                try
                {
                    BajajMeshGenerator.GenerateFaces(mesh);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Slice {slice.Key} [{string.Join(",", slice.AllNodes)}]: THREW {e.GetType().Name}: {e.Message}");
                    continue;
                }

                meshes[slice.Key] = mesh;
                bool touchesFocus = slice.AllNodes.Overlaps(neighborhood);
                if (mesh.ManifoldReport.IsValidSliceSurface == false || touchesFocus)
                {
                    Console.WriteLine($"Slice {slice.Key} above=[{string.Join(",", slice.NodesAbove)}] below=[{string.Join(",", slice.NodesBelow)}] " +
                                      $"hasAbove={slice.HasSliceAbove} hasBelow={slice.HasSliceBelow} faces={mesh.Faces.Count} {(mesh.ManifoldReport.IsValidSliceSurface ? "ok" : "INVALID")}: {mesh.ManifoldReport}");
                }

                if (touchesFocus)
                {
                    DumpSingleFaceEdges(mesh);
                    Console.WriteLine("    faces:");
                    foreach (IFace face in mesh.Faces)
                        Console.WriteLine($"      [{string.Join(", ", face.iVerts.Select(i => $"{i}{Label(mesh[i])}"))}]");
                }
            }

            Console.WriteLine("\n=== Stitched surface (all slices merged by vertex position) ===");
            StitchAndReport(meshes.Values, structure, neighborhood, origin);
        }

        private static string Label(MorphMeshVertex v) =>
            v.ShapeIndex is PolylineIndex pi ? $"(L{pi.ShapeIndex}:{pi.VertexIndex})" : v.ShapeIndex is null ? "(cap)" : $"({v.ShapeIndex})";

        private static void DumpSingleFaceEdges(BajajGeneratorMesh mesh)
        {
            foreach (var kv in mesh.Edges.Where(k => k.Value.Faces.Count != 2).OrderBy(k => k.Key.A))
            {
                MorphMeshEdge edge = (MorphMeshEdge)kv.Value;
                MorphMeshVertex a = mesh[kv.Key.A];
                MorphMeshVertex b = mesh[kv.Key.B];
                Console.WriteLine($"    {kv.Key.A}[{a.ShapeIndex}]-{kv.Key.B}[{b.ShapeIndex}] type={edge.Type} faces={edge.Faces.Count} ribbonEdge={mesh.IsRibbonBoundaryEdge(kv.Key)} " +
                                  $"A=({a.Position.X:F1},{a.Position.Y:F1},{a.Position.Z:F0}) B=({b.Position.X:F1},{b.Position.Y:F1},{b.Position.Z:F0})");
            }
        }

        /// <summary>
        /// Merge every slice mesh into one edge table keyed by rounded endpoint positions.  Edges seen by only one
        /// face across the whole structure are open boundary: legitimate at a ribbon's free ends, a hole anywhere else.
        /// </summary>
        private static void StitchAndReport(IEnumerable<BajajGeneratorMesh> meshes, MorphologyGraph structure, HashSet<ulong> neighborhood, Vector2 origin)
        {
            static (long, long, long) Key(Vector3 p) => ((long)Math.Round(p.X * 100), (long)Math.Round(p.Y * 100), (long)Math.Round(p.Z * 100));

            Dictionary<((long, long, long), (long, long, long)), int> faceCount = [];
            Dictionary<(long, long, long), Vector3> positions = [];
            foreach (BajajGeneratorMesh mesh in meshes)
            {
                foreach (IFace face in mesh.Faces)
                {
                    int n = face.iVerts.Length;
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 pa = mesh[face.iVerts[i]].Position;
                        Vector3 pb = mesh[face.iVerts[(i + 1) % n]].Position;
                        var ka = Key(pa);
                        var kb = Key(pb);
                        positions[ka] = pa;
                        positions[kb] = pb;
                        var key = ka.CompareTo(kb) <= 0 ? (ka, kb) : (kb, ka);
                        faceCount[key] = faceCount.TryGetValue(key, out int c) ? c + 1 : 1;
                    }
                }
            }

            var open = faceCount.Where(kv => kv.Value == 1).ToArray();
            var overshared = faceCount.Where(kv => kv.Value > 2).ToArray();
            Console.WriteLine($"edges={faceCount.Count} open(1 face)={open.Length} nonManifold(>2 faces)={overshared.Length}");

            //Report open edges near the focus locations so the interesting ones are not lost in the free ribbon ends.
            var focusBoxes = neighborhood.Select(id => structure.Nodes[id].BoundingBox).ToArray();
            foreach (var kv in open.OrderBy(kv => positions[kv.Key.Item1].Z).ThenBy(kv => positions[kv.Key.Item1].X))
            {
                Vector3 a = positions[kv.Key.Item1];
                Vector3 b = positions[kv.Key.Item2];
                Vector3 shift = new(origin.X, origin.Y, 0);
                bool nearFocus = focusBoxes.Any(bb => bb.Contains(a + shift) || bb.Contains(b + shift));
                Console.WriteLine($"  open A=({a.X:F1},{a.Y:F1},{a.Z:F0}) B=({b.X:F1},{b.Y:F1},{b.Z:F0}) len={Vector3.Distance(a, b):F1}{(nearFocus ? "  <== near focus" : "")}");
            }
        }
    }
}
