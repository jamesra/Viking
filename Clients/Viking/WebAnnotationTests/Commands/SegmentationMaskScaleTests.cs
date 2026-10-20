using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using WebAnnotation.UI.Commands.Segmentation;
using Rectangle = Geometry.Rectangle;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// The size and scale of a mask raster, the unit the dilate/erode editor works on: full resolution unless it must be coarser to fit
    /// (the GPU's texture), and then the shape at that resolution, neither grown nor shrunk.
    /// </summary>
    [TestClass]
    public class SegmentationMaskScaleTests
    {
        private static Rectangle Box(double x0, double y0, double x1, double y1) => new(new Vector2(x0, y0), new Vector2(x1, y1));

        private static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;

        [TestMethod]
        public void ARegionLayoutIsCeilWidthAndHeightOverScaleWhenTheEdgesAreClipped()
        {
            // 11 x 8 mosaic pixels at scale 4: the right edge block is 3 wide and is clipped to the bounds.
            MaskLayout layout = MaskLayout.ForRegion(0, 10, 0, 7, 4);

            Assert.AreEqual(11, layout.Width);
            Assert.AreEqual(8, layout.Height);
            Assert.AreEqual(3, layout.RasterWidth);
            Assert.AreEqual(2, layout.RasterHeight);
        }

        [TestMethod]
        public void ForRegionOnlyGrowsTheRegionAndLeavesTheFarEdgesAlone()
        {
            foreach (int scale in new[] { 1, 2, 4, 8 })
            {
                for (int x = -9; x <= 9; x += 3)
                {
                    for (int y = -9; y <= 9; y += 4)
                    {
                        MaskLayout layout = MaskLayout.ForRegion(x, x + 50, y, y + 40, scale);
                        Assert.IsTrue(layout.XMin <= x && layout.XMin > x - scale);
                        Assert.AreEqual(x + 50, layout.XMax, "the right edge is not snapped");
                        Assert.AreEqual(y, layout.YMin, "the bottom edge is not snapped");
                        Assert.IsTrue(layout.YMax >= y + 40 && layout.YMax < y + 40 + scale);
                        Assert.AreEqual(0, Mod(layout.XMin, scale));
                        Assert.AreEqual(0, Mod(layout.YMax + 1, scale));
                    }
                }
            }
        }

        [TestMethod]
        public void TheRasterIsFullResolutionUntilItWouldNotFitThenTheScaleDoubles()
        {
            Assert.AreEqual(1, MaskScalePolicy.Plan(0, 1023, 0, 1023).Scale);
            Assert.AreEqual(1, MaskScalePolicy.Plan(0, 3000, 0, 1500).Scale, "a polygon wider than one SAM window is still edited at full resolution");
            Assert.AreEqual(1, MaskScalePolicy.Plan(0, 4095, 0, 100).Scale);
            Assert.AreEqual(2, MaskScalePolicy.Plan(0, 5000, 0, 100).Scale);
            Assert.AreEqual(8, MaskScalePolicy.Plan(0, 20000, 0, 100).Scale, "20000 / 4 is over 4096");
            Assert.AreEqual(8, MaskScalePolicy.Plan(0, 12000, 0, 3000, maxRasterSide: 2048).Scale);
            Assert.AreEqual(2, MaskScalePolicy.Plan(0, 3000, 0, 1500, marginRasterPixels: 128, maxRasterSide: 2048).Scale);
        }

        [TestMethod]
        public void TheMarginIsRasterPixelsAtAnyScale()
        {
            MaskLayout small = MaskScalePolicy.Plan(0, 99, 0, 99, marginRasterPixels: 128);
            Assert.AreEqual(1, small.Scale);
            Assert.AreEqual(100 + 256, small.RasterWidth);

            MaskLayout coarse = MaskScalePolicy.Plan(0, 2999, 0, 1499, marginRasterPixels: 128, maxRasterSide: 1024);
            Assert.IsTrue(coarse.Scale >= 4);
            Assert.IsTrue(coarse.Width >= 3000 + 2 * 128 * coarse.Scale, "128 raster pixels of margin on each side at any scale");
            Assert.IsTrue(coarse.RasterWidth <= 1024 && coarse.RasterHeight <= 1024);
        }

        [TestMethod]
        public void ARasterAtACoarseScaleIsTheShapeAtThatResolutionNotAGrownCopy()
        {
            // 998 x 998 at scale 4: a block is set when its CENTRE is inside, so radius 0 is the polygon, not the polygon plus a block of edge.
            Vector2[] ring = [new(3, 3), new(1001, 3), new(1001, 1001), new(3, 1001)];

            RasterMask mask = RasterMask.FromRings([ring], downsample: 1, marginPixels: 0, maxRasterSide: 300)!;

            Assert.AreEqual(4, mask.Scale);
            int expected = 0;
            for (int row = 0; row < mask.Height; row++)
                for (int column = 0; column < mask.Width; column++)
                {
                    double x = mask.XMin + (column + 0.5) * mask.Scale;
                    double y = mask.YMax + 1 - (row + 0.5) * mask.Scale;
                    if (x >= 3 && x <= 1001 && y >= 3 && y <= 1001)
                        expected++;
                }

            Assert.AreEqual(expected, mask.Pixels.Count(p => p != 0));
            Assert.AreEqual(249 * 249, expected, "the arithmetic: block centres at 4k + 2 inside 3..1001");
        }

        [TestMethod]
        public void ARasterAtFullResolutionIsExactlyThePolygon()
        {
            Vector2[] ring = [new(1000, 2000), new(1800, 2000), new(1800, 2700), new(1000, 2700)];

            RasterMask mask = RasterMask.FromRings([ring], downsample: 1, marginPixels: 128)!;

            Assert.AreEqual(1, mask.Scale, "an 800 x 700 polygon is edited at one pixel per mosaic pixel");
            Assert.AreEqual(800 * 700, mask.Pixels.Count(p => p != 0));
            Assert.AreEqual(800 + 256, mask.Width);
        }

        [TestMethod]
        public void APolygonOfAnySizeIsRasterizedNeverRefusedForItsSize()
        {
            Vector2[] ring = [new(0, 0), new(20000, 0), new(20000, 100), new(0, 100)];

            RasterMask mask = RasterMask.FromRings([ring], downsample: 1, marginPixels: 128, maxRasterSide: 4096)!;

            Assert.IsNotNull(mask);
            Assert.IsTrue(mask.Width <= 4096 && mask.Height <= 4096, "the raster stays within what the caller can hold");
            Assert.IsTrue(mask.Scale > 1);
            Assert.IsTrue(mask.TryGetSetWorldBounds(out Rectangle set));
            Assert.AreEqual(0, set.Left, mask.Scale);
            Assert.AreEqual(20000, set.Right, mask.Scale, "nothing is cut off");
        }

        [TestMethod]
        public void AMaskTooBigForTheTextureIsDownsampledUntilItFits()
        {
            Vector2[] ring = [new(0, 0), new(12000, 0), new(12000, 3000), new(0, 3000)];

            RasterMask roomy = RasterMask.FromRings([ring], downsample: 1, marginPixels: 128, maxRasterSide: 4096)!;
            RasterMask tight = RasterMask.FromRings([ring], downsample: 1, marginPixels: 128, maxRasterSide: 2048)!;

            Assert.AreEqual(4, roomy.Scale);
            Assert.IsTrue(roomy.Width <= 4096 && roomy.Height <= 4096);
            Assert.AreEqual(8, tight.Scale);
            Assert.IsTrue(tight.Width <= 2048 && tight.Height <= 2048, $"{tight.Width} x {tight.Height} must fit a 2048 texture");
            Assert.IsTrue(tight.TryGetSetWorldBounds(out Rectangle set));
            Assert.AreEqual(0, set.Left, tight.Scale);
            Assert.AreEqual(12000, set.Right, tight.Scale, "downsampling loses no extent");
        }

        [TestMethod]
        public void TilesSpannedCountsTheGridTilesARectangleReaches()
        {
            Assert.AreEqual(6, SegmentationMaskTiles.TilesSpanned(Box(0, 0, 3000, 1500), 1), "3 columns by 2 rows");
            Assert.AreEqual(1, SegmentationMaskTiles.TilesSpanned(Box(100, 100, 900, 900), 1));
            Assert.AreEqual(4, SegmentationMaskTiles.TilesSpanned(Box(-10, -10, 10, 10), 1), "straddling the origin");
            Assert.AreEqual(2, SegmentationMaskTiles.TilesSpanned(Box(0, 0, 3000, 1500), 2), "at downsample 2 a tile is 2048 world units: 2 columns by 1 row");
            Assert.AreEqual(1, SegmentationMaskTiles.TilesSpanned(Box(0, 0, 3000, 1500), 4), "at downsample 4 a tile is 4096 world units wide");
            Assert.AreEqual(1, SegmentationMaskTiles.TilesSpanned(Box(0, 0, 1024, 1024), 1), "the right and top edges are exclusive");
            Assert.AreEqual(256, SegmentationMaskTiles.TilesSpanned(Box(0, 0, 16 * 1024, 16 * 1024), 1), "exactly 16 x 16 tiles, not 17 x 17");
            Assert.IsTrue(SegmentationMaskTiles.TilesSpanned(Box(0, 0, 17 * 1024, 16 * 1024), 1) > SegmentationMaskTiles.MaxForegroundTiles);
        }

        [TestMethod]
        public void ARasterIsTopDownSoRowZeroIsTheHighestY()
        {
            // An L: the lower-left 100 x 50 plus the upper-right 50 x 50. The upper-left corner of the bounds is empty.
            Vector2[] ring = [new(0, 0), new(100, 0), new(100, 100), new(50, 100), new(50, 50), new(0, 50)];

            RasterMask mask = RasterMask.FromRings([ring], downsample: 1)!;

            Assert.AreEqual(1, mask.Scale);
            Assert.AreEqual(100, mask.Width);
            Assert.AreEqual(100, mask.Height);
            Assert.AreEqual(99, mask.YMax, "row 0 is the highest mosaic row");
            Assert.AreEqual(0, mask.Pixels[0 * mask.Width + 0], "top left is empty");
            Assert.AreNotEqual(0, mask.Pixels[0 * mask.Width + 99], "top right is set");
            Assert.AreNotEqual(0, mask.Pixels[(mask.Height - 1) * mask.Width + 0], "bottom left is set");
            Assert.AreEqual(100 * 50 + 50 * 50, mask.Pixels.Count(p => p != 0));
        }

        [TestMethod]
        public void SeveralShapesAreOneRasterOverTheirCombinedBounds()
        {
            SegmentationMaskUnion union = new();
            union.Add(Box(0, 0, 200, 200));
            union.Add(Box(3000, 1500, 3200, 1700));

            byte[] raster = union.Rasterize(1, out MaskLayout layout)!;

            Assert.AreEqual(1, layout.Scale, "3200 x 1700 fits a 4096 raster at full resolution");
            Assert.IsTrue(layout.Width >= 3200 && layout.Height >= 1700);
            bool On(int x, int y) => raster[(layout.YMax - y) * layout.RasterWidth + (x - layout.XMin)] != 0;
            Assert.IsTrue(On(100, 100));
            Assert.IsTrue(On(3100, 1600));
            Assert.IsFalse(On(1600, 800), "the gap between the shapes stays off");
            Assert.AreEqual(200 * 200 + 200 * 200, raster.Count(p => p != 0));
        }

        [TestMethod]
        public void ARasterCutEntirelyAwayByBackgroundPointsIsNull()
        {
            SegmentationMaskUnion union = new();
            union.Add(Box(495, 495, 505, 505));

            Assert.IsNull(union.Rasterize(1, out _, holePointsVolume: [new Vector2(500, 500)], holeRadiusPixels: 20));
        }
    }
}
