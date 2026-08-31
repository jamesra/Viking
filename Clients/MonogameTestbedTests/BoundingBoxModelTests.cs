using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using VikingXNAGraphics;
using Rectangle = Geometry.Rectangle;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace MonogameTestbedTests
{
    /// <summary>
    /// Covers the wireframe box models used by the slice-assembly overlay.
    /// </summary>
    [TestClass]
    public class BoundingBoxModelTests
    {
        /// <summary>
        /// A 100 x 200 footprint in XY, centered on Z=50, with the requested Z thickness.
        /// </summary>
        private static Box BoxWithDepth(double depth) =>
            new(new Rectangle(left: 0, right: 100, bottom: 0, top: 200), 50 - (depth / 2), 50 + (depth / 2));

        [TestMethod]
        public void NormalBoxScalesToHalfExtents()
        {
            var model = BoxWithDepth(10).ToMeshModelEdgesOnly(Color.White);

            model.ModelMatrix.Decompose(out Vector3 scale, out _, out Vector3 translation);

            //The unit box spans -1..1, so the scale is the half extent on each axis.
            Assert.AreEqual(50f, scale.X, 0.001f);
            Assert.AreEqual(100f, scale.Y, 0.001f);
            Assert.AreEqual(5f, scale.Z, 0.001f);
            Assert.AreEqual(50f, translation.Z, 0.001f);
        }

        [TestMethod]
        public void SingleZSliceStillHasNonZeroDepth()
        {
            //A slice whose shapes all sit on one Z used to scale the unit box by zero, collapsing it to a plane.
            var model = BoxWithDepth(0).ToMeshModelEdgesOnly(Color.White);

            model.ModelMatrix.Decompose(out Vector3 scale, out _, out _);

            Assert.IsTrue(scale.Z > 0, $"Z scale should be positive but was {scale.Z}.");
            Assert.AreEqual(50f, scale.X, 0.001f, "Clamping Z must not disturb the other axes.");
            Assert.AreEqual(100f, scale.Y, 0.001f);
        }

        [TestMethod]
        public void SingleZSliceKeepsBoxCentered()
        {
            var model = BoxWithDepth(0).ToMeshModelEdgesOnly(Color.White);

            model.ModelMatrix.Decompose(out _, out _, out Vector3 translation);

            Assert.AreEqual(50f, translation.Z, 0.001f, "The thin box should straddle the slice's Z.");
        }

        [TestMethod]
        public void SetColorRecolorsEveryVertex()
        {
            var model = BoxWithDepth(10).ToMeshModelEdgesOnly(Color.LightGray);
            Assert.IsTrue(model.Vertices.Length > 0);

            model.SetColor(Color.Red);

            foreach (var vertex in model.Vertices)
                Assert.AreEqual(Color.Red, vertex.Color, "A failed node's box must recolor completely.");
        }
    }
}
