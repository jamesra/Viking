using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using VikingXNA;

namespace VikingXNAGraphics
{
    public class PolyLineView : IColorView, IRenderable
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

        private List<GridVector2> _ControlPoints = null;

        public IList<GridVector2> ControlPoints
        {
            get { return _ControlPoints; }
            set
            {
                if (value == null)
                {
                    _ControlPoints = new List<GridVector2>();
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


        private float? _DashLength;
        public float? DashLength
        {
            get {
                //Only return a DashLength for Styles that use it
                switch (this.Style)
                {
                    case LineStyle.Ladder:
                    case LineStyle.Dashed:
                        return _DashLength;
                    default:
                        return new float?();
                }
            }
            set
            {
                if (value != _DashLength)
                {
                    _DashLength = value; 
                }
            }
        }


        private Color _Color;
        private Color _HSLColor;
        public Color Color
        {
            get { return _Color; }
            set
            {
                _Color = value;
                _HSLColor = value.ConvertToHSL();
                if (ControlPointViews != null)
                {
                    foreach (CircleView cpv in ControlPointViews)
                    {
                        cpv.Color = value;
                    }
                }

                if (LineViews != null)
                {
                    foreach (LineView lv in LineViews)
                    {
                        lv.Color = value;
                    }
                }
            }
        }

        internal Color HSLColor
        {
            get { return _HSLColor; }
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
            : this(null as GridVector2[], color, texture, lineWidth, lineStyle)
        {
        }

        public PolyLineView(GridPolyline polyline, 
                            Microsoft.Xna.Framework.Color color,
                            Texture2D texture = null,
                            double lineWidth = 16.0,
                            LineStyle lineStyle = LineStyle.Standard)
            : this(polyline.Points, color, texture, lineWidth, lineStyle)
        {
        }

        public PolyLineView(IEnumerable<GridVector2> controlPoints, Microsoft.Xna.Framework.Color color, Texture2D texture = null, double lineWidth = 16.0, LineStyle lineStyle = LineStyle.Standard)
        {
            this._ControlPointTexture = texture;
            this.LineWidth = lineWidth;
            this._ControlPoints = controlPoints?.ToList();
            this.Color = color;
            this.Style = lineStyle;
            this.ControlPointViews = CreateControlPointViews(this.ControlPoints, this.ControlPointRadius, color, texture);
            this.LineViews = CreateLineViews(this.ControlPoints, lineWidth, color, lineStyle);
        }

        public PolyLineView(IEnumerable<IPoint2D> controlPoints, Microsoft.Xna.Framework.Color color, Texture2D texture = null, double lineWidth = 16.0, LineStyle lineStyle = LineStyle.Standard) :
            this(controlPoints.Select(p => new GridVector2(p.X, p.Y)), color, texture, lineWidth, lineStyle)
        {
        }

        /// <summary>
        /// Add a single control point without recalculating all of the existing views
        /// </summary>
        /// <param name="p"></param>
        public void Add(GridVector2 p)
        {
            if(this.ControlPoints == null)
            {
                this._ControlPoints = new List<GridVector2>();
            }

            this.ControlPoints.Add(p);
            
            //Create the view for the control point
            List<CircleView> listControlPointViews = ControlPointViews == null ? new List<CircleView>() : ControlPointViews.ToList();
            listControlPointViews.Add(CreateControlPointView(p, this.ControlPointRadius, this.Color, this.ControlPointTexture));
            this.ControlPointViews = listControlPointViews.ToArray();

            //Create the view for the line
            if (this.ControlPoints.Count >= 2)
            {
                List<LineView> listLineViews = LineViews == null ? new List<LineView>() : LineViews.ToList();
                listLineViews.Add(new LineView(ControlPoints[ControlPoints.Count-2], ControlPoints[ControlPoints.Count - 1], LineWidth, Color, this.Style));
                this.LineViews = listLineViews.ToArray();
            }
        }

        /// <summary>
        /// Remove the last control point without recalculating all of the existing views
        /// </summary>
        /// <param name="p"></param>
        public void Remove()
        {
            if (this.ControlPoints == null || this.ControlPoints.Count == 0)
                return;

            this.ControlPoints.RemoveAt(this.ControlPoints.Count-1);

            //Remove the view for the control point
            List<CircleView> listControlPointViews = ControlPointViews.ToList();
            listControlPointViews.RemoveAt(listControlPointViews.Count - 1);
            this.ControlPointViews = listControlPointViews.ToArray();

            //Remove the view for the line
            if (LineViews.Length >= 1)
            {
                List<LineView> listLineViews = LineViews.ToList();
                listLineViews.RemoveAt(listLineViews.Count - 1);
                this.LineViews = listLineViews.ToArray();
            }
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

            return ControlPoints.Select(cp => CreateControlPointView(cp, Radius, color, texture)).ToArray();
        }

        private static CircleView CreateControlPointView(GridVector2 ControlPoint, double Radius, Microsoft.Xna.Framework.Color color, Texture2D texture)
        {
            if (texture != null)
                return new TextureCircleView(texture, new GridCircle(ControlPoint, Radius), color);
            else
                return new CircleView(new GridCircle(ControlPoint, Radius), color);
        }


        private static LineView[] CreateLineViews(IList<GridVector2> points, double LineWidth, Color color, LineStyle style)
        {
            if (points == null || points.Count < 2)
            {
                return new LineView[0];
            }

            LineView[] lineViews = new LineView[points.Count - 1];
            for (int i = 1; i < points.Count; i++)
            {
                lineViews[i - 1] = new LineView(points[i - 1], points[i], LineWidth, color, style);
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
                          VikingXNA.IScene scene,
                           OverlayStyle Overlay,
                           PolyLineView[] listToDraw)
        {
            DrawControlPoints(device, scene, Overlay, listToDraw);

            int OriginalStencilValue = DeviceStateManager.GetDepthStencilValue(device);
            CompareFunction originalStencilFunction = device.DepthStencilState.StencilFunction;
            DeviceStateManager.SetDepthStencilValue(device, OriginalStencilValue, CompareFunction.GreaterEqual);

            RoundLineCode.RoundLineManager lineManager = Overlay.GetLineManager(device);
            bool UseHSLColor = lineManager.UseHSLColor;

            var renderGroups = listToDraw.Where(pl => pl.LineViews != null).GroupBy(pl => new { color = UseHSLColor ? pl._HSLColor : pl.Color, style = pl.Style, width = pl.LineWidth, dashLength = pl.DashLength });

            foreach (var renderGroup in renderGroups)
            {
                if (renderGroup.Key.dashLength.HasValue)
                {
                    lineManager.DashLength = renderGroup.Key.dashLength.Value;
                }

                IEnumerable<LineView> lineViews = renderGroup.SelectMany(pl => pl.LineViews);
                lineManager.Draw(lineViews.Select(l => l.line),
                             (float)(renderGroup.Key.width / 2.0),
                             renderGroup.Key.color,
                             scene.ViewProj,
                             (float)(System.DateTime.UtcNow.Millisecond / 1000.0),
                             renderGroup.Key.style.ToString());
            }

            DeviceStateManager.SetDepthStencilValue(device, OriginalStencilValue, originalStencilFunction);
        }


        /// <summary>
        /// Draw only the control points
        /// </summary>
        /// <param name="device"></param>
        /// <param name="scene"></param>
        /// <param name="Overlay"></param>
        public static void DrawControlPoints(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.IScene scene, OverlayStyle Overlay, PolyLineView[] listToDraw)
        {
            int OriginalStencilValue = DeviceStateManager.GetDepthStencilValue(device);
            CompareFunction originalStencilFunction = device.DepthStencilState.StencilFunction;

            DeviceStateManager.SetDepthStencilValue(device, OriginalStencilValue + 1);

            IEnumerable<CircleView> controlPointViews = listToDraw.Where(cv => cv.ShowControlPoints && cv.ControlPointViews != null).SelectMany(cv => cv.ControlPointViews);
            CircleView.Draw(device, scene, Overlay, controlPointViews.ToArray());
                 
            DeviceStateManager.SetDepthStencilValue(device, OriginalStencilValue, originalStencilFunction);
        }

        public void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items)
        {
            PolyLineView.Draw(device, scene, Overlay, items.Select(i => i as PolyLineView).Where(i => i != null).ToArray());
        }

        public void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay)
        {
            PolyLineView.Draw(device, scene, Overlay, new PolyLineView[] { this });
        }
    }
}
