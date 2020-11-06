using System;
using System.Windows.Forms;
using VikingXNAGraphics.Controls;

namespace Viking.UI
{
    public static class MouseButtonExtensions
    {
        public static VikingXNAGraphics.Controls.MouseButton ToVikingButton(this System.Windows.Forms.MouseButtons button)
        {
            switch (button)
            {
                case MouseButtons.Left:
                    return MouseButton.LEFT;
                case MouseButtons.Right:
                    return MouseButton.RIGHT;
                case MouseButtons.Middle:
                    return MouseButton.MIDDLE;
                case MouseButtons.XButton1:
                    return MouseButton.X1;
                case MouseButtons.XButton2:
                    return MouseButton.X2;
                case MouseButtons.None:
                    return MouseButton.NONE;
                default:
                    throw new ArgumentException(string.Format("Unknown button type {0}", button));
            }
        }

        public static System.Windows.Forms.MouseButtons ToWinFormButton(this VikingXNAGraphics.Controls.MouseButton button)
        {
            switch (button)
            {
                case MouseButton.LEFT:
                    return MouseButtons.Left;
                case MouseButton.RIGHT:
                    return MouseButtons.Right;
                case MouseButton.MIDDLE:
                    return MouseButtons.Middle;
                case MouseButton.X1:
                    return MouseButtons.XButton1;
                case MouseButton.X2:
                    return MouseButtons.XButton2;
                case MouseButton.NONE:
                    return MouseButtons.None;
                default:
                    throw new ArgumentException(string.Format("Unknown button type {0}", button));
            }
        }
    }
}
