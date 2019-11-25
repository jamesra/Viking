using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Geometry;
using VikingXNAGraphics;

namespace VikingXNAGraphics
{
    public enum HorizontalAlignment
    {
        CENTER,
        LEFT,
        RIGHT
    };

    public enum VerticalAlignment
    {
        CENTER,
        TOP,
        BOTTOM
    };

    public class LabelView : IText, IColorView, IViewPosition2D
    {
        public HorizontalAlignment HorzAlign = HorizontalAlignment.CENTER;
        public VerticalAlignment VertAlign = VerticalAlignment.CENTER;

        /// <summary>
        /// Label must be this large to render
        /// </summary>
        static float LabelVisibleCutoff = 7f;

        static byte DefaultAlpha = 192;

        public Microsoft.Xna.Framework.Color _Color = new Microsoft.Xna.Framework.Color((byte)(0),
                                                                                    (byte)(0),
                                                                                    (byte)(0),
                                                                                    DefaultAlpha);
        public Color Color
        {
            get { return _Color; }
            set { _Color = value; }
        }

        public float Alpha
        {
            get
            {
                return this._Color.GetAlpha();
            }
            set
            {
                _Color = this._Color.SetAlpha(value);
            }
        }

        private double _MaxLineWidth = double.MaxValue;

        public double MaxLineWidth
        {
            get { return _MaxLineWidth; }
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
            get { return _font; }
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
            get { return _FontSize; }
            set
            {
                _IsMeasured = _IsMeasured && _FontSize == value;
                _FontSize = value;
            }
        }

        private static double ScaleFontSizeToVolume(SpriteFont font, double fontsize)
        {
            return fontsize / font.LineSpacing;
        }

        /// <summary>
        /// We have to scale the font to match the scale we need to use for the label sprites.
        /// </summary>
        private double _FontSizeScaledToVolume;

        private bool _IsMeasured = false;
        private string[] _Rows = null; //The label text divided across rows
        private Vector2[] _RowMeasurements; // Measurements for each row

        /// <summary>
        /// True if the label should change size as the user zooms in and out.  Used to keep the label proportional to other objects rendered in a scene.
        /// False if the label is a constant size regarless of the scene.  Used for informational labels that aren't attached to objects in the scene.
        /// </summary>
        public bool ScaleFontWithScene {get; set;} 

        public LabelView(string Text, GridVector2 VolumePosition, HorizontalAlignment hAlign = HorizontalAlignment.CENTER, VerticalAlignment vAlign = VerticalAlignment.CENTER, bool scaleFontWithScene = true, double fontSize = 16.0)
        {
            this._FontSize = fontSize;
            this.Text = Text;
            this.Position = VolumePosition;
            this.VertAlign = vAlign;
            this.HorzAlign = hAlign;
            this.ScaleFontWithScene = scaleFontWithScene;
        }

        private string _Text;
        public string Text
        {
            get
            { return _Text; }
            set
            {
                _IsMeasured = _IsMeasured && _Text == value;
                _Text = value;
            }
        }

        private GridVector2 _Position;
        public GridVector2 Position
        {
            get
            {
                return _Position;
            }
            set
            {
                _Position = value;
            }
        }
        
        private static bool IsLabelTooSmallToSee(double fontSizeInScreenPixels)
        {
            return fontSizeInScreenPixels < 6.0;
        }

        public bool IsVisible(VikingXNA.Scene scene)
        {
            if (font == null) //The first time draw is called font is initialized.  So allow us to draw if we haven't initialized font yet.
                return true;
             
            double fontSizeInScreenPixels = ScaleFontSizeForMagnification(this.FontSize, scene);

            //Don't draw labels if no human could read them
            return !IsLabelTooSmallToSee(fontSizeInScreenPixels);
        }

        /// <summary>
        /// Fonts are always the same size, they aren't rendered on a texture or anything.  So we have to scale the font according to the magnification requested by the viewer.
        /// </summary>
        /// <param name="MagnificationFactor"></param>
        /// <returns></returns>
        private static double ScaleFontSizeForMagnification(double FontSize, VikingXNA.Scene scene)
        {
            return FontSize / scene.Camera.Downsample;
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
        /// <param name="label"></param>
        /// <returns></returns>
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
                }
                else
                {
                    string concatedatedRow = rows[iRow] + " " + word;
                    Vector2 concatenatedRowMeasurement = font.MeasureString(concatedatedRow);
                    if (concatenatedRowMeasurement.X * fontScale > LineWidth)  // The word makes the row too long
                    {
                        RequireNewRow = true;
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
                    labelStack.Push(word);

                    if (iRow >= MaxRows)
                    {
                        rows[iRow - 1] = rows[iRow - 1] + "...";
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

        public static void Draw(SpriteBatch spriteBatch, SpriteFont font, VikingXNA.Scene scene, ICollection<LabelView> Labels)
        {
            if (Labels == null)
                return;

            if (Labels.Count == 0)
                return;

            BlendState originalBlendState = spriteBatch.GraphicsDevice.BlendState;
            DepthStencilState originalDepthState = spriteBatch.GraphicsDevice.DepthStencilState;
            RasterizerState originalRasterizerState = spriteBatch.GraphicsDevice.RasterizerState;
            SamplerState originalSamplerState = spriteBatch.GraphicsDevice.SamplerStates[0];
            SamplerState originalVSamplerState = spriteBatch.GraphicsDevice.VertexSamplerStates[0];
            

            spriteBatch.Begin();

            foreach(LabelView label in Labels.Where(l => l != null))
            {
                label.Draw(spriteBatch, font, scene);
            }

            spriteBatch.End();

            spriteBatch.GraphicsDevice.BlendState = originalBlendState;
            spriteBatch.GraphicsDevice.DepthStencilState = originalDepthState;
            spriteBatch.GraphicsDevice.RasterizerState = originalRasterizerState;

            if(originalSamplerState != null)
                spriteBatch.GraphicsDevice.SamplerStates[0] = originalSamplerState;

            if (originalVSamplerState != null)
                spriteBatch.GraphicsDevice.VertexSamplerStates[0] = originalVSamplerState;
            
        }

        private Vector2 AdjustPositionForHorzAlignment(Vector2 v, Vector2 row_measurement)
        {
            switch (this.HorzAlign)
            {
                case HorizontalAlignment.CENTER:
                    return v;
                case HorizontalAlignment.LEFT:
                    return new Vector2(v.X - (row_measurement.X / 2.0f), v.Y);
                case HorizontalAlignment.RIGHT:
                    return new Vector2(v.X + (row_measurement.X / 2.0f), v.Y);
                default:
                    throw new InvalidOperationException(string.Format("Unexpected horizontal alignment {0}", this.HorzAlign)); 
            } 
        }

        private Vector2 AdjustPositionForVertAlignment(Vector2 v, Vector2 row_measurement)
        {
            switch (this.VertAlign)
            {
                case VerticalAlignment.CENTER:
                    return v;
                case VerticalAlignment.TOP:
                    return new Vector2(v.X, v.Y - (row_measurement.Y / 2.0f));
                case VerticalAlignment.BOTTOM:
                    return new Vector2(v.X, v.Y + (row_measurement.Y / 2.0f));
                default:
                    throw new InvalidOperationException(string.Format("Unexpected vertical alignment {0}", this.VertAlign));

            } 
        }

        private static Vector2 OriginForRow(Vector2 row_measurement, Vector2 max_row_size, HorizontalAlignment hAlign, VerticalAlignment vAlign)
        {
            Vector2 origin = new Vector2();

            switch (hAlign)
            {
                case HorizontalAlignment.CENTER:
                    origin.X = row_measurement.X / 2.0f;
                    break;
                case HorizontalAlignment.LEFT:
                    origin.X = 0;
                    break;
                case HorizontalAlignment.RIGHT:
                    origin.X = -(max_row_size.X - row_measurement.X);
                    break;
                default:
                    throw new InvalidOperationException(string.Format("Unexpected horizontal alignment {0}", hAlign));
            }

            switch (vAlign)
            {
                case VerticalAlignment.CENTER:
                    origin.Y = row_measurement.Y / 2.0f;
                    break;
                case VerticalAlignment.TOP:
                    origin.Y = 0;
                    break;
                case VerticalAlignment.BOTTOM:
                    origin.Y = -(max_row_size.Y - row_measurement.Y);
                    break;
                default:
                    throw new InvalidOperationException(string.Format("Unexpected vertical alignment {0}", vAlign));
            }

            return origin;

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
                              VikingXNA.Scene scene)
        {
            double fontSizeInScreenPixels = ScaleFontSizeForMagnification(this.FontSize, scene);

            if (this.ScaleFontWithScene && IsLabelTooSmallToSee(fontSizeInScreenPixels))
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
                this._Rows = WrapText(this.Text, this.font, FontScaleForVolume, this.MaxLineWidth, out this._RowMeasurements);
                _IsMeasured = true;
            }

            if (this._Rows == null || this._Rows.Length == 0)
                return; 

            Vector2 LocationCenterScreenPosition = scene.WorldToScreen(this.Position).ToXNAVector2();

            float fontScale = this.ScaleFontWithScene ? (float)ScaleFontSizeForMagnification(FontScaleForVolume, scene) : (float)FontScaleForVolume;

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
                Vector2 origin = OriginForRow(_RowMeasurements[iRow], max_row_size, HorzAlign, VertAlign);
                
                spriteBatch.DrawString(font,
                    _Rows[iRow],
                    DrawPosition,
                    this._Color,
                    0,
                    origin, //_RowMeasurements[iRow] / 2.0f, //The string is centered on the drawing position, instead of starting at the top left
                    fontScale,
                    SpriteEffects.None,
                    0);
            }
        }
    }
}
