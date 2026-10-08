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
        public void MissingCapabilitiesKeepTheCurrentProfile()
        {
            Assert.IsNull(SegmentationViewportSession.ProfileToAdopt(null, 1));
        }
    }
}
