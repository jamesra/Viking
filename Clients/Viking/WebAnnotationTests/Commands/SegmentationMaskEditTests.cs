using System;
using System.Linq;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.Commands.Segmentation;
using Rectangle = Geometry.Rectangle;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// The signed-distance arithmetic behind the mask dilate/erode command, and the polygon-to-mask input it is fed.
    /// </summary>
    [TestClass]
    public class SegmentationMaskEditTests
    {
        private const int Size = RasterMask.Size;

        /// <summary>A mask with a filled axis-aligned square of side <paramref name="side"/> whose top-left pixel is (left, top).</summary>
        private static byte[] Square(int left, int top, int side)
        {
            byte[] pixels = new byte[Size * Size];
            for (int y = top; y < top + side; y++)
                for (int x = left; x < left + side; x++)
                    pixels[y * Size + x] = 255;
            return pixels;
        }

        private static int Count(byte[] pixels) => pixels.Count(p => p != 0);

        [TestMethod]
        public void MaskSeed_BlitsTheBitmapAtAnotherDownsampleWithoutTracing()
        {
            Vector2[] ring = [new(1000, 1000), new(1400, 1000), new(1400, 1400), new(1000, 1400), new(1000, 1000)];
            RasterMask mask = RasterMask.FromRings([ring], downsample: 4);
            SegmentationSeed? seed = SegmentationSeed.FromMask(mask);
            Assert.IsNotNull(seed);

            SegmentationMaskUnion union = new();
            union.Add(seed.Value);
            byte[] pixels = union.Rasterize(downsample: 2, seed.Value.Center, out int xMin, out int yMax);

            Assert.IsNotNull(pixels);
            // 400 x 400 volume units at 2 units per pixel is 200 x 200 pixels, give or take the edge.
            Assert.AreEqual(200 * 200, Count(pixels), 200 * 4);
            Vector2 inside = seed.Value.Center;
            int column = (int)Math.Floor(inside.X / 2) - xMin;
            int row = yMax - (int)Math.Floor(inside.Y / 2);
            Assert.AreEqual(255, pixels[row * Size + column]);
        }

        [TestMethod]
        public void MaskSeed_EmptyMaskHasNoSeed()
        {
            RasterMask empty = new(downsample: 1, xMin: 0, yMax: 0, pixels: new byte[Size * Size]);
            Assert.IsNull(SegmentationSeed.FromMask(empty));
        }

        [TestMethod]
        public void InteriorPoint_OfARingIsOnTheObject()
        {
            // A ring: the centroid is in the hole, so the point must be moved onto the band.
            byte[] pixels = Square(100, 100, 300);
            for (int y = 150; y < 350; y++)
                for (int x = 150; x < 350; x++)
                    pixels[y * Size + x] = 0;
            RasterMask ring = new(downsample: 1, xMin: 0, yMax: Size - 1, pixels: pixels);

            Vector2 point = ring.InteriorPoint().Value;
            int column = (int)Math.Floor(point.X);
            int row = ring.YMax - (int)Math.Floor(point.Y);

            Assert.AreEqual(255, pixels[row * Size + column]);
        }

        private static byte[] Edit(byte[] mask, double radius)
        {
            float[] field = SegmentationMaskEdit.SignedDistance(mask, Size, Size)!;
            byte[] result = new byte[mask.Length];
            SegmentationMaskEdit.Threshold(field, radius, result);
            return result;
        }

        [TestMethod]
        public void ARadiusOfZeroReproducesTheMask()
        {
            byte[] mask = Square(400, 300, 100);
            CollectionAssert.AreEqual(mask, Edit(mask, 0));
        }

        [TestMethod]
        public void ErodingASquareByTenLeavesTheEightyPixelSquare()
        {
            byte[] mask = Square(400, 300, 100);
            CollectionAssert.AreEqual(Square(410, 310, 80), Edit(mask, -10));
        }

        [TestMethod]
        public void DilatingASquareGrowsEveryEdgeAndRoundsTheCorners()
        {
            byte[] mask = Square(400, 300, 100);
            byte[] grown = Edit(mask, 10);

            // Edge midpoints move out by the radius, the corner pixel does not (round structuring element).
            Assert.AreEqual(255, grown[350 * Size + 390]);
            Assert.AreEqual(0, grown[350 * Size + 389]);
            Assert.AreEqual(0, grown[290 * Size + 390]);

            // 100x100 square plus four 10x100 strips plus a quarter-disc at each corner.
            double expected = 100 * 100 + 4 * 10 * 100 + Math.PI * 10 * 10;
            Assert.AreEqual(expected, Count(grown), expected * 0.02);
        }

        [TestMethod]
        public void ErosionStopsWithTheDeepestPixelsStillThere()
        {
            byte[] mask = Square(400, 300, 100);
            float[] field = SegmentationMaskEdit.SignedDistance(mask, Size, Size)!;
            double min = SegmentationMaskEdit.MinRadius(field);

            Assert.AreEqual(-49.5, min, 0.6);
            byte[] eroded = new byte[mask.Length];
            SegmentationMaskEdit.Threshold(field, min, eroded);
            Assert.IsTrue(Count(eroded) > 0);
        }

        [TestMethod]
        public void AnEmptyMaskHasNoField() =>
            Assert.IsNull(SegmentationMaskEdit.SignedDistance(new byte[Size * Size], Size, Size));

        [TestMethod]
        public void ADilatedHoleClosesWhenTheMaskGrows()
        {
            byte[] mask = Square(400, 300, 100);
            for (int y = 340; y < 360; y++)
                for (int x = 440; x < 460; x++)
                    mask[y * Size + x] = 0;

            Assert.AreEqual(0, mask[350 * Size + 450]);
            Assert.AreEqual(255, Edit(mask, 11)[350 * Size + 450]);
        }

        [TestMethod]
        public void GrowthIsLimitedByTheNearestWindowEdge()
        {
            Assert.AreEqual(300, SegmentationMaskEdit.MaxRadius(Square(400, 300, 100), Size, Size));
            Assert.AreEqual(0, SegmentationMaskEdit.MaxRadius(Square(0, 300, 100), Size, Size));
        }

        [TestMethod]
        public void ANotchIsNeverLessThanOnePixelAndShiftTakesAQuarter()
        {
            Assert.AreEqual(1.0, SegmentationMaskEdit.RadiusChange(1, 0.5, 4, fine: false), 1e-9);
            Assert.AreEqual(-3.0, SegmentationMaskEdit.RadiusChange(-1, 12, 4, fine: false), 1e-9);
            Assert.AreEqual(0.75, SegmentationMaskEdit.RadiusChange(1, 12, 4, fine: true), 1e-9);
        }

        [TestMethod]
        public void APolygonBecomesAMaskCentredOnIt()
        {
            Vector2[] ring = [new(1000, 2000), new(1100, 2000), new(1100, 2100), new(1000, 2100)];

            RasterMask mask = RasterMask.FromRings([ring], downsample: 1)!;

            Assert.AreEqual(100 * 100, Count(mask.Pixels));
            Assert.AreEqual(1050, mask.WorldBounds.Center.X, 1);
            Assert.AreEqual(2050, mask.WorldBounds.Center.Y, 1);
            Assert.AreEqual(Size, mask.WorldBounds.Width);
        }

        [TestMethod]
        public void APolygonHoleStaysOpen()
        {
            Vector2[] outer = [new(1000, 2000), new(1100, 2000), new(1100, 2100), new(1000, 2100)];
            Vector2[] hole = [new(1040, 2040), new(1060, 2040), new(1060, 2060), new(1040, 2060)];

            RasterMask mask = RasterMask.FromRings([outer, hole], downsample: 1)!;

            Assert.AreEqual(100 * 100 - 20 * 20, Count(mask.Pixels));
        }

        [TestMethod]
        public void TheDownsampleLeavesRoomToGrow()
        {
            Assert.AreEqual(1, RasterMask.ChooseDownsample(new Rectangle(new Vector2(0, 0), new Vector2(800, 100)), 64));
            Assert.AreEqual(4, RasterMask.ChooseDownsample(new Rectangle(new Vector2(0, 0), new Vector2(2000, 100)), 64));
        }
    }
}
