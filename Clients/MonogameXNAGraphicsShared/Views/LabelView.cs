using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using VikingXNA;

namespace VikingXNAGraphics
{
    public class ScaledLabelView : LabelView
    {
        private readonly IScene _Scene;

        public ScaledLabelView(string Text, GridVector2 VolumePosition, Color color, IScene scene, Alignment alignment = null, Anchor anchor = null, double fontSize = 16) : base(Text, VolumePosition, color, alignment, anchor, true, fontSize)
        {
            _Scene = scene;
        }

        public ScaledLabelView(string Text, GridVector2 VolumePosition, IScene scene, Alignment alignment = null, Anchor anchor = null, double fontSize = 16) : base(Text, VolumePosition, alignment, anchor, true, fontSize)
        {
            _Scene = scene;
        }

        public ScaledLabelView(string Text, GridLineSegment VolumePosition, IScene scene, Alignment alignment = null, Anchor anchor = null, double lineWidth = 16) : base(Text, VolumePosition, alignment, anchor, true, lineWidth)
        {
            _Scene = scene;
        }

        public ScaledLabelView(string Text, GridLineSegment VolumePosition, Color color, IScene scene, Alignment alignment = null, Anchor anchor = null, double lineWidth = 16) : base(Text, VolumePosition, color, alignment, anchor, true, lineWidth)
        {
            _Scene = scene;
        }

        public ScaledLabelView(string Text, GridVector2 VolumePosition, SpriteFont font, IScene scene, Alignment alignment = null, Anchor anchor = null, double fontSize = 16) : base(Text, VolumePosition, font, alignment, anchor, true, fontSize)
        {
            _Scene = scene;
        }

        public override GridRectangle BoundingRect{
        
            get{ 
                double FontScaleForVolume = ScaleFontSizeToVolume(font, this.FontSize);
                var scaledFont = ScaleForMagnification(FontScaleForVolume, _Scene);
                var unanchoredBoundingRect = UnanchoredUnscaledBoundingRect;

                GridVector2 label_size = new GridVector2(unanchoredBoundingRect.Width * scaledFont, unanchoredBoundingRect.Height * scaledFont);
                GridVector2 half_label_size = label_size / 2.0;

                GridVector2 origin = Position;
                GridVector2 offset = new GridVector2(
                    Anchor.Horizontal == HorizontalAlignment.LEFT ? 0 : Anchor.Horizontal == HorizontalAlignment.RIGHT ? -label_size.X : -half_label_size.X,
                    Anchor.Vertical == VerticalAlignment.BOTTOM ? 0 : Anchor.Vertical == VerticalAlignment.TOP ? -label_size.Y : -half_label_size.Y
                );

                return new GridRectangle(this.Position + offset, label_size.X, label_size.Y);
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

        static byte DefaultAlpha = 192;

        public Microsoft.Xna.Framework.Color _Color = new Microsoft.Xna.Framework.Color((byte)(0),
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

        /// <summary>
        /// True if the label should change size as the user zooms in and out.  Used to keep the label proportional to other objects rendered in a scene.
        /// False if the label is a constant size regarless of the scene.  Used for informational labels that aren't attached to objects in the scene.
        /// </summary>
        public bool ScaleFontWithScene { get; set; }

        public LabelView(string Text, GridVector2 VolumePosition, Color color, Alignment alignment = null, Anchor anchor = null, bool scaleFontWithScene = true, double fontSize = 16.0)
            : this(Text, VolumePosition, Global.DefaultFont, alignment, anchor, scaleFontWithScene, fontSize)
        {
            this._Color = color;
        }

        public LabelView(string Text, GridVector2 VolumePosition, Alignment alignment = null, Anchor anchor = null, bool scaleFontWithScene = true, double fontSize = 16.0)
            : this(Text, VolumePosition, Global.DefaultFont, alignment, anchor, scaleFontWithScene, fontSize)
        {
        }

        public LabelView(string Text, GridLineSegment VolumePosition, Alignment alignment = null, Anchor anchor = null, bool scaleFontWithScene = true, double lineWidth = 16.0)
            : this(Text, VolumePosition.PointAlongLine(0.5), Global.DefaultFont, alignment, anchor, scaleFontWithScene, lineWidth)
        {
            GridVector2 direction = VolumePosition.Direction;
            this.Rotation = (float)GridVector2.ArcAngle(GridVector2.Zero, GridVector2.UnitX, direction);
            //this.Rotation = (float)Math.Atan2(direction.X, direction.Y);
        }

        public LabelView(string Text, GridLineSegment VolumePosition, Color color, Alignment alignment = null, Anchor anchor = null, bool scaleFontWithScene = true, double lineWidth = 16.0)
            : this(Text, VolumePosition, alignment, anchor, scaleFontWithScene, lineWidth)
        {
            GridVector2 direction = VolumePosition.Direction;
            this.Rotation = (float)GridVector2.ArcAngle(GridVector2.Zero, GridVector2.UnitX, direction);
            this.Color = color;
            //this.Rotation = (float)Math.Atan2(direction.X, direction.Y);
        }

        public LabelView(string Text, GridVector2 VolumePosition, SpriteFont font, Alignment alignment = null, Anchor anchor = null, bool scaleFontWithScene = true, double fontSize = 16.0)
        {
            this.font = font;
            this._FontSize = fontSize;
            this.Text = Text;
            this.Position = VolumePosition;
            //Create copies of anchor and alignment so we can set OnChange action properly
            this.Anchor = anchor == null ? new Anchor { Horizontal = HorizontalAlignment.CENTER, Vertical = VerticalAlignment.CENTER } : new Anchor { Horizontal = anchor.Horizontal, Vertical = anchor.Vertical };
            this.Alignment = alignment == null ? new Alignment { Horizontal = HorizontalAlignment.CENTER, Vertical = VerticalAlignment.CENTER } : new Alignment { Horizontal = alignment.Horizontal, Vertical = alignment.Vertical };
            this.ScaleFontWithScene = scaleFontWithScene;
        }

        private string _Text;
        public string Text
        {
            get => _Text;
            set
            {
                _IsMeasured = _IsMeasured && _Text == value;
                _Text = value;
            }
        }

        private GridVector2 _Position;
        public GridVector2 Position
        {
            get => _Position;
            set => _Position = value;
        }

        /// <summary>
        /// Returns the measured bounding box of the text in the label.  It does not scale the bounding box to the scene if needed or translate the bounding box according to the anchor.
        /// </summary>
        protected GridRectangle UnanchoredUnscaledBoundingRect
        {
            get
            {
                if (!_IsMeasured)
                {
                    MeasureLabel();
                }

                var Width = _RowMeasurements.Max(m => m.X);
                var Height = _RowMeasurements.Sum(m => m.Y);

                return new GridRectangle(this.Position, Width, Height);
            }
        }


        public virtual GridRectangle BoundingRect{
        
            get{ 
                double FontScaleForVolume = ScaleFontSizeToVolume(font, this.FontSize);
                var unanchoredBoundingRect = UnanchoredUnscaledBoundingRect;

                GridVector2 label_size = new GridVector2(unanchoredBoundingRect.Width * FontScaleForVolume, unanchoredBoundingRect.Height * FontScaleForVolume);
                GridVector2 half_label_size = label_size / 2.0;

                GridVector2 origin = Position;
                GridVector2 offset = new GridVector2(
                    Anchor.Horizontal == HorizontalAlignment.LEFT ? 0 : Anchor.Horizontal == HorizontalAlignment.RIGHT ? -label_size.X : -half_label_size.X,
                    Anchor.Vertical == VerticalAlignment.BOTTOM ? 0 : Anchor.Vertical == VerticalAlignment.TOP ? -label_size.Y : -half_label_size.Y
                    );

                return new GridRectangle(this.Position + offset, label_size.X, label_size.Y);
            }
        }

        /// <summary>
        /// Returns the measured bounding box of the text in the label.
        /// This bounding rect is not scaled for magnification if ScaleFontSizeForMagnification is set to true
        /// </summary>
        public GridRectangle GetAnchoredBoundingRect(IScene scene)
        {
            if (!_IsMeasured)
            {
                MeasureLabel();
            }

            double FontScaleForVolume = ScaleFontSizeToVolume(font, this.FontSize);

            double Width = _RowMeasurements.Max(m => m.X);
            double Height = _RowMeasurements.Sum(m => m.Y);

            GridVector2 label_size = new GridVector2(Width * FontScaleForVolume, Height * FontScaleForVolume);
            GridVector2 half_label_size = label_size / 2.0;

            GridVector2 origin = Position;
            GridVector2 offset = new GridVector2(
                Anchor.Horizontal == HorizontalAlignment.LEFT ? 0 : Anchor.Horizontal == HorizontalAlignment.RIGHT ? -label_size.X : -half_label_size.X,
                Anchor.Vertical == VerticalAlignment.BOTTOM ? 0 : Anchor.Vertical == VerticalAlignment.TOP ? -label_size.Y : -half_label_size.Y
            );

            return new GridRectangle(this.Position + offset, Width * FontScaleForVolume, Height * FontScaleForVolume); 
        }

        
        /// <summary>
        /// Fonts are always the same size, they aren't rendered on a texture or anything.  So we have to scale the font according to the magnification requested by the viewer.
        /// </summary>
        /// <param name="MagnificationFactor"></param>
        /// <returns>Fraction (0 to 1) of the screen's Y-axis the font will display upon. </returns>
        protected double ScaleForMagnification(double FontSize, VikingXNA.IScene scene)
        {
            Vector3 center  = scene.Viewport.Project(Position.ToXNAVector3(0), scene.Projection, scene.View, scene.World);
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
        public double GetFontSizeToFitBounds(GridRectangle bbox, GridVector2? Padding_factor=null)
        {
            if(Padding_factor == null)
            {
                Padding_factor = new GridVector2 { X = 1, Y = 1 };
            }
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

        private static bool IsLabelTooSmallToSee(double fontSizeInScreenFraction)
        {
            return fontSizeInScreenFraction < (1.0 / 200.0); //Don't show if label is < 5% of screen's height
        }

        public bool IsVisible(VikingXNA.Scene scene)
        {
            if (font == null) //The first time draw is called font is initialized.  So allow us to draw if we haven't initialized font yet.
                return true;
             
            double fontSizeInScreenPixels = ScaleForMagnification(this.FontSize, scene);

            //Don't draw labels if no human could read them
            return !IsLabelTooSmallToSee(fontSizeInScreenPixels / scene.Viewport.Height);
        }

        

        private static int NumberOfNewlines(string label)
        {
            return label.Count(c => '\n' == c);
        }

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
            string[] parts = word.Split(new char[] { '\n' }, 2);
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
            Stack<string> labelStack = new Stack<string>(label.Split(new char[] { ' ', '\r' }, StringSplitOptions.RemoveEmptyEntries).Reverse());

            //Shortcut the case where the label fits on one line
            if (FullLabelMeasurement.X * fontScale <= LineWidth && !label.Contains('\n'))
            {
                OutputRowMeasurements = new Vector2[] { FullLabelMeasurement };
                return new string[] { label };
            }

            string[] rows = new string[MaxRows];
            Vector2[] rowMeasurements = new Vector2[MaxRows];

            int iRow = 0;
            while (labelStack.Count > 0)
            {
                bool RequireNewRow = false;
                string word = SplitNewlines(labelStack, labelStack.Pop(), out RequireNewRow);
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


            rows = rows.Where(r => !string.IsNullOrEmpty(r)).ToArray();
            int NumRows = rows.Length;

            OutputRowMeasurements = new Vector2[NumRows];
            Array.Copy(rowMeasurements, OutputRowMeasurements, NumRows);

            return rows;
        }

        #endregion

        public static void Draw(SpriteBatch spriteBatch, SpriteFont font, VikingXNA.IScene scene, ICollection<LabelView> Labels)
        {
            if (Labels == null)
                return;

            if (Labels.Count == 0)
                return;

            if (font == null)
                font = Global.DefaultFont;

            BlendState originalBlendState = spriteBatch.GraphicsDevice.BlendState;
            DepthStencilState originalDepthState = spriteBatch.GraphicsDevice.DepthStencilState;
            RasterizerState originalRasterizerState = spriteBatch.GraphicsDevice.RasterizerState;
            SamplerState originalSamplerState = spriteBatch.GraphicsDevice.SamplerStates[0];
            SamplerState originalVSamplerState = spriteBatch.GraphicsDevice.VertexSamplerStates[0];
            

            spriteBatch.Begin();

            foreach(LabelView label in Labels.Where(l => l != null))
            {
                label.Draw(spriteBatch, font, scene as VikingXNA.Scene);
            }

            spriteBatch.End();

            if(originalBlendState != null)
                spriteBatch.GraphicsDevice.BlendState = originalBlendState;

            if (originalDepthState != null)
                spriteBatch.GraphicsDevice.DepthStencilState = originalDepthState;

            if(originalRasterizerState != null)
                spriteBatch.GraphicsDevice.RasterizerState = originalRasterizerState;

            if(originalSamplerState != null)
                spriteBatch.GraphicsDevice.SamplerStates[0] = originalSamplerState;

            if (originalVSamplerState != null)
                spriteBatch.GraphicsDevice.VertexSamplerStates[0] = originalVSamplerState;
            
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
         

        private static Vector2 AlignmentAdjustmentForRow(Vector2 row_measurement, GridRectangle bounds, Vector2 max_row_size, Alignment alignment)
        {
            Vector2 origin = new Vector2();

            switch (alignment.Horizontal)
            {
                case HorizontalAlignment.CENTER:
                    origin.X = (row_measurement.X - max_row_size.X) / 2.0f;
                    break;
                case HorizontalAlignment.LEFT:
                    origin.X = 0;
                    break;
                case HorizontalAlignment.RIGHT:
                    origin.X = row_measurement.X - max_row_size.X;
                    break;
                default:
                    throw new InvalidOperationException(string.Format("Unexpected horizontal alignment {0}", alignment.Horizontal));
            }

            switch (alignment.Vertical)
            {
                case VerticalAlignment.CENTER:
                    origin.Y = (row_measurement.Y - max_row_size.Y) / 2.0f;
                    break;
                case VerticalAlignment.TOP:
                    origin.Y = 0;
                    break;
                case VerticalAlignment.BOTTOM:
                    origin.Y = row_measurement.Y - max_row_size.Y;
                    break;
                default:
                    throw new InvalidOperationException(string.Format("Unexpected vertical alignment {0}", alignment.Vertical));
            }

            return origin; 
        }

        private void MeasureLabel()
        { 
            double FontScaleForVolume = ScaleFontSizeToVolume(font, this.FontSize);
            this._Rows = WrapText(this.Text, this.font, FontScaleForVolume, this.MaxLineWidth, out this._RowMeasurements);
            _IsMeasured = true;
        }
        
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

            if (font == null)
                throw new ArgumentNullException("font");

            if (spriteBatch == null)
                throw new ArgumentNullException("spriteBatch");
            
            //Update our font, will clear the measurements if the font has changed.
            this.font = font;
            //Scale is used to adjust for the magnification factor of the viewer.  Otherwise text would remain at constant size regardless of mag factor.
            //offsets must be multiplied by scale before use
            double FontScaleForVolume = ScaleFontSizeToVolume(font, this.FontSize);
             
            if (!_IsMeasured)////!_IsMeasured)
            {
                MeasureLabel();
            }

            if (this._Rows == null || this._Rows.Length == 0)
                return;

            //Vector3 LocationCenterScreenPosition_v3 = scene.Viewport.Project(Position.ToXNAVector3(0), scene.Projection, scene.View, scene.World);
            GridRectangle bounds = BoundingRect;
            Vector3 LocationCenterScreenPosition_v3 = scene.Viewport.Project(bounds.UpperLeft.ToXNAVector3(0), scene.Projection, scene.View, scene.World);
            Vector2 LocationCenterScreenPosition = new Vector2(LocationCenterScreenPosition_v3.X, LocationCenterScreenPosition_v3.Y);

    //scene.WorldToScreen(this.Position).ToXNAVector2();

            float fontScale = this.ScaleFontWithScene ? (float)ScaleForMagnification(FontScaleForVolume, scene) : (float)FontScaleForVolume;

            float LineStep = (float)font.LineSpacing * fontScale;  //How much do we increment Y to move down a line?
            float yOffset = -((float)font.LineSpacing) * fontScale;  //What is the offset to draw the line at the correct position?  We have to draw below label if it exists
                                                                     //However we only need to drop half a line since the label straddles the center

            Vector2 max_row_size = new Vector2(_RowMeasurements.Max(r => r.X), _RowMeasurements.Max(r => r.Y));

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
            LabelView.Draw(fontData.SpriteBatch, fontData.Font, scene, items.Select(i => i as LabelView).Where(i => i != null).ToArray());
        }

        public void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay)
        {
            var fontData = DeviceFontStore.TryGet(device);
            LabelView.Draw(fontData.SpriteBatch, fontData.Font, scene, new LabelView[] { this });
        }
    }
}
