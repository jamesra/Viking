using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Viking.GrpcSectionCorrectionService;
using Viking.SectionCorrectionServiceTypes.gRPC.V1.Protos;

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
            RebuildStatus s = new RebuildStatusStore().Snapshot();
            Assert.IsFalse(s.InProgress);
            Assert.IsNull(s.LastStartedUtc);
            Assert.IsNull(s.LastCompletedUtc);
            Assert.AreEqual("", s.LastError);
            Assert.AreEqual(0, s.LastVolumes.Count);
            Assert.AreEqual(RebuildStatusStore.ServiceVersion, s.ServiceVersion);
            Assert.IsFalse(string.IsNullOrWhiteSpace(s.ServiceVersion));
            Assert.IsFalse(s.ServiceVersion.Contains('+'), "build metadata after '+' must be stripped");
        }

        [TestMethod]
        public void Begin_SetsInProgressStartTimeAndVolumes_AndClearsPreviousError()
        {
            var store = new RebuildStatusStore();
            store.Begin(["A"]);
            store.Complete("boom");

            DateTime before = DateTime.UtcNow;
            store.Begin(["A", "B"]);
            RebuildStatus s = store.Snapshot();

            Assert.IsTrue(s.InProgress);
            Assert.AreEqual("", s.LastError);
            CollectionAssert.AreEqual(new[] { "A", "B" }, new List<string>(s.LastVolumes));
            DateTime started = s.LastStartedUtc.ToDateTime();
            Assert.AreEqual(DateTimeKind.Utc, started.Kind);
            Assert.IsTrue(started >= before.AddSeconds(-1) && started <= DateTime.UtcNow.AddSeconds(1));
        }

        [TestMethod]
        public void Begin_CopiesVolumeList()
        {
            var store = new RebuildStatusStore();
            var names = new List<string> { "A" };
            store.Begin(names);
            names.Add("Later");
            CollectionAssert.AreEqual(new[] { "A" }, new List<string>(store.Snapshot().LastVolumes));
        }

        [TestMethod]
        public void SetVolumes_ReplacesListAndCopies_WithoutChangingProgress()
        {
            var store = new RebuildStatusStore();
            store.Begin([]);
            var names = new List<string> { "X", "Y" };
            store.SetVolumes(names);
            names.Clear();

            RebuildStatus s = store.Snapshot();
            Assert.IsTrue(s.InProgress);
            CollectionAssert.AreEqual(new[] { "X", "Y" }, new List<string>(s.LastVolumes));
        }

        [TestMethod]
        public void Complete_ClearsInProgressStampsTimeAndRecordsError()
        {
            var store = new RebuildStatusStore();
            store.Begin(["A"]);
            store.Complete("failed: fake");
            RebuildStatus s = store.Snapshot();

            Assert.IsFalse(s.InProgress);
            Assert.AreEqual("failed: fake", s.LastError);
            Assert.IsNotNull(s.LastCompletedUtc);
            Assert.IsTrue(s.LastCompletedUtc.ToDateTime() >= s.LastStartedUtc.ToDateTime());
            CollectionAssert.AreEqual(new[] { "A" }, new List<string>(s.LastVolumes));
        }

        [TestMethod]
        public void Complete_NullError_ReportsEmptyString()
        {
            var store = new RebuildStatusStore();
            store.Begin(["A"]);
            store.Complete(null);
            Assert.AreEqual("", store.Snapshot().LastError);
        }

        [TestMethod]
        public void Snapshot_IsIndependentCopy()
        {
            var store = new RebuildStatusStore();
            store.Begin(["A"]);
            RebuildStatus first = store.Snapshot();
            store.Complete("later");
            Assert.IsTrue(first.InProgress);
            Assert.AreEqual("", first.LastError);
        }
    }
}
