using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;
using Viking.Input;
using WebAnnotation;

namespace WebAnnotationTests
{
    /// <summary>
    /// Exercises IMouseActionSupport + DummyCanvas hit-testing the way AnnotationOverlay
    /// chooses an action for the annotation under the cursor.
    /// </summary>
    [TestClass]
    public class MouseActionSupportTests
    {
        private sealed class DummyMouseActionAnnotation : DummyAnnotation, IMouseActionSupport
        {
            public long LocationId { get; }
            public LocationAction InsideAction { get; set; } = LocationAction.CREATELINKEDLOCATION;

            public DummyMouseActionAnnotation(IShape2D shape, long locationId) : base(shape)
            {
                LocationId = locationId;
            }

            public LocationAction GetMouseClickActionForPositionOnAnnotation(
                Vector2 WorldPosition,
                int VisibleSectionNumber,
                ModifierKeys modifierKeys,
                out long LocationID)
            {
                LocationID = LocationId;
                if (!Contains(WorldPosition))
                    return LocationAction.NONE;
                if (modifierKeys.ShiftOrCtrlPressed())
                    return LocationAction.NONE;
                return InsideAction;
            }
        }

        [TestMethod]
        public void DummyCanvas_HitTest_ReturnsAnnotationUnderCursor()
        {
            var canvas = new DummyCanvas();
            var ann = new DummyMouseActionAnnotation(new Circle(new Vector2(10, 10), 5), locationId: 7);
            canvas.Add(ann);

            var hits = canvas.GetAnnotations(new Vector2(10, 10));
            Assert.AreEqual(1, hits.Count);
            Assert.AreSame(ann, hits[0].obj);

            var misses = canvas.GetAnnotations(new Vector2(100, 100));
            Assert.AreEqual(0, misses.Count);
        }

        [TestMethod]
        public void HitThenGetMouseAction_InsideWithoutModifiers_ReturnsConfiguredAction()
        {
            var canvas = new DummyCanvas();
            var ann = new DummyMouseActionAnnotation(new Circle(new Vector2(0, 0), 20), locationId: 42)
            {
                InsideAction = LocationAction.TRANSLATE
            };
            canvas.Add(ann);

            var hit = canvas.GetAnnotations(new Vector2(5, 5))[0].obj as IMouseActionSupport;
            Assert.IsNotNull(hit);

            var action = hit.GetMouseClickActionForPositionOnAnnotation(
                new Vector2(5, 5), VisibleSectionNumber: 1, ModifierKeys.None, out long locId);

            Assert.AreEqual(LocationAction.TRANSLATE, action);
            Assert.AreEqual(42, locId);
            Assert.AreEqual(Cursors.Hand, action.GetCursor());
        }

        [TestMethod]
        public void HitThenGetMouseAction_WithShift_ReturnsNone()
        {
            var ann = new DummyMouseActionAnnotation(new Circle(new Vector2(0, 0), 20), locationId: 1);
            var action = ann.GetMouseClickActionForPositionOnAnnotation(
                new Vector2(0, 0), 1, ModifierKeys.Shift, out _);
            Assert.AreEqual(LocationAction.NONE, action);
        }
    }
}
