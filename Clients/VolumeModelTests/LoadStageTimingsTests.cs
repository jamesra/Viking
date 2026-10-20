using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Viking.VolumeModel;

namespace VolumeModelTests
{
    /// <summary>
    /// Pins <see cref="LoadStageTimings.Start"/> and <see cref="LoadStageTimings.StageScope"/>:
    /// no-op scopes when nothing listens, one <see cref="LoadStageTimings.StageCompleted"/> per dispose when subscribed,
    /// independent nested stages, and safe concurrent reporting from several threads.
    /// </summary>
    [TestClass]
    public class LoadStageTimingsTests
    {
        private Action<string, string, TimeSpan> _handler;

        [TestCleanup]
        public void DetachHandler()
        {
            if (_handler != null)
            {
                LoadStageTimings.StageCompleted -= _handler;
                _handler = null;
            }
        }

        private void Subscribe(Action<string, string, TimeSpan> handler)
        {
            DetachHandler();
            _handler = handler;
            LoadStageTimings.StageCompleted += _handler;
        }

        [TestMethod]
        public void Start_WithNoSubscriber_ReturnsDefaultScope_DisposeDoesNotThrow()
        {
            using (LoadStageTimings.Start(LoadStageTimings.Warp, "42"))
            {
            }
        }

        [TestMethod]
        public void Start_WithNoSubscriber_DoesNotStartStopwatch()
        {
            LoadStageTimings.StageScope scope = LoadStageTimings.Start(LoadStageTimings.Warp, "42");
            FieldInfo timerField = typeof(LoadStageTimings.StageScope).GetField(
                "_timer",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(timerField);
            Assert.IsNull(timerField.GetValue(scope));
            scope.Dispose();
        }

        [TestMethod]
        public void Start_WithSubscriber_DisposeRaisesStageCompletedOnce()
        {
            int count = 0;
            string stage = null;
            string detail = null;
            TimeSpan elapsed = TimeSpan.Zero;

            Subscribe((s, d, t) =>
            {
                count++;
                stage = s;
                detail = d;
                elapsed = t;
            });

            using (LoadStageTimings.Start(LoadStageTimings.MosaicLoad, "7"))
            {
                Thread.Sleep(15);
            }

            Assert.AreEqual(1, count);
            Assert.AreEqual(LoadStageTimings.MosaicLoad, stage);
            Assert.AreEqual("7", detail);
            Assert.IsTrue(elapsed >= TimeSpan.Zero);
            Assert.IsTrue(elapsed.TotalMilliseconds >= 5, "elapsed should reflect time inside the scope");
        }

        [TestMethod]
        public void Start_NullDetail_PassesNullToSubscriber()
        {
            string detail = "sentinel";
            Subscribe((_, d, _) => detail = d);

            using (LoadStageTimings.Start(LoadStageTimings.SectionQueue))
            {
            }

            Assert.IsNull(detail);
        }

        [TestMethod]
        public void NestedScopes_EachDisposeReportsIndependently()
        {
            var reports = new ConcurrentBag<(string Stage, string Detail)>();
            Subscribe((s, d, _) => reports.Add((s, d)));

            using (LoadStageTimings.Start(LoadStageTimings.StosZipFetch, "g1"))
            {
                using (LoadStageTimings.Start(LoadStageTimings.WarpCacheRead, "3"))
                {
                }
            }

            Assert.AreEqual(2, reports.Count);
            CollectionAssert.AreEquivalent(
                new[] { (LoadStageTimings.StosZipFetch, "g1"), (LoadStageTimings.WarpCacheRead, "3") },
                reports.ToArray());
        }

        [TestMethod]
        public void ConcurrentDispose_FromSeveralThreads_EachScopeReportsOnce()
        {
            int count = 0;
            Subscribe((_, _, _) => Interlocked.Increment(ref count));

            const int threads = 8;
            var barrier = new Barrier(threads);
            var tasks = new Task[threads];

            for (int i = 0; i < threads; i++)
            {
                int section = i;
                tasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    using (LoadStageTimings.Start(LoadStageTimings.Warp, section.ToString()))
                    {
                    }
                });
            }

            Task.WaitAll(tasks);
            Assert.AreEqual(threads, count);
        }
    }
}
