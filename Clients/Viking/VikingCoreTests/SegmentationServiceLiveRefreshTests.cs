using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI.WPF.Services;
using Viking.UI.WPF.ViewModels;

namespace VikingCoreTests
{
    /// <summary>
    /// The open picker refreshes each server's model and load columns every second. These tests drive one refresh at a
    /// time with a fake probe, after the first (real) probe has failed for the reserved .invalid endpoints.
    /// </summary>
    [TestClass]
    public class SegmentationServiceLiveRefreshTests
    {
        private static SegmentationServiceSelectionViewModel ProbedPicker()
        {
            var services = new Dictionary<long, object>
            {
                [1] = Svc("Alpha", "http://a.invalid"),
                [2] = Svc("Beta", "http://b.invalid"),
            };
            var vm = new SegmentationServiceSelectionViewModel(null, "https://identity.invalid", null, services);

            DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
            // The status line is set last, after the first probe has ranked (cleared and refilled) the list.
            while (vm.StatusMessage == null || !vm.StatusMessage.StartsWith("0 reachable, 2 offline.", StringComparison.Ordinal))
            {
                Assert.IsTrue(DateTime.UtcNow < deadline, "The first server probe never finished.");
                Thread.Sleep(10);
            }

            return vm;
        }

        private static object Svc(string name, string endpoint)
        {
            using JsonDocument doc = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, string> { ["name"] = name, ["endpoint"] = endpoint }));
            return doc.RootElement.Clone();
        }

        private static SegmentationServerProbeResult Up(uint inFlight, string modelVersion = "v5.3") => new()
        {
            IsReachable = true,
            ProbeLatencyMs = 12,
            InFlightRequests = inFlight,
            InferenceWorkers = 1,
            RecentLatencyMs = 250,
            ModelId = "v5",
            ModelVersion = modelVersion,
            TrainingDataSet = "v3",
        };

        private static Func<string, CancellationToken, Task<SegmentationServerProbeResult>> Probe(
            ConcurrentBag<string> asked, Func<string, SegmentationServerProbeResult> answer) =>
            (endpoint, _) =>
            {
                asked.Add(endpoint);
                return Task.FromResult(answer(endpoint));
            };

        private static string[] Names(SegmentationServiceSelectionViewModel vm) => vm.ServiceNodes.Select(n => n.Name).ToArray();

        [TestMethod]
        public async Task OfflineServers_AreSkippedExceptOnEveryFifthRefresh()
        {
            var vm = ProbedPicker();
            var asked = new ConcurrentBag<string>();

            for (int refresh = 1; refresh < SegmentationServiceSelectionViewModel.OfflineRecheckEveryRefreshes; refresh++)
            {
                await vm.RefreshOnceAsync(Probe(asked, _ => Up(0)), refresh, CancellationToken.None);
            }

            Assert.AreEqual(0, asked.Count, "Offline servers should not be probed every second.");

            await vm.RefreshOnceAsync(Probe(asked, _ => Up(0)), SegmentationServiceSelectionViewModel.OfflineRecheckEveryRefreshes, CancellationToken.None);

            CollectionAssert.AreEquivalent(new[] { "http://a.invalid", "http://b.invalid" }, asked.ToArray());
        }

        [TestMethod]
        public async Task AServerThatComesBack_GetsItsColumnsUpdatedInPlace_WithoutReordering()
        {
            var vm = ProbedPicker();
            string[] before = Names(vm);
            var asked = new ConcurrentBag<string>();

            await vm.RefreshOnceAsync(
                Probe(asked, endpoint => endpoint == "http://b.invalid" ? Up(2) : new SegmentationServerProbeResult()),
                SegmentationServiceSelectionViewModel.OfflineRecheckEveryRefreshes,
                CancellationToken.None);

            CollectionAssert.AreEqual(before, Names(vm), "A refresh must not move rows under the mouse.");
            var beta = vm.ServiceNodes.Single(n => n.Name == "Beta").Service;
            Assert.IsTrue(beta.IsReachable);
            Assert.AreEqual("v5.3 · set v3", beta.ModelSummary);
            Assert.AreEqual("busy · 1 running, 1 queued · 250 ms/request", beta.LoadSummary);
            Assert.IsTrue(vm.ServiceNodes.Single(n => n.Name == "Alpha").Service.IsUnreachable);
        }

        [TestMethod]
        public async Task OnlineServers_AreProbedEveryRefresh_AndTheLoadColumnFollows()
        {
            var vm = ProbedPicker();
            var asked = new ConcurrentBag<string>();
            await vm.RefreshOnceAsync(Probe(asked, _ => Up(1)), SegmentationServiceSelectionViewModel.OfflineRecheckEveryRefreshes, CancellationToken.None);
            var alpha = vm.ServiceNodes.Single(n => n.Name == "Alpha").Service;
            Assert.AreEqual("busy · 1 running · 250 ms/request", alpha.LoadSummary);

            asked = new ConcurrentBag<string>();
            await vm.RefreshOnceAsync(Probe(asked, _ => Up(0, "v5.4")), 6, CancellationToken.None);

            Assert.AreEqual(2, asked.Count);
            Assert.AreEqual("idle · 250 ms/request", alpha.LoadSummary);
            Assert.AreEqual("v5.4 · set v3", alpha.ModelSummary);
        }

        [TestMethod]
        public async Task ACancelledRefresh_ChangesNothing()
        {
            var vm = ProbedPicker();
            using var cts = new CancellationTokenSource();
            var asked = new ConcurrentBag<string>();

            await vm.RefreshOnceAsync(
                (endpoint, _) =>
                {
                    asked.Add(endpoint);
                    cts.Cancel();
                    return Task.FromResult(Up(0));
                },
                SegmentationServiceSelectionViewModel.OfflineRecheckEveryRefreshes,
                cts.Token);

            Assert.AreEqual(2, asked.Count);
            Assert.IsTrue(vm.ServiceNodes.All(n => n.Service.IsUnreachable), "Results that arrive after the picker closed must be dropped.");
        }

        [TestMethod]
        public void StartAndStop_ToggleTheRefresh_AndStartingTwiceIsHarmless()
        {
            var vm = new SegmentationServiceSelectionViewModel(null, "https://identity.invalid");
            Assert.IsFalse(vm.IsLiveRefreshRunning);

            vm.StartLiveRefresh();
            vm.StartLiveRefresh();
            Assert.IsTrue(vm.IsLiveRefreshRunning);

            vm.StopLiveRefresh();
            vm.StopLiveRefresh();
            Assert.IsFalse(vm.IsLiveRefreshRunning);
        }

        // ---- offline servers are listed but cannot be selected ----

        [TestMethod]
        public void AnOfflineServer_CannotBeSelected()
        {
            var vm = ProbedPicker();
            var alpha = vm.ServiceNodes.Single(n => n.Name == "Alpha");

            vm.SelectedService = alpha;

            Assert.IsNull(vm.SelectedService);
            Assert.IsFalse(vm.SelectCommand.CanExecute(null));
            Assert.AreEqual("offline", alpha.Service.LoadSummary);
            Assert.AreEqual("—", alpha.Service.ModelSummary);
        }

        [TestMethod]
        public void ALastUsedServerThatIsOffline_IsNotPreselected_NorKeptAsTheEndpoint()
        {
            var services = new Dictionary<long, object> { [1] = Svc("Alpha", "http://a.invalid") };
            var vm = new SegmentationServiceSelectionViewModel(null, "https://identity.invalid", "http://a.invalid", services);

            DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
            while (vm.StatusMessage == null || !vm.StatusMessage.StartsWith("0 reachable, 1 offline.", StringComparison.Ordinal))
            {
                Assert.IsTrue(DateTime.UtcNow < deadline, "The first server probe never finished.");
                Thread.Sleep(10);
            }

            Assert.IsNull(vm.SelectedService);
            Assert.AreEqual(string.Empty, vm.SelectedServiceEndpoint);
            Assert.IsFalse(vm.SelectCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task ASelectedServerThatGoesOffline_IsDeselected_AndOneThatComesBack_CanBeSelected()
        {
            var vm = ProbedPicker();
            var asked = new ConcurrentBag<string>();
            int every = SegmentationServiceSelectionViewModel.OfflineRecheckEveryRefreshes;

            await vm.RefreshOnceAsync(Probe(asked, _ => Up(0)), every, CancellationToken.None);
            var alpha = vm.ServiceNodes.Single(n => n.Name == "Alpha");
            vm.SelectedService = alpha;
            Assert.AreSame(alpha, vm.SelectedService, "An online server can be selected.");
            Assert.IsTrue(vm.SelectCommand.CanExecute(null));
            StringAssert.StartsWith(vm.StatusMessage, "2 reachable, 0 offline.");

            await vm.RefreshOnceAsync(Probe(asked, e => e == "http://a.invalid" ? new SegmentationServerProbeResult() : Up(0)), every + 1, CancellationToken.None);

            Assert.IsNull(vm.SelectedService, "A server that went offline must not stay selected.");
            Assert.IsFalse(vm.SelectCommand.CanExecute(null));
            Assert.AreEqual("offline", alpha.Service.LoadSummary);
            StringAssert.StartsWith(vm.StatusMessage, "1 reachable, 1 offline.");

            await vm.RefreshOnceAsync(Probe(asked, _ => Up(0)), 2 * every, CancellationToken.None);
            vm.SelectedService = alpha;
            Assert.AreSame(alpha, vm.SelectedService, "A server that is back can be selected again.");
        }
    }
}
