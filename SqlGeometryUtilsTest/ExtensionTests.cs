using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;

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
    }
}
