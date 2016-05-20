using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework;
using SqlGeometryUtils;
using WebAnnotationModel;
using VikingXNAGraphics;
using Microsoft.Xna.Framework.Graphics;
using VikingXNA;

namespace WebAnnotation.View
{
    class LocationOpenCurveView : LocationCurveView, IColorView, IRenderedLabelView
    {
        public static uint NumInterpolationPoints = Global.NumOpenCurveInterpolationPoints;
        
        public CurveView curveView;
        public CurveLabel curveLabel;
        public CurveLabel curveParentLabel;

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

        public Color LabelTextColor
        {
            get { return curveLabel.Color; }
            set { curveLabel.Color = value; }
        }

        public float LabelTextAlpha
        {
            get { return curveLabel.Alpha; }
            set { curveLabel.Alpha = value; }
        }

        public float ParentLabelTextAlpha
        {
            get { return curveParentLabel.Alpha; }
            set { curveParentLabel.Alpha = value; }
        }

        public LocationOpenCurveView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(obj, mapper)
        {
            //RegisterForLocationEvents();
            //RegisterForStructureChangeEvents();
            
            curveView = new CurveView(VolumeControlPoints, obj.Parent.Type.Color.ToXNAColor(0.5f), false, lineWidth: obj.Radius * 2.0, lineStyle: LineStyle.Tubular);
            CreateLabelViews(VolumeControlPoints, obj.ParentID);
        }

        private void CreateLabelViews(GridVector2[] controlPoints, long? ParentID)
        {
            string LabelText = this.ParentID.ToString() + " " + this.FullLabelText();

            string ParentStructureLabelText = "";
            if (this.Parent.ParentID.HasValue)
            {
                ParentStructureLabelText = this.Parent.ParentID.ToString();
                LabelText = this.Parent.Type.Code + " " + LabelText;
            }

            Color LabelColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);
            Color ParentLabelColor = new Color(1.0f, 0, 0, 0.5f);

            curveLabel = new CurveLabel(LabelText, controlPoints, LabelColor, false);
            curveParentLabel = new CurveLabel(ParentStructureLabelText, controlPoints, ParentLabelColor, false);

            curveLabel.Alignment = RoundCurve.HorizontalAlignment.Left;
            curveParentLabel.Alignment = RoundCurve.HorizontalAlignment.Right;

            float TotalLabelLength = (float)(curveLabel.Text.Length + 1 + curveParentLabel.Text.Length);
            curveLabel.Max_Curve_Length_To_Use_Normalized = (float)curveLabel.Text.Length / TotalLabelLength;

            curveParentLabel.Max_Curve_Length_To_Use_Normalized = (float)curveParentLabel.Text.Length / TotalLabelLength;
            curveParentLabel.LabelEndDistance = 0.90f;
        }
        
        private GridVector2[] _MosaicCurveControlPoints;
        public override GridVector2[] MosaicCurveControlPoints
        {
            get
            {
                if (_MosaicCurveControlPoints == null)
                {
                    _MosaicCurveControlPoints = CurveViewControlPoints.CalculateCurvePoints(MosaicControlPoints, LocationOpenCurveView.NumInterpolationPoints, false).ToArray();
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
                    _VolumeCurveControlPoints = CurveViewControlPoints.CalculateCurvePoints(VolumeControlPoints, LocationOpenCurveView.NumInterpolationPoints, false).ToArray();
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
                    _RenderedVolumeShape = this.VolumeCurveControlPoints.ToPolyLine().STBuffer(this.LineWidth / 2.0);
                }

                return _RenderedVolumeShape;
            }
        }
        
        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundCurve.CurveManager curveManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect,
                          LocationOpenCurveView[] listToDraw)
        {
            CurveView.Draw(device, scene, curveManager, basicEffect, overlayEffect, 0, listToDraw.Select(l => l.curveView).ToArray());
        }

        /// <summary>
        /// Draw the text for the location at the specified screen coordinates
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="font"></param>
        /// <param name="ScreenDrawPosition">Center of the annotation in screen space, which is the coordinate system used for text</param>
        /// <param name="MagnificationFactor"></param>
        /// <param name="DirectionToVisiblePlane">The Z distance of the location to the plane viewed by user.</param>
        public void DrawLabel(GraphicsDevice device, SpriteBatch spriteBatch, SpriteFont font, Scene scene)
        {
            if (font == null)
                throw new ArgumentNullException("font");

            if (spriteBatch == null)
                throw new ArgumentNullException("spriteBatch");

            RoundCurve.CurveManager curveManager = VikingXNA.DeviceEffectsStore<RoundCurve.CurveManager>.TryGet(device);
            if (curveManager == null)
                return;
            
            CurveLabel.Draw(device, scene, spriteBatch, font, curveManager, new CurveLabel[] { curveLabel, curveParentLabel });
        }

        public override double LineWidth
        {
            get
            {
                return curveView.LineWidth;
            }
        }

        public override double ControlPointRadius
        {
            get
            {
                return LineWidth / 2.0;
            }
        }

        
        internal override void OnParentPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == "Label" || args.PropertyName == "Attributes")
            {
                CreateLabelViews(VolumeControlPoints, this.ParentID);
            }
            
            base.OnParentPropertyChanged(o, args);
        }

        internal override void OnObjPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            if(args.PropertyName == "Attributes")
            {
                CreateLabelViews(VolumeControlPoints, this.ParentID);
            }
            base.OnObjPropertyChanged(o, args);
        }

    }
}
