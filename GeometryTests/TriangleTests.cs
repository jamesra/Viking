using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    [TestClass]
    public class TriangleTests
    {
        [TestMethod]
        public void TestBarycentric()
        {
            Vector2[] v = [new(5,5),
                                                 new(5,10),
                                                 new(10,5)];

            Triangle tri = new(v);

            Vector2 center = new(7.5, 7.5);
            Vector2 bary = tri.Barycentric(center);
            Assert.AreEqual(bary.X, bary.Y);
            Vector2 remapped = tri.BaryToVector(bary);

            Assert.AreEqual(center, remapped);

        }
    }
}
