using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VikingXNA;
using VikingXNAGraphics;
using WebAnnotation.UI;
using WebAnnotation.UI.Actions;
using WebAnnotationModel;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace WebAnnotation.View
{
    /// <summary>
    /// Closed curve on the viewed section. The curve itself is built in the constructor; the expensive
    /// <see cref="LabelLayout"/> runs on a background task only when the label would be large enough to read, and
    /// <see cref="IsLabelVisible"/> stays false until that work finishes so the overlay never draws a half-ready label.
    /// </summary>
    internal class LocationClosedCurveView : LocationCurveView, ILabelView, ICanvasViewContainer, Viking.Common.IHelp, IOverlappedLinkHost
    {
        private const int LabelLayoutNotStarted = 0;
        private const int LabelLayoutInFlight = 1;
        private const int LabelLayoutReady = 2;
        private const int LabelLayoutFailed = 3;

        public CurveView curveView;

        public StructureLabelsBase? curveLabels;
        public OverlappedLinkCircleView? OverlappedLinkView;

        OverlappedLinkCircleView? IOverlappedLinkHost.OverlappedLinkView => OverlappedLinkView;

        /// <summary>Guards the label layout inputs and the views built from them; links are set on the UI thread.</summary>
        private readonly object labelSync = new();

        /// <summary>Inscribed circle and runs of the rendered curve's polygon; null until a visible layout task builds it.</summary>
        private LabelLayout? labelLayout;

        /// <summary>0 not started, 1 in flight, 2 ready, 3 failed (invalid ring); see the LabelLayout* constants.</summary>
        private int labelLayoutState = LabelLayoutNotStarted;

        private ICollection<long> overlappedLinks = [];

        public override Viking.Common.HelpEntry[] Help =>
        [
            .. base.Help,
            Viking.Common.HelpEntry.For(Viking.Common.HelpInput.LeftDrag, "Move all control points", where: "near label"),
            Viking.Common.HelpEntry.For(Viking.Common.HelpInput.LeftDrag, "Create link", where: "near edge"),
        ];

        public override Microsoft.Xna.Framework.Color Color
        {
            get => curveView.Color;
            set => curveView.Color = value;
        }

        public override float Alpha
        {
            get => curveView.Alpha;
            set => curveView.Alpha = value;
        }

        private readonly double _ControlPointRadius;

        public override double ControlPointRadius => _ControlPointRadius;

        public double lineWidth = 32;

        /// <summary>
        /// A closed curve on the viewed section: the owner's control points, drawn and hit-tested as the owner's smoothed
        /// <see cref="Viking.VolumeModel.LocationVolumeGeometry.Shape"/>. Label layout is deferred until
        /// <see cref="IsLabelVisible"/> sees a readable size.
        /// </summary>
        public LocationClosedCurveView(LocationObj obj, Viking.VolumeModel.LocationVolumeGeometry geometry) : base(obj, geometry)
        {
            _VolumeCurveControlPoints = geometry.Shape.ToPoints();
            _RenderedVolumeShape = geometry.Outline!;
            _ControlPointRadius = Global.DefaultClosedLineWidth / 2.0;
            bool hasParent = obj.Parent?.ParentID.HasValue ?? false;
            float opacity = Global.AnnotationSettings.GetOpacityForAnnotationType(obj.TypeCode, hasParent);
            Color color = obj.Parent is null ? Color.Gray.SetAlpha(opacity) : ColorForStructure(obj.Parent, opacity);
            curveView = new CurveView(VolumeControlPoints, color, true, lineWidth: VolumeControlPoints.MinDistanceBetweenAnyPoints(), controlPointRadius: ControlPointRadius, lineStyle: LineStyle.HalfTube, numInterpolations: geometry.InterpolationPoints);
        }

        /// <summary>
        /// Maximum inscribed circle of the rendered curve's polygon once labels are laid out, or a control-point-sized
        /// circle at the curve's centre before then (and when the ring is invalid).
        /// </summary>
        protected Circle InscribedCircle => labelLayout?.InscribedCircle ?? new Circle(BoundingBox.Center, ControlPointRadius);

        /// <summary>
        /// Rebuilds the labels and overlapped-link markers together, since the fit depends on the link counts. Eases the
        /// text from the labels it replaces to the new placement. No-op until a layout task has built
        /// <see cref="labelLayout"/>; the slider relayout path skips curves that have not been laid out yet.
        /// </summary>
        /// <returns>Where the label went, Circle if nothing was built.</returns>
        public LabelPlacementKind CreateLabelObjects()
        {
            lock (labelSync)
            {
                if (labelLayout is null)
                {
                    return LabelPlacementKind.Circle;
                }

                PolygonLabelSet labels = PolygonLabelSet.Build(labelLayout, modelObj, ID, (int)Z, overlappedLinks, curveLabels);
                if (labels.LinkView != null)
                {
                    labels.LinkView.Color = Color;
                }

                curveLabels = labels.Labels;
                OverlappedLinkView = labels.LinkView;
                return labels.Kind;
            }
        }

        /// <summary>
        /// False until the background label layout has finished; then the same readability check as other structure labels.
        /// While false, starts a layout task when the annotation is on screen and an estimated circle font would be readable.
        /// Called by <see cref="AnnotationOverlay"/> before each label pass.
        /// </summary>
        public override bool IsLabelVisible(Scene scene)
        {
            RequestLabelLayoutIfNeeded(scene);

            StructureLabelsBase? labels = curveLabels;
            return labels is not null && labels.IsLabelVisible(scene);
        }

        /// <summary>
        /// Starts a single background <see cref="LabelLayout"/> when the curve is on screen and an estimated ID font
        /// (circle font for a bbox-inscribed radius) would be readable. Safe to call every frame; only one task runs.
        /// </summary>
        internal void RequestLabelLayoutIfNeeded(Scene scene)
        {
            if (Volatile.Read(ref labelLayoutState) != LabelLayoutNotStarted)
                return;

            if (!LocationCanvasView.IsPolygonVisible(BoundingBox, scene))
                return;

            // Circle font is R/2; for a circle filling the bbox, R = min(w,h)/2 so font ≈ min/4. Thin processes
            // overestimate, which only starts work early; unreadably small cells still skip.
            double estimatedFont = Math.Min(BoundingBox.Width, BoundingBox.Height) / 4.0;
            if (!LabelView.IsVolumeFontReadable(estimatedFont, BoundingBox.Center, scene))
                return;

            if (Interlocked.CompareExchange(ref labelLayoutState, LabelLayoutInFlight, LabelLayoutNotStarted) != LabelLayoutNotStarted)
                return;

            _ = Task.Run(BuildLabelLayout);
        }

        /// <summary>
        /// Pool-thread body for <see cref="RequestLabelLayoutIfNeeded"/>: polygonise the outline, build
        /// <see cref="LabelLayout"/> and fit labels, then invalidate the overlay. Marks the view failed when the ring is
        /// invalid so the request is not retried every frame.
        /// </summary>
        private void BuildLabelLayout()
        {
            long layoutStart = Stopwatch.GetTimestamp();
            try
            {
                Polygon rendered;
                try
                {
                    rendered = VolumeShapeAsRendered.ToPolygon();
                }
                catch (ArgumentException e)
                {
                    Trace.WriteLine($"Closed curve {ID} has no label layout: {e.Message}");
                    Volatile.Write(ref labelLayoutState, LabelLayoutFailed);
                    return;
                }

                LabelLayout layout = new(rendered);
                LabelPlacementKind labelKind;
                lock (labelSync)
                {
                    labelLayout = layout;
                    labelKind = CreateLabelObjects();
                }

                Volatile.Write(ref labelLayoutState, LabelLayoutReady);
                PolygonViewTimingLog.RecordLabelLayout(null, ID, Stopwatch.GetTimestamp() - layoutStart,
                    rendered.TotalUniqueVertices, labelKind.ToString());
                AnnotationOverlay.CurrentOverlay?.InvalidateParent();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Closed curve {ID} label layout failed: {ex.Message}");
                Volatile.Write(ref labelLayoutState, LabelLayoutFailed);
            }
        }

        private readonly Geometry.Vector2[] _VolumeCurveControlPoints;
        public override Geometry.Vector2[] VolumeCurveControlPoints => _VolumeCurveControlPoints;

        private readonly SqlGeometry _RenderedVolumeShape;

        /// <summary>The owner's <see cref="Viking.VolumeModel.LocationVolumeGeometry.Outline"/>, the ring of the smoothed curve.</summary>
        public override SqlGeometry VolumeShapeAsRendered => _RenderedVolumeShape;

        /// <summary>
        /// We have this because with the current renderings the control points are circles that fall outside the polygon we use to render the closed curves
        /// </summary>
        private Geometry.Rectangle? _BoundingBox;
        public override Geometry.Rectangle BoundingBox
        {
            get
            {
                if (!_BoundingBox.HasValue)
                {
                    _BoundingBox = Geometry.Rectangle.Pad(VolumeCurveControlPoints.BoundingBox(), lineWidth / 2.0);
                }

                return _BoundingBox.Value;
            }
        }

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundCurve.CurveManager lineManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          VikingXNAGraphics.OverlayShaderEffect overlayEffect,
                          LocationClosedCurveView[] listToDraw)
        {
            OverlappedLinkCircleView[] overlappedLocations = [.. listToDraw.Select(l => l.OverlappedLinkView).Where(l => l != null && l.IsVisible(scene))];
            OverlappedLinkCircleView.Draw(device, scene, basicEffect, overlayEffect, overlappedLocations);

            CurveView.Draw(device, scene, lineManager, basicEffect, overlayEffect, 0, [.. listToDraw.Select(l => l.curveView)]);
        }

        public override bool Contains(Geometry.Vector2 Position)
        {
            if (VolumeControlPoints.Any(p => new Circle(p, lineWidth / 2.0).Covers(Position)))
            {
                return true;
            }

            if (OverlappedLinkView != null && OverlappedLinkView.Contains(Position))
            {
                return true;
            }

            return base.Contains(Position);
        }

        public override bool Intersects(LineSegment line)
        {
            if (VolumeControlPoints.Any(p => new Circle(p, lineWidth / 2.0).Intersects(line)))
            {
                return true;
            }

            if (OverlappedLinkView != null && OverlappedLinkView.Intersects(line))
            {
                return true;
            }

            return base.Intersects(line);
        }

        public void DrawLabel(SpriteBatch spriteBatch, SpriteFont font, Scene scene)
        {
            OverlappedLinkView?.DrawLabel(spriteBatch, font, scene);
            curveLabels?.DrawLabel(spriteBatch, font, scene);
        }

        public ICanvasView GetAnnotationAtPosition(Geometry.Vector2 position)
        {
            if (OverlappedLinkView != null)
            {
                ICanvasView containedAnnotation = OverlappedLinkView.GetAnnotationAtPosition(position);
                if (containedAnnotation != null)
                {
                    return containedAnnotation;
                }
            }

            if (Contains(position))
            {
                return this;
            }

            return null;
        }

        public override double LineWidth => curveView.LineWidth;

        public override ICollection<long> OverlappedLinks
        {
            protected get => overlappedLinks;

            set
            {
                lock (labelSync)
                {
                    overlappedLinks = value is null ? [] : [.. value];
                    CreateLabelObjects();
                }
            }
        }

        /// <summary>
        /// Unmodified contact returns <see cref="LocationAction.NONE"/> so pen free-draw starts.
        /// Shift over the inner half of the inscribed circle still translates, matching the mouse path.
        /// Called from <see cref="LocationActionDispatch"/> and the pen cursor.
        /// </summary>
        public override LocationAction GetPenContactActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
        {
            LocationID = ID;
            if (ModifierKeys.ShiftPressed())
            {
                Circle translateTargetCircle = new(InscribedCircle.Center, InscribedCircle.Radius / 2.0);
                if (translateTargetCircle.Covers(WorldPosition))
                {
                    return LocationAction.TRANSLATE;
                }
            }

            return LocationAction.NONE;
        }

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
        {
            Circle TranslateTargetCircle = new(InscribedCircle.Center, InscribedCircle.Radius / 2.0);
            if (TranslateTargetCircle.Covers(WorldPosition))
            {
                LocationID = ID;
                return LocationAction.TRANSLATE;
            }

            return base.GetMouseClickActionForPositionOnAnnotation(WorldPosition, VisibleSectionNumber, ModifierKeys, out LocationID);
        }

        internal override void OnParentPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            if (IsParentPropertyAffectingLabels(args.PropertyName))
            {
                CreateLabelObjects();
            }

            base.OnParentPropertyChanged(o, args);
        }

        internal override void OnObjPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            if (IsLocationPropertyAffectingLabels(args.PropertyName))
            {
                CreateLabelObjects();
            }
        }

        /// <summary>
        /// Offers contour replace and links when a pen stroke ends on this curve, the same family as open curves and polygons.
        /// A stroke with no loop and no border crossings only checks links and does not rebuild the rendered polygon.
        /// Called by the pen free-draw command when the path completes.
        /// </summary>
        public override List<IAction> GetPenActionsForShapeAnnotation(Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber)
        {
            List<IAction> listActions = [];
            if (path is null || interaction_log is null)
            {
                return listActions;
            }

            bool borderCrossings = interaction_log.Any(entry =>
                entry.Annotation == this &&
                (entry.Interaction == AnnotationRegionInteraction.ENTER || entry.Interaction == AnnotationRegionInteraction.EXIT));

            if (path.HasSelfIntersection || borderCrossings)
            {
                Polygon smoothed = VolumeShapeAsRendered.ToPolygon();
                listActions.AddRange(Shared2DShapeActionsForPath.GetPenActionsForShapeAnnotation(
                    this, smoothed, path, interaction_log, VisibleSectionNumber));
            }

            listActions.AddRange(interaction_log.IdentifyPossibleLinkActions(ID));
            return listActions;
        }
    }
}
