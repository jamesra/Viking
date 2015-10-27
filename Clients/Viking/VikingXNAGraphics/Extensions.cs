using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VikingXNAGraphics
{
    public static class ColorExtensions
    {
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
                                                    alpha);
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
    }
}

