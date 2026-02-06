using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNAGraphics
{
    /// <summary>
    /// Abstraction for drawing digit textures so that NumberDisplayView does not depend on SpriteBatch or SpriteFont.
    /// Implemented by the overlay (or adapter) using SharedDigitTextureCache + SpriteBatch + font + device.
    /// </summary>
    public interface INumberDisplayDrawContext
    {
        /// <summary>
        /// Draw the digit texture at the given center with scale and color (texture-based, like LabelView.DrawWithTexture).
        /// </summary>
        void DrawDigit(int digitIndex, Vector2 centerPosition, float drawScale, Color color);

        /// <summary>
        /// Width of the digit in fixed high-res space (~128 px texture height). Caller multiplies by draw scale for display.
        /// </summary>
        float GetDigitWidth(int digitIndex);

        /// <summary>
        /// Height of a digit in fixed high-res space (~128 px texture height). Caller multiplies by draw scale for display.
        /// </summary>
        float GetDigitHeight();
    }
}
