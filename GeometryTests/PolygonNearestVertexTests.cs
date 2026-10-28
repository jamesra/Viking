using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Pins <see cref="Polygon.NearestVertex"/> and <see cref="Polygon.RemoveVertex(Vector2)"/> against a brute-force
    /// search over every exterior and interior ring vertex. Regression: the search once seeded its best distance with
    /// the linear distance to vertex 0 and compared it with squared distances, so vertex 0 won whenever its distance was
    /// below the true nearest vertex's squared distance.
    /// </summary>
    [TestClass]
    public class PolygonNearestVertexTests
    {
        private static readonly Vector2[] Square =
        [
            new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(0, 0)
        ];

        private static double BruteForceNearestDistance(Polygon poly, Vector2 p) =>
            poly.ExteriorRing.Concat(poly.InteriorRings.SelectMany(r => r)).Min(v => Vector2.Distance(v, p));

        [TestMethod]
        public void NearestVertexSquareRegression()
        {
            Polygon square = new(Square);
            Vector2 query = new(7, 2);

            double distance = square.NearestVertex(query, out PolygonIndex index);

            Assert.AreEqual(new Vector2(10, 0), square[index]);
            Assert.AreEqual(Math.Sqrt(13), distance, 1e-12);
        }

        [TestMethod]
        public void NearestVertexSquareRegressionAtVolumeScale()
        {
            Vector2 offset = new(250000, 375000);
            Polygon square = new Polygon(Square).Translate(offset);

            square.NearestVertex(new Vector2(7, 2) + offset, out PolygonIndex index);

            Assert.AreEqual(new Vector2(10, 0) + offset, square[index]);
        }

        [TestMethod]
        public void NearestVertexFindsHoleVertex()
        {
            Polygon poly = new(
                [new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 100), new Vector2(0, 100), new Vector2(0, 0)],
                [[new Vector2(6, 6), new Vector2(10, 6), new Vector2(10, 10), new Vector2(6, 10), new Vector2(6, 6)]]);
            Vector2 query = new(6, 3);

            double distance = poly.NearestVertex(query, out PolygonIndex index);

            Assert.IsTrue(index.IsInner);
            Assert.AreEqual(new Vector2(6, 6), poly[index]);
            Assert.AreEqual(3.0, distance, 1e-12);
        }

        [TestMethod]
        public void NearestVertexExactHitReturnsZero()
        {
            Polygon poly = new(
                [new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 100), new Vector2(0, 100), new Vector2(0, 0)],
                [[new Vector2(6, 6), new Vector2(10, 6), new Vector2(10, 10), new Vector2(6, 10), new Vector2(6, 6)]]);

            Assert.AreEqual(0.0, poly.NearestVertex(new Vector2(10, 6), out PolygonIndex inner));
            Assert.AreEqual(new Vector2(10, 6), poly[inner]);

            Assert.AreEqual(0.0, poly.NearestVertex(new Vector2(100, 100), out PolygonIndex outer));
            Assert.IsFalse(outer.IsInner);
            Assert.AreEqual(new Vector2(100, 100), poly[outer]);

            Assert.AreEqual(0.0, poly.NearestVertex(new Vector2(0, 0), out PolygonIndex first));
            Assert.AreEqual(new Vector2(0, 0), poly[first]);
            Assert.AreEqual(4, first.NumUniqueInRing);
        }

        [TestMethod]
        public void RemoveVertexByPositionRemovesNearestVertex()
        {
            Polygon pentagon = new([new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(5, 15), new Vector2(0, 10), new Vector2(0, 0)]);

            pentagon.RemoveVertex(new Vector2(7, 2));

            CollectionAssert.DoesNotContain(pentagon.ExteriorRing, new Vector2(10, 0));
            CollectionAssert.Contains(pentagon.ExteriorRing, new Vector2(0, 0));
            Assert.AreEqual(5, pentagon.ExteriorRing.Length);
        }

        /// <summary>
        /// Convex and holed polygons scaled up to 100x and moved to volume-scale offsets, queried around their bounding
        /// box: the returned vertex and distance agree with a brute-force search over every ring.
        /// </summary>
        [TestMethod]
        public void NearestVertexMatchesBruteForce()
        {
            Gen<Tuple<Polygon, Vector2>> cases =
                from shape in Gen.OneOf(CoreArbitraries.ConvexPolygon(), CoreArbitraries.HoledPolygon())
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

                    double distance = poly.NearestVertex(query, out PolygonIndex index);

                    return (Math.Abs(Vector2.Distance(poly[index], query) - expected) <= tolerance)
                        .Label($"vertex {poly[index]} at {Vector2.Distance(poly[index], query)}, brute force {expected}")
                        .And((Math.Abs(distance - expected) <= tolerance).Label($"returned {distance}, brute force {expected}"))
                        .And((index.NumUniqueInRing == index.Polygon(poly).ExteriorRing.Length - 1).Label($"ring size {index.NumUniqueInRing}"));
                }),
                nameof(NearestVertexMatchesBruteForce));
        }
    }
}
