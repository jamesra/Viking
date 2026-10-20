#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Viking.UI.Controls;
using VikingXNAGraphics;
using GeometryCircle = Geometry.Circle;
using GeometryVector2 = Geometry.Vector2;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>The choices offered by the on-screen buttons of a <see cref="SegmentationCommand"/>.</summary>
    internal enum SegmentationPenButton
    {
        /// <summary>Save the segmented polygon (a check mark).</summary>
        Accept,

        /// <summary>Leave segmentation and save the polygon as it was dragged (a double chevron).</summary>
        Skip,
    }

    /// <summary>
    /// Large buttons down the right edge of the viewer, below the shared <see cref="PenCancelButton"/>, so a pen user can
    /// finish a <see cref="SegmentationCommand"/> without the keyboard or a double-click: accept and skip.
    /// </summary>
    /// <remarks>
    /// Layout and hit testing are pure screen-pixel arithmetic (<see cref="Layout"/>, <see cref="HitTest"/>), so they
    /// do not depend on the camera. Drawing converts the pixel positions to world space every frame, because the
    /// buttons stay put on screen while the view pans and zooms. Call <see cref="Draw"/> from the render thread only.
    /// </remarks>
    internal sealed class SegmentationPenButtons(SectionViewerControl parent)
    {
        /// <summary>Radius of each button in screen pixels. Large enough for a pen tip.</summary>
        internal const double RadiusPixels = 30;

        private const double GapPixels = 14;
        private const double MarginPixels = 24;
        private const double TopPixels = PenCancelButton.ReservedTopPixels;
        private const double GlyphLineWidthPixels = 5;

        private readonly SectionViewerControl parent = parent ?? throw new ArgumentNullException(nameof(parent));

        /// <summary>
        /// The buttons shown, top to bottom. Skip is offered only when there is a dragged polygon to fall back to.
        /// </summary>
        internal static IReadOnlyList<SegmentationPenButton> Layout(bool includeSkip) =>
            includeSkip
                ? [SegmentationPenButton.Accept, SegmentationPenButton.Skip]
                : [SegmentationPenButton.Accept];

        /// <summary>Screen position of the centre of the button at <paramref name="index"/> in the layout.</summary>
        internal static (double X, double Y) CenterOf(int index, int clientWidth, Padding insets = default) =>
            (clientWidth - insets.Right - MarginPixels - RadiusPixels,
             insets.Top + TopPixels + RadiusPixels + index * (2 * RadiusPixels + GapPixels));

        /// <summary>
        /// The button under the screen point (<paramref name="x"/>, <paramref name="y"/>), or null when the point is on
        /// none of them.
        /// </summary>
        internal static SegmentationPenButton? HitTest(bool includeSkip, int clientWidth, double x, double y, Padding insets = default)
        {
            IReadOnlyList<SegmentationPenButton> buttons = Layout(includeSkip);
            for (int i = 0; i < buttons.Count; i++)
            {
                (double cx, double cy) = CenterOf(i, clientWidth, insets);
                double dx = x - cx;
                double dy = y - cy;
                if (dx * dx + dy * dy <= RadiusPixels * RadiusPixels)
                    return buttons[i];
            }

            return null;
        }

        /// <summary>Draws the buttons over the scene. Depth testing should already be off.</summary>
        internal void Draw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, bool includeSkip)
        {
            IReadOnlyList<SegmentationPenButton> buttons = Layout(includeSkip);
            double downsample = parent.Downsample;
            int width = parent.ClientSize.Width;
            Padding insets = parent.DockedChromeInsets;

            for (int i = 0; i < buttons.Count; i++)
            {
                (double cx, double cy) = CenterOf(i, width, insets);
                GeometryVector2 centerWorld = parent.ScreenToWorld(cx, cy);

                GeometryCircle circle = new(centerWorld, RadiusPixels * downsample);
                Color color = DiscColor(buttons[i]);
                CircleView.Draw(graphicsDevice, scene, OverlayStyle.Alpha,
                    [new CircleView(circle, color) { Alpha = PenButtonStyle.DiscAlpha }, PenButtonStyle.RingFor(circle, color)]);

                foreach (GeometryVector2[] stroke in Glyph(buttons[i]))
                {
                    PolyLineView line = new(
                        stroke.Select(offset => parent.ScreenToWorld(cx + offset.X, cy + offset.Y)),
                        Color.White,
                        null,
                        GlyphLineWidthPixels * downsample);
                    line.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
                }
            }
        }

        private static Color DiscColor(SegmentationPenButton button) => button switch
        {
            SegmentationPenButton.Accept => new Color(30, 150, 60),
            _ => new Color(200, 140, 20),
        };

        /// <summary>The strokes of a button's symbol as pixel offsets from its centre (x right, y down).</summary>
        private static IEnumerable<GeometryVector2[]> Glyph(SegmentationPenButton button)
        {
            switch (button)
            {
                case SegmentationPenButton.Accept:
                    yield return [new(-13, 0), new(-4, 10), new(13, -11)];
                    break;
                default:
                    yield return [new(-13, -11), new(-3, 0), new(-13, 11)];
                    yield return [new(1, -11), new(11, 0), new(1, 11)];
                    break;
            }
        }
    }
}
