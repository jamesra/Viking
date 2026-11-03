using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Pins how an existing mask (a polygon) seeds the paintbrush canvas: the painted pixels match the polygon, holes stay clear, and
    /// the two rasters may differ in scale.
    /// </summary>
    [TestClass]
    public class PaintCanvasSeedTests
    {
        private static PaintCanvas NewCanvas(int scale = 1)
        {
            int side = 100 * scale;
            return new PaintCanvas(new MaskLayout(0, side - 1, side, side, scale), 1);
        }

        private static Vector2[] Square(double x, double y, double size) =>
            new[] { new Vector2(x, y), new Vector2(x + size, y), new Vector2(x + size, y + size), new Vector2(x, y + size), new Vector2(x, y) };

        private static int Count(PaintCanvas canvas, byte value) => System.Linq.Enumerable.Count(canvas.Pixels, p => p == value);

        [TestMethod]
        public void Fill_Square_PaintsThePolygonsPixels()
        {
            PaintCanvas canvas = NewCanvas();
            RasterMask mask = RasterMask.FromRings([Square(20, 30, 10)], 1);

            int changed = canvas.Fill(mask, PaintCanvas.Foreground);

            Assert.AreEqual(100, changed);
            Assert.AreEqual(100, Count(canvas, PaintCanvas.Foreground));
            Assert.AreEqual(PaintCanvas.Foreground, canvas.ValueAt(new Vector2(25, 35)));
            Assert.AreEqual(PaintCanvas.None, canvas.ValueAt(new Vector2(31, 35)));
            Assert.AreEqual(PaintCanvas.None, canvas.ValueAt(new Vector2(19, 35)));
        }

        [TestMethod]
        public void Fill_PolygonWithAHole_LeavesTheHoleClear()
        {
            PaintCanvas canvas = NewCanvas();
            RasterMask mask = RasterMask.FromRings([Square(20, 20, 30), Square(30, 30, 10)], 1);

            canvas.Fill(mask, PaintCanvas.Foreground);

            Assert.AreEqual(PaintCanvas.None, canvas.ValueAt(new Vector2(35, 35)), "inside the hole");
            Assert.AreEqual(PaintCanvas.Foreground, canvas.ValueAt(new Vector2(25, 25)));
            Assert.AreEqual(900 - 100, Count(canvas, PaintCanvas.Foreground));
        }

        [TestMethod]
        public void Fill_CoarserCanvasThanMask_StillCoversThePolygon()
        {
            PaintCanvas canvas = NewCanvas(scale: 2);
            RasterMask mask = RasterMask.FromRings([Square(20, 30, 20)], 1);

            canvas.Fill(mask, PaintCanvas.Foreground);

            Assert.AreEqual(PaintCanvas.Foreground, canvas.ValueAt(new Vector2(30, 40)));
            Assert.AreEqual(PaintCanvas.Foreground, canvas.ValueAt(new Vector2(21, 31)));
            Assert.AreEqual(PaintCanvas.None, canvas.ValueAt(new Vector2(45, 40)));
        }

        [TestMethod]
        public void Fill_MaskPartlyOutsideTheCanvas_IsClippedWithoutError()
        {
            PaintCanvas canvas = NewCanvas();
            RasterMask mask = RasterMask.FromRings([Square(90, 90, 30)], 1);

            canvas.Fill(mask, PaintCanvas.Foreground);

            Assert.AreEqual(100, Count(canvas, PaintCanvas.Foreground), "only the 10 x 10 corner is inside the canvas");
        }

        [TestMethod]
        public void Fill_ThenInteriorPoints_GivesOneClickInsideThePolygon()
        {
            PaintCanvas canvas = NewCanvas();
            canvas.Fill(RasterMask.FromRings([Square(20, 30, 10)], 1), PaintCanvas.Foreground);

            var points = canvas.InteriorPoints(PaintCanvas.Foreground);

            Assert.AreEqual(1, points.Count);
            Assert.IsTrue(points[0].X > 20 && points[0].X < 30 && points[0].Y > 30 && points[0].Y < 40);
        }

        [TestMethod]
        public void RasterMask_Contains_ReadsSetPixelsOnly()
        {
            RasterMask mask = RasterMask.FromRings([Square(20, 30, 10)], 1);

            Assert.IsTrue(mask.Contains(new Vector2(25, 35)));
            Assert.IsFalse(mask.Contains(new Vector2(35, 35)));
            Assert.IsFalse(mask.Contains(new Vector2(-5, -5)));
        }
    }
}
