using Microsoft.Xna.Framework;

namespace VikingXNAGraphics
{
    /// <summary>
    /// Displays a single integer by composing shared digit textures at draw time.
    /// Does not own digit textures; uses INumberDisplayDrawContext to draw.
    /// </summary>
    public class NumberDisplayView
    {
        /// <summary>
        /// The integer value to display (e.g. section number).
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// Screen-space position (center of the composed number).
        /// </summary>
        public Vector2 Position { get; set; }

        /// <summary>
        /// Color (including alpha) for the number.
        /// </summary>
        public Microsoft.Xna.Framework.Color Color { get; set; }

        /// <summary>
        /// Scale factor at draw time (applied to digit textures; overlay sets this from display scale and section zoom).
        /// </summary>
        public float DrawScale { get; set; } = 1.0f;

        /// <summary>
        /// Renders any number at the given position with the given color and scale using the context. No instance required.
        /// </summary>
        public static void Draw(INumberDisplayDrawContext context, int value, Vector2 position, Microsoft.Xna.Framework.Color color, float drawScale)
        {
            if (context == null)
                return;

            string str = value.ToString();
            if (string.IsNullOrEmpty(str))
                return;

            float totalWidth = 0f;
            for (int i = 0; i < str.Length; i++)
            {
                int digitIndex = str[i] - '0';
                if (digitIndex < 0 || digitIndex > 9)
                    continue;
                totalWidth += context.GetDigitWidth(digitIndex) * drawScale;
            }

            float leftX = position.X - totalWidth / 2f;

            for (int i = 0; i < str.Length; i++)
            {
                int digitIndex = str[i] - '0';
                if (digitIndex < 0 || digitIndex > 9)
                    continue;

                float digitWidth = context.GetDigitWidth(digitIndex) * drawScale;
                float digitCenterX = leftX + digitWidth / 2f;
                Vector2 digitCenter = new Vector2(digitCenterX, position.Y);

                context.DrawDigit(digitIndex, digitCenter, drawScale, color);

                leftX += digitWidth;
            }
        }

        /// <summary>
        /// Draw the number by composing digits using the context. Uses this instance's Value, Position, Color, DrawScale.
        /// </summary>
        public void Draw(INumberDisplayDrawContext context)
        {
            Draw(context, Value, Position, Color, DrawScale);
        }
    }
}
