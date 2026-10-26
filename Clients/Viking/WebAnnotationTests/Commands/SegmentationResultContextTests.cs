using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.gRPC.SegmentationServiceTypes.V1;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// A response must keep the tile level it was requested at even after the session moves on to a later request.
    /// </summary>
    [TestClass]
    public class SegmentationResultContextTests
    {
        [TestMethod]
        public void EachResponseKeepsItsOwnRequestedLevel()
        {
            SegmentationResultContexts contexts = new();
            SegmentationResponse atLevelOne = new() { RequestId = 1 };
            SegmentationResponse atLevelFour = new() { RequestId = 1 };

            contexts.Record(atLevelOne, 1);
            contexts.Record(atLevelFour, 4);

            Assert.AreEqual(1, contexts.DownsampleOrDefault(atLevelOne, fallback: 9));
            Assert.AreEqual(4, contexts.DownsampleOrDefault(atLevelFour, fallback: 9));
        }

        [TestMethod]
        public void RecordingTwiceKeepsTheFirstLevel()
        {
            SegmentationResultContexts contexts = new();
            SegmentationResponse response = new();

            contexts.Record(response, 2);
            contexts.Record(response, 8);

            Assert.AreEqual(2, contexts.DownsampleOrDefault(response, fallback: 1));
        }

        [TestMethod]
        public void UnknownResponseUsesTheFallback()
        {
            SegmentationResultContexts contexts = new();

            Assert.AreEqual(3, contexts.DownsampleOrDefault(new SegmentationResponse(), fallback: 3));
            Assert.AreEqual(3, contexts.DownsampleOrDefault(null!, fallback: 3));
        }

        [TestMethod]
        public void LogitBoundsScaleByTheRequestedLevelNotTheSessionLevel()
        {
            // 10 x 20 samples of 4 pixels; sample (0, 0)'s top-left pixel is grid pixel (2, 83), Y up, so the logits cover pixel
            // columns 2..41 and rows 4..83: x in [2, 42), y in [4, 84).
            SegmentResult segment = SegmentationLogitFieldTests.SegmentWithLogits(10, 20, 4, 2, 83, (_, _) => 0f);

            Geometry.Rectangle atLevelOne = SegmentationViewportSession.SegmentWorldBounds(segment, 1)!.Value;
            Geometry.Rectangle atLevelFour = SegmentationViewportSession.SegmentWorldBounds(segment, 4)!.Value;

            Assert.AreEqual(2, atLevelOne.Left);
            Assert.AreEqual(4, atLevelOne.Bottom);
            Assert.AreEqual(42, atLevelOne.Right);
            Assert.AreEqual(84, atLevelOne.Top);
            Assert.AreEqual(8, atLevelFour.Left);
            Assert.AreEqual(16, atLevelFour.Bottom);
            Assert.AreEqual(168, atLevelFour.Right);
            Assert.AreEqual(336, atLevelFour.Top);
        }
    }
}
