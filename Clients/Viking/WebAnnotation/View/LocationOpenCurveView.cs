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
    class LocationOpenCurveView : LocationCurveView
    {
        public static int NumInterpolationPoints = Global.NumCurveInterpolationPoints;

        public CurveView curveView;

        public LocationOpenCurveView(LocationObj obj) : base(obj)
        {
            curveView = new CurveView(obj.VolumeShape.ToPoints(), obj.Parent.Type.Color.ToXNAColor().ConvertToHSL(0.5f), false);
        }
        
        private GridVector2[] _MosaicControlPoints;
        public override GridVector2[] MosaicCurveControlPoints
        {
            get
            {
                if (_MosaicControlPoints == null)
                {
                    _MosaicControlPoints = CurveView.CalculateCurvePoints(modelObj.MosaicShape.ToPoints(), LocationOpenCurveView.NumInterpolationPoints, false).ToArray();
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
                    _VolumeCurveControlPoints = CurveView.CalculateCurvePoints(modelObj.VolumeShape.ToPoints(), LocationOpenCurveView.NumInterpolationPoints, false).ToArray();
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
                    _RenderedVolumeShape = this.VolumeCurveControlPoints.ToPolyLine().STBuffer(this.Width);
                }

                return _RenderedVolumeShape;
            }
        }

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.RoundLineManager lineManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect,
                          LocationOpenCurveView[] listToDraw)
        {
            CurveView.Draw(device, scene, lineManager, basicEffect, overlayEffect, listToDraw.Select(l => l.curveView).ToArray());
        }
    }
}
