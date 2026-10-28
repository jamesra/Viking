using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>Ctrl+Z in the paint command: one completed stroke per step, back through the canvas it was painted on.</summary>
    [TestClass]
    public class PaintUndoHistoryTests
    {
        private static PaintCanvas NewCanvas() =>
            PaintCanvas.ForView(new Rectangle(0, 100, 0, 100), downsample: 1, maxRasterSide: 512);

        [TestMethod]
        public void EachStrokeIsUndoneSeparatelyNewestFirst()
        {
            PaintCanvas canvas = NewCanvas();
            PaintUndoHistory history = new();

            byte[] before = PaintUndoHistory.Snapshot(canvas);
            canvas.PaintDisc(new Vector2(20, 20), 5, PaintCanvas.Foreground);
            history.Push(canvas, before);
            byte[] afterFirst = PaintUndoHistory.Snapshot(canvas);

            before = PaintUndoHistory.Snapshot(canvas);
            canvas.PaintDisc(new Vector2(60, 60), 5, PaintCanvas.Background);
            history.Push(canvas, before);

            Assert.IsTrue(history.TryUndo(out PaintCanvas? restored));
            Assert.AreSame(canvas, restored);
            CollectionAssert.AreEqual(afterFirst, canvas.Pixels);
            Assert.AreEqual(PaintCanvas.Foreground, canvas.ValueAt(new Vector2(20, 20)));

            Assert.IsTrue(history.TryUndo(out _));
            Assert.IsFalse(canvas.HasPaint);
            Assert.IsFalse(history.TryUndo(out _));
        }

        [TestMethod]
        public void UndoBringsBackTheCanvasTheStrokeWasPaintedOnEvenAfterItGrew()
        {
            PaintCanvas small = NewCanvas();
            PaintUndoHistory history = new();

            byte[] before = PaintUndoHistory.Snapshot(small);
            small.PaintDisc(new Vector2(20, 20), 5, PaintCanvas.Foreground);
            history.Push(small, before);

            PaintCanvas grown = small.GrownToInclude(new Rectangle(-200, 300, -200, 300), 512);
            Assert.AreNotSame(small, grown);

            Assert.IsTrue(history.TryUndo(out PaintCanvas? restored));
            Assert.AreSame(small, restored);
            Assert.IsFalse(small.HasPaint);
        }

        [TestMethod]
        public void TheOldestEntriesGoWhenTheHistoryIsFull()
        {
            PaintCanvas canvas = NewCanvas();
            PaintUndoHistory history = new(maxEntries: 2);

            for (int i = 0; i < 4; i++)
                history.Push(canvas, PaintUndoHistory.Snapshot(canvas));

            Assert.AreEqual(2, history.Count);
        }
    }
}
