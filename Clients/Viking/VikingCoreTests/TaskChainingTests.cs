using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    }
}
