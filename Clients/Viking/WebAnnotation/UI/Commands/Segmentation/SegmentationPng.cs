using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Lossless PNGs for the segmentation server, written by Windows' own encoder (WIC, through WPF), so the client
    /// carries no image library. The server keys its caches by pixels, not bytes, so the encoder's compression choices
    /// do not matter. Safe on any thread: each call builds and encodes its own bitmap.
    /// </summary>
    internal static class SegmentationPng
    {
        /// <summary>An 8-bit greyscale PNG, one byte per pixel, rows top to bottom.</summary>
        public static byte[] EncodeGray8(byte[] pixels, int width, int height)
        {
            CheckLength(pixels, width * height);
            return Encode(PixelFormats.Gray8, pixels, width, height, width);
        }

        /// <summary>An 8-bit RGBA PNG from straight (not premultiplied) BGRA bytes, four per pixel.</summary>
        public static byte[] EncodeBgra32(byte[] bgra, int width, int height)
        {
            CheckLength(bgra, width * height * 4);
            return Encode(PixelFormats.Bgra32, bgra, width, height, width * 4);
        }

        /// <summary>A 1-bit greyscale PNG: a pixel is set (white) when its byte is non-zero.</summary>
        public static byte[] EncodeBlackWhite(byte[] pixels, int width, int height)
        {
            CheckLength(pixels, width * height);
            int stride = (width + 7) / 8;
            byte[] packed = new byte[stride * height];
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                int packedRow = y * stride;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x] != 0)
                        packed[packedRow + (x >> 3)] |= (byte)(0x80 >> (x & 7));
                }
            }

            return Encode(PixelFormats.BlackWhite, packed, width, height, stride);
        }

        private static byte[] Encode(PixelFormat format, byte[] pixels, int width, int height, int stride)
        {
            BitmapSource source = BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
            PngBitmapEncoder encoder = new() { Interlace = PngInterlaceOption.Off };
            encoder.Frames.Add(BitmapFrame.Create(source));
            using MemoryStream stream = new();
            encoder.Save(stream);
            return stream.ToArray();
        }

        private static void CheckLength(byte[] pixels, int expected)
        {
            if (pixels is null)
                throw new ArgumentNullException(nameof(pixels));
            if (pixels.Length != expected)
                throw new ArgumentException($"Expected {expected} bytes, got {pixels.Length}.", nameof(pixels));
        }
    }
}
