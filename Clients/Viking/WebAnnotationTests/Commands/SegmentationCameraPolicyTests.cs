using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using WebAnnotation.UI.Commands.Segmentation;
using ModelCapabilities = Viking.gRPC.SegmentationServiceTypes.V1.ModelCapabilities;
using ResolutionMode = Viking.gRPC.SegmentationServiceTypes.V1.ResolutionMode;
using SubmissionMode = Viking.gRPC.SegmentationServiceTypes.V1.SubmissionMode;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Camera-move rules per model profile. A fixed-tile result depends on the submitted level and the
    /// prompts, so a pan or zoom inside one level must never cancel, drop, or resubmit it. A full-viewport
    /// result is the screen itself, so any view move makes it stale.
    /// </summary>
    [TestClass]
    public class SegmentationCameraPolicyTests
    {
        private static readonly SegmentationModelProfile SingleTiles =
            new(SubmissionMode.FixedTileGrid, ResolutionMode.Single);

        private static readonly SegmentationModelProfile MultiTiles =
            new(SubmissionMode.FixedTileGrid, ResolutionMode.Multi);

        private static readonly SegmentationModelProfile FullViewport =
            new(SubmissionMode.FullViewport, ResolutionMode.Single);

        [TestMethod]
        public void TheDefaultProfileIsFixedTilesAtASingleResolution()
        {
            Assert.AreEqual(SubmissionMode.FixedTileGrid, SegmentationModelProfile.Default.Submission);
            Assert.AreEqual(ResolutionMode.Single, SegmentationModelProfile.Default.Resolution);
            Assert.IsFalse(SegmentationModelProfile.Default.CameraMoveCanStaleResults);
        }

        [TestMethod]
        public void TheSessionStartsOnTheDefaultProfile()
        {
            Assert.AreSame(SegmentationModelProfile.Default, SegmentationViewportSession.ModelProfile);
        }

        [TestMethod]
        public void AnUnspecifiedFlagIsRejectedRatherThanGuessed()
        {
            Assert.ThrowsException<ArgumentException>(() => SegmentationModelProfile.FromAdvertised(
                new ModelCapabilities { ResolutionMode = ResolutionMode.Single }, 1));
            Assert.ThrowsException<ArgumentException>(() => SegmentationModelProfile.FromAdvertised(
                new ModelCapabilities { SubmissionMode = SubmissionMode.FixedTileGrid }, 1));
            Assert.ThrowsException<ArgumentNullException>(() => SegmentationModelProfile.FromAdvertised(null!, 1));
        }

        [TestMethod]
        public void AdvertisedSubmissionIsKeptAsSent()
        {
            Assert.AreEqual(
                SubmissionMode.FixedTileGrid,
                SegmentationModelProfile.FromAdvertised(Advertise(SubmissionMode.FixedTileGrid, ResolutionMode.Multi), 4).Submission);
            Assert.AreEqual(
                SubmissionMode.FullViewport,
                SegmentationModelProfile.FromAdvertised(Advertise(SubmissionMode.FullViewport, ResolutionMode.Single), 4).Submission);
        }

        private static ModelCapabilities Advertise(SubmissionMode submission, ResolutionMode resolution)
            => new() { SubmissionMode = submission, ResolutionMode = resolution };

        [TestMethod]
        public void AServerThatForbidsMultiResolutionOverridesAHigherLocalCeiling()
        {
            ModelCapabilities advertised = Advertise(SubmissionMode.FixedTileGrid, ResolutionMode.Single);

            Assert.AreEqual(ResolutionMode.Single, SegmentationModelProfile.FromAdvertised(advertised, 4).Resolution);
        }

        [TestMethod]
        public void ALocalCeilingOfOneNarrowsAServerThatAllowsMultiResolution()
        {
            ModelCapabilities advertised = Advertise(SubmissionMode.FixedTileGrid, ResolutionMode.Multi);

            Assert.AreEqual(ResolutionMode.Single, SegmentationModelProfile.FromAdvertised(advertised, 1).Resolution);
            Assert.AreEqual(ResolutionMode.Multi, SegmentationModelProfile.FromAdvertised(advertised, 2).Resolution);
        }

        [TestMethod]
        public void AServerThatWantsTheWholeScreenIsFollowed()
        {
            ModelCapabilities advertised = Advertise(SubmissionMode.FullViewport, ResolutionMode.Single);

            SegmentationModelProfile profile = SegmentationModelProfile.FromAdvertised(advertised, 1);

            Assert.IsFalse(profile.UsesFixedTiles);
            Assert.IsTrue(profile.ResultDependsOnViewport);
            Assert.IsFalse(profile.TileLevelCanChange);
        }

        [TestMethod]
        public void OnlySingleResolutionTilesAreImmuneToCameraMoves()
        {
            Assert.IsFalse(SingleTiles.CameraMoveCanStaleResults);
            Assert.IsTrue(MultiTiles.CameraMoveCanStaleResults);
            Assert.IsTrue(FullViewport.CameraMoveCanStaleResults);
        }

        [TestMethod]
        public void SingleResolutionTilesKeepInFlightWorkAndFinishedResultsOnAnyMove()
        {
            SegmentationCameraPolicy.CameraMoveResponse pan = SegmentationCameraPolicy.OnCameraMoved(
                SingleTiles, viewportMoved: true, tileLevelChanged: false);
            Assert.IsFalse(pan.CancelInFlightSegmentation);
            Assert.IsFalse(pan.DropFinishedResults);
            Assert.IsTrue(pan.ReorderQueue);

            SegmentationCameraPolicy.CameraMoveResponse stale = SegmentationCameraPolicy.OnCameraMoved(
                SingleTiles, viewportMoved: true, tileLevelChanged: true);
            Assert.IsFalse(stale.CancelInFlightSegmentation);
            Assert.IsFalse(stale.DropFinishedResults);
        }

        [TestMethod]
        public void SingleResolutionTilesNeverReportATileLevelChange()
        {
            Assert.IsFalse(SegmentationCameraPolicy.TileLevelChanged(SingleTiles, 1, 2));
        }

        [TestMethod]
        public void MultiResolutionPanInsideOneLevelKeepsWork()
        {
            Assert.IsFalse(SegmentationCameraPolicy.TileLevelChanged(MultiTiles, 2, 2));

            SegmentationCameraPolicy.CameraMoveResponse response = SegmentationCameraPolicy.OnCameraMoved(
                MultiTiles, viewportMoved: true, tileLevelChanged: false);
            Assert.IsFalse(response.CancelInFlightSegmentation);
            Assert.IsFalse(response.DropFinishedResults);
        }

        [TestMethod]
        public void MultiResolutionLevelChangeCancelsAndDrops()
        {
            Assert.IsTrue(SegmentationCameraPolicy.TileLevelChanged(MultiTiles, 1, 2));
            Assert.IsFalse(SegmentationCameraPolicy.TileLevelChanged(MultiTiles, 0, 2));

            SegmentationCameraPolicy.CameraMoveResponse response = SegmentationCameraPolicy.OnCameraMoved(
                MultiTiles, viewportMoved: true, tileLevelChanged: true);
            Assert.IsTrue(response.CancelInFlightSegmentation);
            Assert.IsTrue(response.DropFinishedResults);
        }

        [TestMethod]
        public void AFullViewportModelIsStaleOnAnyViewMoveEvenAtTheSameLevel()
        {
            SegmentationCameraPolicy.CameraMoveResponse moved = SegmentationCameraPolicy.OnCameraMoved(
                FullViewport, viewportMoved: true, tileLevelChanged: false);
            Assert.IsTrue(moved.CancelInFlightSegmentation);
            Assert.IsTrue(moved.DropFinishedResults);

            SegmentationCameraPolicy.CameraMoveResponse still = SegmentationCameraPolicy.OnCameraMoved(
                FullViewport, viewportMoved: false, tileLevelChanged: false);
            Assert.IsFalse(still.CancelInFlightSegmentation);
            Assert.IsFalse(still.DropFinishedResults);
        }

        [TestMethod]
        public void AFullViewportModelNeverReportsATileLevelChange()
        {
            Assert.IsFalse(SegmentationCameraPolicy.TileLevelChanged(FullViewport, 1, 2));
        }

        [TestMethod]
        public void ResultValidityFollowsTheProfile()
        {
            Assert.IsTrue(SegmentationCameraPolicy.IsResultStillValid(SingleTiles, 1, 2, viewportUnchanged: false));

            Assert.IsTrue(SegmentationCameraPolicy.IsResultStillValid(MultiTiles, 2, 2, viewportUnchanged: false));
            Assert.IsFalse(SegmentationCameraPolicy.IsResultStillValid(MultiTiles, 1, 2, viewportUnchanged: true));

            Assert.IsTrue(SegmentationCameraPolicy.IsResultStillValid(FullViewport, 1, 2, viewportUnchanged: true));
            Assert.IsFalse(SegmentationCameraPolicy.IsResultStillValid(FullViewport, 1, 1, viewportUnchanged: false));
        }

        [TestMethod]
        public void InteractiveRequestSurvivesAMoveOnlyForSingleResolutionTiles()
        {
            Assert.IsFalse(SegmentationCameraPolicy.ViewMoveInvalidatesRequest(SingleTiles));
            Assert.IsTrue(SegmentationCameraPolicy.ViewMoveInvalidatesRequest(MultiTiles));
            Assert.IsTrue(SegmentationCameraPolicy.ViewMoveInvalidatesRequest(FullViewport));
        }

        [TestMethod]
        public void SettledSingleResolutionTilesResubmitOnlyWhenThePromptsChanged()
        {
            string sent = SegmentationCameraPolicy.PromptSignature([new Vector2(10, 20)], [new Vector2(1, 2)]);

            Assert.IsFalse(SegmentationCameraPolicy.SettleNeedsResubmit(SingleTiles, sent, sent));

            string moreBackground = SegmentationCameraPolicy.PromptSignature(
                [new Vector2(10, 20)], [new Vector2(1, 2), new Vector2(3, 4)]);
            Assert.IsTrue(SegmentationCameraPolicy.SettleNeedsResubmit(SingleTiles, sent, moreBackground));
        }

        [TestMethod]
        public void SettledSingleResolutionTilesRetryWhenNothingWasDelivered()
        {
            string current = SegmentationCameraPolicy.PromptSignature([new Vector2(10, 20)], []);

            Assert.IsTrue(SegmentationCameraPolicy.SettleNeedsResubmit(SingleTiles, lastSent: null, current));
        }

        [TestMethod]
        public void SettledMultiResolutionAndFullViewportAlwaysResubmit()
        {
            string sent = SegmentationCameraPolicy.PromptSignature([new Vector2(10, 20)], []);

            Assert.IsTrue(SegmentationCameraPolicy.SettleNeedsResubmit(MultiTiles, sent, sent));
            Assert.IsTrue(SegmentationCameraPolicy.SettleNeedsResubmit(FullViewport, sent, sent));
        }

        [TestMethod]
        public void PromptSignatureDistinguishesForegroundFromBackgroundAndOrder()
        {
            Vector2 a = new(1, 2);
            Vector2 b = new(3, 4);

            string foregroundA = SegmentationCameraPolicy.PromptSignature([a], []);
            string backgroundA = SegmentationCameraPolicy.PromptSignature([], [a]);
            Assert.AreNotEqual(foregroundA, backgroundA);

            string ab = SegmentationCameraPolicy.PromptSignature([a, b], []);
            string ba = SegmentationCameraPolicy.PromptSignature([b, a], []);
            Assert.AreNotEqual(ab, ba);

            List<Vector2> same = [a, b];
            Assert.AreEqual(ab, SegmentationCameraPolicy.PromptSignature(same, []));
        }
    }
}
