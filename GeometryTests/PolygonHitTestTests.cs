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
    /// Pins the pen and UI hit tests <see cref="Polygon.PointIntersectsAnyPolygonVertex"/> (control-point pick) and
    /// <see cref="Polygon.PointIntersectsAnyPolygonSegment"/> (outline pick) used by <c>LocationPolygonView</c> and
    /// <c>AddRemovePolygonVertexCommand</c>: which polygon is reported (the exterior or the hole), the radius and
    /// half-line-width boundaries, and agreement with a brute-force search over every ring at volume-scale offsets.
    /// </summary>
    [TestClass]
    public class PolygonHitTestTests
    {
        /// <summary>A 100 by 100 square, and the 20 by 20 hole centred on it used by <see cref="SquareWithHole"/>.</summary>
        private static readonly Vector2[] Outer = [new(0, 0), new(100, 0), new(100, 100), new(0, 100), new(0, 0)];
        private static readonly Vector2[] Hole = [new(40, 40), new(60, 40), new(60, 60), new(40, 60), new(40, 40)];

        private static Polygon Square() => new(Outer);

        private static Polygon SquareWithHole() => new(Outer, [Hole]);

        /// <summary>
        /// Triangle whose closing segment, (50,100) to (0,0), is the last ring segment.
        /// </summary>
        private static Polygon Triangle() => new([new Vector2(0, 0), new Vector2(100, 0), new Vector2(50, 100), new Vector2(0, 0)]);

        private static readonly Vector2[] Offsets = [Vector2.Zero, new(250000, 375000), new(-480000, 120000)];

        #region Vertex

        [TestMethod]
        public void VertexInsideRadiusHitsExterior()
        {
            Polygon square = Square();

            Assert.IsTrue(square.PointIntersectsAnyPolygonVertex(new Vector2(103, 102), 6, out Polygon hit));
            Assert.AreSame(square, hit);
        }

        [TestMethod]
        public void VertexAtRadiusBoundaryHitsAndJustOutsideMisses()
        {
            Polygon square = Square();

            // 3-4-5 triangle: exactly the radius from corner (100,100). The circle test is closed.
            Assert.IsTrue(square.PointIntersectsAnyPolygonVertex(new Vector2(103, 104), 5, out Polygon hit));
            Assert.AreSame(square, hit);

            Assert.IsFalse(square.PointIntersectsAnyPolygonVertex(new Vector2(103, 104.01), 5, out Polygon miss));
            Assert.IsNull(miss);
        }

        [TestMethod]
        public void VertexHitsEveryExteriorCornerIncludingTheRepeatedClosingVertex()
        {
            Polygon square = Square();

            foreach (Vector2 corner in Outer)
            {
                Assert.IsTrue(square.PointIntersectsAnyPolygonVertex(corner + new Vector2(-2, 2), 6, out Polygon hit), $"corner {corner}");
                Assert.AreSame(square, hit);
            }
        }

        [TestMethod]
        public void VertexTestIgnoresEdgesAwayFromVertices()
        {
            // Mid-edge point: on the outline, but 50 from every vertex.
            Assert.IsFalse(Square().PointIntersectsAnyPolygonVertex(new Vector2(50, 0), 6, out Polygon hit));
            Assert.IsNull(hit);
        }

        [TestMethod]
        public void VertexOfHoleReportsTheHole()
        {
            Polygon holed = SquareWithHole();

            Assert.IsTrue(holed.PointIntersectsAnyPolygonVertex(new Vector2(43, 43), 6, out Polygon hit));

            Assert.AreSame(holed.InteriorPolygons[0], hit);
            Assert.AreNotSame(holed, hit);
        }

        [TestMethod]
        public void VertexOfExteriorWithHolePresentReportsTheExterior()
        {
            Polygon holed = SquareWithHole();

            Assert.IsTrue(holed.PointIntersectsAnyPolygonVertex(new Vector2(2, 3), 6, out Polygon hit));

            Assert.AreSame(holed, hit);
        }

        [TestMethod]
        public void VertexNearNothingInsideTheBoxMisses()
        {
            // Centre of the holed square: inside the padded box, 28 from the nearest hole vertex.
            Assert.IsFalse(SquareWithHole().PointIntersectsAnyPolygonVertex(new Vector2(50, 50), 6, out Polygon hit));
            Assert.IsNull(hit);
        }

        [TestMethod]
        public void VertexOutsidePaddedBoundingBoxMissesWithNull()
        {
            Polygon holed = SquareWithHole();

            foreach (Vector2 far in new Vector2[] { new(1000, 1000), new(-50, 50), new(50, 150), new(50, -300) })
            {
                Assert.IsFalse(holed.PointIntersectsAnyPolygonVertex(far, 6, out Polygon hit), $"{far}");
                Assert.IsNull(hit, $"{far}");
            }
        }

        #endregion

        #region Segment

        [TestMethod]
        public void SegmentWithinHalfLineWidthHitsAndOutsideMisses()
        {
            Polygon square = Square();

            // lineWidth 12: the pick band is 6 either side of the bottom edge.
            Assert.IsTrue(square.PointIntersectsAnyPolygonSegment(new Vector2(50, 5.9), 12, out Polygon hit));
            Assert.AreSame(square, hit);
            Assert.IsTrue(square.PointIntersectsAnyPolygonSegment(new Vector2(50, -5.9), 12, out hit));
            Assert.AreSame(square, hit);

            Assert.IsFalse(square.PointIntersectsAnyPolygonSegment(new Vector2(50, 6.1), 12, out Polygon miss));
            Assert.IsNull(miss);
            Assert.IsFalse(square.PointIntersectsAnyPolygonSegment(new Vector2(50, -6.1), 12, out miss));
            Assert.IsNull(miss);
        }

        /// <summary>
        /// The segment band is open (<c>distance &lt; lineWidth / 2</c>) while the vertex disk is closed; a point exactly
        /// half a line width from the edge is not a hit. The measure-zero difference does not matter to the callers; this
        /// pins the current behaviour so a change to the comparison or to the halving is noticed.
        /// </summary>
        [TestMethod]
        public void SegmentExactlyHalfLineWidthAwayIsNotAHit()
        {
            Assert.IsFalse(Square().PointIntersectsAnyPolygonSegment(new Vector2(50, 6), 12, out Polygon hit));
            Assert.IsNull(hit);
        }

        [TestMethod]
        public void SegmentTestUsesHalfTheLineWidthNotTheFullWidth()
        {
            // 8 from the edge: outside half of 12, inside half of 18.
            Assert.IsFalse(Square().PointIntersectsAnyPolygonSegment(new Vector2(50, 8), 12, out _));
            Assert.IsTrue(Square().PointIntersectsAnyPolygonSegment(new Vector2(50, 8), 18, out _));
        }

        [TestMethod]
        public void SegmentHitsEachSideOfTheSquare()
        {
            Polygon square = Square();

            foreach (Vector2 p in new Vector2[] { new(50, 2), new(98, 50), new(50, 98), new(2, 50), new(50, -2), new(102, 50), new(50, 102), new(-2, 50) })
            {
                Assert.IsTrue(square.PointIntersectsAnyPolygonSegment(p, 12, out Polygon hit), $"{p}");
                Assert.AreSame(square, hit, $"{p}");
            }
        }

        [TestMethod]
        public void SegmentHitsTheClosingSegment()
        {
            Polygon triangle = Triangle();
            Vector2 midpoint = new(25, 50);
            Vector2 unitNormal = new Vector2(100, -50) / Math.Sqrt(12500);

            // Both sides of the closing segment (50,100)-(0,0), 3 away: hit. 7 away on the outside: miss.
            Assert.IsTrue(triangle.PointIntersectsAnyPolygonSegment(midpoint + (unitNormal * 3), 12, out Polygon hit));
            Assert.AreSame(triangle, hit);
            Assert.IsTrue(triangle.PointIntersectsAnyPolygonSegment(midpoint - (unitNormal * 3), 12, out hit));
            Assert.AreSame(triangle, hit);
            Assert.IsFalse(triangle.PointIntersectsAnyPolygonSegment(midpoint - (unitNormal * 7), 12, out hit));
            Assert.IsNull(hit);
        }

        [TestMethod]
        public void SegmentOfHoleReportsTheHole()
        {
            Polygon holed = SquareWithHole();

            // 2 below the hole's bottom edge (y = 40), 38 above the exterior's.
            Assert.IsTrue(holed.PointIntersectsAnyPolygonSegment(new Vector2(50, 38), 12, out Polygon hit));

            Assert.AreSame(holed.InteriorPolygons[0], hit);
        }

        [TestMethod]
        public void SegmentOfExteriorWithHolePresentReportsTheExterior()
        {
            Polygon holed = SquareWithHole();

            Assert.IsTrue(holed.PointIntersectsAnyPolygonSegment(new Vector2(50, 2), 12, out Polygon hit));

            Assert.AreSame(holed, hit);
        }

        [TestMethod]
        public void SegmentFarFromEveryRingInsideTheBoxMisses()
        {
            // 10 from the hole edge and 30 from the exterior: outside the 6 band of both.
            Assert.IsFalse(SquareWithHole().PointIntersectsAnyPolygonSegment(new Vector2(50, 30), 12, out Polygon hit));
            Assert.IsNull(hit);
        }

        [TestMethod]
        public void SegmentOutsidePaddedBoundingBoxMissesWithNull()
        {
            Polygon holed = SquareWithHole();

            foreach (Vector2 far in new Vector2[] { new(1000, 1000), new(-50, 50), new(50, 150), new(50, -300) })
            {
                Assert.IsFalse(holed.PointIntersectsAnyPolygonSegment(far, 12, out Polygon hit), $"{far}");
                Assert.IsNull(hit, $"{far}");
            }
        }

        #endregion

        #region Volume scale

        [TestMethod]
        public void HitsAndMissesAreUnchangedAtVolumeScaleOffsets()
        {
            foreach (Vector2 offset in Offsets)
            {
                Polygon holed = SquareWithHole().Translate(offset);

                Assert.IsTrue(holed.PointIntersectsAnyPolygonVertex(new Vector2(103, 104) + offset, 5, out Polygon vertexHit), $"{offset}");
                Assert.AreSame(holed, vertexHit);
                Assert.IsTrue(holed.PointIntersectsAnyPolygonVertex(new Vector2(43, 43) + offset, 6, out vertexHit));
                Assert.AreSame(holed.InteriorPolygons[0], vertexHit);
                Assert.IsFalse(holed.PointIntersectsAnyPolygonVertex(new Vector2(103, 104.01) + offset, 5, out vertexHit));
                Assert.IsNull(vertexHit);

                Assert.IsTrue(holed.PointIntersectsAnyPolygonSegment(new Vector2(50, 38) + offset, 12, out Polygon segmentHit), $"{offset}");
                Assert.AreSame(holed.InteriorPolygons[0], segmentHit);
                Assert.IsTrue(holed.PointIntersectsAnyPolygonSegment(new Vector2(50, 5.9) + offset, 12, out segmentHit));
                Assert.AreSame(holed, segmentHit);
                Assert.IsFalse(holed.PointIntersectsAnyPolygonSegment(new Vector2(50, 6.1) + offset, 12, out segmentHit));
                Assert.IsNull(segmentHit);
            }
        }

        private static IEnumerable<Polygon> Rings(Polygon poly) => new[] { poly }.Concat(poly.InteriorPolygons);

        /// <summary>
        /// Queries scattered within twice the pick radius of a random vertex of convex and holed polygons (1x to 100x,
        /// moved to volume-scale offsets) give the same hit and miss as a brute-force scan of every ring, and a hit
        /// names a ring that really has a vertex inside the disk. Queries within 0.01 of the boundary are skipped (the
        /// disk test is closed with a 0.001 tolerance, the band test is open).
        /// </summary>
        [TestMethod]
        public void VertexHitMatchesBruteForce()
        {
            CoreCheck.Run(
                Prop.ForAll(Arb.From(HitCases()), c =>
                {
                    (Polygon poly, Vector2 query, double radius) = (c.Item1, c.Item2, c.Item3);
                    double nearest = poly.ExteriorRing.Concat(poly.InteriorRings.SelectMany(r => r)).Min(v => Vector2.Distance(v, query));
                    if (Math.Abs(nearest - radius) < 0.01)
                        return true.ToProperty();

                    bool hit = poly.PointIntersectsAnyPolygonVertex(query, radius, out Polygon ring);

                    Property result = (hit == nearest < radius).Label($"hit {hit}, nearest vertex {nearest}, radius {radius}");
                    return hit
                        ? result.And((ring is not null && ring.ExteriorRing.Any(v => Vector2.Distance(v, query) < radius + 0.01)).Label("reported ring has no vertex in the disk"))
                        : result.And((ring is null).Label("miss must report null"));
                }),
                nameof(VertexHitMatchesBruteForce));
        }

        /// <summary>
        /// As <see cref="VertexHitMatchesBruteForce"/> for the outline: the hit agrees with the distance to the
        /// nearest segment of any ring being under half the line width, and the reported ring has such a segment.
        /// </summary>
        [TestMethod]
        public void SegmentHitMatchesBruteForce()
        {
            CoreCheck.Run(
                Prop.ForAll(Arb.From(HitCases()), c =>
                {
                    (Polygon poly, Vector2 query, double halfWidth) = (c.Item1, c.Item2, c.Item3);
                    double nearest = Rings(poly).SelectMany(r => r.ExteriorSegments).Min(s => s.DistanceToPoint(query));
                    if (Math.Abs(nearest - halfWidth) < 0.01)
                        return true.ToProperty();

                    bool hit = poly.PointIntersectsAnyPolygonSegment(query, halfWidth * 2, out Polygon ring);

                    Property result = (hit == nearest < halfWidth).Label($"hit {hit}, nearest segment {nearest}, half width {halfWidth}");
                    return hit
                        ? result.And((ring is not null && ring.ExteriorSegments.Any(s => s.DistanceToPoint(query) < halfWidth)).Label("reported ring has no segment in the band"))
                        : result.And((ring is null).Label("miss must report null"));
                }),
                nameof(SegmentHitMatchesBruteForce));
        }

        /// <summary>
        /// Polygon, query and pick radius. The polygon is at least 10 across and the radius at least 6, the range the
        /// callers use, so the bounding-box prefilter is not the subject (see the ignored tests).
        /// </summary>
        private static Gen<Tuple<Polygon, Vector2, double>> HitCases() =>
            from shape in Gen.OneOf(CoreArbitraries.ConvexPolygon(), CoreArbitraries.HoledPolygon())
            from scale in Gen.Elements(1.0, 10.0, 100.0)
            from ox in Gen.Choose(-500000, 500000)
            from oy in Gen.Choose(-500000, 500000)
            from radius in Gen.Elements(6.0, 12.0, 24.0)
            let poly = shape.Scale(scale, Vector2.Zero).Translate(new Vector2(ox, oy))
            let vertices = poly.ExteriorRing.Concat(poly.InteriorRings.SelectMany(r => r)).ToArray()
            from iVertex in Gen.Choose(0, vertices.Length - 1)
            from dx in Gen.Choose(-200, 200)
            from dy in Gen.Choose(-200, 200)
            select Tuple.Create(poly, vertices[iVertex] + new Vector2(radius * dx / 100.0, radius * dy / 100.0), radius);

        #endregion

        #region Known defects

        /// <summary>
        /// Defect: the prefilter pads the bounding box with <c>BoundingBox + padding</c>, and <c>Rectangle + double</c>
        /// scales the box about its centre by that factor instead of growing each edge by it. A radius under 1 shrinks
        /// the box (0.5 halves each side), so a click exactly on an extreme vertex is rejected before any vertex is
        /// tested; a radius near 1 on a small polygon leaves the box without its pick margin.
        /// </summary>
        [TestMethod]
        [Ignore("Defect: PaddedBoundingBoxCovers scales the box by the radius (Rectangle + double) instead of padding it; radius 0.5 shrinks it to half size. Un-ignore when fixed.")]
        public void VertexOnTheBoundingBoxCornerHitsWithSubPixelRadius()
        {
            Assert.IsTrue(Square().PointIntersectsAnyPolygonVertex(new Vector2(100, 100), 0.5, out Polygon hit));
            Assert.IsNotNull(hit);
        }

        [TestMethod]
        [Ignore("Defect: PaddedBoundingBoxCovers scales the box by lineWidth/2 (Rectangle + double) instead of padding it; lineWidth 1 shrinks it to half size. Un-ignore when fixed.")]
        public void SegmentOnTheBoundingBoxEdgeHitsWithSubPixelHalfWidth()
        {
            Assert.IsTrue(Square().PointIntersectsAnyPolygonSegment(new Vector2(100, 50), 1, out Polygon hit));
            Assert.IsNotNull(hit);
        }

        #endregion
    }
}
