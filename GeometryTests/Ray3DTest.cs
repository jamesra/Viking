using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    [TestClass]
    public class Ray3DTest
    {
        static readonly Vector3 A = new(0, 0, 0);
        static readonly Vector3 B = new(10, 0, 0);
        static readonly Vector3 C = new(0, 10, 0);

        [TestMethod]
        public void RayHitsTriangleInterior()
        {
            Ray3D ray = new(new Vector3(1, 1, 5), new Vector3(0, 0, -1));

            Assert.IsTrue(RayIntersection.TryIntersectTriangle(ray, A, B, C, out double distance, out double u, out double v));
            Assert.AreEqual(5.0, distance, Tolerance.Epsilon);
            Assert.AreEqual(0.1, u, Tolerance.Epsilon);
            Assert.AreEqual(0.1, v, Tolerance.Epsilon);
        }

        [TestMethod]
        public void RayMissesOutsideTriangle()
        {
            Ray3D ray = new(new Vector3(9, 9, 5), new Vector3(0, 0, -1));

            Assert.IsFalse(RayIntersection.TryIntersectTriangle(ray, A, B, C, out _));
        }

        [TestMethod]
        public void TriangleBehindRayOriginMisses()
        {
            Ray3D ray = new(new Vector3(1, 1, 5), new Vector3(0, 0, 1));

            Assert.IsFalse(RayIntersection.TryIntersectTriangle(ray, A, B, C, out _));
        }

        [TestMethod]
        public void BackFaceHitsUnlessCulled()
        {
            Ray3D ray = new(new Vector3(1, 1, -5), new Vector3(0, 0, 1));

            Assert.IsTrue(RayIntersection.TryIntersectTriangle(ray, A, B, C, out double distance));
            Assert.AreEqual(5.0, distance, Tolerance.Epsilon);
            Assert.IsFalse(RayIntersection.TryIntersectTriangle(ray, A, B, C, out _, cullBackFaces: true));
        }

        [TestMethod]
        public void RayParallelToTrianglePlaneMisses()
        {
            Ray3D ray = new(new Vector3(-5, 1, 1), new Vector3(1, 0, 0));

            Assert.IsFalse(RayIntersection.TryIntersectTriangle(ray, A, B, C, out _));
        }

        [TestMethod]
        public void RayEntersBoxAtNearSlab()
        {
            Box box = new(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));
            Ray3D ray = new(new Vector3(-10, 0, 0), new Vector3(1, 0, 0));

            Assert.IsTrue(RayIntersection.TryIntersectBox(ray, box, out double distance));
            Assert.AreEqual(9.0, distance, Tolerance.Epsilon);
        }

        [TestMethod]
        public void RayOriginInsideBoxReportsZeroDistance()
        {
            Box box = new(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));
            Ray3D ray = new(Vector3.Zero, new Vector3(1, 1, 1));

            Assert.IsTrue(RayIntersection.TryIntersectBox(ray, box, out double distance));
            Assert.AreEqual(0.0, distance, Tolerance.Epsilon);
        }

        [TestMethod]
        public void RayPointingAwayFromBoxMisses()
        {
            Box box = new(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));
            Ray3D ray = new(new Vector3(-10, 0, 0), new Vector3(-1, 0, 0));

            Assert.IsFalse(RayIntersection.TryIntersectBox(ray, box, out _));
        }

        [TestMethod]
        public void RayParallelToBoxSlabsMissesWhenOutside()
        {
            Box box = new(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));
            Ray3D ray = new(new Vector3(-10, 5, 0), new Vector3(1, 0, 0));

            Assert.IsFalse(RayIntersection.TryIntersectBox(ray, box, out _));
        }
    }
}
