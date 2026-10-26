using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Camera-move rules on the single-resolution tile grid. A result depends on the fixed tile level and the prompts, so a
    /// pan or zoom must never cancel or drop it, and a settled view resubmits only when the prompts changed.
    /// </summary>
    [TestClass]
    public class SegmentationCameraPolicyTests
    {
        [TestMethod]
        public void SettledSingleResolutionTilesResubmitOnlyWhenThePromptsChanged()
        {
            string sent = SegmentationCameraPolicy.PromptSignature([new Vector2(10, 20)], [new Vector2(1, 2)]);

            Assert.IsFalse(SegmentationCameraPolicy.SettleNeedsResubmit(sent, sent));

            string moreBackground = SegmentationCameraPolicy.PromptSignature(
                [new Vector2(10, 20)], [new Vector2(1, 2), new Vector2(3, 4)]);
            Assert.IsTrue(SegmentationCameraPolicy.SettleNeedsResubmit(sent, moreBackground));
        }

        [TestMethod]
        public void SettledSingleResolutionTilesRetryWhenNothingWasDelivered()
        {
            string current = SegmentationCameraPolicy.PromptSignature([new Vector2(10, 20)], []);

            Assert.IsTrue(SegmentationCameraPolicy.SettleNeedsResubmit(lastSent: null, current));
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
