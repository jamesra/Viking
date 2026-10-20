using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.gRPC.SegmentationServiceTypes.V1;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// What the client does with the capabilities a server advertises in GetServerStatus.
    /// </summary>
    [TestClass]
    public class SegmentationCapabilitiesTests
    {
        private static ModelCapabilities Advertise(SubmissionMode submission, ResolutionMode resolution) =>
            new() { SubmissionMode = submission, ResolutionMode = resolution };

        [TestMethod]
        public void FixedTilesAreAdopted()
        {
            SegmentationModelProfile? profile = SegmentationViewportSession.ProfileToAdopt(
                Advertise(SubmissionMode.FixedTileGrid, ResolutionMode.Multi),
                maxTileDownsample: 4);

            Assert.IsNotNull(profile);
            Assert.AreEqual(SubmissionMode.FixedTileGrid, profile.Submission);
            Assert.AreEqual(ResolutionMode.Multi, profile.Resolution);
        }

        [TestMethod]
        public void LocalCeilingOfOneNarrowsMultiToSingle()
        {
            SegmentationModelProfile? profile = SegmentationViewportSession.ProfileToAdopt(
                Advertise(SubmissionMode.FixedTileGrid, ResolutionMode.Multi),
                maxTileDownsample: 1);

            Assert.IsNotNull(profile);
            Assert.AreEqual(ResolutionMode.Single, profile.Resolution);
        }

        [TestMethod]
        public void UnspecifiedModesKeepTheCurrentProfile()
        {
            Assert.IsNull(SegmentationViewportSession.ProfileToAdopt(
                Advertise(SubmissionMode.Unspecified, ResolutionMode.Single), 1));
            Assert.IsNull(SegmentationViewportSession.ProfileToAdopt(
                Advertise(SubmissionMode.FixedTileGrid, ResolutionMode.Unspecified), 1));
        }

        [TestMethod]
        public void FullViewportIsNotAdoptedBecauseTheClientCannotSendIt()
        {
            Assert.IsNull(SegmentationViewportSession.ProfileToAdopt(
                Advertise(SubmissionMode.FullViewport, ResolutionMode.Single), 1));
        }

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
        public void MissingCapabilitiesKeepTheCurrentProfile()
        {
            Assert.IsNull(SegmentationViewportSession.ProfileToAdopt(null, 1));
        }

        [TestMethod]
        public async System.Threading.Tasks.Task WaitingForCapabilitiesReturnsAtOnceWhenNoQueryWasStarted()
        {
            SegmentationViewportSession.ResetModelProfile();

            System.Threading.Tasks.Task waiting = SegmentationViewportSession.WhenCapabilitiesKnownAsync(timeoutSeconds: 30);

            Assert.IsTrue(waiting.IsCompleted, "nothing to wait for: the model id is simply not known yet");
            await waiting;
            Assert.IsNull(SegmentationViewportSession.ServerModelId);
        }
    }
}
