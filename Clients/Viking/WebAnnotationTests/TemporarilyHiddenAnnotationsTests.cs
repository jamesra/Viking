using AnnotationService.Types;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using WebAnnotation;
using WebAnnotation.UI;
using WebAnnotation.UI.Actions;
using WebAnnotation.View;
using WebAnnotationModel;
using GridVector2 = Geometry.Vector2;

namespace WebAnnotationTests
{
    /// <summary>
    /// Del hides the annotation under the mouse on the current section for a while and Insert brings it back, so an
    /// annotation beneath a large one can be picked. Pins the timing, which annotation each key picks, and the keys.
    /// </summary>
    [TestClass]
    public class TemporarilyHiddenAnnotationsTests
    {
        private const int Section = 10;

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
        public void DelPicksTheTopAnnotationOnTheSectionNotOneOnAnAdjacentSection()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            StubLine big = Line(1, Section);
            StubLine adjacent = Line(2, Section + 1);

            Assert.AreSame(big, hidden.PickToHide([Hit(adjacent, 0.1), Hit(big, 0.9)], Section));
            Assert.IsNull(hidden.PickToHide([Hit(adjacent, 0.1)], Section), "nothing of this section under the mouse");
        }

        [TestMethod]
        public void DelSkipsAnnotationsAlreadyHiddenAndPicksTheNextOneDown()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            StubLine top = Line(1, Section);
            StubLine next = Line(2, Section);
            hidden.Hide(1, TimeSpan.FromSeconds(30));

            Assert.AreSame(next, hidden.PickToHide([Hit(top, 0.1), Hit(next, 0.5)], Section));
        }

        [TestMethod]
        public void DelIgnoresAnAnnotationTheMouseIsNotOver()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            StubLine hole = Line(1, Section);

            Assert.IsNull(hidden.PickToHide([Hit(hole, 1.4)], Section), "distance above 1 is outside the shape");
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

            Assert.AreSame(near, hidden.PickToRestore([Hit(far, 0.8), Hit(near, 0.2), Hit(visible, 0.0)], Section));
            Assert.IsNull(hidden.PickToRestore([Hit(visible, 0.0)], Section));
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
        public void TheDefaultHideTimeIsThirtySeconds()
        {
            Assert.AreEqual(30, Global.AnnotationSettings.DEFAULT_HIDE_ANNOTATION_SECONDS);
        }

        private static HitTestResult Hit(StubLine view, double distance) => new(view, (int)view.Z, 0, distance);

        /// <summary>
        /// A line view that skips the constructor (which needs a real shape and transform) and only knows its location,
        /// which is all the ID and Z the hit ranking reads.
        /// </summary>
        private static StubLine Line(long id, int section)
        {
            StubLine view = (StubLine)FormatterServices.GetUninitializedObject(typeof(StubLine));
            FieldInfo field = typeof(LocationCanvasView).GetField("modelObj", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(view, new LocationObj(new Location { ID = id, ParentID = id, Position = new AnnotationPoint(0, 0, section) }));
            return view;
        }

        private sealed class StubLine : LocationLineViewBase
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
