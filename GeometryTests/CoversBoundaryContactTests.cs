using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    /// <summary>
    /// OGC Covers is B ⊆ closure(A): a shape nestled inside and meeting the boundary is covered, a shape that only
    /// touches from outside is not. Pins that split for every shape whose relation used to report both as Touching.
    /// </summary>
    [TestClass]
    public class CoversBoundaryContactTests
    {
        private static Polygon Box(double left, double right, double bottom, double top) =>
            ShapeRelationHelpers.RectangleAsPolygon(new Rectangle(left, right, bottom, top));

        [TestMethod]
        public void PolygonDoesNotCoverNeighborSharingAnEdgeOrCorner()
        {
            Polygon a = Box(0, 10, 0, 10);
            Polygon edge = Box(10, 20, 0, 10);
            Polygon partialEdge = Box(10, 20, 5, 15);
            Polygon corner = Box(10, 20, 10, 20);

            foreach (Polygon b in new[] { edge, partialEdge, corner })
            {
                Assert.IsFalse(a.Covers(b), $"Covers {b}");
                Assert.IsFalse(a.Covers((IShape2D)b), $"Covers(IShape2D) {b}");
                Assert.IsFalse(a.Contains(b), $"Contains {b}");
                Assert.IsTrue(a.Intersects(b), $"Intersects {b}");
                Assert.AreEqual(ShapeRelation.Touching | ShapeRelation.Exterior, a.GetRelation(b), $"GetRelation {b}");
            }
        }

        [TestMethod]
        public void PolygonCoversPolygonNestledOnItsBoundary()
        {
            Polygon a = Box(0, 10, 0, 10);
            Polygon nestled = Box(0, 5, 0, 5);
            Assert.IsTrue(a.Covers(nestled));
            Assert.AreEqual(ShapeRelation.Touching, a.GetRelation(nestled));
            Assert.IsTrue(a.Covers(Box(0, 10, 0, 10)), "Identical polygon");
            Assert.IsFalse(a.Covers(Box(5, 15, 0, 10)), "Partial overlap");
            Assert.IsTrue(a.Covers((IShape2D)new Vector2(10, 5)), "Point on the boundary");
        }

        [TestMethod]
        public void RectangleTriangleAndQuadCoversRejectExternalTouch()
        {
            Rectangle rect = new(0, 10, 0, 10);
            Quad quad = new(rect);
            Triangle tri = new(new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 10));
            Polygon neighbor = Box(-10, 0, 0, 10);
            Triangle outsideTri = new(new Vector2(0, 0), new Vector2(-10, 0), new Vector2(0, 10));

            Assert.IsFalse(rect.Covers((IShape2D)neighbor), "Rectangle vs polygon");
            Assert.IsFalse(rect.Covers((IShape2D)outsideTri), "Rectangle vs triangle");
            Assert.IsFalse(quad.Covers((IShape2D)neighbor), "Quad vs polygon");
            Assert.IsFalse(tri.Covers((IShape2D)new Rectangle(-10, 0, 0, 10)), "Triangle vs rectangle");

            Assert.IsTrue(rect.Covers((IShape2D)Box(0, 5, 0, 5)), "Rectangle vs nestled polygon");
            Assert.IsTrue(rect.Covers((IShape2D)tri), "Rectangle vs nestled triangle");
            Assert.IsTrue(quad.Covers((IShape2D)Box(0, 5, 0, 5)), "Quad vs nestled polygon");
            Assert.IsTrue(tri.Covers((IShape2D)new Rectangle(0, 2, 0, 2)), "Triangle vs nestled rectangle");
        }

        [TestMethod]
        public void PolygonCoversInscribedTangentCircleButNotExternalTangent()
        {
            Polygon box = Box(0, 10, 0, 10);
            Circle inscribed = new(new Vector2(5, 5), 5);
            Circle outside = new(new Vector2(15, 5), 5);

            Assert.IsTrue(box.Covers(inscribed));
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(inscribed));
            Assert.IsFalse(box.Covers(outside));
            Assert.IsFalse(box.Covers((IShape2D)outside));
            Assert.AreEqual(ShapeRelation.Touching | ShapeRelation.Exterior, box.GetRelation(outside));
        }

        [TestMethod]
        public void CircleDoesNotCoverTangentLines()
        {
            Circle circle = new(Vector2.Zero, 10);
            LineSegment tangent = new(new Vector2(-5, 10), new Vector2(5, 10));
            Line tangentLine = new(new Vector2(0, 10), Vector2.UnitX);

            const ShapeRelation external = ShapeRelation.Touching | ShapeRelation.Exterior;
            Assert.IsFalse(circle.Covers(tangent));
            Assert.IsFalse(circle.Covers((IShape2D)tangentLine));
            Assert.IsTrue(circle.Intersects(tangent));
            Assert.AreEqual(external, circle.GetRelation(tangent));
            Assert.AreEqual(external, circle.GetRelation((IShape2D)tangentLine));
            Assert.AreEqual(external, tangentLine.GetRelation((IShape2D)circle), "Infinite line vs tangent circle");
            Assert.IsFalse(tangentLine.Covers((IShape2D)circle));
            Assert.IsTrue(circle.Covers(new LineSegment(Vector2.Zero, new Vector2(10, 0))), "Radius to the boundary");
        }

        [TestMethod]
        public void LinesDoNotCoverSegmentsTouchingAtOnePoint()
        {
            LineSegment a = new(new Vector2(0, 0), new Vector2(10, 0));
            LineSegment endToEnd = new(new Vector2(10, 0), new Vector2(20, 0));
            LineSegment corner = new(new Vector2(10, 0), new Vector2(10, 5));
            Line axis = new(Vector2.Zero, Vector2.UnitX);

            const ShapeRelation external = ShapeRelation.Touching | ShapeRelation.Exterior;
            Assert.IsFalse(a.Covers((IShape2D)endToEnd), "Collinear end to end");
            Assert.IsFalse(a.Covers((IShape2D)corner), "Shared endpoint at an angle");
            Assert.IsTrue(a.Intersects(corner));
            Assert.AreEqual(external, a.GetRelation(endToEnd));
            Assert.AreEqual(external, a.GetRelation(corner));
            Assert.IsFalse(axis.Covers((IShape2D)corner), "Infinite line vs segment ending on it");
            Assert.AreEqual(external, axis.GetRelation((IShape2D)corner));
            Assert.AreEqual(external, axis.GetRelation((IShape2D)new LineSegment(new Vector2(3, 5), new Vector2(3, 0))), "Second endpoint on the line");
            Assert.IsTrue(axis.Covers((IShape2D)endToEnd), "Segment on the infinite line");
        }

        [TestMethod]
        public void RectangleCoversSegmentAlongItsEdge()
        {
            Rectangle rect = new(0, 10, 0, 10);
            Assert.IsTrue(rect.Covers((IShape2D)new LineSegment(new Vector2(0, 0), new Vector2(5, 0))));
            Assert.IsFalse(rect.Covers((IShape2D)new LineSegment(new Vector2(10, 5), new Vector2(20, 5))));
        }

        private static Shape2DCollection Parts(params IShape2D[] shapes)
        {
            Shape2DCollection collection = new();
            foreach (IShape2D s in shapes)
                collection.Add(s);
            return collection;
        }

        /// <summary>A collection is covered only when every part is; one externally touching part is enough to fail.</summary>
        [TestMethod]
        public void CollectionWithAnExternallyTouchingPartIsNotCovered()
        {
            Polygon a = Box(0, 10, 0, 10);
            Polygon inside = Box(2, 4, 2, 4);
            Polygon nestled = Box(0, 5, 0, 5);
            Polygon right = Box(10, 20, 0, 10);
            Polygon left = Box(-10, 0, 0, 10);
            const ShapeRelation external = ShapeRelation.Touching | ShapeRelation.Exterior;

            Assert.AreEqual(external, a.GetRelation(Parts(nestled, right)), "Nestled and external");
            Assert.AreEqual(external, a.GetRelation(Parts(right, left)), "Two external");
            Assert.AreEqual(ShapeRelation.Intersecting, a.GetRelation(Parts(inside, right)), "Inside and external");
            Assert.AreEqual(ShapeRelation.Contained, a.GetRelation(Parts(inside)), "Inside only");
            Assert.AreEqual(ShapeRelation.Touching, a.GetRelation(Parts(nestled, Box(5, 10, 5, 10))), "Two nestled");
            Assert.IsFalse(a.Covers((IShape2D)Parts(nestled, right)));
            Assert.IsTrue(a.Covers((IShape2D)Parts(nestled, inside)));
        }

        /// <summary>
        /// A rectangle classifies a circle by its bounding box: a box touching from outside stays external
        /// contact, a box inside is Contained.
        /// </summary>
        [TestMethod]
        public void RectangleCircleRelationKeepsExternalContact()
        {
            Rectangle rect = new(0, 10, 0, 10);
            Assert.AreEqual(ShapeRelation.Touching | ShapeRelation.Exterior, rect.GetRelation(new Circle(new Vector2(15, 5), 5)));
            Assert.AreEqual(ShapeRelation.Contained, rect.GetRelation(new Circle(new Vector2(5, 5), 2)));
            Assert.IsFalse(rect.Covers((IShape2D)new Circle(new Vector2(15, 5), 5)));
        }

        /// <summary>
        /// A segment whose endpoint lies in the epsilon band just outside an edge and runs away from the rectangle
        /// meets the boundary only (no edge crossing) and is not covered.
        /// </summary>
        [TestMethod]
        public void RectangleSegmentLeavingFromTheEpsilonBandIsExternalContact()
        {
            Rectangle rect = new(0, 10, 0, 10);
            LineSegment away = new(new Vector2(-Tolerance.Epsilon / 2, 5), new Vector2(-10, 5));
            Assert.AreEqual(ShapeRelation.Touching | ShapeRelation.Exterior, rect.GetRelation((ILineSegment2D)away));
            Assert.IsFalse(rect.Covers((IShape2D)away));
        }
    }
}
