using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Geometry;
using VikingXNAGraphics;

namespace WebAnnotation.View
{
    public class LabelView
    {
        /// <summary>
        /// Label must be this large to render
        /// </summary>
        static float LabelVisibleCutoff = 7f;

        static byte DefaultAlpha = 192;

        public Microsoft.Xna.Framework.Color Color = new Microsoft.Xna.Framework.Color((byte)(0),
                                                                                    (byte)(0),
                                                                                    (byte)(0),
                                                                                    DefaultAlpha);

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

        private double _FontSize = 16.0;

        public double FontSize
        {
            get { return _FontSize; }
            set
            {
                _IsMeasured = _IsMeasured && _FontSize == value;
                _FontSize = value;
            }
        }

        public float Alpha
        {
            get
            {
                return (float)this.Color.A / 255.0f;
            }
            set
            {
                Color = new Microsoft.Xna.Framework.Color(Color.R, Color.G, Color.B, (byte)(value * 255.0f));
            }
        }


        private bool _IsMeasured = false;
        private string[] _Rows = null; //The label text divided across rows
        private Vector2[] _RowMeasurements; // Measurements for each row

        public LabelView(string Text, GridVector2 VolumePosition)
        {
            this.Text = Text;
            this.Position = VolumePosition;
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

        private static bool IsLabelTooSmallToSee(float fontScale, float LineSpacing)
        {
            return LineSpacing * fontScale < LabelView.LabelVisibleCutoff;
        }

        private static bool ScaleReducedForLowMag(float baseScale)
        {
            return baseScale < 1.0;
        }

        /// <summary>
        /// Fonts are always the same size, they aren't rendered on a texture or anything.  So we have to scale the font according to the magnification requested by the viewer.
        /// </summary>
        /// <param name="MagnificationFactor"></param>
        /// <returns></returns>
        private static float GetFontSizeAdjustedForMagnification(float FontSize, float MagnificationFactor)
        {
            return ((float)FontSize * MagnificationFactor);
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
            int MaxRows = (int)Math.Ceiling((double)FullLabelMeasurement.X / LineWidth) + NumberOfNewlines(label);
            //string[] labelParts = label.Split();
            Stack<string> labelStack = new Stack<string>(label.Split(' ').Reverse());

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
                }
            }


            rows = rows.Where(r => !string.IsNullOrEmpty(r)).ToArray();
            int NumRows = rows.Length;

            OutputRowMeasurements = new Vector2[NumRows];
            Array.Copy(rowMeasurements, OutputRowMeasurements, NumRows);

            return rows;
        }

        #endregion

        public void Draw(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                              Microsoft.Xna.Framework.Graphics.SpriteFont font,
                              VikingXNA.Scene scene,
                              float MagnificationFactor)
        {
            Vector2 LocationCenterScreenPosition = scene.WorldToScreen(this.Position).ToVector2();
            if (font == null)
                throw new ArgumentNullException("font");

            if (spriteBatch == null)
                throw new ArgumentNullException("spriteBatch");

            //Update our font, will clear the measurements if the font has changed.
            this.font = font;

            //Scale is used to adjust for the magnification factor of the viewer.  Otherwise text would remain at constant size regardless of mag factor.
            //offsets must be multiplied by scale before use
            float fontScale = GetFontSizeAdjustedForMagnification((float)this.FontSize, MagnificationFactor);
            bool LowMagScale = ScaleReducedForLowMag(fontScale);

            //Don't draw labels if no human could read them
            if (IsLabelTooSmallToSee(fontScale, font.LineSpacing))
                return;

            if (true)////!_IsMeasured)
            {
                this._Rows = WrapText(this.Text, this.font, this.FontSize, this.MaxLineWidth, out this._RowMeasurements);
                _IsMeasured = true;
            }

            float LineStep = font.LineSpacing * fontScale;  //How much do we increment Y to move down a line?
            float yOffset = -(font.LineSpacing) * fontScale;  //What is the offset to draw the line at the correct position?  We have to draw below label if it exists
                                                              //However we only need to drop half a line since the label straddles the center


            for (int iRow = 0; iRow < _Rows.Length; iRow++)
            {
                Vector2 DrawPosition = LocationCenterScreenPosition;
                DrawPosition.Y += LineStep * iRow;

                spriteBatch.DrawString(font,
                    _Rows[iRow],
                    DrawPosition,
                    this.Color,
                    0,
                    _RowMeasurements[iRow] / 2.0f, //The string is centered on the drawing position, instead of starting at the top left
                    fontScale,
                    SpriteEffects.None,
                    0);
            }
        }
    }
}
