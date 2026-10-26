using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Runtime.CompilerServices;

namespace VikingXNAGraphics
{
    /// <summary>
    /// A plain up-arrow (a triangular head over a stem) drawn in code, white on transparent, for drawing beside label text.
    /// Label fonts cover ASCII only, so there is no arrow character to use. Tint it like text; flip it vertically for a
    /// down arrow. One texture per <see cref="GraphicsDevice"/>, made on first use.
    /// </summary>
    public static class ArrowGlyph
    {
        /// <summary>Width and height of the texture in pixels.</summary>
        public const int Size = 64;

        /// <summary>Samples per pixel along each axis when smoothing the edges.</summary>
        private const int Supersample = 4;

        private static readonly ConditionalWeakTable<GraphicsDevice, Texture2D> textureByDevice = new();
        private static readonly object gate = new();

        /// <summary>The arrow texture for <paramref name="device"/>, premultiplied for SpriteBatch's default blending.</summary>
        public static Texture2D Get(GraphicsDevice device)
        {
            lock (gate)
            {
                if (textureByDevice.TryGetValue(device, out Texture2D existing) && !existing.IsDisposed)
                    return existing;

                textureByDevice.Remove(device);
                float[] alpha = AlphaMask(Size);
                Color[] pixels = new Color[alpha.Length];
                for (int i = 0; i < alpha.Length; i++)
                    pixels[i] = Color.White * alpha[i];

                Texture2D texture = new(device, Size, Size, false, SurfaceFormat.Color);
                texture.SetData(pixels);
                textureByDevice.Add(device, texture);
                return texture;
            }
        }

        /// <summary>
        /// Coverage of the up-arrow in each pixel of a <paramref name="size"/> by <paramref name="size"/> image, row by row
        /// from the top, 0 (clear) to 1 (solid), averaged over <see cref="Supersample"/>² samples so the edges are smooth.
        /// </summary>
        public static float[] AlphaMask(int size)
        {
            float[] mask = new float[size * size];
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            double x = (column + ((sx + 0.5) / Supersample)) / size;
                            double y = (row + ((sy + 0.5) / Supersample)) / size;
                            if (IsInArrow(x, y))
                                inside++;
                        }
                    }

                    mask[(row * size) + column] = inside / (float)(Supersample * Supersample);
                }
            }

            return mask;
        }

        /// <summary>
        /// True when (<paramref name="x"/>, <paramref name="y"/>), both 0 to 1 with y down, is inside the arrow: a head from
        /// the apex at the top centre down to a base halfway, then a stem a quarter of the width wide to near the bottom.
        /// </summary>
        private static bool IsInArrow(double x, double y)
        {
            const double apex = 0.06;
            const double headBase = 0.52;
            const double headHalfWidth = 0.40;
            const double stemHalfWidth = 0.13;
            const double stemBottom = 0.94;

            if (y >= apex && y <= headBase)
            {
                double halfWidth = headHalfWidth * (y - apex) / (headBase - apex);
                return Math.Abs(x - 0.5) <= halfWidth;
            }

            return y > headBase && y <= stemBottom && Math.Abs(x - 0.5) <= stemHalfWidth;
        }
    }
}
