using System;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryOGCMapperTest
{
    [TestClass]
    public class TestFromWKT
    {
        static readonly string[] BadPoints =
        [
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
        ];

        static readonly string[] GoodPoints =
        [
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
        ];

        static readonly string[] BadCoordLists =
        [
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
        ];

        static readonly string[] GoodCoordLists =
        [
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
        ];

        static readonly string[] BadParenLists =
        [
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
        ];

        [TestMethod]
        public void TestReadPoint()
        {
            string wkt = "Point (30 10)";
            Vector2 expected = new(30, 10);
            var result = FromWKT.ParseWKT(wkt);
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

        public static void TestReadBadWkt(string bad_wkt)
        {
            try
            {
                var result = FromWKT.ParseWKT(bad_wkt);
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

        public static void TestReadGoodWkt(string good_wkt)
        {
            try
            {
                var result = FromWKT.ParseWKT(good_wkt);
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

        public static void TestReadBadCoordList(string bad_wkt)
        {
            try
            {
                var result = FromWKT.ParsePointsFromParameters(bad_wkt);
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

        public static void TestReadGoodCoordList(string bad_wkt)
        {
            try
            {
                var result = FromWKT.ParsePointsFromParameters(bad_wkt);
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

        public static void TestReadBadParenList(string bad_wkt)
        {
            try
            {
                var result = FromWKT.ParseParenListFromParameters(bad_wkt);
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
            Vector2 expected = new(30, 10);
            var result = FromWKT.ParseWKT(wkt);
            Assert.IsTrue(result.Equals(expected));
        }


        [TestMethod]
        public void TestWKTReadLineString()
        {
            string wkt = "LINESTRING (30 10, 10 30, 40 40)";
            Polyline expected = new([new(30, 10), new(10, 30), new(40, 40)]);

            var result = FromWKT.ParseWKT(wkt);
            Assert.IsTrue(result.Equals(expected));
        }

        [TestMethod]
        public void TestWKTReadMultiLineString()
        {
            string wkt = "MULTILINESTRING ((30 10, 10 30, 40 40), (-5 3, -8 -2))";
            Polyline A = new([new(30, 10), new(10, 30), new(40, 40)]);

            Polyline B = new([new(-5, 3), new(-8, -2)]);

            Shape2DCollection expected = new(2);
            expected.Add(A);
            expected.Add(B);

            var result = FromWKT.ParseWKT(wkt);
            Assert.IsTrue(result.Equals(expected));
        }

        [TestMethod]
        public void TestWKTReadSimplePolygon()
        {
            string wkt = "POLYGON ((30 10, 40 40, 20 40, 10 20, 30 10))";
            Polygon expected = new(
            [
                new(30, 10),
                new(40, 40),
                new(20, 40),
                new(10, 20),
                new(30, 10)
            ]);
            var result = FromWKT.ParseWKT(wkt);
            Assert.IsTrue(expected.Equals(result));
        }

        [TestMethod]
        public void TestWKTReadPolygonWithInteriorHole()
        {
            string wkt = @"POLYGON ((35 10, 45 45, 15 40, 10 20, 35 10),
                (20 30, 35 35, 30 20, 20 30))";
            Polygon expected = new(
            [
                new(35, 10),
                new(45, 45),
                new(15, 40),
                new(10, 20),
                new(35, 10)
            ]);

            Vector2[] innerPoly =
            [
                new(20, 30),
                new(35, 35),
                new(30, 20),
                new(20, 30)
            ];

            expected.AddInteriorRing(innerPoly);

            var result = FromWKT.ParseWKT(wkt);
            Assert.IsTrue(expected.Equals(result));
        }

        [TestMethod]
        public void TestWKTReadCurvePolygon()
        {
            string wkt = @"CURVEPOLYGON ((-1 0, 0 1, 1 0, 0 -1, -1 0))";
            Circle expected = new(new Vector2(0, 0), 1);

            var result = FromWKT.ParseWKT(wkt);
            Assert.IsTrue(expected.Equals(result));
        }
    }
}
