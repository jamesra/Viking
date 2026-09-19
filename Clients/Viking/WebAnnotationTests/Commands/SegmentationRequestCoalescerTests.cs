using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    [TestClass]
    public class SegmentationRequestCoalescerTests
    {
        [TestMethod]
        public void BusyClickRetriesOnceAfterFinish()
        {
            SegmentationRequestCoalescer coalescer = new();

            Assert.IsTrue(coalescer.TryStart(out int first));
            Assert.AreEqual(1, first);
            Assert.IsFalse(coalescer.TryStart(out _));
            Assert.IsTrue(coalescer.PendingRefresh);
            Assert.IsTrue(coalescer.OnFinishedShouldRetry());
            Assert.IsFalse(coalescer.PendingRefresh);
            Assert.IsTrue(coalescer.TryStart(out int followUp));
            Assert.AreEqual(2, followUp);
        }

        [TestMethod]
        public void TwoClicksWhileBusyStillOneRetry()
        {
            SegmentationRequestCoalescer coalescer = new();

            Assert.IsTrue(coalescer.TryStart(out _));
            coalescer.MarkDirty();
            coalescer.MarkDirty();
            Assert.IsFalse(coalescer.TryStart(out _));

            Assert.IsTrue(coalescer.OnFinishedShouldRetry());
            Assert.IsFalse(coalescer.OnFinishedShouldRetry());
        }

        [TestMethod]
        public void CancelPendingDoesNotRetry()
        {
            SegmentationRequestCoalescer coalescer = new();

            Assert.IsTrue(coalescer.TryStart(out _));
            coalescer.MarkDirty();
            coalescer.CancelPending();
            Assert.IsFalse(coalescer.OnFinishedShouldRetry());
        }

        [TestMethod]
        public void InvalidateRejectsInFlightGeneration()
        {
            SegmentationRequestCoalescer coalescer = new();

            Assert.IsTrue(coalescer.TryStart(out int generation));
            coalescer.MarkDirty();
            coalescer.Invalidate();

            Assert.IsFalse(coalescer.ShouldApply(generation));
            Assert.IsFalse(coalescer.TryApply(generation));
            Assert.IsFalse(coalescer.OnFinishedShouldRetry());
        }

        [TestMethod]
        public void OlderGenerationIsRejectedAfterNewerApplies()
        {
            SegmentationRequestCoalescer coalescer = new();

            Assert.IsTrue(coalescer.TryStart(out int older));
            coalescer.OnFinishedShouldRetry();
            Assert.IsTrue(coalescer.TryStart(out int newer));

            Assert.IsTrue(coalescer.TryApply(newer));
            Assert.IsFalse(coalescer.TryApply(older));
            Assert.IsFalse(coalescer.ShouldApply(older));
        }

        [TestMethod]
        public void MarkDirtyDuringUploadSchedulesFollowUp()
        {
            SegmentationRequestCoalescer coalescer = new();

            coalescer.MarkDirty();
            Assert.IsTrue(coalescer.TryStart(out _));
            coalescer.MarkDirty();
            Assert.IsTrue(coalescer.OnFinishedShouldRetry());
        }
    }
}
