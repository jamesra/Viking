using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    [TestClass]
    public class GeometryHashCodeTests
    {
        [TestMethod]
        public void LineSegmentUndirected_IgnoresEndpointOrder()
        {
            Vector2 a = new(1, 2);
            Vector2 b = new(5, -3);

            Assert.AreEqual(GeometryHashCode.LineSegmentUndirected(a, b), GeometryHashCode.LineSegmentUndirected(b, a));
        }

        [TestMethod]
        public void LineSegmentDirected_DependsOnEndpointOrder()
        {
            Vector2 a = new(1, 2);
            Vector2 b = new(5, -3);

            Assert.AreEqual(GeometryHashCode.Combine(GeometryHashCode.Point2D(a), GeometryHashCode.Point2D(b)),
                GeometryHashCode.LineSegmentDirected(a, b));
            Assert.AreNotEqual(GeometryHashCode.LineSegmentDirected(a, b), GeometryHashCode.LineSegmentDirected(b, a));
        }

        [TestMethod]
        public void Point3D_CombinesQuantizedCoordinatesInOrder()
        {
            int expected = GeometryHashCode.Combine(
                GeometryHashCode.QuantizedCoord(1.5), GeometryHashCode.QuantizedCoord(-2.5), GeometryHashCode.QuantizedCoord(4));

            Assert.AreEqual(expected, GeometryHashCode.Point3D(1.5, -2.5, 4));
            Assert.AreNotEqual(GeometryHashCode.Point3D(1.5, -2.5, 4), GeometryHashCode.Point3D(4, -2.5, 1.5));
        }

        [TestMethod]
        public void PointN_MatchesFoldAndIsOrderSensitive()
        {
            int expected = 17;
            double[] coords = { 1, 2, 3 };
            foreach (double c in coords)
                expected = unchecked((expected * 397) ^ GeometryHashCode.QuantizedCoord(c));

            Assert.AreEqual(expected, GeometryHashCode.PointN(coords));
            Assert.AreEqual(17, GeometryHashCode.PointN(new double[0]));
            Assert.AreNotEqual(GeometryHashCode.PointN(coords), GeometryHashCode.PointN(new double[] { 3, 2, 1 }));
        }

        [TestMethod]
        public void Point2D_SharesHashWithinEpsilon()
        {
            Assert.AreEqual(GeometryHashCode.Point2D(1.25, 2.5), GeometryHashCode.Point2D(1.25 + Tolerance.Epsilon / 1000, 2.5));
        }
    }
}
