using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace GeometryTests
{
    [TestClass]
    public class HasControlPointsTest
    {
        [TestMethod]
        public void LineSegment_ExposesEndpoints()
        {
            Vector2 a = new(-10, 0);
            Vector2 b = new(10, 0);
            AssertControlPoints(new LineSegment(a, b), ShapeType2D.Line, a, b);
        }

        [TestMethod]
        public void Polyline_ExposesPointsInOrder()
        {
            Vector2[] expected = [new(0, 0), new(1, 0), new(1, 1)];
            AssertControlPoints(new Polyline(expected), ShapeType2D.Polyline, expected);
        }

        [TestMethod]
        public void Polygon_ExposesExteriorRingOnly()
        {
            Polygon poly = Primitives.BoxPolygon(1);
            IPolygon2D asPoly = poly;
            AssertControlPoints(poly, ShapeType2D.Polygon,
                [.. asPoly.ExteriorRing.Select(p => new Vector2(p.X, p.Y))]);
            Assert.AreEqual(0, asPoly.InteriorRings.Count);
        }

        [TestMethod]
        public void Circle_ExposesCenter()
        {
            Vector2 center = new(3, 4);
            AssertControlPoints(new Circle(center, 2), ShapeType2D.Circle, center);
        }

        [TestMethod]
        public void Triangle_ExposesThreeVertices()
        {
            Vector2 p1 = new(0, 0);
            Vector2 p2 = new(1, 0);
            Vector2 p3 = new(0, 1);
            AssertControlPoints(new Triangle(p1, p2, p3), ShapeType2D.Triangle, p1, p2, p3);
        }

        [TestMethod]
        public void Rectangle_ExposesCorners()
        {
            Rectangle rect = new(new Vector2(0, 0), new Vector2(2, 3));
            AssertControlPoints(rect, ShapeType2D.Rectangle,
                rect.LowerLeft, rect.UpperLeft, rect.UpperRight, rect.LowerRight);
        }

        [TestMethod]
        public void Quad_ExposesCorners()
        {
            Quad quad = new(new Vector2(0, 0), 2, 3);
            AssertControlPoints(quad, ShapeType2D.Quad,
                quad.BottomLeft, quad.BottomRight, quad.TopRight, quad.TopLeft);
        }

        [TestMethod]
        public void Vector2_ExposesSelf()
        {
            Vector2 p = new(5, 7);
            AssertControlPoints(p, ShapeType2D.Point, p);
        }

        private static void AssertControlPoints(IShape2D shape, ShapeType2D expectedType, params Vector2[] expected)
        {
            Assert.IsInstanceOfType(shape, typeof(IHasControlPoints));
            IHasControlPoints cps = (IHasControlPoints)shape;
            Assert.AreEqual(expectedType, cps.ShapeType);
            Assert.AreEqual(expected.Length, cps.ControlPoints.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                IPoint2D p = cps.ControlPoints[i];
                Assert.AreEqual(expected[i].X, p.X, 0.0001, $"X mismatch at {i}");
                Assert.AreEqual(expected[i].Y, p.Y, 0.0001, $"Y mismatch at {i}");
            }
        }
    }
}
