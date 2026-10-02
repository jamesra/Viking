using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    [TestClass]
    public class SegmentationUserFeedbackTests
    {
        [TestMethod]
        public void FormatZoomTooCoarseMentionsDownsample()
        {
            string? text = SegmentationUserFeedback.Format(SegmentationSkipKind.ZoomTooCoarse, null);
            Assert.IsNotNull(text);
            StringAssert.Contains(text, "zoom in");
            StringAssert.Contains(text, "downsample");
        }

        [TestMethod]
        public void FormatEmptyMaskAsksForAnotherPoint()
        {
            string? text = SegmentationUserFeedback.Format(SegmentationSkipKind.EmptyMask, null);
            Assert.IsNotNull(text);
            StringAssert.Contains(text, "empty mask");
        }

        [TestMethod]
        public void FormatCancelledAndNoneAreSilent()
        {
            Assert.IsNull(SegmentationUserFeedback.Format(SegmentationSkipKind.None, null));
            Assert.IsNull(SegmentationUserFeedback.Format(SegmentationSkipKind.Cancelled, null));
        }

        [TestMethod]
        public void FormatErrorIncludesDetail()
        {
            string? text = SegmentationUserFeedback.Format(SegmentationSkipKind.Error, "rpc failed");
            Assert.IsNotNull(text);
            StringAssert.Contains(text, "rpc failed");
        }
    }
}
