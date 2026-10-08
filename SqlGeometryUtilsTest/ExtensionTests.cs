using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;
using System;

namespace SqlGeometryUtilsTest
{
    [TestClass]
    public class SqlGeometryUtilsTest
    {
        static SqlGeometryUtilsTest()
        {
            //SqlServerTypes.Utilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);
        }

        private static void AssertPosition(Vector2 A, Vector2 B) => Assert.IsTrue(Vector2.Distance(A, B) <= .001);

        [TestMethod]
        public void TestTranslateCircleGeometry()
        {
            SqlGeometry circle = Extensions.ToCircle(0, 0, 0, 100);
            TestTranslateMoveGeometry(circle);
        }

        [TestMethod]
        public void IShape2D_LineSegment_ToSqlGeometryAndToPoints()
        {
            IShape2D shape = new LineSegment(new Vector2(-10, 0), new Vector2(10, 0));
            Vector2[] points = shape.ToPoints();
            Assert.AreEqual(2, points.Length);
            AssertPosition(points[0], new Vector2(-10, 0));
            AssertPosition(points[1], new Vector2(10, 0));

            SqlGeometry geom = shape.ToSqlGeometry();
            Assert.AreEqual("LineString", geom.STGeometryType().Value);
            Assert.AreEqual(2, (int)geom.STNumPoints().Value);
        }

        [TestMethod]
        public void IShape2D_Polyline_ToPoints_DoesNotUseSqlGeometry()
        {
            IShape2D shape = new Polyline([new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1)]);
            Vector2[] points = shape.ToPoints();
            Assert.AreEqual(3, points.Length);
            AssertPosition(points[0], new Vector2(0, 0));
            AssertPosition(points[1], new Vector2(1, 0));
            AssertPosition(points[2], new Vector2(1, 1));
        }

        /// <summary>
        /// <see cref="Polygon"/> stores its exterior ring counter-clockwise and reverses a clockwise input,
        /// so <c>ToPoints</c> must match the constructed polygon's ring, not the raw input array.
        /// </summary>
        [TestMethod]
        public void IShape2D_Polygon_ToPoints_IsExteriorRing()
        {
            Vector2[] clockwiseRing =
            [
                new(-10, -10),
                new(-10, 10),
                new(10, 10),
                new(10, -10),
                new(-10, -10)
            ];
            Polygon polygon = new(clockwiseRing);
            Vector2[] points = ((IShape2D)polygon).ToPoints();
            Vector2[] exteriorRing = polygon.ExteriorRing;

            Assert.AreEqual(exteriorRing.Length, points.Length);
            for (int i = 0; i < exteriorRing.Length; i++)
                AssertPosition(points[i], exteriorRing[i]);
        }

        [TestMethod]
        public void IShape2D_Polygon_ToPoints_ClockwiseInputIsReversed()
        {
            Vector2[] clockwiseRing =
            [
                new(-10, -10),
                new(-10, 10),
                new(10, 10),
                new(10, -10),
                new(-10, -10)
            ];
            Vector2[] points = ((IShape2D)new Polygon(clockwiseRing)).ToPoints();

            Assert.AreEqual(clockwiseRing.Length, points.Length);
            for (int i = 0; i < clockwiseRing.Length; i++)
                AssertPosition(points[i], clockwiseRing[clockwiseRing.Length - 1 - i]);
        }

        [TestMethod]
        public void IShape2D_Polygon_ToPoints_CounterClockwiseInputIsKept()
        {
            Vector2[] counterClockwiseRing =
            [
                new(-10, -10),
                new(10, -10),
                new(10, 10),
                new(-10, 10),
                new(-10, -10)
            ];
            Vector2[] points = ((IShape2D)new Polygon(counterClockwiseRing)).ToPoints();

            Assert.AreEqual(counterClockwiseRing.Length, points.Length);
            for (int i = 0; i < counterClockwiseRing.Length; i++)
                AssertPosition(points[i], counterClockwiseRing[i]);
        }

        [TestMethod]
        public void IShape2D_Polygon_ToPoints_ExcludesInteriorRings()
        {
            Vector2[] exterior =
            [
                new(-10, -10),
                new(10, -10),
                new(10, 10),
                new(-10, 10),
                new(-10, -10)
            ];
            Vector2[] hole =
            [
                new(-5, -5),
                new(5, -5),
                new(5, 5),
                new(-5, 5),
                new(-5, -5)
            ];
            Polygon polygon = new(exterior, [hole]);
            Vector2[] points = ((IShape2D)polygon).ToPoints();

            Assert.AreEqual(polygon.ExteriorRing.Length, points.Length);
            foreach (Vector2 p in points)
                Assert.IsTrue(Math.Abs(p.X) == 10 || Math.Abs(p.Y) == 10, $"{p} is not on the exterior ring");
        }

        [TestMethod]
        public void TestTranslateLineGeometry()
        {
            Vector2[] points = [ new(-10,0),
                                                       new(0,0),
                                                       new(10,0)];
            SqlGeometry line = Extensions.ToSqlGeometry(points);
            TestTranslateMoveGeometry(line);
        }

        [TestMethod]
        public void TestTranslatePolyGeometry()
        {
            Vector2[] points = [ new(-10,-10),
                                                       new(-10,10),
                                                       new(10,0)];
            SqlGeometry line = Extensions.ToPolygon(points);
            TestTranslateMoveGeometry(line);
        }

        [TestMethod]
        public void TestTranslatePolywithInnerRingsGeometry()
        {
            Vector2[] points = [ new(-10,-10),
                                                       new(-10,10),
                                                       new(10,10),
                                                       new(10,-10)];

            Vector2[] innerring = [ new(-5,-5),
                                                       new(-5,5),
                                                       new(5,5),
                                                       new(5,-5)];

            SqlGeometry line = Extensions.ToPolygon(points, [innerring]);
            TestTranslateMoveGeometry(line);
        }

        [TestMethod]
        public void TestTranslatePointGeometry()
        {
            Vector2 point = new(0, 0);
            SqlGeometry p = point.ToSqlGeometry();
            TestTranslateMoveGeometry(p);
        }

        [TestMethod]
        public void CurvePolygon_UsesStableCardinalSamples()
        {
            SqlGeometry circle = Extensions.ToCircle(10, 20, 0, 100);
            Vector2[] samples = circle.ToPoints();

            Assert.AreEqual(Extensions.CircleCardinalPointCount + 1, samples.Length);
            AssertPosition(samples[0], new Vector2(110, 20));
            AssertPosition(samples[0], samples[samples.Length - 1]);
        }

        public void TestTranslateMoveGeometry(SqlGeometry geometry)
        {
            Vector2 origin = geometry.Centroid();
            //AssertPosition(geometry.Centroid(), origin);

            Vector2 move_target = new(100, 100);
            SqlGeometry movedgeometry = Extensions.MoveTo(geometry, move_target);
            AssertPosition(movedgeometry.Centroid(), move_target);

            Vector2 move_offset = movedgeometry.Centroid() - origin;

            //Ensure we didn't lose the interior rings
            Assert.AreEqual(geometry.NumInteriorRings(), movedgeometry.NumInteriorRings());

            Vector2 translate_offset = new(50, 50);
            SqlGeometry translatedGeometry = Extensions.Translate(geometry, translate_offset);
            AssertPosition(translatedGeometry.Centroid() - origin, translate_offset);

            //Ensure we didn't lose the interior rings
            Assert.AreEqual(geometry.NumInteriorRings(), translatedGeometry.NumInteriorRings());

            //Check both results to ensure the interior rings actually moved too
            for (int iRing = 0; iRing < geometry.NumInteriorRings(); iRing++)
            {
                SqlGeometry originalRing = geometry.GetInteriorRing(iRing);
                SqlGeometry movedRing = movedgeometry.GetInteriorRing(iRing);
                SqlGeometry translatedRing = translatedGeometry.GetInteriorRing(iRing);

                AssertPosition(translatedRing.Centroid() - originalRing.Centroid(), translate_offset);
                AssertPosition(movedRing.Centroid() - originalRing.Centroid(), move_offset);
            }
        }

        /// <summary>
        /// A bowtie passes <see cref="Polygon"/> construction and fails SQL Server.
        /// Saving a pen cut must return a valid polygon instead of throwing.
        /// </summary>
        [TestMethod]
        public void ToSqlGeometry_SelfIntersectingRing_ReturnsValidPolygon()
        {
            Polygon bowtie = new(
            [
                new Vector2(0, 0),
                new Vector2(2, 2),
                new Vector2(2, 0),
                new Vector2(0, 2),
                new Vector2(0, 0)
            ]);

            SqlGeometry geom = bowtie.ToSqlGeometry();
            Assert.IsTrue(geom.STIsValid().IsTrue);
            Assert.AreEqual("Polygon", geom.STGeometryType().Value);
            Assert.IsTrue(geom.STArea().Value > 0);
        }
    }
}
