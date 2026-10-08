using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    [TestClass]
    public class PanZoomDebouncerTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        [TestMethod]
        public void SettleIsPostedToTheUiAndRunsOnce()
        {
            using ManualResetEventSlim settled = new();
            int settles = 0;
            int posts = 0;
            using PanZoomDebouncer debouncer = new(
                20,
                () =>
                {
                    Interlocked.Increment(ref settles);
                    settled.Set();
                },
                action =>
                {
                    Interlocked.Increment(ref posts);
                    action();
                });

            debouncer.Restart();

            Assert.IsTrue(settled.Wait(Timeout), "The settle callback never ran.");
            Thread.Sleep(100);
            Assert.AreEqual(1, settles);
            Assert.AreEqual(1, posts, "The callback must go through the UI post, not run on the timer thread directly.");
        }

        [TestMethod]
        public void RestartKeepsPushingTheSettleBack()
        {
            using ManualResetEventSlim settled = new();
            DateTime started = DateTime.UtcNow;
            DateTime settledAt = default;
            using PanZoomDebouncer debouncer = new(
                150,
                () =>
                {
                    settledAt = DateTime.UtcNow;
                    settled.Set();
                },
                action => action());

            for (int i = 0; i < 4; i++)
            {
                debouncer.Restart();
                Thread.Sleep(60);
            }

            Assert.IsTrue(settled.Wait(Timeout));
            Assert.IsTrue(
                settledAt - started >= TimeSpan.FromMilliseconds(4 * 60),
                "The settle fired while the view was still moving.");
        }

        [TestMethod]
        public void PostedSettleIsDiscardedAfterDispose()
        {
            Action? posted = null;
            using ManualResetEventSlim queued = new();
            int settles = 0;
            PanZoomDebouncer debouncer = new(
                10,
                () => Interlocked.Increment(ref settles),
                action =>
                {
                    posted = action;
                    queued.Set();
                });

            debouncer.Restart();
            Assert.IsTrue(queued.Wait(Timeout), "The timer never queued the settle.");

            debouncer.Dispose();
            posted!();

            Assert.AreEqual(0, settles, "Deactivating the command must discard a settle that is already queued.");
        }

        [TestMethod]
        public void RestartAfterDisposeDoesNothing()
        {
            int settles = 0;
            PanZoomDebouncer debouncer = new(10, () => Interlocked.Increment(ref settles), action => action());

            debouncer.Dispose();
            debouncer.Restart();
            Thread.Sleep(80);

            Assert.AreEqual(0, settles);
        }
    }
}
