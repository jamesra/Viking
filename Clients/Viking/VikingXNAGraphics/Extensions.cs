using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text;
using System.Threading.Tasks;

namespace VikingXNAGraphics
{


    public enum LineStyle
    {
        Standard,
        AlphaGradient,
        NoBlur,
        AnimatedLinear,
        AnimatedBidirectional,
        AnimatedRadial,
        Modern,
        Tubular,
        Glow,
        Texture
    }

    public static class FloatExtensions
    {
        /// <summary>
        /// Return the power of two less than or equal to the passed value
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static double FloorToPowerOfTwo(this double value)
        {
            return Math.Pow(Math.Floor(Math.Log(value, 2)), 2);
        }
    }

    public static class LineManagerExtensions
    {
        public static string ToString(this LineStyle style)
        {
            switch (style)
            {
                case LineStyle.Standard:
                    return "Standard";
                case LineStyle.AlphaGradient:
                    return "AlphaGradient";
                case LineStyle.NoBlur:
                    return "NoBlur";
                case LineStyle.AnimatedLinear:
                    return "AnimatedLinear";
                case LineStyle.AnimatedBidirectional:
                    return "AnimatedBidirectional";
                case LineStyle.AnimatedRadial:
                    return "AnimatedRadial";
                case LineStyle.Modern:
                    return "Modern";
                case LineStyle.Tubular:
                    return "Tubular";
                case LineStyle.Glow:
                    return "Glow";
                case LineStyle.Texture:
                    return "Texture";
                default:
                    throw new ArgumentException("Unknown line style " + style.ToString());
            }
        }
    }

    public static class VectorExtensions
    {
        public static Microsoft.Xna.Framework.Vector2 ToVector2(this Geometry.GridVector2 vec)
        {
            return new Vector2((float)vec.X, (float)vec.Y);
        }

        public static Geometry.GridVector2 ToGridVector(this Vector2 vec)
        {
            return new Geometry.GridVector2(vec.X, vec.Y);
        }
    }

    public static class ColorExtensions
    {
        /// <summary>
        /// Return alpha 0 to 1
        /// </summary>
        /// <param name="color"></param>
        /// <param name="alpha"></param>
        /// <returns></returns>
        public static float GetAlpha(this Microsoft.Xna.Framework.Color color)
        {
            return (float)color.A / 255.0f;
        }

        public static Microsoft.Xna.Framework.Color SetAlpha(this Microsoft.Xna.Framework.Color color, float alpha)
        {
            if(alpha > 1.0f || alpha < 0f)
            {
                throw new ArgumentOutOfRangeException("Alpha value must be between 0 to 1.0");
            }

            Vector3 colorVector = color.ToVector3();
            return new Microsoft.Xna.Framework.Color(colorVector.X, colorVector.Y, colorVector.Z, alpha);
        }

        public static Microsoft.Xna.Framework.Color ToXNAColor(this System.Drawing.Color color)
        {
            return new Microsoft.Xna.Framework.Color((int)color.R,
                                                    (int)color.G,
                                                    (int)color.B,
                                                    (int)color.A);
        }

        public static Microsoft.Xna.Framework.Color ToXNAColor(this System.Drawing.Color color, float alpha)
        {
            return new Microsoft.Xna.Framework.Color((int)color.R,
                                                    (int)color.G,
                                                    (int)color.B,
                                                    (int)(alpha * 255.0f));
        }

        public static Microsoft.Xna.Framework.Color ToXNAColor(this int color, float alpha)
        {
            System.Drawing.Color sysColor = System.Drawing.Color.FromArgb(color);
            return sysColor.ToXNAColor(alpha);
        }

        public static Microsoft.Xna.Framework.Color ToXNAColor(this int color)
        {
            System.Drawing.Color sysColor = System.Drawing.Color.FromArgb(color);
            return sysColor.ToXNAColor();
        }

        public static Microsoft.Xna.Framework.Color ConvertToHSL(this Microsoft.Xna.Framework.Color color, float alpha)
        {
            System.Drawing.Color WinColor = System.Drawing.Color.FromArgb(color.R, color.G, color.B);

            Microsoft.Xna.Framework.Color HSLColor = new Microsoft.Xna.Framework.Color();
            HSLColor.R = (byte)(255.0 * (WinColor.GetHue() / 360.0));
            HSLColor.G = (byte)(255.0 * WinColor.GetSaturation());
            HSLColor.B = (byte)((color.R * 0.3) + (color.G * 0.59) + (color.B * 0.11));
            HSLColor.A = (byte)(alpha * 255f);

            return HSLColor;
        }

        public static Microsoft.Xna.Framework.Color ConvertToHSL(this Microsoft.Xna.Framework.Color color)
        {
            return color.ConvertToHSL((float)color.A / 255f);
        }

        public static Microsoft.Xna.Framework.Color ConvertToHSL(this System.Drawing.Color WinColor, float alpha)
        {
            Microsoft.Xna.Framework.Color HSLColor = new Microsoft.Xna.Framework.Color();
            HSLColor.R = (byte)(255.0 * (WinColor.GetHue() / 360.0));
            HSLColor.G = (byte)(255.0 * WinColor.GetSaturation());
            HSLColor.B = (byte)(((float)WinColor.R * 0.3) + ((float)WinColor.G * 0.59) + ((float)WinColor.B * 0.11));
            HSLColor.A = (byte)(alpha * 255f);

            return HSLColor;
        }

        public static Microsoft.Xna.Framework.Color ConvertToHSL(this System.Drawing.Color WinColor)
        {
            return WinColor.ConvertToHSL((float)WinColor.A / 255f);
        }

        public static Vector2[] MeasureStrings(this SpriteFont font, string[] lines)
        {
            return lines.Select(line => font.MeasureString(line)).ToArray();
        }
    }
}

