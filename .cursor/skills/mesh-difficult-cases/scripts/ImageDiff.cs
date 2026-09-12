using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MeshDifficultCases
{
    /// <summary>
    /// Pixel comparison for BajajTest capture PNGs, compiled into PowerShell via Add-Type because a per-pixel loop
    /// in script takes seconds per image.
    /// </summary>
    public static class ImageDiff
    {
        public sealed class Result
        {
            public long TotalPixels;
            public long DifferentPixels;
            public bool Resized;
            public double Percent { get { return TotalPixels == 0 ? 0 : 100.0 * DifferentPixels / TotalPixels; } }
        }

        /// <summary>
        /// Compare two PNGs and write a diff image: changed pixels in red over a faded copy of the baseline.
        /// A channel difference of more than <paramref name="tolerance"/> (0-255) counts as changed, so antialiasing
        /// jitter between runs does not register.
        /// </summary>
        public static Result Compare(string baselinePath, string candidatePath, string diffPath, int tolerance)
        {
            using (Bitmap baseline = new Bitmap(baselinePath))
            using (Bitmap candidateRaw = new Bitmap(candidatePath))
            {
                Result result = new Result();
                Bitmap candidate = candidateRaw;
                if (candidateRaw.Width != baseline.Width || candidateRaw.Height != baseline.Height)
                {
                    result.Resized = true;
                    candidate = new Bitmap(baseline.Width, baseline.Height);
                    using (Graphics g = Graphics.FromImage(candidate))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(candidateRaw, 0, 0, baseline.Width, baseline.Height);
                    }
                }

                try
                {
                    byte[] a = Bytes(baseline);
                    byte[] b = Bytes(candidate);
                    byte[] d = new byte[a.Length];
                    result.TotalPixels = a.Length / 4;

                    for (int i = 0; i < a.Length; i += 4)
                    {
                        bool changed = Math.Abs(a[i] - b[i]) > tolerance
                                    || Math.Abs(a[i + 1] - b[i + 1]) > tolerance
                                    || Math.Abs(a[i + 2] - b[i + 2]) > tolerance;
                        if (changed)
                        {
                            result.DifferentPixels++;
                            d[i] = 0; d[i + 1] = 0; d[i + 2] = 255; d[i + 3] = 255;
                        }
                        else
                        {
                            byte gray = (byte)(((a[i] + a[i + 1] + a[i + 2]) / 3) / 3 + 40);
                            d[i] = gray; d[i + 1] = gray; d[i + 2] = gray; d[i + 3] = 255;
                        }
                    }

                    if (!string.IsNullOrEmpty(diffPath))
                    {
                        using (Bitmap diff = new Bitmap(baseline.Width, baseline.Height, PixelFormat.Format32bppArgb))
                        {
                            BitmapData data = diff.LockBits(new Rectangle(0, 0, diff.Width, diff.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                            try { Marshal.Copy(d, 0, data.Scan0, d.Length); }
                            finally { diff.UnlockBits(data); }
                            diff.Save(diffPath, ImageFormat.Png);
                        }
                    }

                    return result;
                }
                finally
                {
                    if (!ReferenceEquals(candidate, candidateRaw))
                        candidate.Dispose();
                }
            }
        }

        /// <summary>
        /// Save a width-limited copy of a capture.  Baselines live in the repository, and a native-resolution
        /// screenshot is several hundred kilobytes each; 1280 px keeps the mesh readable at a fraction of that.
        /// </summary>
        public static void SaveScaled(string sourcePath, string destinationPath, int maxWidth)
        {
            using (Bitmap source = new Bitmap(sourcePath))
            {
                if (source.Width <= maxWidth)
                {
                    source.Save(destinationPath, ImageFormat.Png);
                    return;
                }

                int height = (int)Math.Round(source.Height * (double)maxWidth / source.Width);
                using (Bitmap scaled = new Bitmap(maxWidth, height, PixelFormat.Format32bppArgb))
                using (Graphics g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(source, 0, 0, maxWidth, height);
                    scaled.Save(destinationPath, ImageFormat.Png);
                }
            }
        }

        private static byte[] Bytes(Bitmap bitmap)
        {
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            using (Bitmap argb = bitmap.Clone(rect, PixelFormat.Format32bppArgb))
            {
                BitmapData data = argb.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    byte[] bytes = new byte[Math.Abs(data.Stride) * data.Height];
                    Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                    return bytes;
                }
                finally
                {
                    argb.UnlockBits(data);
                }
            }
        }
    }
}
