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

namespace WebAnnotation.View
{
    class LocationClosedCurveView : LocationCurveView
    {
        public CurveView curveView;

        public Microsoft.Xna.Framework.Color Color
        {
            get { return curveView.Color; }
            set { curveView.Color = value; }
        }


        public static uint NumInterpolationPoints = Global.NumCurveInterpolationPoints;
        public LocationClosedCurveView(LocationObj obj) : base(obj)
        {
            curveView = new CurveView(modelObj.VolumeShape.ToPoints(), obj.Parent.Type.Color.ToXNAColor().ConvertToHSL(0.5f), true);
        }

        private GridVector2[] _MosaicCurveControlPoints;
        public override GridVector2[] MosaicCurveControlPoints
        {
            get
            {
                if (_MosaicCurveControlPoints == null)
                {
                    _MosaicCurveControlPoints = CurveViewControlPoints.CalculateCurvePoints(modelObj.MosaicShape.ToPoints(), LocationOpenCurveView.NumInterpolationPoints, true).ToArray();
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
                    _VolumeCurveControlPoints = CurveViewControlPoints.CalculateCurvePoints(modelObj.VolumeShape.ToPoints(), LocationOpenCurveView.NumInterpolationPoints, true).ToArray();
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
                          RoundCurve.CurveManager lineManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect,
                          LocationClosedCurveView[] listToDraw)
        {
            CurveView.Draw(device, scene, lineManager, basicEffect, overlayEffect, 0, listToDraw.Select(l => l.curveView).ToArray());
        }

        public override double Width
        {
            get
            {
                return curveView.LineWidth;
            }
        }
    }
}
