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

        public bool ShowControlPoints { get; set; } = true;

        private List<GridVector2> _ControlPoints;

        public IList<GridVector2> ControlPoints
        {
            get { return _ControlPoints; }
            set
            {
                if (value == null)
                {
                    _ControlPoints = null;
                }
                else
                {
                    _ControlPoints = new List<GridVector2>(value);
                }

                UpdateAllViews();
            }
        }

        public void SetPoint(int i, GridVector2 value)
        {
            _ControlPoints[i] = value;
            this.ControlPointViews = CreateControlPointViews(this.ControlPoints, this.LineWidth, this.Color, this.ControlPointTexture);
            this.LineViews = CreateLineViews(this.ControlPoints.ToArray(), this.LineWidth, this.Color, this.Style);
        }

        private LineStyle _Style; 
        public LineStyle Style
        {
            get { return _Style; }
            set
            {
                if(value != _Style)
                {
                    _Style = value;
                    UpdateAllViews();
                }
            }
        }

        private LineView[] LineViews;

        private CircleView[] ControlPointViews;

        private double _LineWidth;
        public double LineWidth
        {
            get { return _LineWidth; }
            set
            {
                if(value != _LineWidth)
                {
                    _LineWidth = value;

                    UpdateAllViews();
                }
            }
        }

        private double? _ControlPointRadius;
        public double ControlPointRadius
        {
            get {
                return _ControlPointRadius.HasValue ? _ControlPointRadius.Value : _LineWidth;
                }
            set
            {
                if (value != _ControlPointRadius)
                {
                    _ControlPointRadius = value;

                    UpdateAllViews();
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

        public PolyLineView(Microsoft.Xna.Framework.Color color, 
                            Texture2D texture = null,
                            double lineWidth = 16.0,
                            LineStyle lineStyle = LineStyle.Standard) 
            : this(null, color, texture, lineWidth, lineStyle)
        {
        }

        public PolyLineView(IReadOnlyList<GridVector2> controlPoints, Microsoft.Xna.Framework.Color color, Texture2D texture = null, double lineWidth = 16.0, LineStyle lineStyle = LineStyle.Standard)
        {
            this._ControlPointTexture = texture;
            this.LineWidth = lineWidth;
            this._ControlPoints = controlPoints == null ? null : controlPoints.ToList();
            this._Color = color;
            this.Style = lineStyle;
            this.ControlPointViews = CreateControlPointViews(this.ControlPoints, this.ControlPointRadius, color, texture);
            this.LineViews = CreateLineViews(this.ControlPoints, lineWidth, color, lineStyle);
        }

        /// <summary>
        /// Recreate all views used by this object
        /// </summary>
        private void UpdateAllViews()
        {
            if (_ControlPoints == null || _ControlPoints.Count == 0)
            {
                this.ControlPointViews = null;
                this.LineViews = null;
            }
            else if (_ControlPoints.Count == 1)
            {
                this.ControlPointViews = CreateControlPointViews(this.ControlPoints, this.ControlPointRadius, this.Color, this.ControlPointTexture);
                this.LineViews = null;
            }
            else
            {
                this.LineViews = CreateLineViews(this.ControlPoints.ToArray(), this.LineWidth, this.Color, this.Style);

                //Don't create a duplicate circle view if we are drawing a loop
                if (_ControlPoints.First() == _ControlPoints.Last())
                    _ControlPoints.RemoveAt(_ControlPoints.Count - 1);

                this.ControlPointViews = CreateControlPointViews(this.ControlPoints, this.ControlPointRadius, this.Color, this.ControlPointTexture);
                
            }
        }

        private static CircleView[] CreateControlPointViews(IList<GridVector2> ControlPoints, double Radius, Microsoft.Xna.Framework.Color color, Texture2D texture)
        {
            if(ControlPoints == null)
            {
                return new CircleView[0];
            }

            if (texture != null)
                return ControlPoints.Select(cp => new TextureCircleView(texture, new GridCircle(cp, Radius), color)).ToArray();
            else
                return ControlPoints.Select(cp => new CircleView(new GridCircle(cp, Radius), color)).ToArray();
        }

        private static LineView[] CreateLineViews(IList<GridVector2> CurvePoints, double LineWidth, Color color, LineStyle style)
        {
            if (CurvePoints == null || CurvePoints.Count < 2)
            {
                return new LineView[0];
            }

            LineView[] lineViews = new LineView[CurvePoints.Count - 1];
            for (int i = 1; i < CurvePoints.Count; i++)
            {
                lineViews[i - 1] = new LineView(CurvePoints[i - 1], CurvePoints[i], LineWidth, color, style, false);
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
                          AnnotationOverBackgroundLumaEffect overlayEffect,
                          PolyLineView[] listToDraw)
        {
            int OriginalStencilValue = DeviceStateManager.GetDepthStencilValue(device);
            CompareFunction originalStencilFunction = device.DepthStencilState.StencilFunction;
             
            DeviceStateManager.SetDepthStencilValue(device, OriginalStencilValue + 1);

            IEnumerable<CircleView> controlPointViews = listToDraw.Where(cv => cv.ShowControlPoints && cv.ControlPointViews != null).SelectMany(cv => cv.ControlPointViews);
            CircleView.Draw(device, scene, basicEffect, overlayEffect, controlPointViews.ToArray());

            DeviceStateManager.SetDepthStencilValue(device, OriginalStencilValue);
             

            IEnumerable<LineView> lineViews = listToDraw.Where(cv => cv.LineViews != null).SelectMany(cv => cv.LineViews);
            LineView.Draw(device, scene, lineManager, lineViews.ToArray());

            //DeviceStateManager.SetDepthStencilValue(device, OriginalStencilValue, originalStencilFunction);
        }
    }
}
