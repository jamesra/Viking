using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    [TestClass]
    public class CapturedPngTests
    {
        private static byte[] EncodedPng(int width, int height) =>
            SegmentationCaptureEncoder.EncodeToPng(new Color[width * height], width, height, grayscale: true);

        [TestMethod]
        public void MatchingDimensionsAreValid()
        {
            (bool isValid, string message) = CapturedPng.Validate(EncodedPng(8, 5), 8, 5);

            Assert.IsTrue(isValid, message);
        }

        [TestMethod]
        public void DimensionMismatchIsReported()
        {
            (bool isValid, string message) = CapturedPng.Validate(EncodedPng(8, 5), 8, 6);

            Assert.IsFalse(isValid);
            StringAssert.Contains(message, "dimension mismatch");
        }

        [TestMethod]
        public void NullEmptyAndNonPngDataAreRejected()
        {
            Assert.IsFalse(CapturedPng.Validate(null!, 8, 5).isValid);
            Assert.IsFalse(CapturedPng.Validate([], 8, 5).isValid);
            Assert.IsFalse(CapturedPng.Validate(new byte[64], 8, 5).isValid);
        }

        [TestMethod]
        public void InvalidExpectedSizeIsRejected()
        {
            Assert.IsFalse(CapturedPng.Validate(EncodedPng(8, 5), 0, 5).isValid);
            Assert.IsFalse(CapturedPng.Validate(EncodedPng(8, 5), 8, -1).isValid);
        }

        [TestMethod]
        public void DescribeDimensionsReadsTheHeaderOrSaysInvalid()
        {
            Assert.AreEqual("8x5", CapturedPng.DescribeDimensions(EncodedPng(8, 5)));
            Assert.AreEqual("invalid", CapturedPng.DescribeDimensions(new byte[40]));
            Assert.AreEqual("invalid", CapturedPng.DescribeDimensions(null!));
        }
    }
}
