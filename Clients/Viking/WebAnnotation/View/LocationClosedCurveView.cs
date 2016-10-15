using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using WebAnnotationModel;
using VikingXNAGraphics;
using System.Windows.Forms;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using VikingXNA;
using System.ComponentModel;

namespace WebAnnotation.View
{
    class LocationClosedCurveView : LocationCurveView, ILabelView, ICanvasViewContainer, Viking.Common.IHelpStrings
    {
        public CurveView curveView;

        public StructureCircleLabels curveLabels;
        public OverlappedLinkCircleView OverlappedLinkView;

        public override string[] HelpStrings
        {
            get
            {
                List<string> listStrings = new List<string>(base.HelpStrings);
                listStrings.Add("Hold Left Click and drag near label: Move all control points");
                listStrings.Add("Hold Left Click and drag near edge: Create link");
                return listStrings.ToArray();
            }
        }

        public override Microsoft.Xna.Framework.Color Color
        {
            get { return curveView.Color; }
            set { curveView.Color = value; }
        }

        public override float Alpha
        {
            get { return curveView.Alpha; }
            set { curveView.Alpha = value; }
        }

        private double _ControlPointRadius;

        public override double ControlPointRadius
        {
            get
            {
                return _ControlPointRadius;
            }
        }


        public double lineWidth = 32;

        public static uint NumInterpolationPoints = Global.NumClosedCurveInterpolationPoints;
        public LocationClosedCurveView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(obj, mapper)
        {
            _ControlPointRadius = Global.DefaultClosedLineWidth / 2.0;
            Color color = obj.Parent == null ? Color.Gray.SetAlpha(0.5f) : obj.Parent.Type.Color.ToXNAColor(0.5f);
            curveView = new CurveView(this.VolumeControlPoints, color, true, lineWidth: this.VolumeControlPoints.MinDistanceBetweenPoints(), controlPointRadius: ControlPointRadius, lineStyle: LineStyle.HalfTube, numInterpolations: NumInterpolationPoints);
            CreateLabelObjects();
        }

        private GridCircle? _InscribedCircle;
        protected GridCircle InscribedCircle
        {
            get
            {
                if(!_InscribedCircle.HasValue)
                {
                    _InscribedCircle = this.VolumeShapeAsRendered.CalculateInscribedCircle(VolumeControlPoints);
                }

                return _InscribedCircle.Value;
            }
        }

        

        public void CreateLabelObjects()
        {
            curveLabels = new StructureCircleLabels(this.modelObj, this.InscribedCircle);
        }

        private GridVector2[] _MosaicCurveControlPoints;
        public override GridVector2[] MosaicCurveControlPoints
        {
            get
            {
                if (_MosaicCurveControlPoints == null)
                {
                    _MosaicCurveControlPoints = this.MosaicControlPoints.CalculateCurvePoints(LocationOpenCurveView.NumInterpolationPoints, true).ToArray();
                }

                return _MosaicCurveControlPoints;
            }
        }

        private GridVector2[] _VolumeCurveControlPoints;
        public override GridVector2[] VolumeCurveControlPoints
        {
            get
            {
                if (_VolumeCurveControlPoints == null)
                {
                    _VolumeCurveControlPoints = this.VolumeControlPoints.CalculateCurvePoints(LocationOpenCurveView.NumInterpolationPoints, true).ToArray();
                }

                return _VolumeCurveControlPoints;
            }
        }

        private SqlGeometry _RenderedVolumeShape;
        public override SqlGeometry VolumeShapeAsRendered
        {
            get
            {
                if (_RenderedVolumeShape == null)
                {
                    _RenderedVolumeShape = this.VolumeCurveControlPoints.ToPolygon();// this.VolumeCurveControlPoints.ToPolyLine().STBuffer(this.Width / 2.0);                    
                }

                return _RenderedVolumeShape;
            }
        }

        /// <summary>
        /// We have this because with the current renderings the control points are circles that fall outside the polygon we use to render the closed curves
        /// </summary>
        private GridRectangle? _BoundingBox;
        public override GridRectangle BoundingBox
        {
            get
            {
                if (!_BoundingBox.HasValue)
                {
                    _BoundingBox = VolumeCurveControlPoints.BoundingBox().Pad(this.lineWidth / 2.0);
                }

                return _BoundingBox.Value;
                /*
                if (_RenderedVolumeShapeEnvelope == null)
                    _RenderedVolumeShapeEnvelope = this.VolumeShapeAsRendered.STBuffer(this.lineWidth / 2.0);

                return _RenderedVolumeShapeEnvelope.Envelope();
                */
            }
        }

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundCurve.CurveManager lineManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect,
                          LocationClosedCurveView[] listToDraw)
        {
            OverlappedLinkCircleView[] overlappedLocations = listToDraw.Select(l => l.OverlappedLinkView).Where(l => l != null && l.IsVisible(scene)).ToArray();
            OverlappedLinkCircleView.Draw(device, scene, basicEffect, overlayEffect, overlappedLocations);

            CurveView.Draw(device, scene, lineManager, basicEffect, overlayEffect, 0, listToDraw.Select(l => l.curveView).ToArray());
        }

        public override bool Intersects(GridVector2 Position)
        {
            if (this.VolumeControlPoints.Any(p => new GridCircle(p, lineWidth / 2.0).Contains(Position)))
                return true;

            if (this.OverlappedLinkView != null && this.OverlappedLinkView.Intersects(Position))
                return true;
            
            return base.Intersects(Position);
        }
        
        public void DrawLabel(SpriteBatch spriteBatch, SpriteFont font, Scene scene)
        {
            if (OverlappedLinkView != null)
            {
                OverlappedLinkView.DrawLabel(spriteBatch, font, scene);
            }
            curveLabels.DrawLabel(spriteBatch, font, scene);
        }

        public ICanvasView GetAnnotationAtPosition(GridVector2 position)
        {
            if (OverlappedLinkView != null)
            {
                ICanvasView containedAnnotation = OverlappedLinkView.GetAnnotationAtPosition(position);
                if (containedAnnotation != null)
                    return containedAnnotation;
            }

            if (this.Intersects(position))
                return this;

            return null;
        }

        public override double LineWidth
        {
            get
            {
                return curveView.LineWidth;
            }
        }

        public override ICollection<long> OverlappedLinks
        {
            protected get
            {
                if (this.OverlappedLinkView == null)
                    return new long[0];

                return this.OverlappedLinkView.OverlappedLinks;
            }

            set
            {
                if (value == null || value.Count == 0)
                {
                    this.OverlappedLinkView = null;
                }

                this.OverlappedLinkView = new OverlappedLinkCircleView(this.InscribedCircle, this.ID, (int)this.Z, value);
                this.OverlappedLinkView.Color = this.Color;

                this.CreateLabelObjects();
            }
        }

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
        {
            GridCircle TranslateTargetCircle = new GridCircle(this.InscribedCircle.Center, this.InscribedCircle.Radius / 2.0);
            if (TranslateTargetCircle.Contains(WorldPosition))
            {
                LocationID = this.ID;
                return LocationAction.TRANSLATE;
            }

            return base.GetMouseClickActionForPositionOnAnnotation(WorldPosition, VisibleSectionNumber, ModifierKeys, out LocationID);
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
                CreateLabelObjects();
        }

    }
}
