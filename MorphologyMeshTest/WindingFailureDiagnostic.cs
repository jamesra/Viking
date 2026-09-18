using AnnotationVizLib;
using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Live dump of winding-patch structure for slices that threw from
    /// <see cref="MeshWindingReorientation"/> in the RPC1 2628 BajajMultiTest run, plus the kidney-hole pick.
    /// Does not assert a complete surface; it prints 2-face contour edges, non-manifold junctions, and the
    /// conflict edge named in the recorded winding error so the winding walk can be blamed or cleared.
    /// </summary>
    [TestClass]
    public class WindingFailureDiagnostic
    {
        private static readonly Uri Endpoint = Viking.Common.ODataEndpointCatalog.EndpointMap[Viking.Common.Endpoint.RPC1];
        private const int LoadHops = 3;

        [DataTestMethod]
        [TestCategory("LiveData")]
        [Timeout(300000)]
        [DataRow("365824,365826", DisplayName = "3054 non-orientable 50 faces")]
        [DataRow("364728,364729,364761", DisplayName = "2449 non-orientable 46 faces")]
        [DataRow("102434,102614,364316,364317,364318,364384", DisplayName = "445 ray-seed 2-face patch")]
        [DataRow("108112,108210,108211", DisplayName = "634 non-orientable 544 faces")]
        [DataRow("112796,112815", DisplayName = "nearby 112815 slice")]
        public async Task DumpWindingState(string locationCsv)
        {
            ulong[] locations = [.. locationCsv.Split(',').Select(ulong.Parse)];
            MorphologyGraph morphology = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync(
                [.. locations.Select(id => (long)id)], Endpoint, LoadHops);
            foreach (ulong id in locations)
                Assert.IsTrue(morphology.Nodes.ContainsKey(id), $"Missing location {id}");

            MorphologyGraph.CurveFitProcesses(morphology);

            SliceGraph slices = await SliceGraph.Create(morphology, ContourSimplifyOptions.Default);
            Slice target = slices.Nodes.Values.FirstOrDefault(s => locations.All(id => s.AllNodes.Contains(id)));
            Assert.IsNotNull(target, $"No slice contains {locationCsv}");
            Console.WriteLine($"Slice {target.Key} [{string.Join(",", target.AllNodes)}] {slices.FormatSectionNumbers(target)}");
            Console.WriteLine($"  above=[{string.Join(",", target.NodesAbove)}] below=[{string.Join(",", target.NodesBelow)}]");

            SliceTopology topology = slices.GetTopology(target);
            Assert.IsTrue(topology.IsValid, "Topology failed");
            for (int i = 0; i < topology.Shapes.Length; i++)
            {
                IShape2D shape = topology.Shapes[i];
                int pts = shape is Polyline pl ? pl.PointCount : shape is Polygon pg ? pg.ExteriorRing.Length : -1;
                Console.WriteLine(
                    $"  shape[{i}] loc={topology.ShapeIndexToMorphNodeIndex[i]} upper={topology.IsUpper[i]} " +
                    $"Z={topology.ShapeZ[i]:F1} type={topology.ShapeLocationTypes[i]} pts={pts}");
            }

            BajajGeneratorMesh mesh = new(topology, target);
            BajajMeshGenerator.GenerateFaces(mesh);

            Console.WriteLine($"faces={mesh.Faces.Count} verts={mesh.Vertices.Count} errors={mesh.GenerationHadErrors}");
            Console.WriteLine($"report: {mesh.ManifoldReport}");
            foreach (string error in mesh.GenerationErrors)
                Console.WriteLine($"  error: {error}");

            var stats = MeshWindingDiagnostics.Analyze(mesh);
            Console.WriteLine(
                $"winding: manifold={stats.ManifoldEdges} inconsistent={stats.InconsistentManifoldEdges} " +
                $"nonManifold={stats.NonManifoldEdges} boundary={stats.BoundaryEdges} " +
                $"inconsistentAway={MeshWindingDiagnostics.CountInconsistentAwayFromNonManifold(mesh)}");

            List<List<IFace>> patches = MeshWindingReorientation.CollectTwoManifoldPatches(mesh);
            Console.WriteLine($"patches={patches.Count}");
            for (int i = 0; i < patches.Count; i++)
            {
                List<IFace> patch = patches[i];
                int boundary = 0;
                int contourTwoFace = 0;
                foreach (IFace face in patch)
                {
                    foreach (IEdgeKey ek in face.Edges)
                    {
                        IEdge edge = mesh.Edges[ek];
                        if (edge.Faces.Count == 1)
                            boundary++;
                        if (edge.Faces.Count == 2 && edge is MorphMeshEdge morph && morph.Type == EdgeType.CONTOUR)
                            contourTwoFace++;
                    }
                }

                Console.WriteLine($"  patch[{i}] faces={patch.Count} boundaryWalks={boundary} twoFaceContourWalks={contourTwoFace}");
            }

            Console.WriteLine("2-face CONTOUR edges (winding treats these as interior):");
            int twoFaceContour = 0;
            foreach (var kvp in mesh.Edges)
            {
                if (kvp.Value.Faces.Count != 2 || kvp.Value is not MorphMeshEdge morph || morph.Type != EdgeType.CONTOUR)
                    continue;

                twoFaceContour++;
                DumpEdge(mesh, kvp.Key, morph);
            }

            if (twoFaceContour == 0)
                Console.WriteLine("  (none)");

            Console.WriteLine("non-manifold edges:");
            int nonManifold = 0;
            foreach (var kvp in mesh.Edges.Where(k => k.Value.Faces.Count > 2))
            {
                nonManifold++;
                DumpEdge(mesh, kvp.Key, (MorphMeshEdge)kvp.Value);
            }

            if (nonManifold == 0)
                Console.WriteLine("  (none)");

            Console.WriteLine("inconsistent 2-manifold edges:");
            int inconsistent = 0;
            foreach (var kvp in mesh.Edges)
            {
                if (kvp.Value.Faces.Count != 2)
                    continue;

                IFace[] faces = [.. kvp.Value.Faces];
                if (Traverses(faces[0].iVerts, kvp.Key.A, kvp.Key.B) != Traverses(faces[1].iVerts, kvp.Key.A, kvp.Key.B))
                    continue;

                inconsistent++;
                DumpEdge(mesh, kvp.Key, (MorphMeshEdge)kvp.Value);
            }

            if (inconsistent == 0)
                Console.WriteLine("  (none)");

            foreach (string error in mesh.GenerationErrors)
            {
                Match conflict = Regex.Match(error, @"conflict at edge (\d+)-(\d+)");
                if (conflict.Success == false)
                    continue;

                int a = int.Parse(conflict.Groups[1].Value);
                int b = int.Parse(conflict.Groups[2].Value);
                Console.WriteLine($"conflict edge {a}-{b}:");
                if (mesh.Edges.TryGetValue(new EdgeKey(a, b), out IEdge conflictEdge) == false)
                {
                    Console.WriteLine("  not present after generation");
                    continue;
                }

                DumpEdge(mesh, new EdgeKey(a, b), (MorphMeshEdge)conflictEdge);
                foreach (IFace face in conflictEdge.Faces)
                {
                    Console.WriteLine($"    face [{string.Join(",", face.iVerts)}] " +
                                      $"shapes=[{string.Join(",", face.iVerts.Select(i => mesh[i].ShapeIndex))}] " +
                                      $"forward={Traverses(face.iVerts, a, b)}");
                    foreach (IEdgeKey ek in face.Edges)
                    {
                        MorphMeshEdge adj = (MorphMeshEdge)mesh.Edges[ek];
                        Console.WriteLine($"      adj {ek.A}-{ek.B} type={adj.Type} faces={adj.Faces.Count}");
                    }
                }
            }
        }

        private static void DumpEdge(BajajGeneratorMesh mesh, IEdgeKey key, MorphMeshEdge edge)
        {
            MorphMeshVertex va = mesh[key.A];
            MorphMeshVertex vb = mesh[key.B];
            Console.WriteLine(
                $"  {key.A}[{va.ShapeIndex}]-{key.B}[{vb.ShapeIndex}] type={edge.Type} faces={edge.Faces.Count} " +
                $"A=({va.Position.X:F1},{va.Position.Y:F1},{va.Position.Z:F0}) " +
                $"B=({vb.Position.X:F1},{vb.Position.Y:F1},{vb.Position.Z:F0})");
            foreach (IFace face in edge.Faces)
                Console.WriteLine($"    face [{string.Join(",", face.iVerts)}] forward={Traverses(face.iVerts, key.A, key.B)}");
        }

        private static bool Traverses(System.Collections.Immutable.ImmutableArray<int> iVerts, int a, int b)
        {
            for (int i = 0; i < iVerts.Length; i++)
            {
                int x = iVerts[i];
                int y = iVerts[(i + 1) % iVerts.Length];
                if (x == a && y == b)
                    return true;
                if (x == b && y == a)
                    return false;
            }

            return false;
        }
    }
}
