#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using WebAnnotation.UI.Commands;
using GeometryVector2 = Geometry.Vector2;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>
    /// The pictures of every look of the proposal buttons, rasterised once into one texture atlas: a row per button kind and
    /// hover state, a column per <see cref="ProposalButtonPhase"/>. Each frame then draws one textured quad per button, scaled
    /// to the layout's radius, instead of building circles, polylines and labels.
    /// </summary>
    /// <remarks>
    /// The shapes are drawn from signed distance functions with one pixel of anti-aliasing, so the edges are smooth at every
    /// mip level. Colours are not premultiplied, to match the viewer's SourceAlpha / InverseSourceAlpha blending; mip levels
    /// are averaged premultiplied so transparent texels do not darken the edges.
    /// </remarks>
    internal static class ProposalButtonAtlas
    {
        /// <summary>Side of one cell, in texels.</summary>
        internal const int CellPixels = 128;

        /// <summary>Button radius inside a cell, in texels; the rest of the cell is a transparent margin for the mip levels.</summary>
        internal const double ButtonRadiusPixels = 56;

        internal const int Columns = 4;
        internal const int Rows = 4;
        internal const int Width = Columns * CellPixels;
        internal const int Height = Rows * CellPixels;

        /// <summary>How much darker a pressed button is drawn.</summary>
        private const float PressedShade = 0.65f;

        /// <summary>How far a hovered button's fill moves towards white.</summary>
        private const float HoverTint = 0.25f;

        /// <summary>A pressed button shrinks to this fraction of its radius, so it looks pushed in.</summary>
        internal const double PressedScale = 0.92;

        /// <summary>Half the side of the hide square, as a fraction of the button radius, so its area is close to the circle's.</summary>
        private const double SquareHalfSide = 0.88;

        /// <summary>Corner radius of the hide square, as a fraction of its half side.</summary>
        private const double SquareCornerFraction = 0.35;

        /// <summary>Width of the outline, inside the shape's edge, as a fraction of the button radius.</summary>
        private const double OutlineFraction = 0.1;

        /// <summary>Half the width of the check, X and "?" strokes, as a fraction of the button radius.</summary>
        private const double StrokeHalfWidth = 0.08;

        internal static readonly Color AcceptColor = new(20, 170, 60);
        internal static readonly Color HideColor = new(225, 35, 35);

        /// <summary>Half the side of the quad that shows a button of world <paramref name="radius"/>: the cell plus its margin.</summary>
        internal static double QuadHalfSize(double radius) => radius * (CellPixels / 2.0) / ButtonRadiusPixels;

        /// <summary>The cell of a look: column = phase, row = kind and hover.</summary>
        internal static (int Column, int Row) CellOf(ProposalButtonKind kind, ProposalButtonPhase phase, bool hovered) =>
            ((int)phase, (int)kind * 2 + (hovered ? 1 : 0));

        /// <summary>The texture coordinates of a look's cell: top left and bottom right.</summary>
        internal static (Vector2 TopLeft, Vector2 BottomRight) UvOf(ProposalButtonKind kind, ProposalButtonPhase phase, bool hovered)
        {
            (int column, int row) = CellOf(kind, phase, hovered);
            return (new Vector2((float)column / Columns, (float)row / Rows),
                    new Vector2((float)(column + 1) / Columns, (float)(row + 1) / Rows));
        }

        /// <summary>The fill of <paramref name="kind"/> in <paramref name="phase"/>: darker when pressed, lighter when hovered.</summary>
        internal static Color FillColor(ProposalButtonKind kind, ProposalButtonPhase phase, bool hovered)
        {
            Color color = kind == ProposalButtonKind.Accept ? AcceptColor : HideColor;
            if (phase == ProposalButtonPhase.Pressed)
                return new Color((int)(color.R * PressedShade), (int)(color.G * PressedShade), (int)(color.B * PressedShade));
            if (hovered)
                return Color.Lerp(color, Color.White, HoverTint);
            return color;
        }

        /// <summary>
        /// The strokes of a button's symbol as offsets from its centre in button radii, world y up: the check follows the
        /// segmentation pen buttons' check, the X spans most of the square.
        /// </summary>
        internal static GeometryVector2[][] Symbol(ProposalButtonKind kind) => kind == ProposalButtonKind.Accept
            ? [[new(-0.43, 0), new(-0.13, -0.33), new(0.43, 0.37)]]
            : [[new(-0.42, -0.42), new(0.42, 0.42)], [new(-0.42, 0.42), new(0.42, -0.42)]];

        /// <summary>The "?" as strokes in button radii, world y up: a hook, a short stem, and a dot (<see cref="QuestionDot"/>).</summary>
        internal static GeometryVector2[][] QuestionMark { get; } = BuildQuestionMark();

        /// <summary>Centre and radius of the dot of the "?", in button radii.</summary>
        internal static readonly (GeometryVector2 Center, double Radius) QuestionDot = (new GeometryVector2(0, -0.42), 0.1);

        /// <summary>
        /// Every mip level of the atlas, largest first, as non-premultiplied texels in rows from the top. Level 0 is
        /// <see cref="Width"/> x <see cref="Height"/>; each next level halves both, down to 1 x 1.
        /// </summary>
        internal static List<Color[]> BuildMipLevels()
        {
            Vector4[] premultiplied = RasterizeLevelZero();
            List<Color[]> levels = [ToColors(premultiplied)];

            int width = Width, height = Height;
            while (width > 1 || height > 1)
            {
                int nextWidth = Math.Max(1, width / 2), nextHeight = Math.Max(1, height / 2);
                Vector4[] next = new Vector4[nextWidth * nextHeight];
                for (int y = 0; y < nextHeight; y++)
                {
                    for (int x = 0; x < nextWidth; x++)
                    {
                        int x0 = Math.Min(2 * x, width - 1), x1 = Math.Min(2 * x + 1, width - 1);
                        int y0 = Math.Min(2 * y, height - 1), y1 = Math.Min(2 * y + 1, height - 1);
                        next[y * nextWidth + x] = (premultiplied[y0 * width + x0] + premultiplied[y0 * width + x1] +
                                                   premultiplied[y1 * width + x0] + premultiplied[y1 * width + x1]) / 4f;
                    }
                }

                premultiplied = next;
                width = nextWidth;
                height = nextHeight;
                levels.Add(ToColors(premultiplied));
            }

            return levels;
        }

        /// <summary>Level 0, premultiplied (rgb already times alpha), in 0..1.</summary>
        private static Vector4[] RasterizeLevelZero()
        {
            Vector4[] texels = new Vector4[Width * Height];
            foreach (ProposalButtonKind kind in new[] { ProposalButtonKind.Accept, ProposalButtonKind.Hide })
            {
                foreach (ProposalButtonPhase phase in new[] { ProposalButtonPhase.Idle, ProposalButtonPhase.Pressed, ProposalButtonPhase.Waiting, ProposalButtonPhase.Armed })
                {
                    foreach (bool hovered in new[] { false, true })
                    {
                        (int column, int row) = CellOf(kind, phase, hovered);
                        RasterizeCell(texels, column * CellPixels, row * CellPixels, kind, phase, hovered);
                    }
                }
            }

            return texels;
        }

        private static void RasterizeCell(Vector4[] texels, int left, int top, ProposalButtonKind kind, ProposalButtonPhase phase, bool hovered)
        {
            double scale = phase == ProposalButtonPhase.Pressed ? PressedScale : 1;
            Color fill = FillColor(kind, phase, hovered);
            Color outline = hovered ? Color.White : Color.Black;
            Color glyph = phase == ProposalButtonPhase.Armed
                ? PenButtonStyle.LabelColorFor(fill)
                : kind == ProposalButtonKind.Accept ? Color.White : Color.Black;
            GeometryVector2[][]? strokes = phase switch
            {
                ProposalButtonPhase.Idle => Symbol(kind),
                ProposalButtonPhase.Armed => QuestionMark,
                _ => null,
            };

            double texel = 1 / ButtonRadiusPixels;
            double half = CellPixels / 2.0;
            for (int y = 0; y < CellPixels; y++)
            {
                for (int x = 0; x < CellPixels; x++)
                {
                    // Button radii, origin at the cell centre, y up.
                    GeometryVector2 p = new((x + 0.5 - half) * texel, (half - (y + 0.5)) * texel);

                    double shape = kind == ProposalButtonKind.Accept
                        ? Math.Sqrt(p.X * p.X + p.Y * p.Y) - scale
                        : RoundedSquareDistance(p, SquareHalfSide * scale, SquareHalfSide * scale * SquareCornerFraction);

                    float alpha = Coverage(shape, texel);
                    if (alpha <= 0)
                        continue;

                    // Outline inside the edge, fill inside that, then the symbol on top.
                    Vector4 color = Vector4.Lerp(outline.ToVector4(), fill.ToVector4(), Coverage(shape + OutlineFraction * scale, texel));
                    if (strokes is not null)
                    {
                        double symbol = StrokesDistance(p, strokes, scale);
                        if (phase == ProposalButtonPhase.Armed)
                            symbol = Math.Min(symbol, GeometryVector2.Distance(p, QuestionDot.Center * scale) - QuestionDot.Radius * scale);
                        color = Vector4.Lerp(color, glyph.ToVector4(), Coverage(symbol, texel));
                    }

                    texels[(top + y) * Width + left + x] = new Vector4(color.X * alpha, color.Y * alpha, color.Z * alpha, alpha);
                }
            }
        }

        /// <summary>How much of a texel a shape covers, from its signed distance (negative inside), with one texel of blur.</summary>
        private static float Coverage(double distance, double texel) => (float)Math.Max(0, Math.Min(1, 0.5 - distance / texel));

        private static double RoundedSquareDistance(GeometryVector2 p, double halfSide, double cornerRadius)
        {
            double inner = halfSide - cornerRadius;
            double qx = Math.Abs(p.X) - inner, qy = Math.Abs(p.Y) - inner;
            double outside = Math.Sqrt(Math.Max(qx, 0) * Math.Max(qx, 0) + Math.Max(qy, 0) * Math.Max(qy, 0));
            return outside + Math.Min(Math.Max(qx, qy), 0) - cornerRadius;
        }

        /// <summary>Signed distance to strokes of <see cref="StrokeHalfWidth"/> with round ends, all scaled by <paramref name="scale"/>.</summary>
        private static double StrokesDistance(GeometryVector2 p, GeometryVector2[][] strokes, double scale)
        {
            double best = double.MaxValue;
            foreach (GeometryVector2[] stroke in strokes)
            {
                for (int i = 1; i < stroke.Length; i++)
                    best = Math.Min(best, SegmentDistance(p, stroke[i - 1] * scale, stroke[i] * scale));
            }

            return best - StrokeHalfWidth * scale;
        }

        private static double SegmentDistance(GeometryVector2 p, GeometryVector2 a, GeometryVector2 b)
        {
            GeometryVector2 ab = b - a;
            double lengthSquared = ab.X * ab.X + ab.Y * ab.Y;
            double t = lengthSquared <= 0 ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / lengthSquared));
            return GeometryVector2.Distance(p, new GeometryVector2(a.X + ab.X * t, a.Y + ab.Y * t));
        }

        private static GeometryVector2[][] BuildQuestionMark()
        {
            // The hook: an arc round (0, 0.2) from upper left, over the top, down to its bottom, then the stem.
            const int segments = 24;
            const double radius = 0.26;
            GeometryVector2 center = new(0, 0.2);
            double start = Math.PI * 160 / 180, end = -Math.PI / 2;
            List<GeometryVector2> hook = new(segments + 2);
            for (int i = 0; i <= segments; i++)
            {
                double angle = start + (end - start) * i / segments;
                hook.Add(new GeometryVector2(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle)));
            }

            hook.Add(new GeometryVector2(0, -0.2));
            return [hook.ToArray()];
        }

        private static Color[] ToColors(Vector4[] premultiplied)
        {
            Color[] colors = new Color[premultiplied.Length];
            for (int i = 0; i < premultiplied.Length; i++)
            {
                Vector4 v = premultiplied[i];
                colors[i] = v.W <= 0 ? Color.Transparent : new Color(v.X / v.W, v.Y / v.W, v.Z / v.W, v.W);
            }

            return colors;
        }
    }
}
