using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;

namespace GeometryOGCMapperTest
{
    /// <summary>
    /// WKT text must be identical and round-trip under a comma-decimal current culture,
    /// because SQL Server only accepts '.' as the decimal separator.
    /// </summary>
    [TestClass]
    public class TestWktCulture
    {
        /// <summary>Runs <paramref name="body"/> with the current culture set to de-DE, then restores it.</summary>
        private static void UnderGerman(System.Action body)
        {
            CultureInfo saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                body();
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [TestMethod]
        public void PointWktUsesDecimalPointAndRoundTrips()
        {
            Vector2 point = new(10.5, -20.25);
            string invariant = point.ToWKT();
            UnderGerman(() =>
            {
                string wkt = point.ToWKT();
                Assert.AreEqual(invariant, wkt);
                Assert.IsFalse(wkt.Contains(","));
                Assert.IsTrue(wkt.ParseWKT().Equals(point));
            });
        }

        [TestMethod]
        public void CircleAndPolygonWktUseDecimalPoint()
        {
            Circle circle = new(new Vector2(100.5, 200.25), 12.75);
            Polygon poly = new([new(0.5, 0.5), new(10.5, 0.5), new(10.5, 10.5), new(0.5, 10.5), new(0.5, 0.5)]);
            string circleInvariant = circle.ToWKT();
            string polyInvariant = poly.ToWKT();
            UnderGerman(() =>
            {
                Assert.AreEqual(circleInvariant, circle.ToWKT());
                Assert.AreEqual(polyInvariant, poly.ToWKT());
                Assert.IsTrue(circle.ToWKT().ParseWKT().Equals(circle));
            });
        }
    }
}
