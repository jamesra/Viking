using FsCheck;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MIConvexHull;
using MIConvexHullExtensions;

namespace GeometryTests
{
    /// <summary>
    /// Pins MIConvexHull <see cref="IVertex.Position"/> wiring after GridVector removal.
    /// </summary>
    [TestClass]
    public class MIVectorVertexTests
    {
        [TestMethod]
        public void MIVector2_Position_MatchesXY()
        {
            var v = new MIVector2(new Vector2(12.5, -3.25), new PolygonIndex(0, 1, 4));
            double[] pos = ((IVertex)v).Position;
            Assert.AreEqual(2, pos.Length);
            Assert.AreEqual(12.5, pos[0], 1e-12);
            Assert.AreEqual(-3.25, pos[1], 1e-12);
        }

        [TestMethod]
        public void MIVector3_Position_MatchesCoords()
        {
            var v = new MIVector3(new Vector3(1, 2, 3), new PolygonIndex(0, 1, 4));
            double[] pos = ((IVertex)v).Position;
            CollectionAssert.AreEqual(new[] { 1.0, 2.0, 3.0 }, pos);
        }

        [TestMethod]
        public void MIVector2_Position_RoundTripsGeneratedXY()
        {
            Prop.ForAll<double, double>((x, y) =>
            {
                if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y))
                    return true;

                var v = new MIVector2(new Vector2(x, y), new PolygonIndex(0, 0, 1));
                double[] pos = ((IVertex)v).Position;
                return pos.Length == 2 && pos[0] == x && pos[1] == y;
            }).QuickCheckThrowOnFailure();
        }
    }
}
