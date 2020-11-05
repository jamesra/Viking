namespace VikingXNAWinForms
{
    public static class WinFormColorExtensions
    {
        public static System.Drawing.Color SetAlpha(this System.Drawing.Color WinColor, float alpha)
        {
            return System.Drawing.Color.FromArgb((int)(alpha * 255.0), WinColor);
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


        public static System.Drawing.Color ToSystemColor(this Microsoft.Xna.Framework.Color color)
        {
            return System.Drawing.Color.FromArgb((int)color.A, (int)color.R, (int)color.G, (int)color.B);
        }

    }


    public static class MouseButtonExtensions
    {
        public static bool Left(this System.Windows.Forms.MouseButtons buttons)
        {
            return (int)(buttons & System.Windows.Forms.MouseButtons.Left) == (int)System.Windows.Forms.MouseButtons.Left;
        }

        public static bool LeftOnly(this System.Windows.Forms.MouseButtons buttons)
        {
            return buttons == System.Windows.Forms.MouseButtons.Left;
        }

        public static bool Right(this System.Windows.Forms.MouseButtons buttons)
        {
            return (int)(buttons & System.Windows.Forms.MouseButtons.Right) == (int)System.Windows.Forms.MouseButtons.Right;
        }

        public static bool RightOnly(this System.Windows.Forms.MouseButtons buttons)
        {
            return buttons == System.Windows.Forms.MouseButtons.Right;
        }

        public static bool Middle(this System.Windows.Forms.MouseButtons buttons)
        {
            return (int)(buttons & System.Windows.Forms.MouseButtons.Middle) == (int)System.Windows.Forms.MouseButtons.Middle;
        }

        public static bool MiddleOnly(this System.Windows.Forms.MouseButtons buttons)
        {
            return buttons == System.Windows.Forms.MouseButtons.Middle;
        }

        public static bool X1(this System.Windows.Forms.MouseButtons buttons)
        {
            return (int)(buttons & System.Windows.Forms.MouseButtons.XButton1) == (int)System.Windows.Forms.MouseButtons.XButton1;
        }

        public static bool X1Only(this System.Windows.Forms.MouseButtons buttons)
        {
            return buttons == System.Windows.Forms.MouseButtons.XButton1;
        }

        public static bool X2(this System.Windows.Forms.MouseButtons buttons)
        {
            return (int)(buttons & System.Windows.Forms.MouseButtons.XButton2) == (int)System.Windows.Forms.MouseButtons.XButton2;
        }

        public static bool X2Only(this System.Windows.Forms.MouseButtons buttons)
        {
            return buttons == System.Windows.Forms.MouseButtons.XButton2;
        }

        public static bool None(this System.Windows.Forms.MouseButtons buttons)
        {
            return buttons == System.Windows.Forms.MouseButtons.None;
        }
    }

}
