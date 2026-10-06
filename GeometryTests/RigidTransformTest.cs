using Geometry;
using Geometry.Transforms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GeometryTests
{
    /// <summary>
    /// ITK Rigid2D semantics: <c>T(p) = R(angle) * (p - center) + center + offset</c>.
    /// </summary>
    [TestClass]
    public class RigidTransformTest
    {
        private static readonly Vector2 Offset = new(1000, -250);
        private static readonly Vector2 Center = new(50000, 40000);
        private const double Angle = Math.PI / 2;

        [TestMethod]
        public void ForwardRotatesAboutCenterThenTranslates()
        {
            RigidTransform t = new(Offset, Center, Angle, new TransformBasicInfo());
            Vector2 p = new(51000, 40000);

            Vector2 expected = new(50000 + 1000, 41000 - 250);
            Assert.IsTrue(Vector2.Distance(t.Transform(p), expected) < 1e-6, $"{t.Transform(p)} should be {expected}");
            Assert.IsTrue(Vector2.Distance(t.Transform([p])[0], expected) < 1e-6);
        }

        [TestMethod]
        public void InverseUndoesForward()
        {
            RigidTransform t = new(Offset, Center, 0.3, new TransformBasicInfo());
            Random random = new(1);
            for (int i = 0; i < 100; i++)
            {
                Vector2 p = new(random.NextDouble() * 500000, random.NextDouble() * 500000);
                Assert.IsTrue(Vector2.Distance(t.InverseTransform(t.Transform(p)), p) < 1e-6);
                Assert.IsTrue(Vector2.Distance(t.InverseTransform([t.Transform(p)])[0], p) < 1e-6);
            }
        }

        [TestMethod]
        public void ItkTextKeepsTheRotation()
        {
            RigidTransform t = new(Offset, Center, Angle, new TransformBasicInfo());
            StringAssert.StartsWith(t.GetITKTransform(), $"Rigid2DTransform_double_2_2 vp 3 {Angle} ");
        }
    }
}
