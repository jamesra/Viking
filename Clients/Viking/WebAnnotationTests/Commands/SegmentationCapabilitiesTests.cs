using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// When the client submits, and what it waits for from GetServerStatus.
    /// </summary>
    [TestClass]
    public class SegmentationCapabilitiesTests
    {
        [TestMethod]
        public void ManualOperationsIgnoreTheAutoDownsampleLimit()
        {
            // Dilate/erode and manual segmentation run at any zoom.
            Assert.IsTrue(SegmentationViewportSession.CameraAllowsSubmission(8, 2, enforceLimit: false));
        }

        [TestMethod]
        public void AutoOperationsStopAtTheDownsampleLimit()
        {
            Assert.IsTrue(SegmentationViewportSession.CameraAllowsSubmission(2, 2, enforceLimit: true), "equality still submits");
            Assert.IsFalse(SegmentationViewportSession.CameraAllowsSubmission(2.25, 2, enforceLimit: true));
        }

        [TestMethod]
        public async System.Threading.Tasks.Task WaitingForServerStatusReturnsAtOnceWhenNoQueryWasStarted()
        {
            SegmentationViewportSession.ResetServerModel();

            System.Threading.Tasks.Task waiting = SegmentationViewportSession.WhenServerStatusKnownAsync(timeoutSeconds: 30);

            Assert.IsTrue(waiting.IsCompleted, "nothing to wait for: the model id is simply not known yet");
            await waiting;
            Assert.IsNull(SegmentationViewportSession.ServerModelId);
        }
    }
}
