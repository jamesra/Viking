using AnnotationService.Types;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public void RestoreBringsBackOnlyAHiddenAnnotation()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            hidden.Hide(5, TimeSpan.FromSeconds(30));

            Assert.IsFalse(hidden.Restore(6), "6 was never hidden");
            Assert.IsTrue(hidden.Restore(5));
            Assert.IsFalse(hidden.IsHidden(5));
            Assert.IsFalse(hidden.Restore(5), "already restored");
        }

        [TestMethod]
        public void NextExpiryIsTheSoonestOfTheHiddenAnnotations()
        {
            TemporarilyHiddenAnnotations hidden = Create();
            Assert.IsNull(hidden.TimeUntilNextExpiry());

            hidden.Hide(1, TimeSpan.FromSeconds(30));
            now += TimeSpan.FromSeconds(10);
            hidden.Hide(2, TimeSpan.FromSeconds(30));

            Assert.AreEqual(TimeSpan.FromSeconds(20), hidden.TimeUntilNextExpiry());
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

        private static StubLine Line(long id, int section) =>
            new(new LocationObj(new Location { ID = id, ParentID = id, Position = new AnnotationPoint(0, 0, section) }));

        private sealed class StubLine(LocationObj obj) : LocationLineViewBase(obj, null!)
        {
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
