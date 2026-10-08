using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Phase 7 equivalence tests for the divide-and-conquer mesh helpers (<c>EdgesByAngle</c>) and the face path search.
    /// </summary>
    [TestClass]
    public class Phase7MeshTests
    {
        #region Mesh builders

        private static TriangulationMesh<TriangulationVertex> DelaunayMesh(Vector2[] points) => DelaunayMeshGenerator2D.TriangulateToMesh(points);

        /// <summary>
        /// A hub vertex 0 at the origin with spokes to vertices placed on compass directions at several distances, so
        /// many edges have exactly the same angle, plus some at arbitrary angles.
        /// </summary>
        private static TriangulationMesh<TriangulationVertex> StarMesh(Random random, int spokes, bool includeArbitraryAngles)
        {
            int[][] directions = [[1, 0], [0, 1], [-1, 0], [0, -1], [1, 1], [-1, 1], [-1, -1], [1, -1]];
            HashSet<(int, int)> used = [];
            List<Vector2> points = [new Vector2(0, 0)];
            while (points.Count <= spokes)
            {
                if (includeArbitraryAngles && random.NextDouble() < 0.4)
                {
                    points.Add(new Vector2((random.NextDouble() - 0.5) * 200, (random.NextDouble() - 0.5) * 200));
                    continue;
                }

                int[] d = directions[random.Next(directions.Length)];
                int distance = random.Next(1, 8);
                if (used.Add((d[0] * distance, d[1] * distance)))
                    points.Add(new Vector2(d[0] * distance * 10, d[1] * distance * 10));
            }

            TriangulationMesh<TriangulationVertex> mesh = new();
            mesh.CreateEdge ??= Edge.Create;
            foreach (Vector2 point in points)
                mesh.AddVertex(new TriangulationVertex(point));
            for (int i = 1; i < points.Count; i++)
                mesh.AddEdge(0, i);
            return mesh;
        }

        private static Mesh2D QuadGridMesh(int columns, int rows)
        {
            Mesh2D mesh = new();
            if (mesh.CreateEdge is null)
                mesh.CreateEdge = Edge.Create;
            if (mesh.CreateFace is null)
                mesh.CreateFace = Face.Create;

            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= columns; x++)
                    mesh.AddVertex(new Vertex2D(new Vector2(x * 10, y * 10)));

            int stride = columns + 1;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    int a = (y * stride) + x;
                    mesh.AddFace(new Face(a, a + 1, a + 1 + stride, a + stride));
                }

            return mesh;
        }

        #endregion

        #region EdgesByAngle

        private static int AssertSameEdgesByAngle(TriangulationMesh<TriangulationVertex> mesh, string context)
        {
            int ties = 0;
            foreach (TriangulationVertex origin in mesh.Vertices)
            {
                foreach (IEdgeKey spoke in origin.Edges)
                {
                    long target = spoke.OppositeEnd((long)origin.Index);
                    foreach (bool clockwise in new[] { false, true })
                    {
                        EdgeAngle[] expected = Phase7ReferenceImplementations.EdgesByAngle(mesh, origin, target, clockwise);
                        EdgeAngle[] actual = GenericDelaunayMeshGenerator2D<TriangulationVertex>.EdgesByAngle(mesh, origin, target, clockwise);
                        string where = $"{context}: origin {origin.Index} target {target} clockwise {clockwise}";

                        Assert.AreEqual(expected.Length, actual.Length, where + ": length");
                        for (int i = 0; i < expected.Length; i++)
                        {
                            Assert.AreEqual(expected[i].Origin, actual[i].Origin, where + $": Origin[{i}]");
                            Assert.AreEqual(expected[i].Target, actual[i].Target, where + $": Target[{i}]");
                            Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected[i].Angle), BitConverter.DoubleToInt64Bits(actual[i].Angle), where + $": Angle[{i}]");
                            Assert.AreEqual(expected[i].IsClockwise, actual[i].IsClockwise, where + $": IsClockwise[{i}]");
                            if (i > 0 && expected[i].Angle == expected[i - 1].Angle)
                                ties++;
                        }
                    }
                }
            }

            return ties;
        }

        [TestMethod]
        public void EdgesByAngleMatchesReferenceOnDelaunayMeshes()
        {
            AssertSameEdgesByAngle(DelaunayMesh(Phase7MeshCurveTests.RandomPoints(51, 120, 500000)), "random 120");
            AssertSameEdgesByAngle(DelaunayMesh(Phase7MeshCurveTests.RandomPoints(52, 60, 6)), "tiny 60");
            AssertSameEdgesByAngle(DelaunayMesh(Phase7MeshCurveTests.GridPoints(12, 12, 100, 1000, 1000)), "grid 12x12");
            AssertSameEdgesByAngle(DelaunayMesh(Phase7MeshCurveTests.GridPoints(9, 9, 0.01, 0, 0)), "tiny grid 9x9");
            AssertSameEdgesByAngle(DelaunayMesh(Phase7MeshCurveTests.CirclePoints(40, 100000, 250000, 250000)), "circle 40");
        }

        /// <summary>
        /// Array.Sort(keys, items) is not stable (three elements with ties, and more than sixteen elements, can reorder equal
        /// keys), so the sort call itself is kept. These stars put several spokes at exactly the same angle and use
        /// spoke counts that cross both thresholds, so any change to how ties are ordered shows up here.
        /// </summary>
        [TestMethod]
        public void EdgesByAngleOrdersTiesTheSameWayAsBefore()
        {
            Random random = new(53);
            int ties = 0;
            for (int spokes = 2; spokes <= 45; spokes++)
            {
                for (int rep = 0; rep < 6; rep++)
                {
                    TriangulationMesh<TriangulationVertex> mesh = StarMesh(random, spokes, includeArbitraryAngles: rep % 2 == 1);
                    ties += AssertSameEdgesByAngle(mesh, $"star {spokes} rep {rep}");
                }
            }

            Assert.IsTrue(ties > 100, $"The stars must actually contain tied angles ({ties} found)");
        }

        #endregion

        #region Face path search

        private sealed class CallLog
        {
            public readonly List<string> Calls = [];
        }

        private static void AssertSameFaceList(List<IFace> expected, List<IFace> actual, string context)
        {
            Assert.AreEqual(expected is null, actual is null, context + ": null");
            if (expected is null)
                return;

            Assert.AreEqual(expected.Count, actual.Count, context + ": length");
            for (int i = 0; i < expected.Count; i++)
                Assert.AreSame(expected[i], actual[i], context + $": face {i}");
        }

        private static void AssertSameFaceSet(SortedSet<IFace> expected, SortedSet<IFace> actual, string context)
        {
            IFace[] e = [.. expected];
            IFace[] a = [.. actual];
            Assert.AreEqual(e.Length, a.Length, context + ": checked face count");
            for (int i = 0; i < e.Length; i++)
                Assert.AreSame(e[i], a[i], context + $": checked face {i}");
        }

        /// <summary>
        /// Runs the old and new search and requires the same path (the same face objects), the same checked-face set, and the
        /// same sequence of calls into both callbacks, which is the strongest evidence the traversal order is unchanged.
        /// </summary>
        private static void AssertSameFacePath<VERTEX>(MeshBase<VERTEX> mesh, IFace start, ISet<IFace> blocked, ISet<IFace> targets, string context, bool seedChecked)
            where VERTEX : IVertex
        {
            CallLog oldLog = new();
            CallLog newLog = new();
            Func<IFace, bool> Can(CallLog log) => face => { log.Calls.Add("can " + face); return !blocked.Contains(face); };
            Func<IFace, bool> Meets(CallLog log) => face => { log.Calls.Add("meets " + face); return targets.Contains(face); };

            SortedSet<IFace> expectedChecked = [];
            SortedSet<IFace> actualChecked = [];
            if (seedChecked)
            {
                IFace[] all = [.. mesh.Faces];
                for (int i = 0; i < all.Length; i += 7)
                {
                    if (!ReferenceEquals(all[i], start))
                    {
                        expectedChecked.Add(all[i]);
                        actualChecked.Add(all[i]);
                    }
                }
            }

            List<IFace> expected = Phase7ReferenceImplementations.FindFacesInPath(mesh, start, Can(oldLog), Meets(oldLog), ref expectedChecked);
            List<IFace> actual = mesh.FindFacesInPath(start, Can(newLog), Meets(newLog), ref actualChecked);

            AssertSameFaceList(expected, actual, context);
            AssertSameFaceSet(expectedChecked, actualChecked, context);
            CollectionAssert.AreEqual(oldLog.Calls, newLog.Calls, context + ": callback sequence");

            if (!seedChecked)
            {
                CallLog oldLog2 = new();
                CallLog newLog2 = new();
                List<IFace> expected2 = Phase7ReferenceImplementations.FindFacesInPath(mesh, start, Can(oldLog2), Meets(oldLog2));
                List<IFace> actual2 = mesh.FindFacesInPath(start, Can(newLog2), Meets(newLog2));
                AssertSameFaceList(expected2, actual2, context + " (no seed)");
                CollectionAssert.AreEqual(oldLog2.Calls, newLog2.Calls, context + " (no seed): callback sequence");
            }
        }

        private static (int Found, int Missing) RunRandomPaths<VERTEX>(MeshBase<VERTEX> mesh, int seed, int trials, string name)
            where VERTEX : IVertex
        {
            Random random = new(seed);
            IFace[] faces = [.. mesh.Faces];
            int found = 0, missing = 0;
            for (int trial = 0; trial < trials; trial++)
            {
                double blockedFraction = new[] { 0.0, 0.1, 0.3, 0.5 }[trial % 4];
                HashSet<IFace> blocked = [.. faces.Where(_ => random.NextDouble() < blockedFraction)];
                HashSet<IFace> targets = [.. Enumerable.Range(0, random.Next(1, 4)).Select(_ => faces[random.Next(faces.Length)])];
                IFace start = faces[random.Next(faces.Length)];

                string context = $"{name} trial {trial}";
                AssertSameFacePath(mesh, start, blocked, targets, context, seedChecked: false);
                AssertSameFacePath(mesh, start, blocked, targets, context + " seeded", seedChecked: true);

                List<IFace> path = mesh.FindFacesInPath(start, f => !blocked.Contains(f), f => targets.Contains(f));
                if (path is null)
                    missing++;
                else
                    found++;
            }

            return (found, missing);
        }

        [TestMethod]
        public void FacePathMatchesReferenceOnDelaunayMeshes()
        {
            var random50 = RunRandomPaths(DelaunayMesh(Phase7MeshCurveTests.RandomPoints(61, 45, 500000)), 1, 80, "random 45");
            var grid = RunRandomPaths(DelaunayMesh(Phase7MeshCurveTests.GridPoints(6, 6, 100, 1000, 1000)), 2, 80, "grid 6x6");
            var tiny = RunRandomPaths(DelaunayMesh(Phase7MeshCurveTests.RandomPoints(62, 30, 5)), 3, 60, "tiny 30");

            Assert.IsTrue(random50.Found > 5 && random50.Missing > 5, $"Both outcomes must be exercised: found {random50.Found} missing {random50.Missing}");
            Assert.IsTrue(grid.Found > 5 && grid.Missing > 5, $"Both outcomes must be exercised: found {grid.Found} missing {grid.Missing}");
            Assert.IsTrue(tiny.Found > 5, $"Paths must be found: {tiny.Found}");
        }

        [TestMethod]
        public void FacePathMatchesReferenceOnQuadGrids()
        {
            var small = RunRandomPaths(QuadGridMesh(4, 4), 4, 120, "quads 4x4");
            var wide = RunRandomPaths(QuadGridMesh(7, 3), 5, 120, "quads 7x3");
            var line = RunRandomPaths(QuadGridMesh(12, 1), 6, 60, "quads 12x1");

            Assert.IsTrue(small.Found > 10 && small.Missing > 5, $"found {small.Found} missing {small.Missing}");
            Assert.IsTrue(wide.Found > 10 && wide.Missing > 5, $"found {wide.Found} missing {wide.Missing}");
            Assert.IsTrue(line.Found > 5, $"found {line.Found}");
        }

        /// <summary>
        /// The search MorphMeshVertex runs: walk the fan of faces around one vertex to a face with a given edge.
        /// </summary>
        [TestMethod]
        public void FacePathAroundAVertexMatchesReference()
        {
            TriangulationMesh<TriangulationVertex> mesh = DelaunayMesh(Phase7MeshCurveTests.RandomPoints(63, 200, 500000));
            int compared = 0;
            foreach (TriangulationVertex vertex in mesh.Vertices.Take(80))
            {
                IFace[] fan = [.. mesh.Faces.Where(f => f.iVerts.Contains(vertex.Index))];
                if (fan.Length < 3)
                    continue;

                foreach (IEdgeKey spoke in vertex.Edges)
                {
                    List<IFace> expected = Phase7ReferenceImplementations.FindFacesInPath(mesh, fan[0], f => f.iVerts.Contains(vertex.Index), f => f.Edges.Contains(spoke));
                    List<IFace> actual = mesh.FindFacesInPath(fan[0], f => f.iVerts.Contains(vertex.Index), f => f.Edges.Contains(spoke));
                    AssertSameFaceList(expected, actual, $"vertex {vertex.Index} spoke {spoke}");
                    compared++;
                }
            }

            Assert.IsTrue(compared > 100);
        }

        [TestMethod]
        public void AdjacentFacesMatchesReference()
        {
            foreach (MeshBase<TriangulationVertex> mesh in new[]
            {
                DelaunayMesh(Phase7MeshCurveTests.RandomPoints(64, 100, 500000)),
                DelaunayMesh(Phase7MeshCurveTests.GridPoints(8, 8, 50, 0, 0)),
            })
            {
                foreach (IFace face in mesh.Faces)
                {
                    IFace[] expected = Phase7ReferenceImplementations.AdjacentFaces(mesh, face);
                    IFace[] actual = mesh.AdjacentFaces(face);
                    Assert.AreEqual(expected.Length, actual.Length);
                    for (int i = 0; i < expected.Length; i++)
                        Assert.AreSame(expected[i], actual[i]);
                }
            }

            Mesh2D quads = QuadGridMesh(5, 5);
            foreach (IFace face in quads.Faces)
            {
                IFace[] expected = Phase7ReferenceImplementations.AdjacentFaces(quads, face);
                IFace[] actual = quads.AdjacentFaces(face);
                Assert.AreEqual(expected.Length, actual.Length);
                for (int i = 0; i < expected.Length; i++)
                    Assert.AreSame(expected[i], actual[i]);
            }
        }

        #endregion
    }
}

