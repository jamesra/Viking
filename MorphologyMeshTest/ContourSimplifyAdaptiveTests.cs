using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;

namespace MorphologyMeshTest
{
    [TestClass]
    public class ContourSimplifyAdaptiveTests
    {
        [TestMethod]
        public void SpacingForHullRatio_EndpointsAndMidpoint()
        {
            Assert.AreEqual(50.0, ContourSimplifyOptions.SpacingForHullRatio(0.95), 1e-9);
            Assert.AreEqual(50.0, ContourSimplifyOptions.SpacingForHullRatio(1.0), 1e-9);
            Assert.AreEqual(20.0, ContourSimplifyOptions.SpacingForHullRatio(0.70), 1e-9);
            Assert.AreEqual(20.0, ContourSimplifyOptions.SpacingForHullRatio(0.50), 1e-9);

            // Midway between 0.70 and 0.95 → halfway between 20 and 50
            double midRatio = 0.70 + (0.5 * (0.95 - 0.70));
            Assert.AreEqual(35.0, ContourSimplifyOptions.SpacingForHullRatio(midRatio), 1e-6);
        }

        [TestMethod]
        public void HullAreaRatio_SquareIsNearlyOne()
        {
            Vector2[] square =
            [
                new(0, 0), new(100, 0), new(100, 100), new(0, 100), new(0, 0)
            ];
            Assert.AreEqual(1.0, ContourSimplifyOptions.HullAreaRatio(square), 1e-6);
            Assert.AreEqual(50.0, ContourSimplifyOptions.Default.SpacingForRing(square), 1e-6);
        }

        [TestMethod]
        public void HullAreaRatio_CShapeIsBelowHighThreshold()
        {
            // Open C: outer box with a deep bite removed from the right side (as a simple concave ring).
            Vector2[] cShape =
            [
                new(0, 0),
                new(100, 0),
                new(100, 20),
                new(30, 20),
                new(30, 80),
                new(100, 80),
                new(100, 100),
                new(0, 100),
                new(0, 0)
            ];
            double ratio = ContourSimplifyOptions.HullAreaRatio(cShape);
            Assert.IsTrue(ratio < ContourSimplifyOptions.AdaptiveHighHullRatio,
                $"Expected concave C-shape ratio < 0.95, got {ratio}");
            double spacing = ContourSimplifyOptions.Default.SpacingForRing(cShape);
            Assert.IsTrue(spacing < 50.0, $"Expected spacing < 50 for concave ring, got {spacing}");
            Assert.IsTrue(spacing >= ContourSimplifyOptions.AdaptiveMinSpacingNm);
        }

        [TestMethod]
        public void Default_ToleranceIsTenAndAdaptive()
        {
            Assert.AreEqual(10.0, ContourSimplifyOptions.Default.ToleranceNm);
            Assert.IsTrue(ContourSimplifyOptions.Default.AdaptiveHullSpacing);
            Assert.AreEqual(50.0, ContourSimplifyOptions.Default.MinNmPerVertex);
        }
    }
}
