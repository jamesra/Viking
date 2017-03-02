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
            SqlServerTypes.Utilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);
        }

        private void AssertPosition(GridVector2 A, GridVector2 B)
        {
            Assert.IsTrue(GridVector2.Distance(A, B) <= .001);
        }

        [TestMethod]
        public void TestTranslateCircleGeometry()
        {
            SqlGeometry circle = GeometryExtensions.ToCircle(0, 0, 0, 100);
            TestTranslateMoveGeometry(circle);
        }

        [TestMethod]
        public void TestTranslateLineGeometry()
        {
            GridVector2[] points = new GridVector2[] { new GridVector2(-10,0),
                                                       new GridVector2(0,0),
                                                       new GridVector2(10,0)};
            SqlGeometry line = GeometryExtensions.ToPolyLine(points);
            TestTranslateMoveGeometry(line);
        }

        [TestMethod]
        public void TestTranslatePolyGeometry()
        {
            GridVector2[] points = new GridVector2[] { new GridVector2(-10,0),
                                                       new GridVector2(0,0),
                                                       new GridVector2(10,0)};
            SqlGeometry line = GeometryExtensions.ToPolyLine(points);
            TestTranslateMoveGeometry(line);
        }

        [TestMethod]
        public void TestTranslatePointGeometry()
        {
            GridVector2 point = new GridVector2(0,0);
            SqlGeometry p = point.ToGeometryPoint();
            TestTranslateMoveGeometry(p);
        }

        public void TestTranslateMoveGeometry(SqlGeometry geometry)
        {
            GridVector2 origin = new Geometry.GridVector2(0, 0); 
            AssertPosition(geometry.Centroid(), origin);

            GridVector2 move_offset = new GridVector2(100, 100);
            SqlGeometry movedgeometry = GeometryExtensions.MoveTo(geometry, move_offset);
            AssertPosition(movedgeometry.Centroid(), move_offset);

            GridVector2 translate_offset = new GridVector2(50, 50);
            SqlGeometry translatedGeometry = GeometryExtensions.Translate(movedgeometry, translate_offset);
            AssertPosition(translatedGeometry.Centroid(), move_offset + translate_offset);
        }
    }
}
