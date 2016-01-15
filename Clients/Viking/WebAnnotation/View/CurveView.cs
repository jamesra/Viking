using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Geometry;
using WebAnnotation.ViewModel;
using Viking.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WebAnnotation.View
{
    /// <summary>
    /// Draws a closed curve through the control points using Catmull-rom
    /// </summary>
    class CurveView : System.Windows.IWeakEventListener
    {
        private List<GridVector2> _ControlPoints;

        private Texture2D _ControlPointTexture;
        public Texture2D ControlPointTexture
        {
            get { return _ControlPointTexture; }
            set { _ControlPointTexture = value;
                  }
        }
        
        /// <summary>
        /// Even in a closed curve the control points are not looped, the first and last control points should be different
        /// </summary>
        public List<GridVector2> ControlPoints
        {
            get { return _ControlPoints; }
            set { _ControlPoints = new List<GridVector2>(value);
                if (_ControlPoints.First() == _ControlPoints.Last())
                    _ControlPoints.RemoveAt(_ControlPoints.Count - 1);

                CurvePoints = CalculateCurvePoints(this.ControlPoints, this.NumInterpolations, this.TryCloseCurve);
                this.ControlPointViews = CreateControlPointViews(this.ControlPoints, this.LineWidth, this.Color, null);
                this.CurveLineViews = CreateCurveLineViews(this.CurvePoints.ToArray(), this.LineWidth, this.Color);
            }
        }

        public void SetPoint(int i, GridVector2 value)
        {
            _ControlPoints[i] = value;
            CurvePoints = CalculateCurvePoints(this.ControlPoints, this.NumInterpolations, this.TryCloseCurve);
            this.ControlPointViews = CreateControlPointViews(this.ControlPoints, this.LineWidth, this.Color, null);
            this.CurveLineViews = CreateCurveLineViews(this.CurvePoints.ToArray(), this.LineWidth, this.Color);
        }
                
        private List<GridVector2> CurvePoints = new List<GridVector2>();

        private LineView[] CurveLineViews;

        private CircleView[] ControlPointViews;

        public double LineWidth;
        
        private Color _Color;
        public Color Color
        {
            get { return _Color; }
            set
            {
                _Color = value;
                foreach (CircleView cpv in ControlPointViews)
                {
                    cpv.BackgroundColor = value;
                }

                foreach (LineView lv in CurveLineViews)
                {
                    lv.Color = value;
                }
            }
        }

        public int NumInterpolations = Global.NumCurveInterpolationPoints;
        /// <summary>
        /// True if we should close the curve if we have enough points
        /// </summary>
        public bool TryCloseCurve;

        public CurveView(ICollection<GridVector2> controlPoints, Microsoft.Xna.Framework.Color color, bool TryToClose,  Texture2D texture = null, double lineWidth = 16.0, int numInterpolations = 5)
        {
            this._Color = color;
            this._ControlPointTexture = texture;
            this.LineWidth = lineWidth;
            this.NumInterpolations = numInterpolations;
            this.TryCloseCurve = TryToClose;
            this.ControlPoints = controlPoints.ToList();
            this.ControlPointViews = CreateControlPointViews(this.ControlPoints, lineWidth / 2.0f, color, null);
            this.CurveLineViews = CreateCurveLineViews(this.CurvePoints.ToArray(), lineWidth, color);
        }

        private static CircleView[] CreateControlPointViews(ICollection<GridVector2> ControlPoints, double Radius, Microsoft.Xna.Framework.Color color, Texture2D texture)
        {
            if(texture != null)
                return ControlPoints.Select(cp => new TextureCircleView(texture, new GridCircle(cp, Radius), color)).ToArray();
            else
                return ControlPoints.Select(cp => new CircleView(new GridCircle(cp, Radius), color)).ToArray();   
        }


        public static List<GridVector2> CalculateCurvePoints(ICollection<GridVector2> ControlPoints, int NumInterpolations, bool closeCurve)
        {
            if (closeCurve)
                return CalculateClosedCurvePoints(ControlPoints, NumInterpolations);
            else
                return CalculateOpenCurvePoints(ControlPoints, NumInterpolations);
        }

        private static LineView[] CreateCurveLineViews(GridVector2[] CurvePoints, double LineWidth, Color color)
        {
            LineView[] lineViews = new LineView[CurvePoints.Length - 1];
            for(int i= 1; i < CurvePoints.Length; i++)
            {
                lineViews[i - 1] = new LineView(CurvePoints[i - 1], CurvePoints[i], LineWidth, color, VikingXNAGraphics.LineStyle.Standard);
            }

            return lineViews;
        }

        private static List<GridVector2> CalculateClosedCurvePoints(ICollection<GridVector2> ControlPoints, int NumInterpolations)
        {
            List<GridVector2> CurvePoints = new List<GridVector2>(ControlPoints.Count);
            if (ControlPoints.Count <= 2)
            {
                CurvePoints = new List<GridVector2>(ControlPoints);
            }
            if (ControlPoints.Count == 3)
            {
                CurvePoints = Geometry.Lagrange.FitCurve(ControlPoints.ToArray(), NumInterpolations * ControlPoints.Count).ToList();
            }
            else if (ControlPoints.Count > 3)
            {
                CurvePoints = Geometry.CatmullRom.FitCurve(ControlPoints.ToArray(), NumInterpolations, true).ToList();
                CurvePoints.Add(CurvePoints.First());
            }

            return CurvePoints;
        }

        private static List<GridVector2> CalculateOpenCurvePoints(ICollection<GridVector2> ControlPoints, int NumInterpolations)
        {
            List<GridVector2> CurvePoints = new List<GridVector2>(ControlPoints.Count);
            if (ControlPoints.Count <= 2)
            {
                CurvePoints = new List<GridVector2>(ControlPoints);
            }
            if (ControlPoints.Count >= 3)
            {
                CurvePoints = Geometry.Lagrange.FitCurve(ControlPoints.ToArray(), NumInterpolations * ControlPoints.Count).ToList();
            }

            return CurvePoints;
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

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device, RoundLineCode.RoundLineManager LineManager, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect, GridVector2[] ControlPoints, int NumInterpolations, bool IsClosed, Microsoft.Xna.Framework.Color Color, double LineWidth = 16.0)
        {
            GridVector2[] CurvePoints;
            if (NumInterpolations > 0)
                CurvePoints = CalculateCurvePoints(ControlPoints, NumInterpolations, IsClosed).ToArray();
            else
                CurvePoints = ControlPoints;

            Draw(device, LineManager, basicEffect, ControlPoints, CurvePoints, Color, LineWidth);
        }

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device, RoundLineCode.RoundLineManager LineManager, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect, GridVector2[] ControlPoints, GridVector2[] CurvePoints, Microsoft.Xna.Framework.Color Color, double LineWidth = 16.0)
        {
            Microsoft.Xna.Framework.Color pointColor = ControlPointColor(Color);
            //GlobalPrimitives.DrawPoints(LineManager, basicEffect, ControlPoints.ToList(), LineWidth, pointColor); 

            foreach (GridVector2 cp in ControlPoints)
            {
                GlobalPrimitives.DrawCircle(device, basicEffect, cp, LineWidth, pointColor);
            }

            GlobalPrimitives.DrawPolyline(LineManager, basicEffect, CurvePoints, LineWidth, Color);
        }

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.RoundLineManager lineManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect,
                          CurveView[] listToDraw)
        {
            int OriginalStencilValue = DeviceStateManager.GetDepthStencilValue(device);
            CompareFunction originalStencilFunction = device.DepthStencilState.StencilFunction;

            IEnumerable<CircleView> controlPointViews = listToDraw.SelectMany(cv => cv.ControlPointViews);            
            CircleView.Draw(device, scene, basicEffect, overlayEffect, controlPointViews.ToArray());

            DeviceStateManager.SetDepthStencilValue(device, OriginalStencilValue - 1, CompareFunction.Greater);

            IEnumerable<LineView> lineViews = listToDraw.SelectMany(cv => cv.CurveLineViews);
            LineView.Draw(device, scene, lineManager, lineViews.ToArray());

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
                this.CurvePoints = CalculateCurvePoints(this.ControlPoints, this.NumInterpolations, this.TryCloseCurve);
                return true;
            }

            return false;
        }
    }
}
