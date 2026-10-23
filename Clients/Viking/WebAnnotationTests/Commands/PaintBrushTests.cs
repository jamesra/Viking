using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.Commands.Segmentation;
using GVector2 = Geometry.Vector2;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Pins the rules of the paintbrush segmentation command that decide what a stroke does: the mode it starts in (foreground,
    /// background, or erase of the nearest kind of paint) and how the brush is resized.
    /// </summary>
    [TestClass]
    public class PaintBrushTests
    {
        private static PaintCanvas NewCanvas() => new(new MaskLayout(0, 99, 100, 100, 1), 1);

        private static void Dot(PaintCanvas canvas, int column, int row, byte value) =>
            canvas.PaintDisc(canvas.PixelCenter(column, row), 0.5, value);

        private static GVector2 Center(PaintCanvas canvas) => canvas.PixelCenter(50, 50);

        [TestMethod]
        public void ChooseMode_NoModifier_IsForegroundWhereverTheBrushIs()
        {
            PaintCanvas canvas = NewCanvas();
            Assert.AreEqual(PaintMode.Foreground, PaintBrush.ChooseMode(canvas, Center(canvas), 10, alternate: false));

            Dot(canvas, 50, 50, PaintCanvas.Background);
            Assert.AreEqual(PaintMode.Foreground, PaintBrush.ChooseMode(canvas, Center(canvas), 10, alternate: false));
        }

        [TestMethod]
        public void ChooseMode_ModifierOverBareGround_IsBackground()
        {
            PaintCanvas canvas = NewCanvas();
            Dot(canvas, 10, 10, PaintCanvas.Foreground);

            Assert.AreEqual(PaintMode.Background, PaintBrush.ChooseMode(canvas, Center(canvas), 10, alternate: true));
        }

        [TestMethod]
        public void ChooseMode_ModifierOverOnlyForeground_ErasesForeground()
        {
            PaintCanvas canvas = NewCanvas();
            Dot(canvas, 52, 50, PaintCanvas.Foreground);

            Assert.AreEqual(PaintMode.EraseForeground, PaintBrush.ChooseMode(canvas, Center(canvas), 10, alternate: true));
        }

        [TestMethod]
        public void ChooseMode_ModifierOverOnlyBackground_ErasesBackground()
        {
            PaintCanvas canvas = NewCanvas();
            Dot(canvas, 52, 50, PaintCanvas.Background);

            Assert.AreEqual(PaintMode.EraseBackground, PaintBrush.ChooseMode(canvas, Center(canvas), 10, alternate: true));
        }

        [TestMethod]
        public void ChooseMode_BothUnderTheBrush_ErasesTheNearerType()
        {
            PaintCanvas canvas = NewCanvas();
            Dot(canvas, 53, 50, PaintCanvas.Foreground);
            Dot(canvas, 50, 56, PaintCanvas.Background);
            Dot(canvas, 40, 40, PaintCanvas.Background);
            Dot(canvas, 41, 40, PaintCanvas.Background);

            Assert.AreEqual(PaintMode.EraseForeground, PaintBrush.ChooseMode(canvas, Center(canvas), 20, alternate: true),
                "foreground is nearer even though background has more pixels");
        }

        [TestMethod]
        public void ChooseMode_EquallyNear_ErasesTheTypeWithMorePixelsUnderTheBrush()
        {
            PaintCanvas canvas = NewCanvas();
            Dot(canvas, 45, 50, PaintCanvas.Foreground);
            Dot(canvas, 30, 50, PaintCanvas.Foreground);
            Dot(canvas, 31, 50, PaintCanvas.Foreground);
            Dot(canvas, 55, 50, PaintCanvas.Background);

            Assert.AreEqual(PaintMode.EraseForeground, PaintBrush.ChooseMode(canvas, Center(canvas), 25, alternate: true));

            PaintCanvas other = NewCanvas();
            Dot(other, 45, 50, PaintCanvas.Foreground);
            Dot(other, 55, 50, PaintCanvas.Background);
            Dot(other, 70, 50, PaintCanvas.Background);
            Dot(other, 71, 50, PaintCanvas.Background);

            Assert.AreEqual(PaintMode.EraseBackground, PaintBrush.ChooseMode(other, Center(other), 25, alternate: true));
        }

        [TestMethod]
        public void ChooseMode_EquallyNearAndEquallyMany_ErasesForeground()
        {
            PaintCanvas canvas = NewCanvas();
            Dot(canvas, 45, 50, PaintCanvas.Foreground);
            Dot(canvas, 55, 50, PaintCanvas.Background);

            Assert.AreEqual(PaintMode.EraseForeground, PaintBrush.ChooseMode(canvas, Center(canvas), 20, alternate: true));
        }

        [TestMethod]
        public void ChooseMode_PaintOutsideTheBrush_IsIgnored()
        {
            PaintCanvas canvas = NewCanvas();
            Dot(canvas, 80, 50, PaintCanvas.Foreground);

            Assert.AreEqual(PaintMode.Background, PaintBrush.ChooseMode(canvas, Center(canvas), 5, alternate: true));
        }

        [TestMethod]
        public void Apply_EraseForeground_LeavesBackground()
        {
            PaintCanvas canvas = NewCanvas();
            canvas.PaintDisc(Center(canvas), 4, PaintCanvas.Foreground);
            Dot(canvas, 52, 50, PaintCanvas.Background);

            PaintBrush.Apply(canvas, PaintMode.EraseForeground, Center(canvas), Center(canvas), 10);

            Assert.IsFalse(canvas.Has(PaintCanvas.Foreground));
            Assert.IsTrue(canvas.Has(PaintCanvas.Background));
        }

        [TestMethod]
        public void Apply_Background_PaintsBackground()
        {
            PaintCanvas canvas = NewCanvas();

            PaintBrush.Apply(canvas, PaintMode.Background, Center(canvas), Center(canvas), 3);

            Assert.IsTrue(canvas.Has(PaintCanvas.Background));
            Assert.IsFalse(canvas.Has(PaintCanvas.Foreground));
        }

        [TestMethod]
        public void ResizeRadius_GrowsAndShrinksByTheNotchFactor()
        {
            Assert.AreEqual(20 * PaintBrush.NotchFactor, PaintBrush.ResizeRadius(20, 1), 1e-9);
            Assert.AreEqual(20 / PaintBrush.NotchFactor, PaintBrush.ResizeRadius(20, -1), 1e-9);
            Assert.AreEqual(20, PaintBrush.ResizeRadius(20, 0), 1e-9);
        }

        [TestMethod]
        public void ResizeRadius_IsClamped()
        {
            Assert.AreEqual(PaintBrush.MaxRadiusPixels, PaintBrush.ResizeRadius(390, 20), 1e-9);
            Assert.AreEqual(PaintBrush.MinRadiusPixels, PaintBrush.ResizeRadius(3, -20), 1e-9);
        }
    }
}
