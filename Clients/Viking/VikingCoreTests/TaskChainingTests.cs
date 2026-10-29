using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="TaskChaining.ThenAsync{T}"/> (the chained task must not complete before the second stage does) and
    /// <see cref="TaskChaining.DrainToAsync"/> (throttle that surfaces a failed task).
    /// </summary>
    [TestClass]
    public class TaskChainingTests
    {
        [TestMethod]
        public async Task ThenAsync_DoesNotCompleteUntilSecondStageCompletes()
        {
            TaskCompletionSource<int> first = new TaskCompletionSource<int>();
            TaskCompletionSource<bool> save = new TaskCompletionSource<bool>();
            int received = 0;

            Task chained = first.Task.ThenAsync(v => { received = v; return save.Task; });

            first.SetResult(7);
            await Task.Delay(100);
            Assert.AreEqual(7, received, "second stage should have started");
            Assert.IsFalse(chained.IsCompleted, "chained task finished while the second stage was still running");

            save.SetResult(true);
            await chained;
        }

        [TestMethod]
        public async Task ThenAsync_SecondStageFailureFaultsChainedTask()
        {
            Task<int> first = Task.FromResult(1);
            Task chained = first.ThenAsync(_ => Task.FromException(new InvalidOperationException("save failed")));

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => chained);
        }

        [TestMethod]
        public async Task ThenAsync_FirstStageFailureSkipsSecondStage()
        {
            bool ran = false;
            Task<int> first = Task.FromException<int>(new ArgumentException("render failed"));
            Task chained = first.ThenAsync(_ => { ran = true; return Task.CompletedTask; });

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => chained);
            Assert.IsFalse(ran);
        }

        [TestMethod]
        public async Task DrainToAsync_WaitsUntilAtMostMaxPendingRemain()
        {
            TaskCompletionSource<bool> a = new TaskCompletionSource<bool>();
            TaskCompletionSource<bool> b = new TaskCompletionSource<bool>();
            List<Task> pending = new List<Task> { a.Task, b.Task };

            Task drain = TaskChaining.DrainToAsync(pending, 1);
            await Task.Delay(50);
            Assert.IsFalse(drain.IsCompleted);

            b.SetResult(true);
            await drain;
            Assert.AreEqual(1, pending.Count);
            Assert.AreSame(a.Task, pending[0]);
        }

        [TestMethod]
        public async Task DrainToAsync_ZeroMaxAwaitsEverything()
        {
            List<Task> pending = new List<Task> { Task.Delay(20), Task.Delay(40), Task.CompletedTask };

            await TaskChaining.DrainToAsync(pending, 0);

            Assert.AreEqual(0, pending.Count);
        }

        [TestMethod]
        public async Task DrainToAsync_RethrowsFailureOfCompletedTask()
        {
            List<Task> pending = new List<Task>
            {
                Task.FromException(new InvalidOperationException("png not written")),
                Task.CompletedTask
            };

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => TaskChaining.DrainToAsync(pending, 1));
        }

        [TestMethod]
        public async Task DrainToAsync_FailureWaitsForRemainingTasksAndClearsList()
        {
            TaskCompletionSource<bool> slow = new TaskCompletionSource<bool>();
            List<Task> pending = new List<Task> { Task.FromException(new InvalidOperationException("boom")), slow.Task };

            Task drain = TaskChaining.DrainToAsync(pending, 0);
            await Task.Delay(50);
            Assert.IsFalse(drain.IsCompleted, "failure must not be reported while another export is still writing");

            slow.SetResult(true);
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => drain);
            Assert.AreEqual(0, pending.Count);
        }

        [TestMethod]
        public async Task DrainToAsync_RethrowsCancellation()
        {
            TaskCompletionSource<bool> canceled = new TaskCompletionSource<bool>();
            canceled.SetCanceled();
            List<Task> pending = new List<Task> { canceled.Task };

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => TaskChaining.DrainToAsync(pending, 0));
        }

        /// <summary>
        /// Regression: Export Tiles dropped a tile whose render or PNG save failed (<c>Task.WhenAny</c> + <c>Remove</c>) and still wrote
        /// the section's index XML, so the pyramid had a hole the XML called complete. Export Tiles now throttles with
        /// <c>DrainToAsync(tiles, 256)</c> and drains with <c>DrainToAsync(tiles, 0)</c> before writing the index. For any tile
        /// outcomes, completion order and limit, the code after the drain runs only when no tile it took off the list failed or was
        /// canceled, and a failure surfaces only once every tile has finished, so none is still writing when the export reports it.
        /// </summary>
        [TestMethod]
        public void DrainToAsync_CodeAfterDrainRunsOnlyWhenNoTileFailed()
        {
            var gen = from outcomes in Gen.ListOf(Gen.Elements("ok", "fail", "cancel"))
                      from order in Gen.ListOf(outcomes.Count(), Gen.Choose(0, 1000))
                      from max in Gen.Choose(0, outcomes.Count())
                      select (outcomes: outcomes.ToArray(), order: order.ToArray(), max);

            Prop.ForAll(Arb.From(gen), t =>
            {
                int n = t.outcomes.Length;
                TaskCompletionSource<bool>[] tiles = Enumerable.Range(0, n).Select(_ => new TaskCompletionSource<bool>()).ToArray();
                List<Task> pending = tiles.Select(tile => (Task)tile.Task).ToList();
                bool indexWritten = false;
                async Task Section()
                {
                    await TaskChaining.DrainToAsync(pending, t.max);
                    indexWritten = true;
                }

                Task run = Section();

                //Model: done once at most max tiles are left, unless a tile it took off the list failed; then once every tile finished.
                bool expectDone = n <= t.max, expectSuccess = expectDone, failed = false;
                Assert.AreEqual(expectDone, run.IsCompleted, "a drain with at most max tiles pending must not wait");
                int finished = 0;
                foreach (int i in Enumerable.Range(0, n).OrderBy(i => t.order[i]))
                {
                    if (!expectDone)
                        Assert.IsFalse(run.IsCompleted, "the drain finished while a tile it must wait for was still writing");

                    switch (t.outcomes[i])
                    {
                        case "fail": tiles[i].SetException(new InvalidOperationException("png not written")); break;
                        case "cancel": tiles[i].SetCanceled(); break;
                        default: tiles[i].SetResult(true); break;
                    }

                    finished++;
                    if (expectDone)
                        continue;

                    failed |= t.outcomes[i] != "ok";
                    if (failed ? finished == n : n - finished <= t.max)
                    {
                        (expectDone, expectSuccess) = (true, !failed);
                        //Continuations run inline on SetResult here, so a drain that keeps waiting is caught at this point
                        Assert.IsTrue(run.IsCompleted, "the drain kept waiting after its condition was met");
                    }
                }

                Assert.AreSame(run, Task.WhenAny(run, Task.Delay(5000)).GetAwaiter().GetResult(), "the drain never finished");
                _ = run.Exception;
                Assert.AreEqual(expectSuccess, indexWritten, "the index was written although a tile failed, or not written after all succeeded");
                Assert.AreEqual(expectSuccess, run.Status == TaskStatus.RanToCompletion, "a tile's failure must reach the caller");
                if (!expectSuccess)
                    Assert.AreEqual(0, pending.Count);
            }).QuickCheckThrowOnFailure();
        }

        /// <summary>
        /// Regression: Section > Export frames and Export Tiles switch the viewer to each exported section and left it on the last
        /// one. Whatever sections the body visits, and whether it finishes, fails or is canceled, the value ends where it started.
        /// </summary>
        [TestMethod]
        public void RestoreAfterAsync_RestoresStartingValueAfterAnySequenceOfChanges()
        {
            var gen = from start in Gen.Choose(0, 5000)
                      from visited in Gen.ListOf(Gen.Choose(0, 5000))
                      from ending in Gen.Elements("finish", "fail", "cancel")
                      select (start, visited, ending);

            Prop.ForAll(Arb.From(gen), t =>
            {
                int section = t.start;
                Task body()
                {
                    foreach (int z in t.visited)
                        section = z;
                    return t.ending switch
                    {
                        "fail" => Task.FromException(new InvalidOperationException("tile failed")),
                        "cancel" => Task.FromCanceled(new System.Threading.CancellationToken(true)),
                        _ => Task.CompletedTask,
                    };
                }

                Task run = TaskChaining.RestoreAfterAsync(() => section, s => section = s, body);
                try
                {
                    run.GetAwaiter().GetResult();
                }
                catch (InvalidOperationException) when (t.ending == "fail") { }
                catch (TaskCanceledException) when (t.ending == "cancel") { }

                Assert.AreEqual(t.start, section);
                Assert.AreEqual(t.ending == "finish", run.Status == TaskStatus.RanToCompletion, "the body's outcome must reach the caller");
            }).QuickCheckThrowOnFailure();
        }

        /// <summary>
        /// The restore waits for the body: tiles still rendering would otherwise switch the section again after it was restored.
        /// </summary>
        [TestMethod]
        public async Task RestoreAfterAsync_DoesNotRestoreWhileBodyIsRunning()
        {
            int section = 10;
            TaskCompletionSource<bool> tiles = new TaskCompletionSource<bool>();

            Task run = TaskChaining.RestoreAfterAsync(() => section, s => section = s, () => { section = 42; return tiles.Task; });
            await Task.Delay(50);
            Assert.AreEqual(42, section, "restored before the body finished");
            Assert.IsFalse(run.IsCompleted);

            tiles.SetResult(true);
            await run;
            Assert.AreEqual(10, section);
        }

        /// <summary>
        /// The restore runs on the caller's synchronization context, as <see cref="TaskChaining.RestoreAfterAsync{T}"/> documents:
        /// the exports call it on the UI thread and their <c>set</c> assigns the viewer's section, which must not happen on a
        /// thread-pool thread. A <c>ConfigureAwait(false)</c> on the body's await would move the restore to the thread that finished
        /// the last tile.
        /// </summary>
        [TestMethod]
        public void RestoreAfterAsync_RestoresOnTheCallersContext()
        {
            SynchronizationContext? previous = SynchronizationContext.Current;
            PostingContext context = new();
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                bool restoredOnContext = false;
                //The body finishes on a timer thread, so the restore reaches the context only if the await captured it
                Task run = TaskChaining.RestoreAfterAsync(() => 0, _ => restoredOnContext = SynchronizationContext.Current == context, () => Task.Delay(100));
                Assert.IsTrue(run.Wait(5000), "the restore never ran");
                Assert.IsTrue(restoredOnContext, "the section was restored off the caller's context");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        /// <summary>
        /// Stands in for the Windows Forms context: posted work runs with this context current, as on the UI thread's message loop.
        /// </summary>
        private sealed class PostingContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object? state) =>
                ThreadPool.QueueUserWorkItem(_ => { SetSynchronizationContext(this); d(state); });
        }
    }
}
