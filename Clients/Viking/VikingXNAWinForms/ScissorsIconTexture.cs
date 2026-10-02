using Microsoft.Xna.Framework.Graphics;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace VikingXNAWinForms
{
    /// <summary>
    /// Draws the scissors button icon used by the pen "Cut hole" choice.
    /// The other circle icons ship as content-pipeline PNGs; this one is rendered with GDI+ so it
    /// needs no new content files in the per-project content lists.
    /// Matches the existing icons: white ring, black disc, white glyph, transparent outside the ring.
    /// </summary>
    internal static class ScissorsIconTexture
    {
        private const int Size = 256;

        /// <summary>
        /// Builds the texture on <paramref name="device"/>. The caller owns the result and must rebuild it after a device reset.
        /// </summary>
        public static Texture2D Create(GraphicsDevice device)
        {
            using Bitmap bitmap = Render();
            Microsoft.Xna.Framework.Color[] pixels = ReadPixels(bitmap);
            Texture2D texture = new(device, Size, Size);
            texture.SetData(pixels);
            return texture;
        }

        private static Bitmap Render()
        {
            Bitmap bitmap = new(Size, Size, PixelFormat.Format32bppArgb);
            using Graphics g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);

            float s = Size;
            g.FillEllipse(Brushes.White, 1, 1, s - 2, s - 2);
            float inset = s * 0.07f;
            g.FillEllipse(Brushes.Black, inset, inset, s - (2 * inset), s - (2 * inset));

            using Pen blade = new(System.Drawing.Color.White, s * 0.055f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            using Pen handle = new(System.Drawing.Color.White, s * 0.05f);

            // Blades cross at the pivot and open toward the right; the handles are the two rings on the left.
            PointF pivot = new(s * 0.46f, s * 0.5f);
            PointF handleTop = new(s * 0.27f, s * 0.38f);
            PointF handleBottom = new(s * 0.27f, s * 0.62f);
            PointF tipTop = new(s * 0.80f, s * 0.30f);
            PointF tipBottom = new(s * 0.80f, s * 0.70f);
            g.DrawLine(blade, handleBottom, tipTop);
            g.DrawLine(blade, handleTop, tipBottom);

            float ring = s * 0.075f;
            g.DrawEllipse(handle, handleTop.X - (ring * 1.6f), handleTop.Y - ring, ring * 2f, ring * 2f);
            g.DrawEllipse(handle, handleBottom.X - (ring * 1.6f), handleBottom.Y - ring, ring * 2f, ring * 2f);

            g.FillEllipse(Brushes.Black, pivot.X - (s * 0.018f), pivot.Y - (s * 0.018f), s * 0.036f, s * 0.036f);
            return bitmap;
        }

        private static Microsoft.Xna.Framework.Color[] ReadPixels(Bitmap bitmap)
        {
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, Size, Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] bytes = new byte[Math.Abs(data.Stride) * Size];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                Microsoft.Xna.Framework.Color[] pixels = new Microsoft.Xna.Framework.Color[Size * Size];
                for (int y = 0; y < Size; y++)
                {
                    int row = y * data.Stride;
                    for (int x = 0; x < Size; x++)
                    {
                        int i = row + (x * 4);
                        pixels[(y * Size) + x] = new Microsoft.Xna.Framework.Color(bytes[i + 2], bytes[i + 1], bytes[i], bytes[i + 3]);
                    }
                }

                return pixels;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }
}
