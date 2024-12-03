using Microsoft.VisualStudio.TestTools.UnitTesting;
using Geometry;
using System;

namespace GeometryTests
{
    [TestClass]
    public class ShapeIndexTests
    {
        [TestMethod]
        public void TestPolygonIndex()
        {
            PolygonIndex A1 = new PolygonIndex(1, 2, 3);
            PolygonIndex A2 = new PolygonIndex(1, 2, 3);

            Assert.AreEqual(A1, A2);
            Assert.IsTrue(A1 == A2);
            Assert.IsFalse(A1 != A2);
            Assert.IsTrue(A1 == (IShapeIndex)A2);
            Assert.IsTrue(((IShapeIndex)A1).Equals((IShapeIndex)A2));
            Assert.IsTrue(A1.Equals(A2));

            PolygonIndex B1 = new PolygonIndex(1, 2, 4);

            Assert.AreNotEqual(A1, B1);
            Assert.IsFalse(A1 == B1);
            Assert.IsTrue(A1 != B1);
            Assert.IsFalse(A1 == (IShapeIndex)B1);
            Assert.IsFalse(((IShapeIndex)A1).Equals((IShapeIndex)B1));
            Assert.IsFalse((IShapeIndex)A1 == (IShapeIndex)B1);
            Assert.IsFalse(A1.Equals(B1));
        }
    }
}
