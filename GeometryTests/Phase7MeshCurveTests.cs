using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GeometryTests
{
    /// <summary>
    /// Phase 7 equivalence tests for the meshing, curve-fitting and Delaunay changes. Each test compares the current code
    /// with <see cref="Phase7ReferenceImplementations"/>, a verbatim copy of the code that was replaced, and requires the
    /// same arrays in the same order with bit-identical doubles. Whole-pipeline golden fingerprints were recorded from the
    /// code before the change.
    /// </summary>
    [TestClass]
    public class Phase7MeshCurveTests
    {
        #region Helpers

        internal static ulong Fnv(IEnumerable<long> values)
        {
            ulong hash = 14695981039346656037UL;
            foreach (long v in values)
            {
                for (int shift = 0; shift < 64; shift += 8)
                {
                    hash ^= (byte)(v >> shift);
                    hash *= 1099511628211UL;
                }
            }

            return hash;
        }

        internal static string FingerprintOf(IEnumerable<long> values) => Fnv(values).ToString("x16");

        internal static IEnumerable<long> Bits(Vector2 v)
        {
            yield return BitConverter.DoubleToInt64Bits(v.X);
            yield return BitConverter.DoubleToInt64Bits(v.Y);
        }

        internal static IEnumerable<long> Bits(IEnumerable<Vector2> points)
        {
            yield return points.Count();
            foreach (Vector2 p in points)
                foreach (long b in Bits(p))
                    yield return b;
        }

        internal static void AssertBitIdentical(IReadOnlyList<Vector2> expected, IReadOnlyList<Vector2> actual, string context)
        {
            Assert.AreEqual(expected.Count, actual.Count, context + ": length");
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected[i].X), BitConverter.DoubleToInt64Bits(actual[i].X), $"{context}: X[{i}] {expected[i].X:R} vs {actual[i].X:R}");
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected[i].Y), BitConverter.DoubleToInt64Bits(actual[i].Y), $"{context}: Y[{i}] {expected[i].Y:R} vs {actual[i].Y:R}");
            }
        }

        internal static void AssertBitIdentical(IReadOnlyList<double> expected, IReadOnlyList<double> actual, string context)
        {
            Assert.AreEqual(expected.Count, actual.Count, context + ": length");
            for (int i = 0; i < expected.Count; i++)
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected[i]), BitConverter.DoubleToInt64Bits(actual[i]), $"{context}: [{i}] {expected[i]:R} vs {actual[i]:R}");
        }

        /// <summary>Points that are at least 0.01 apart, so Delaunay2D's duplicate check cannot fire by accident.</summary>
        internal static Vector2[] RandomPoints(int seed, int count, double scale, double origin = 0)
        {
            Random random = new(seed);
            List<Vector2> points = new(count);
            while (points.Count < count)
            {
                Vector2 candidate = new(origin + (random.NextDouble() * scale), origin + (random.NextDouble() * scale));
                bool tooClose = false;
                foreach (Vector2 existing in points)
                {
                    if (Vector2.DistanceSquared(existing, candidate) < 0.0001)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                    points.Add(candidate);
            }

            return [.. points];
        }

        internal static Vector2[] GridPoints(int columns, int rows, double spacing, double originX, double originY)
        {
            Vector2[] points = new Vector2[columns * rows];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                    points[(y * columns) + x] = new Vector2(originX + (x * spacing), originY + (y * spacing));
            return points;
        }

        internal static Vector2[] CirclePoints(int count, double radius, double cx, double cy)
        {
            Vector2[] points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                double angle = 2 * Math.PI * i / count;
                points[i] = new Vector2(cx + (radius * Math.Cos(angle)), cy + (radius * Math.Sin(angle)));
            }

            return points;
        }

        internal static string RunOrException(Func<int[]> run)
        {
            try
            {
                return "ok:" + string.Join(",", run());
            }
            catch (Exception ex)
            {
                return "exception:" + ex.GetType().Name;
            }
        }

        #endregion

        #region Edge and face keys

        [TestMethod]
        public void EdgeKeyOrdersItsEndpoints()
        {
            Random random = new(1);
            List<(int, int)> pairs = [(0, 0), (5, 5), (0, 1), (1, 0), (int.MaxValue, 0), (0, int.MaxValue), (-3, 7), (7, -3), (int.MinValue, int.MaxValue)];
            for (int i = 0; i < 5000; i++)
                pairs.Add((random.Next(-1000, 500000), random.Next(-1000, 500000)));

            foreach ((int a, int b) in pairs)
            {
                EdgeKey key = new(a, b);
                Assert.AreEqual(Math.Min(a, b), key.A);
                Assert.AreEqual(Math.Max(a, b), key.B);
                Assert.AreEqual(key, new EdgeKey(b, a));
                Assert.AreEqual(key, new EdgeKey((long)a, (long)b));
                Assert.AreEqual(key.GetHashCode(), new EdgeKey(b, a).GetHashCode());
                Assert.AreEqual($"{key.A}-{key.B}", key.ToString());
            }
        }

        [TestMethod]
        public void EdgeKeyAndEndpointPairAndEdgeShareOneHash()
        {
            Random random = new(2);
            for (int i = 0; i < 5000; i++)
            {
                int a = random.Next(0, 1000000);
                int b = random.Next(0, 1000000);
                if (a == b)
                    continue;

                int hash = new EdgeKey(a, b).GetHashCode();
                Assert.AreEqual(hash, new EdgeKey(b, a).GetHashCode());
                Assert.AreEqual(hash, new EndpointPair(a, b).GetHashCode());
                Assert.AreEqual(hash, new EndpointPair(b, a).GetHashCode());
                Assert.AreEqual(hash, new Edge(a, b).GetHashCode());
                Assert.IsTrue(hash >= 0, "Hashes stay non-negative like the A*B hash they replace");
            }
        }

        /// <summary>
        /// The old hash was A*B, so every edge touching vertex 0 collided (a hub vertex, or a boundary vertex shared by
        /// many faces, gives thousands of identical hashes). The new hash must spread the same edges.
        /// </summary>
        [TestMethod]
        public void EdgeKeyHashSpreadsGridAndHubEdges()
        {
            const int columns = 150;
            HashSet<EdgeKey> keys = [];
            for (int y = 0; y < columns; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int v = (y * columns) + x;
                    if (x + 1 < columns)
                        keys.Add(new EdgeKey(v, v + 1));
                    if (y + 1 < columns)
                        keys.Add(new EdgeKey(v, v + columns));
                }
            }

            for (int i = 1; i <= 5000; i++)
                keys.Add(new EdgeKey(0, i));

            int oldDistinct = keys.Select(k => Phase7ReferenceImplementations.OldEdgeHash(k.A, k.B)).Distinct().Count();
            int newDistinct = keys.Select(k => k.GetHashCode()).Distinct().Count();
            Assert.IsTrue(oldDistinct < keys.Count - 4000, $"the old hash collapsed the hub edges: {oldDistinct} distinct of {keys.Count}");
            Assert.IsTrue(newDistinct > keys.Count * 0.999, $"new hash produced {newDistinct} distinct values for {keys.Count} edges");
        }

        [TestMethod]
        public void TriangleFaceHashIgnoresVertexOrder()
        {
            Random random = new(3);
            for (int i = 0; i < 2000; i++)
            {
                int a = random.Next(0, 100000);
                int b = random.Next(100000, 200000);
                int c = random.Next(200000, 300000);
                int[][] permutations = [[a, b, c], [a, c, b], [b, a, c], [b, c, a], [c, a, b], [c, b, a]];
                int hash = new Face(a, b, c).GetHashCode();
                foreach (int[] p in permutations)
                {
                    Face face = new(p[0], p[1], p[2]);
                    Assert.AreEqual(hash, face.GetHashCode());
                    Assert.IsTrue(face.Equals(new Face(a, b, c)));
                }
            }
        }

        /// <summary>
        /// Quads that are Equal but listed from a different corner never shared a hash. A hash that fixed this would change
        /// which entries a HashSet or Dictionary of faces finds, so the quad formula is pinned to the old one.
        /// </summary>
        [TestMethod]
        public void QuadFaceHashKeepsTheOldFormula()
        {
            Random random = new(4);
            for (int i = 0; i < 2000; i++)
            {
                int[] v = [random.Next(0, 100000), random.Next(100001, 200000), random.Next(200001, 300000), random.Next(300001, 400000)];
                Face quad = new(v[0], v[1], v[2], v[3]);
                Assert.AreEqual(Phase7ReferenceImplementations.OldFaceHash(quad), quad.GetHashCode());
            }
        }

        /// <summary>
        /// Enumeration order of Dictionary and HashSet follows insertion and removal history, not the hash. Replaying one
        /// operation sequence against a dictionary that uses the old A*B hash must give the same key order as MeshEdgeMap,
        /// which is why changing the hash cannot change any output that enumerates an edge or face collection.
        /// </summary>
        [TestMethod]
        public void MeshEdgeMapEnumerationOrderDoesNotDependOnTheHash()
        {
            Random random = new(5);
            MeshEdgeMap map = new();
            Dictionary<(int, int), IEdge> oldHashed = new(new OldHashPairComparer());
            List<(int, int)> live = [];

            for (int step = 0; step < 6000; step++)
            {
                if (live.Count > 0 && random.NextDouble() < 0.35)
                {
                    int index = random.Next(live.Count);
                    (int a, int b) = live[index];
                    live.RemoveAt(index);
                    Assert.IsTrue(map.Remove(new EdgeKey(a, b)));
                    Assert.IsTrue(oldHashed.Remove((a, b)));
                }
                else
                {
                    int a = random.Next(0, 300);
                    int b = random.Next(0, 300);
                    if (a == b)
                        continue;

                    (int lo, int hi) = a < b ? (a, b) : (b, a);
                    if (oldHashed.ContainsKey((lo, hi)))
                        continue;

                    Edge edge = new(lo, hi);
                    map.Add(new EdgeKey(lo, hi), edge);
                    oldHashed.Add((lo, hi), edge);
                    live.Add((lo, hi));
                }
            }

            (int, int)[] expected = [.. oldHashed.Keys];
            (int, int)[] actual = [.. map.Keys.Select(k => (k.A, k.B))];
            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void FaceHashSetEnumerationOrderDoesNotDependOnTheHash()
        {
            Random random = new(6);
            HashSet<IFace> current = [];
            HashSet<IFace> oldHashed = new(new OldHashFaceComparer());
            List<IFace> live = [];

            for (int step = 0; step < 6000; step++)
            {
                if (live.Count > 0 && random.NextDouble() < 0.35)
                {
                    int index = random.Next(live.Count);
                    IFace face = live[index];
                    live.RemoveAt(index);
                    Assert.IsTrue(current.Remove(face));
                    Assert.IsTrue(oldHashed.Remove(face));
                }
                else
                {
                    int a = random.Next(0, 40);
                    int b = random.Next(40, 80);
                    int c = random.Next(80, 120);
                    IFace face = random.Next(3) == 0 ? new Face(a, b, c, random.Next(120, 160)) : new Face(a, b, c);
                    if (oldHashed.Contains(face))
                    {
                        Assert.IsTrue(current.Contains(face));
                        continue;
                    }

                    Assert.IsTrue(current.Add(face));
                    Assert.IsTrue(oldHashed.Add(face));
                    live.Add(face);
                }
            }

            CollectionAssert.AreEqual(oldHashed.ToArray(), current.ToArray());
        }

        private sealed class OldHashPairComparer : IEqualityComparer<(int, int)>
        {
            public bool Equals((int, int) x, (int, int) y) => x == y;

            public int GetHashCode((int, int) pair) => Phase7ReferenceImplementations.OldEdgeHash(pair.Item1, pair.Item2);
        }

        private sealed class OldHashFaceComparer : IEqualityComparer<IFace>
        {
            public bool Equals(IFace x, IFace y) => x.Equals(y);

            public int GetHashCode(IFace face) => Phase7ReferenceImplementations.OldFaceHash(face);
        }

        #endregion

        #region Delaunay2D

        private static void AssertSameTriangulation(Vector2[] points, string name)
        {
            string expected = RunOrException(() => Phase7ReferenceImplementations.DelaunayTriangulate(points, Phase7ReferenceImplementations.DelaunayCorners(points)));
            string actual = RunOrException(() => Delaunay2D.Triangulate(points));
            Assert.AreEqual(expected, actual, name);
        }

        public static IEnumerable<object[]> DelaunayInputs()
        {
            yield return new object[] { "random 1000 at 500000", RandomPoints(11, 1000, 500000) };
            yield return new object[] { "random 400 offset 400000", RandomPoints(12, 400, 100000, 400000) };
            yield return new object[] { "random 150 tiny", RandomPoints(13, 150, 4) };
            yield return new object[] { "random 60 tiny negative", RandomPoints(14, 60, 3, -2) };
            yield return new object[] { "grid 40x40", GridPoints(40, 40, 128, 100000, 200000) };
            yield return new object[] { "grid 12x12 tiny spacing", GridPoints(12, 12, 0.01, 0, 0) };
            yield return new object[] { "grid 30x30 huge spacing", GridPoints(30, 30, 16000, 0, 0) };
            yield return new object[] { "grid 25x4 stretched", GridPoints(25, 4, 1000, 500, 500) };
            yield return new object[] { "grid 1x30 column", GridPoints(1, 30, 50, 10, 10) };
            yield return new object[] { "grid 30x1 row", GridPoints(30, 1, 50, 10, 10) };
            yield return new object[] { "circle 64 cocircular", CirclePoints(64, 250000, 250000, 250000) };
            yield return new object[] { "circle 200 plus center", CirclePoints(200, 3000, 10000, 10000).Append(new Vector2(10000, 10000)).ToArray() };
            yield return new object[] { "collinear diagonal 40", Enumerable.Range(0, 40).Select(i => new Vector2(i * 1000, i * 1000)).ToArray() };
            yield return new object[] { "collinear vertical 40", Enumerable.Range(0, 40).Select(i => new Vector2(7, i * 25)).ToArray() };
            yield return new object[] { "collinear slanted 40", Enumerable.Range(0, 40).Select(i => new Vector2(i * 3, (i * 5) + 1)).ToArray() };
            yield return new object[] { "near collinear 60", Enumerable.Range(0, 60).Select(i => new Vector2(i * 100, (i % 2) * 0.5)).ToArray() };
            yield return new object[] { "lattice with column offset", GridPoints(20, 20, 100, 0, 0).Select((p, i) => i % 7 == 0 ? new Vector2(p.X + 0.37, p.Y) : p).ToArray() };
            yield return new object[] { "two clusters far apart", RandomPoints(15, 80, 10).Concat(RandomPoints(16, 80, 10, 499990)).ToArray() };
            yield return new object[] { "three points", new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(5, 8) } };
            yield return new object[] { "four points square", new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10) } };
            yield return new object[] { "two points", new[] { new Vector2(0, 0), new Vector2(10, 0) } };
            yield return new object[] { "duplicates", new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 0), new Vector2(5, 8), new Vector2(7, 3) } };
            yield return new object[] { "near duplicates", new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10.0004, 0), new Vector2(5, 8) } };
        }

        public static string DelaunayInputName(System.Reflection.MethodInfo method, object[] data) => (string)data[0];

        [TestMethod]
        [DynamicData(nameof(DelaunayInputs), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(DelaunayInputName))]
        public void DelaunayTriangulateMatchesReference(string name, Vector2[] points) => AssertSameTriangulation(points, name);

        /// <summary>
        /// Large cavities use a hashed edge cancel and small ones compare pairs. Forcing every cavity through each path (limit 0
        /// is all hashed, int.MaxValue is all pairwise, 6 mixes them) must give the reference triangles in the reference order.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(DelaunayInputs), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(DelaunayInputName))]
        public void DelaunayEdgeCancelPathsMatchReference(string name, Vector2[] points)
        {
            Vector2[] corners = Phase7ReferenceImplementations.DelaunayCorners(points);
            string expected = RunOrException(() => Phase7ReferenceImplementations.DelaunayTriangulate(points, corners));
            foreach (int limit in new[] { 0, 6, int.MaxValue })
                Assert.AreEqual(expected, RunOrException(() => Delaunay2D.TriangulateCore(points, corners, limit)), $"{name}, pairwise limit {limit}");
        }

        [TestMethod]
        public void DelaunayEdgeCancelPathsMatchReferenceOnManyRandomInputs()
        {
            for (int seed = 200; seed < 240; seed++)
            {
                Random random = new(seed);
                int count = random.Next(3, 160);
                double scale = new[] { 5.0, 300.0, 40000.0, 500000.0 }[seed % 4];
                Vector2[] points = seed % 3 == 0
                    ? GridPoints(random.Next(2, 14), random.Next(2, 14), scale / 20, scale, scale)
                    : RandomPoints(seed, count, scale);
                Vector2[] corners = Phase7ReferenceImplementations.DelaunayCorners(points);
                string expected = RunOrException(() => Phase7ReferenceImplementations.DelaunayTriangulate(points, corners));
                foreach (int limit in new[] { 0, 3, 12, int.MaxValue })
                    Assert.AreEqual(expected, RunOrException(() => Delaunay2D.TriangulateCore(points, corners, limit)), $"seed {seed}, pairwise limit {limit}");
            }
        }

        [TestMethod]
        public void DelaunayTriangulateMatchesReferenceOnManyRandomInputs()
        {
            for (int seed = 100; seed < 160; seed++)
            {
                Random random = new(seed);
                int count = random.Next(3, 140);
                double scale = new[] { 5.0, 300.0, 40000.0, 500000.0 }[seed % 4];
                AssertSameTriangulation(RandomPoints(seed, count, scale), $"seed {seed} count {count} scale {scale}");
            }
        }

        [TestMethod]
        public void DelaunayTriangulateWithBoundsAndLeavingBordersMatchReference()
        {
            Vector2[] points = RandomPoints(21, 300, 200000, 100000);
            Rectangle bounds = new(new Vector2(100000, 100000), new Vector2(300000, 300000));
            double margin = bounds.Width;
            Vector2[] boundingPoints = [new(bounds.Left - margin, bounds.Bottom - bounds.Height),
                                        new(bounds.Right + margin, bounds.Bottom - bounds.Height),
                                        new(bounds.Left - margin, bounds.Top + bounds.Height),
                                        new(bounds.Right + margin, bounds.Top + bounds.Height)];
            string expected = RunOrException(() => Phase7ReferenceImplementations.DelaunayTriangulate(points, boundingPoints));
            Assert.AreEqual(expected, RunOrException(() => Delaunay2D.Triangulate(points, bounds)));
            Assert.AreEqual(expected, RunOrException(() => Delaunay2D.TriangulateLeavingBorders(points, bounds)));
        }

        #endregion
    }
}

