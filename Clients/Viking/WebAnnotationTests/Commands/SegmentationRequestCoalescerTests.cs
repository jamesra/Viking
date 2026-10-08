using System;
using System.Threading;
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

        /// <summary>
        /// One thread plays the UI (every click bumps a prompt version, then asks to start); another plays
        /// the worker (finishes the attempt and starts the follow-up). However the two interleave, the
        /// newest attempt must have started after the last click, otherwise a click was dropped.
        /// </summary>
        [TestMethod]
        public void ConcurrentClicksNeverLoseTheFollowUp()
        {
            const int iterations = 100_000;
            SegmentationRequestCoalescer coalescer = new();
            int version = 0;
            int newestCaptured = 0;
            bool clicksDone = false;

            void Capture()
            {
                int seen = Volatile.Read(ref version);
                int current;
                do
                {
                    current = Volatile.Read(ref newestCaptured);
                    if (seen <= current)
                        return;
                }
                while (Interlocked.CompareExchange(ref newestCaptured, seen, current) != current);
            }

            Thread worker = new(() =>
            {
                while (!Volatile.Read(ref clicksDone) || coalescer.IsBusy)
                {
                    if (!coalescer.IsBusy)
                        continue;

                    if (coalescer.OnFinishedShouldRetry() && coalescer.TryStart(out _))
                        Capture();
                }
            });
            worker.Start();

            for (int i = 0; i < iterations; i++)
            {
                Interlocked.Increment(ref version);
                if (coalescer.TryStart(out _))
                    Capture();
            }

            Volatile.Write(ref clicksDone, true);
            Assert.IsTrue(worker.Join(TimeSpan.FromSeconds(30)), "Worker did not drain the coalescer.");

            Assert.AreEqual(
                Volatile.Read(ref version),
                Volatile.Read(ref newestCaptured),
                "The last click was never answered by a started attempt.");
            Assert.IsFalse(coalescer.IsBusy);
            Assert.IsFalse(coalescer.PendingRefresh);
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
