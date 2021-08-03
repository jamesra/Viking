using System;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryOGCMapperTest
{
    [TestClass]
    public class TestWKT
    {
        static readonly string[] BadPoints = new string[]
        {
            null,
            "",
            "P",
            "Point",
            "Point (",
            "Point )",
            "Point 10)",
            "Point (10)",
            "Point (10",
            "Point (30 10",
            "Point (30, 10",
            "Point (30, 10)",
            "Point 30, 10)",
            "Point 30 10",
            "Point ()",
            "Point (30 10 1)"
        };

        static readonly string[] GoodPoints = new string[]
        {
            "Point(10 10)",
            "Point (10 10)",
            "Point  (10 10)",
            "Point(10 10) ",
            "Point ( 10 10)",
            "Point (10  10)",
            "Point (10 10 )",
            "Point (1 1)",
            "Point (1.0 1.0)",
            "Point(-10 -10)",
            "Point (-10 -10)",
            "Point  (-10 -10)",
            "Point(-10 -10) ",
            "Point ( -10 -10)",
            "Point (-10  -10)",
            "Point (-10 -10 )",
            "Point (-1 -1)",
            "Point (-1.0 -1.0)",
        };

        static readonly string[] BadCoordLists = new string[]
        {
            null,
            "",
            "P",
            "Point",
            "(",
            ")",
            "10",
            "10, ",
            "10 20, 10",
            "10, 20 30",
            "10 20, 30",
            "()",
            "10 20 30",
            "10 20, 30 40, 50"
        };

        static readonly string[] GoodCoordLists = new string[]
        {
            "10 20",
            "10 20, 30 40",
            "10 20, 30 40, 50 60",
            "10 20, 30 40, 50 60, 70 80",
            "10 20, 30 40, 50 60, 70 80",
            "10 20 , 30 40 , 50 60 , 70 80",
            " 10  20, 30 40 ,50 60,70 80",
            "-10 -20",
            "-10 -20, -30 -40",
            "-10 -20, -30 -40, -50 -60",
            "-10 -20, -30 -40, -50 -60, -70 -80",
            "-10 -20, -30 -40, -50 -60, -70 -80",
            "-10 -20 , -30 -40 , -50 -60 , -70 -80",
            " -10  -20, -30 -40 ,-50 -60,-70 -80",
        };

        static readonly string[] BadParenLists = new string[]
        {
            null,
            "",
            "Point",
            "(",
            ")",
            ",",
            "),",
            "10)",
            "(10, ",
            "(10 20), 10",
            "(10 10), (20 30",
            "10 20, (20 30)",
            "() ()",
            "(),(),() ()",
        };

        [TestMethod]
        public void TestReadPoint()
        {
            string wkt = "Point (30 10)";
            GridVector2 expected = new GridVector2 {X = 30, Y = 10};
            var result = WKT.ParseWKT(wkt);
            Assert.IsTrue(result.Equals(expected));
        }

        [TestMethod]
        public void TestReadBadPoints()
        {
            foreach (var bad_wkt in BadPoints)
            {
                TestReadBadWkt(bad_wkt);
            }
        }
        
        public void TestReadBadWkt(string bad_wkt)
        {
            try
            {
                var result = WKT.ParseWKT(bad_wkt);
                Assert.Fail($"Should not be able to parse '{bad_wkt}'");
            }
            catch (FormatException)
            {
            }
        }

        [TestMethod]
        public void TestReadGoodPoints()
        {
            foreach (var good_wkt in GoodPoints)
            {
                TestReadGoodWkt(good_wkt);
            }
        }

        public void TestReadGoodWkt(string good_wkt)
        {
            try
            {
                var result = WKT.ParseWKT(good_wkt);
            }
            catch (FormatException)
            {
                Assert.Fail($"Should be able to parse '{good_wkt}'");
            }
        }

        [TestMethod]
        public void TestReadBadCoordLists()
        {
            foreach (var bad_wkt in BadCoordLists)
            {
                TestReadBadWkt(bad_wkt);
            }
        }

        public void TestReadBadCoordList(string bad_wkt)
        {
            try
            {
                var result = WKT.ParsePointsFromParameters(bad_wkt);
                Assert.Fail($"Should not be able to parse '{bad_wkt}'");
            }
            catch (FormatException)
            {
            }
        }

        [TestMethod]
        public void TestReadGoodCoordLists()
        {
            foreach (var good_wkt in GoodCoordLists)
            {
                TestReadGoodCoordList(good_wkt);
            }
        }

        public void TestReadGoodCoordList(string bad_wkt)
        {
            try
            {
                var result = WKT.ParsePointsFromParameters(bad_wkt);
            }
            catch (FormatException)
            {
                Assert.Fail($"Should be able to parse '{bad_wkt}'");
            }
        }

        [TestMethod]
        public void TestReadBadParenLists()
        {
            foreach (var bad_wkt in BadParenLists)
            {
                TestReadBadWkt(bad_wkt);
            }
        }

        public void TestReadBadParenList(string bad_wkt)
        {
            try
            {
                var result = WKT.ParseParenListFromParameters(bad_wkt);
                Assert.Fail($"Should not be able to parse '{bad_wkt}'");
            }
            catch (FormatException)
            {
            }
        }


        [TestMethod]
        public void TestWKTReadPoint()
        {
            string wkt = "Point (30 10)";
            GridVector2 expected = new GridVector2 { X = 30, Y = 10 };
            var result = WKT.ParseWKT(wkt);
            Assert.IsTrue(result.Equals(expected));
        }

        
        [TestMethod]
        public void TestWKTReadLineString()
        {
            string wkt = "LINESTRING (30 10, 10 30, 40 40)";
            GridPolyline expected = new GridPolyline(new GridVector2[]
            {
                new GridVector2(30, 10),
                new GridVector2(10, 30),
                new GridVector2(40, 40)
            });

            var result = WKT.ParseWKT(wkt);
            Assert.IsTrue(result.Equals(expected));
        }
        
        [TestMethod]
        public void TestWKTReadSimplePolygon()
        {
            string wkt = "POLYGON ((30 10, 40 40, 20 40, 10 20, 30 10))";
            GridPolygon expected = new GridPolygon(new GridVector2[]
            {
                new GridVector2(30, 10),
                new GridVector2(40, 40),
                new GridVector2(20, 40),
                new GridVector2(10, 20),
                new GridVector2(30, 10)
            });
            var result = WKT.ParseWKT(wkt);
            Assert.IsTrue(expected.Equals(result));
        }

        [TestMethod]
        public void TestWKTReadPolygonWithInteriorHole()
        {
            string wkt = @"POLYGON ((35 10, 45 45, 15 40, 10 20, 35 10),
                (20 30, 35 35, 30 20, 20 30))";
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

            var result = WKT.ParseWKT(wkt);
            Assert.IsTrue(expected.Equals(result));
        }

        [TestMethod]
        public void TestWKTReadCurvePolygon()
        {
            string wkt = @"CURVEPOLYGON ((-1 0, 0 1, 1 0, 0 -1, -1 0))";
            var expected = new GridCircle(new GridVector2(0, 0), 1);
            
            var result = WKT.ParseWKT(wkt);
            Assert.IsTrue(expected.Equals(result));
        }
    }
}
