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
    /// Pins <see cref="Polygon.NearestSegment"/> and <see cref="Polygon.AddVertex"/> against a brute-force search over
    /// every exterior and interior ring segment, including each ring's closing segment. Regression: the search once
    /// seeded its best distance with the distance to vertex 0 and skipped segment 0, so segment 0 was never measured.
    /// </summary>
    [TestClass]
    public class PolygonNearestSegmentTests
    {
        /// <summary>
        /// A thin profile, 100 wide and 1 tall; segment 0 is the bottom edge from (0,0) to (100,0).
        /// </summary>
        private static readonly Vector2[] ThinRectangle =
        [
            new(0, 0), new(100, 0), new(100, 1), new(0, 1), new(0, 0)
        ];

        private static IEnumerable<LineSegment> AllSegments(Polygon poly) =>
            poly.ExteriorSegments.Concat(poly.InteriorPolygons.SelectMany(inner => inner.ExteriorSegments));

        private static double BruteForceNearestDistance(Polygon poly, Vector2 p) =>
            AllSegments(poly).Min(s => s.DistanceToPoint(p));

        [TestMethod]
        public void NearestSegmentThinRectangleSegmentZero()
        {
            Polygon thin = new(ThinRectangle);
            Vector2 click = new(50, 0.1);

            double distance = thin.NearestSegment(click, out PolygonIndex index);

            Assert.AreEqual(0, index.VertexIndex);
            Assert.IsFalse(index.IsInner);
            Assert.AreEqual(0.1, distance, 1e-12);
        }

        /// <summary>
        /// Regression: no other segment is nearer than vertex 0, so the search once returned segment 0 with the
        /// distance to vertex 0 (sqrt 26) instead of to segment 0.
        /// </summary>
        [TestMethod]
        public void NearestSegmentReturnsSegmentZeroDistance()
        {
            Polygon square = new([new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10), new Vector2(0, 0)]);

            double distance = square.NearestSegment(new Vector2(5, -1), out PolygonIndex index);

            Assert.AreEqual(0, index.VertexIndex);
            Assert.AreEqual(1.0, distance, 1e-12);
        }

        [TestMethod]
        public void NearestSegmentThinRectangleAtVolumeScale()
        {
            Vector2 offset = new(250000, 375000);
            Polygon thin = new Polygon(ThinRectangle).Translate(offset);

            double distance = thin.NearestSegment(new Vector2(50, 0.1) + offset, out PolygonIndex index);

            Assert.AreEqual(0, index.VertexIndex);
            Assert.AreEqual(0.1, distance, 1e-9);
        }

        [TestMethod]
        public void NearestSegmentFindsClosingSegment()
        {
            Polygon thin = new(ThinRectangle);

            double distance = thin.NearestSegment(new Vector2(-0.25, 0.5), out PolygonIndex index);

            Assert.AreEqual(3, index.VertexIndex);
            Assert.AreEqual(0.25, distance, 1e-12);
        }

        [TestMethod]
        public void NearestSegmentFindsHoleSegment()
        {
            Polygon poly = new(
                [new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 100), new Vector2(0, 100), new Vector2(0, 0)],
                [[new Vector2(6, 6), new Vector2(10, 6), new Vector2(10, 10), new Vector2(6, 10), new Vector2(6, 6)]]);
            Vector2 query = new(8, 5);

            double distance = poly.NearestSegment(query, out PolygonIndex index);

            Assert.IsTrue(index.IsInner);
            LineSegment seg = index.Segment(poly);
            Assert.AreEqual(0.0, seg.DistanceToPoint(new Vector2(8, 6)), 1e-12);
            Assert.AreEqual(1.0, distance, 1e-12);
        }

        [TestMethod]
        public void AddVertexOnSegmentZeroLandsOnSegmentZero()
        {
            Polygon thin = new(ThinRectangle);
            Vector2 click = new(50, 0.1);

            thin.AddVertex(click);

            Assert.AreEqual(6, thin.ExteriorRing.Length);
            Assert.AreEqual(new Vector2(0, 0), thin.ExteriorRing[0]);
            Assert.AreEqual(click, thin.ExteriorRing[1]);
            Assert.AreEqual(new Vector2(100, 0), thin.ExteriorRing[2]);
        }

        [TestMethod]
        public void AddVertexAtExistingVertexIsNoOp()
        {
            Polygon thin = new(ThinRectangle);

            thin.AddVertex(new Vector2(100, 1));

            Assert.AreEqual(5, thin.ExteriorRing.Length);
        }

        /// <summary>
        /// Rectangles, convex and holed polygons scaled up to 100x and moved to volume-scale offsets, queried around
        /// their bounding box: the returned segment and distance agree with a brute-force search over every ring
        /// segment, including the closing segments.
        /// </summary>
        [TestMethod]
        public void NearestSegmentMatchesBruteForce()
        {
            Gen<Tuple<Polygon, Vector2>> cases =
                from shape in Gen.OneOf(CoreArbitraries.SimplePolygon(), CoreArbitraries.ConvexPolygon(), CoreArbitraries.HoledPolygon())
                from scale in Gen.Elements(1.0, 10.0, 100.0)
                from ox in Gen.Choose(-500000, 500000)
                from oy in Gen.Choose(-500000, 500000)
                from fx in Gen.Choose(-75, 75)
                from fy in Gen.Choose(-75, 75)
                let poly = shape.Scale(scale, Vector2.Zero).Translate(new Vector2(ox, oy))
                let box = poly.BoundingBox
                select Tuple.Create(poly, box.Center + new Vector2(box.Width * fx / 100.0, box.Height * fy / 100.0));

            CoreCheck.Run(
                Prop.ForAll(Arb.From(cases), c =>
                {
                    (Polygon poly, Vector2 query) = (c.Item1, c.Item2);
                    double expected = BruteForceNearestDistance(poly, query);
                    double tolerance = 1e-9 * (1 + expected);

                    double distance = poly.NearestSegment(query, out PolygonIndex index);
                    double segmentDistance = index.Segment(poly).DistanceToPoint(query);

                    return (Math.Abs(segmentDistance - expected) <= tolerance)
                        .Label($"segment {index} at {segmentDistance}, brute force {expected}")
                        .And((Math.Abs(distance - expected) <= tolerance).Label($"returned {distance}, brute force {expected}"))
                        .And((index.NumUniqueInRing == index.Polygon(poly).ExteriorRing.Length - 1).Label($"ring size {index.NumUniqueInRing}"));
                }),
                nameof(NearestSegmentMatchesBruteForce));
        }
    }
}
