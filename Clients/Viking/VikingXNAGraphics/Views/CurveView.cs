using System;
using System.Collections.Generic;
using System.Linq;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNAGraphics
{
    
    public class CurveViewControlPoints
    {
        public CurveViewControlPoints(ICollection<GridVector2> cps, uint NumInterpolations, bool TryToClose)
        {
            this._NumInterpolations = NumInterpolations;
            this._TryCloseCurve = TryToClose;
            this.ControlPoints = ReverseControlPointsIfTextUpsideDown(cps);
        }

        private static GridVector2[] ReverseControlPointsIfTextUpsideDown(ICollection<GridVector2> cps)
        {
            if(cps.First().X > cps.Last().X)
            {
                return cps.Reverse().ToArray();
            }

            return cps.ToArray();
        }

        /// <summary>
        /// Try to close the curve if we have enough control points
        /// </summary>
        private bool _TryCloseCurve;
        public bool TryCloseCurve
        {
            get { return _TryCloseCurve; }
            set {
                if (_TryCloseCurve != value)
                {
                    _TryCloseCurve = value; 
                    RecalculateCurvePoints();
                }
                    
            }
        }

        private GridVector2[] _ControlPoints;

        /// <summary>
        /// In a closed curve the control points are not looped, the first and last control points should be different
        /// </summary>
        public GridVector2[] ControlPoints
        {
            get { return _ControlPoints; }
            set
            {
                _ControlPoints = value;
                while(_ControlPoints[0] == _ControlPoints[_ControlPoints.Length - 1])
                {
                    _ControlPoints = RemoveLastEntry(_ControlPoints);
                }

                RecalculateCurvePoints();
            }
        }

        private GridVector2[] _CurvePoints;
        public GridVector2[] CurvePoints
        {
            get { return _CurvePoints; }
        }

        private uint _NumInterpolations = 1;
        public uint NumInterpolations
        {
            get { return _NumInterpolations; }
            set {
                    if(value != _NumInterpolations)
                    {
                        _NumInterpolations = value;
                        RecalculateCurvePoints();
                    }
            }
        } 

        public void SetPoint(int i, GridVector2 value)
        {
            _ControlPoints[i] = value;
            RecalculateCurvePoints();
        } 

        private static GridVector2[] RemoveLastEntry(GridVector2[] array)
        {
            GridVector2[] cps = new GridVector2[array.Length - 1];
            Array.Copy(array, cps, array.Length - 1);
            return cps;
        }


        public void RecalculateCurvePoints()
        {
            this._CurvePoints = CalculateCurvePoints(this._ControlPoints, this._NumInterpolations, this._TryCloseCurve).ToArray();
        }


        public static List<GridVector2> CalculateCurvePoints(ICollection<GridVector2> ControlPoints, uint NumInterpolations, bool closeCurve)
        {
            if (closeCurve)
                return CalculateClosedCurvePoints(ControlPoints, NumInterpolations);
            else
                return CalculateOpenCurvePoints(ControlPoints, NumInterpolations);
        }
     
        private static List<GridVector2> CalculateClosedCurvePoints(ICollection<GridVector2> ControlPoints, uint NumInterpolations)
        {
            List<GridVector2> CurvePoints = new List<GridVector2>(ControlPoints.Count);
            if (ControlPoints.Count <= 2)
            {
                CurvePoints = new List<GridVector2>(ControlPoints);
            }
            if (ControlPoints.Count == 3)
            {
                CurvePoints = Geometry.Lagrange.FitCurve(ControlPoints.ToArray(), (int)NumInterpolations * ControlPoints.Count).ToList();
            }
            else if (ControlPoints.Count > 3)
            {
                CurvePoints = Geometry.CatmullRom.FitCurve(ControlPoints.ToArray(), (int)NumInterpolations, true).ToList();
                CurvePoints.Add(CurvePoints.First());
            }

            return CurvePoints;
        }

        private static List<GridVector2> CalculateOpenCurvePoints(ICollection<GridVector2> ControlPoints, uint NumInterpolations)
        {
            List<GridVector2> CurvePoints = new List<GridVector2>(ControlPoints.Count);
            if (ControlPoints.Count <= 2)
            {
                CurvePoints = new List<GridVector2>(ControlPoints);
            }
            if (ControlPoints.Count >= 3)
            {
                CurvePoints = Geometry.Lagrange.FitCurve(ControlPoints.ToArray(), (int)NumInterpolations * ControlPoints.Count).ToList();
            }

            return CurvePoints;
        }
    }

    public class CurveLabel
    {
        private string _Label; 
        public string Label
        {
            get { return _Label; }
            set
            {
                _Label = value;
                _LabelTexture = null;
            }
        }

        public Color Color;
        public double LineWidth;
        
        RenderTarget2D _LabelTexture;

        RoundCurve.RoundCurve Curve;


        public RoundCurve.HorizontalAlignment Alignment
        {
            get;
            set;
        } = RoundCurve.HorizontalAlignment.Center;

        /// <summary>
        /// Indicates how much of the curve we should use to render our text.  Useful to put two labels on the same curve while guaranteeing no overlap
        /// </summary>
        public float Max_Curve_Length_To_Use_Normalized = 1.0f;

        private CurveViewControlPoints _CurveControlPoints;

        /// <summary>
        /// How far down the length of the curve should the label start, normalized from 0 to 1
        /// </summary>
        public float LabelStartDistance;

        /// <summary>
        /// How far down the length of the curve should the label end, normalized from 0 to 1
        /// </summary>
        public float LabelEndDistance;

        public GridVector2[] ControlPoints
        {
            get { return _CurveControlPoints.ControlPoints; }
            set { _CurveControlPoints.ControlPoints = value; UpdateView(); }
        }

        public uint NumInterpolations
        {
            get { return _CurveControlPoints.NumInterpolations; }
            set
            {
                if (_CurveControlPoints.NumInterpolations != value)
                {
                    _CurveControlPoints.NumInterpolations = value;
                    UpdateView();
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
                    UpdateView();
                }
            }
        }

        public bool IsVisible(VikingXNA.Scene scene)
        {
            return this.LineWidth / scene.DevicePixelWidth > 2.0;
        }

        protected Texture2D GetOrCreateLabelTexture(GraphicsDevice device, SpriteBatch spritebatch, SpriteFont font)
        {
            if(_LabelTexture == null)
            {
                _LabelTexture = CreateTextureForLabel(Label, device, spritebatch, font, this.Color);
            }
            else if(_LabelTexture.IsDisposed || _LabelTexture.IsContentLost)
            {
                _LabelTexture = CreateTextureForLabel(Label, device, spritebatch, font, this.Color);
            }

            return _LabelTexture;
        }


        public CurveLabel(string label, ICollection<GridVector2> controlPoints, Microsoft.Xna.Framework.Color color,
                            bool TryToClose, Texture2D texture = null, double lineWidth = 16.0, uint numInterpolations = 5)
        {
            this.Label = label;
            this.Color = color;
            this.LineWidth = lineWidth;
            _CurveControlPoints = new CurveViewControlPoints(controlPoints, numInterpolations, TryToClose);
            UpdateView();
        }

        private void UpdateView()
        { 
            this.Curve = new RoundCurve.RoundCurve(_CurveControlPoints.CurvePoints);
        }

        public static RenderTarget2D CreateTextureForLabel(string label, Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                              Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                              Microsoft.Xna.Framework.Graphics.SpriteFont font,
                              Color color,
                              float scale = 2.0f)
        {
            if (string.IsNullOrEmpty(label))
                return null;

            Vector2 labelDimensions = font.MeasureString(label);
            labelDimensions *= scale; 
            RenderTarget2D target = new RenderTarget2D(device, (int)labelDimensions.X, (int)labelDimensions.Y);

            RenderTargetBinding[] oldRenderTargets = device.GetRenderTargets();
            device.SetRenderTarget(target);

            device.Clear(Color.Transparent);
            
            spriteBatch.Begin();
            spriteBatch.DrawString(font, label, new Vector2(0, 0), color, 0, new Vector2(0,0), scale, SpriteEffects.None, 0);
            spriteBatch.End();

            device.SetRenderTargets(oldRenderTargets);

            return target;
        }

        public void Draw(GraphicsDevice device, VikingXNA.Scene scene,
                                SpriteBatch spriteBatch, SpriteFont font,
                                RoundCurve.CurveManager CurveManager, BasicEffect basicEffect)
        {
            Matrix ViewProj = scene.Camera.View * scene.Projection;
            this.Draw(device, ViewProj, spriteBatch, font, CurveManager, basicEffect);
        }

        public void Draw(GraphicsDevice device, Matrix ViewProj,
                                SpriteBatch spriteBatch, SpriteFont font,
                                RoundCurve.CurveManager CurveManager, BasicEffect basicEffect)
        {
            Texture2D labelTexture = GetOrCreateLabelTexture(device, spriteBatch, font);
            if (labelTexture == null) //Happens when the label text is null or empty
                return; 

            CurveManager.DrawLabel(this.Curve, (float)this.LineWidth / 2.0f, this.Color, ViewProj, 0, labelTexture, this.Alignment, this.Max_Curve_Length_To_Use_Normalized);
        }

        public static void Draw(GraphicsDevice device, VikingXNA.Scene scene,
                                SpriteBatch spriteBatch, SpriteFont font,
                                RoundCurve.CurveManager CurveManager, BasicEffect basicEffect,
                                CurveLabel[] labels)
        {
            Matrix ViewProj = scene.Camera.View * scene.Projection;
            foreach (CurveLabel label in labels)
            {
                label.Draw(device, ViewProj, spriteBatch, font, CurveManager, basicEffect);
            }
        }
    }

    /// <summary>
    /// Draws a closed curve through the control points using Catmull-rom
    /// </summary>
    public class CurveView
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
            }
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

        public CurveView(ICollection<GridVector2> controlPoints, Microsoft.Xna.Framework.Color color, bool TryToClose,  Texture2D texture = null, double lineWidth = 16.0, LineStyle lineStyle = LineStyle.Standard, uint numInterpolations = 5)
        {
            this._CurveControlPoints = new CurveViewControlPoints(controlPoints, numInterpolations, TryToClose);
            this._Color = color;
            this.Style = lineStyle;
            this._ControlPointTexture = texture;
            this.LineWidth = lineWidth;
            UpdateViews();
        }

        private void UpdateViews()
        {
            this.ControlPointViews = CreateControlPointViews(this.ControlPoints, this.LineWidth / 2.0, this.Color, null);
            this.Curve = CreateCurveView(this.CurvePoints.ToArray(), this.LineWidth, this.Color);
        }

        private static CircleView[] CreateControlPointViews(ICollection<GridVector2> ControlPoints, double Radius, Microsoft.Xna.Framework.Color color, Texture2D texture)
        {
            if(texture != null)
                return ControlPoints.Select(cp => new TextureCircleView(texture, new GridCircle(cp, Radius), color)).ToArray();
            else
                return ControlPoints.Select(cp => new CircleView(new GridCircle(cp, Radius), color)).ToArray();   
        }


        

        private static RoundCurve.RoundCurve CreateCurveView(GridVector2[] CurvePoints, double LineWidth, Color color)
        {
            return new RoundCurve.RoundCurve(CurvePoints);
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
            Draw(device, scene, CurveManager, basicEffect, ControlPoints, curvePoints.CurvePoints, Color, LineWidth);
        }

        public static void Draw(GraphicsDevice device, VikingXNA.Scene scene, RoundCurve.CurveManager CurveManager, 
                                BasicEffect basicEffect, 
                                GridVector2[] ControlPoints, GridVector2[] CurvePoints, 
                                Microsoft.Xna.Framework.Color Color, double LineWidth = 16.0)
        {
            Microsoft.Xna.Framework.Color pointColor = ControlPointColor(Color);
            //GlobalPrimitives.DrawPoints(LineManager, basicEffect, ControlPoints.ToList(), LineWidth, pointColor); 

            foreach (GridVector2 cp in ControlPoints)
            {
                GlobalPrimitives.DrawCircle(device, basicEffect, cp, LineWidth / 2.0, pointColor);
            }

            RoundCurve.RoundCurve curve = new RoundCurve.RoundCurve(CurvePoints);
            CurveManager.Draw(curve, (float)LineWidth / 2.0f, Color, scene.ViewProj, 0, "Standard");
        }

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundCurve.CurveManager curveManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect,
                          float time,
                          CurveView[] listToDraw)
        {
            int OriginalStencilValue = DeviceStateManager.GetDepthStencilValue(device);
            CompareFunction originalStencilFunction = device.DepthStencilState.StencilFunction;

            IEnumerable<CircleView> controlPointViews = listToDraw.SelectMany(cv => cv.ControlPointViews);            
            CircleView.Draw(device, scene, basicEffect, overlayEffect, controlPointViews.ToArray());

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
