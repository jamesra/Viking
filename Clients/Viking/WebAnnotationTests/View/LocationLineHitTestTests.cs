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
            public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID) => throw new System.NotSupportedException();
            public override LocationAction GetPenContactActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID) => throw new System.NotSupportedException();
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
            public override GridVector2[] MosaicCurveControlPoints => CurvePoints;
            public override GridVector2[] VolumeCurveControlPoints => CurvePoints;
            public override Color Color { get; set; }
            public override float Alpha { get; set; }
            public override SqlGeometry VolumeShapeAsRendered => throw new System.NotSupportedException();
            public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID) => throw new System.NotSupportedException();
            public override LocationAction GetPenContactActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID) => throw new System.NotSupportedException();
            public override List<IAction> GetPenActionsForShapeAnnotation(Geometry.Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber) => throw new System.NotSupportedException();
            public override string[] HelpStrings => [];
        }
    }
}
