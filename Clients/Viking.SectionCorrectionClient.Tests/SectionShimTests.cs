using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Google.Protobuf.WellKnownTypes;
using Viking.SectionCorrectionServiceTypes.gRPC.V2.Protos;
using GridCell = Viking.gRPC.Geometry.V1.GridCell;
using Vector2 = Viking.gRPC.Geometry.V1.Vector2;

namespace Viking.SectionCorrectionClient.Tests
{
    [TestClass]
    public class SectionShimSettingsTests
    {
        [TestMethod]
        [DataRow(16.0, 0.0)]
        [DataRow(8.0, 0.0)]
        [DataRow(6.0, 0.5)]
        [DataRow(4.0, 1.0)]
        [DataRow(2.0, 1.0)]
        public void Weight_FadesLinearlyBetweenStartAndEnd(double downsample, double expected)
        {
            Assert.AreEqual(expected, new SectionShimSettings().Weight(downsample), 1e-9);
        }

        [TestMethod]
        public void Weight_InvertedPreferencesBecomeAStepAtTheEnd()
        {
            var settings = new SectionShimSettings { FadeStartDownsample = 3, FadeEndDownsample = 5 };

            Assert.AreEqual(0, settings.Weight(6));
            Assert.AreEqual(1, settings.Weight(5));
        }
    }

    [TestClass]
    public class SectionLatticeFieldTests
    {
        static SectionLatticeField Square(double pitch = 1000) => new(pitch, 4000,
            [(0, 0, 10, 0), (1, 0, 20, 0), (0, 1, 10, 10), (1, 1, 20, 10)]);

        [TestMethod]
        public void InsideASurroundedCell_InterpolatesBilinearly()
        {
            (double dx, double dy, bool trusted) = Square().Sample(500, 500);

            Assert.IsTrue(trusted);
            Assert.AreEqual(15, dx, 1e-9);
            Assert.AreEqual(5, dy, 1e-9);
        }

        /// <summary>
        /// A cell with only some corners published interpolates over those corners, as the service does
        /// (<c>SectionVectorField</c>, <c>NeighborResidualField</c>). The client used to skip to the weighted fallback.
        /// </summary>
        [TestMethod]
        public void ACellWithSomeCornersPublished_InterpolatesOverThoseCorners()
        {
            var field = new SectionLatticeField(1000, 4000, [(0, 0, 10, 0), (1, 0, 20, 0)]);

            // Weights 0.75 * 0.5 and 0.25 * 0.5, rescaled to sum to one
            (double dx, double dy, bool trusted) = field.Sample(250, 500);

            Assert.IsTrue(trusted);
            Assert.AreEqual(12.5, dx, 1e-9);
            Assert.AreEqual(0, dy, 1e-9);
        }

        [TestMethod]
        public void ACellWithOneCornerPublished_TakesThatCornersOffset()
        {
            var field = new SectionLatticeField(2000, 8000, [(0, 0, 8, 4)]);

            (double dx, double dy, bool trusted) = field.Sample(1000, 1000);

            Assert.IsTrue(trusted);
            Assert.AreEqual(8, dx, 1e-9);
            Assert.AreEqual(4, dy, 1e-9);
        }

        [TestMethod]
        public void FarFromEveryNode_IsNotTrustedAndOffsetsNothing()
        {
            (double dx, double dy, bool trusted) = Square().Sample(100000, 100000);

            Assert.IsFalse(trusted);
            Assert.AreEqual(0, dx);
            Assert.AreEqual(0, dy);
        }

        [TestMethod]
        public void NonPositivePitchOrKernel_UseTheServiceDefaults()
        {
            var field = new SectionLatticeField(0, 0, []);

            Assert.AreEqual(SectionLatticeField.DefaultPitchNm, field.PitchNm);
            Assert.AreEqual(SectionLatticeField.DefaultKernelRadiusNm, field.KernelRadiusNm);
        }
    }

    [TestClass]
    public class SectionShimServiceTests
    {
        const double NmPerPixel = 2.0;
        const double Pitch = 1_000_000;

        /// <summary>A lattice covering 0..1e6 nm in both axes whose offset is the same everywhere.</summary>
        static SectionLatticeField Uniform(double dxNm, double dyNm) => new(Pitch, 8000,
            [(0, 0, dxNm, dyNm), (1, 0, dxNm, dyNm), (0, 1, dxNm, dyNm), (1, 1, dxNm, dyNm)]);

        /// <summary>A lattice whose X offset grows from 0 to 100 nm across the covered span.</summary>
        static SectionLatticeField Ramp() => new(Pitch, 8000,
            [(0, 0, 0, 0), (1, 0, 100, 0), (0, 1, 0, 0), (1, 1, 100, 0)]);

        sealed class FakeSource : ISectionShimSource
        {
            public List<long> LatticeCalls { get; } = [];
            public int VersionCalls { get; private set; }
            public string Version { get; set; } = "v1";
            public SectionLatticeField Default { get; set; } = Uniform(100, -40);
            public Dictionary<long, SectionLatticeField> Lattices { get; } = [];
            public bool ThrowOnLattice { get; set; }
            public bool ThrowOnVersion { get; set; }

            /// <summary>Time the fake service takes to answer, so a test can look at the state before the answer lands.</summary>
            public TimeSpan Delay { get; set; }

            public (Uri Url, string Database, string Stos)? LastKey { get; private set; }

            public async Task<SectionLatticeField> GetLatticeAsync(Uri serviceUrl, string annotationDatabase, string stosGroup, long z,
                CancellationToken cancellationToken)
            {
                lock (LatticeCalls)
                {
                    LatticeCalls.Add(z);
                    LastKey = (serviceUrl, annotationDatabase, stosGroup);
                }

                // A real call never completes before it returns to its caller.
                await Task.Yield();
                if (ThrowOnLattice)
                    throw new InvalidOperationException("down");
                if (Delay > TimeSpan.Zero)
                    await Task.Delay(Delay, cancellationToken);
                return Lattices.TryGetValue(z, out SectionLatticeField field) ? field : Default;
            }

            public Task<string> GetVersionAsync(Uri serviceUrl, string annotationDatabase, string stosGroup, CancellationToken cancellationToken)
            {
                VersionCalls++;
                if (ThrowOnVersion)
                    throw new InvalidOperationException("down");
                return Task.FromResult(Version);
            }
        }

        static async Task WaitFor(Func<bool> condition)
        {
            for (int i = 0; i < 300 && !condition(); i++)
                await Task.Delay(10);
            Assert.IsTrue(condition(), "condition not reached");
        }

        static (SectionShimService Service, List<(double X, double Y)> Applied) Create(FakeSource source, TimeSpan? versionCheckInterval = null)
        {
            var applied = new List<(double X, double Y)>();
            var service = new SectionShimService(source, (x, y) => { lock (applied) applied.Add((x, y)); },
                versionCheckInterval: versionCheckInterval);
            service.Configure("https://corr.example/", "RC1db", NmPerPixel);
            return (service, applied);
        }

        static (double X, double Y) Last(List<(double X, double Y)> applied)
        {
            lock (applied)
                return applied[^1];
        }

        [TestMethod]
        public async Task LowMagnification_AppliesNothingAndAsksNothing()
        {
            var source = new FakeSource();
            var (service, applied) = Create(source);

            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 16);
            await Task.Delay(60);

            Assert.AreEqual(0, source.LatticeCalls.Count);
            Assert.AreEqual(0, source.VersionCalls);
            Assert.AreEqual((0.0, 0.0), Last(applied));
        }

        [TestMethod]
        public async Task DownloadsTheSectionLattice_ThenFadesByDownsampleWithoutAskingAgain()
        {
            var source = new FakeSource();
            var (service, applied) = Create(source);

            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 6);
            await WaitFor(() => Last(applied) != (0.0, 0.0));

            CollectionAssert.AreEqual(new long[] { 10 }, source.LatticeCalls);
            Assert.AreEqual(1, source.VersionCalls);
            Assert.AreEqual("RC1db", source.LastKey!.Value.Database);
            Assert.AreEqual("grid", source.LastKey.Value.Stos);
            // 100 nm / 2 nm per pixel = 50 volume units, at half weight (DS 6 is midway through the fade).
            Assert.AreEqual((25.0, -10.0), Last(applied));

            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            Assert.AreEqual((50.0, -20.0), Last(applied));
            Assert.AreEqual(1, source.LatticeCalls.Count, "a cached lattice must not be downloaded again");
        }

        [TestMethod]
        public async Task PanningInsideACachedSection_IsSampledLocallyWithoutTheNetwork()
        {
            var source = new FakeSource { Default = Ramp() };
            var (service, applied) = Create(source);

            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            await WaitFor(() => Last(applied) != (0.0, 0.0));
            // 10 000 nm across a 1 000 000 nm span of a 100 nm ramp = 1 nm = 0.5 volume units.
            Assert.AreEqual(0.5, Last(applied).X, 1e-6);

            service.SceneChanged("grid", 10, 250000, 6000, 400, 300, downsample: 4);

            Assert.AreEqual(25.0, Last(applied).X, 1e-6, "the new position is sampled at once");
            Assert.AreEqual(1, source.LatticeCalls.Count);
        }

        [TestMethod]
        public async Task SwitchedOff_AsksNothingAndClearsTheShim_ThenResumesWhenSwitchedOn()
        {
            var source = new FakeSource();
            var applied = new List<(double X, double Y)>();
            var settings = new SectionShimSettings();
            var service = new SectionShimService(source, (x, y) => { lock (applied) applied.Add((x, y)); }, settings);
            service.Configure("https://corr.example/", "RC1db", NmPerPixel);
            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            await WaitFor(() => Last(applied) != (0.0, 0.0));

            settings.Enabled = false;
            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            Assert.AreEqual((0.0, 0.0), Last(applied));
            service.SceneChanged("grid", 11, 5000, 6000, 400, 300, downsample: 4);
            await Task.Delay(60);
            Assert.AreEqual(1, source.LatticeCalls.Count, "no download while switched off");
            Assert.IsTrue(service.IsAvailable, "the service is still present, only switched off");

            settings.Enabled = true;
            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            Assert.AreEqual((50.0, -20.0), Last(applied), "the cached lattice returns without a new download");
        }

        [TestMethod]
        public async Task SectionChange_DropsShimUntilThatSectionIsDownloaded()
        {
            var source = new FakeSource();
            var (service, applied) = Create(source);

            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            await WaitFor(() => Last(applied) != (0.0, 0.0));

            source.Lattices[11] = Uniform(20, 0);
            source.Delay = TimeSpan.FromMilliseconds(100);
            service.SceneChanged("grid", 11, 5000, 6000, 400, 300, downsample: 4);
            Assert.AreEqual((0.0, 0.0), Last(applied));
            await WaitFor(() => Last(applied) == (10.0, 0.0));
        }

        [TestMethod]
        public async Task Neighbours_AreDownloadedAfterTheSectionOnScreen_AndSteppingToThemIsInstant()
        {
            var source = new FakeSource();
            source.Lattices[11] = Uniform(20, 0);
            var (service, applied) = Create(source);

            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4, neighborZs: [9, 11, 8, 12]);
            await WaitFor(() => source.LatticeCalls.Count == 5);

            Assert.AreEqual(10, source.LatticeCalls[0], "the section on screen is first");
            CollectionAssert.AreEquivalent(new long[] { 8, 9, 10, 11, 12 }, source.LatticeCalls);

            int before = applied.Count;
            service.SceneChanged("grid", 11, 5000, 6000, 400, 300, downsample: 4, neighborZs: [10, 12, 9, 13]);

            Assert.AreEqual((10.0, 0.0), Last(applied));
            Assert.IsFalse(applied.Skip(before).Contains((0.0, 0.0)), "a prefetched section never drops the shim to zero");
        }

        [TestMethod]
        public async Task SteppingUp_DownloadsTheSectionsAboveBeforeTheOnesBehind()
        {
            var source = new FakeSource();
            var (service, applied) = Create(source);
            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            await WaitFor(() => Last(applied) != (0.0, 0.0));

            service.SceneChanged("grid", 11, 5000, 6000, 400, 300, downsample: 4, neighborZs: [10, 12, 9, 13]);
            await WaitFor(() => source.LatticeCalls.Count >= 5);

            // 10 was held; 11 itself, then the upward neighbours 12 and 13, then 9 behind.
            CollectionAssert.AreEqual(new long[] { 10, 11, 12, 13, 9 }, source.LatticeCalls);
        }

        [TestMethod]
        public async Task ChangedServiceVersion_DropsCachedLatticesAndDownloadsAgain()
        {
            var source = new FakeSource();
            var (service, applied) = Create(source, versionCheckInterval: TimeSpan.Zero);
            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            await WaitFor(() => Last(applied) == (50.0, -20.0));

            source.Version = "v2";
            source.Lattices[10] = Uniform(200, 0);
            service.SceneChanged("grid", 10, 5001, 6000, 400, 300, downsample: 4);

            await WaitFor(() => Last(applied) == (100.0, 0.0));
            Assert.AreEqual(2, source.LatticeCalls.Count);
        }

        [TestMethod]
        public async Task UnchangedServiceVersion_KeepsTheCache()
        {
            var source = new FakeSource();
            var (service, applied) = Create(source, versionCheckInterval: TimeSpan.Zero);
            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            await WaitFor(() => Last(applied) == (50.0, -20.0));

            service.SceneChanged("grid", 10, 5001, 6000, 400, 300, downsample: 4);
            await WaitFor(() => source.VersionCalls == 2);

            Assert.AreEqual(1, source.LatticeCalls.Count);
            Assert.AreEqual((50.0, -20.0), Last(applied));
        }

        [TestMethod]
        public async Task NoCorrectionForASection_IsRememberedNotRetried()
        {
            var source = new FakeSource { Default = null };
            var (service, applied) = Create(source);

            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            await WaitFor(() => source.LatticeCalls.Count == 1);
            await Task.Delay(30);
            service.SceneChanged("grid", 10, 5001, 6001, 400, 300, downsample: 4);
            await Task.Delay(60);

            Assert.AreEqual(1, source.LatticeCalls.Count);
            Assert.AreEqual((0.0, 0.0), Last(applied));
        }

        [TestMethod]
        public async Task DownloadFailure_BacksOffInsteadOfRetryingEveryMove()
        {
            var source = new FakeSource { ThrowOnLattice = true };
            var (service, _) = Create(source);

            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            await WaitFor(() => source.LatticeCalls.Count == 1);
            await Task.Delay(30);
            service.SceneChanged("grid", 10, 90000, 6000, 400, 300, downsample: 4);
            await Task.Delay(60);

            Assert.AreEqual(1, source.LatticeCalls.Count);
        }

        [TestMethod]
        public async Task VersionFailure_DownloadsNothingAndBacksOff()
        {
            var source = new FakeSource { ThrowOnVersion = true };
            var (service, _) = Create(source);

            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            await WaitFor(() => source.VersionCalls == 1);
            await Task.Delay(30);
            service.SceneChanged("grid", 10, 90000, 6000, 400, 300, downsample: 4);
            await Task.Delay(60);

            Assert.AreEqual(1, source.VersionCalls);
            Assert.AreEqual(0, source.LatticeCalls.Count);
        }

        [TestMethod]
        public void NoServiceOrNoStosGroup_NeverAsks()
        {
            var source = new FakeSource();
            var (service, applied) = Create(source);
            service.Configure(null, null, NmPerPixel);

            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            Assert.IsFalse(service.IsAvailable);
            Assert.AreEqual(0, source.VersionCalls);
            Assert.AreEqual((0.0, 0.0), Last(applied));

            service.Configure("https://corr.example/", "RC1db", NmPerPixel);
            service.SceneChanged("", 10, 5000, 6000, 400, 300, downsample: 4);
            Assert.AreEqual(0, source.VersionCalls);
        }
    }

    [TestClass]
    public class GrpcSectionShimSourceTests
    {
        sealed class StubInvoker(Func<object, object> respond) : CallInvoker
        {
            public List<object> Requests { get; } = [];

            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
                Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
            {
                Requests.Add(request);
                try
                {
                    TResponse response = (TResponse)respond(request);
                    return new AsyncUnaryCall<TResponse>(Task.FromResult(response),
                        Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
                }
                catch (RpcException ex)
                {
                    return new AsyncUnaryCall<TResponse>(Task.FromException<TResponse>(ex),
                        Task.FromResult(new Metadata()), () => ex.Status, () => new Metadata(), () => { });
                }
            }

            public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request) => throw new NotSupportedException();
            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request) => throw new NotSupportedException();
            public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options) => throw new NotSupportedException();
            public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options) => throw new NotSupportedException();
        }

        static readonly Uri Url = new("https://c/");

        static ListCorrectionSetsResponse Sets(DateTime builtUtc, double kernel) => new()
        {
            Sets =
            {
                new CorrectionSet
                {
                    Id = new CorrectionSetId { VolumeName = "RC1db", StosGroup = "grid" },
                    BuiltUtc = Timestamp.FromDateTime(DateTime.SpecifyKind(builtUtc, DateTimeKind.Utc)),
                    KernelRadiusNm = kernel
                }
            }
        };

        static SectionCorrection SectionOf(long z)
        {
            var message = new SectionCorrection { PitchNm = 1000 };
            foreach ((int gx, int gy, double dx) in new[] { (0, 0, 10.0), (1, 0, 20.0), (0, 1, 10.0), (1, 1, 20.0) })
                message.Nodes.Add(new LatticeNode { Node = new GridCell { Col = gx, Row = gy }, Offset = new Vector2 { X = dx } });
            return message;
        }

        [TestMethod]
        public async Task Lattice_CarriesPitchNodesAndTheSetsKernelRadius_AndKeysTheSetByDatabaseAndStosGroup()
        {
            var invoker = new StubInvoker(request => request switch
            {
                ListCorrectionSetsRequest => Sets(new DateTime(2026, 1, 1), 1500),
                Empty => new RebuildStatus { ServiceVersion = "1.0" },
                GetSectionCorrectionRequest r => SectionOf(r.Z),
                _ => throw new NotSupportedException()
            });
            var source = new GrpcSectionShimSource(_ => invoker);
            await source.GetVersionAsync(Url, "RC1db", "grid", CancellationToken.None);

            SectionLatticeField field = await source.GetLatticeAsync(Url, "RC1db", "grid", 7, CancellationToken.None);

            var request = invoker.Requests.OfType<GetSectionCorrectionRequest>().Single();
            Assert.AreEqual("RC1db", request.Set.VolumeName);
            Assert.AreEqual("grid", request.Set.StosGroup);
            Assert.AreEqual(7, request.Z);
            Assert.AreEqual(1000, field.PitchNm);
            Assert.AreEqual(1500, field.KernelRadiusNm);
            Assert.AreEqual(4, field.NodeCount);
            Assert.AreEqual(15, field.Sample(500, 500).Dx, 1e-9);
        }

        [TestMethod]
        public async Task SectionWithoutAMap_YieldsNull()
        {
            var source = new GrpcSectionShimSource(_ => new StubInvoker(_ => throw new RpcException(new Status(StatusCode.NotFound, "no map"))));

            Assert.IsNull(await source.GetLatticeAsync(Url, "RC1db", "grid", 7, CancellationToken.None));
            Assert.IsNull(await source.GetVersionAsync(Url, "RC1db", "grid", CancellationToken.None));
        }

        [TestMethod]
        public async Task Version_ChangesWithTheBuildTimeAndWithTheServiceVersion()
        {
            DateTime built = new(2026, 1, 1);
            string serviceVersion = "1.0";
            var invoker = new StubInvoker(request => request switch
            {
                ListCorrectionSetsRequest => Sets(built, 8000),
                Empty => new RebuildStatus { ServiceVersion = serviceVersion },
                _ => throw new NotSupportedException()
            });
            var source = new GrpcSectionShimSource(_ => invoker);

            string first = await source.GetVersionAsync(Url, "RC1db", "grid", CancellationToken.None);
            Assert.AreEqual(first, await source.GetVersionAsync(Url, "RC1db", "grid", CancellationToken.None));

            built = built.AddDays(7);
            string afterRebuild = await source.GetVersionAsync(Url, "RC1db", "grid", CancellationToken.None);
            serviceVersion = "1.1";
            string afterDeploy = await source.GetVersionAsync(Url, "RC1db", "grid", CancellationToken.None);

            Assert.AreNotEqual(first, afterRebuild);
            Assert.AreNotEqual(afterRebuild, afterDeploy);
        }
    }
}
