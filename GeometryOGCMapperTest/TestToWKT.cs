using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text; 

namespace GeometryOGCMapperTest
{
    [TestClass]
    public class TestToWKT
    {
        [TestMethod]
        public void TestEncodePoint()
        {
            GridVector2 expected = new GridVector2(10, 20);
            string wkt = expected.ToWKT();
            var result = wkt.ParseWKT();
            Assert.IsTrue(result.Equals(expected));
        }

        [TestMethod]
        public void TestEncodeLineSegment()
        {
            GridLineSegment expected = new GridLineSegment(new GridVector2(10,20), 
                                                           new GridVector2(20, 30));
            string wkt = expected.ToWKT();
            var result = wkt.ParseWKT();
            Assert.IsTrue(result.Equals(expected));
        }

        [TestMethod]
        public void TestEncodePolyline()
        {
            GridPolyline expected = new GridPolyline(new GridVector2[]
                {
                    new GridVector2(10, 20),
                    new GridVector2(20, 30),
                    new GridVector2(40, 50)
                }
            );

            string wkt = expected.ToWKT();
            var result = wkt.ParseWKT();
            Assert.IsTrue(result.Equals(expected));
        }

        [TestMethod]
        public void TestEncodePolygon()
        {
            GridPolygon expected = new GridPolygon(new GridVector2[]
            {
                new GridVector2(35, 10),
                new GridVector2(45, 45),
                new GridVector2(15, 40),
                new GridVector2(10, 20),
                new GridVector2(35, 10)
            });

            GridVector2[] innerPoly = new GridVector2[]
            {
                new GridVector2(20, 30),
                new GridVector2(35, 35),
                new GridVector2(30, 20),
                new GridVector2(20, 30)
            };

            expected.AddInteriorRing(innerPoly);

            string wkt = expected.ToWKT();
            var result = wkt.ParseWKT();
            Assert.IsTrue(result.Equals(expected));
        }

        [TestMethod]
        public void TestEncodeCircle()
        {
            GridCircle expected = new GridCircle(5, -2, 13);

            string wkt = expected.ToWKT();
            var result = wkt.ParseWKT();
            Assert.IsTrue(result.Equals(expected));
        }
    }
}
