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
            SegmentationResponse atLevelOne = new() { Width = 100, Height = 100 };
            SegmentationResponse atLevelFour = new() { Width = 100, Height = 100 };

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
        public void MosaicBoundsScaleByTheRequestedLevelNotTheSessionLevel()
        {
            SegmentationResponse response = new() { OriginX = 2, OriginY = 3, Width = 10, Height = 20 };

            Geometry.Rectangle atLevelOne = SegmentationViewportSession.MosaicWorldBounds(response, 1);
            Geometry.Rectangle atLevelFour = SegmentationViewportSession.MosaicWorldBounds(response, 4);

            Assert.AreEqual(2, atLevelOne.LowerLeft.X);
            Assert.AreEqual(3, atLevelOne.LowerLeft.Y);
            Assert.AreEqual(12, atLevelOne.UpperRight.X);
            Assert.AreEqual(23, atLevelOne.UpperRight.Y);
            Assert.AreEqual(8, atLevelFour.LowerLeft.X);
            Assert.AreEqual(12, atLevelFour.LowerLeft.Y);
            Assert.AreEqual(48, atLevelFour.UpperRight.X);
            Assert.AreEqual(92, atLevelFour.UpperRight.Y);
        }
    }
}
