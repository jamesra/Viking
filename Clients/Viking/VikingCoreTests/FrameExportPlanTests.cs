using System;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="FrameExportPlan"/>: frames too tall (not only too wide) are split, and tile centers form a grid
    /// centered on the frame instead of starting at its center and running off to one side.
    /// </summary>
    [TestClass]
    public class FrameExportPlanTests
    {
        [TestMethod]
        public void SmallFrame_IsOneTileCenteredOnFrame()
        {
            FrameExportPlan plan = new(1000.7, 2000.2, 1024, 512, 1.0, 4096);

            Assert.AreEqual(1, plan.NumTilesX);
            Assert.AreEqual(1, plan.NumTilesY);
            Assert.AreEqual(1024, plan.CapturedTileSizeX);
            Assert.AreEqual(512, plan.CapturedTileSizeY);
            Assert.AreEqual((1000.0, 2000.0), plan.TileCenter(0, 0));
        }

        [TestMethod]
        public void TallNarrowFrame_IsSplitVertically()
        {
            // Width fits in a single pass but height (5000 px) does not; the old check compared X twice and rendered it as one tile.
            FrameExportPlan plan = new(0, 0, 1000, 5000, 1.0, 4096);

            Assert.AreEqual(1, plan.NumTilesX);
            Assert.AreEqual(2, plan.NumTilesY);
            Assert.IsTrue(plan.CapturedTileSizeY <= 4096);
        }

        [TestMethod]
        public void TwoByTwoGrid_TileCentersStraddleFrameCenter()
        {
            FrameExportPlan plan = new(10000, 20000, 6000, 6000, 1.0, 4096);

            Assert.AreEqual(2, plan.NumTilesX);
            Assert.AreEqual(2, plan.NumTilesY);
            Assert.AreEqual(3000, plan.WorldTileSizeX);
            Assert.AreEqual((8500.0, 18500.0), plan.TileCenter(0, 0));
            Assert.AreEqual((11500.0, 21500.0), plan.TileCenter(1, 1));
        }

        [TestMethod]
        public void Grid_IsSymmetricAboutFrameCenterAndWithinCaptureLimit()
        {
            var sizeGen = Gen.Choose(1, 40_000);
            var centerGen = Gen.Choose(-500_000, 500_000);
            var downsampleGen = Gen.Elements(0.25, 0.5, 1.0, 2.0, 4.0, 8.0);
            var gen = from cx in centerGen
                      from cy in centerGen
                      from w in sizeGen
                      from h in sizeGen
                      from ds in downsampleGen
                      select (cx, cy, w, h, ds);

            Prop.ForAll(Arb.From(gen), t =>
            {
                FrameExportPlan plan = new(t.cx, t.cy, t.w, t.h, t.ds, 4096);

                Assert.IsTrue(plan.CapturedTileSizeX <= Math.Max(4096, FrameExportPlan.SingleTileMaxPixels));
                Assert.IsTrue(plan.CapturedTileSizeY <= Math.Max(4096, FrameExportPlan.SingleTileMaxPixels));

                (double firstX, double firstY) = plan.TileCenter(0, 0);
                (double lastX, double lastY) = plan.TileCenter(plan.NumTilesX - 1, plan.NumTilesY - 1);
                Assert.AreEqual(2.0 * t.cx, firstX + lastX, 1e-6);
                Assert.AreEqual(2.0 * t.cy, firstY + lastY, 1e-6);
            }).QuickCheckThrowOnFailure();
        }
    }
}
