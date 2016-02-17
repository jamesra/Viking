using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using WebAnnotationModel;
using VikingXNAGraphics;

namespace WebAnnotation.View
{
    class LocationOpenCurveView : LocationCurveView
    {
        public static uint NumInterpolationPoints = Global.NumCurveInterpolationPoints;

        public CurveView curveView;
        public CurveLabel curveLabel;
        public CurveLabel curveParentLabel;

        public Microsoft.Xna.Framework.Color Color
        {
            get { return curveView.Color; }
            set { curveView.Color = value; }
        }
         
        public LocationOpenCurveView(LocationObj obj) : base(obj)
        {
            RegisterForLocationEvents();
            RegisterForStructureChangeEvents();
            GridVector2[] controlPoints = obj.VolumeShape.ToPoints();
            curveView = new CurveView(controlPoints, obj.Parent.Type.Color.ToXNAColor(0.5f), false, lineWidth: obj.Radius * 2.0);
            CreateLabelViews(obj);
        }

        private void CreateLabelViews(LocationObj obj)
        {
            string LabelText = this.ParentID.ToString() + " " + this.FullLabelText();
            GridVector2[] controlPoints = obj.VolumeShape.ToPoints();
            
            string ParentStructureLabelText = "";
            if (obj.Parent.ParentID.HasValue)
            {
                ParentStructureLabelText = obj.Parent.ParentID.ToString();
                LabelText = this.Parent.Type.Code + " " + LabelText;
            }

            curveLabel = new CurveLabel(LabelText, controlPoints, Microsoft.Xna.Framework.Color.Black, false);
            curveParentLabel = new CurveLabel(ParentStructureLabelText, controlPoints, Microsoft.Xna.Framework.Color.Red, false);

            curveLabel.Alignment = RoundCurve.HorizontalAlignment.Left;
            curveParentLabel.Alignment = RoundCurve.HorizontalAlignment.Right;

            float TotalLabelLength = (float)(curveLabel.Label.Length + 1 + curveParentLabel.Label.Length);
            curveLabel.Max_Curve_Length_To_Use_Normalized = (float)curveLabel.Label.Length / TotalLabelLength;
            curveParentLabel.Max_Curve_Length_To_Use_Normalized = (float)curveParentLabel.Label.Length / TotalLabelLength;
        }
        
        private GridVector2[] _MosaicControlPoints;
        public override GridVector2[] MosaicCurveControlPoints
        {
            get
            {
                if (_MosaicControlPoints == null)
                {
                    _MosaicControlPoints = CurveViewControlPoints.CalculateCurvePoints(modelObj.MosaicShape.ToPoints(), LocationOpenCurveView.NumInterpolationPoints, false).ToArray();
                }

                return _MosaicControlPoints;
            }
        }

        private GridVector2[] _VolumeCurveControlPoints;
        public override GridVector2[] VolumeCurveControlPoints
        {
            get
            {
                if (_VolumeCurveControlPoints == null)
                {
                    _VolumeCurveControlPoints = CurveViewControlPoints.CalculateCurvePoints(modelObj.VolumeShape.ToPoints(), LocationOpenCurveView.NumInterpolationPoints, false).ToArray();
                }

                return _VolumeCurveControlPoints;
            }
        }

        private SqlGeometry _RenderedVolumeShape;
        public override SqlGeometry RenderedVolumeShape
        {
            get
            {
                if (_RenderedVolumeShape == null)
                {
                    _RenderedVolumeShape = this.VolumeCurveControlPoints.ToPolyLine().STBuffer(this.Width / 2.0);
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
        public void DrawLabel(
                                Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                                VikingXNA.Scene scene,
                                Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                                Microsoft.Xna.Framework.Graphics.SpriteFont font,
                                RoundCurve.CurveManager curveManager,
                                Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            if (font == null)
                throw new ArgumentNullException("font");

            if (spriteBatch == null)
                throw new ArgumentNullException("spriteBatch");

            

            CurveLabel.Draw(device, scene, spriteBatch, font, curveManager, basicEffect, new CurveLabel[] { curveLabel, curveParentLabel });
        }

        public override double Width
        {
            get
            {
                return curveView.LineWidth;
            }
        }

        protected bool IsLocationPropertyAffectingLabels(string PropertyName)
        {
            return string.IsNullOrEmpty(PropertyName) ||
                PropertyName == "Terminal" ||
                PropertyName == "OffEdge" ||
                PropertyName == "Attributes";
        }

        protected override void OnObjPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            //ClearOverlappingLinkedLocationCache();

            //CreateViewObjects();
            if (IsLocationPropertyAffectingLabels(args.PropertyName))
                CreateLabelViews(this.modelObj);
        }

        protected override void OnParentPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == "Label" || args.PropertyName == "Attributes")
            {
                CreateLabelViews(this.modelObj);
            }

            base.OnParentPropertyChanged(o, args);
        }

    }
}
