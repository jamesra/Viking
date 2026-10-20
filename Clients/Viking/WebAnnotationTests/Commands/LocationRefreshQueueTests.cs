using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.AutoPolygonize;

namespace WebAnnotationTests.Commands
{
    [TestClass]
    public class LocationRefreshQueueTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        /// <summary>Awaits <paramref name="task"/>, failing the test instead of hanging when it does not finish.</summary>
        private static async Task Within(Task task)
        {
            if (await Task.WhenAny(task, Task.Delay(Timeout)).ConfigureAwait(false) != task)
                Assert.Fail("The operation did not complete in time.");

            await task.ConfigureAwait(false);
        }

        [TestMethod]
        public async Task RequestsForABusyLocationCollapseToTheNewestOne()
        {
            List<(long Id, int Section, int Generation)> calls = [];
            TaskCompletionSource<bool> releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

            LocationRefreshQueue queue = new(
                async (id, section, generation) =>
                {
                    lock (calls)
                        calls.Add((id, section, generation));

                    if (generation == 1)
                    {
                        firstStarted.SetResult(true);
                        await releaseFirst.Task.ConfigureAwait(false);
                    }
                },
                () => true,
                maxConcurrent: 2);

            Task first = queue.RefreshAsync(7, 3, 1);
            await Within(firstStarted.Task);

            await queue.RefreshAsync(7, 3, 2);
            await queue.RefreshAsync(7, 3, 3);
            releaseFirst.SetResult(true);
            await Within(first);

            CollectionAssert.AreEqual(
                new[] { (7L, 3, 1), (7L, 3, 3) },
                calls,
                "The in-flight refresh runs, then only the newest queued request; the older queued one is replaced.");
        }

        [TestMethod]
        public async Task DifferentLocationsNeverExceedTheConcurrencyCap()
        {
            int running = 0;
            int highWater = 0;
            TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

            LocationRefreshQueue queue = new(
                async (id, section, generation) =>
                {
                    int now = Interlocked.Increment(ref running);
                    int seen;
                    do
                    {
                        seen = Volatile.Read(ref highWater);
                    }
                    while (now > seen && Interlocked.CompareExchange(ref highWater, now, seen) != seen);

                    await release.Task.ConfigureAwait(false);
                    Interlocked.Decrement(ref running);
                },
                () => true,
                maxConcurrent: 2);

            Task[] all = [queue.RefreshAsync(1, 1, 1), queue.RefreshAsync(2, 1, 1), queue.RefreshAsync(3, 1, 1), queue.RefreshAsync(4, 1, 1)];

            SpinWait.SpinUntil(() => Volatile.Read(ref running) >= 2, Timeout);
            await Task.Delay(100);
            Assert.AreEqual(2, Volatile.Read(ref running), "Only two refreshes may hold a slot at once.");

            release.SetResult(true);
            await Within(Task.WhenAll(all));
            Assert.AreEqual(2, Volatile.Read(ref highWater));
        }

        [TestMethod]
        public async Task DisabledQueueDoesNotRun()
        {
            int calls = 0;
            LocationRefreshQueue queue = new(
                (id, section, generation) =>
                {
                    Interlocked.Increment(ref calls);
                    return Task.CompletedTask;
                },
                () => false,
                maxConcurrent: 1);

            await Within(queue.RefreshAsync(1, 1, 1));

            Assert.AreEqual(0, calls);
        }

        [TestMethod]
        public async Task LocationCanRefreshAgainAfterItFinished()
        {
            int calls = 0;
            LocationRefreshQueue queue = new(
                (id, section, generation) =>
                {
                    Interlocked.Increment(ref calls);
                    return Task.CompletedTask;
                },
                () => true,
                maxConcurrent: 1);

            await Within(queue.RefreshAsync(1, 1, 1));
            await Within(queue.RefreshAsync(1, 1, 2));

            Assert.AreEqual(2, calls);
        }
    }
}
