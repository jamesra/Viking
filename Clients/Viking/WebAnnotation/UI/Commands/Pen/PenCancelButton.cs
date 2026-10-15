#nullable enable

using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Viking.UI.Controls;
using VikingXNAGraphics;
using GeometryCircle = Geometry.Circle;
using GeometryVector2 = Geometry.Vector2;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// The red X pinned to the top right corner of the viewer that cancels the pen command in progress. It sits in the
    /// same place for every pen command so a pen user always knows where to find it.
    /// </summary>
    /// <remarks>
    /// Layout and hit testing are pure screen-pixel arithmetic (<see cref="CenterOf"/>, <see cref="HitTest"/>), so they
    /// do not depend on the camera. Drawing converts the pixel position to world space every frame, because the button
    /// stays put on screen while the view pans and zooms. Call <see cref="Draw"/> from the render thread only.
    /// </remarks>
    internal sealed class PenCancelButton(SectionViewerControl parent)
    {
        /// <summary>Radius of the button in screen pixels. Large enough for a pen tip.</summary>
        internal const double RadiusPixels = 28;

        private const double MarginPixels = 24;
        private const double GapBelowPixels = 14;
        private const double GlyphLineWidthPixels = 5;
        private const double GlyphHalfSizePixels = 10;

        /// <summary>
        /// Distance in screen pixels from the top of the viewer below which other pen chrome may be stacked without
        /// touching the cancel button.
        /// </summary>
        internal const double ReservedTopPixels = MarginPixels + 2 * RadiusPixels + GapBelowPixels;

        private static readonly Color DiscColor = new(200, 30, 30);

        private readonly SectionViewerControl parent = parent ?? throw new ArgumentNullException(nameof(parent));

        /// <summary>Screen position of the centre of the button for a viewer <paramref name="clientWidth"/> pixels wide.</summary>
        internal static (double X, double Y) CenterOf(int clientWidth) =>
            (clientWidth - MarginPixels - RadiusPixels, MarginPixels + RadiusPixels);

        /// <summary>True when the screen point (<paramref name="x"/>, <paramref name="y"/>) is on the button.</summary>
        internal static bool HitTest(int clientWidth, double x, double y)
        {
            (double cx, double cy) = CenterOf(clientWidth);
            double dx = x - cx;
            double dy = y - cy;
            return dx * dx + dy * dy <= RadiusPixels * RadiusPixels;
        }

        /// <summary>True when the screen point is on the button of this viewer.</summary>
        internal bool HitTest(double x, double y) => HitTest(parent.ClientSize.Width, x, y);

        /// <summary>Draws the button over the scene. Depth testing should already be off.</summary>
        internal void Draw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene)
        {
            double downsample = parent.Downsample;
            (double cx, double cy) = CenterOf(parent.ClientSize.Width);
            GeometryVector2 centerWorld = parent.ScreenToWorld(cx, cy);
            GeometryCircle disc = new(centerWorld, RadiusPixels * downsample);

            CircleView.Draw(graphicsDevice, scene, OverlayStyle.Alpha,
                [new CircleView(disc, DiscColor) { Alpha = 1f }, PenButtonStyle.RingFor(disc, DiscColor)]);

            foreach (GeometryVector2[] stroke in Glyph())
            {
                PolyLineView line = new(
                    stroke.Select(offset => parent.ScreenToWorld(cx + offset.X, cy + offset.Y)),
                    Color.White,
                    null,
                    GlyphLineWidthPixels * downsample);
                line.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            }
        }

        /// <summary>The strokes of the X as pixel offsets from the centre (x right, y down).</summary>
        private static GeometryVector2[][] Glyph() =>
        [
            [new(-GlyphHalfSizePixels, -GlyphHalfSizePixels), new(GlyphHalfSizePixels, GlyphHalfSizePixels)],
            [new(-GlyphHalfSizePixels, GlyphHalfSizePixels), new(GlyphHalfSizePixels, -GlyphHalfSizePixels)],
        ];
    }
}
