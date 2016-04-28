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


        public static uint NumInterpolationPoints = Global.NumCurveInterpolationPoints;
        public LocationClosedCurveView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(obj, mapper)
        {
            curveView = new CurveView(this.VolumeControlPoints, obj.Parent.Type.Color.ToXNAColor(0.5f), true);
        }

        private GridVector2[] _MosaicCurveControlPoints;
        public override GridVector2[] MosaicCurveControlPoints
        {
            get
            {
                if (_MosaicCurveControlPoints == null)
                {
                    _MosaicCurveControlPoints = CurveViewControlPoints.CalculateCurvePoints(this.MosaicControlPoints, LocationOpenCurveView.NumInterpolationPoints, true).ToArray();
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
                    _VolumeCurveControlPoints = CurveViewControlPoints.CalculateCurvePoints(this.VolumeControlPoints, LocationOpenCurveView.NumInterpolationPoints, true).ToArray();
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
