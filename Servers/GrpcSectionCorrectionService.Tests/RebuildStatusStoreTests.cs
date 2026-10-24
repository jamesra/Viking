using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Viking.GrpcSectionCorrectionService;

namespace GrpcSectionCorrectionService.Tests
{
    /// <summary>
    /// Tests for <see cref="RebuildStatusStore"/> state transitions and snapshot contents.
    /// </summary>
    [TestClass]
    public class RebuildStatusStoreTests
    {
        [TestMethod]
        public void Snapshot_Fresh_HasNoTimestampsAndNotInProgress()
        {
            RebuildSnapshot s = new RebuildStatusStore().Snapshot();
            Assert.IsFalse(s.InProgress);
            Assert.IsNull(s.LastStartedUtc);
            Assert.IsNull(s.LastCompletedUtc);
            Assert.AreEqual("", s.LastError);
            Assert.IsFalse(string.IsNullOrWhiteSpace(RebuildStatusStore.ServiceVersion));
            Assert.IsFalse(RebuildStatusStore.ServiceVersion.Contains('+'), "build metadata after '+' must be stripped");
        }

        [TestMethod]
        public void Begin_SetsInProgressAndStartTime_AndClearsPreviousError()
        {
            var store = new RebuildStatusStore();
            store.Begin();
            store.Complete("boom");

            DateTime before = DateTime.UtcNow;
            store.Begin();
            RebuildSnapshot s = store.Snapshot();

            Assert.IsTrue(s.InProgress);
            Assert.AreEqual("", s.LastError);
            DateTime started = s.LastStartedUtc.Value;
            Assert.AreEqual(DateTimeKind.Utc, started.Kind);
            Assert.IsTrue(started >= before.AddSeconds(-1) && started <= DateTime.UtcNow.AddSeconds(1));
        }

        [TestMethod]
        public void Complete_ClearsInProgressStampsTimeAndRecordsError()
        {
            var store = new RebuildStatusStore();
            store.Begin();
            store.Complete("failed: fake");
            RebuildSnapshot s = store.Snapshot();

            Assert.IsFalse(s.InProgress);
            Assert.AreEqual("failed: fake", s.LastError);
            Assert.IsNotNull(s.LastCompletedUtc);
            Assert.IsTrue(s.LastCompletedUtc.Value >= s.LastStartedUtc.Value);
        }

        [TestMethod]
        public void Complete_NullError_ReportsEmptyString()
        {
            var store = new RebuildStatusStore();
            store.Begin();
            store.Complete(null);
            Assert.AreEqual("", store.Snapshot().LastError);
        }

        [TestMethod]
        public void Snapshot_IsIndependentCopy()
        {
            var store = new RebuildStatusStore();
            store.Begin();
            RebuildSnapshot first = store.Snapshot();
            store.Complete("later");
            Assert.IsTrue(first.InProgress);
            Assert.AreEqual("", first.LastError);
        }
    }
}
