using Geometry;
using Rectangle = Geometry.Rectangle;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VikingXNA;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace VikingXNAGraphics
{
    public class ScaledLabelView : LabelView
    {
        private readonly IScene _Scene;

        public ScaledLabelView(string Text, Geometry.Vector2 VolumePosition, Color color, IScene scene, Alignment alignment = null, Anchor anchor = null, double fontSize = 16) : base(Text, VolumePosition, color, alignment, anchor, true, fontSize)
        {
            _Scene = scene;
        }

        public ScaledLabelView(string Text, Geometry.Vector2 VolumePosition, IScene scene, Alignment alignment = null, Anchor anchor = null, double fontSize = 16) : base(Text, VolumePosition, alignment, anchor, true, fontSize)
        {
            _Scene = scene;
        }

        public ScaledLabelView(string Text, LineSegment VolumePosition, IScene scene, Alignment alignment = null, Anchor anchor = null, double lineWidth = 16) : base(Text, VolumePosition, alignment, anchor, true, lineWidth)
        {
            _Scene = scene;
        }

        public ScaledLabelView(string Text, LineSegment VolumePosition, Color color, IScene scene, Alignment alignment = null, Anchor anchor = null, double lineWidth = 16) : base(Text, VolumePosition, color, alignment, anchor, true, lineWidth)
        {
            _Scene = scene;
        }

        public ScaledLabelView(string Text, Geometry.Vector2 VolumePosition, SpriteFont font, IScene scene, Alignment alignment = null, Anchor anchor = null, double fontSize = 16) : base(Text, VolumePosition, font, alignment, anchor, true, fontSize)
        {
            _Scene = scene;
        }

        public override Rectangle BoundingRect
        {

            get
            {
                double FontScaleForVolume = ScaleFontSizeToVolume(font, this.FontSize);
                var scaledFont = ScaleForMagnification(FontScaleForVolume, _Scene);
                var unanchoredBoundingRect = UnanchoredUnscaledBoundingRect;

                Geometry.Vector2 label_size = new(unanchoredBoundingRect.Width * scaledFont, unanchoredBoundingRect.Height * scaledFont);
                Geometry.Vector2 half_label_size = label_size / 2.0;

                Geometry.Vector2 origin = Position;
                Geometry.Vector2 offset = new(
                    Anchor.Horizontal == HorizontalAlignment.LEFT ? 0 : Anchor.Horizontal == HorizontalAlignment.RIGHT ? -label_size.X : -half_label_size.X,
                    Anchor.Vertical == VerticalAlignment.BOTTOM ? 0 : Anchor.Vertical == VerticalAlignment.TOP ? -label_size.Y : -half_label_size.Y
                );

                return new Rectangle(this.Position + offset, label_size.X, label_size.Y);
            }
        }

    }

    public class LabelView : IText, IColorView, IViewPosition2D, IRenderable, IAnchor, IAlignment
    {
        public readonly Anchor Anchor; //Readonly because we listen to a delegate
        public readonly Alignment Alignment; //Readonly because we listen to a delegate

        HorizontalAlignment IAnchor.Horizontal { get => ((IAnchor)Anchor).Horizontal; set => ((IAnchor)Anchor).Horizontal = value; }
        VerticalAlignment IAnchor.Vertical { get => ((IAnchor)Anchor).Vertical; set => ((IAnchor)Anchor).Vertical = value; }

        HorizontalAlignment IAlignment.Horizontal { get => ((IAnchor)Alignment).Horizontal; set => ((IAnchor)Alignment).Horizontal = value; }
        VerticalAlignment IAlignment.Vertical { get => ((IAnchor)Alignment).Vertical; set => ((IAnchor)Alignment).Vertical = value; }

        /// <summary>
        /// Label must be this large to render
        /// </summary>
        //static float LabelVisibleCutoff = 7f;

        static readonly byte DefaultAlpha = 192;

        public Microsoft.Xna.Framework.Color _Color = new((byte)(0),
                                                                                    (byte)(0),
                                                                                    (byte)(0),
                                                                                    DefaultAlpha);
        public Color Color
        {
            get => _Color;
            set => _Color = value;
        }

        public float Alpha
        {
            get => this._Color.GetAlpha();
            set => _Color = this._Color.SetAlpha(value);
        }

        public float Rotation
        {
            get; set;
        } = 0f;

        private double _MaxLineWidth = double.MaxValue;

        public double MaxLineWidth
        {
            get => _MaxLineWidth;
            set
            {
                if (_MaxLineWidth != value)
                    InvalidateTexture();
                _IsMeasured = _MaxLineWidth == value && _IsMeasured;
                _MaxLineWidth = value;
            }
        }

        private SpriteFont _font = null;

        /// <summary>
        /// Font used for this label
        /// </summary>
        public SpriteFont font
        {
            get => _font;
            set
            {
                _IsMeasured = _font == value && _IsMeasured;
                _font = value;
            }
        }

        private double _FontSize;

        /// <summary>
        /// Font size should be the size of the font in volume space pixels
        /// </summary>
        public double FontSize
        {
            get => _FontSize;
            set
            {
                if (_FontSize != value)
                    InvalidateTexture();
                _IsMeasured = _IsMeasured && _FontSize == value;
                _FontSize = value;
            }
        }

        protected static double ScaleFontSizeToVolume(SpriteFont font, double fontsize) => fontsize / font.LineSpacing;

        /// <summary>
        /// We have to scale the font to match the scale we need to use for the label sprites.
        /// </summary>
        //private double _FontSizeScaledToVolume;

        private bool _IsMeasured = false;
        private string[] _Rows = null; //The label text divided across rows
        private Vector2[] _RowMeasurements; // Measurements for each row

        // Texture caching for async rendering
        private RenderTarget2D _LabelTexture;
        private bool _TextureGenerating = false;
        private bool _TextureInvalidated = true;
        private CancellationTokenSource _TextureGenerationCts;

        /// <summary>
        /// True if the label should change size as the user zooms in and out.  Used to keep the label proportional to other objects rendered in a scene.
        /// False if the label is a constant size regarless of the scene.  Used for informational labels that aren't attached to objects in the scene.
        /// </summary>
        public bool ScaleFontWithScene { get; set; }

        public LabelView(string Text, Geometry.Vector2 VolumePosition, Color color, Alignment alignment = null, Anchor anchor = null, bool scaleFontWithScene = true, double fontSize = 16.0)
            : this(Text, VolumePosition, Global.DefaultFont, alignment, anchor, scaleFontWithScene, fontSize)
        {
            this._Color = color;
        }

        public LabelView(string Text, Geometry.Vector2 VolumePosition, Alignment alignment = null, Anchor anchor = null, bool scaleFontWithScene = true, double fontSize = 16.0)
            : this(Text, VolumePosition, Global.DefaultFont, alignment, anchor, scaleFontWithScene, fontSize)
        {
        }

        public LabelView(string Text, LineSegment VolumePosition, Alignment alignment = null, Anchor anchor = null, bool scaleFontWithScene = true, double lineWidth = 16.0)
            : this(Text, VolumePosition.PointAlongLine(0.5), Global.DefaultFont, alignment, anchor, scaleFontWithScene, lineWidth)
        {
            Geometry.Vector2 direction = VolumePosition.Direction;
            this.Rotation = (float)Geometry.Vector2.ArcAngle(Geometry.Vector2.Zero, Geometry.Vector2.UnitX, direction);
            //this.Rotation = (float)Math.Atan2(direction.X, direction.Y);
        }

        public LabelView(string Text, LineSegment VolumePosition, Color color, Alignment alignment = null, Anchor anchor = null, bool scaleFontWithScene = true, double lineWidth = 16.0)
            : this(Text, VolumePosition, alignment, anchor, scaleFontWithScene, lineWidth)
        {
            Geometry.Vector2 direction = VolumePosition.Direction;
            this.Rotation = (float)Geometry.Vector2.ArcAngle(Geometry.Vector2.Zero, Geometry.Vector2.UnitX, direction);
            this.Color = color;
            //this.Rotation = (float)Math.Atan2(direction.X, direction.Y);
        }

        public LabelView(string Text, Geometry.Vector2 VolumePosition, SpriteFont font, Alignment alignment = null, Anchor anchor = null, bool scaleFontWithScene = true, double fontSize = 16.0)
        {
            this.font = font;
            this._FontSize = fontSize;
            this.Text = Text;
            this.Position = VolumePosition;
            //Create copies of anchor and alignment so we can set OnChange action properly
            this.Anchor = anchor is null ? new Anchor { Horizontal = HorizontalAlignment.CENTER, Vertical = VerticalAlignment.CENTER } : new Anchor { Horizontal = anchor.Horizontal, Vertical = anchor.Vertical };
            this.Alignment = alignment is null ? new Alignment { Horizontal = HorizontalAlignment.CENTER, Vertical = VerticalAlignment.CENTER } : new Alignment { Horizontal = alignment.Horizontal, Vertical = alignment.Vertical };
            this.ScaleFontWithScene = scaleFontWithScene;
        }

        private string _Text;
        public string Text
        {
            get => _Text;
            set
            {
                if (_Text != value)
                InvalidateTexture();
                _IsMeasured = _IsMeasured && _Text == value;
                _Text = value;
            }
        }

        private Geometry.Vector2 _Position;
        public Geometry.Vector2 Position
        {
            get => _Position;
            set => _Position = value;
        }

        /// <summary>
        /// Returns the measured bounding box of the text in the label.  It does not scale the bounding box to the scene if needed or translate the bounding box according to the anchor.
        /// </summary>
        protected Rectangle UnanchoredUnscaledBoundingRect
        {
            get
            {
                if (!_IsMeasured)
                {
                    MeasureLabel();
                }

                var Width = _RowMeasurements.Max(m => m.X);
                var Height = _RowMeasurements.Sum(m => m.Y);

                return new Rectangle(this.Position, Width, Height);
            }
        }


        public virtual Rectangle BoundingRect
        {

            get
            {
                double FontScaleForVolume = ScaleFontSizeToVolume(font, this.FontSize);
                var unanchoredBoundingRect = UnanchoredUnscaledBoundingRect;

                Geometry.Vector2 label_size = new(unanchoredBoundingRect.Width * FontScaleForVolume, unanchoredBoundingRect.Height * FontScaleForVolume);
                Geometry.Vector2 half_label_size = label_size / 2.0;

                Geometry.Vector2 origin = Position;
                Geometry.Vector2 offset = new(
                    Anchor.Horizontal == HorizontalAlignment.LEFT ? 0 : Anchor.Horizontal == HorizontalAlignment.RIGHT ? -label_size.X : -half_label_size.X,
                    Anchor.Vertical == VerticalAlignment.BOTTOM ? 0 : Anchor.Vertical == VerticalAlignment.TOP ? -label_size.Y : -half_label_size.Y
                    );

                return new Rectangle(this.Position + offset, label_size.X, label_size.Y);
            }
        }

        /// <summary>
        /// Returns the measured bounding box of the text in the label.
        /// This bounding rect is not scaled for magnification if ScaleFontSizeForMagnification is set to true
        /// </summary>
        public Rectangle GetAnchoredBoundingRect(IScene scene)
        {
            if (!_IsMeasured)
            {
                MeasureLabel();
            }

            double FontScaleForVolume = ScaleFontSizeToVolume(font, this.FontSize);

            double Width = _RowMeasurements.Max(m => m.X);
            double Height = _RowMeasurements.Sum(m => m.Y);

            Geometry.Vector2 label_size = new(Width * FontScaleForVolume, Height * FontScaleForVolume);
            Geometry.Vector2 half_label_size = label_size / 2.0;

            Geometry.Vector2 origin = Position;
            Geometry.Vector2 offset = new(
                Anchor.Horizontal == HorizontalAlignment.LEFT ? 0 : Anchor.Horizontal == HorizontalAlignment.RIGHT ? -label_size.X : -half_label_size.X,
                Anchor.Vertical == VerticalAlignment.BOTTOM ? 0 : Anchor.Vertical == VerticalAlignment.TOP ? -label_size.Y : -half_label_size.Y
            );

            return new Rectangle(this.Position + offset, Width * FontScaleForVolume, Height * FontScaleForVolume);
        }


        /// <summary>
        /// Fonts are always the same size, they aren't rendered on a texture or anything.  So we have to scale the font according to the magnification requested by the viewer.
        /// </summary>
        /// <param name="MagnificationFactor"></param>
        /// <returns>Fraction (0 to 1) of the screen's Y-axis the font will display upon. </returns>
        protected double ScaleForMagnification(double FontSize, VikingXNA.IScene scene)
        {
            Vector3 center = scene.Viewport.Project(Position.ToXNAVector3(0), scene.Projection, scene.View, scene.World);
            Vector3 topedge = scene.Viewport.Project(Position.ToXNAVector3(0) - new Vector3(0, (float)FontSize / 2, 0), scene.Projection, scene.View, scene.World);
            //return FontSize / scene.Camera.Downsample;
            return (topedge.Y - center.Y) * 2;
        }


        /// <summary>
        /// What does the font size need to be to fit the provided bounds?
        /// </summary>
        /// <param name="bbox"></param>
        /// <param name="Padding_factor">Scalar to indicate how much padding to add around text. 1.05 = 5% additional space around text</param>
        /// <returns></returns>
        public double GetFontSizeToFitBounds(Rectangle bbox, Geometry.Vector2? Padding_factor = null)
        {
            Padding_factor ??= new Geometry.Vector2(1, 1);
            //Determine how to fix the text within the width of the rectangle

            double FontScaleForVolume = ScaleFontSizeToVolume(font, this.FontSize);
            string[] Rows = this.Text.Split('\n');
            //int MinRows = Rows.Length;
            Vector2[] RowMeasurements = font.MeasureStrings(Rows);

            //List<string> row_list = Rows.ToList();

            double bbox_aspect = bbox.Width / bbox.Height;

            double text_width = RowMeasurements.Max(m => m.X);
            double text_height = RowMeasurements.Sum(m => m.Y);
            double row_height = RowMeasurements.Average(m => m.Y);

            double padded_width = text_width * Padding_factor.Value.X;
            double padded_height = text_height * Padding_factor.Value.Y;

            //            double text_aspect = text_width / text_height;
            //            double padded_text_aspect = padded_width / padded_height;

            double horz_font_scale = bbox.Width / padded_width;
            double vert_font_scale = bbox.Height / padded_height;

            return Math.Min(horz_font_scale, vert_font_scale) * font.LineSpacing;

            //If our text is wider than our bbox aspect, add rows until our aspect is smaller
            //TODO: I'm not going to deal with wrapping text yet.  It could be flag that is added later
            /*
            while(text_aspect > bbox_aspect)
            {
                //Try wrapping the longest row of text to reduce the width of the text
                int widest_row = 0;
                for (int iRow = row_list.Count-1; iRow >= 0; iRow--)
                {
                    if(RowMeasurements[iRow].X == text_width)
                    {
                        
                    }
                }
                
            }
            */
        }

        private static bool IsLabelTooSmallToSee(double fontSizeInScreenFraction) => fontSizeInScreenFraction < (1.0 / 200.0); //Don't show if label is < 5% of screen's height

        public bool IsVisible(VikingXNA.Scene scene)
        {
            if (font is null) //The first time draw is called font is initialized.  So allow us to draw if we haven't initialized font yet.
                return true;

            double fontSizeInScreenPixels = ScaleForMagnification(this.FontSize, scene);

            //Don't draw labels if no human could read them
            return !IsLabelTooSmallToSee(fontSizeInScreenPixels / scene.Viewport.Height);
        }



        private static int NumberOfNewlines(string label) => label.Count(c => '\n' == c);

        /// <summary>
        /// Remove newlines from string, and push portion after the newline back on the stack
        /// </summary>
        /// <param name="strStack"></param>
        /// <param name="word"></param>
        /// <returns></returns>
        private static string SplitNewlines(Stack<string> strStack, string word, out bool NewlineFound)
        {
            NewlineFound = false;
            if (!word.Contains('\n'))
                return word;

            NewlineFound = true;
            string[] parts = word.Split(['\n'], 2);
            strStack.Push(parts[1]);
            return parts[0];
        }

        #region Multiline support
        /// <summary>
        /// Divide the label into multiple lines of no more than LineWidth size
        /// </summary>
        /// <param name="label">Text to display</param>
        /// <param name="LineWidth">Maximum length of a line of text</param>
        /// <param name="OutputRowMeasurements">Output parameter of the bounding box for each row of text</param>
        /// <returns>An array of each row's text</returns>
        private static string[] WrapText(string label, SpriteFont font, double fontScale, double LineWidth, out Vector2[] OutputRowMeasurements)
        {
            //Split the string at the first space before the midpoint
            Vector2 FullLabelMeasurement = font.MeasureString(label);
            int MaxRows = (int)Math.Ceiling((double)(FullLabelMeasurement.X * fontScale) / LineWidth) + NumberOfNewlines(label);
            //string[] labelParts = label.Split();
            Stack<string> labelStack = new(((IEnumerable<string>)label.Split([' ', '\r'], StringSplitOptions.RemoveEmptyEntries)).Reverse());

            //Shortcut the case where the label fits on one line
            if (FullLabelMeasurement.X * fontScale <= LineWidth && !label.Contains('\n'))
            {
                OutputRowMeasurements = [FullLabelMeasurement];
                return [label];
            }

            string[] rows = new string[MaxRows];
            Vector2[] rowMeasurements = new Vector2[MaxRows];

            int iRow = 0;
            while (labelStack.Count > 0)
            {
                string word = SplitNewlines(labelStack, labelStack.Pop(), out bool RequireNewRow);
                if (string.IsNullOrEmpty(rows[iRow])) //The row is still empty
                {
                    rows[iRow] = word;
                    rowMeasurements[iRow] = font.MeasureString(word);

                    //Check if we already exceeded the max width of the row with the first string added
                    if (rowMeasurements[iRow].X * fontScale > LineWidth)
                    {
                        RequireNewRow = true;
                    }
                }
                else
                {
                    string concatedatedRow = rows[iRow] + " " + word;
                    Vector2 concatenatedRowMeasurement = font.MeasureString(concatedatedRow);
                    if (concatenatedRowMeasurement.X * fontScale > LineWidth)  // The word makes the row too long
                    {
                        RequireNewRow = true;

                        labelStack.Push(word); //Push the word that exceeded the length back on the stack so we don't lose it. 

                        //rows[iRow + 1] = word;
                        //rowMeasurements[iRow + 1] = font.MeasureString(word);
                        // rowMeasurement[iRow] = font.MeasureString(rows[iRow]); //Measured the last time we added a word to this row
                    }
                    else //The word fits on the row
                    {
                        rows[iRow] = concatedatedRow;
                        rowMeasurements[iRow] = concatenatedRowMeasurement;
                    }
                }

                if (RequireNewRow)
                {
                    iRow++;
                    if (iRow >= MaxRows && labelStack.Count > 0)
                    {
                        //Replace the last three characters with "..." to indicate there was more text.
                        rows[iRow - 1] = rows[iRow - 1].Insert(rows[iRow - 1].Length - 3 < 0 ? 0 : rows[iRow - 1].Length - 3, "...");
                        break;
                    }
                }
            }


            rows = [.. rows.Where(r => !string.IsNullOrEmpty(r))];
            int NumRows = rows.Length;

            OutputRowMeasurements = new Vector2[NumRows];
            Array.Copy(rowMeasurements, OutputRowMeasurements, NumRows);

            return rows;
        }

        #endregion

        public static void Draw(SpriteBatch spriteBatch, SpriteFont font, VikingXNA.IScene scene, ICollection<LabelView> Labels)
        {
            if (Labels is null)
                return;

            if (Labels.Count == 0)
                return;

            font ??= Global.DefaultFont;


            BlendState originalBlendState = spriteBatch.GraphicsDevice.BlendState;
            DepthStencilState originalDepthState = spriteBatch.GraphicsDevice.DepthStencilState;
            RasterizerState originalRasterizerState = spriteBatch.GraphicsDevice.RasterizerState;
            SamplerState originalSamplerState = spriteBatch.GraphicsDevice.SamplerStates[0];
            // Vertex sampler slots are not guaranteed to exist on all backends (e.g. DesktopGL/OpenGL
            // exposes 0 vertex texture samplers), so reading [0] can throw IndexOutOfRangeException.
            SamplerState originalVSamplerState = null;
            try { originalVSamplerState = spriteBatch.GraphicsDevice.VertexSamplerStates[0]; }
            catch (IndexOutOfRangeException) { }

            try
            {
                spriteBatch.Begin();

                foreach (LabelView label in Labels.Where(l => l != null))
                {
                    label.Draw(spriteBatch, font, scene as VikingXNA.Scene);
                }

                spriteBatch.End();
            }
            finally
            {
                if (originalBlendState != null)
                    spriteBatch.GraphicsDevice.BlendState = originalBlendState;

                if (originalDepthState != null)
                    spriteBatch.GraphicsDevice.DepthStencilState = originalDepthState;

                if (originalRasterizerState != null)
                    spriteBatch.GraphicsDevice.RasterizerState = originalRasterizerState;

                if (originalSamplerState != null)
                    spriteBatch.GraphicsDevice.SamplerStates[0] = originalSamplerState;

                if (originalVSamplerState != null)
                    spriteBatch.GraphicsDevice.VertexSamplerStates[0] = originalVSamplerState;
            }
        }

        /*
        private Vector2 PositionAdjustmentForAnchro(Vector2 v, Anchor anchor)
        {
            Vector2 v; 

            switch (Anchor.Horizontal)
            {
                case HorizontalAlignment.CENTER:
                    break;
                case HorizontalAlignment.LEFT:
                    v.X = -(row_measurement.X / 2.0f);
                case HorizontalAlignment.RIGHT:
                    return new Vector2(v.X + (row_measurement.X / 2.0f), v.Y);
                default:
                    throw new InvalidOperationException(string.Format("Unexpected horizontal alignment {0}", Anchor.Horizontal)); 
            } 
        
            switch (Anchor.Vertical)
            {
                case VerticalAlignment.CENTER:
                    return v;
                case VerticalAlignment.TOP:
                    return new Vector2(v.X, v.Y - (row_measurement.Y / 2.0f));
                case VerticalAlignment.BOTTOM:
                    return new Vector2(v.X, v.Y + (row_measurement.Y / 2.0f));
                default:
                    throw new InvalidOperationException(string.Format("Unexpected vertical alignment {0}", Anchor.Vertical));

            }
            return v;

        }
        */


        private static Vector2 AlignmentAdjustmentForRow(Vector2 row_measurement, Rectangle bounds, Vector2 max_row_size, Alignment alignment)
        {
            Vector2 origin = new();

            origin.X = alignment.Horizontal switch
            {
                HorizontalAlignment.CENTER => (row_measurement.X - max_row_size.X) / 2.0f,
                HorizontalAlignment.LEFT => 0,
                HorizontalAlignment.RIGHT => row_measurement.X - max_row_size.X,
                _ => throw new InvalidOperationException(string.Format("Unexpected horizontal alignment {0}", alignment.Horizontal)),
            };
            origin.Y = alignment.Vertical switch
            {
                VerticalAlignment.CENTER => (row_measurement.Y - max_row_size.Y) / 2.0f,
                VerticalAlignment.TOP => 0,
                VerticalAlignment.BOTTOM => row_measurement.Y - max_row_size.Y,
                _ => throw new InvalidOperationException(string.Format("Unexpected vertical alignment {0}", alignment.Vertical)),
            };
            return origin;
        }

        private void MeasureLabel()
        {
            double FontScaleForVolume = ScaleFontSizeToVolume(font, this.FontSize);
            this._Rows = WrapText(this.Text, this.font, FontScaleForVolume, this.MaxLineWidth, out this._RowMeasurements);
            _IsMeasured = true;
        }

        #region Texture Caching

        /// <summary>
        /// Begin async texture generation for this label.
        /// Cancels any in-progress generation if label properties have changed.
        /// </summary>
        private void BeginInvokeGenerateTexture(GraphicsDevice device, SpriteBatch spriteBatch, SpriteFont font)
        {
            if (_TextureGenerating)
                return;

            // Cancel any previous generation
            _TextureGenerationCts?.Cancel();
            _TextureGenerationCts?.Dispose();
            _TextureGenerationCts = new CancellationTokenSource();
            var token = _TextureGenerationCts.Token;

            _TextureGenerating = true;

            GpuSynchronizationManager.RunTask(() =>
            {
                if (token.IsCancellationRequested)
                {
                    _TextureGenerating = false;
                    return;
                }

                var texture = CreateTextureForLabel(device, spriteBatch, font);

                // Only assign if not cancelled
                if (!token.IsCancellationRequested)
                {
                    _LabelTexture?.Dispose();
                    _LabelTexture = texture;
                    _TextureInvalidated = false;
                }
                else
                {
                    texture?.Dispose();
                }

                _TextureGenerating = false;
            }, token);
        }

        /// <summary>
        /// Get the cached label texture, or trigger generation if needed.
        /// Returns null if texture is not ready yet.
        /// </summary>
        protected Texture2D GetOrCreateLabelTexture(GraphicsDevice device, SpriteBatch spriteBatch, SpriteFont font)
        {
            if (_TextureInvalidated || _LabelTexture == null)
            {
                BeginInvokeGenerateTexture(device, spriteBatch, font);
                return null; // Texture not ready yet
            }

            if (_LabelTexture.IsDisposed)
            {
                _LabelTexture = null;
                _TextureInvalidated = true;
                BeginInvokeGenerateTexture(device, spriteBatch, font);
                return null; // Texture not ready yet
            }

            return _LabelTexture;
        }

        /// <summary>
        /// Create a texture for this label, rendering text in white so color can be applied as tint at draw time.
        /// Supports multi-line text using existing _Rows and _RowMeasurements.
        /// </summary>
        private RenderTarget2D CreateTextureForLabel(GraphicsDevice device, SpriteBatch spriteBatch, SpriteFont font)
        {
            if (string.IsNullOrEmpty(this.Text) || font == null)
                return null;

            // Ensure label is measured
            if (!_IsMeasured)
            {
                MeasureLabel();
            }

            if (_Rows == null || _Rows.Length == 0)
                return null;

            // Calculate texture dimensions based on row measurements
            double FontScaleForVolume = ScaleFontSizeToVolume(font, this.FontSize);
            float fontScale = (float)FontScaleForVolume;

            float maxWidth = _RowMeasurements.Max(r => r.X) * fontScale;
            float totalHeight = _RowMeasurements.Sum(r => r.Y) * fontScale;

            if (maxWidth <= 0 || totalHeight <= 0)
                return null;

            RenderTarget2D target = new(device, (int)Math.Ceiling(maxWidth), (int)Math.Ceiling(totalHeight), 
                mipMap: true, preferredFormat: SurfaceFormat.Color, preferredDepthFormat: DepthFormat.None);

            // Save current render targets
            RenderTargetBinding[] oldRenderTargets = device.GetRenderTargets();
            
            device.SetRenderTarget(target);
            device.Clear(Color.Transparent);

            spriteBatch.Begin();

            // Draw all rows in white (color will be applied as tint at draw time)
            float yPos = 0;
            for (int iRow = 0; iRow < _Rows.Length; iRow++)
            {
                spriteBatch.DrawString(font, _Rows[iRow], new Vector2(0, yPos), Color.White, 
                    this.Rotation, Vector2.Zero, fontScale, SpriteEffects.None, 0);
                yPos += _RowMeasurements[iRow].Y * fontScale;
            }

            spriteBatch.End();

            // Restore render targets
            device.SetRenderTargets(oldRenderTargets);

            return target;
        }

        /// <summary>
        /// Invalidate and dispose the cached texture.
        /// Cancels any in-progress generation.
        /// </summary>
        private void InvalidateTexture()
        {
            // Cancel any in-progress generation
            _TextureGenerationCts?.Cancel();
            _TextureGenerationCts?.Dispose();
            _TextureGenerationCts = null;

            _TextureInvalidated = true;
            _TextureGenerating = false;
            _LabelTexture?.Dispose();
            _LabelTexture = null;
        }

        /// <summary>
        /// Draw the label using cached texture with color tinting (for screen-space rendering).
        /// Falls back to direct DrawString if texture is not ready yet.
        /// </summary>
        /// <param name="spriteBatch">SpriteBatch for rendering</param>
        /// <param name="font">Font to use (for fallback rendering)</param>
        /// <param name="device">GraphicsDevice (for texture generation)</param>
        /// <param name="screenPosition">Screen-space position to draw at</param>
        /// <param name="drawScale">Scale factor at draw time (e.g. 1.0 to 2.0). Texture is drawn at base size * drawScale without regenerating.</param>
        public void DrawWithTexture(SpriteBatch spriteBatch, SpriteFont font, GraphicsDevice device, Vector2 screenPosition, float drawScale = 1.0f)
        {
            if (string.IsNullOrEmpty(this.Text) || font == null)
                return;

            // Ensure font is set
            this.font = font;

            // Ensure label is measured
            if (!_IsMeasured)
            {
                MeasureLabel();
            }

            if (_Rows == null || _Rows.Length == 0)
                return;

            // Try to get cached texture
            Texture2D texture = GetOrCreateLabelTexture(device, spriteBatch, font);

            if (texture == null)
            {
                // Texture not ready yet - fall back to direct DrawString
                double FontScaleForVolume = ScaleFontSizeToVolume(font, this.FontSize);
                float fontScale = (float)FontScaleForVolume * drawScale;

                // Calculate total height for centering
                float totalHeight = _RowMeasurements.Sum(r => r.Y) * fontScale;
                float maxWidth = _RowMeasurements.Max(r => r.X) * fontScale;

                // Draw all rows, centered vertically
                float yPos = screenPosition.Y - totalHeight / 2.0f;
                for (int iRow = 0; iRow < _Rows.Length; iRow++)
                {
                    Vector2 rowMeasurement = _RowMeasurements[iRow] * fontScale;
                    Vector2 drawPos = new Vector2(
                        screenPosition.X - maxWidth / 2.0f,
                        yPos
                    );
                    spriteBatch.DrawString(font, _Rows[iRow], drawPos, this._Color, 
                        this.Rotation, Vector2.Zero, fontScale, SpriteEffects.None, 0);
                    yPos += rowMeasurement.Y;
                }
            }
            else
            {
                // Draw texture with color tint at draw-time scale (no texture regeneration)
                // SpriteBatch uses premultiplied alpha blend, so tint must be premultiplied for opacity to work
                float textureWidth = texture.Width * drawScale;
                float textureHeight = texture.Height * drawScale;

                // Center the texture at the screen position
                Vector2 drawPos = new Vector2(
                    screenPosition.X - textureWidth / 2.0f,
                    screenPosition.Y - textureHeight / 2.0f
                );

                var destRect = new Microsoft.Xna.Framework.Rectangle((int)drawPos.X, (int)drawPos.Y, (int)textureWidth, (int)textureHeight);
                byte a = this._Color.A;
                Color premultiplied = new Color(
                    (byte)(this._Color.R * a / 255),
                    (byte)(this._Color.G * a / 255),
                    (byte)(this._Color.B * a / 255),
                    a);
                spriteBatch.Draw(texture, destRect, null, premultiplied);
            }
        }

        #endregion

        /// <summary>
        /// Draw a single label. 
        /// The caller is expected to call Begin and End on the sprite batch.  They should also preserve all state on the graphics device. 
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="font"></param>
        /// <param name="scene"></param>
        public void Draw(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                            Microsoft.Xna.Framework.Graphics.SpriteFont font,
                            VikingXNA.IScene scene)
        {
            double fontSizeInScreenPixels = ScaleForMagnification(this.FontSize, scene);

            if (this.ScaleFontWithScene && IsLabelTooSmallToSee(fontSizeInScreenPixels / scene.Viewport.Height))
                return;

            if (spriteBatch is null)
                throw new ArgumentNullException(nameof(spriteBatch));

            //Update our font, will clear the measurements if the font has changed.
            this.font = font ?? throw new ArgumentNullException(nameof(font));
            //Scale is used to adjust for the magnification factor of the viewer.  Otherwise text would remain at constant size regardless of mag factor.
            //offsets must be multiplied by scale before use
            double FontScaleForVolume = ScaleFontSizeToVolume(font, this.FontSize);

            if (!_IsMeasured)////!_IsMeasured)
            {
                MeasureLabel();
            }

            if (this._Rows is null || this._Rows.Length == 0)
                return;

            //Vector3 LocationCenterScreenPosition_v3 = scene.Viewport.Project(Position.ToXNAVector3(0), scene.Projection, scene.View, scene.World);
            Rectangle bounds = BoundingRect;
            Vector3 LocationCenterScreenPosition_v3 = scene.Viewport.Project(bounds.UpperLeft.ToXNAVector3(0), scene.Projection, scene.View, scene.World);
            Vector2 LocationCenterScreenPosition = new(LocationCenterScreenPosition_v3.X, LocationCenterScreenPosition_v3.Y);

            //scene.WorldToScreen(this.Position).ToXNAVector2();

            float fontScale = this.ScaleFontWithScene ? (float)ScaleForMagnification(FontScaleForVolume, scene) : (float)FontScaleForVolume;

            float LineStep = (float)font.LineSpacing * fontScale;  //How much do we increment Y to move down a line?
            float yOffset = -((float)font.LineSpacing) * fontScale;  //What is the offset to draw the line at the correct position?  We have to draw below label if it exists
                                                                     //However we only need to drop half a line since the label straddles the center

            Vector2 max_row_size = new(_RowMeasurements.Max(r => r.X), _RowMeasurements.Max(r => r.Y));

            for (int iRow = 0; iRow < _Rows.Length; iRow++)
            {
                Vector2 DrawPosition = LocationCenterScreenPosition;

                //DrawPosition = AdjustPositionForHorzAlignment(DrawPosition, _RowMeasurements[iRow]);
                //DrawPosition = AdjustPositionForVertAlignment(DrawPosition, _RowMeasurements[iRow]);
                DrawPosition.Y += LineStep * iRow;
                Vector2 origin = AlignmentAdjustmentForRow(_RowMeasurements[iRow], bounds, max_row_size, Alignment);

                spriteBatch.DrawString(font,
                                       _Rows[iRow],
                                       DrawPosition,
                                       this._Color,
                                       this.Rotation,
                                       origin, //_RowMeasurements[iRow] / 2.0f, //The string is centered on the drawing position, instead of starting at the top left
                                       fontScale,
                                       SpriteEffects.None,
                                       0);
            }
        }


        public void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items)
        {
            var fontData = DeviceFontStore.TryGet(device);
            LabelView.Draw(fontData.SpriteBatch, fontData.Font, scene, [.. items.Select(i => i as LabelView).Where(i => i != null)]);
        }

        public void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay)
        {
            var fontData = DeviceFontStore.TryGet(device);
            LabelView.Draw(fontData.SpriteBatch, fontData.Font, scene, [this]);
        }
    }
}
