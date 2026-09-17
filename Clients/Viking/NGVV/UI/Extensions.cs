using System;
using System.Windows.Forms;
using VikingXNAGraphics.Controls;

namespace Viking.UI
{
    public static class MouseButtonExtensions
    {
        public static VikingXNAGraphics.Controls.MouseButton ToVikingButton(this System.Windows.Forms.MouseButtons button)
        {
            return button switch
            {
                MouseButtons.Left => MouseButton.LEFT,
                MouseButtons.Right => MouseButton.RIGHT,
                MouseButtons.Middle => MouseButton.MIDDLE,
                MouseButtons.XButton1 => MouseButton.X1,
                MouseButtons.XButton2 => MouseButton.X2,
                MouseButtons.None => MouseButton.NONE,
                _ => throw new ArgumentException(string.Format("Unknown button type {0}", button)),
            };
        }

        public static System.Windows.Forms.MouseButtons ToWinFormButton(this VikingXNAGraphics.Controls.MouseButton button)
        {
            return button switch
            {
                MouseButton.LEFT => MouseButtons.Left,
                MouseButton.RIGHT => MouseButtons.Right,
                MouseButton.MIDDLE => MouseButtons.Middle,
                MouseButton.X1 => MouseButtons.XButton1,
                MouseButton.X2 => MouseButtons.XButton2,
                MouseButton.NONE => MouseButtons.None,
                _ => throw new ArgumentException(string.Format("Unknown button type {0}", button)),
            };
        }
    }
}
