using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    [TestClass]
    public class TileUploadCacheTests
    {
        [TestMethod]
        public async Task GivingUpOnAnUploadLeavesItRunning()
        {
            TaskCompletionSource<bool> upload = new();
            using CancellationTokenSource waiter = new();
            Task<bool> wait = SegmentationViewportSession.WaitForUploadAsync(upload.Task, waiter.Token);

            waiter.Cancel();
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => wait);
            Assert.IsFalse(upload.Task.IsCompleted);

            // The next caller that wants the tile still gets the upload's answer.
            upload.SetResult(true);
            Assert.IsTrue(await SegmentationViewportSession.WaitForUploadAsync(upload.Task, CancellationToken.None));
        }

        [TestMethod]
        public async Task WaitingForAnUploadReturnsItsResultAndFault()
        {
            Assert.IsFalse(await SegmentationViewportSession.WaitForUploadAsync(Task.FromResult(false), new CancellationTokenSource().Token));

            TaskCompletionSource<bool> failing = new();
            Task<bool> wait = SegmentationViewportSession.WaitForUploadAsync(failing.Task, new CancellationTokenSource().Token);
            failing.SetException(new InvalidOperationException("upload failed"));
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => wait);
        }

        [TestMethod]
        public void AddRemoveClearAndCountBehaveLikeASet()
        {
            TileUploadCache cache = new();

            cache.Add("a");
            cache.Add("a");
            cache.Add("b");
            Assert.AreEqual(2, cache.Count);
            Assert.IsTrue(cache.Contains("a"));

            cache.Remove("a");
            Assert.IsFalse(cache.Contains("a"));

            cache.Clear();
            Assert.AreEqual(0, cache.Count);
        }

        /// <summary>
        /// Upload continuations add keys on pool threads while a camera move clears them from the UI thread.
        /// A plain HashSet corrupts or throws under that load; this cache must survive it.
        /// </summary>
        [TestMethod]
        public void ConcurrentAddRemoveClearAndReadDoNotThrow()
        {
            TileUploadCache cache = new();
            const int perWorker = 20_000;

            Task[] workers =
            [
                Task.Run(() => { for (int i = 0; i < perWorker; i++) cache.Add($"t{i % 512}"); }),
                Task.Run(() => { for (int i = 0; i < perWorker; i++) cache.Add($"u{i % 512}"); }),
                Task.Run(() => { for (int i = 0; i < perWorker; i++) cache.Remove($"t{i % 512}"); }),
                Task.Run(() => { for (int i = 0; i < perWorker / 20; i++) cache.Clear(); }),
                Task.Run(() => { for (int i = 0; i < perWorker; i++) _ = cache.Contains($"u{i % 512}") | cache.Count > 0; }),
            ];

            Assert.IsTrue(Task.WaitAll(workers, TimeSpan.FromSeconds(30)), "A worker hung, as a corrupted hash set can.");
            Assert.IsTrue(workers.All(worker => worker.Status == TaskStatus.RanToCompletion));
        }
    }
}
