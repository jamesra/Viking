using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
using WebAnnotation.UI.Commands.Segmentation;

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

            using var image = SixLabors.ImageSharp.Image.Load<L8>(png);
            Assert.AreEqual(2, image.Width);
            Assert.AreEqual(2, image.Height);
            Assert.AreEqual(8, png[24], "PNG bit depth");
            Assert.AreEqual(0, png[25], "PNG color type must be greyscale");

            L8[] decoded = new L8[4];
            image.CopyPixelDataTo(decoded);
            Assert.AreEqual(0, decoded[0].PackedValue);
            Assert.AreEqual(255, decoded[2].PackedValue);
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
            using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(new MemoryStream(png));
            Rgba32[] decoded = new Rgba32[2];
            image.CopyPixelDataTo(decoded);

            Assert.AreEqual(0, decoded[0].R);
            Assert.AreEqual(255, decoded[1].R);
        }
    }
}
