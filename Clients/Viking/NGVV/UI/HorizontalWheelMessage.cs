using System;
using System.Drawing;

namespace Viking.UI
{
    /// <summary>
    /// Decodes the sideways (tilt) mouse wheel messages, which WinForms never turns into a <c>MouseWheel</c> event.
    /// <see cref="SectionViewerControl"/> raises <c>MouseHWheel</c> from them.
    /// </summary>
    public static class HorizontalWheelMessage
    {
        /// <summary>Win32 WM_MOUSEHWHEEL.</summary>
        public const int WM_MOUSEHWHEEL = 0x020E;

        /// <summary>
        /// True for the two messages that carry a sideways wheel step: WM_MOUSEHWHEEL, and WM_POINTERHWHEEL which a window
        /// receives first when mouse-in-pointer is enabled (as Viking does for the pen) and which Windows would otherwise
        /// translate to the former.
        /// </summary>
        public static bool IsHorizontalWheel(int msg) => msg == WM_MOUSEHWHEEL || msg == WinMsgInput.WM_POINTERHWHEEL;

        /// <summary>
        /// Wheel movement in the high word of <paramref name="wParam"/>: positive when the wheel is tilted right, in multiples of 120.
        /// </summary>
        public static int Delta(IntPtr wParam) => (short)((wParam.ToInt64() >> 16) & 0xFFFF);

        /// <summary>The cursor position in screen coordinates held in <paramref name="lParam"/> (signed words, so negative on a left-hand monitor).</summary>
        public static Point ScreenPosition(IntPtr lParam)
        {
            long value = lParam.ToInt64();
            return new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF));
        }
    }
}
