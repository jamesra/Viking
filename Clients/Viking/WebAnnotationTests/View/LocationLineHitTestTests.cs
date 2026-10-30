using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.SqlServer.Types;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using WebAnnotation.UI;
using WebAnnotation.UI.Actions;
using WebAnnotation;
using WebAnnotation.View;
using WebAnnotationModel;
using Viking.VolumeModel;
using GridVector2 = Geometry.Vector2;

namespace WebAnnotationTests.View
{
    /// <summary>
    /// Pins the "is this world point within half a line width of the polyline" rule shared by
    /// <see cref="LocationLineViewBase"/> (control-point polyline) and <see cref="LocationCurveView"/>
    /// (interpolated curve points).
    /// </summary>
    [TestClass]
    public class LocationLineHitTestTests
    {
        private static readonly GridVector2[] Control = [new(0, 0), new(10, 0)];

        // Control polyline is flat along y = 0; the curve bulges up through (5, 5).
        private static readonly GridVector2[] Curve = [new(0, 0), new(5, 5), new(10, 0)];

        [TestMethod]
        public void LineHitsOnlyWithinHalfLineWidthOfControlSegments()
        {
            StubLine view = Create<StubLine>(Control, lineWidth: 2);

            Assert.IsTrue(view.Hit(new GridVector2(5, 0.5)));
            Assert.IsFalse(view.Hit(new GridVector2(5, 1.5)));
            Assert.IsFalse(view.Hit(new GridVector2(5, 5)));
        }

        [TestMethod]
        public void CurveHitsAlongTheInterpolatedCurveNotTheControlPolyline()
        {
            StubCurve view = Create<StubCurve>(Control, lineWidth: 2);
            view.CurvePoints = Curve;

            Assert.IsTrue(view.Hit(new GridVector2(5, 5)), "on the curve, far from the control polyline");
            Assert.IsTrue(view.Hit(new GridVector2(2.5, 2.5)), "on the first curve segment");
            Assert.IsFalse(view.Hit(new GridVector2(5, 0)), "on the control polyline, far from the curve");
        }

        /// <summary>
        /// Hit ranking reads <see cref="LocationCanvasView.DistanceFromCenterNormalized"/> as 0 on the centreline and 1 at the
        /// line's edge. A line used to return half-width minus distance, so with a width over 2 a click on the centreline scored
        /// above 1 and the line fell out of the "mouse is over it" set.
        /// </summary>
        [TestMethod]
        public void LineDistanceIsZeroOnTheCentrelineAndGrowsToOneAtTheEdge()
        {
            StubLine view = Create<StubLine>(Control, lineWidth: 4);

            Assert.AreEqual(0.0, view.DistanceFromCenterNormalized(new GridVector2(5, 0)), 1e-12);
            Assert.AreEqual(0.5, view.DistanceFromCenterNormalized(new GridVector2(5, 1)), 1e-12);
            Assert.AreEqual(0.95, view.DistanceFromCenterNormalized(new GridVector2(5, -1.9)), 1e-12);
        }

        [TestMethod]
        public void CurveDistanceIsMeasuredFromTheInterpolatedCurve()
        {
            StubCurve view = Create<StubCurve>(Control, lineWidth: 4);
            view.CurvePoints = Curve;

            Assert.AreEqual(0.0, view.DistanceFromCenterNormalized(new GridVector2(5, 5)), 1e-12);
            Assert.AreEqual(0.5, view.DistanceFromCenterNormalized(new GridVector2(5, 6)), 1e-12);
        }

        /// <summary>
        /// A wide line clicked on its centreline is picked over a circle on the adjacent section, and the nearer of two lines wins.
        /// </summary>
        [TestMethod]
        public void LineClickedOnItsCentrelineIsSelectedOnTheCurrentSection()
        {
            const int section = 10;
            // Long enough that the click is outside the control point circles and scores against the segment.
            GridVector2[] longLine = [new(-50, 0), new(50, 0)];
            StubLine line = Create<StubLine>(longLine, lineWidth: 16);
            StubLine adjacent = Create<StubLine>(longLine, lineWidth: 16);
            GridVector2 click = new(5, 0);

            HitTestResult hit = new List<HitTestResult>
            {
                new(adjacent, section + 1, 0, 0),
                new(line, section, 0, line.DistanceFromCenterNormalized(click)),
            }.NearestObjectOnCurrentSectionThenAdjacent(section);
            Assert.AreSame(line, hit.obj);

            StubLine nearer = Create<StubLine>([new(-50, 1), new(50, 1)], lineWidth: 16);
            StubLine farther = Create<StubLine>([new(-50, 6), new(50, 6)], lineWidth: 16);
            hit = new List<HitTestResult>
            {
                new(farther, section, 0, farther.DistanceFromCenterNormalized(click)),
                new(nearer, section, 0, nearer.DistanceFromCenterNormalized(click)),
            }.NearestObjectOnCurrentSectionThenAdjacent(section);
            Assert.AreSame(nearer, hit.obj);
        }

        private static T Create<T>(GridVector2[] controlPoints, double lineWidth) where T : LocationLineViewBase
        {
            T view = (T)FormatterServices.GetUninitializedObject(typeof(T));
            FieldInfo? field = typeof(MultipleControlPointLocationCanvasViewBase).GetField(
                "VolumeControlPoints", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(view, controlPoints);
            ((IStub)view).Width = lineWidth;
            return view;
        }

        private interface IStub
        {
            double Width { get; set; }
        }

        private class StubLine : LocationLineViewBase, IStub
        {
            public StubLine() : base(null!, null!) { }
            public double Width { get; set; }
            public bool Hit(GridVector2 p) => PointIntersectsAnyLineSegment(p);
            public override double LineWidth => Width;
            public override double ControlPointRadius => Width / 2.0;
            public override SqlGeometry VolumeShapeAsRendered => throw new System.NotSupportedException();
            public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys ModifierKeys, out long LocationID) => throw new System.NotSupportedException();
            public override LocationAction GetPenContactActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys ModifierKeys, out long LocationID) => throw new System.NotSupportedException();
            public override List<IAction> GetPenActionsForShapeAnnotation(Geometry.Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber) => throw new System.NotSupportedException();
            public override string[] HelpStrings => [];
        }

        private sealed class StubCurve : LocationCurveView, IStub
        {
            public StubCurve() : base(null!, null!) { }
            public double Width { get; set; }
            public GridVector2[] CurvePoints { get; set; } = [];
            public bool Hit(GridVector2 p) => PointIntersectsAnyLineSegment(p);
            public override double LineWidth => Width;
            public override double ControlPointRadius => Width / 2.0;
            public override GridVector2[] VolumeCurveControlPoints => CurvePoints;
            public override Color Color { get; set; }
            public override float Alpha { get; set; }
            public override SqlGeometry VolumeShapeAsRendered => throw new System.NotSupportedException();
            public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys ModifierKeys, out long LocationID) => throw new System.NotSupportedException();
            public override LocationAction GetPenContactActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys ModifierKeys, out long LocationID) => throw new System.NotSupportedException();
            public override List<IAction> GetPenActionsForShapeAnnotation(Geometry.Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber) => throw new System.NotSupportedException();
            public override string[] HelpStrings => [];
        }
    }
}
