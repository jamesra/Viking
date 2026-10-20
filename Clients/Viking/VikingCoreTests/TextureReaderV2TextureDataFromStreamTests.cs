using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins the greyscale tile decode path in <see cref="TextureReaderV2.TextureDataFromStream"/>:
    /// indexed palettes map to luminance bytes, true-color streams take the first locked byte per pixel
    /// (blue channel for <see cref="PixelFormat.Format24bppRgb"/>), and undecodable streams throw
    /// <see cref="ArgumentException"/> from GDI+.
    /// </summary>
    [TestClass]
    public class TextureReaderV2TextureDataFromStreamTests
    {
        private static TextureData Decode(Bitmap bitmap, ImageFormat format = null!)
        {
            format ??= ImageFormat.Png;
            using (bitmap)
            {
                using var stream = new MemoryStream();
                bitmap.Save(stream, format);
                stream.Position = 0;
                return TextureReaderV2.TextureDataFromStream(stream);
            }
        }

        [TestMethod]
        public void EmptyStreamThrowsArgumentException()
        {
            using var stream = new MemoryStream();
            Assert.ThrowsException<ArgumentException>(() => TextureReaderV2.TextureDataFromStream(stream));
        }

        [TestMethod]
        public void NonImageBytesThrowArgumentException()
        {
            using var stream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 });
            Assert.ThrowsException<ArgumentException>(() => TextureReaderV2.TextureDataFromStream(stream));
        }

        [TestMethod]
        public void Indexed8bppGreyscaleMapsPaletteRedChannelToPixelBytes()
        {
            const byte paletteIndex = 7;
            const byte expectedLuminance = 140;

            using var bitmap = new Bitmap(1, 1, PixelFormat.Format8bppIndexed);
            ColorPalette palette = bitmap.Palette;
            palette.Entries[paletteIndex] = Color.FromArgb(expectedLuminance, 0, 0);
            bitmap.Palette = palette;

            Rectangle rect = new(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bits = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            try
            {
                int bytes = bits.Stride * bits.Height;
                for (int i = 0; i < bytes; i++)
                    Marshal.WriteByte(bits.Scan0, i, paletteIndex);
            }
            finally
            {
                bitmap.UnlockBits(bits);
            }

            TextureData data = Decode(bitmap, ImageFormat.Bmp);

            Assert.IsFalse(data.IsEmpty);
            Assert.AreEqual(1, data.width);
            Assert.AreEqual(1, data.height);
            Assert.AreEqual(1, data.pixelBytes.Length);
            Assert.AreEqual(expectedLuminance, data.pixelBytes[0]);
        }

        [TestMethod]
        public void Format24bppRgbUsesFirstLockedBytePerPixelAsGreyscale()
        {
            using var bitmap = new Bitmap(4, 2, PixelFormat.Format24bppRgb);
            bitmap.SetPixel(0, 0, Color.FromArgb(200, 10, 20));
            bitmap.SetPixel(1, 0, Color.FromArgb(55, 200, 200));
            bitmap.SetPixel(2, 0, Color.FromArgb(0, 0, 0));
            bitmap.SetPixel(3, 0, Color.FromArgb(1, 2, 3));
            bitmap.SetPixel(0, 1, Color.FromArgb(128, 64, 32));
            bitmap.SetPixel(1, 1, Color.FromArgb(255, 0, 255));
            bitmap.SetPixel(2, 1, Color.FromArgb(9, 8, 7));
            bitmap.SetPixel(3, 1, Color.FromArgb(250, 251, 252));

            TextureData data = Decode(bitmap);

            Assert.IsFalse(data.IsEmpty);
            Assert.AreEqual(4, data.width);
            Assert.AreEqual(2, data.height);
            CollectionAssert.AreEqual(
                new byte[]
                {
                    Color.FromArgb(200, 10, 20).B,
                    Color.FromArgb(55, 200, 200).B,
                    Color.FromArgb(0, 0, 0).B,
                    Color.FromArgb(1, 2, 3).B,
                    Color.FromArgb(128, 64, 32).B,
                    Color.FromArgb(255, 0, 255).B,
                    Color.FromArgb(9, 8, 7).B,
                    Color.FromArgb(250, 251, 252).B,
                },
                data.pixelBytes);
        }

        /// <summary>
        /// Random small true-color tiles with uniform grey keep one byte per pixel at the chosen luminance.
        /// </summary>
        [TestMethod]
        public void UniformGreyscaleTilesRoundTripDimensionsAndLuminance()
        {
            Prop.ForAll(
                Arb.From(Gen.Choose(1, 24)),
                Arb.From(Gen.Choose(1, 24)),
                Arb.From(Gen.Choose(0, 255)),
                (int width, int height, int luminance) =>
                {
                    using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                    var color = Color.FromArgb(luminance, luminance, luminance);
                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        bitmap.SetPixel(x, y, color);

                    TextureData data = Decode(bitmap);

                    Assert.IsFalse(data.IsEmpty);
                    Assert.AreEqual(width, data.width);
                    Assert.AreEqual(height, data.height);
                    Assert.AreEqual(width * height, data.pixelBytes.Length);
                    Assert.IsTrue(data.pixelBytes.All(b => b == (byte)luminance));
                }).QuickCheckThrowOnFailure();
        }
    }
}
