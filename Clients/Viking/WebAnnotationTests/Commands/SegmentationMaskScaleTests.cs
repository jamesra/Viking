using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Linq;
using WebAnnotation.UI.Commands.Segmentation;
using Rectangle = Geometry.Rectangle;
using WireMask = Viking.gRPC.SegmentationServiceTypes.V1.Mask;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Masks larger than one SAM2 window: the bounds, scale and PNG the server is sent, and that snapping the bounds to the
    /// scale never loses any of the shape. The expansion in <see cref="Expand"/> is the server's rule: PNG pixel (c, r)
    /// stands for the scale x scale block whose low-x, high-y corner is (x_min + c * scale, y_max - r * scale), clipped to the bounds.
    /// </summary>
    [TestClass]
    public class SegmentationMaskScaleTests
    {
        private static readonly Vector2 Centre = new(0, 0);

        private static Rectangle Box(double x0, double y0, double x1, double y1) => new(new Vector2(x0, y0), new Vector2(x1, y1));

        private static WireMask MaskOf(Rectangle rectangle, int downsample = 1)
        {
            SegmentationMaskUnion union = new();
            union.Add(rectangle);
            return union.ToMask(downsample, rectangle.Center)!;
        }

        private static int Scale(WireMask mask) => mask.HasScale ? (int)mask.Scale : 1;

        private static int Width(WireMask mask) => mask.Bounds.XMax - mask.Bounds.XMin + 1;

        private static int Height(WireMask mask) => mask.Bounds.YMax - mask.Bounds.YMin + 1;

        /// <summary>The mosaic pixels of <paramref name="mask"/> (row-major from y_max down) after the server's block expansion.</summary>
        private static bool[] Expand(WireMask mask)
        {
            using Image<L8> png = Image.Load<L8>(mask.Png.ToByteArray());
            int scale = Scale(mask), width = Width(mask), height = Height(mask);
            bool[] expanded = new bool[width * height];
            for (int row = 0; row < height; row++)
                for (int column = 0; column < width; column++)
                    expanded[row * width + column] = png[column / scale, row / scale].PackedValue != 0;

            return expanded;
        }

        private static bool On(bool[] expanded, WireMask mask, int x, int y)
            => x >= mask.Bounds.XMin && x <= mask.Bounds.XMax && y >= mask.Bounds.YMin && y <= mask.Bounds.YMax &&
               expanded[(mask.Bounds.YMax - y) * Width(mask) + (x - mask.Bounds.XMin)];

        /// <summary>True when the centre of mosaic pixel (x, y) is inside the rectangle, the rule the encoder fills by.</summary>
        private static bool Inside(Rectangle rectangle, int x, int y)
            => x + 0.5 >= rectangle.Left && x + 0.5 <= rectangle.Right && y + 0.5 >= rectangle.Bottom && y + 0.5 <= rectangle.Top;

        private static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;

        [TestMethod]
        public void ARegionWithinOneWindowIsSentAtFullResolution()
        {
            WireMask mask = MaskOf(Box(100, 200, 600, 500));
            using Image<L8> png = Image.Load<L8>(mask.Png.ToByteArray());

            Assert.IsFalse(mask.HasScale, "Scale is unset at full resolution");
            Assert.AreEqual(Width(mask), png.Width);
            Assert.AreEqual(Height(mask), png.Height);
            Assert.AreEqual(100, mask.Bounds.XMin);
            Assert.AreEqual(599, mask.Bounds.XMax);
            Assert.AreEqual(200, mask.Bounds.YMin);
            Assert.AreEqual(499, mask.Bounds.YMax);
            Assert.AreEqual(255, png[0, 0].PackedValue, "row 0 is y_max");
            Assert.AreEqual(255, png[png.Width - 1, png.Height - 1].PackedValue, "and the last row is y_min");
            Assert.AreEqual(1, png.Frames.Count);
            Assert.AreEqual(1, Image.Identify(mask.Png.ToByteArray()).PixelType.BitsPerPixel);
        }

        [TestMethod]
        public void ARegionExactlyOneWindowWideStaysAtFullResolution()
        {
            WireMask mask = MaskOf(Box(0, 0, 1023.5, 1023.5));

            Assert.IsFalse(mask.HasScale);
            Assert.AreEqual(1024, Width(mask));
        }

        [TestMethod]
        public void ARegionBeyondOneWindowIsSentAtScaleFourWithTheCeilingPngSize()
        {
            WireMask mask = MaskOf(Box(0, 0, 3000, 1500));
            using Image<L8> png = Image.Load<L8>(mask.Png.ToByteArray());

            Assert.IsTrue(mask.HasScale);
            Assert.AreEqual(MaskScalePolicy.LargeRegionScale, (int)mask.Scale);
            Assert.AreEqual((Width(mask) + 3) / 4, png.Width);
            Assert.AreEqual((Height(mask) + 3) / 4, png.Height);
            Assert.IsTrue(Width(mask) >= 3000 && Height(mask) >= 1500, "the bounds cover the region");
            Assert.AreEqual(1, Image.Identify(mask.Png.ToByteArray()).PixelType.BitsPerPixel);
        }

        [TestMethod]
        public void TheBoundsAreInclusiveAndPngRowZeroIsYMax()
        {
            WireMask mask = MaskOf(Box(0, 0, 3000, 1500));
            bool[] expanded = Expand(mask);

            Assert.AreEqual(Width(mask) * Height(mask), expanded.Length);
            Assert.IsTrue(On(expanded, mask, mask.Bounds.XMin + 10, 1499), "just under the region's top edge");
            Assert.IsFalse(On(expanded, mask, mask.Bounds.XMin + 10, 1502), "above the region's top edge");
            Assert.IsTrue(On(expanded, mask, 10, 10));
            Assert.IsFalse(On(expanded, mask, 10, -10));
        }

        [TestMethod]
        public void ThePngIsExactlyCeilWidthAndHeightOverScaleWhenTheEdgesAreClipped()
        {
            // 11 x 8 mosaic pixels at scale 4: the right edge block is 3 wide and is clipped to the bounds.
            MaskLayout layout = MaskLayout.ForRegion(0, 10, 0, 7, 4);
            Assert.AreEqual(11, layout.Width);
            Assert.AreEqual(3, layout.RasterWidth);
            Assert.AreEqual(2, layout.RasterHeight);

            WireMask mask = SegmentationMaskUnion.EncodeMask(new byte[3 * 2], layout);
            using Image<L8> png = Image.Load<L8>(mask.Png.ToByteArray());

            Assert.AreEqual(3, png.Width);
            Assert.AreEqual(2, png.Height);
            Assert.AreEqual(0, mask.Bounds.XMin);
            Assert.AreEqual(10, mask.Bounds.XMax);
            Assert.AreEqual(0, mask.Bounds.YMin);
            Assert.AreEqual(7, mask.Bounds.YMax);
            Assert.AreEqual(4u, mask.Scale);
        }

        [TestMethod]
        public void TheBoundsAreSnappedToTheScaleAndTheShapeIsStillWhollyCovered()
        {
            (int X, int Y)[] origins = [(0, 0), (1, 3), (2, 1), (3, 2), (1001, -7), (-5, -9), (-1023, 1025), (513, 511)];
            foreach ((int originX, int originY) in origins)
            {
                Rectangle region = Box(originX, originY, originX + 2000, originY + 1100);
                WireMask mask = MaskOf(region);
                int scale = Scale(mask);

                Assert.IsTrue(scale > 1);
                Assert.AreEqual(0, Mod(mask.Bounds.XMin, scale), $"x_min is a multiple of the scale at origin ({originX}, {originY})");
                Assert.AreEqual(0, Mod(mask.Bounds.YMax + 1, scale), $"y_max + 1 is a multiple of the scale at origin ({originX}, {originY})");
                Assert.IsTrue(mask.Bounds.XMin <= originX && mask.Bounds.XMax >= originX + 1999, "snapping only grows the bounds");
                Assert.IsTrue(mask.Bounds.YMin <= originY && mask.Bounds.YMax >= originY + 1099);

                bool[] expanded = Expand(mask);
                for (int y = originY; y < originY + 1100; y++)
                    for (int x = originX; x < originX + 2000; x++)
                        if (Inside(region, x, y) && !On(expanded, mask, x, y))
                            Assert.Fail($"pixel ({x}, {y}) of the shape is not covered at origin ({originX}, {originY})");
            }
        }

        [TestMethod]
        public void TheExpandedMaskIsASupersetThatDiffersByNoMoreThanTheBlockEdge()
        {
            Rectangle region = Box(1003, 2001, 3017, 3102);
            WireMask mask = MaskOf(region);
            int scale = Scale(mask);
            bool[] expanded = Expand(mask);

            int extra = 0;
            for (int y = mask.Bounds.YMin; y <= mask.Bounds.YMax; y++)
            {
                for (int x = mask.Bounds.XMin; x <= mask.Bounds.XMax; x++)
                {
                    bool inside = Inside(region, x, y);
                    bool set = On(expanded, mask, x, y);
                    if (inside && !set)
                        Assert.Fail($"({x}, {y}) is in the shape but not in the expanded mask");
                    if (!set || inside)
                        continue;

                    extra++;
                    double dx = Math.Max(0, Math.Max(region.Left - (x + 0.5), x + 0.5 - region.Right));
                    double dy = Math.Max(0, Math.Max(region.Bottom - (y + 0.5), y + 0.5 - region.Top));
                    if (dx >= scale || dy >= scale)
                        Assert.Fail($"({x}, {y}) is {dx}, {dy} from the shape, more than one block");
                }
            }

            double perimeter = 2.0 * (region.Width + region.Height);
            Assert.IsTrue(extra <= perimeter * scale, $"{extra} extra pixels is more than a block-wide edge ({perimeter * scale})");
        }

        [TestMethod]
        public void ASingleWindowMaskIsExactlyTheShapeAndNeverCoarse()
        {
            Rectangle region = Box(100, 200, 600, 500);
            WireMask mask = MaskOf(region);
            bool[] expanded = Expand(mask);

            Assert.AreEqual(1, Scale(mask));
            for (int y = mask.Bounds.YMin; y <= mask.Bounds.YMax; y++)
                for (int x = mask.Bounds.XMin; x <= mask.Bounds.XMax; x++)
                    if (Inside(region, x, y) != On(expanded, mask, x, y))
                        Assert.Fail($"pixel ({x}, {y}) differs from the exact fill");
        }

        [TestMethod]
        public void ARegionLargerThanOneWindowProducesAValidMask()
        {
            Vector2[] ring = [new(0, 0), new(3000, 200), new(2900, 1500), new(100, 1400)];
            SegmentationMaskUnion union = new();
            union.Add([ring]);

            WireMask mask = union.ToMask(1, new Vector2(1500, 700))!;
            using Image<L8> png = Image.Load<L8>(mask.Png.ToByteArray());

            Assert.AreEqual(4u, mask.Scale);
            Assert.AreEqual((Width(mask) + 3) / 4, png.Width);
            Assert.AreEqual((Height(mask) + 3) / 4, png.Height);
            Assert.IsTrue(png.Width > 1024 / 4, "a mask wider than one window");
            bool[] expanded = Expand(mask);
            Assert.IsTrue(On(expanded, mask, 1500, 700));
            Assert.IsFalse(On(expanded, mask, 50, 1450), "outside the quadrilateral");
        }

        [TestMethod]
        public void SeveralShapesAreOneMaskOverTheirCombinedBoundsAtOneScale()
        {
            SegmentationMaskUnion union = new();
            union.Add(Box(0, 0, 200, 200));
            union.Add(Box(3000, 1500, 3200, 1700));

            WireMask mask = union.ToMask(1, new Vector2(100, 100))!;
            bool[] expanded = Expand(mask);

            Assert.AreEqual(4, Scale(mask));
            Assert.IsTrue(Width(mask) >= 3200 && Height(mask) >= 1700);
            Assert.IsTrue(On(expanded, mask, 100, 100));
            Assert.IsTrue(On(expanded, mask, 3100, 1600));
            Assert.IsFalse(On(expanded, mask, 1600, 800), "the gap between the shapes stays off");
        }

        [TestMethod]
        public void ARegionWiderThanTheServersPixelBudgetIsCutDownAroundTheCentre()
        {
            SegmentationMaskUnion union = new();
            union.Add(Box(-50000, -50000, 50000, 50000));

            WireMask mask = union.ToMask(1, Centre)!;

            Assert.IsTrue(Width(mask) <= MaskScalePolicy.MaxBoundsSide + MaskScalePolicy.LargeRegionScale);
            Assert.IsTrue(Height(mask) <= MaskScalePolicy.MaxBoundsSide + MaskScalePolicy.LargeRegionScale);
            Assert.IsTrue((long)Width(mask) * Height(mask) <= 1L << 26, "under the server's 1 << 26 pixel limit");
            Assert.IsTrue(mask.Bounds.XMin <= 0 && mask.Bounds.XMax >= 0, "the cut keeps the centre");
        }

        [TestMethod]
        public void TheScalePolicyDoublesUntilThePngFits()
        {
            Assert.AreEqual(1, MaskScalePolicy.Plan(0, 1023, 0, 1023).Scale);
            Assert.AreEqual(4, MaskScalePolicy.Plan(0, 1024, 0, 1023).Scale);
            Assert.AreEqual(4, MaskScalePolicy.Plan(0, 16000, 0, 100).Scale);
            Assert.AreEqual(8, MaskScalePolicy.Plan(0, 20000, 0, 100).Scale, "20000 / 4 is over the PNG ceiling");
            Assert.AreEqual(16, MaskScalePolicy.Plan(0, 40000, 0, 100).Scale);
        }

        [TestMethod]
        public void TheMarginIsRasterPixelsAtAnyScale()
        {
            MaskLayout small = MaskScalePolicy.Plan(0, 99, 0, 99, marginRasterPixels: 128);
            Assert.AreEqual(1, small.Scale);
            Assert.AreEqual(100 + 256, small.RasterWidth);

            MaskLayout large = MaskScalePolicy.Plan(0, 2999, 0, 1499, marginRasterPixels: 128);
            Assert.AreEqual(4, large.Scale);
            Assert.IsTrue(large.Width >= 3000 + 2 * 128 * 4, "128 raster pixels of 4 mosaic pixels on each side");
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
        public void ALargePolygonBecomesACoarseRasterWithRoomToGrow()
        {
            Vector2[] ring = [new(0, 0), new(3000, 0), new(3000, 1500), new(0, 1500)];

            RasterMask mask = RasterMask.FromRings([ring], downsample: 1, marginPixels: 128)!;

            Assert.AreEqual(4, mask.Scale);
            Assert.AreEqual(mask.Width * 4, mask.Layout.Width);
            Assert.IsTrue(mask.WorldBounds.Left <= -128 * 4 && mask.WorldBounds.Right >= 3000 + 128 * 4);
            Assert.IsTrue(mask.WorldBounds.Bottom <= -128 * 4 && mask.WorldBounds.Top >= 1500 + 128 * 4);
            Assert.AreEqual(750 * 375, mask.Pixels.Count(p => p != 0), 750.0 + 375 + 8, "one set pixel per 4 x 4 block of the polygon, give or take the edge");
            Assert.IsTrue(mask.TryGetSetWorldBounds(out Rectangle set));
            Assert.AreEqual(0, set.Left, 4);
            Assert.AreEqual(3000, set.Right, 8);
        }

        [TestMethod]
        public void ATooLargePolygonIsNotRasterizedForEditing()
        {
            Vector2[] ring = [new(0, 0), new(20000, 0), new(20000, 100), new(0, 100)];

            Assert.IsNull(RasterMask.FromRings([ring], downsample: 1));
            Assert.IsNotNull(RasterMask.FromRings([ring], downsample: 4), "at a coarser tile level it fits");
        }

        [TestMethod]
        public void APolygonWiderThanTheWireLimitIsRasterizedWholeForTheTiledPath()
        {
            Vector2[] ring = [new(0, 0), new(20000, 0), new(20000, 100), new(0, 100)];

            RasterMask mask = RasterMask.FromRings([ring], downsample: 1, marginPixels: 128, wholeRegion: true)!;

            Assert.IsNotNull(mask, "the tiled path has no bounds limit");
            Assert.IsTrue(mask.Width <= MaskScalePolicy.MaxPngSide && mask.Height <= MaskScalePolicy.MaxPngSide, "the raster stays editable in memory");
            Assert.IsTrue(mask.Scale > MaskScalePolicy.LargeRegionScale, "the scale doubles until the raster fits");
            Assert.IsTrue(mask.TryGetSetWorldBounds(out Rectangle set));
            Assert.AreEqual(0, set.Left, mask.Scale);
            Assert.AreEqual(20000, set.Right, mask.Scale, "nothing is cut off around the centre");
        }

        [TestMethod]
        public void TheOldWireStillCutsAnOversizedUnionAroundTheCentre()
        {
            SegmentationMaskUnion union = new();
            union.Add(Box(0, 0, 20000, 100));

            Assert.IsNotNull(union.Rasterize(1, new Vector2(10000, 50), out MaskLayout cut));
            Assert.IsTrue(cut.Width <= MaskScalePolicy.MaxBoundsSide + MaskScalePolicy.LargeRegionScale);

            Assert.IsNotNull(union.Rasterize(1, new Vector2(10000, 50), out MaskLayout whole, cutToLimit: false));
            Assert.IsTrue(whole.Width >= 20000);
        }

        [TestMethod]
        public void TilesSpannedCountsTheGridTilesARectangleReaches()
        {
            Assert.AreEqual(6, SegmentationMaskTiles.TilesSpanned(Box(0, 0, 3000, 1500), 1), "3 columns by 2 rows");
            Assert.AreEqual(1, SegmentationMaskTiles.TilesSpanned(Box(100, 100, 900, 900), 1));
            Assert.AreEqual(4, SegmentationMaskTiles.TilesSpanned(Box(-10, -10, 10, 10), 1), "straddling the origin");
            Assert.AreEqual(2, SegmentationMaskTiles.TilesSpanned(Box(0, 0, 3000, 1500), 2), "at downsample 2 a tile is 2048 world units: 2 columns by 1 row");
            Assert.AreEqual(1, SegmentationMaskTiles.TilesSpanned(Box(0, 0, 3000, 1500), 4), "at downsample 4 a tile is 4096 world units wide");
            Assert.IsTrue(SegmentationMaskTiles.TilesSpanned(Box(0, 0, 17 * 1024, 16 * 1024), 1) > SegmentationMaskTiles.MaxForegroundTiles);
        }

        [TestMethod]
        public void AnEditedCoarseMaskSentAsASeedCoversEveryPixelItMarked()
        {
            Vector2[] ring = [new(0, 0), new(3000, 0), new(3000, 1500), new(0, 1500)];
            RasterMask mask = RasterMask.FromRings([ring], downsample: 1, marginPixels: 128)!;
            SegmentationSeed? seed = SegmentationSeed.FromMask(mask);
            SegmentationMaskUnion union = new();
            union.Add(seed!.Value);

            WireMask wire = union.ToMask(1, seed.Value.Center)!;
            bool[] expanded = Expand(wire);

            Assert.AreEqual(4, Scale(wire));
            for (int y = 0; y < 1500; y += 7)
                for (int x = 0; x < 3000; x += 7)
                    if (!On(expanded, wire, x, y))
                        Assert.Fail($"({x}, {y}) of the edited mask is not in the wire mask");
            Assert.IsFalse(On(expanded, wire, -200, 700), "the dilate margin is not set");
        }

        [TestMethod]
        public void ACoarseBitmapInASmallUnionBecomesWholeFullResolutionBlocks()
        {
            // A 4x-coarse raster whose set pixels span only 40 x 40 mosaic pixels: the union fits one window, so it is sent at scale 1.
            byte[] pixels = new byte[300 * 300];
            for (int row = 100; row < 110; row++)
                for (int column = 100; column < 110; column++)
                    pixels[row * 300 + column] = 255;
            RasterMask coarse = new(downsample: 1, xMin: 0, yMax: 1199, width: 300, height: 300, pixels: pixels, scale: 4);
            SegmentationMaskUnion union = new();
            union.Add(SegmentationSeed.FromMask(coarse)!.Value);

            WireMask wire = union.ToMask(1, new Vector2(420, 780))!;
            bool[] expanded = Expand(wire);

            Assert.IsFalse(wire.HasScale);
            int set = expanded.Count(on => on);
            Assert.AreEqual(40 * 40, set, "ten 4-pixel blocks each way, nothing lost and nothing added");
        }
    }
}
