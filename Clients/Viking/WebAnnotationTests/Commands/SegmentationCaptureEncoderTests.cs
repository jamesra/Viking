using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using System.IO;
using System.Windows.Media.Imaging;
using WebAnnotation.UI.Commands.Segmentation;
using MediaPixelFormats = System.Windows.Media.PixelFormats;

namespace WebAnnotationTests.Commands
{
    [TestClass]
    public class SegmentationCaptureEncoderTests
    {
        [TestMethod]
        public void NarrowLuminanceRangeStretchesToFullEightBit()
        {
            byte[] values = new byte[100];
            for (int i = 0; i < 50; i++)
                values[i] = 80;
            for (int i = 50; i < 100; i++)
                values[i] = 100;

            (byte lo, byte hi) = SegmentationCaptureEncoder.ComputeStretchRange(values);

            Assert.AreEqual(80, lo);
            Assert.AreEqual(100, hi);
            Assert.AreEqual(0, SegmentationCaptureEncoder.Stretch(80, lo, hi));
            Assert.AreEqual(255, SegmentationCaptureEncoder.Stretch(100, lo, hi));
        }

        [TestMethod]
        public void PngHeaderDimensionsMatchTheEncodedImage()
        {
            Color[] pixels = new Color[6 * 3];
            byte[] png = SegmentationCaptureEncoder.EncodeToPng(pixels, 6, 3, grayscale: true);

            Assert.IsTrue(CapturedPng.TryReadDimensions(png, out int width, out int height));
            Assert.AreEqual(6, width);
            Assert.AreEqual(3, height);
        }

        [TestMethod]
        public void PngHeaderReadRejectsTruncatedAndNonPngData()
        {
            Assert.IsFalse(CapturedPng.TryReadDimensions(null!, out _, out _));
            Assert.IsFalse(CapturedPng.TryReadDimensions(new byte[10], out _, out _));
            Assert.IsFalse(CapturedPng.TryReadDimensions(new byte[40], out _, out _));
        }

        [TestMethod]
        public void FlatImageIsNotStretched()
        {
            byte[] values = new byte[16];
            for (int i = 0; i < values.Length; i++)
                values[i] = 40;

            (byte lo, byte hi) = SegmentationCaptureEncoder.ComputeStretchRange(values);
            Assert.AreEqual(40, lo);
            Assert.AreEqual(40, hi);
            Assert.AreEqual(40, SegmentationCaptureEncoder.Stretch(40, lo, hi));
        }

        [TestMethod]
        public void SingleChannelEncodesAsEightBitGrayscalePng()
        {
            Color[] pixels = new Color[4];
            pixels[0] = new Color((byte)80, (byte)10, (byte)10);
            pixels[1] = new Color((byte)80, (byte)10, (byte)10);
            pixels[2] = new Color((byte)100, (byte)200, (byte)200);
            pixels[3] = new Color((byte)100, (byte)200, (byte)200);

            byte[] png = SegmentationCaptureEncoder.EncodeToPng(pixels, 2, 2, grayscale: true);

            DecodedPng image = DecodedPng.Read(png);
            Assert.AreEqual(2, image.Width);
            Assert.AreEqual(2, image.Height);
            Assert.AreEqual(8, png[24], "PNG bit depth");
            Assert.AreEqual(0, png[25], "PNG color type must be greyscale");

            Assert.AreEqual(0, image.Gray(0, 0));
            Assert.AreEqual(255, image.Gray(0, 1));
        }

        [TestMethod]
        public void ColorEncodeStretchesRgbWithLuminanceRange()
        {
            Color[] pixels =
            [
                new Color((byte)80, (byte)80, (byte)80),
                new Color((byte)100, (byte)100, (byte)100)
            ];

            byte[] png = SegmentationCaptureEncoder.EncodeToPng(pixels, 2, 1, grayscale: false);
            DecodedPng image = DecodedPng.Read(png);

            Assert.AreEqual(6, png[25], "PNG color type is RGBA");
            Assert.AreEqual(0, image.Red(0, 0));
            Assert.AreEqual(255, image.Red(1, 0));
            Assert.AreEqual(255, image.Alpha(1, 0));
        }
    }

    /// <summary>A PNG decoded by Windows' own decoder (WIC, through WPF), as greyscale and as straight BGRA.</summary>
    internal sealed class DecodedPng
    {
        private readonly byte[] gray;
        private readonly byte[] bgra;

        private DecodedPng(BitmapSource frame)
        {
            Width = frame.PixelWidth;
            Height = frame.PixelHeight;
            BitsPerPixel = frame.Format.BitsPerPixel;
            gray = Pixels(frame, MediaPixelFormats.Gray8, 1);
            bgra = Pixels(frame, MediaPixelFormats.Bgra32, 4);
        }

        public int Width { get; }
        public int Height { get; }

        /// <summary>Bits per pixel as stored in the file: 1 for a black-and-white mask, 8 for greyscale.</summary>
        public int BitsPerPixel { get; }

        public static DecodedPng Read(byte[] png)
        {
            PngBitmapDecoder decoder = new(new MemoryStream(png), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return new DecodedPng(decoder.Frames[0]);
        }

        public byte Gray(int x, int y) => gray[y * Width + x];
        public byte Red(int x, int y) => bgra[(y * Width + x) * 4 + 2];
        public byte Alpha(int x, int y) => bgra[(y * Width + x) * 4 + 3];

        private static byte[] Pixels(BitmapSource frame, System.Windows.Media.PixelFormat format, int bytesPerPixel)
        {
            FormatConvertedBitmap converted = new(frame, format, null, 0);
            byte[] pixels = new byte[frame.PixelWidth * frame.PixelHeight * bytesPerPixel];
            converted.CopyPixels(pixels, frame.PixelWidth * bytesPerPixel, 0);
            return pixels;
        }
    }
}
