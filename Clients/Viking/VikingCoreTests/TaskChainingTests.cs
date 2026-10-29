using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}
