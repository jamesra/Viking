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
        /// <summary>Side of the square rasters these tests build by hand.</summary>
        private const int Size = 1024;

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
            byte[] pixels = union.Rasterize(downsample: 2, out MaskLayout layout)!;

            Assert.IsNotNull(pixels);
            Assert.AreEqual(1, layout.Scale);
            // 400 x 400 volume units at 2 units per pixel is 200 x 200 pixels, give or take the edge.
            Assert.AreEqual(200 * 200, Count(pixels), 200 * 4);
            Vector2 inside = seed.Value.Center;
            int column = (int)Math.Floor(inside.X / 2) - layout.XMin;
            int row = layout.YMax - (int)Math.Floor(inside.Y / 2);
            Assert.AreEqual(255, pixels[row * layout.RasterWidth + column]);
        }

        [TestMethod]
        public void MaskSeed_EmptyMaskHasNoSeed()
        {
            RasterMask empty = new(downsample: 1, xMin: 0, yMax: 0, width: Size, height: Size, pixels: new byte[Size * Size]);
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
            RasterMask ring = new(downsample: 1, xMin: 0, yMax: Size - 1, width: Size, height: Size, pixels: pixels);

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
            Assert.AreEqual(100, mask.WorldBounds.Width, "no margin was asked for");
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
        public void TheMarginLeavesRoomToGrow()
        {
            Vector2[] ring = [new(1000, 2000), new(1100, 2000), new(1100, 2100), new(1000, 2100)];

            RasterMask mask = RasterMask.FromRings([ring], downsample: 1, marginPixels: 64)!;

            Assert.AreEqual(100 + 2 * 64, mask.Width);
            Assert.AreEqual(100 + 2 * 64, mask.Height);
            Assert.AreEqual(1, mask.Scale);
            Assert.AreEqual(100 * 100, Count(mask.Pixels));
        }

        [TestMethod]
        public void TheInteriorPointOfARingIsOnTheBandAtItsDeepestNotOnItsEdge()
        {
            byte[] pixels = Square(100, 100, 300);
            for (int y = 150; y < 350; y++)
                for (int x = 150; x < 350; x++)
                    pixels[y * Size + x] = 0;     // a ring with a 50 pixel band
            RasterMask ring = new(downsample: 1, xMin: 0, yMax: Size - 1, width: Size, height: Size, pixels: pixels);

            Vector2 point = ring.InteriorPoint()!.Value;
            int column = (int)Math.Floor(point.X), row = ring.YMax - (int)Math.Floor(point.Y);

            Assert.AreEqual(255, pixels[row * Size + column]);
            double depth = Math.Min(Math.Min(column - 100, 399 - column), Math.Min(row - 100, 399 - row));
            double holeDx = Math.Max(0, Math.Max(150 - column, column - 349)), holeDy = Math.Max(0, Math.Max(150 - row, row - 349));
            double depthFromHole = Math.Sqrt(holeDx * holeDx + holeDy * holeDy);
            Assert.IsTrue(Math.Min(depth, depthFromHole) >= 24, $"the point is in the middle of the 50 pixel band, not on its edge ({column}, {row})");
        }

        [TestMethod]
        public void TheInteriorPointOfASplitMaskIsInTheLargerPiece()
        {
            byte[] pixels = Square(100, 100, 60);
            byte[] big = Square(400, 400, 300);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = (byte)Math.Max(pixels[i], big[i]);
            RasterMask split = new(downsample: 1, xMin: 0, yMax: Size - 1, width: Size, height: Size, pixels: pixels);

            Vector2 point = split.InteriorPoint()!.Value;

            Assert.IsTrue(point.X >= 400 && point.X < 700, $"x {point.X}");
            Assert.IsTrue(point.Y >= Size - 700 && point.Y < Size - 400, $"y {point.Y}");
        }

        [TestMethod]
        public void AnEmptyMaskHasNoInteriorPoint()
        {
            RasterMask empty = new(downsample: 1, xMin: 0, yMax: 0, width: 8, height: 8, pixels: new byte[64]);

            Assert.IsNull(empty.InteriorPoint());
        }

        [TestMethod]
        public void TranslatedMovesTheWorldBoundsByWholePixelsAndKeepsThePixels()
        {
            RasterMask mask = new(downsample: 4, xMin: 10, yMax: 20, width: 8, height: 8, pixels: new byte[64], scale: 2);

            RasterMask moved = mask.Translated(6, -4);

            Assert.AreSame(mask.Pixels, moved.Pixels);
            Assert.AreEqual(mask.WorldBounds.Left + 6 * 4, moved.WorldBounds.Left, 1e-9);
            Assert.AreEqual(mask.WorldBounds.Bottom - 4 * 4, moved.WorldBounds.Bottom, 1e-9);
            Assert.AreEqual(mask.WorldBounds.Width, moved.WorldBounds.Width, 1e-9);
        }

        /// <summary>
        /// The on-canvas label tells the user how to go on, so it must name only gestures the command really binds: a
        /// double-click on the mask and a click on the check accept, Escape cancels.
        /// </summary>
        [TestMethod]
        public void Label_NamesTheAcceptAndCancelGesturesTheMaskEditBinds()
        {
            string text = SegmentationMaskEdit.LabelText(40);

            Assert.IsTrue(text.StartsWith("+40 nm", StringComparison.Ordinal), text);
            StringAssert.Contains(text, "double-click");
            StringAssert.Contains(text, "check");
            StringAssert.Contains(text, "segment");
            StringAssert.Contains(text, "Esc cancels");
            Assert.IsTrue(SegmentationMaskEditCommand.MouseHelp.Any(e =>
                e.Inputs is { Length: 1 } inputs && inputs[0] == Viking.Common.HelpInput.LeftDoubleClick &&
                e.Description.StartsWith("Accept", StringComparison.Ordinal)));
            Assert.IsTrue(SegmentationMaskEditCommand.MouseHelp.Any(e =>
                e.Inputs is { Length: 1 } inputs && inputs[0] == Viking.Common.HelpInput.LeftClick &&
                e.Description.Contains("green check") && e.Description.Contains("red X")));
        }

        /// <summary>The viewer's sprite font holds printable ASCII only; any other character would stop the label drawing.</summary>
        [TestMethod]
        public void Label_IsPrintableAscii()
        {
            foreach (double nm in new[] { -1234.0, -20, 0, 20, 98765 })
            {
                string text = SegmentationMaskEdit.LabelText(nm);
                Assert.IsTrue(text.All(c => c >= ' ' && c <= '~'), text);
            }
        }

        [TestMethod]
        public void ChangeLabel_IsSignedWholeNanometres()
        {
            Assert.AreEqual("+40 nm", SegmentationMaskEdit.ChangeLabel(40));
            Assert.AreEqual("-20 nm", SegmentationMaskEdit.ChangeLabel(-20));
            Assert.AreEqual("0 nm", SegmentationMaskEdit.ChangeLabel(0));
            Assert.AreEqual("0 nm", SegmentationMaskEdit.ChangeLabel(-0.4), "a change that rounds to nothing has no sign");
            Assert.AreEqual("+20 nm", SegmentationMaskEdit.ChangeLabel(19.5));
            Assert.AreEqual("-5 nm", SegmentationMaskEdit.ChangeLabel(-4.5));
        }

        [TestMethod]
        public void ChangeNanometers_IsRadiusTimesPixelSizeTimesScale()
        {
            // Two raster pixels of a scale-2 raster at downsample 4 is 16 volume units; at 2.18 nm per unit that is 34.88 nm.
            Assert.AreEqual(34.88, SegmentationMaskEdit.ChangeNanometers(2, worldPerPixel: 8, nanometersPerWorld: 2.18), 1e-9);
            Assert.AreEqual(-34.88, SegmentationMaskEdit.ChangeNanometers(-2, worldPerPixel: 8, nanometersPerWorld: 2.18), 1e-9);
        }

        /// <summary>A small hand-drawn mask: rows of 0/1 characters, '1' inside.</summary>
        private static byte[] Rows(params string[] rows)
        {
            byte[] pixels = new byte[rows.Length * rows[0].Length];
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    pixels[y * rows[0].Length + x] = rows[y][x] == '1' ? (byte)255 : (byte)0;
            return pixels;
        }

        /// <summary>An inside pixel is an edge pixel exactly when one of its four neighbours is outside; diagonals do not count.</summary>
        [TestMethod]
        public void IsEdge_InsidePixelsWithAnOutsideFourNeighbour()
        {
            byte[] pixels = Rows(
                "000000",
                "011110",
                "011110",
                "011110",
                "000000");

            Assert.IsTrue(SegmentationMaskEdit.IsEdge(pixels, 6, 5, 1, 1), "corner");
            Assert.IsTrue(SegmentationMaskEdit.IsEdge(pixels, 6, 5, 2, 1), "top side");
            Assert.IsTrue(SegmentationMaskEdit.IsEdge(pixels, 6, 5, 4, 2), "right side");
            Assert.IsFalse(SegmentationMaskEdit.IsEdge(pixels, 6, 5, 2, 2), "interior");
            Assert.IsFalse(SegmentationMaskEdit.IsEdge(pixels, 6, 5, 3, 2), "interior");
            Assert.IsFalse(SegmentationMaskEdit.IsEdge(pixels, 6, 5, 0, 0), "outside is never an edge");

            // Only a diagonal neighbour outside: (2, 2) here touches the hole at (3, 3) diagonally, so it is not an edge.
            byte[] notched = Rows(
                "11111",
                "11111",
                "11111",
                "11101",
                "11111");
            Assert.IsFalse(SegmentationMaskEdit.IsEdge(notched, 5, 5, 2, 2));
            Assert.IsTrue(SegmentationMaskEdit.IsEdge(notched, 5, 5, 3, 2));
        }

        [TestMethod]
        public void IsEdge_TheRasterBorderCountsAsOutside()
        {
            byte[] full = Rows("111", "111", "111");

            Assert.IsTrue(SegmentationMaskEdit.IsEdge(full, 3, 3, 0, 1));
            Assert.IsTrue(SegmentationMaskEdit.IsEdge(full, 3, 3, 2, 2));
            Assert.IsFalse(SegmentationMaskEdit.IsEdge(full, 3, 3, 1, 1));
        }

        [TestMethod]
        public void Colorize_SplitsEdgeFillAndOutside()
        {
            byte[] pixels = Rows(
                "000000",
                "011110",
                "011110",
                "011110",
                "000000");
            char[] colors = new char[pixels.Length];

            SegmentationMaskEdit.Colorize(pixels, 6, 5, 'f', 'e', '.', colors);

            Assert.AreEqual(
                "......" + ".eeee." + ".effe." + ".eeee." + "......",
                new string(colors));
        }

        private const int ViewWidth = 1200, ViewHeight = 800;

        private static MaskEditChrome Place(ScreenBox mask, double labelWidth = 300) =>
            MaskEditChromeLayout.Place(mask, ViewWidth, ViewHeight, default, labelWidth);

        /// <summary>The buttons, label and mask must not cover each other.</summary>
        private static void AssertClear(ScreenBox mask, MaskEditChrome chrome, double labelWidth = 300)
        {
            ScreenBox accept = ScreenBox.AroundCircle(chrome.Accept, chrome.ButtonRadius);
            ScreenBox cancel = ScreenBox.AroundCircle(chrome.Cancel, chrome.ButtonRadius);
            double h = MaskEditChromeLayout.LabelFontPixels;
            ScreenBox label = new(chrome.LabelCenter.X - labelWidth / 2, chrome.LabelCenter.Y - h / 2, chrome.LabelCenter.X + labelWidth / 2, chrome.LabelCenter.Y + h / 2);

            Assert.IsFalse(accept.Overlaps(mask), "accept covers the mask");
            Assert.IsFalse(cancel.Overlaps(mask), "cancel covers the mask");
            Assert.IsFalse(accept.Overlaps(cancel), "the buttons overlap");
            Assert.IsFalse(label.Overlaps(mask), "the label covers the mask");
            Assert.IsFalse(label.Overlaps(accept) || label.Overlaps(cancel), "the label covers a button");
        }

        [TestMethod]
        public void Chrome_ButtonsStandRightOfTheMaskAcceptOnTopAndTheLabelBelow()
        {
            ScreenBox mask = new(400, 300, 600, 500);

            MaskEditChrome chrome = Place(mask);

            Assert.IsTrue(chrome.ButtonsRight);
            Assert.IsTrue(chrome.LabelBelow);
            Assert.IsTrue(chrome.Accept.X - chrome.ButtonRadius > mask.Right, "the buttons are right of the mask");
            Assert.AreEqual(chrome.Accept.X, chrome.Cancel.X, 1e-9);
            Assert.IsTrue(chrome.Accept.Y < chrome.Cancel.Y, "accept above cancel");
            Assert.AreEqual(mask.Center.Y, (chrome.Accept.Y + chrome.Cancel.Y) / 2, 1e-9, "centred on the mask's height");
            Assert.AreEqual(mask.Center.X, chrome.LabelCenter.X, 1e-9);
            Assert.IsTrue(chrome.LabelCenter.Y > mask.Bottom);
            Assert.AreEqual(MaskEditChromeLayout.ButtonRadiusPixels, chrome.ButtonRadius, "a fixed screen size");
            AssertClear(mask, chrome);
        }

        [TestMethod]
        public void Chrome_GoesLeftWhenTheRightIsTakenAndKeepsClearOfTheCornerButtons()
        {
            // The mask's right edge is in the corner pen buttons' column.
            ScreenBox mask = new(800, 300, ViewWidth - 60, 500);

            MaskEditChrome chrome = Place(mask);

            Assert.IsFalse(chrome.ButtonsRight);
            Assert.IsTrue(chrome.Accept.X + chrome.ButtonRadius < mask.Left);
            AssertClear(mask, chrome);
            Assert.IsTrue(chrome.LabelCenter.X + 150 <= ViewWidth - MaskEditChromeLayout.CornerColumnPixels, "the label stays out of the corner column");
        }

        [TestMethod]
        public void Chrome_LabelGoesAboveWhenBelowIsOffTheView()
        {
            ScreenBox mask = new(400, 500, 600, ViewHeight - 4);

            MaskEditChrome chrome = Place(mask);

            Assert.IsFalse(chrome.LabelBelow);
            Assert.IsTrue(chrome.LabelCenter.Y < mask.Top);
            Assert.IsTrue(chrome.Cancel.Y + chrome.ButtonRadius <= ViewHeight, "the buttons stay on the view");
            AssertClear(mask, chrome);
        }

        [TestMethod]
        public void Chrome_LabelMovesPastTheButtonsBesideAShortMask()
        {
            // A wide, short mask: the button pair reaches below it, where a wide label would go.
            ScreenBox mask = new(300, 400, 700, 410);

            MaskEditChrome chrome = Place(mask, labelWidth: 600);

            AssertClear(mask, chrome, labelWidth: 600);
        }

        [TestMethod]
        public void Chrome_StaysOnTheViewWhenTheMaskIsLargerThanIt()
        {
            ScreenBox mask = new(-500, -500, ViewWidth + 500, ViewHeight + 500);

            MaskEditChrome chrome = Place(mask);

            Assert.IsTrue(chrome.Accept.X - chrome.ButtonRadius >= 0 && chrome.Accept.X + chrome.ButtonRadius <= ViewWidth - MaskEditChromeLayout.CornerColumnPixels);
            Assert.IsTrue(chrome.Accept.Y - chrome.ButtonRadius >= 0 && chrome.Cancel.Y + chrome.ButtonRadius <= ViewHeight);
            Assert.IsTrue(chrome.LabelCenter.Y >= 0 && chrome.LabelCenter.Y <= ViewHeight);
        }

        [TestMethod]
        public void Chrome_HitTestFindsEachButtonAndNothingBetween()
        {
            MaskEditChrome chrome = Place(new ScreenBox(400, 300, 600, 500));

            Assert.AreEqual(WebAnnotation.UI.AutoPolygonize.ProposalButtonKind.Accept, chrome.HitTest(chrome.Accept.X, chrome.Accept.Y));
            Assert.AreEqual(WebAnnotation.UI.AutoPolygonize.ProposalButtonKind.Hide, chrome.HitTest(chrome.Cancel.X + 5, chrome.Cancel.Y - 5));
            Assert.IsNull(chrome.HitTest(chrome.Accept.X, (chrome.Accept.Y + chrome.Cancel.Y) / 2), "the gap between them");
            Assert.IsNull(chrome.HitTest(500, 400), "the mask itself");
        }

        /// <summary>The pending mask ends once, whichever of answer, failure or the command ending comes first.</summary>
        [TestMethod]
        public void PendingView_EndsOnceAndStaysEnded()
        {
            RasterMask mask = new(downsample: 1, xMin: 0, yMax: 7, width: 8, height: 8, pixels: Square8());
            Vector2[] ring = [new(1, 1), new(6, 1), new(6, 6), new(1, 6), new(1, 1)];
            ResegmentPendingView pending = new(mask, [ring]);

            Assert.IsTrue(pending.IsShown);
            Assert.IsNull(pending.EndReason);

            Assert.IsTrue(pending.End("the server's outline arrived"));
            Assert.IsFalse(pending.IsShown);
            Assert.IsFalse(pending.End("the request failed"), "a later outcome changes nothing");
            pending.Dispose();
            Assert.AreEqual("the server's outline arrived", pending.EndReason);
        }

        private static byte[] Square8()
        {
            byte[] pixels = new byte[64];
            for (int y = 2; y < 6; y++)
                for (int x = 2; x < 6; x++)
                    pixels[y * 8 + x] = 255;
            return pixels;
        }
    }
}
