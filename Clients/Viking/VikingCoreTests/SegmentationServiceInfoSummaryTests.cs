using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI.WPF.Models;

namespace VikingCoreTests
{
    /// <summary>
    /// The picker's Model and Load columns, built from a GetServerStatus probe. No network: the probe results are
    /// set directly on <see cref="SegmentationServiceInfo"/>.
    /// </summary>
    [TestClass]
    public class SegmentationServiceInfoSummaryTests
    {
        private static SegmentationServiceInfo Reachable(uint inFlight = 0, uint workers = 1, double latencyMs = 0)
        {
            var info = new SegmentationServiceInfo { Name = "SAM2 v5.2", Endpoint = "v5.segmentation.example:443" };
            info.IsReachable = true;
            info.HasProbeCompleted = true;
            info.InferenceWorkers = workers;
            info.InFlightRequests = inFlight;
            info.RecentLatencyMs = latencyMs;
            return info;
        }

        [TestMethod]
        public void BeforeAnyProbe_ModelIsBlank_AndLoadIsNotChecked()
        {
            var info = new SegmentationServiceInfo { Name = "x" };

            Assert.AreEqual(string.Empty, info.ModelSummary);
            Assert.AreEqual("not checked", info.LoadSummary);
        }

        [TestMethod]
        public void WhileProbing_BothColumnsSaySo()
        {
            var info = new SegmentationServiceInfo { Name = "x", IsProbing = true };

            Assert.AreEqual("…", info.ModelSummary);
            Assert.AreEqual("checking…", info.LoadSummary);
        }

        [TestMethod]
        public void UnreachableServer_IsOffline()
        {
            var info = new SegmentationServiceInfo { Name = "x" };
            info.HasProbeCompleted = true;
            info.IsReachable = false;

            Assert.AreEqual("—", info.ModelSummary);
            Assert.AreEqual("offline", info.LoadSummary);
            Assert.IsNull(info.ModelToolTip);
        }

        [TestMethod]
        public void ModelColumn_ShowsTheServedModelAndTrainingSet_NotTheIdentityName()
        {
            var info = Reachable();
            info.ModelId = "v5";
            info.ModelVersion = "v5.3";
            info.TrainingDataSet = "v3";
            info.ModelDescription = "Run sam2_em_v5, trained on training set v3.";

            Assert.AreEqual("v5.3 · set v3", info.ModelSummary);
            StringAssert.Contains(info.ModelToolTip, "Model v5.3 (id v5)");
            StringAssert.Contains(info.ModelToolTip, "Training set v3");
            StringAssert.Contains(info.ModelToolTip, "Run sam2_em_v5");
            Assert.AreEqual("SAM2 v5.2", info.Name, "the Identity name is left as registered");
        }

        [TestMethod]
        public void ModelColumn_FallsBackToDisplayNameThenId()
        {
            var info = Reachable();
            info.ModelId = "v4";
            Assert.AreEqual("v4", info.ModelSummary);

            info.ModelDisplayName = "SEM v1";
            Assert.AreEqual("SEM v1", info.ModelSummary);
        }

        [TestMethod]
        public void ServerWithoutModelInfo_SaysUnknown()
        {
            var info = Reachable();

            Assert.AreEqual("unknown model", info.ModelSummary);
            StringAssert.Contains(info.ModelToolTip, "no model information");
        }

        [TestMethod]
        public void IdleServer_IsIdle()
        {
            Assert.AreEqual("idle", Reachable().LoadSummary);
            Assert.AreEqual("idle · 95 ms/request", Reachable(latencyMs: 95.4).LoadSummary);
        }

        [TestMethod]
        public void RequestsUpToTheWorkerCount_AreRunning()
        {
            Assert.AreEqual("busy · 2 running", Reachable(inFlight: 2, workers: 2).LoadSummary);
        }

        [TestMethod]
        public void RequestsBeyondTheWorkers_AreQueued()
        {
            Assert.AreEqual(
                "busy · 1 running, 2 queued · 820 ms/request",
                Reachable(inFlight: 3, workers: 1, latencyMs: 820).LoadSummary);
        }

        [TestMethod]
        public void ZeroWorkersReported_CountsAsOne()
        {
            Assert.AreEqual("busy · 1 running, 1 queued", Reachable(inFlight: 2, workers: 0).LoadSummary);
        }

        [TestMethod]
        public void LoadToolTip_CarriesTimingsAndCache()
        {
            var info = Reachable(inFlight: 1, latencyMs: 300);
            info.ProbeLatencyMs = 42;
            info.Version = "0.1.0";
            info.CachedImages = 12;
            info.CacheMemoryBytes = 64UL * 1024 * 1024;

            StringAssert.Contains(info.LoadToolTip, "42 ms RTT");
            StringAssert.Contains(info.LoadToolTip, "Server 0.1.0");
            StringAssert.Contains(info.LoadToolTip, "12 tiles, 64 MiB");
        }

        [TestMethod]
        public void ChangingTheModel_RaisesModelSummaryChange()
        {
            var info = Reachable();
            var changed = new List<string>();
            ((INotifyPropertyChanged)info).PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            info.ModelVersion = "v5.4";
            info.ModelVersion = "v5.4";

            CollectionAssert.Contains(changed, nameof(SegmentationServiceInfo.ModelVersion));
            CollectionAssert.Contains(changed, nameof(SegmentationServiceInfo.ModelSummary));
            Assert.AreEqual(1, changed.FindAll(n => n == nameof(SegmentationServiceInfo.ModelVersion)).Count);
        }

        [TestMethod]
        public void BusierServer_RanksBehindAnIdleOne()
        {
            Assert.IsTrue(Reachable(inFlight: 0, latencyMs: 900).PreferenceScore < Reachable(inFlight: 1, latencyMs: 50).PreferenceScore);
        }
    }
}
