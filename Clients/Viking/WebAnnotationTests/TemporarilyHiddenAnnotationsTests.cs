using AnnotationService.Types;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Viking.AnnotationServiceTypes;
using WebAnnotation;
using WebAnnotation.UI;
using WebAnnotation.UI.Actions;
using WebAnnotation.View;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using GridVector2 = Geometry.Vector2;

namespace WebAnnotationTests
{
    /// <summary>
    /// Del hides what a click under the mouse would pick for a while and Insert brings it back, so an annotation beneath
    /// a large one can be picked. Pins the timing, which view each key picks, the keys every kind of view is hidden under,
    /// and the keys on the keyboard.
    /// </summary>
    [TestClass]
    public class TemporarilyHiddenAnnotationsTests
    {
        private const int Section = 10;

        /// <summary>Mouse position for picks whose hits carry no arrow markers, where it is not read.</summary>
        private static readonly GridVector2 Mouse = new(0, 0);

        private DateTime now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

        private TemporarilyHiddenAnnotations Create() => new(() => now);

        [TestMethod]
        public void AnAnnotationIsHiddenUntilItsTimeRunsOut()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            hidden.Hide(5, TimeSpan.FromSeconds(30));

            Assert.IsTrue(hidden.IsHidden(5));
            now += TimeSpan.FromSeconds(29.9);
            Assert.IsTrue(hidden.IsHidden(5), "still hidden just before 30 s");
            now += TimeSpan.FromSeconds(0.2);
            Assert.IsFalse(hidden.IsHidden(5), "reappears after 30 s");
            Assert.IsTrue(hidden.IsEmpty);
        }

        [TestMethod]
        public void HidingAgainRestartsTheTime()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            hidden.Hide(5, TimeSpan.FromSeconds(30));
            now += TimeSpan.FromSeconds(20);
            hidden.Hide(5, TimeSpan.FromSeconds(30));
            now += TimeSpan.FromSeconds(20);

            Assert.IsTrue(hidden.IsHidden(5));
        }

        [TestMethod]
        public void RestoreOnlyAffectsAHiddenAnnotationAndFadesItInBeforeItIsClickable()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            hidden.Hide(5, TimeSpan.FromSeconds(30));

            Assert.IsFalse(hidden.Restore(6), "6 was never hidden");
            Assert.IsTrue(hidden.Restore(5));
            Assert.IsTrue(hidden.IsHidden(5), "not clickable while it fades in");
            Assert.AreEqual(0f, hidden.FadeIn(5), 1e-6, "fade starts from invisible");

            now += TimeSpan.FromSeconds(1.4);
            Assert.IsTrue(hidden.IsHidden(5));
            now += TimeSpan.FromSeconds(0.2);
            Assert.IsFalse(hidden.IsHidden(5), "clickable once the 1.5 s fade is done");
            Assert.IsFalse(hidden.Restore(5), "already restored");
        }

        [TestMethod]
        public void AnnotationIsInvisibleUntilTheLastFivePercentOfTheHideTime()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            hidden.Hide(5, TimeSpan.FromSeconds(30));

            Assert.AreEqual(1f, hidden.FadeIn(7), "an annotation that is not hidden is fully opaque");
            now += TimeSpan.FromSeconds(28.5);
            Assert.AreEqual(0f, hidden.FadeIn(5), "the fade starts at 95%, from invisible");
            now += TimeSpan.FromSeconds(0.75);
            Assert.AreEqual(Math.Sin(Math.PI / 4), hidden.FadeIn(5), 1e-4, "halfway through the fade is sin(45 degrees)");
            now += TimeSpan.FromSeconds(0.7499);
            Assert.AreEqual(1.0, hidden.FadeIn(5), 1e-3, "nearly the whole quarter wave: almost fully opaque");
            now += TimeSpan.FromSeconds(0.01);
            Assert.AreEqual(1f, hidden.FadeIn(5), "back to normal once it is clickable");
        }

        [TestMethod]
        public void TheFadeRisesAlongAQuarterSineWave()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            hidden.Hide(5, TimeSpan.FromSeconds(100));
            now += TimeSpan.FromSeconds(95);

            float previous = 0f;
            for (int step = 1; step <= 10; step++)
            {
                now += TimeSpan.FromSeconds(0.45);
                float fade = hidden.FadeIn(5);
                Assert.AreEqual(Math.Sin(step / 11.1111111 * Math.PI / 2), fade, 1e-2);
                Assert.IsTrue(fade > previous, "rising");
                previous = fade;
            }
        }

        [TestMethod]
        public void RedrawIsDueWhenTheFadeStartsThenEveryFrameUntilItEnds()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            Assert.IsNull(hidden.TimeUntilNextRedraw());

            hidden.Hide(1, TimeSpan.FromSeconds(30));
            Assert.AreEqual(TimeSpan.FromSeconds(28.5), hidden.TimeUntilNextRedraw());

            now += TimeSpan.FromSeconds(28.5);
            Assert.AreEqual(TemporarilyHiddenAnnotations.FadeFrameInterval, hidden.TimeUntilNextRedraw());

            now += TimeSpan.FromSeconds(1.49);
            Assert.IsTrue(hidden.TimeUntilNextRedraw() < TemporarilyHiddenAnnotations.FadeFrameInterval, "one last frame after the fade ends");
        }

        [TestMethod]
        public void AViewWithoutAlphaAppearsHalfwayThroughTheFade()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            StubLine line = Line(1, Section);
            hidden.Hide(1, TimeSpan.FromSeconds(30));

            Assert.AreEqual(0, hidden.VisibleToDraw([line]).Count, "invisible before the fade");
            now += TimeSpan.FromSeconds(28.5 + 0.2);
            Assert.AreEqual(0, hidden.VisibleToDraw([line]).Count, "sin is still under 0.5");
            now += TimeSpan.FromSeconds(0.4);
            Assert.AreEqual(1, hidden.VisibleToDraw([line]).Count, "sin passes 0.5 a third of the way through the fade");
            now += TimeSpan.FromSeconds(2);
            Assert.AreEqual(1, hidden.VisibleToDraw([line]).Count, "visible after it is back");
        }

        [TestMethod]
        public void ExcludeDropsHiddenAnnotationsFromHits()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            StubLine big = Line(1, Section);
            StubLine under = Line(2, Section + 1);
            hidden.Hide(1, TimeSpan.FromSeconds(30));

            List<HitTestResult> remaining = hidden.Exclude([Hit(big, 0), Hit(under, 0.2)]);

            CollectionAssert.AreEqual(new[] { under }, remaining.Select(h => h.obj).ToArray());
        }

        [TestMethod]
        public void DelPicksTheTopAnnotationOnTheSectionBeforeOneOnAnAdjacentSection()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            StubLine big = Line(1, Section);
            StubLine adjacent = Line(2, Section + 1);

            Assert.AreSame(big, hidden.PickToHide([Hit(adjacent, 0.1), Hit(big, 0.9)], Section, Mouse));
            Assert.AreSame(adjacent, hidden.PickToHide([Hit(adjacent, 0.1)], Section, Mouse), "with nothing of this section there, the adjacent one");
        }

        [TestMethod]
        public void InsertBringsBackAHiddenAnnotationOnAnAdjacentSection()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            StubLine adjacent = Line(2, Section - 1);
            hidden.Hide(2, TimeSpan.FromSeconds(30));

            Assert.AreSame(adjacent, hidden.PickToRestore([Hit(adjacent, 0.3)], Mouse));
        }

        [TestMethod]
        public void DelSkipsAnnotationsAlreadyHiddenAndPicksTheNextOneDown()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            StubLine top = Line(1, Section);
            StubLine next = Line(2, Section);
            hidden.Hide(1, TimeSpan.FromSeconds(30));

            Assert.AreSame(next, hidden.PickToHide([Hit(top, 0.1), Hit(next, 0.5)], Section, Mouse));
        }

        [TestMethod]
        public void DelIgnoresAnAnnotationTheMouseIsNotOver()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            StubLine hole = Line(1, Section);

            Assert.IsNull(hidden.PickToHide([Hit(hole, 1.4)], Section, Mouse), "distance above 1 is outside the shape");
        }

        [TestMethod]
        public void InsertPicksTheNearestHiddenAnnotationUnderTheMouse()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            StubLine far = Line(1, Section);
            StubLine near = Line(2, Section);
            StubLine visible = Line(3, Section);
            hidden.Hide(1, TimeSpan.FromSeconds(30));
            hidden.Hide(2, TimeSpan.FromSeconds(30));

            Assert.AreSame(near, hidden.PickToRestore([Hit(far, 0.8), Hit(near, 0.2), Hit(visible, 0.0)], Mouse));
            Assert.IsNull(hidden.PickToRestore([Hit(visible, 0.0)], Mouse));
        }

        [TestMethod]
        [DataRow(Keys.Delete, true)]
        [DataRow(Keys.Insert, true)]
        [DataRow(Keys.Shift | Keys.Delete, false, DisplayName = "Shift+Del is left alone")]
        [DataRow(Keys.Control | Keys.Insert, false, DisplayName = "Ctrl+Insert (copy) is left alone")]
        public void DelAndInsertAreOverlayKeys(Keys keyData, bool expected)
        {
            Assert.AreEqual(expected, OverlayHotkeys.IsReserved(keyData));
        }

        [TestMethod]
        public void ALinkIsHiddenUntilItsTimeRunsOut()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            LocationLinkKey key = new(1, 2);
            hidden.Hide(key, TimeSpan.FromSeconds(30));

            Assert.IsTrue(hidden.IsHidden(key));
            Assert.IsFalse(hidden.IsHidden(1), "hiding a link leaves its locations alone");
            Assert.AreEqual(0f, hidden.FadeIn(key));
            now += TimeSpan.FromSeconds(30.1);
            Assert.IsFalse(hidden.IsHidden(key));
            Assert.IsTrue(hidden.IsEmpty);
        }

        /// <summary>A link to a hidden location would point at nothing, so it hides and fades back in with the location.</summary>
        [TestMethod]
        public void ALinkIsHiddenWhileEitherOfItsLocationsIs()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            LocationLinkKey key = new(1, 2);
            hidden.Hide(2, TimeSpan.FromSeconds(30));

            Assert.IsTrue(hidden.IsHidden(key));
            Assert.AreEqual(0f, hidden.FadeIn(key));
            Assert.IsFalse(hidden.Restore(key), "the link itself was not hidden");
            Assert.IsFalse(hidden.IsHidden(new LocationLinkKey(1, 3)));
        }

        [TestMethod]
        public void HiddenLinksAreNeitherHitNorDrawn()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            LocationLinkView shown = LocationLinkVisibilityTests.CreateLink(a: 1, b: 2);
            LocationLinkView gone = LocationLinkVisibilityTests.CreateLink(a: 3, b: 4);
            hidden.Hide(gone.Key, TimeSpan.FromSeconds(30));

            List<HitTestResult> remaining = hidden.Exclude([LinkHit(shown, 0.2), LinkHit(gone, 0.1)]);
            CollectionAssert.AreEqual(new object[] { shown }, remaining.Select(h => h.obj).ToArray());
            CollectionAssert.AreEqual(new[] { shown }, hidden.VisibleToDraw([shown, gone]));
        }

        [TestMethod]
        public void DelHidesALinkWhenItIsWhatAClickWouldPick()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            LocationLinkView link = LocationLinkVisibilityTests.CreateLink();

            Assert.AreSame(link, hidden.PickToHide([LinkHit(link, 0.4)], Section, Mouse));
            Assert.IsNull(hidden.PickToHide([LinkHit(link, 1.4)], Section, Mouse), "the mouse is not over it");

            hidden.Hide(link.Key, TimeSpan.FromSeconds(30));
            Assert.IsNull(hidden.PickToHide([LinkHit(link, 0.4)], Section, Mouse), "already hidden");
        }

        [TestMethod]
        public void InsertBringsBackAHiddenLinkUnderTheMouse()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            LocationLinkView link = LocationLinkVisibilityTests.CreateLink();
            hidden.Hide(link.Key, TimeSpan.FromSeconds(30));

            Assert.AreSame(link, hidden.PickToRestore([LinkHit(link, 0.4)], Mouse));
            Assert.IsTrue(hidden.Restore(link.Key));
            now += TimeSpan.FromSeconds(30 * TemporarilyHiddenAnnotations.FadeFraction + 0.1);
            Assert.IsFalse(hidden.IsHidden(link.Key));
        }

        [TestMethod]
        public void ALinkFadingBackInIsDrawnFainterUntilTheDrawIsDone()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            LocationLinkView link = LocationLinkVisibilityTests.CreateLink();
            hidden.Hide(link.Key, TimeSpan.FromSeconds(30));
            now += TimeSpan.FromSeconds(30 * (1 - (TemporarilyHiddenAnnotations.FadeFraction / 2)));

            using (hidden.ApplyFade([link]))
            {
                Assert.IsTrue(link.Alpha > 0f && link.Alpha < 1f);
            }

            Assert.AreEqual(1f, link.Alpha, 1e-6);
        }

        private static HitTestResult LinkHit(LocationLinkView link, double distance) => new(link, Section, 0, distance);

        /// <summary>
        /// A link to an overlapping location on an adjacent section is drawn as an arrow marker inside the location. Del on
        /// the marker hides that link, not the location, and Insert there brings it back.
        /// </summary>
        [TestMethod]
        public void DelAndInsertActOnTheArrowMarkerUnderTheMouseNotItsLocation()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            OverlappedLocationLinkView up = Marker(1, 2, new GridVector2(0, 5));
            OverlappedLocationLinkView down = Marker(1, 3, new GridVector2(0, -5));
            StubMarkerHost host = Host(1, up, down);
            HitTestResult[] hits = [Hit(host, 0.5)];
            GridVector2 onUp = new(0, 5.2);

            Assert.AreSame(up, hidden.PickToHide(hits, Section, onUp));
            Assert.AreSame(host, hidden.PickToHide(hits, Section, new GridVector2(0, 0)), "between the markers the location wins");

            hidden.Hide(new LocationLinkKey(1, 2), TimeSpan.FromSeconds(30));
            Assert.AreSame(host, hidden.PickToHide(hits, Section, onUp), "the hidden marker is gone, so Del goes to the location");
            CollectionAssert.AreEqual(new[] { down }, hidden.VisibleToDraw([up, down]));

            Assert.AreSame(up, hidden.PickToRestore(hits, onUp));
            Assert.IsNull(hidden.PickToRestore(hits, new GridVector2(0, -5)), "the down marker was never hidden");
        }

        /// <summary>
        /// A polygon's marker can sit outside its outline. Once the marker is hidden the polygon is not hit there, so Insert
        /// also looks in the locations whose bounding box holds the mouse.
        /// </summary>
        [TestMethod]
        public void InsertFindsAHiddenMarkerWhoseLocationIsNotHit()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            OverlappedLocationLinkView up = Marker(1, 2, new GridVector2(0, 5));
            StubMarkerHost host = Host(1, up);
            hidden.Hide(new LocationLinkKey(1, 2), TimeSpan.FromSeconds(30));

            Assert.IsNull(hidden.PickToRestore([], new GridVector2(0, 5)));
            Assert.AreSame(up, hidden.PickToRestore([], new GridVector2(0, 5), [host]));
        }

        [TestMethod]
        public void DelHidesAStructureLinkAndItHidesWithEitherOfItsLocations()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            StructureLinkCirclesView link = StructureLink(source: 1, target: 2);
            HitTestResult[] hits = [new(link, Section, 0, 0.3)];

            Assert.AreSame(link, hidden.PickToHide(hits, Section, Mouse));
            Assert.IsTrue(hidden.Hide(link, TimeSpan.FromSeconds(30)));
            Assert.IsTrue(hidden.IsHidden(link));
            Assert.IsFalse(hidden.IsHidden(1), "hiding a structure link leaves its locations alone");
            Assert.AreEqual(0, hidden.Exclude(hits).Count);
            Assert.AreSame(link, hidden.PickToRestore(hits, Mouse));

            TemporarilyHiddenAnnotations byLocation = Create();
            byLocation.Hide(2, TimeSpan.FromSeconds(30));
            Assert.IsTrue(byLocation.IsHidden(link), "it would point at a hidden location");
            Assert.IsNull(byLocation.PickToRestore(hits, Mouse), "the link itself was not hidden");
        }

        [TestMethod]
        public void StructureLinksAndPolylinesFadeByTheirAlpha()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            StructureLinkCirclesView link = StructureLink(source: 1, target: 2);
            float original = link.alpha;
            hidden.Hide(link, TimeSpan.FromSeconds(30));
            now += TimeSpan.FromSeconds(30 * (1 - (TemporarilyHiddenAnnotations.FadeFraction / 2)));

            Assert.AreEqual(1, hidden.VisibleToDraw([link]).Count, "a view with an alpha is drawn as soon as its fade starts");
            using (hidden.ApplyFade([link]))
            {
                Assert.IsTrue(link.alpha > 0f && link.alpha < original);
            }

            Assert.AreEqual(original, link.alpha, 1f / 255);
            Assert.IsInstanceOfType(FormatterServices.GetUninitializedObject(typeof(LocationLineView)), typeof(VikingXNAGraphics.IColorView), "polylines fade too");
            Assert.IsInstanceOfType(FormatterServices.GetUninitializedObject(typeof(AdjacentLocationLineView)), typeof(VikingXNAGraphics.IColorView));
        }

        /// <summary>Rings share the store with locations, so a ring's key cannot collide with the location it came from.</summary>
        [TestMethod]
        public void ARingIsHiddenUnderItsOwnKey()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            hidden.Hide(new TemporarilyHiddenAnnotations.RingKey(5), TimeSpan.FromSeconds(30));

            Assert.IsTrue(hidden.IsHidden(new TemporarilyHiddenAnnotations.RingKey(5)));
            Assert.IsFalse(hidden.IsHidden(5), "location 5 is not hidden by its ring");
            Assert.IsNull(TemporarilyHiddenAnnotations.KeyOf(new object()), "Del cannot hide an unknown view");
        }

        /// <summary>A structure link between two locations, skipping the constructor (which reads the store and maps the shapes).</summary>
        private static StructureLinkCirclesView StructureLink(long source, long target)
        {
            StructureLinkCirclesView link = (StructureLinkCirclesView)FormatterServices.GetUninitializedObject(typeof(StructureLinkCirclesView));
            FieldInfo model = typeof(StructureLinkViewModelBase).GetField("modelObj", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(model);
            model.SetValue(link, new StructureLinkObj(source + 100, target + 100, false));
            link.SourceLocation = new LocationObj(new Location { ID = source, ParentID = source + 100, Position = new AnnotationPoint(0, 0, Section) });
            link.TargetLocation = new LocationObj(new Location { ID = target, ParentID = target + 100, Position = new AnnotationPoint(0, 0, Section) });
            link.lineView = new VikingXNAGraphics.LineView(new GridVector2(0, 0), new GridVector2(10, 0), 1, new Microsoft.Xna.Framework.Color(255, 255, 255, 128), VikingXNAGraphics.LineStyle.Standard);
            return link;
        }

        [TestMethod]
        public void AnArrowMarkerGetsTheLinkContextMenu()
        {
            using ContextMenuStrip menu = new LocationLink_CanvasContextMenuView().BuildMenuFor(Marker(1, 2, new GridVector2(0, 5)), new ContextMenuStrip());

            CollectionAssert.AreEqual(new[] { "Delete Link", "Split structure" },
                menu.Items.OfType<ToolStripMenuItem>().Select(i => i.Text).ToArray());
            Assert.IsInstanceOfType(menu.Items[0], typeof(ToolStripMenuItem), "no separator above the first item");
        }

        /// <summary>A marker of radius 1 at <paramref name="center"/>, skipping the constructor (which reads the linked location's structure).</summary>
        private static OverlappedLocationLinkView Marker(long locationId, long linkedId, GridVector2 center)
        {
            OverlappedLocationLinkView marker = (OverlappedLocationLinkView)FormatterServices.GetUninitializedObject(typeof(OverlappedLocationLinkView));
            SetField(marker, "circleView", new VikingXNAGraphics.TextureCircleView(null!, new Geometry.Circle(center, 1), Microsoft.Xna.Framework.Color.White));
            SetField(marker, "linkKey", new LocationLinkKey(locationId, linkedId));
            marker.LocationID = locationId;
            return marker;
        }

        private static StubMarkerHost Host(long id, params OverlappedLocationLinkView[] markers)
        {
            StubMarkerHost host = Line<StubMarkerHost>(id, Section);
            OverlappedLinkCircleView links = (OverlappedLinkCircleView)FormatterServices.GetUninitializedObject(typeof(OverlappedLinkCircleView));
            SetField(links, "linkViews", new List<OverlappedLocationLinkView>(markers));
            host.OverlappedLinkView = links;
            return host;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        [TestMethod]
        public void TheDefaultHideTimeIsThirtySeconds()
        {
            Assert.AreEqual(30, Global.AnnotationSettings.DEFAULT_HIDE_ANNOTATION_SECONDS);
        }

        private static HitTestResult Hit(StubLine view, double distance) => new(view, (int)view.Z, 0, distance);

        /// <summary>
        /// A line view that skips the constructor (which needs a real shape and transform) and only knows its location,
        /// which is all the ID and Z the hit ranking reads.
        /// </summary>
        private static StubLine Line(long id, int section) => Line<StubLine>(id, section);

        private static T Line<T>(long id, int section) where T : StubLine
        {
            T view = (T)FormatterServices.GetUninitializedObject(typeof(T));
            FieldInfo field = typeof(LocationCanvasView).GetField("modelObj", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(view, new LocationObj(new Location { ID = id, ParentID = id, Position = new AnnotationPoint(0, 0, section) }));
            return view;
        }

        /// <summary>A location that draws arrow markers, like a circle, polygon or closed curve, and returns the one under the mouse.</summary>
        private sealed class StubMarkerHost : StubLine, IOverlappedLinkHost, ICanvasViewContainer
        {
            public OverlappedLinkCircleView? OverlappedLinkView { get; set; }

            public ICanvasView GetAnnotationAtPosition(GridVector2 position) => OverlappedLinkView?.GetAnnotationAtPosition(position) ?? this;
        }

        private class StubLine : LocationLineViewBase
        {
            public StubLine() : base(null!, null!) { }

            public override double LineWidth => 2;
            public override double ControlPointRadius => 1;
            public override SqlGeometry VolumeShapeAsRendered => throw new NotSupportedException();
            public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, Keys ModifierKeys, out long LocationID) => throw new NotSupportedException();
            public override LocationAction GetPenContactActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, Keys ModifierKeys, out long LocationID) => throw new NotSupportedException();
            public override List<IAction> GetPenActionsForShapeAnnotation(Geometry.Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber) => throw new NotSupportedException();
            public override string[] HelpStrings => [];
        }
    }
}
