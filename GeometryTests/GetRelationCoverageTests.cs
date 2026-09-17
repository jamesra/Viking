using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GeometryTests
{
    [TestClass]
    public class GetRelationCoverageTests
    {
        [TestMethod]
        public void LineSegmentMidpointIsContainedEndpointIsTouching()
        {
            LineSegment seg = new(new Vector2(0, 0), new Vector2(10, 0));
            Assert.AreEqual(ShapeRelation.Contained, seg.GetRelation((IPoint2D)new Vector2(5, 0)));
            Assert.IsTrue(seg.Contains(new Vector2(5, 0)));
            Assert.AreEqual(ShapeRelation.Touching, seg.GetRelation((IPoint2D)seg.A));
            Assert.IsFalse(seg.Contains(seg.A));
            Assert.IsTrue(seg.Covers(seg.A));
            Assert.AreEqual(ShapeRelation.None, seg.GetRelation((IPoint2D)new Vector2(5, 1)));
            Assert.IsFalse(seg.Covers(new Vector2(15, 0)));
            Assert.AreEqual(ShapeRelation.None, seg.GetRelation((IPoint2D)new Vector2(15, 0)));
        }

        [TestMethod]
        public void InfiniteLineOnLinePointIsContained()
        {
            Line line = new(new Vector2(0, 0), Vector2.UnitX);
            Assert.AreEqual(ShapeRelation.Contained, line.GetRelation((IPoint2D)new Vector2(100, 0)));
            Assert.IsTrue(line.Contains((IPoint2D)new Vector2(100, 0)));
            Assert.IsTrue(line.Covers((IPoint2D)new Vector2(100, 0)));
        }

        [TestMethod]
        public void CircleBoundaryWithinEpsilonIsTouching()
        {
            Circle circle = new(Vector2.Zero, 10);
            Vector2 boundary = new(10, 0);
            Assert.AreEqual(ShapeRelation.Touching, circle.GetRelation(boundary));
            Assert.IsFalse(circle.Contains(boundary));
            Assert.IsTrue(circle.Covers(boundary));

            Vector2 justInside = new(10 - (Tolerance.Epsilon / 2), 0);
            ShapeRelation insideRel = circle.GetRelation(justInside);
            Assert.IsTrue(insideRel is ShapeRelation.Contained or ShapeRelation.Touching);
            Assert.IsTrue(circle.Covers(justInside));
        }

        [TestMethod]
        public void RectangleCornerIsTouchingInteriorIsContained()
        {
            Rectangle rect = new(0, 10, 0, 10);
            Assert.AreEqual(ShapeRelation.Touching, rect.GetRelation((IPoint2D)rect.LowerLeft));
            Assert.IsFalse(rect.Contains((IPoint2D)rect.LowerLeft));
            Assert.IsTrue(rect.Covers((IPoint2D)rect.LowerLeft));
            Assert.AreEqual(ShapeRelation.Contained, rect.GetRelation((IPoint2D)rect.Center));
            Assert.IsTrue(rect.Contains((IPoint2D)rect.Center));
        }

        [TestMethod]
        public void PolygonPolygonSharedEdgeIsTouching()
        {
            Polygon left = new([
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 10),
                new Vector2(0, 10),
                new Vector2(0, 0)
            ]);
            Polygon right = new([
                new Vector2(10, 0),
                new Vector2(20, 0),
                new Vector2(20, 10),
                new Vector2(10, 10),
                new Vector2(10, 0)
            ]);

            Assert.AreEqual(ShapeRelation.Touching, left.GetRelation(right));
            Assert.IsFalse(left.Contains(right));
            Assert.IsTrue(left.Covers(right));
            Assert.IsTrue(left.Intersects(right));
        }

        [TestMethod]
        public void PolygonPolygonNestedIsContainedCrossingIsIntersecting()
        {
            Polygon outer = Primitives.BoxPolygon(10);
            Polygon inner = Primitives.BoxPolygon(1);
            Assert.AreEqual(ShapeRelation.Contained, outer.GetRelation(inner));
            Assert.IsTrue(outer.Contains(inner));
            Assert.IsTrue(outer.Covers(inner));

            Polygon shifted = inner.Translate(new Vector2(10, 0));
            Assert.AreEqual(ShapeRelation.Intersecting, outer.GetRelation(shifted));
            Assert.IsFalse(outer.Contains(shifted));
            Assert.IsFalse(outer.Covers(shifted));
            Assert.IsTrue(outer.Intersects(shifted));
        }

        [TestMethod]
        public void PolygonCircleInsideOutsideTangentAndHole()
        {
            Polygon box = Primitives.BoxPolygon(10);
            Circle inside = new(Vector2.Zero, 1);
            Assert.AreEqual(ShapeRelation.Contained, box.GetRelation(inside));
            Assert.IsTrue(box.Contains(inside));

            Circle outside = new(new Vector2(50, 0), 1);
            Assert.AreEqual(ShapeRelation.None, box.GetRelation(outside));

            Circle tangent = new(new Vector2(15, 0), 5);
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(tangent));

            Circle crossing = new(new Vector2(10, 0), 3);
            Assert.AreEqual(ShapeRelation.Intersecting, box.GetRelation(crossing));

            Polygon holed = Primitives.BoxPolygon(10);
            holed.AddInteriorRing(Primitives.BoxPolygon(5).ExteriorRing);
            Circle inHole = new(Vector2.Zero, 1);
            Assert.AreEqual(ShapeRelation.None, holed.GetRelation(inHole));

            Vector2 holeVertex = holed.InteriorRings[0][0];
            Assert.AreEqual(ShapeRelation.Touching, holed.GetRelation(holeVertex));
            Assert.IsFalse(holed.Contains(holeVertex));
            Assert.IsTrue(holed.Covers(holeVertex));
        }

        [TestMethod]
        public void CircleContainsAndCoversRectangleAndTriangle()
        {
            Circle circle = new(Vector2.Zero, 10);
            Rectangle rect = new(-2, 2, -2, 2);
            Assert.AreEqual(ShapeRelation.Contained, circle.GetRelation(rect));
            Assert.IsTrue(circle.Contains(rect));
            Assert.IsTrue(circle.Covers((IShape2D)rect));

            Triangle tri = new(new Vector2(-1, -1), new Vector2(1, -1), new Vector2(0, 1));
            Assert.AreEqual(ShapeRelation.Contained, circle.GetRelation(tri));
            Assert.IsTrue(circle.Contains(tri));
            Assert.IsTrue(circle.Covers((IShape2D)tri));

            Rectangle inscribedCorners = new(-10, 10, -10, 10);
            Assert.AreEqual(ShapeRelation.Intersecting, circle.GetRelation(inscribedCorners));
            Assert.IsFalse(circle.Contains(inscribedCorners));
            Assert.IsFalse(circle.Covers((IShape2D)inscribedCorners));
        }

        [TestMethod]
        public void CircleContainsShapeImpliesCoversAndIntersects()
        {
            Circle circle = new(Vector2.Zero, 10);
            Polygon box = Primitives.BoxPolygon(1);
            Assert.IsTrue(circle.Contains(box));
            Assert.IsTrue(circle.Covers(box));
            Assert.IsTrue(circle.Intersects(box));
        }

        [TestMethod]
        public void RectangleEpsilonBandIsTouchingAndCovers()
        {
            Rectangle rect = new(0, 10, 0, 10);
            Vector2 justOutside = new(-Tolerance.Epsilon / 2, 5);
            Assert.AreEqual(ShapeRelation.Touching, rect.GetRelation((IPoint2D)justOutside));
            Assert.IsTrue(rect.Covers(justOutside, Tolerance.Epsilon));
            Assert.IsFalse(rect.Contains((IPoint2D)justOutside));
        }

        [TestMethod]
        public void ZeroLengthLineSegmentConstructorThrows() =>
            Assert.ThrowsException<ArgumentException>(() => new LineSegment(Vector2.Zero, Vector2.Zero));

        [TestMethod]
        public void PolylineAreaThrows() =>
            Assert.ThrowsException<ArgumentException>(() =>
            {
                double unused = new Polyline([new Vector2(0, 0), new Vector2(1, 0)]).Area;
            });

        [TestMethod]
        public void PointAsShapeVersusPolygonUsesShapeOverload()
        {
            Polygon box = Primitives.BoxPolygon(10);
            IShape2D interior = new Vector2(0, 0);
            Assert.AreEqual(ShapeRelation.Contained, box.GetRelation(interior));
            Assert.IsTrue(box.Contains(interior));
            Assert.IsTrue(box.Covers(interior));

            IShape2D onEdge = new Vector2(10, 0);
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(onEdge));
            Assert.IsFalse(box.Contains(onEdge));
            Assert.IsTrue(box.Covers(onEdge));
        }

        [TestMethod]
        public void CollectionGetRelationOrsFlagsWhileContainsIsAnyChild()
        {
            Polygon outer = Primitives.BoxPolygon(10);
            Polygon nested = Primitives.BoxPolygon(0.5);
            Shape2DCollection coll = new();
            coll.Add(outer);
            coll.Add(nested);

            Circle query = new(Vector2.Zero, 1);
            Assert.AreEqual(ShapeRelation.Contained, outer.GetRelation(query));
            Assert.AreEqual(ShapeRelation.Intersecting, nested.GetRelation(query));

            ShapeRelation rel = coll.GetRelation(query);
            Assert.AreEqual(ShapeRelation.Contained | ShapeRelation.Intersecting, rel);
            Assert.IsFalse(rel.IsContains());
            Assert.IsFalse(rel.IsCovers());
            Assert.IsTrue(coll.Contains(query));
            Assert.IsTrue(coll.Covers(query));
            Assert.IsTrue(coll.Intersects(query));
        }

        [TestMethod]
        public void InfiniteLineContainsColinearSegment()
        {
            Line line = new(new Vector2(0, 0), Vector2.UnitX);
            LineSegment onLine = new(new Vector2(5, 0), new Vector2(15, 0));
            Assert.AreEqual(ShapeRelation.Contained, line.GetRelation((IShape2D)onLine));
            Assert.IsTrue(line.Contains(onLine));
            Assert.IsTrue(line.Covers(onLine));
            Assert.IsTrue(line.Intersects(onLine));

            LineSegment crossing = new(new Vector2(0, -1), new Vector2(0, 1));
            Assert.AreEqual(ShapeRelation.Intersecting, line.GetRelation((IShape2D)crossing));
            Assert.IsFalse(line.Contains(crossing));
            Assert.IsTrue(line.Intersects(crossing));
        }
    }
}
