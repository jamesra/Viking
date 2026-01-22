using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    [TestClass]
    public class GridTriangleTests
    {
        [TestMethod]
        public void TestBarycentric()
        {
            GridVector2[] v = [new(5,5),
                                                 new(5,10),
                                                 new(10,5)];

            GridTriangle tri = new(v);

            GridVector2 center = new(7.5, 7.5);
            GridVector2 bary = tri.Barycentric(center);
            Assert.AreEqual(bary.X, bary.Y);
            GridVector2 remapped = tri.BaryToVector(bary);

            Assert.AreEqual(center, remapped);

        }
    }
}
