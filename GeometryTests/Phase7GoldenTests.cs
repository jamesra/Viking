using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Whole-pipeline fingerprints recorded from the code as it was before Phase 7. They cover outputs that have no
    /// reference copy to compare against: the full Delaunay mesh (face order, edge map enumeration order, per-vertex edge
    /// order), face paths, and curves built from the public entry points.
    /// </summary>
    [TestClass]
    public class Phase7GoldenTests
    {
        private static IEnumerable<long> MeshValues(TriangulationMesh<TriangulationVertex> mesh)
        {
            yield return mesh.Vertices.Count;
            foreach (IFace face in mesh.Faces)
            {
                yield return face.iVerts.Length;
                foreach (int v in face.iVerts)
                    yield return v;
            }

            foreach (KeyValuePair<IEdgeKey, IEdge> pair in mesh.Edges)
            {
                yield return pair.Key.A;
                yield return pair.Key.B;
                foreach (IFace face in pair.Value.Faces)
                    foreach (int v in face.iVerts)
                        yield return v;
            }

            foreach (TriangulationVertex vertex in mesh.Vertices)
            {
                yield return vertex.Index;
                foreach (IEdgeKey edge in vertex.Edges)
                {
                    yield return edge.A;
                    yield return edge.B;
                }
            }
        }

        private static IEnumerable<long> PathValues(TriangulationMesh<TriangulationVertex> mesh)
        {
            foreach (TriangulationVertex vertex in mesh.Vertices.Take(60))
            {
                IFace[] fan = [.. mesh.Faces.Where(f => f.iVerts.Contains(vertex.Index))];
                if (fan.Length < 2)
                    continue;

                foreach (IEdgeKey spoke in vertex.Edges)
                {
                    List<IFace> path = mesh.FindFacesInPath(fan[0], f => f.iVerts.Contains(vertex.Index), f => f.Edges.Contains(spoke));
                    yield return path is null ? -1 : path.Count;
                    if (path is null)
                        continue;
                    foreach (IFace face in path)
                        foreach (int v in face.iVerts)
                            yield return v;
                }
            }
        }

        private static IEnumerable<long> IntValues(int[] values)
        {
            yield return values.Length;
            foreach (int v in values)
                yield return v;
        }

        private static Vector2[] Ring(int seed, int count, double radius, double cx, double cy, double jitter)
        {
            Random random = new(seed);
            Vector2[] ring = new Vector2[count + 1];
            for (int i = 0; i < count; i++)
            {
                double angle = 2 * Math.PI * i / count;
                double r = radius * (1 + (jitter * random.NextDouble()));
                ring[i] = new Vector2(cx + (r * Math.Cos(angle)), cy + (r * Math.Sin(angle)));
            }

            ring[count] = ring[0];
            return ring;
        }

        private static Vector2[] OpenPath(int seed, int count, double step, double origin)
        {
            Random random = new(seed);
            Vector2[] path = new Vector2[count];
            Vector2 position = new(origin, origin);
            for (int i = 0; i < count; i++)
            {
                position += new Vector2(step * (0.2 + random.NextDouble()), step * (random.NextDouble() - 0.5));
                path[i] = position;
            }

            return path;
        }

        internal static SortedDictionary<string, string> Compute()
        {
            SortedDictionary<string, string> values = [];

            Vector2[] random1000 = Phase7MeshCurveTests.RandomPoints(11, 1000, 500000);
            values["delaunay random1000"] = Phase7MeshCurveTests.FingerprintOf(IntValues(Delaunay2D.Triangulate(random1000)));
            values["delaunay grid40"] = Phase7MeshCurveTests.FingerprintOf(IntValues(Delaunay2D.Triangulate(Phase7MeshCurveTests.GridPoints(40, 40, 128, 100000, 200000))));
            values["delaunay circle64"] = Phase7MeshCurveTests.FingerprintOf(IntValues(Delaunay2D.Triangulate(Phase7MeshCurveTests.CirclePoints(64, 250000, 250000, 250000))));
            values["delaunay tiny"] = Phase7MeshCurveTests.FingerprintOf(IntValues(Delaunay2D.Triangulate(Phase7MeshCurveTests.RandomPoints(13, 150, 4))));

            (string, Vector2[])[] meshInputs =
            [
                ("random300", Phase7MeshCurveTests.RandomPoints(71, 300, 500000)),
                ("grid14", Phase7MeshCurveTests.GridPoints(14, 14, 64, 400000, 400000)),
                ("circle60", Phase7MeshCurveTests.CirclePoints(60, 120000, 250000, 250000).Append(new Vector2(250000, 250000)).ToArray()),
                ("tiny100", Phase7MeshCurveTests.RandomPoints(72, 100, 8)),
            ];

            foreach ((string name, Vector2[] points) in meshInputs)
            {
                TriangulationMesh<TriangulationVertex> mesh = DelaunayMeshGenerator2D.TriangulateToMesh(points);
                values["mesh " + name] = Phase7MeshCurveTests.FingerprintOf(MeshValues(mesh));
                values["mesh paths " + name] = Phase7MeshCurveTests.FingerprintOf(PathValues(mesh));
            }

            Vector2[] ring200 = Ring(81, 200, 5000, 250000, 250000, 0.1);
            values["curve ring200 n8"] = Phase7MeshCurveTests.FingerprintOf(Phase7MeshCurveTests.Bits(ring200.CalculateCurvePoints(8, true)));
            values["curve ring200 n3"] = Phase7MeshCurveTests.FingerprintOf(Phase7MeshCurveTests.Bits(ring200.CalculateCurvePoints(3, true)));
            values["curve ring40 n1"] = Phase7MeshCurveTests.FingerprintOf(Phase7MeshCurveTests.Bits(Ring(82, 40, 800, 100000, 100000, 0.3).CalculateCurvePoints(1, true)));
            values["curve ring30 tiny n8"] = Phase7MeshCurveTests.FingerprintOf(Phase7MeshCurveTests.Bits(Ring(83, 30, 4, 10, 10, 0.2).CalculateCurvePoints(8, true)));
            values["curve open60 n8"] = Phase7MeshCurveTests.FingerprintOf(Phase7MeshCurveTests.Bits(OpenPath(84, 60, 900, 100000).CalculateCurvePoints(8, false)));
            values["curve open5 n20"] = Phase7MeshCurveTests.FingerprintOf(Phase7MeshCurveTests.Bits(OpenPath(85, 5, 5000, 400000).CalculateCurvePoints(20, false)));
            values["curve ring200 gaussian"] = Phase7MeshCurveTests.FingerprintOf(Phase7MeshCurveTests.Bits(Smoothing.Gaussian(ring200)));
            values["curve ring200 control points"] = Phase7MeshCurveTests.FingerprintOf(Phase7MeshCurveTests.Bits(((IReadOnlyList<Vector2>)ring200).IdentifyControlPoints(2.0, true, 8)));
            values["curve open60 control points"] = Phase7MeshCurveTests.FingerprintOf(Phase7MeshCurveTests.Bits(((IReadOnlyList<Vector2>)OpenPath(84, 60, 900, 100000)).IdentifyControlPoints(5.0, false, 8)));

            return values;
        }

        /// <summary>
        /// Recorded on the code before Phase 7. Two curve and mesh inputs go through Math.Cos or Math.Pow results that
        /// differ in the last bit between .NET Framework and .NET 9, so those two are recorded per runtime.
        /// </summary>
        private static readonly Dictionary<string, string> Recorded = new()
        {
            ["curve open5 n20"] = "a1adeb34c1871086",
            ["curve open60 control points"] = "4355f7fb8dbf9df6",
            ["curve open60 n8"] = "675c8e56217cd018",
            ["curve ring200 control points"] = "3994294dd67a5466",
            ["curve ring200 gaussian"] = "88fc6d413ba665ac",
            ["curve ring200 n3"] = "eced2c18f07747f0",
            ["curve ring200 n8"] = "d0813813b394ef4c",
#if NETFRAMEWORK
            ["curve ring30 tiny n8"] = "8a691514a63d156f",
            ["mesh circle60"] = "7cbc9d2918923704",
#else
            ["curve ring30 tiny n8"] = "a07c868a77de645a",
            ["mesh circle60"] = "ba32540e95ccdce4",
#endif
            ["curve ring40 n1"] = "6a25223a2314e22d",
            ["delaunay circle64"] = "e88601271f0b3a7c",
            ["delaunay grid40"] = "2890b6dd8b815596",
            ["delaunay random1000"] = "8653b063a73842de",
            ["delaunay tiny"] = "7c350ef9e4db1798",
            ["mesh grid14"] = "b80842d8012d70ee",
            ["mesh paths circle60"] = "38b0592d3ba2a4a5",
            ["mesh paths grid14"] = "29a86b3825e6f7f8",
            ["mesh paths random300"] = "2fe5bdb2a36b5630",
            ["mesh paths tiny100"] = "97dbb3980455647e",
            ["mesh random300"] = "42057da0dad82711",
            ["mesh tiny100"] = "b8cc6676879866df",
        };

        [TestMethod]
        public void WholePipelineFingerprintsMatchTheRecordedValues()
        {
            SortedDictionary<string, string> actual = Compute();

            foreach (var pair in actual)
            {
                Assert.IsTrue(Recorded.TryGetValue(pair.Key, out string expected), "No recorded value for " + pair.Key);
                Assert.AreEqual(expected, pair.Value, pair.Key);
            }

            Assert.AreEqual(Recorded.Count, actual.Count);
        }
    }
}
