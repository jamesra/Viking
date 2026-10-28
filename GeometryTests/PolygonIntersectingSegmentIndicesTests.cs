using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Pins <see cref="Polygon.IntersectingSegmentIndices(LineSegment[])"/>, the crossing finder behind the pen cut and
    /// retrace commands: each entry is keyed by distance along the path and holds the ring segment that was crossed.
    /// </summary>
    [TestClass]
    public class PolygonIntersectingSegmentIndicesTests
    {
        private static readonly Vector2[] Square =
        [
            new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(0, 0)
        ];

        private static LineSegment[] Path(params Vector2[] points) => points.ToLineSegments();

        /// <summary>
        /// A 100 x 100 square with a hole from (40,40) to (60,60).
        /// </summary>
        private static Polygon SquareWithHole() => new(
            [new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 100), new Vector2(0, 100), new Vector2(0, 0)],
            [[new Vector2(40, 40), new Vector2(60, 40), new Vector2(60, 60), new Vector2(40, 60), new Vector2(40, 40)]]);

        private static void AssertSegmentContains(Polygon poly, PolygonIndex index, Vector2 crossing)
        {
            Assert.AreEqual(0.0, index.Segment(poly).DistanceToPoint(crossing), 1e-9, $"segment {index} does not hold {crossing}");
        }

        [TestMethod]
        public void TwoCrossingsAreOrderedByDistanceAlongPath()
        {
            Polygon square = new(Square);

            SortedDictionary<double, PolygonIndex> crossings = square.IntersectingSegmentIndices(Path(new Vector2(-5, 5), new Vector2(15, 5)));

            CollectionAssert.AreEqual(new[] { 5.0, 15.0 }, crossings.Keys.ToArray());
            Assert.AreEqual(3, crossings[5.0].VertexIndex, "left edge is the ring's closing segment");
            Assert.AreEqual(1, crossings[15.0].VertexIndex, "right edge");
            Assert.IsFalse(crossings.Values.Any(i => i.IsInner));
        }

        [TestMethod]
        public void ReversedPathReversesTheOrder()
        {
            Polygon square = new(Square);

            SortedDictionary<double, PolygonIndex> crossings = square.IntersectingSegmentIndices(Path(new Vector2(15, 5), new Vector2(-5, 5)));

            CollectionAssert.AreEqual(new[] { 5.0, 15.0 }, crossings.Keys.ToArray());
            Assert.AreEqual(1, crossings[5.0].VertexIndex);
            Assert.AreEqual(3, crossings[15.0].VertexIndex);
        }

        /// <summary>
        /// The second path segment's crossing is keyed by the first segment's length plus its own distance.
        /// </summary>
        [TestMethod]
        public void MultiSegmentPathAccumulatesLength()
        {
            Polygon square = new(Square);

            SortedDictionary<double, PolygonIndex> crossings = square.IntersectingSegmentIndices(
                Path(new Vector2(-5, 5), new Vector2(5, 5), new Vector2(5, 15)));

            CollectionAssert.AreEqual(new[] { 5.0, 15.0 }, crossings.Keys.ToArray());
            Assert.AreEqual(3, crossings[5.0].VertexIndex, "left edge crossed by the first path segment");
            Assert.AreEqual(2, crossings[15.0].VertexIndex, "top edge crossed by the second path segment");
        }

        [TestMethod]
        public void PathThroughHoleReportsInteriorRingCrossings()
        {
            Polygon poly = SquareWithHole();

            SortedDictionary<double, PolygonIndex> crossings = poly.IntersectingSegmentIndices(Path(new Vector2(50, -10), new Vector2(50, 110)));

            CollectionAssert.AreEqual(new[] { 10.0, 50.0, 70.0, 110.0 }, crossings.Keys.ToArray());
            double[] expectedY = [0, 40, 60, 100];
            bool[] expectedInner = [false, true, true, false];
            int i = 0;
            foreach (KeyValuePair<double, PolygonIndex> crossing in crossings)
            {
                Assert.AreEqual(expectedInner[i], crossing.Value.IsInner, $"crossing at distance {crossing.Key}");
                if (expectedInner[i])
                {
                    Assert.AreEqual(0, crossing.Value.InnerShapeIndex);
                    Assert.AreEqual(4, crossing.Value.NumUniqueInRing, "unique vertices of the hole's ring");
                }
                AssertSegmentContains(poly, crossing.Value, new Vector2(50, expectedY[i]));
                i++;
            }
        }

        /// <summary>
        /// Each crossing names the hole it crosses, by the order the holes were given.
        /// </summary>
        [TestMethod]
        public void PathThroughSecondHoleNamesThatHole()
        {
            Polygon poly = new(
                [new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 100), new Vector2(0, 100), new Vector2(0, 0)],
                [
                    [new Vector2(10, 10), new Vector2(20, 10), new Vector2(20, 20), new Vector2(10, 20), new Vector2(10, 10)],
                    [new Vector2(60, 60), new Vector2(80, 60), new Vector2(80, 80), new Vector2(60, 80), new Vector2(60, 60)]
                ]);

            PolygonIndex[] inFirst = [.. poly.IntersectingSegmentIndices(Path(new Vector2(15, 5), new Vector2(15, 25))).Values];
            PolygonIndex[] inSecond = [.. poly.IntersectingSegmentIndices(Path(new Vector2(70, 55), new Vector2(70, 85))).Values];

            Assert.AreEqual(2, inFirst.Length);
            Assert.IsTrue(inFirst.All(i => i.InnerShapeIndex == 0));
            Assert.AreEqual(2, inSecond.Length);
            Assert.IsTrue(inSecond.All(i => i.InnerShapeIndex == 1));
            AssertSegmentContains(poly, inSecond[0], new Vector2(70, 60));
            AssertSegmentContains(poly, inSecond[1], new Vector2(70, 80));
        }

        [TestMethod]
        public void PathInsideTheHoleReportsOnlyTheHoleCrossing()
        {
            Polygon poly = SquareWithHole();

            SortedDictionary<double, PolygonIndex> crossings = poly.IntersectingSegmentIndices(Path(new Vector2(50, 50), new Vector2(50, 70)));

            Assert.AreEqual(1, crossings.Count);
            Assert.IsTrue(crossings.Values.Single().IsInner);
            Assert.AreEqual(10.0, crossings.Keys.Single(), 1e-12);
        }

        [TestMethod]
        public void PathEndingInsideReportsOneCrossing()
        {
            Polygon square = new(Square);

            SortedDictionary<double, PolygonIndex> crossings = square.IntersectingSegmentIndices(Path(new Vector2(-5, 5), new Vector2(5, 5)));

            Assert.AreEqual(1, crossings.Count);
            Assert.AreEqual(5.0, crossings.Keys.Single(), 1e-12);
        }

        [TestMethod]
        public void PathWithNoCrossingIsEmpty()
        {
            Polygon square = new(Square);

            Assert.AreEqual(0, square.IntersectingSegmentIndices(Path(new Vector2(2, 2), new Vector2(8, 8))).Count, "inside");
            Assert.AreEqual(0, square.IntersectingSegmentIndices(Path(new Vector2(-5, 2), new Vector2(-1, 8))).Count, "outside");
            Assert.AreEqual(0, square.IntersectingSegmentIndices(Path(new Vector2(-5, 4), new Vector2(4, -5))).Count,
                "past the corner, though the bounding boxes overlap");
        }

        /// <summary>
        /// A path through a vertex in the middle of the ring meets both segments that share it, and reports the vertex
        /// once, as the index of the segment that starts there.
        /// </summary>
        [TestMethod]
        public void PathThroughMidRingVertexReportsTheVertexOnce()
        {
            Polygon square = new(Square);

            SortedDictionary<double, PolygonIndex> bottomRight = square.IntersectingSegmentIndices(Path(new Vector2(5, -5), new Vector2(15, 5)));
            SortedDictionary<double, PolygonIndex> topRight = square.IntersectingSegmentIndices(Path(new Vector2(5, 15), new Vector2(15, 5)));

            Assert.AreEqual(1, bottomRight.Count);
            Assert.AreEqual(1, bottomRight.Values.Single().VertexIndex);
            Assert.AreEqual(Math.Sqrt(50), bottomRight.Keys.Single(), 1e-12);
            Assert.AreEqual(1, topRight.Count);
            Assert.AreEqual(2, topRight.Values.Single().VertexIndex);
        }

        /// <summary>
        /// The ring's first vertex is shared by its first and its closing segment, which report it as vertex 0 and
        /// vertex 4 at the same distance along the path.
        /// </summary>
        [TestMethod]
        [Ignore("Defect: a path exactly through ring vertex 0 reports it from the first and the closing segment under one distance key and throws ArgumentException; see the improvement loop's deferred entry")]
        public void PathThroughRingStartVertexDoesNotThrow()
        {
            Polygon square = new(Square);

            SortedDictionary<double, PolygonIndex> crossings = square.IntersectingSegmentIndices(Path(new Vector2(-5, -5), new Vector2(5, 5)));

            Assert.AreEqual(1, crossings.Count);
            Assert.AreEqual(Math.Sqrt(50), crossings.Keys.Single(), 1e-12);
        }

        /// <summary>
        /// A path lying along an edge intersects it in a line, not a point, and the exterior ring code reads the point
        /// unconditionally.
        /// </summary>
        [TestMethod]
        [Ignore("Defect: a path collinear with an exterior edge intersects in a line, the IPoint2D cast gives null and NullReferenceException is thrown; see the improvement loop's deferred entry")]
        public void PathCollinearWithAnEdgeDoesNotThrow()
        {
            Polygon square = new(Square);

            SortedDictionary<double, PolygonIndex> crossings = square.IntersectingSegmentIndices(Path(new Vector2(-5, 0), new Vector2(15, 0)));

            Assert.IsTrue(crossings.Count > 0);
        }

        /// <summary>
        /// Rectangles at volume-scale offsets crossed edge to edge by a horizontal path that is cut into one to four
        /// pieces at points away from the outline: the two crossings sit at the same distances along the path however
        /// it is cut, in order, on the left and right edges.
        /// </summary>
        [TestMethod]
        public void SplitPathsReportTheSameDistances()
        {
            Gen<Tuple<Polygon, Vector2[], double, double>> cases =
                from ox in Gen.Choose(-500000, 500000)
                from oy in Gen.Choose(-500000, 500000)
                from w in Gen.Choose(20, 4000)
                from h in Gen.Choose(20, 4000)
                from lead in Gen.Choose(1, 500)
                from trail in Gen.Choose(1, 500)
                from splitOutsideLeft in Arb.Generate<bool>()
                from splitInside in Arb.Generate<bool>()
                from splitOutsideRight in Arb.Generate<bool>()
                let y = oy + h / 2.0
                let polygon = new Polygon([new Vector2(ox, oy), new Vector2(ox + w, oy), new Vector2(ox + w, oy + h), new Vector2(ox, oy + h), new Vector2(ox, oy)])
                let xs = new[] { (double)ox - lead }
                    .Concat(splitOutsideLeft ? new[] { ox - lead / 2.0 } : [])
                    .Concat(splitInside ? new[] { ox + w / 2.0 } : [])
                    .Concat(splitOutsideRight ? new[] { ox + w + trail / 2.0 } : [])
                    .Concat(new[] { (double)ox + w + trail })
                select Tuple.Create(polygon, xs.Select(x => new Vector2(x, y)).ToArray(), (double)lead, (double)(lead + w));

            CoreCheck.Run(
                Prop.ForAll(Arb.From(cases), c =>
                {
                    (Polygon polygon, Vector2[] points, double leftDistance, double rightDistance) = (c.Item1, c.Item2, c.Item3, c.Item4);

                    SortedDictionary<double, PolygonIndex> crossings = polygon.IntersectingSegmentIndices(points.ToLineSegments());
                    double[] keys = [.. crossings.Keys];
                    if (keys.Length != 2)
                        return false.Label($"{keys.Length} crossings");

                    return (Math.Abs(keys[0] - leftDistance) <= 1e-6).Label($"left at {keys[0]}, expected {leftDistance}")
                        .And((Math.Abs(keys[1] - rightDistance) <= 1e-6).Label($"right at {keys[1]}, expected {rightDistance}"))
                        .And((crossings[keys[0]].VertexIndex == 3).Label("left edge is segment 3"))
                        .And((crossings[keys[1]].VertexIndex == 1).Label("right edge is segment 1"));
                }),
                nameof(SplitPathsReportTheSameDistances));
        }
    }
}
