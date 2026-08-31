using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Guards the non-copying internal accessors and the cached <see cref="Polyline.BoundingBox"/> added for
    /// mesh-generation performance. These assert observable behaviour, not allocation counts: the risk of the
    /// optimization is a shared collection leaking to a caller that mutates it, or a stale bounding box.
    /// </summary>
    [TestClass]
    public class HotPathAllocationTests
    {
        private static Polygon SquareWithHole()
        {
            Vector2[] exterior =
            [
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 10),
                new Vector2(0, 10),
                new Vector2(0, 0)
            ];

            Vector2[] interior =
            [
                new Vector2(4, 4),
                new Vector2(6, 4),
                new Vector2(6, 6),
                new Vector2(4, 6),
                new Vector2(4, 4)
            ];

            Polygon poly = new(exterior);
            poly.AddInteriorRing(interior);
            return poly;
        }

        [TestMethod]
        public void PolygonGetRelationPointClassification()
        {
            Polygon poly = SquareWithHole();

            Assert.AreEqual(ShapeRelation.Contained, poly.GetRelation((IPoint2D)new Vector2(2, 2)), "Interior point");
            Assert.AreEqual(ShapeRelation.None, poly.GetRelation((IPoint2D)new Vector2(-1, 5)), "Point outside the bounding box");
            Assert.AreEqual(ShapeRelation.None, poly.GetRelation((IPoint2D)new Vector2(20, 20)), "Point far outside");
            Assert.AreEqual(ShapeRelation.Touching, poly.GetRelation((IPoint2D)new Vector2(5, 0)), "Point on an exterior edge");
            Assert.AreEqual(ShapeRelation.Touching, poly.GetRelation((IPoint2D)new Vector2(0, 0)), "Point on an exterior vertex");
            Assert.AreEqual(ShapeRelation.None, poly.GetRelation((IPoint2D)new Vector2(5, 5)), "Point inside a hole is outside the polygon");
            Assert.AreEqual(ShapeRelation.Touching, poly.GetRelation((IPoint2D)new Vector2(5, 4)), "Point on a hole edge");
            Assert.AreEqual(ShapeRelation.Touching, poly.GetRelation((IPoint2D)new Vector2(4, 4)), "Point on a hole vertex");

            Assert.IsTrue(poly.Contains(new Vector2(2, 2)));
            Assert.IsFalse(poly.Contains(new Vector2(5, 5)));
        }

        /// <summary>
        /// The winding test must see every exterior segment. A concave polygon whose test ray crosses several
        /// edges catches an accidental spatial-index narrowing of the segment set.
        /// </summary>
        [TestMethod]
        public void PolygonGetRelationConcaveShape()
        {
            Vector2[] comb =
            [
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 10),
                new Vector2(8, 10),
                new Vector2(8, 3),
                new Vector2(6, 3),
                new Vector2(6, 10),
                new Vector2(4, 10),
                new Vector2(4, 3),
                new Vector2(2, 3),
                new Vector2(2, 10),
                new Vector2(0, 10),
                new Vector2(0, 0)
            ];

            Polygon poly = new(comb);

            Assert.AreEqual(ShapeRelation.Contained, poly.GetRelation((IPoint2D)new Vector2(5, 1)), "Below the teeth");
            Assert.AreEqual(ShapeRelation.Contained, poly.GetRelation((IPoint2D)new Vector2(1, 8)), "Inside the left tooth");
            Assert.AreEqual(ShapeRelation.Contained, poly.GetRelation((IPoint2D)new Vector2(9, 8)), "Inside the right tooth");
            Assert.AreEqual(ShapeRelation.None, poly.GetRelation((IPoint2D)new Vector2(3, 8)), "In the gap between teeth");
            Assert.AreEqual(ShapeRelation.None, poly.GetRelation((IPoint2D)new Vector2(7, 8)), "In the second gap");
        }

        /// <summary>
        /// Many-segment polygon: the removed ternary in GetRelation had a >32 segment branch, so exercise both sides.
        /// </summary>
        [TestMethod]
        public void PolygonGetRelationManySegments()
        {
            const int nVerts = 128;
            List<Vector2> circle = new(nVerts + 1);
            for (int i = 0; i < nVerts; i++)
            {
                double theta = 2.0 * System.Math.PI * i / nVerts;
                circle.Add(new Vector2(System.Math.Cos(theta) * 10.0, System.Math.Sin(theta) * 10.0));
            }
            circle.Add(circle[0]);

            Polygon poly = new(circle.ToArray());

            Assert.AreEqual(ShapeRelation.Contained, poly.GetRelation((IPoint2D)new Vector2(0, 0)));
            Assert.AreEqual(ShapeRelation.Contained, poly.GetRelation((IPoint2D)new Vector2(9, 0)));
            Assert.AreEqual(ShapeRelation.None, poly.GetRelation((IPoint2D)new Vector2(11, 0)));
            Assert.AreEqual(ShapeRelation.Touching, poly.GetRelation((IPoint2D)circle[0]), "On a vertex");
        }

        [TestMethod]
        public void PolygonExteriorRingIsDefensiveCopy()
        {
            Polygon poly = SquareWithHole();

            Vector2[] first = poly.ExteriorRing;
            Assert.IsFalse(object.ReferenceEquals(first, poly.ExteriorRing), "ExteriorRing must hand out a copy");

            Vector2 original = first[1];
            first[1] = new Vector2(1000, 1000);

            Assert.AreEqual(original, poly.ExteriorRing[1], "Mutating the returned ring must not change the polygon");
            Assert.AreEqual(ShapeRelation.Contained, poly.GetRelation((IPoint2D)new Vector2(2, 2)));
            Assert.AreEqual(ShapeRelation.None, poly.GetRelation((IPoint2D)new Vector2(500, 500)));
        }

        private static Polyline MakePolyline(params Vector2[] points) => new Polyline(points);

        [TestMethod]
        public void PolylineLineSegmentsIsDefensiveCopy()
        {
            Polyline line = MakePolyline(new Vector2(0, 0), new Vector2(5, 0), new Vector2(5, 5));

            List<LineSegment> segments = line.LineSegments;
            Assert.IsFalse(object.ReferenceEquals(segments, line.LineSegments), "LineSegments must hand out a copy");

            int expectedCount = segments.Count;
            segments.Clear();
            segments.Add(new LineSegment(new Vector2(-100, -100), new Vector2(-99, -99)));

            Assert.AreEqual(expectedCount, line.LineSegments.Count, "Mutating the returned list must not change the polyline");
            Assert.AreEqual(expectedCount, line.LineCount);
            Assert.AreEqual(new Vector2(0, 0), line.LineSegments[0].A);
        }

        private static void AssertBoundingBoxMatchesFreshCopy(Polyline line, string message)
        {
            Polyline fresh = new([.. line.Points.Select(p => p.ToVector2())]);
            Rectangle expected = fresh.BoundingBox;
            Rectangle actual = line.BoundingBox;

            Assert.AreEqual(expected.Left, actual.Left, message + " (Left)");
            Assert.AreEqual(expected.Right, actual.Right, message + " (Right)");
            Assert.AreEqual(expected.Bottom, actual.Bottom, message + " (Bottom)");
            Assert.AreEqual(expected.Top, actual.Top, message + " (Top)");
        }

        [TestMethod]
        public void PolylineBoundingBoxCorrectForConstructedShape()
        {
            Polyline line = MakePolyline(new Vector2(1, 2), new Vector2(7, -3), new Vector2(4, 9));
            Rectangle bbox = line.BoundingBox;

            Assert.AreEqual(1, bbox.Left);
            Assert.AreEqual(7, bbox.Right);
            Assert.AreEqual(-3, bbox.Bottom);
            Assert.AreEqual(9, bbox.Top);

            Assert.AreEqual(bbox.Left, line.BoundingBox.Left, "Repeated reads must agree");
            Assert.AreEqual(bbox.Top, line.BoundingBox.Top, "Repeated reads must agree");
        }

        /// <summary>
        /// The bounding box cache is keyed on point count, so every mutator must be read-mutate-read tested.
        /// Add and Insert are the only mutators of the point list.
        /// </summary>
        [TestMethod]
        public void PolylineBoundingBoxInvalidatedByAdd()
        {
            Polyline line = new();
            line.Add(new Vector2(0, 0));
            AssertBoundingBoxMatchesFreshCopy(line, "Single point");

            line.Add(new Vector2(2, 2));
            AssertBoundingBoxMatchesFreshCopy(line, "Second point");

            _ = line.BoundingBox;
            line.Add(new Vector2(-5, 7));
            AssertBoundingBoxMatchesFreshCopy(line, "Add after a cached read expands the box");

            _ = line.BoundingBox;
            line.Add(new Vector2(1, 1.5));
            AssertBoundingBoxMatchesFreshCopy(line, "Add inside the existing box");
        }

        [TestMethod]
        public void PolylineBoundingBoxInvalidatedByInsertAtEnds()
        {
            Polyline line = MakePolyline(new Vector2(0, 0), new Vector2(4, 0));
            _ = line.BoundingBox;

            line.Insert(line.PointCount, new Vector2(4, 6));
            AssertBoundingBoxMatchesFreshCopy(line, "Insert at the end (delegates to Add)");

            _ = line.BoundingBox;
            line.Insert(0, new Vector2(-3, -2));
            AssertBoundingBoxMatchesFreshCopy(line, "Insert at the start");
        }

        [TestMethod]
        public void PolylineBoundingBoxInvalidatedByInsertOnSinglePointLine()
        {
            Polyline line = new();
            line.Add(new Vector2(3, 3));
            _ = line.BoundingBox;

            line.Insert(0, new Vector2(-8, 12));
            AssertBoundingBoxMatchesFreshCopy(line, "Insert into a one point polyline");
        }

        [TestMethod]
        public void PolylineBoundingBoxInvalidatedByInsertInMiddle()
        {
            Polyline line = MakePolyline(new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10));
            _ = line.BoundingBox;

            line.Insert(1, new Vector2(5, -20));
            AssertBoundingBoxMatchesFreshCopy(line, "Mid-polyline insert that extends the box downward");
        }

        [TestMethod]
        public void PolylineBoundingBoxInvalidatedByAddPointsAtIntersections()
        {
            Polyline line = MakePolyline(new Vector2(0, 0), new Vector2(10, 0));
            _ = line.BoundingBox;

            line.AddPointsAtIntersections(new LineSegment(new Vector2(5, -5), new Vector2(5, 5)));
            AssertBoundingBoxMatchesFreshCopy(line, "Intersection vertex inserted");
            Assert.AreEqual(3, line.PointCount);
        }

        /// <summary>
        /// Translate returns a new polyline; the original must be untouched and the copy must have its own box.
        /// </summary>
        [TestMethod]
        public void PolylineTranslateDoesNotDisturbSourceBoundingBox()
        {
            Polyline line = MakePolyline(new Vector2(0, 0), new Vector2(4, 3));
            Rectangle before = line.BoundingBox;

            Polyline moved = line.Translate(new Vector2(10, 10));

            AssertBoundingBoxMatchesFreshCopy(line, "Source polyline after Translate");
            AssertBoundingBoxMatchesFreshCopy(moved, "Translated polyline");
            Assert.AreEqual(before.Left, line.BoundingBox.Left);
            Assert.AreEqual(before.Left + 10, moved.BoundingBox.Left);
        }

        [TestMethod]
        public void PolylineGetRelationUnchangedByCacheReuse()
        {
            Polyline line = MakePolyline(new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10));

            Assert.AreEqual(ShapeRelation.Touching, line.GetRelation((IPoint2D)new Vector2(0, 0)), "Start point");
            Assert.AreEqual(ShapeRelation.Touching, line.GetRelation((IPoint2D)new Vector2(10, 10)), "End point");
            Assert.AreEqual(ShapeRelation.Contained, line.GetRelation((IPoint2D)new Vector2(5, 0)), "On a segment");
            Assert.AreEqual(ShapeRelation.None, line.GetRelation((IPoint2D)new Vector2(5, 5)), "Off the polyline");

            _ = line.LineSegments;
            Assert.AreEqual(ShapeRelation.Contained, line.GetRelation((IPoint2D)new Vector2(5, 0)), "Answers stable after a public copy was taken");
        }
    }
}
