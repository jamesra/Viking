using System;
using System.Collections.Generic;
using System.Linq;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNAGraphics
{  
    /// <summary>
    /// Draws a closed curve through the control points using Catmull-rom
    /// </summary>
    public class CurveView : IColorView
    {
        public LineStyle Style;

        private CurveViewControlPoints _CurveControlPoints; 

        private Texture2D _ControlPointTexture;
        public Texture2D ControlPointTexture
        {
            get { return _ControlPointTexture; }
            set { _ControlPointTexture = value; }
        }
        
        /// <summary>
        /// Even in a closed curve the control points are not looped, the first and last control points should be different
        /// </summary>
        public GridVector2[] ControlPoints
        {
            get { return _CurveControlPoints.ControlPoints; }
            set {
                _CurveControlPoints.ControlPoints = value;
                UpdateViews();
            }
        }

        public void SetPoint(int i, GridVector2 value)
        {
            _CurveControlPoints.SetPoint(i, value);
            UpdateViews();
        }
                
        private GridVector2[] CurvePoints
        {
            get { return _CurveControlPoints.CurvePoints; }
        }

        private RoundCurve.RoundCurve Curve;

        private CircleView[] ControlPointViews;

        private double _LineWidth;

        public double LineWidth
        {
            get { return _LineWidth; }
            set {
                if (_LineWidth != value)
                {
                    _LineWidth = value;
                    UpdateViews();
                }
            }
        }

        private double _ControlPointRadius;

        public double ControlPointRadius
        {
            get { return _ControlPointRadius; }
            set
            {
                if (_ControlPointRadius != value)
                {
                    _ControlPointRadius = value;
                    UpdateViews();
                }
            }
        }

        private Color _Color;
        public Color Color
        {
            get { return _Color; }
            set
            {
                _Color = value;
                foreach (CircleView cpv in ControlPointViews)
                {
                    cpv.Color = value;
                } 
            }
        } 

        public float Alpha
        {
            get { return _Color.GetAlpha(); }
            set { Color = _Color.SetAlpha(value); }
        }

        public uint NumInterpolations
        {
            get { return _CurveControlPoints.NumInterpolations; }
            set {
                if (_CurveControlPoints.NumInterpolations != value)
                {
                    _CurveControlPoints.NumInterpolations = value;
                    UpdateViews();
                }
            }
        }

        /// <summary>
        /// True if we should close the curve if we have enough points
        /// </summary>
        public bool TryCloseCurve
        {
            get { return _CurveControlPoints.TryCloseCurve; }
            set
            {
                if (_CurveControlPoints.TryCloseCurve != value)
                {
                    _CurveControlPoints.TryCloseCurve = value;
                    UpdateViews();
                }
            }
        }

        public CurveView(ICollection<GridVector2> controlPoints, Microsoft.Xna.Framework.Color color, bool TryToClose, uint numInterpolations,
                         Texture2D texture = null, double lineWidth = 16.0, double? controlPointRadius = null, LineStyle lineStyle = LineStyle.Standard)
        {
            this._CurveControlPoints = new CurveViewControlPoints(controlPoints, numInterpolations, TryToClose);
            this._Color = color;
            this.Style = lineStyle;
            this._ControlPointTexture = texture;
            this._LineWidth = lineWidth;
            if (!controlPointRadius.HasValue)
                this.ControlPointRadius = lineWidth / 2.0;
            else
                this.ControlPointRadius = controlPointRadius.Value;

            UpdateViews();
        }

        private void UpdateViews()
        {
            this.ControlPointViews = CreateControlPointViews(this.ControlPoints, this.ControlPointRadius, this.Color, null);
            this.Curve = CreateCurveView(this.CurvePoints.ToArray(), this.LineWidth, this.Color, _CurveControlPoints.TryCloseCurve);
        }

        private static CircleView[] CreateControlPointViews(ICollection<GridVector2> ControlPoints, double Radius, Microsoft.Xna.Framework.Color color, Texture2D texture)
        {
            if(texture != null)
                return ControlPoints.Select(cp => new TextureCircleView(texture, new GridCircle(cp, Radius), color)).ToArray();
            else
                return ControlPoints.Select(cp => new CircleView(new GridCircle(cp, Radius), color)).ToArray();   
        }


        

        private static RoundCurve.RoundCurve CreateCurveView(GridVector2[] CurvePoints, double LineWidth, Color color, bool Closed)
        {
            return new RoundCurve.RoundCurve(CurvePoints, Closed);
        }


        /// <summary>
        /// Fetch the control point color to use for a line
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        private static Microsoft.Xna.Framework.Color ControlPointColor(Microsoft.Xna.Framework.Color color)
        {
            return new Microsoft.Xna.Framework.Color(255 - (int)color.R, 255 - (int)color.G, 255 - (int)color.B, (int)color.A / 2f);
            //return color;
        }

        public static void Draw(GraphicsDevice device, VikingXNA.Scene scene, 
                                RoundCurve.CurveManager CurveManager,
                                BasicEffect basicEffect, 
                                GridVector2[] ControlPoints, uint NumInterpolations,
                                bool IsClosed, Microsoft.Xna.Framework.Color Color,
                                double LineWidth = 16.0)
        {
            CurveViewControlPoints curvePoints = new CurveViewControlPoints(ControlPoints, NumInterpolations, IsClosed);
            Draw(device, scene, CurveManager, basicEffect, ControlPoints, curvePoints.CurvePoints, IsClosed, Color, LineWidth);
        }

        public static void Draw(GraphicsDevice device, VikingXNA.Scene scene, RoundCurve.CurveManager CurveManager, 
                                BasicEffect basicEffect, 
                                GridVector2[] ControlPoints, GridVector2[] CurvePoints, bool Closed,
                                Microsoft.Xna.Framework.Color Color, double LineWidth = 16.0)
        {
            Microsoft.Xna.Framework.Color pointColor = ControlPointColor(Color);
            //GlobalPrimitives.DrawPoints(LineManager, basicEffect, ControlPoints.ToList(), LineWidth, pointColor); 

            foreach (GridVector2 cp in ControlPoints)
            {
                GlobalPrimitives.DrawCircle(device, basicEffect, cp, LineWidth / 2.0, pointColor);
            }

            RoundCurve.RoundCurve curve = new RoundCurve.RoundCurve(CurvePoints, Closed);
            CurveManager.Draw(curve, (float)LineWidth / 2.0f, Color, scene.ViewProj, 0, "Standard");
        }

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundCurve.CurveManager curveManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          AnnotationOverBackgroundLumaEffect overlayEffect,
                          float time,
                          CurveView[] listToDraw)
        {

            IEnumerable<CircleView> controlPointViews = listToDraw.SelectMany(cv => cv.ControlPointViews);            
            CircleView.Draw(device, scene, basicEffect, overlayEffect, controlPointViews.ToArray());
           
            int OriginalStencilValue = DeviceStateManager.GetDepthStencilValue(device);
            CompareFunction originalStencilFunction = device.DepthStencilState.StencilFunction;

            DeviceStateManager.SetDepthStencilValue(device, OriginalStencilValue - 1, CompareFunction.Greater);
            
            Matrix ViewProj = scene.Camera.View * scene.Projection;
            foreach (CurveView curve in listToDraw)
            {
                curveManager.Draw(curve.Curve, (float)curve.LineWidth / 2.0f, curve.Color.ConvertToHSL(), ViewProj, time, curve.Style.ToString());
            }

            DeviceStateManager.SetDepthStencilValue(device, OriginalStencilValue, originalStencilFunction);
        }

        public void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device, RoundLineCode.RoundLineManager LineManager, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            Microsoft.Xna.Framework.Color pointColor = ControlPointColor(Color);
            //GlobalPrimitives.DrawPoints(LineManager, basicEffect, ControlPoints, LineWidth, pointColor);

            foreach (GridVector2 cp in ControlPoints)
            {
                GlobalPrimitives.DrawCircle(device, basicEffect, cp, LineWidth, pointColor);
            }

            GlobalPrimitives.DrawPolyline(LineManager, basicEffect, CurvePoints, this.LineWidth, this.Color);
        }

        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
            if (sender == null)
                throw new ArgumentNullException("sender");

            System.Collections.Specialized.NotifyCollectionChangedEventArgs CollectionChangeArgs = e as System.Collections.Specialized.NotifyCollectionChangedEventArgs;
            if (CollectionChangeArgs != null)
            {
                UpdateViews();
                return true;
            }

            return false;
        }
    }
}
