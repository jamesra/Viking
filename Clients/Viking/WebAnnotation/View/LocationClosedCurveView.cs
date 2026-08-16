using Geometry;
using Viking.Input;
using Rectangle = Geometry.Rectangle;
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
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.View
{
    internal class LocationClosedCurveView : LocationCurveView, ILabelView, ICanvasViewContainer, Viking.Common.IHelpStrings
    {
        public CurveView curveView;

        public StructureCircleLabels curveLabels;
        public OverlappedLinkCircleView OverlappedLinkView;

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

        public static uint NumInterpolationPoints = Global.NumClosedCurveInterpolationPoints;
        public LocationClosedCurveView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(obj, mapper)
        {
            _ControlPointRadius = Global.DefaultClosedLineWidth / 2.0;
            bool hasParent = obj.Parent?.ParentID.HasValue ?? false;
            float opacity = Global.AnnotationSettings.GetOpacityForAnnotationType(obj.TypeCode, hasParent);
            Color color = obj.Parent is null ? Color.Gray.SetAlpha(opacity) : obj.Parent.Type.Color.ToXNAColor(opacity);
            curveView = new CurveView(VolumeControlPoints, color, true, lineWidth: VolumeControlPoints.MinDistanceBetweenAnyPoints(), controlPointRadius: ControlPointRadius, lineStyle: LineStyle.HalfTube, numInterpolations: NumInterpolationPoints);
            CreateLabelObjects();
        }

        private Circle? _InscribedCircle;
        protected Circle InscribedCircle
        {
            get
            {
                if (!_InscribedCircle.HasValue)
                {
                    _InscribedCircle = VolumeShapeAsRendered.CalculateInscribedCircle(VolumeControlPoints);
                }

                return _InscribedCircle.Value;
            }
        }



        public void CreateLabelObjects() => curveLabels = new StructureCircleLabels(modelObj, InscribedCircle);

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
        private Rectangle? _BoundingBox;
        public override Rectangle BoundingBox
        {
            get
            {
                if (!_BoundingBox.HasValue)
                {
                    _BoundingBox = Rectangle.Pad(VolumeCurveControlPoints.BoundingBox(), lineWidth / 2.0);
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
            curveLabels.DrawLabel(spriteBatch, font, scene);
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
            protected get
            {
                if (OverlappedLinkView is null)
                {
                    return new long[0];
                }

                return OverlappedLinkView.OverlappedLinks;
            }

            set
            {
                if (value is null || value.Count == 0)
                {
                    OverlappedLinkView = null;
                }

                OverlappedLinkView = new OverlappedLinkCircleView(InscribedCircle, ID, (int)Z, value)
                {
                    Color = Color
                };

                CreateLabelObjects();
            }
        }

        public override LocationAction GetPenContactActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID) => throw new NotImplementedException();

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID)
        {
            Circle TranslateTargetCircle = new(InscribedCircle.Center, InscribedCircle.Radius / 2.0);
            if (TranslateTargetCircle.Covers(WorldPosition))
            {
                LocationID = ID;
                return LocationAction.TRANSLATE;
            }

            return base.GetMouseClickActionForPositionOnAnnotation(WorldPosition, VisibleSectionNumber, modifierKeys, out LocationID);
        }

        internal override void OnParentPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == "Label" || args.PropertyName == "Attributes")
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

        public override List<IAction> GetPenActionsForShapeAnnotation(Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber) => throw new NotImplementedException();
    }
}
