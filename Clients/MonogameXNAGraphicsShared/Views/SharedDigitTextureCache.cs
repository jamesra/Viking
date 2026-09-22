using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace VikingXNAGraphics
{
    /// <summary>
    /// Shared cache of digit 0-9 LabelViews and their display widths/height.
    /// Digit textures are created at a fixed high resolution (~128 px) and are mipmapped (LabelView uses mipMap: true).
    /// </summary>
    public class SharedDigitTextureCache
    {
        /// <summary>
        /// Target texture height in pixels for digit textures (~128 px). GPU selects mip level when scaling at draw time.
        /// </summary>
        public const float DigitTextureResolution = 128f;

        private readonly LabelView[] _digitLabels = new LabelView[10];
        private readonly float[] _digitWidths = new float[10];
        private float _digitHeight;
        private bool _initialized;
        private SpriteFont _lastFont;

        /// <summary>
        /// Create the 10 digit LabelViews if needed; set FontSize so texture height ≈ DigitTextureResolution; recompute widths/height.
        /// </summary>
        public void EnsureInitialized(SpriteFont font)
        {
            if (font == null)
                return;

            if (_initialized && _lastFont == font)
                return;

            _lastFont = font;
            float measureY = font.MeasureString("0").Y;
            if (measureY <= 0)
                return;

            // FontSize so texture height ≈ DigitTextureResolution: texture height = measureY * (FontSize / font.LineSpacing) => FontSize = DigitTextureResolution * font.LineSpacing / measureY
            double fontSize = DigitTextureResolution * font.LineSpacing / measureY;

            for (int i = 0; i < 10; i++)
            {
                string digit = i.ToString();
                if (_digitLabels[i] == null)
                {
                    _digitLabels[i] = new LabelView(
                        digit,
                        Geometry.Vector2.Zero,
                        Color.White,
                        Alignment.CenterCenter,
                        Anchor.CenterCenter,
                        scaleFontWithScene: false,
                        fontSize: fontSize
                    );
                }
                else
                {
                    _digitLabels[i].FontSize = fontSize;
                    _digitLabels[i].Text = digit;
                }
            }

            // Recompute widths/height at this fixed resolution (texture space)
            for (int i = 0; i < 10; i++)
            {
                Vector2 m = font.MeasureString(i.ToString());
                double fontScale = fontSize / font.LineSpacing;
                _digitWidths[i] = (float)(m.X * fontScale);
            }
            Vector2 m0 = font.MeasureString("0");
            _digitHeight = (float)(m0.Y * (fontSize / font.LineSpacing));
            _initialized = true;
        }

        /// <summary>
        /// Draw the digit at the given center with scale and color. Uses the digit LabelView's DrawWithTexture (texture-based with fallback to DrawString).
        /// Caller must have called spriteBatch.Begin() with appropriate state.
        /// </summary>
        public void DrawDigit(SpriteBatch spriteBatch, GraphicsDevice device, SpriteFont font, int digitIndex, Vector2 centerPosition, float drawScale, Color color)
        {
            if (digitIndex < 0 || digitIndex > 9 || !_initialized)
                return;
            LabelView label = _digitLabels[digitIndex];
            if (label == null)
                return;
            label.font = font;
            label.Position = new Geometry.Vector2(centerPosition.X, centerPosition.Y);
            label.Color = color;
            label.DrawWithTexture(spriteBatch, font, device, centerPosition, drawScale);
        }

        /// <summary>
        /// Width of the digit in fixed high-res space (~128 px texture height).
        /// </summary>
        public float GetDigitWidth(int digitIndex)
        {
            if (digitIndex < 0 || digitIndex > 9)
                return 0f;
            return _digitWidths[digitIndex];
        }

        /// <summary>
        /// Height of a digit in fixed high-res space (~128 px texture height).
        /// </summary>
        public float GetDigitHeight()
        {
            return _digitHeight;
        }

        /// <summary>
        /// Sum of digit widths for the given string (e.g. "42" or MaxSectionNumber.ToString()). In fixed high-res space.
        /// </summary>
        public float GetWidthForNumber(string digits)
        {
            if (string.IsNullOrEmpty(digits))
                return 0f;
            float total = 0f;
            foreach (char c in digits)
            {
                int i = c - '0';
                if (i >= 0 && i <= 9)
                    total += _digitWidths[i];
            }
            return total;
        }
    }
}
