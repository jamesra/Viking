using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using VikingXNA;
using VikingXNAGraphics;
using WebAnnotation.UI;
using WebAnnotation.UI.Actions;
using WebAnnotationModel;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.View
{
    internal class LocationClosedCurveView : LocationCurveView, ILabelView, ICanvasViewContainer, Viking.Common.IHelpStrings, IOverlappedLinkHost
    {
        public CurveView curveView;

        public StructureLabelsBase? curveLabels;
        public OverlappedLinkCircleView? OverlappedLinkView;

        OverlappedLinkCircleView? IOverlappedLinkHost.OverlappedLinkView => OverlappedLinkView;

        /// <summary>Guards the label layout inputs and the views built from them; links are set on the UI thread.</summary>
        private readonly object labelSync = new();

        /// <summary>Inscribed circle and runs of the rendered curve's polygon; null until the constructor makes it.</summary>
        private LabelLayout? labelLayout;

        private ICollection<long> overlappedLinks = [];

        public override string[] HelpStrings
        {
            get
            {
                List<string> listStrings = [.. base.HelpStrings, "Hold Left Click and drag near label: Move all control points", "Hold Left Click and drag near edge: Create link"];
                return [.. listStrings];
            }
        }

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
        /// Interpolation count for new closed-curve views. Read when a view is constructed so loading the type does not touch volume settings.
        /// </summary>
        public static uint NumInterpolationPoints => Global.NumClosedCurveInterpolationPoints;
        public LocationClosedCurveView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(obj, mapper)
        {
            _ControlPointRadius = Global.DefaultClosedLineWidth / 2.0;
            bool hasParent = obj.Parent?.ParentID.HasValue ?? false;
            float opacity = Global.AnnotationSettings.GetOpacityForAnnotationType(obj.TypeCode, hasParent);
            Color color = obj.Parent is null ? Color.Gray.SetAlpha(opacity) : ColorForStructure(obj.Parent, opacity);
            curveView = new CurveView(VolumeControlPoints, color, true, lineWidth: VolumeControlPoints.MinDistanceBetweenAnyPoints(), controlPointRadius: ControlPointRadius, lineStyle: LineStyle.HalfTube, numInterpolations: NumInterpolationPoints);

            long layoutStart = System.Diagnostics.Stopwatch.GetTimestamp();
            Polygon rendered;
            try
            {
                rendered = VolumeShapeAsRendered.ToPolygon();
            }
            catch (ArgumentException e)
            {
                // The curve through the control points is not a valid ring; draw it without labels or link markers
                System.Diagnostics.Trace.WriteLine($"Closed curve {ID} has no label layout: {e.Message}");
                return;
            }

            LabelLayout layout = new(rendered);
            LabelPlacementKind labelKind;
            lock (labelSync)
            {
                labelLayout = layout;
                labelKind = CreateLabelObjects();
            }

            PolygonViewTimingLog.RecordLabelLayout(null, ID, System.Diagnostics.Stopwatch.GetTimestamp() - layoutStart,
                rendered.TotalUniqueVertices, labelKind.ToString());
        }

        /// <summary>
        /// Maximum inscribed circle of the rendered curve's polygon, or a control-point-sized circle at the curve's centre
        /// when the curve is not a valid ring.
        /// </summary>
        protected Circle InscribedCircle => labelLayout?.InscribedCircle ?? new Circle(BoundingBox.Center, ControlPointRadius);

        /// <summary>
        /// Rebuilds the labels and overlapped-link markers together, since the fit depends on the link counts. Eases the
        /// text from the labels it replaces to the new placement.
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

        private Geometry.Vector2[] _MosaicCurveControlPoints;
        public override Geometry.Vector2[] MosaicCurveControlPoints
        {
            get
            {
                _MosaicCurveControlPoints ??= [.. MosaicControlPoints.CalculateCurvePoints(LocationOpenCurveView.NumInterpolationPoints, true)];

                return _MosaicCurveControlPoints;
            }
        }

        private Geometry.Vector2[] _VolumeCurveControlPoints;
        public override Geometry.Vector2[] VolumeCurveControlPoints
        {
            get
            {
                _VolumeCurveControlPoints ??= [.. VolumeControlPoints.CalculateCurvePoints(LocationOpenCurveView.NumInterpolationPoints, true)];

                return _VolumeCurveControlPoints;
            }
        }

        private SqlGeometry _RenderedVolumeShape;
        public override SqlGeometry VolumeShapeAsRendered
        {
            get
            {
                _RenderedVolumeShape ??= VolumeCurveControlPoints.ToPolygon();// this.VolumeCurveControlPoints.ToPolyLine().STBuffer(this.Width / 2.0);                    

                return _RenderedVolumeShape;
            }
        }

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
                /*
                if (_RenderedVolumeShapeEnvelope is null)
                    _RenderedVolumeShapeEnvelope = this.VolumeShapeAsRendered.STBuffer(this.lineWidth / 2.0);

                return _RenderedVolumeShapeEnvelope.Envelope();
                */
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
            //ClearOverlappingLinkedLocationCache();

            //CreateViewObjects();
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
