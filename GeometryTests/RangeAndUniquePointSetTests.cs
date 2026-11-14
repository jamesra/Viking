using System;
using System.Collections.Generic;
using System.Linq;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoRange = Geometry.Range;

namespace GeometryTests
{
    [TestClass]
    public class RangeTests
    {
        [TestMethod]
        public void ConstructorRejectsMinAboveMax()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new GeoRange(2, 1));
        }

        [TestMethod]
        public void ContainsIsClosedInterval()
        {
            GeoRange r = new(1, 3);

            Assert.IsTrue(r.Contains(1));
            Assert.IsTrue(r.Contains(3));
            Assert.IsFalse(r.Contains(0.999));
            Assert.IsFalse(r.Contains(3.001));
        }

        [TestMethod]
        public void NormalizeMapsMinMaxToZeroOne_AndClipLimitsOutsideValues()
        {
            GeoRange r = new(10, 20);

            Assert.AreEqual(0.5, r.Normalize(15), Tolerance.Epsilon);
            Assert.AreEqual(2.0, r.Normalize(30), Tolerance.Epsilon);
            Assert.AreEqual(1.0, r.Normalize(30, clip: true), Tolerance.Epsilon);
            Assert.AreEqual(0.0, r.Normalize(0, clip: true), Tolerance.Epsilon);
        }

        [TestMethod]
        public void NormalizeOfZeroSpanIsZero()
        {
            Assert.AreEqual(0.0, new GeoRange(5, 5).Normalize(7));
        }

        [TestMethod]
        public void InterpolateInvertsNormalize()
        {
            GeoRange r = new(-4, 6);

            Assert.AreEqual(1.0, r.Interpolate(0.5), Tolerance.Epsilon);
            Assert.AreEqual(0.25, r.Normalize(r.Interpolate(0.25)), Tolerance.Epsilon);
        }

        [TestMethod]
        public void ClipClampsToBounds()
        {
            GeoRange r = new(1, 2);

            Assert.AreEqual(1.0, r.Clip(-5));
            Assert.AreEqual(2.0, r.Clip(9));
            Assert.AreEqual(1.5, r.Clip(1.5));
        }
    }

    [TestClass]
    public class UniquePointSetTests
    {
        [TestMethod]
        public void TryAddRejectsDuplicate_AddThrows()
        {
            UniquePointSet set = new();

            Assert.IsTrue(set.TryAdd(new Vector2(1, 1)));
            Assert.IsFalse(set.TryAdd(new Vector2(1, 1)));
            Assert.ThrowsExactly<ArgumentException>(() => set.Add(new Vector2(1, 1)));
        }

        [TestMethod]
        public void TryFindNearestReturnsClosestPoint_FalseWhenEmpty()
        {
            UniquePointSet set = new();
            Assert.IsFalse(set.TryFindNearest(new Vector2(0, 0), out _, out double emptyDistance));
            Assert.IsTrue(double.IsPositiveInfinity(emptyDistance));

            set.Add(new Vector2(10, 0));
            set.Add(new Vector2(2, 0));

            Assert.IsTrue(set.TryFindNearest(new Vector2(0, 0), out Vector2 nearest, out double distance));
            Assert.AreEqual(new Vector2(2, 0), nearest);
            Assert.AreEqual(2.0, distance, Tolerance.Epsilon);
        }

        [TestMethod]
        public void TryRemoveDropsPointOnce()
        {
            UniquePointSet set = new();
            set.Add(new Vector2(3, 3));

            Assert.IsTrue(set.TryRemove(new Vector2(3, 3), out Vector2 removed));
            Assert.AreEqual(new Vector2(3, 3), removed);
            Assert.IsFalse(set.TryRemove(new Vector2(3, 3), out _));
        }

        [TestMethod]
        public void PointMapKeepsValueOfNearestPoint_AndRejectsDuplicates()
        {
            UniquePointMap<string> map = new();
            map.Add(new Vector2(0, 0), "origin");
            map.Add(new Vector2(5, 5), "far");

            Assert.IsTrue(map.Contains(new Vector2(5, 5)));
            Assert.ThrowsExactly<ArgumentException>(() => map.Add(new Vector2(0, 0), "again"));
            Assert.IsTrue(map.TryFindNearest(new Vector2(4, 4), out Vector2 nearest, out string value, out _));
            Assert.AreEqual(new Vector2(5, 5), nearest);
            Assert.AreEqual("far", value);
        }

        /// <summary>
        /// The map buckets points in grid cells; it must answer exactly as a linear scan does: equality within
        /// Tolerance.Epsilon also across a cell edge, nearest by distance, and ties to the point added first.
        /// </summary>
        [TestMethod]
        public void UniquePointMapAnswersAsALinearScan()
        {
            Random random = new(1234);
            for (int trial = 0; trial < 40; trial++)
            {
                double spread = trial % 4 == 0 ? 3 : trial % 4 == 1 ? 50 : trial % 4 == 2 ? 2000 : 400000;
                UniquePointMap<int> map = new();
                List<Vector2> added = [];
                for (int i = 0; i < 300; i++)
                {
                    // Integers and half-integers put points on cell edges and make equal distances common.
                    Vector2 p = i % 3 == 0
                        ? new Vector2(Math.Round(random.NextDouble() * spread * 2) / 2, Math.Round(random.NextDouble() * spread * 2) / 2)
                        : new Vector2(random.NextDouble() * spread, random.NextDouble() * spread);
                    bool linearContains = added.Any(a => a.Equals(p));
                    Assert.AreEqual(linearContains, map.Contains(p));
                    if (!linearContains)
                    {
                        map.Add(p, added.Count);
                        added.Add(p);
                    }

                    // Within epsilon of a point, on the other side of a cell edge.
                    Vector2 near = new(Math.Floor(p.X) - 0.0004, p.Y + 0.0004);
                    Assert.AreEqual(added.Any(a => a.Equals(near)), map.Contains(near));
                }

                for (int q = 0; q < 200; q++)
                {
                    double reach = q % 5 == 0 ? spread * 20 : spread;
                    Vector2 query = q % 7 == 0
                        ? new Vector2(Math.Round(random.NextDouble() * spread) + 0.5, Math.Round(random.NextDouble() * spread))
                        : new Vector2(random.NextDouble() * reach - reach / 4, random.NextDouble() * reach - reach / 4);

                    int expected = 0;
                    double expectedDistance = double.PositiveInfinity;
                    for (int i = 0; i < added.Count; i++)
                    {
                        double d = Vector2.Distance(query, added[i]);
                        if (d < expectedDistance)
                        {
                            expectedDistance = d;
                            expected = i;
                        }
                    }

                    Assert.IsTrue(map.TryFindNearest(query, out Vector2 nearest, out int value, out double distance));
                    Assert.AreEqual(expected, value, $"trial {trial} query {query}");
                    Assert.AreEqual(added[expected], nearest);
                    Assert.AreEqual(expectedDistance, distance);
                }
            }
        }

        [TestMethod]
        public void UniquePointMapStaysFastForAFittedOutline()
        {
            // A fitted segmentation outline: tens of thousands of points around a ring 2000 px across.
            UniquePointMap<int> map = new();
            const int count = 40000;
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                double a = 2 * Math.PI * i / count;
                Vector2 p = new(90000 + 1000 * Math.Cos(a), 54000 + 1000 * Math.Sin(a));
                if (!map.Contains(p))
                    map.Add(p, i);
            }

            for (int i = 0; i < 2000; i++)
                Assert.IsTrue(map.TryFindNearest(new Vector2(90000 + 990 * Math.Cos(i), 54000 + 990 * Math.Sin(i)), out _, out _, out _));

            Assert.IsTrue(clock.ElapsedMilliseconds < 2000, $"took {clock.ElapsedMilliseconds} ms; a linear scan takes minutes");
        }

        /// <summary>
        /// Polygon intersection adds, removes and searches points; the set must answer as the list it replaced:
        /// the first equal point in list order is the one removed, and nearest ties go to the earliest still present.
        /// </summary>
        [TestMethod]
        public void UniquePointSetWithRemovalsAnswersAsAList()
        {
            Random random = new(99);
            for (int trial = 0; trial < 20; trial++)
            {
                double spread = trial % 2 == 0 ? 4 : 600;
                UniquePointSet set = new();
                List<Vector2> list = [];
                for (int step = 0; step < 600; step++)
                {
                    Vector2 p = step % 2 == 0
                        ? new Vector2(Math.Round(random.NextDouble() * spread * 2) / 2, Math.Round(random.NextDouble() * spread * 2) / 2)
                        : new Vector2(random.NextDouble() * spread, random.NextDouble() * spread);

                    switch (random.Next(4))
                    {
                        case 0:
                        case 1:
                            bool listAdded = !list.Any(a => a.Equals(p));
                            if (listAdded)
                                list.Add(p);
                            Assert.AreEqual(listAdded, set.TryAdd(p));
                            break;
                        case 2:
                            // Remove a point present, or one just off it, so near-equal removal is exercised.
                            Vector2 target = list.Count > 0 && random.Next(2) == 0
                                ? list[random.Next(list.Count)] + new Vector2(0.0004, -0.0004)
                                : p;
                            int at = list.FindIndex(a => a.Equals(target));
                            Assert.AreEqual(at >= 0, set.TryRemove(target, out Vector2 removed));
                            if (at >= 0)
                            {
                                Assert.AreEqual(list[at], removed);
                                list.RemoveAt(at);
                            }
                            break;
                        default:
                            bool found = set.TryFindNearest(p, out Vector2 nearest, out double distance);
                            Assert.AreEqual(list.Count > 0, found);
                            if (!found)
                                break;
                            Vector2 expected = list[0];
                            double expectedDistance = Vector2.Distance(p, expected);
                            foreach (Vector2 candidate in list)
                            {
                                double d = Vector2.Distance(p, candidate);
                                if (d < expectedDistance)
                                {
                                    expectedDistance = d;
                                    expected = candidate;
                                }
                            }
                            Assert.AreEqual(expected, nearest, $"trial {trial} step {step}");
                            Assert.AreEqual(expectedDistance, distance);
                            break;
                    }

                    Assert.AreEqual(list.Count, set.Count);
                }
            }
        }
    }
}
