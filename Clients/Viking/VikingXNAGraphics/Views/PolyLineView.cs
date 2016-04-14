using System.Collections.Generic;
using System.Linq;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNAGraphics
{
    public class PolyLineView : IColorView
    {
        private Texture2D _ControlPointTexture;
        public Texture2D ControlPointTexture
        {
            get { return _ControlPointTexture; }
            set
            {
                _ControlPointTexture = value;
                this.ControlPointViews = CreateControlPointViews(this.ControlPoints, this.LineWidth, this.Color, this.ControlPointTexture);
            }
        }

        private List<GridVector2> _ControlPoints;

        public List<GridVector2> ControlPoints
        {
            get { return _ControlPoints; }
            set
            {
                _ControlPoints = new List<GridVector2>(value);
                if (_ControlPoints.First() == _ControlPoints.Last())
                    _ControlPoints.RemoveAt(_ControlPoints.Count - 1);

                this.ControlPointViews = CreateControlPointViews(this.ControlPoints, this.LineWidth, this.Color, this.ControlPointTexture);
                this.LineViews = CreateLineViews(this.ControlPoints.ToArray(), this.LineWidth, this.Color);
            }
        }

        public void SetPoint(int i, GridVector2 value)
        {
            _ControlPoints[i] = value;
            this.ControlPointViews = CreateControlPointViews(this.ControlPoints, this.LineWidth, this.Color, this.ControlPointTexture);
            this.LineViews = CreateLineViews(this.ControlPoints.ToArray(), this.LineWidth, this.Color);
        }

        private LineView[] LineViews;

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
                    cpv.Color = value; 
                }

                foreach(LineView lv in LineViews)
                {
                    lv.Color = value;
                }
            }
        }

        public float Alpha
        {
            get { return _Color.GetAlpha(); }
            set { Color = _Color.SetAlpha(value); }
        }

        public PolyLineView(ICollection<GridVector2> controlPoints, Microsoft.Xna.Framework.Color color, Texture2D texture = null, double lineWidth = 16.0)
        {
            this._ControlPointTexture = texture;
            this.LineWidth = lineWidth;
            this._ControlPoints = controlPoints.ToList();
            this._Color = color;
            this.ControlPointViews = CreateControlPointViews(this.ControlPoints, lineWidth, color, texture);
            this.LineViews = CreateLineViews(this.ControlPoints.ToArray(), lineWidth, color);
        }

        private static CircleView[] CreateControlPointViews(ICollection<GridVector2> ControlPoints, double Radius, Microsoft.Xna.Framework.Color color, Texture2D texture)
        {
            if (texture != null)
                return ControlPoints.Select(cp => new TextureCircleView(texture, new GridCircle(cp, Radius), color)).ToArray();
            else
                return ControlPoints.Select(cp => new CircleView(new GridCircle(cp, Radius), color)).ToArray();
        }

        private static LineView[] CreateLineViews(GridVector2[] CurvePoints, double LineWidth, Color color)
        {
            LineView[] lineViews = new LineView[CurvePoints.Length - 1];
            for (int i = 1; i < CurvePoints.Length; i++)
            {
                lineViews[i - 1] = new LineView(CurvePoints[i - 1], CurvePoints[i], LineWidth, color, VikingXNAGraphics.LineStyle.Standard);
            }

            return lineViews;
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

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.RoundLineManager lineManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect,
                          PolyLineView[] listToDraw)
        {
            int OriginalStencilValue = DeviceStateManager.GetDepthStencilValue(device);
            CompareFunction originalStencilFunction = device.DepthStencilState.StencilFunction;

            DeviceStateManager.SetDepthStencilValue(device, OriginalStencilValue + 1);

            IEnumerable<CircleView> controlPointViews = listToDraw.SelectMany(cv => cv.ControlPointViews);
            CircleView.Draw(device, scene, basicEffect, overlayEffect, controlPointViews.ToArray());

            DeviceStateManager.SetDepthStencilValue(device, OriginalStencilValue);

            IEnumerable<LineView> lineViews = listToDraw.SelectMany(cv => cv.LineViews);
            LineView.Draw(device, scene, lineManager, lineViews.ToArray());

            //DeviceStateManager.SetDepthStencilValue(device, OriginalStencilValue, originalStencilFunction);
        }
    }
}
