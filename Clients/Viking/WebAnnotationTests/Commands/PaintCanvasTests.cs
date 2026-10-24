using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.Commands.Segmentation;
using GVector2 = Geometry.Vector2;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Pins the layer the paintbrush segmentation command paints on: where a brush mark lands in volume space, that foreground and
    /// background never share a pixel, that a stroke has no gaps, and that the painted pixels become a correctly placed mask.
    /// </summary>
    [TestClass]
    public class PaintCanvasTests
    {
        /// <summary>100 x 100 pixels, one volume unit each, pixel (c, r) centred on (c + 0.5, 99.5 - r).</summary>
        private static PaintCanvas NewCanvas() => new(new MaskLayout(0, 99, 100, 100, 1), 1);

        private static GVector2 CenterOf(PaintCanvas canvas, int column, int row) => canvas.PixelCenter(column, row);

        private static void Dot(PaintCanvas canvas, int column, int row, byte value) =>
            canvas.PaintDisc(CenterOf(canvas, column, row), 0.5, value);

        private static int Count(PaintCanvas canvas, byte value)
        {
            int count = 0;
            foreach (byte pixel in canvas.Pixels)
                count += pixel == value ? 1 : 0;

            return count;
        }

        [TestMethod]
        public void PixelCenter_AndTryPixelAt_AreInverses()
        {
            PaintCanvas canvas = NewCanvas();

            Assert.IsTrue(canvas.TryPixelAt(canvas.PixelCenter(10, 20), out int column, out int row));
            Assert.AreEqual(10, column);
            Assert.AreEqual(20, row);
            Assert.AreEqual(10.5, canvas.PixelCenter(10, 20).X, 1e-9);
            Assert.AreEqual(79.5, canvas.PixelCenter(10, 20).Y, 1e-9);
        }

        [TestMethod]
        public void TryPixelAt_OutsideTheCanvas_IsFalse()
        {
            PaintCanvas canvas = NewCanvas();

            Assert.IsFalse(canvas.TryPixelAt(new GVector2(-0.5, 50), out _, out _));
            Assert.IsFalse(canvas.TryPixelAt(new GVector2(50, 100.5), out _, out _));
            Assert.IsFalse(canvas.TryPixelAt(new GVector2(100.5, 50), out _, out _));
        }

        [TestMethod]
        public void PaintDisc_MarksPixelCentresInsideTheRadiusOnly()
        {
            PaintCanvas canvas = NewCanvas();
            GVector2 center = CenterOf(canvas, 50, 50);

            int changed = canvas.PaintDisc(center, 3, PaintCanvas.Foreground);

            Assert.AreEqual(changed, Count(canvas, PaintCanvas.Foreground));
            Assert.AreEqual(PaintCanvas.Foreground, canvas.ValueAt(center));
            Assert.AreEqual(PaintCanvas.Foreground, canvas.ValueAt(CenterOf(canvas, 53, 50)), "distance 3 is inside");
            Assert.AreEqual(PaintCanvas.None, canvas.ValueAt(CenterOf(canvas, 54, 50)), "distance 4 is outside");
            Assert.AreEqual(29, changed, "the lattice points within distance 3 of a pixel centre");
        }

        [TestMethod]
        public void PaintDisc_ForegroundOverBackground_ReplacesIt()
        {
            PaintCanvas canvas = NewCanvas();
            GVector2 center = CenterOf(canvas, 50, 50);
            canvas.PaintDisc(center, 4, PaintCanvas.Background);

            canvas.PaintDisc(center, 2, PaintCanvas.Foreground);

            Assert.AreEqual(PaintCanvas.Foreground, canvas.ValueAt(center));
            Assert.AreEqual(PaintCanvas.Background, canvas.ValueAt(CenterOf(canvas, 53, 50)));
        }

        [TestMethod]
        public void PaintDisc_NearTheEdge_IsClippedWithoutError()
        {
            PaintCanvas canvas = NewCanvas();

            int changed = canvas.PaintDisc(new GVector2(0.5, 99.5), 5, PaintCanvas.Foreground);

            Assert.IsTrue(changed > 0);
            Assert.AreEqual(PaintCanvas.Foreground, canvas.Pixels[0]);
        }

        [TestMethod]
        public void EraseDisc_ClearsOnlyTheNamedType()
        {
            PaintCanvas canvas = NewCanvas();
            Dot(canvas, 40, 50, PaintCanvas.Foreground);
            Dot(canvas, 41, 50, PaintCanvas.Background);

            int cleared = canvas.EraseDisc(CenterOf(canvas, 40, 50), 3, PaintCanvas.Foreground);

            Assert.AreEqual(1, cleared);
            Assert.AreEqual(0, Count(canvas, PaintCanvas.Foreground));
            Assert.AreEqual(1, Count(canvas, PaintCanvas.Background));
        }

        [TestMethod]
        public void PaintStroke_FastMove_LeavesNoGap()
        {
            PaintCanvas canvas = NewCanvas();

            canvas.PaintStroke(CenterOf(canvas, 10, 50), CenterOf(canvas, 90, 50), 2, PaintCanvas.Foreground, erase: false);

            for (int column = 10; column <= 90; column++)
                Assert.AreEqual(PaintCanvas.Foreground, canvas.ValueAt(CenterOf(canvas, column, 50)), $"column {column}");
        }

        [TestMethod]
        public void PaintStroke_Erase_ClearsAlongTheStroke()
        {
            PaintCanvas canvas = NewCanvas();
            canvas.PaintStroke(CenterOf(canvas, 10, 50), CenterOf(canvas, 90, 50), 2, PaintCanvas.Foreground, erase: false);

            canvas.PaintStroke(CenterOf(canvas, 10, 50), CenterOf(canvas, 90, 50), 2, PaintCanvas.Foreground, erase: true);

            Assert.AreEqual(0, Count(canvas, PaintCanvas.Foreground));
        }

        [TestMethod]
        public void PaintStroke_ZeroLength_PaintsOneDisc()
        {
            PaintCanvas canvas = NewCanvas();
            GVector2 center = CenterOf(canvas, 50, 50);

            canvas.PaintStroke(center, center, 3, PaintCanvas.Background, erase: false);

            Assert.AreEqual(29, Count(canvas, PaintCanvas.Background));
        }

        [TestMethod]
        public void Survey_CountsEachTypeAndFindsTheNearest()
        {
            PaintCanvas canvas = NewCanvas();
            Dot(canvas, 45, 50, PaintCanvas.Foreground);
            Dot(canvas, 30, 50, PaintCanvas.Foreground);
            Dot(canvas, 57, 50, PaintCanvas.Background);

            var survey = canvas.Survey(CenterOf(canvas, 50, 50), 30);

            Assert.AreEqual(2, survey.Foreground);
            Assert.AreEqual(1, survey.Background);
            Assert.AreEqual(5, survey.NearestForeground, 1e-9);
            Assert.AreEqual(7, survey.NearestBackground, 1e-9);
        }

        [TestMethod]
        public void Survey_NothingPainted_HasInfiniteDistances()
        {
            var survey = NewCanvas().Survey(new GVector2(50, 50), 10);

            Assert.AreEqual(0, survey.Foreground);
            Assert.AreEqual(0, survey.Background);
            Assert.IsTrue(double.IsPositiveInfinity(survey.NearestForeground));
            Assert.IsTrue(double.IsPositiveInfinity(survey.NearestBackground));
        }

        [TestMethod]
        public void ToMask_NothingOfThatType_IsNull()
        {
            PaintCanvas canvas = NewCanvas();
            Dot(canvas, 10, 10, PaintCanvas.Foreground);

            Assert.IsNull(canvas.ToMask(PaintCanvas.Background));
        }

        [TestMethod]
        public void ToMask_IsCroppedAndPlacedWhereThePaintIs()
        {
            PaintCanvas canvas = NewCanvas();
            canvas.PaintDisc(CenterOf(canvas, 30, 40), 3, PaintCanvas.Foreground);
            Dot(canvas, 80, 80, PaintCanvas.Background);

            RasterMask mask = canvas.ToMask(PaintCanvas.Foreground);

            Assert.IsNotNull(mask);
            Assert.AreEqual(7, mask.Width);
            Assert.AreEqual(7, mask.Height);
            Assert.AreEqual(1, mask.Downsample);
            Assert.IsTrue(mask.TryGetSetWorldBounds(out Geometry.Rectangle bounds));
            Assert.AreEqual(27.0, bounds.Left, 1e-9, "the left edge of the leftmost painted pixel (column 27)");
            Assert.AreEqual(34.0, bounds.Right, 1e-9);
            Assert.AreEqual(56.0, bounds.Bottom, 1e-9, "row 43 is the lowest painted row");
            Assert.AreEqual(63.0, bounds.Top, 1e-9);
            GVector2 interior = mask.InteriorPoint().Value;
            Assert.AreEqual(30.5, interior.X, 1.0);
            Assert.AreEqual(59.5, interior.Y, 1.0);
            Assert.AreEqual(Count(canvas, PaintCanvas.Foreground), System.Linq.Enumerable.Count(mask.Pixels, p => p == 255));
        }

        [TestMethod]
        public void InteriorPoints_TwoSeparateBlobs_GiveOnePointInEach()
        {
            PaintCanvas canvas = NewCanvas();
            GVector2 left = CenterOf(canvas, 20, 50), right = CenterOf(canvas, 70, 50);
            canvas.PaintDisc(left, 5, PaintCanvas.Foreground);
            canvas.PaintDisc(right, 5, PaintCanvas.Foreground);

            var points = canvas.InteriorPoints(PaintCanvas.Foreground);

            Assert.AreEqual(2, points.Count);
            Assert.IsTrue(points.Exists(p => GVector2.Distance(p, left) <= 2), "a point near the left blob's middle");
            Assert.IsTrue(points.Exists(p => GVector2.Distance(p, right) <= 2), "a point near the right blob's middle");
        }

        [TestMethod]
        public void InteriorPoints_OverlappingStrokes_MergeIntoOnePoint()
        {
            PaintCanvas canvas = NewCanvas();
            canvas.PaintDisc(CenterOf(canvas, 40, 50), 5, PaintCanvas.Foreground);
            canvas.PaintDisc(CenterOf(canvas, 46, 50), 5, PaintCanvas.Foreground);

            Assert.AreEqual(1, canvas.InteriorPoints(PaintCanvas.Foreground).Count);
        }

        [TestMethod]
        public void InteriorPoints_BlobsJoinedLater_BecomeOnePoint()
        {
            PaintCanvas canvas = NewCanvas();
            canvas.PaintDisc(CenterOf(canvas, 20, 50), 4, PaintCanvas.Foreground);
            canvas.PaintDisc(CenterOf(canvas, 40, 50), 4, PaintCanvas.Foreground);
            Assert.AreEqual(2, canvas.InteriorPoints(PaintCanvas.Foreground).Count);

            canvas.PaintStroke(CenterOf(canvas, 20, 50), CenterOf(canvas, 40, 50), 3, PaintCanvas.Foreground, erase: false);

            Assert.AreEqual(1, canvas.InteriorPoints(PaintCanvas.Foreground).Count);
        }

        [TestMethod]
        public void InteriorPoints_DiagonalNeighboursAreOnePiece()
        {
            PaintCanvas canvas = NewCanvas();
            Dot(canvas, 10, 10, PaintCanvas.Foreground);
            Dot(canvas, 11, 11, PaintCanvas.Foreground);

            Assert.AreEqual(1, canvas.InteriorPoints(PaintCanvas.Foreground).Count);
        }

        [TestMethod]
        public void InteriorPoints_ErasingABlob_DropsItsPoint()
        {
            PaintCanvas canvas = NewCanvas();
            GVector2 left = CenterOf(canvas, 20, 50), right = CenterOf(canvas, 70, 50);
            canvas.PaintDisc(left, 5, PaintCanvas.Foreground);
            canvas.PaintDisc(right, 5, PaintCanvas.Foreground);

            canvas.EraseDisc(left, 10, PaintCanvas.Foreground);

            var points = canvas.InteriorPoints(PaintCanvas.Foreground);
            Assert.AreEqual(1, points.Count);
            Assert.IsTrue(GVector2.Distance(points[0], right) <= 2);
        }

        [TestMethod]
        public void InteriorPoints_IgnoresTheOtherTypeAndEmptyCanvas()
        {
            PaintCanvas canvas = NewCanvas();
            Assert.AreEqual(0, canvas.InteriorPoints(PaintCanvas.Foreground).Count);

            canvas.PaintDisc(CenterOf(canvas, 50, 50), 5, PaintCanvas.Background);

            Assert.AreEqual(0, canvas.InteriorPoints(PaintCanvas.Foreground).Count);
            Assert.AreEqual(1, canvas.InteriorPoints(PaintCanvas.Background).Count);
        }

        [TestMethod]
        public void ComponentMasks_AreCroppedToEachPiece()
        {
            PaintCanvas canvas = NewCanvas();
            canvas.PaintDisc(CenterOf(canvas, 20, 50), 2, PaintCanvas.Foreground);
            canvas.PaintDisc(CenterOf(canvas, 70, 50), 4, PaintCanvas.Foreground);

            var pieces = canvas.ComponentMasks(PaintCanvas.Foreground);

            Assert.AreEqual(2, pieces.Count);
            CollectionAssert.AreEquivalent(new[] { 5, 9 }, new[] { pieces[0].Width, pieces[1].Width });
        }

        [TestMethod]
        public void ToMask_PreservesTheCoarseScale()
        {
            PaintCanvas canvas = new(new MaskLayout(0, 199, 200, 200, 2), 1);
            canvas.PaintDisc(canvas.PixelCenter(10, 10), 3, PaintCanvas.Foreground);

            RasterMask mask = canvas.ToMask(PaintCanvas.Foreground);

            Assert.AreEqual(2, mask.Scale);
            Assert.AreEqual(2.0, mask.WorldPerPixel, 1e-9);
            Assert.IsTrue(mask.TryGetSetWorldBounds(out Geometry.Rectangle bounds));
            Assert.IsTrue(bounds.Contains(canvas.PixelCenter(10, 10)));
        }

        [TestMethod]
        public void TakeDirtyRegion_ReportsTheChangedRectangleOnce()
        {
            PaintCanvas canvas = NewCanvas();
            Assert.IsFalse(canvas.TakeDirtyRegion(out _, out _, out _, out _));

            Dot(canvas, 10, 20, PaintCanvas.Foreground);
            Dot(canvas, 12, 25, PaintCanvas.Foreground);

            Assert.IsTrue(canvas.TakeDirtyRegion(out int column, out int row, out int width, out int height));
            Assert.AreEqual(10, column);
            Assert.AreEqual(20, row);
            Assert.AreEqual(3, width);
            Assert.AreEqual(6, height);
            Assert.IsFalse(canvas.TakeDirtyRegion(out _, out _, out _, out _));
        }

        [TestMethod]
        public void ForView_CoversTheViewPlusMargin()
        {
            var view = new Geometry.Rectangle(new GVector2(1000, 2000), new GVector2(1200, 2100));

            PaintCanvas canvas = PaintCanvas.ForView(view, 2, 4096, marginFraction: 0.5);

            Assert.IsTrue(canvas.WorldBounds.Contains(new GVector2(1000, 2000)));
            Assert.IsTrue(canvas.WorldBounds.Contains(new GVector2(1200, 2100)));
            Assert.IsTrue(canvas.WorldBounds.Contains(new GVector2(910, 1960)), "half a view of margin on the left and bottom");
            Assert.AreEqual(1, canvas.Scale);
        }

        [TestMethod]
        public void ForView_TooBigForTheTexture_UsesCoarserBlocks()
        {
            var view = new Geometry.Rectangle(new GVector2(0, 0), new GVector2(10000, 10000));

            PaintCanvas canvas = PaintCanvas.ForView(view, 1, 1024, marginFraction: 0);

            Assert.IsTrue(canvas.Scale > 1);
            Assert.IsTrue(canvas.Width <= 1024 && canvas.Height <= 1024);
        }

        [TestMethod]
        public void GrownToInclude_KeepsThePaintWhereItWasAndPaintsBeyondTheOldEdge()
        {
            PaintCanvas canvas = NewCanvas();
            Dot(canvas, 10, 20, PaintCanvas.Foreground);
            Dot(canvas, 90, 80, PaintCanvas.Background);
            GVector2 foreground = CenterOf(canvas, 10, 20), background = CenterOf(canvas, 90, 80);
            var beyond = new Geometry.Rectangle(new GVector2(250, 150), new GVector2(300, 200));

            PaintCanvas grown = canvas.GrownToInclude(beyond, 4096);

            Assert.AreEqual(1, grown.Scale);
            Assert.IsTrue(grown.Covers(beyond) && grown.Covers(canvas.WorldBounds));
            Assert.AreEqual(PaintCanvas.Foreground, grown.ValueAt(foreground));
            Assert.AreEqual(PaintCanvas.Background, grown.ValueAt(background));
            Assert.AreEqual(1, Count(grown, PaintCanvas.Foreground));
            Assert.AreEqual(1, Count(grown, PaintCanvas.Background));

            Assert.IsTrue(grown.PaintDisc(new GVector2(275, 175), 3, PaintCanvas.Foreground) > 0, "paint past the old canvas must land");
            Assert.AreEqual(1, Count(canvas, PaintCanvas.Foreground), "the old canvas is left unchanged");
        }

        [TestMethod]
        public void GrownToInclude_CoarserBlocks_KeepAOnePixelLine()
        {
            PaintCanvas canvas = NewCanvas();
            for (int column = 0; column < 100; column++)
                Dot(canvas, column, 50, PaintCanvas.Foreground);

            PaintCanvas grown = canvas.GrownToInclude(new Geometry.Rectangle(new GVector2(0, 0), new GVector2(4000, 4000)), 1024);

            Assert.IsTrue(grown.Scale > 1);
            for (int column = 0; column < 100; column++)
                Assert.AreEqual(PaintCanvas.Foreground, grown.ValueAt(CenterOf(canvas, column, 50)), $"column {column}");
        }

        [TestMethod]
        public void GrownToInclude_ReportsTheWholeRasterDirty()
        {
            PaintCanvas grown = NewCanvas().GrownToInclude(new Geometry.Rectangle(new GVector2(0, 0), new GVector2(200, 100)), 4096);

            Assert.IsTrue(grown.TakeDirtyRegion(out int column, out int row, out int width, out int height));
            Assert.AreEqual((0, 0, grown.Width, grown.Height), (column, row, width, height));
        }

        [TestMethod]
        public void PaintOutsideTheCanvas_IsDropped_SoTheCommandMustGrowItFirst()
        {
            PaintCanvas canvas = NewCanvas();

            Assert.AreEqual(0, canvas.PaintDisc(new GVector2(150, 50), 3, PaintCanvas.Foreground));
            Assert.IsFalse(canvas.Covers(new Geometry.Rectangle(new GVector2(147, 47), new GVector2(153, 53))));
            Assert.IsTrue(canvas.Covers(canvas.WorldBounds));
        }
    }
}
