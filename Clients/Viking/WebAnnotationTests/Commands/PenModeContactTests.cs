using Geometry;
using GeometryTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using WebAnnotation;
using WebAnnotation.UI;
using WebAnnotation.UI.Actions;
using WebAnnotation.View;
using WebAnnotationModel;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Pen Mode contact must start free-draw on an unmodified hit and must not throw
    /// when a view has no pen implementation. Ctrl hole actions stay on the polygon.
    /// </summary>
    [TestClass]
    public class PenModeContactTests
    {
        [TestMethod]
        public void UnimplementedPenOrMouseActionReturnsFalse()
        {
            ThrowingHit hit = new();

            bool pen = LocationActionDispatch.TryGetAction(
                hit, Vector2.Zero, 1, Keys.None, penContact: true, out LocationAction penAction, out long penId);
            bool mouse = LocationActionDispatch.TryGetAction(
                hit, Vector2.Zero, 1, Keys.None, penContact: false, out LocationAction mouseAction, out long mouseId);

            Assert.IsFalse(pen);
            Assert.IsFalse(mouse);
            Assert.AreEqual(LocationAction.NONE, penAction);
            Assert.AreEqual(LocationAction.NONE, mouseAction);
            Assert.AreEqual(0, penId);
            Assert.AreEqual(0, mouseId);
        }

        [TestMethod]
        public void NoneActionLeavesFreeDrawAvailable()
        {
            bool started = LocationActionDispatch.TryGetAction(
                new NoneHit(), Vector2.Zero, 1, Keys.None, penContact: true, out LocationAction action, out _);

            Assert.IsFalse(started);
            Assert.AreEqual(LocationAction.NONE, action);
        }

        [TestMethod]
        public void PolygonPenModeUnmodifiedHitIsNone()
        {
            Polygon polygon = BoxWithHole();

            LocationAction action = PolygonPenModeContact.Action(
                polygon, new Circle(Vector2.Zero, 1), 7, 1, new Vector2(5, 0), 1, Keys.None, out long id);

            Assert.AreEqual(LocationAction.NONE, action);
            Assert.AreEqual(7, id);
        }

        [TestMethod]
        public void PolygonPenModeCtrlOnRingCutsHole()
        {
            Polygon polygon = BoxWithHole();
            Vector2 onRing = new(5, 0);
            Assert.IsTrue(polygon.Covers(onRing));

            LocationAction action = PolygonPenModeContact.Action(
                polygon, new Circle(Vector2.Zero, 1), 7, 1, onRing, 1, Keys.Control, out long id);

            Assert.AreEqual(LocationAction.CUTHOLE, action);
            Assert.AreEqual(7, id);
        }

        [TestMethod]
        public void PolygonPenModeCtrlInHoleRemovesHole()
        {
            Polygon polygon = BoxWithHole();
            Vector2 inHole = polygon.InteriorPolygons[0].Centroid;
            Assert.IsTrue(polygon.InteriorPolygonContains(inHole));

            LocationAction action = PolygonPenModeContact.Action(
                polygon, new Circle(Vector2.Zero, 1), 7, 1, inHole, 1, Keys.Control, out long id);

            Assert.AreEqual(LocationAction.REMOVEHOLE, action);
            Assert.AreEqual(7, id);
        }

        [TestMethod]
        public void ClosedCurveUnmodifiedContactAndEmptyStrokeDoNotThrow()
        {
            LocationClosedCurveView view = ViewWithoutConstructor<LocationClosedCurveView>(new LocationObj());
            Path path = new();

            LocationAction contact = view.GetPenContactActionForPositionOnAnnotation(
                Vector2.Zero, 1, Keys.None, out long id);
            List<IAction> actions = view.GetPenActionsForShapeAnnotation(path, [], 1);

            Assert.AreEqual(LocationAction.NONE, contact);
            Assert.AreEqual(0, id);
            Assert.AreEqual(0, actions.Count);
        }

        [TestMethod]
        public void OverlappedLocationPenContactAndActionsDoNotThrow()
        {
            OverlappedLocationView view = ViewWithoutConstructor<OverlappedLocationView>(new LocationObj());

            LocationAction contact = view.GetPenContactActionForPositionOnAnnotation(
                Vector2.Zero, 1, Keys.None, out _);
            List<IAction> actions = view.GetPenActionsForShapeAnnotation(new Path(), [], 1);

            Assert.AreEqual(LocationAction.NONE, contact);
            Assert.AreEqual(0, actions.Count);
        }

        private static Polygon BoxWithHole()
        {
            Polygon outer = Primitives.BoxPolygon(10);
            outer.AddInteriorRing(Primitives.BoxPolygon(3));
            return outer;
        }

        /// <summary>
        /// Skips view constructors that need mapped geometry or the annotation store.
        /// </summary>
        private static TView ViewWithoutConstructor<TView>(LocationObj obj) where TView : LocationCanvasView
        {
            TView view = (TView)FormatterServices.GetUninitializedObject(typeof(TView));
            FieldInfo? model = typeof(LocationCanvasView).GetField(
                "modelObj",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(model);
            model.SetValue(view, obj);
            return view;
        }

        private sealed class ThrowingHit : IPenActionSupport, IMouseActionSupport
        {
            public LocationAction GetPenContactActionForPositionOnAnnotation(Vector2 WorldPosition, int VisibleSectionNumber, Keys ModifierKeys, out long LocationID)
                => throw new System.NotImplementedException("pen");

            public LocationAction GetMouseClickActionForPositionOnAnnotation(Vector2 WorldPosition, int VisibleSectionNumber, Keys ModifierKeys, out long LocationID)
                => throw new System.NotImplementedException("mouse");

            public List<IAction> GetPenActionsForShapeAnnotation(Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber)
                => throw new System.NotImplementedException("actions");
        }

        private sealed class NoneHit : IPenActionSupport, IMouseActionSupport
        {
            public LocationAction GetPenContactActionForPositionOnAnnotation(Vector2 WorldPosition, int VisibleSectionNumber, Keys ModifierKeys, out long LocationID)
            {
                LocationID = 0;
                return LocationAction.NONE;
            }

            public LocationAction GetMouseClickActionForPositionOnAnnotation(Vector2 WorldPosition, int VisibleSectionNumber, Keys ModifierKeys, out long LocationID)
            {
                LocationID = 0;
                return LocationAction.NONE;
            }

            public List<IAction> GetPenActionsForShapeAnnotation(Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber) => [];
        }
    }
}
