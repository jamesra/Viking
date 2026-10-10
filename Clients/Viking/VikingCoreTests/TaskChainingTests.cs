using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="TaskChaining.ThenAsync{T}"/>: the chained task must not complete before the second stage does.
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
    }
}
