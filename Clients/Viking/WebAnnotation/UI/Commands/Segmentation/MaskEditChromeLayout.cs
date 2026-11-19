#nullable enable

using System;
using System.Windows.Forms;
using WebAnnotation.UI.AutoPolygonize;
using GeometryVector2 = Geometry.Vector2;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>A rectangle in screen pixels, y down: <see cref="Top"/> is above <see cref="Bottom"/>.</summary>
    internal readonly record struct ScreenBox(double Left, double Top, double Right, double Bottom)
    {
        /// <summary>The middle of the box.</summary>
        public GeometryVector2 Center => new((Left + Right) / 2, (Top + Bottom) / 2);

        /// <summary>True when the two boxes share some area (touching edges do not count).</summary>
        public bool Overlaps(ScreenBox other) =>
            Left < other.Right && other.Left < Right && Top < other.Bottom && other.Top < Bottom;

        /// <summary>The box around a circle.</summary>
        public static ScreenBox AroundCircle(GeometryVector2 center, double radius) =>
            new(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);
    }

    /// <summary>
    /// Where the mask edit's on-canvas chrome goes this frame, in screen pixels (y down): the accept and cancel buttons and the
    /// label. Made by <see cref="MaskEditChromeLayout.Place"/>.
    /// </summary>
    /// <param name="Accept">Centre of the green accept button.</param>
    /// <param name="Cancel">Centre of the red cancel button, just below <paramref name="Accept"/>.</param>
    /// <param name="ButtonRadius">Radius of both buttons.</param>
    /// <param name="LabelCenter">Centre of the label.</param>
    /// <param name="ButtonsRight">True when the buttons are right of the mask, false when left of it or, with no room on either side, over it.</param>
    /// <param name="LabelBelow">True when the label is below the mask, false when above it or, with no room either way, over it.</param>
    internal readonly record struct MaskEditChrome(
        GeometryVector2 Accept, GeometryVector2 Cancel, double ButtonRadius, GeometryVector2 LabelCenter, bool ButtonsRight, bool LabelBelow)
    {
        /// <summary>
        /// The button under the screen point (<paramref name="x"/>, <paramref name="y"/>), or null.
        /// <see cref="ProposalButtonKind.Hide"/> stands for cancel: the buttons borrow the proposal buttons' look.
        /// </summary>
        public ProposalButtonKind? HitTest(double x, double y)
        {
            GeometryVector2 p = new(x, y);
            if (GeometryVector2.Distance(p, Accept) <= ButtonRadius)
                return ProposalButtonKind.Accept;
            if (GeometryVector2.Distance(p, Cancel) <= ButtonRadius)
                return ProposalButtonKind.Hide;
            return null;
        }
    }

    /// <summary>
    /// Places the mask edit's accept and cancel buttons and its label beside the mask, at a fixed screen size whatever the zoom.
    /// Pure screen-pixel arithmetic, so it is tested without a viewer; <see cref="SegmentationMaskEditCommand"/> lays the chrome
    /// out again for every frame and every click from the mask's projected bounds.
    /// </summary>
    /// <remarks>
    /// The buttons stand one above the other (accept on top), centred on the mask's middle height, just right of the mask, or
    /// just left of it when the right side has no room. With no room on either side they sit at the view's right edge, over the
    /// mask. They keep clear of the column of corner pen buttons (<see cref="CornerColumnPixels"/>) and of the view's edges. The
    /// label goes just below the mask, centred on it, or just above it when below is off the view, and moves on past the buttons
    /// if it would cover them.
    /// </remarks>
    internal static class MaskEditChromeLayout
    {
        /// <summary>Radius of each button, in screen pixels: a comfortable mouse target that does not hide much image.</summary>
        internal const double ButtonRadiusPixels = 20;

        /// <summary>Gap between the two buttons.</summary>
        internal const double ButtonGapPixels = 8;

        /// <summary>Gap between the mask's bounds and the buttons or the label.</summary>
        internal const double BoundsGapPixels = 12;

        /// <summary>Space kept free along the view's edges.</summary>
        internal const double EdgeMarginPixels = 8;

        /// <summary>Line height of the label, in screen pixels.</summary>
        internal const double LabelFontPixels = 16;

        /// <summary>
        /// Width kept free at the view's right edge for the corner pen buttons (<see cref="PenCancelButton"/> and
        /// <see cref="SegmentationPenButtons"/>, the wider): its 24 pixel margin, its diameter and a little more.
        /// </summary>
        internal static readonly double CornerColumnPixels = 24 + 2 * PenCancelButton.RadiusPixels + EdgeMarginPixels;

        /// <summary>
        /// The chrome for a mask whose bounds are <paramref name="mask"/> in a viewer of <paramref name="clientWidth"/> x
        /// <paramref name="clientHeight"/> pixels, <paramref name="insets"/> of which are covered by docked controls.
        /// <paramref name="labelWidth"/> is the label's measured width in pixels.
        /// </summary>
        public static MaskEditChrome Place(ScreenBox mask, int clientWidth, int clientHeight, Padding insets, double labelWidth)
        {
            double left = insets.Left + EdgeMarginPixels;
            double top = insets.Top + EdgeMarginPixels;
            double right = clientWidth - insets.Right - CornerColumnPixels;
            double bottom = clientHeight - insets.Bottom - EdgeMarginPixels;

            const double r = ButtonRadiusPixels;
            const double offset = r + ButtonGapPixels / 2;

            double rightX = mask.Right + BoundsGapPixels + r;
            double leftX = mask.Left - BoundsGapPixels - r;
            bool buttonsRight = rightX + r <= right;
            double buttonX = buttonsRight ? rightX : leftX - r >= left ? leftX : right - r;
            buttonX = Clamp(buttonX, left + r, right - r);
            double buttonY = Clamp(mask.Center.Y, top + offset + r, bottom - offset - r);
            GeometryVector2 accept = new(buttonX, buttonY - offset);
            GeometryVector2 cancel = new(buttonX, buttonY + offset);
            ScreenBox buttons = new(buttonX - r, accept.Y - r, buttonX + r, cancel.Y + r);

            const double h = LabelFontPixels;
            double labelX = labelWidth >= right - left
                ? (left + right) / 2
                : Clamp(mask.Center.X, left + labelWidth / 2, right - labelWidth / 2);

            double belowY = mask.Bottom + BoundsGapPixels + h / 2;
            double aboveY = mask.Top - BoundsGapPixels - h / 2;
            bool labelBelow = belowY + h / 2 <= bottom;
            double labelY = labelBelow ? belowY : aboveY - h / 2 >= top ? aboveY : bottom - h / 2;

            // A short mask leaves the button pair reaching past its top or bottom, where the label would go.
            ScreenBox label = new(labelX - labelWidth / 2, labelY - h / 2, labelX + labelWidth / 2, labelY + h / 2);
            if (label.Overlaps(buttons))
                labelY = labelBelow ? buttons.Bottom + BoundsGapPixels + h / 2 : buttons.Top - BoundsGapPixels - h / 2;

            labelY = Clamp(labelY, top + h / 2, bottom - h / 2);
            return new MaskEditChrome(accept, cancel, r, new GeometryVector2(labelX, labelY), buttonsRight, labelBelow);
        }

        /// <summary><paramref name="value"/> kept in [<paramref name="low"/>, <paramref name="high"/>]; <paramref name="low"/> wins when they cross.</summary>
        private static double Clamp(double value, double low, double high) => Math.Max(low, Math.Min(high, value));
    }
}
