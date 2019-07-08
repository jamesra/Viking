using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNAGraphics
{

    public class CurveLabel : IText, IColorView
    {
        private string _Text;
        public string Text
        {
            get { return _Text; }
            set
            {
                _Text = value;
                _LabelTexture = null;
            }
        }

        public double FontSize
        {
            get { return (float)LineWidth; }
            set { LineWidth = value; }
        }


        public Color Color { get; set; }
        public float Alpha
        {
            get { return Color.GetAlpha(); }
            set { Color = Color.SetAlpha(value); }
        }

        public double LineWidth;

        RenderTarget2D _LabelTexture;

        RoundCurve.RoundCurve Curve;

        bool TextureGenerating = false;


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
        public float LabelStartDistance = 0f;

        /// <summary>
        /// How far down the length of the curve should the label end, normalized from 0 to 1
        /// </summary>
        public float LabelEndDistance = 1.0f;

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
            return this.LineWidth / scene.DevicePixelWidth > 3;
        }

        private void BeginInvokeGenerateTexture(GraphicsDevice device, SpriteBatch spritebatch, SpriteFont font)
        {
            if (TextureGenerating)
                return;
            else
            {
                this.TextureGenerating = true;
                //Func<String, GraphicsDevice, SpriteBatch, SpriteFont, Color, float, RenderTarget2D> CreateTextureFunc = CreateTextureForLabel;
                //CreateTextureFunc.BeginInvoke(Label, device, spritebatch, font, this.Color, 2.0f, EndInvokeGenerateTexture, CreateTextureFunc);
                Action a = new Action(() =>
                {
                    this._LabelTexture = CreateTextureForLabel(this.Text, device, spritebatch, font, Color);
                    this.TextureGenerating = false;
                });
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(a, System.Windows.Threading.DispatcherPriority.Background, null);
            }
        }

        protected Texture2D GetOrCreateLabelTexture(GraphicsDevice device, SpriteBatch spritebatch, SpriteFont font)
        {
            if (_LabelTexture == null)
            {
                BeginInvokeGenerateTexture(device, spritebatch, font);
            }
            else if (_LabelTexture.IsDisposed || _LabelTexture.IsContentLost)
            {
                _LabelTexture = null;
                BeginInvokeGenerateTexture(device, spritebatch, font);
            }

            return _LabelTexture;
        }

        /// <summary>
        /// Helper function to create a label for single line
        /// </summary>
        /// <param name="label"></param>
        /// <param name="line"></param>
        /// <param name="color"></param>
        /// <param name="texture"></param>
        /// <param name="lineWidth"></param>
        /// <returns></returns>
        public static CurveLabel CreateLineLabel(string label, GridLineSegment line, Microsoft.Xna.Framework.Color color,
                            Texture2D texture = null, double lineWidth = 16.0)
        {
            CurveLabel labelView = new CurveLabel(label, new GridVector2[] { line.A, line.B }, color, false, texture: texture, lineWidth: lineWidth, numInterpolations: 0);
            return labelView;
        }


        public CurveLabel(string label, ICollection<GridVector2> controlPoints, Microsoft.Xna.Framework.Color color,
                            bool TryToClose, Texture2D texture = null, double lineWidth = 16.0, uint numInterpolations = 5)
        {
            this.Text = label;
            this.Color = color;
            this.LineWidth = lineWidth;
            _CurveControlPoints = new CurveViewControlPoints(controlPoints, numInterpolations, TryToClose);
            UpdateView();
        }

        private void UpdateView()
        {
            this.Curve = new RoundCurve.RoundCurve(_CurveControlPoints.CurvePoints, _CurveControlPoints.TryCloseCurve);
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

            //RenderTargetBinding[] oldRenderTargets = device.GetRenderTargets();
            //TODO: Setting the render target when the scene is being drawn causes flickering
            device.SetRenderTarget(target);

            device.Clear(Color.Transparent);

            spriteBatch.Begin();
            spriteBatch.DrawString(font, label, new Vector2(0, 0), color, 0, new Vector2(0, 0), scale, SpriteEffects.None, 0);
            spriteBatch.End();

            device.SetRenderTargets(null);

            return target;
        }

        public void Draw(GraphicsDevice device, VikingXNA.Scene scene,
                                SpriteBatch spriteBatch, SpriteFont font,
                                RoundCurve.CurveManager CurveManager)
        {
            Matrix ViewProj = scene.Camera.View * scene.Projection;
            this.Draw(device, ViewProj, spriteBatch, font, CurveManager);
        }

        public void Draw(GraphicsDevice device, Matrix ViewProj,
                                SpriteBatch spriteBatch, SpriteFont font,
                                RoundCurve.CurveManager CurveManager)
        {
            Texture2D labelTexture = GetOrCreateLabelTexture(device, spriteBatch, font);
            if (labelTexture == null) //Happens when the label text is null or empty
                return;

            CurveManager.DrawLabel(this.Curve, (float)this.LineWidth / 2.0f, this.Color, ViewProj, 0, labelTexture, this.Alignment, this.Max_Curve_Length_To_Use_Normalized);
        }

        public static void Draw(GraphicsDevice device, VikingXNA.Scene scene,
                                SpriteBatch spriteBatch, SpriteFont font,
                                RoundCurve.CurveManager CurveManager,
                                CurveLabel[] labels)
        {
            Matrix ViewProj = scene.Camera.View * scene.Projection;
            foreach (CurveLabel label in labels)
            {
                label.Draw(device, ViewProj, spriteBatch, font, CurveManager);
            }
        }
    }
}
