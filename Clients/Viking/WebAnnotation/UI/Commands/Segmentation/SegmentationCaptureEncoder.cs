using Microsoft.Xna.Framework;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Diagnostics;
using System.IO;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Encodes viewport captures for SAM2: 1–99 percentile luminance stretch, then
    /// an 8-bit greyscale PNG for single-channel views (RGBA PNG when color).
    /// Debug builds also write the exact upload bytes to %TEMP%\VikingSegmentation.
    /// </summary>
    internal static class SegmentationCaptureEncoder
    {
        /// <summary>Inclusive low percentile used by the luminance stretch.</summary>
        internal const int LowPercentile = 1;

        /// <summary>Inclusive high percentile used by the luminance stretch.</summary>
        internal const int HighPercentile = 99;

        public static string CaptureDirectory { get; } =
            Path.Combine(Path.GetTempPath(), "VikingSegmentation");

        public static string LatestCapturePath { get; } =
            Path.Combine(CaptureDirectory, "segmentation_capture_latest.png");

        /// <summary>
        /// Stretches luminance, then writes L8 greyscale PNG when <paramref name="grayscale"/> is true, otherwise RGB.
        /// Color overlays keep RGB so SAM2 sees the overlay channels.
        /// </summary>
        public static byte[] EncodeToPng(Color[] pixels, int width, int height, bool grayscale)
        {
            if (pixels is null)
                throw new ArgumentNullException(nameof(pixels));
            if (pixels.Length != width * height)
                throw new ArgumentException("Pixel count must equal width * height.");

            byte[] luma = new byte[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                luma[i] = grayscale ? pixels[i].R : Luminance(pixels[i]);

            (byte lo, byte hi) = ComputeStretchRange(luma);

            return grayscale
                ? EncodeGrayscalePng(luma, width, height, lo, hi)
                : EncodeRgbPng(pixels, width, height, lo, hi);
        }

        /// <summary>
        /// Inclusive 1st/99th percentile of an 8-bit histogram. Flat images return the same lo/hi
        /// so <see cref="Stretch"/> leaves values unchanged.
        /// </summary>
        public static (byte lo, byte hi) ComputeStretchRange(byte[] values)
        {
            if (values is null || values.Length == 0)
                return (0, 255);

            int[] histogram = new int[256];
            for (int i = 0; i < values.Length; i++)
                histogram[values[i]]++;

            int lowCount = Math.Max(1, (int)Math.Ceiling(values.Length * (LowPercentile / 100.0)));
            int highCount = Math.Max(lowCount, (int)Math.Ceiling(values.Length * (HighPercentile / 100.0)));

            byte lo = PercentileFromHistogram(histogram, lowCount);
            byte hi = PercentileFromHistogram(histogram, highCount);
            if (hi <= lo)
            {
                lo = FirstOccupied(histogram);
                hi = LastOccupied(histogram);
            }

            return (lo, hi);
        }

        /// <summary>
        /// Maps <paramref name="value"/> from [lo, hi] onto 0–255. Unchanged when the range is empty.
        /// </summary>
        public static byte Stretch(byte value, byte lo, byte hi)
        {
            if (hi <= lo)
                return value;

            int scaled = (value - lo) * 255 / (hi - lo);
            if (scaled < 0)
                return 0;
            if (scaled > 255)
                return 255;
            return (byte)scaled;
        }

        /// <summary>
        /// Debug-only: writes latest.png plus a timestamped copy, then deletes captures older than 24 hours.
        /// Release builds return immediately so %TEMP% is not filled in production.
        /// </summary>
        public static void SaveCaptureForReview(byte[] pngData, int width, int height)
        {
            if (pngData is null || pngData.Length == 0)
                return;

            try
            {
#if !DEBUG
                return;
#else
                Directory.CreateDirectory(CaptureDirectory);
                DeleteCapturesOlderThan(TimeSpan.FromHours(24));
                File.WriteAllBytes(LatestCapturePath, pngData);
                Debug.WriteLine($"[SegmentationProfile] Saved capture for review: {LatestCapturePath}");

                string stamped = Path.Combine(
                    CaptureDirectory,
                    $"segmentation_capture_{DateTime.Now:yyyyMMdd_HHmmss}_{width}x{height}.png");
                File.WriteAllBytes(stamped, pngData);
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save captured image to disk: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes timestamped review PNGs older than <paramref name="age"/>. latest.png is included if it matches the pattern.
        /// </summary>
        private static void DeleteCapturesOlderThan(TimeSpan age)
        {
            if (!Directory.Exists(CaptureDirectory))
                return;

            DateTime cutoff = DateTime.Now - age;
            foreach (string path in Directory.EnumerateFiles(CaptureDirectory, "segmentation_capture_*.png"))
            {
                try
                {
                    if (File.GetLastWriteTime(path) < cutoff)
                        File.Delete(path);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to delete old capture {path}: {ex.Message}");
                }
            }
        }

        private static byte[] EncodeGrayscalePng(byte[] luma, int width, int height, byte lo, byte hi)
        {
            L8[] pixels = new L8[luma.Length];
            for (int i = 0; i < luma.Length; i++)
                pixels[i] = new L8(Stretch(luma[i], lo, hi));

            using MemoryStream stream = new();
            using var image = SixLabors.ImageSharp.Image.LoadPixelData<L8>(pixels, width, height);
            image.Save(stream, new PngEncoder
            {
                ColorType = PngColorType.Grayscale,
                BitDepth = PngBitDepth.Bit8,
                CompressionLevel = PngCompressionLevel.BestSpeed
            });
            return stream.ToArray();
        }

        private static byte[] EncodeRgbPng(Color[] source, int width, int height, byte lo, byte hi)
        {
            Rgba32[] pixels = new Rgba32[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                pixels[i] = new Rgba32(
                    Stretch(source[i].R, lo, hi),
                    Stretch(source[i].G, lo, hi),
                    Stretch(source[i].B, lo, hi),
                    source[i].A);
            }

            using MemoryStream stream = new();
            using var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(pixels, width, height);
            image.Save(stream, new PngEncoder
            {
                CompressionLevel = PngCompressionLevel.BestSpeed
            });
            return stream.ToArray();
        }

        private static byte Luminance(Color color)
            => (byte)((color.R * 77 + color.G * 150 + color.B * 29) >> 8);

        private static byte PercentileFromHistogram(int[] histogram, int targetCount)
        {
            int running = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                running += histogram[i];
                if (running >= targetCount)
                    return (byte)i;
            }

            return 255;
        }

        private static byte FirstOccupied(int[] histogram)
        {
            for (int i = 0; i < histogram.Length; i++)
            {
                if (histogram[i] > 0)
                    return (byte)i;
            }

            return 0;
        }

        private static byte LastOccupied(int[] histogram)
        {
            for (int i = histogram.Length - 1; i >= 0; i--)
            {
                if (histogram[i] > 0)
                    return (byte)i;
            }

            return 255;
        }
    }
}
