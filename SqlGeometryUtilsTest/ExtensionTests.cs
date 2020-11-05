using System;
using Geometry;
using SqlGeometryUtils;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlGeometryUtilsTest
{
    [TestClass]
    public class SqlGeometryUtilsTest
    {
        static SqlGeometryUtilsTest()
        {
            //SqlServerTypes.Utilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);
        }

        private void AssertPosition(GridVector2 A, GridVector2 B)
        {
            Assert.IsTrue(GridVector2.Distance(A, B) <= .001);
        }

        [TestMethod]
        public void TestTranslateCircleGeometry()
        {
            SqlGeometry circle = Extensions.ToCircle(0, 0, 0, 100);
            TestTranslateMoveGeometry(circle);
        }

        [TestMethod]
        public void TestTranslateLineGeometry()
        {
            GridVector2[] points = new GridVector2[] { new GridVector2(-10,0),
                                                       new GridVector2(0,0),
                                                       new GridVector2(10,0)};
            SqlGeometry line = Extensions.ToSqlGeometry(points);
            TestTranslateMoveGeometry(line);
        }

        [TestMethod]
        public void TestTranslatePolyGeometry()
        {
            GridVector2[] points = new GridVector2[] { new GridVector2(-10,-10),
                                                       new GridVector2(-10,10),
                                                       new GridVector2(10,0)};
            SqlGeometry line = Extensions.ToPolygon(points);
            TestTranslateMoveGeometry(line);
        }

        [TestMethod]
        public void TestTranslatePolywithInnerRingsGeometry()
        {
            GridVector2[] points = new GridVector2[] { new GridVector2(-10,-10),
                                                       new GridVector2(-10,10),
                                                       new GridVector2(10,10),
                                                       new GridVector2(10,-10)};

            GridVector2[] innerring = new GridVector2[] { new GridVector2(-5,-5),
                                                       new GridVector2(-5,5),
                                                       new GridVector2(5,5),
                                                       new GridVector2(5,-5)};

            SqlGeometry line = Extensions.ToPolygon(points, new GridVector2[][] { innerring });
            TestTranslateMoveGeometry(line);
        }

        [TestMethod]
        public void TestTranslatePointGeometry()
        {
            GridVector2 point = new GridVector2(0,0);
            SqlGeometry p = point.ToSqlGeometry();
            TestTranslateMoveGeometry(p);
        }

        public void TestTranslateMoveGeometry(SqlGeometry geometry)
        {
            GridVector2 origin = geometry.Centroid();
            //AssertPosition(geometry.Centroid(), origin);

            GridVector2 move_target = new GridVector2(100, 100);
            SqlGeometry movedgeometry = Extensions.MoveTo(geometry, move_target);
            AssertPosition(movedgeometry.Centroid(), move_target);

            GridVector2 move_offset = movedgeometry.Centroid() - origin; 

            //Ensure we didn't lose the interior rings
            Assert.AreEqual(geometry.NumInteriorRings(), movedgeometry.NumInteriorRings());

            GridVector2 translate_offset = new GridVector2(50, 50);
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
