using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Equivalence tests for the Geometry.Core allocation and speed changes. Each test keeps a copy of the code that
    /// was replaced and requires the new code to agree with it, including at tolerance boundaries.
    /// </summary>
    [TestClass]
    public class Phase6CorePrimitivesTests
    {
        private static double Step(double value, int ulps)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);
            return BitConverter.Int64BitsToDouble(bits + ulps);
        }

        #region Vector2 and hashing

        [TestMethod]
        public void Vector2MagnitudeMatchesPowForm()
        {
            Random random = new(1);
            for (int i = 0; i < 5000; i++)
            {
                double scale = Math.Pow(10, random.Next(-6, 7));
                Vector2 v = new((random.NextDouble() - 0.5) * scale, (random.NextDouble() - 0.5) * scale);
                double old = Math.Sqrt(Math.Pow(v.X, 2) + Math.Pow(v.Y, 2));
                Assert.AreEqual(old, v.Magnitude, Math.Abs(old) * 1e-15);
                Assert.AreEqual((v.X * v.X) + (v.Y * v.Y), v.MagnitudeSquared);
            }

            Assert.AreEqual(5.0, new Vector2(3, 4).Magnitude);
            Assert.AreEqual(25.0, new Vector2(3, 4).MagnitudeSquared);
        }

        [TestMethod]
        public void QuantizedHashMatchesPowForm()
        {
            Random random = new(2);
            for (int i = 0; i < 20000; i++)
            {
                double x = (random.NextDouble() - 0.5) * Math.Pow(10, random.Next(-3, 7));
                double y = (random.NextDouble() - 0.5) * Math.Pow(10, random.Next(-3, 7));
                int oldX = (int)Math.Round(x * Math.Pow(10, Tolerance.SignificantDigits));
                int oldY = (int)Math.Round(y * Math.Pow(10, Tolerance.SignificantDigits));
                Assert.AreEqual(oldX, GeometryHashCode.QuantizedCoord(x));
                Assert.AreEqual(unchecked((oldX * 397) ^ oldY), GeometryHashCode.Point2D(x, y));
                Assert.AreEqual(GeometryHashCode.Point2D(x, y), new Vector2(x, y).GetHashCode());
            }
        }

        #endregion

        #region Circle

        /// <summary>
        /// Circle.GetRelation(Vector2) keeps its square root; pin the tolerance band so a later squared-distance
        /// rewrite has to match it exactly.
        /// </summary>
        [TestMethod]
        public void CircleGetRelationToleranceBandIsPinned()
        {
            Circle circle = new(new Vector2(0, 0), 10);
            Assert.AreEqual(ShapeRelation.Contained, circle.GetRelation(new Vector2(9.99, 0)));
            Assert.AreEqual(ShapeRelation.Touching, circle.GetRelation(new Vector2(9.9995, 0)));
            Assert.AreEqual(ShapeRelation.Touching, circle.GetRelation(new Vector2(10, 0)));
            Assert.AreEqual(ShapeRelation.Touching, circle.GetRelation(new Vector2(0, -10.0005)));
            Assert.AreEqual(ShapeRelation.None, circle.GetRelation(new Vector2(10.01, 0)));
            Assert.AreEqual(ShapeRelation.Touching, new Circle(new Vector2(5, 5), 0).GetRelation(new Vector2(5, 5)));
        }
        private static double[] OldRow(Vector2 p) => [p.X, p.Y, (p.X * p.X) + (p.Y * p.Y), 1];

        private static double OldDeterminant4x4(double[] r0, double[] r1, double[] r2, double[] r3)
        {
            static double Det3(double a, double b, double c, double d, double e, double f, double g, double h, double i) =>
                (a * ((e * i) - (f * h))) - (b * ((d * i) - (f * g))) + (c * ((d * h) - (e * g)));

            return (r0[0] * Det3(r1[1], r1[2], r1[3], r2[1], r2[2], r2[3], r3[1], r3[2], r3[3]))
                 - (r0[1] * Det3(r1[0], r1[2], r1[3], r2[0], r2[2], r2[3], r3[0], r3[2], r3[3]))
                 + (r0[2] * Det3(r1[0], r1[1], r1[3], r2[0], r2[1], r2[3], r3[0], r3[1], r3[3]))
                 - (r0[3] * Det3(r1[0], r1[1], r1[2], r2[0], r2[1], r2[2], r3[0], r3[1], r3[2]));
        }

        private static ShapeRelation OldInCircle(Vector2[] cp, Vector2 p)
        {
            double det = OldDeterminant4x4(OldRow(cp[0]), OldRow(cp[1]), OldRow(cp[2]), OldRow(p));
            if (det >= Tolerance.EpsilonSquared)
                return ShapeRelation.Contained;
            else if (det > -Tolerance.EpsilonSquared && det < Tolerance.EpsilonSquared)
                return ShapeRelation.Touching;
            else
                return ShapeRelation.None;
        }

        private static ShapeRelation OldInCircleBatch(Vector2[] cp, Vector2 p)
        {
            double det = OldDeterminant4x4(OldRow(cp[0]), OldRow(cp[1]), OldRow(cp[2]), OldRow(p));
            if (det < 0)
                return ShapeRelation.None;
            else if (det <= Tolerance.EpsilonSquared)
                return ShapeRelation.Touching;
            else
                return ShapeRelation.Contained;
        }

        [TestMethod]
        public void InCircleScalarLocalsMatchArrayDeterminant()
        {
            Random random = new(4);
            int touching = 0;

            for (int trial = 0; trial < 3000; trial++)
            {
                bool integer = trial % 2 == 0;
                double scale = integer ? 1 : Math.Pow(10, random.Next(-2, 6));
                double offset = trial % 3 == 0 ? 250000 : 0;
                Vector2 Pt() => integer
                    ? new Vector2(offset + random.Next(-8, 9), offset + random.Next(-8, 9))
                    : new Vector2(offset + ((random.NextDouble() - 0.5) * scale), offset + ((random.NextDouble() - 0.5) * scale));

                Vector2[] cp = [Pt(), Pt(), Pt()];
                Vector2[] probes = [Pt(), Pt(), Pt(), cp[0], cp[1], cp[2]];

                ShapeRelation[] batch = Circle.Contains(cp, probes);
                for (int i = 0; i < probes.Length; i++)
                {
                    ShapeRelation expected = OldInCircle(cp, probes[i]);
                    Assert.AreEqual(expected, Circle.Contains(cp, probes[i]));
                    Assert.AreEqual(expected, Circle.Contains(cp[0], cp[1], cp[2], probes[i]));
                    Assert.AreEqual(OldInCircleBatch(cp, probes[i]), batch[i]);
                    if (expected == ShapeRelation.Touching)
                        touching++;
                }
            }

            Assert.IsTrue(touching > 100, "Test data must include cocircular points");
        }

        [TestMethod]
        public void InCircleNullArgumentsStillThrowArgumentNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() => Circle.Contains((Vector2[])null, new Vector2(0, 0)));
            Assert.ThrowsException<ArgumentNullException>(() => Circle.Contains((Vector2[])null, new[] { new Vector2(0, 0) }));
            Assert.IsNull(Circle.Contains([new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1)], (IEnumerable<Vector2>)null));
        }

        #endregion

        #region PolygonArea and centroid

        private static double OldPolygonArea(Vector2[] points)
        {
            points = points.EnsureClosedRing();
            Vector2 avg = points.Average();
            points = points.Translate(-avg);

            double accumulator = 0;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 p0 = points[i];
                Vector2 p1 = points[i + 1];
                accumulator += ((p0.X * p1.Y) - (p1.X * p0.Y));
            }

            return accumulator / 2.0;
        }

        private static Vector2 OldCentroid(Vector2[] ring)
        {
            double accumulator_X = 0;
            double accumulator_Y = 0;

            ring = [.. ring.EnsureClosedRing()];
            Vector2 average = ring.Average();
            Vector2[] translated = ring.Translate(-average);

            for (int i = 0; i < translated.Length - 1; i++)
            {
                Vector2 p0 = translated[i];
                Vector2 p1 = translated[i + 1];
                double shared = ((p0.X * p1.Y) - (p1.X * p0.Y));
                accumulator_X += (p0.X + p1.X) * shared;
                accumulator_Y += (p0.Y + p1.Y) * shared;
            }

            double area = OldPolygonArea(translated);
            double scalar = area * 6;
            return new Vector2((accumulator_X / scalar) + average.X, (accumulator_Y / scalar) + average.Y);
        }

        private static Vector2[] StarRing(Random random, int n, double cx, double cy, double radius, bool close)
        {
            Vector2[] ring = new Vector2[close ? n + 1 : n];
            for (int i = 0; i < n; i++)
            {
                double angle = 2 * Math.PI * i / n;
                double r = radius * (1 + (0.3 * random.NextDouble()));
                ring[i] = new Vector2(cx + (r * Math.Cos(angle)), cy + (r * Math.Sin(angle)));
            }

            if (close)
                ring[n] = ring[0];
            return ring;
        }

        [TestMethod]
        public void PolygonAreaAndCentroidAreBitIdenticalToCopyingForm()
        {
            Random random = new(5);
            foreach (int n in new[] { 3, 4, 7, 50, 333 })
            {
                foreach (double cx in new[] { 0.0, 250000.0, -480000.5 })
                {
                    foreach (bool close in new[] { true, false })
                    {
                        Vector2[] ring = StarRing(random, n, cx, cx / 2, 10000, close);
                        Vector2[] before = [.. ring];

                        Assert.AreEqual(OldPolygonArea(ring), ring.PolygonArea(), $"area n={n} cx={cx} close={close}");
                        Assert.AreEqual(OldCentroid(ring), Polygon.CalculateCentroid(ring), $"centroid n={n} cx={cx} close={close}");
                        Assert.AreEqual(OldCentroid(ring).X, Polygon.CalculateCentroid(ring).X, 0);
                        Assert.AreEqual(OldCentroid(ring).Y, Polygon.CalculateCentroid(ring).Y, 0);
                        CollectionAssert.AreEqual(before, ring, "Input ring must not be modified");
                    }
                }
            }

            Assert.AreEqual(0.0, new[] { new Vector2(1, 1) }.PolygonArea());
            Assert.ThrowsException<InvalidOperationException>(() => Array.Empty<Vector2>().PolygonArea());
            Assert.ThrowsException<ArgumentNullException>(() => ((Vector2[])null).PolygonArea());
        }

        [TestMethod]
        public void PolygonCentroidPropertyUsesCurrentRing()
        {
            Vector2[] square = [new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(0, 0)];
            Polygon poly = new(square);
            Assert.AreEqual(new Vector2(5, 5), poly.Centroid);
            Assert.AreEqual(100.0, poly.Area, 1e-9);

            poly.AddVertex(new Vector2(10, 5));
            Assert.AreEqual(5, poly.ExteriorRing.Length - 1);
            Assert.AreEqual(new Vector2(5, 5), poly.Centroid);
            Assert.AreEqual(Polygon.CalculateCentroid(poly.ExteriorRing).X, poly.Centroid.X, 0);
        }

        #endregion

        #region LineSegment and winding test

        [TestMethod]
        public void IsWithinEpsilonOfMatchesDistanceToPoint()
        {
            Random random = new(6);
            double eps = Tolerance.Epsilon;
            int near = 0;

            for (int trial = 0; trial < 3000; trial++)
            {
                Vector2 a = new(random.Next(-5, 6) * 10.5, random.Next(-5, 6) * 7.25);
                Vector2 b = trial % 3 == 0 ? new Vector2(a.X, a.Y + random.Next(1, 20))
                          : trial % 3 == 1 ? new Vector2(a.X + random.Next(1, 20), a.Y)
                          : new Vector2(a.X + random.Next(1, 20) + random.NextDouble(), a.Y + random.Next(-20, 20) + 0.5);
                LineSegment seg = new(a, b);

                // Aim points at distance == epsilon from the segment, stepping by ulps across the boundary.
                double length = Vector2.Distance(a, b);
                Vector2 dir = new((b.X - a.X) / length, (b.Y - a.Y) / length);
                Vector2 normal = new(-dir.Y, dir.X);
                double along = random.NextDouble();
                Vector2 onLine = new(a.X + ((b.X - a.X) * along), a.Y + ((b.Y - a.Y) * along));

                for (int ulps = -30; ulps <= 30; ulps += 3)
                {
                    double d = Step(eps, ulps);
                    Vector2[] probes =
                    [
                        new(onLine.X + (normal.X * d), onLine.Y + (normal.Y * d)),
                        new(a.X - (dir.X * d), a.Y - (dir.Y * d)),
                        new(b.X + (dir.X * d), b.Y + (dir.Y * d)),
                        new(a.X - (dir.X * d * 0.6) + (normal.X * d * 0.8), a.Y - (dir.Y * d * 0.6) + (normal.Y * d * 0.8)),
                    ];

                    foreach (Vector2 p in probes)
                    {
                        bool expected = seg.DistanceToPoint(p) < eps;
                        Assert.AreEqual(expected, seg.IsWithinEpsilonOf(p), $"{seg} {p}");
                        if (expected)
                            near++;
                    }
                }

                Vector2 far = new(random.NextDouble() * 100 - 50, random.NextDouble() * 100 - 50);
                Assert.AreEqual(seg.DistanceToPoint(far) < eps, seg.IsWithinEpsilonOf(far));
            }

            Assert.IsTrue(near > 1000);
            LineSegment unit = new(new Vector2(0, 0), new Vector2(1, 1));
            Assert.AreEqual(unit.DistanceToPoint(new Vector2(double.NaN, 0)) < eps, unit.IsWithinEpsilonOf(new Vector2(double.NaN, 0)));
        }

        private readonly struct OldLeftData(int a, int b, LineSegment s, int? pLeft)
        {
            public readonly int A = a;
            public readonly int B = b;
            public readonly LineSegment S = s;
            public readonly int? PLeft = pLeft;
            public bool Touches => (A == 0) ^ (B == 0);
            public bool Crosses => !Touches && A != B;
            public bool OnLine => A == 0 && B == 0;
            public bool SameSide => A == B && A != 0;
        }

        /// <summary>The winding test as it was before it was made allocation-light, using DistanceToPoint and a parallel working list.</summary>
        private static ShapeRelation OldWinding(IReadOnlyList<LineSegment> polygonSegments, Line test_line)
        {
            Vector2 test_point = test_line.Origin;
            List<OldLeftData> isLeft = new(polygonSegments.Count);

            for (int i = 0; i < polygonSegments.Count; i++)
            {
                LineSegment s = polygonSegments[i];
                if (s.IsEndpoint(test_line.Origin))
                    return ShapeRelation.Touching;

                OldLeftData seg = new(test_line.IsLeft(s.A), test_line.IsLeft(s.B), s, null);
                if (seg.Touches)
                {
                    if (seg.S.DistanceToPoint(test_point) < Tolerance.Epsilon)
                        return ShapeRelation.Touching;
                }
                else if (seg.Crosses || seg.OnLine)
                {
                    if (seg.S.DistanceToPoint(test_point) < Tolerance.Epsilon)
                        return ShapeRelation.Touching;
                }

                if (seg.SameSide || seg.OnLine)
                    continue;

                isLeft.Add(seg);
            }

            if (isLeft.Count == 0)
                return ShapeRelation.None;

            List<LineSegment> working = [.. isLeft.Select(l => l.S)];

            for (int i = 0; i < isLeft.Count; i++)
            {
                int iNext = i + 1 >= isLeft.Count ? 0 : i + 1;
                OldLeftData seg = isLeft[i];
                if (seg.A != 0 && seg.B != 0)
                {
                    if (seg.S.DistanceToPoint(test_point) < Tolerance.Epsilon)
                        return ShapeRelation.Touching;

                    continue;
                }

                if (seg.B == 0)
                {
                    OldLeftData nextSeg = isLeft[iNext];
                    int nextSegIsLeft = nextSeg.A != 0 ? nextSeg.A : nextSeg.B;
                    Vector2 nextSegEndpoint = nextSeg.A != 0 ? nextSeg.S.A : nextSeg.S.B;

                    if (nextSegIsLeft == seg.A)
                    {
                        working.RemoveAt(Math.Max(i, iNext));
                        working.RemoveAt(Math.Min(i, iNext));
                        isLeft.RemoveAt(Math.Max(i, iNext));
                        isLeft.RemoveAt(Math.Min(i, iNext));
                        i -= i < iNext ? 1 : 2;
                    }
                    else
                    {
                        LineSegment virtualSeg = new(seg.S.A, nextSegEndpoint);
                        working.RemoveAt(i);
                        working.Insert(i, virtualSeg);
                        working.RemoveAt(iNext);
                        OldLeftData entry = new(seg.A, nextSegIsLeft, virtualSeg, seg.S.IsLeft(test_point));
                        isLeft.RemoveAt(i);
                        isLeft.Insert(i, entry);
                        isLeft.RemoveAt(iNext);
                    }
                }
            }

            int wind = 0;
            for (int i = 0; i < working.Count; i++)
            {
                OldLeftData data = isLeft[i];
                LineSegment polySeg = data.S;
                int aboveToBelow = data.S.A.Y.CompareTo(data.S.B.Y);
                int pIsLeft = data.PLeft.HasValue == false ? polySeg.IsLeft(test_point) : data.PLeft.Value;

                if (aboveToBelow == 0)
                    continue;
                else if (aboveToBelow > 0)
                {
                    if (pIsLeft >= 0)
                        wind += 1;
                }
                else if (pIsLeft <= 0)
                    wind -= 1;
            }

            return wind != 0 ? ShapeRelation.Contained : ShapeRelation.None;
        }

        private static ShapeRelation OldPolygonRelation(Polygon poly, Vector2 p)
        {
            if (!poly.BoundingBox.Covers(p))
                return ShapeRelation.None;

            Line testLine = new(p, Vector2.UnitX);
            ShapeRelation result = OldWinding(poly.ExteriorSegments, testLine);
            if (result == ShapeRelation.Contained)
            {
                foreach (Polygon inner in poly.InteriorPolygons)
                {
                    ShapeRelation innerResult = OldPolygonRelation(inner, p);
                    if (innerResult == ShapeRelation.Contained)
                        return ShapeRelation.None;
                    if (innerResult == ShapeRelation.Touching)
                        return innerResult;
                }
            }

            return result;
        }

        private static Polygon IntegerStar(Random random, int n, int radius, int cx, int cy)
        {
            while (true)
            {
                Vector2[] ring = new Vector2[n + 1];
                for (int i = 0; i < n; i++)
                {
                    double angle = 2 * Math.PI * i / n;
                    double r = radius * (0.55 + (0.45 * random.NextDouble()));
                    ring[i] = new Vector2(cx + Math.Round(r * Math.Cos(angle)), cy + Math.Round(r * Math.Sin(angle)));
                }

                ring[n] = ring[0];
                try
                {
                    return new Polygon(ring);
                }
                catch (ArgumentException)
                {
                }
            }
        }

        [TestMethod]
        public void PolygonGetRelationMatchesOriginalWindingTestOnLatticeQueries()
        {
            Random random = new(7);
            int touching = 0, contained = 0, none = 0;

            for (int shape = 0; shape < 40; shape++)
            {
                int n = new[] { 5, 8, 13, 24, 40 }[shape % 5];
                Polygon poly = IntegerStar(random, n, 12 + (shape % 4), shape % 3, -(shape % 2));
                if (shape % 4 == 3)
                {
                    int hx = shape % 3;
                    int hy = -(shape % 2);
                    try
                    {
                        poly.AddInteriorRing([new Vector2(hx - 1, hy - 1), new Vector2(hx + 1, hy - 1), new Vector2(hx + 1, hy + 1), new Vector2(hx - 1, hy + 1), new Vector2(hx - 1, hy - 1)]);
                    }
                    catch (ArgumentException)
                    {
                    }
                }

                Rectangle box = poly.BoundingBox;
                for (double y = box.Bottom - 1; y <= box.Top + 1; y += 0.5)
                {
                    for (double x = box.Left - 1; x <= box.Right + 1; x += 0.5)
                    {
                        Vector2 p = new(x, y);
                        ShapeRelation expected = OldPolygonRelation(poly, p);
                        ShapeRelation actual = poly.GetRelation(p);
                        Assert.AreEqual(expected, actual, $"shape {shape} point {p}");
                        switch (actual)
                        {
                            case ShapeRelation.Touching: touching++; break;
                            case ShapeRelation.Contained: contained++; break;
                            default: none++; break;
                        }
                    }
                }
            }

            Assert.IsTrue(touching > 200 && contained > 1000 && none > 1000, $"{touching} {contained} {none}");
        }

        [TestMethod]
        public void PolygonGetRelationMatchesOriginalWindingTestOnRandomQueries()
        {
            Random random = new(8);
            for (int shape = 0; shape < 10; shape++)
            {
                Polygon poly = new(StarRing(random, 20 + (shape * 37), 250000, 250000, 10000, true));
                for (int i = 0; i < 300; i++)
                {
                    Vector2 p = new(250000 + ((random.NextDouble() - 0.5) * 30000), 250000 + ((random.NextDouble() - 0.5) * 30000));
                    Assert.AreEqual(OldPolygonRelation(poly, p), poly.GetRelation(p));
                }

                foreach (Vector2 vertex in poly.ExteriorRing.Take(10))
                    Assert.AreEqual(ShapeRelation.Touching, poly.GetRelation(vertex));
            }
        }

        [TestMethod]
        public void PolygonGetRelationIsThreadSafe()
        {
            Random random = new(9);
            Polygon poly = new(StarRing(random, 200, 0, 0, 1000, true));
            Vector2[] points = Enumerable.Range(0, 2000).Select(_ => new Vector2((random.NextDouble() - 0.5) * 3000, (random.NextDouble() - 0.5) * 3000)).ToArray();
            ShapeRelation[] expected = points.Select(p => OldPolygonRelation(poly, p)).ToArray();
            ShapeRelation[] actual = new ShapeRelation[points.Length];

            System.Threading.Tasks.Parallel.For(0, points.Length, i => actual[i] = poly.GetRelation(points[i]));

            CollectionAssert.AreEqual(expected, actual);
        }

        #endregion

        #region AllSegments

        [TestMethod]
        public void AllSegmentsListsExteriorThenHolesAsSnapshot()
        {
            Polygon poly = new([new(0, 0), new(20, 0), new(20, 20), new(0, 20), new(0, 0)]);
            poly.AddInteriorRing([new Vector2(2, 2), new Vector2(5, 2), new Vector2(5, 5), new Vector2(2, 5), new Vector2(2, 2)]);
            poly.AddInteriorRing([new Vector2(10, 10), new Vector2(14, 10), new Vector2(12, 14), new Vector2(10, 10)]);

            List<LineSegment> expected = [.. poly.ExteriorSegments];
            foreach (Polygon inner in poly.InteriorPolygons)
                expected.AddRange(inner.AllSegments);

            List<LineSegment> all = poly.AllSegments;
            CollectionAssert.AreEqual(expected, all);
            Assert.AreEqual(4 + 4 + 3, all.Count);

            all.Clear();
            Assert.AreEqual(11, poly.AllSegments.Count, "AllSegments must be a fresh snapshot");

            poly.InteriorPolygons[1].AddVertex(new Vector2(12, 10));
            Assert.AreEqual(12, poly.AllSegments.Count, "A hole edited through its own reference must show up");
        }

        [TestMethod]
        public void CircleRelationToPolygonWithHoleUnchangedByInPlaceBoundaryScan()
        {
            Polygon poly = new([new(0, 0), new(20, 0), new(20, 20), new(0, 20), new(0, 0)]);
            poly.AddInteriorRing([new Vector2(8, 8), new Vector2(12, 8), new Vector2(12, 12), new Vector2(8, 12), new Vector2(8, 8)]);

            Assert.IsTrue(poly.CircleIntersectsBoundary(new Circle(new Vector2(10, 10), 3)), "Circle crosses only the hole boundary");
            Assert.IsFalse(poly.CircleIntersectsBoundary(new Circle(new Vector2(10, 10), 1)), "Circle inside the hole");
            Assert.IsTrue(poly.CircleIntersectsBoundary(new Circle(new Vector2(0, 10), 2)), "Circle crosses the exterior");
            Assert.AreEqual(ShapeRelation.None, poly.GetRelation(new Circle(new Vector2(10, 10), 1)));
            Assert.AreEqual(ShapeRelation.Contained, poly.GetRelation(new Circle(new Vector2(4, 4), 1)));
            Assert.AreEqual(ShapeRelation.Intersecting, poly.GetRelation((IShape2D)new Line(new Vector2(10, 10), new Vector2(1, 0))));
        }

        #endregion

        #region BoundingBoxIndex

        private static Rectangle RandomRect(Random random, double extent, double maxSize)
        {
            double x = random.NextDouble() * extent;
            double y = random.NextDouble() * extent;
            double w = random.NextDouble() < 0.1 ? 0 : random.NextDouble() * maxSize;
            double h = random.NextDouble() < 0.1 ? 0 : random.NextDouble() * maxSize;
            return new Rectangle(x, x + w, y, y + h);
        }

        private static BoundingBoxIndex<int> BuildIndex(Random random, int count, double extent, double maxSize, bool withOutliers)
        {
            BoundingBoxIndex<int> index = new();
            for (int i = 0; i < count; i++)
                index.Add(RandomRect(random, extent, maxSize), i);

            if (withOutliers)
            {
                index.Add(new Rectangle(-1e6, 1e6, -1e6, 1e6), count);
                index.Add(new Rectangle(double.NaN, 5, 0, 5), count + 1);
                index.Add(new Rectangle(extent, extent, extent, extent), count + 2);
                index.Add(new Rectangle(-extent * 3, -extent * 3 + 1, 7, 8), count + 3);
            }

            return index;
        }

        private static void AssertGridMatchesLinear(BoundingBoxIndex<int> index, Random random, double extent, int queries)
        {
            for (int q = 0; q < queries; q++)
            {
                double px = random.NextDouble() * extent;
                double py = random.NextDouble() * extent;
                Rectangle query = q % 5 == 0
                    ? new Rectangle(px, px, py, py)
                    : RandomRect(random, extent * 1.2, extent * (q % 7 == 0 ? 1.5 : 0.05));
                List<int> expected = index.IntersectsLinear(query);
                CollectionAssert.AreEqual(expected, index.Intersects(query), $"Intersects query {q}");
                CollectionAssert.AreEqual(expected, index.IntersectionGenerator(query).ToList(), $"Generator query {q}");
            }
        }

        [TestMethod]
        public void SpatialIndexReturnsSameHitsInSameOrderAsLinearScan()
        {
            Random random = new(10);
            foreach (int count in new[] { 65, 200, 3000 })
            {
                foreach (bool outliers in new[] { false, true })
                {
                    BoundingBoxIndex<int> index = BuildIndex(random, count, 1000, 30, outliers);
                    index.BuildSpatialIndex();
                    Assert.IsTrue(index.HasSpatialIndex);
                    AssertGridMatchesLinear(index, random, 1000, 600);
                }
            }
        }

        [TestMethod]
        public void SpatialIndexHandlesDegenerateLayouts()
        {
            Random random = new(11);

            BoundingBoxIndex<int> vertical = new();
            BoundingBoxIndex<int> horizontal = new();
            BoundingBoxIndex<int> identical = new();
            BoundingBoxIndex<int> hugeCoordinates = new();
            for (int i = 0; i < 300; i++)
            {
                vertical.Add(new Rectangle(5, 5, i, i + 0.5), i);
                horizontal.Add(new Rectangle(i, i + 0.5, 7, 7), i);
                identical.Add(new Rectangle(1, 2, 1, 2), i);
                hugeCoordinates.Add(new Rectangle(250000 + (i * 800), 250000 + (i * 800) + 1, 400000 + (i * 3), 400000 + (i * 3) + 1), i);
            }

            foreach (BoundingBoxIndex<int> index in new[] { vertical, horizontal, identical, hugeCoordinates })
            {
                index.BuildSpatialIndex();
                Assert.IsTrue(index.HasSpatialIndex);
                for (int q = 0; q < 400; q++)
                {
                    double x = q % 2 == 0 ? random.NextDouble() * 400 : 250000 + (random.NextDouble() * 240000);
                    double y = q % 2 == 0 ? random.NextDouble() * 400 : 400000 + (random.NextDouble() * 900);
                    Rectangle query = new(x, x + (random.NextDouble() * 3), y, y + (random.NextDouble() * 3));
                    CollectionAssert.AreEqual(index.IntersectsLinear(query), index.Intersects(query));
                }
            }
        }

        [TestMethod]
        public void SpatialIndexIsDroppedByEditsAndAnswersStayCorrect()
        {
            Random random = new(12);
            BoundingBoxIndex<int> index = BuildIndex(random, 400, 1000, 30, false);
            index.BuildSpatialIndex();
            Assert.IsTrue(index.HasSpatialIndex);

            index.Update(5, 100005);
            Assert.IsTrue(index.HasSpatialIndex, "Rename keeps bounds and position, so the grid stays valid");
            AssertGridMatchesLinear(index, random, 1000, 200);

            Assert.IsTrue(index.Delete(17, out _));
            Assert.IsFalse(index.HasSpatialIndex);
            index.Add(RandomRect(random, 1000, 30), 99999);
            Assert.IsFalse(index.HasSpatialIndex);

            for (int i = 0; i < BoundingBoxIndex<int>.QueriesBeforeSpatialIndex + 2; i++)
                CollectionAssert.AreEqual(index.IntersectsLinear(new Rectangle(0, 1000, 0, 1000)), index.Intersects(new Rectangle(0, 1000, 0, 1000)));

            AssertGridMatchesLinear(index, random, 1000, 400);
            Assert.IsTrue(index.HasSpatialIndex, "Grid is rebuilt after enough queries");
        }

        [TestMethod]
        public void SmallIndexStaysLinear()
        {
            Random random = new(13);
            BoundingBoxIndex<int> index = BuildIndex(random, BoundingBoxIndex<int>.MinEntriesForSpatialIndex, 100, 5, false);
            AssertGridMatchesLinear(index, random, 100, 100);
            Assert.IsFalse(index.HasSpatialIndex);
        }

        [TestMethod]
        public void GeneratorIsNotAffectedByEditsDuringEnumeration()
        {
            Random random = new(14);
            BoundingBoxIndex<int> index = BuildIndex(random, 300, 1000, 30, false);
            index.BuildSpatialIndex();
            Rectangle query = new(0, 300, 0, 300);
            List<int> expected = index.IntersectsLinear(query);
            Assert.IsTrue(expected.Count > 3);

            List<int> seen = [];
            foreach (int item in index.IntersectionGenerator(query))
            {
                seen.Add(item);
                if (seen.Count == 2)
                    index.Delete(expected[0], out _);
            }

            CollectionAssert.AreEqual(expected, seen);
        }

        private static List<LineSegment> BruteForceSegmentsIn(Polygon poly, Rectangle rect) =>
            [.. poly.AllSegments.Where(s => rect.Intersects(s))];

        private static string Key(LineSegment s) => $"{s.A.X:R},{s.A.Y:R},{s.B.X:R},{s.B.Y:R}";

        [TestMethod]
        public void PolygonSegmentQueriesMatchBruteForceThroughEdits()
        {
            Random random = new(15);
            Polygon poly = new(StarRing(random, 150, 250000, 250000, 10000, true));

            for (int round = 0; round < 6; round++)
            {
                for (int q = 0; q < 60; q++)
                {
                    Rectangle rect = new(new Vector2(250000 + ((random.NextDouble() - 0.5) * 26000), 250000 + ((random.NextDouble() - 0.5) * 26000)), 200 + (random.NextDouble() * 4000));
                    List<string> expected = BruteForceSegmentsIn(poly, rect).Select(Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
                    List<string> actual = poly.GetIntersectingSegments(rect).Select(Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
                    CollectionAssert.AreEqual(expected, actual, $"round {round} query {q}");
                }

                Vector2 mid = new(
                    (poly.ExteriorRing[round * 5].X + poly.ExteriorRing[(round * 5) + 1].X) / 2,
                    (poly.ExteriorRing[round * 5].Y + poly.ExteriorRing[(round * 5) + 1].Y) / 2);
                poly.InsertVertex(mid, new PolygonIndex(0, (round * 5) + 1, poly.ExteriorRing.Length - 1));
            }

            Assert.IsTrue(poly.IsValid());
        }

        private static bool BruteForceSelfIntersects(Vector2[] ring)
        {
            LineSegment[] segs = LineSegment.SegmentsFromPoints(ring);
            for (int i = 0; i < segs.Length; i++)
            {
                for (int j = i + 1; j < segs.Length; j++)
                {
                    bool adjacent = j == i + 1 || (i == 0 && j == segs.Length - 1);
                    if (adjacent)
                    {
                        continue;
                    }

                    if (segs[i].Intersects(segs[j], false))
                        return true;
                }
            }

            return false;
        }

        [TestMethod]
        public void PolygonIsValidAgreesWithBruteForceOnLargeRings()
        {
            Random random = new(16);
            Polygon valid = new(StarRing(random, 800, 250000, 250000, 10000, true));
            Assert.IsFalse(BruteForceSelfIntersects(valid.ExteriorRing));
            Assert.IsTrue(valid.IsValid());
            Assert.IsTrue(valid.IsValid(), "A second call runs with the grid built");

            Vector2[] crossing = StarRing(random, 300, 250000, 250000, 10000, true);
            (crossing[100], crossing[200]) = (crossing[200], crossing[100]);
            crossing[300] = crossing[0];
            Assert.IsTrue(BruteForceSelfIntersects(crossing));
            Polygon bad = new(crossing);
            Assert.IsFalse(bad.IsValid());
            Assert.IsFalse(bad.IsValid());
        }

        #endregion
    }

}
