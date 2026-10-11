using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.SectionCorrectionServiceTypes.gRPC.V1.Protos;

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
    public class SectionShimServiceTests
    {
        const double NmPerPixel = 2.0;

        sealed class FakeSource : ISectionShimSource
        {
            public List<(Uri Url, string Database, string Stos, long Z, IReadOnlyList<(double X, double Y)> Points)> Calls { get; } = [];
            public ShimSample? Result { get; set; } = new ShimSample(100, -40);
            public bool Throw { get; set; }

            public Task<ShimSample?> GetShimAsync(Uri serviceUrl, string annotationDatabase, string stosGroup, long z,
                IReadOnlyList<(double X, double Y)> pointsNm, CancellationToken cancellationToken)
            {
                lock (Calls)
                    Calls.Add((serviceUrl, annotationDatabase, stosGroup, z, pointsNm));
                if (Throw)
                    throw new InvalidOperationException("down");
                return Task.FromResult(Result);
            }
        }

        static async Task WaitFor(Func<bool> condition)
        {
            for (int i = 0; i < 200 && !condition(); i++)
                await Task.Delay(10);
            Assert.IsTrue(condition(), "condition not reached");
        }

        static (SectionShimService Service, List<(double X, double Y)> Applied) Create(FakeSource source)
        {
            var applied = new List<(double X, double Y)>();
            var service = new SectionShimService(source, (x, y) => { lock (applied) applied.Add((x, y)); },
                debounce: TimeSpan.FromMilliseconds(5));
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

            Assert.AreEqual(0, source.Calls.Count);
            Assert.AreEqual((0.0, 0.0), Last(applied));
        }

        [TestMethod]
        public async Task SendsCentreAndCornersInNanometres_ThenFadesByDownsample()
        {
            var source = new FakeSource();
            var (service, applied) = Create(source);

            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 6);
            await WaitFor(() => Last(applied) != (0.0, 0.0));

            Assert.AreEqual(1, source.Calls.Count);
            var call = source.Calls[0];
            Assert.AreEqual("RC1db", call.Database);
            Assert.AreEqual("grid", call.Stos);
            Assert.AreEqual(10, call.Z);
            Assert.AreEqual(5, call.Points.Count);
            Assert.AreEqual((10000.0, 12000.0), call.Points[0]);
            CollectionAssert.AreEquivalent(
                new[] { (9200.0, 11400.0), (9200.0, 12600.0), (10800.0, 11400.0), (10800.0, 12600.0) },
                call.Points.Skip(1).ToArray());
            // 100 nm / 2 nm per pixel = 50 volume units, at half weight (DS 6 is midway through the fade).
            Assert.AreEqual((25.0, -10.0), Last(applied));

            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            Assert.AreEqual((50.0, -20.0), Last(applied));
            Assert.AreEqual(1, source.Calls.Count, "a cached view must not be requested again");
        }

        [TestMethod]
        public async Task SwitchedOff_AsksNothingAndClearsTheShim_ThenResumesWhenSwitchedOn()
        {
            var source = new FakeSource();
            var applied = new List<(double X, double Y)>();
            var settings = new SectionShimSettings();
            var service = new SectionShimService(source, (x, y) => { lock (applied) applied.Add((x, y)); }, settings,
                debounce: TimeSpan.FromMilliseconds(5));
            service.Configure("https://corr.example/", "RC1db", NmPerPixel);
            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            await WaitFor(() => Last(applied) != (0.0, 0.0));

            settings.Enabled = false;
            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            Assert.AreEqual((0.0, 0.0), Last(applied));
            service.SceneChanged("grid", 11, 5000, 6000, 400, 300, downsample: 4);
            await Task.Delay(60);
            Assert.AreEqual(1, source.Calls.Count, "no request while switched off");
            Assert.IsTrue(service.IsAvailable, "the service is still present, only switched off");

            settings.Enabled = true;
            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            Assert.AreEqual((50.0, -20.0), Last(applied), "the cached answer returns without a new request");
        }

        [TestMethod]
        public async Task SectionChange_DropsShimUntilThatSectionIsAnswered()
        {
            var source = new FakeSource();
            var (service, applied) = Create(source);
            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            await WaitFor(() => Last(applied) != (0.0, 0.0));

            source.Result = new ShimSample(20, 0);
            service.SceneChanged("grid", 11, 5000, 6000, 400, 300, downsample: 4);
            Assert.AreEqual((0.0, 0.0), Last(applied));
            await WaitFor(() => Last(applied) == (10.0, 0.0));
        }

        [TestMethod]
        public async Task NoUsableCorrection_IsRememberedNotRetried()
        {
            var source = new FakeSource { Result = null };
            var (service, applied) = Create(source);

            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            await WaitFor(() => source.Calls.Count == 1);
            await Task.Delay(30);
            service.SceneChanged("grid", 10, 5001, 6001, 400, 300, downsample: 4);
            await Task.Delay(60);

            Assert.AreEqual(1, source.Calls.Count);
            Assert.AreEqual((0.0, 0.0), Last(applied));
        }

        [TestMethod]
        public async Task RequestFailure_BacksOffInsteadOfRetryingEveryMove()
        {
            var source = new FakeSource { Throw = true };
            var (service, _) = Create(source);

            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            await WaitFor(() => source.Calls.Count == 1);
            await Task.Delay(30);
            service.SceneChanged("grid", 10, 90000, 6000, 400, 300, downsample: 4);
            await Task.Delay(60);

            Assert.AreEqual(1, source.Calls.Count);
        }

        [TestMethod]
        public void NoServiceOrNoStosGroup_NeverAsks()
        {
            var source = new FakeSource();
            var (service, applied) = Create(source);
            service.Configure(null, null, NmPerPixel);

            service.SceneChanged("grid", 10, 5000, 6000, 400, 300, downsample: 4);
            Assert.IsFalse(service.IsAvailable);
            Assert.AreEqual(0, source.Calls.Count);
            Assert.AreEqual((0.0, 0.0), Last(applied));

            service.Configure("https://corr.example/", "RC1db", NmPerPixel);
            service.SceneChanged("", 10, 5000, 6000, 400, 300, downsample: 4);
            Assert.AreEqual(0, source.Calls.Count);
        }
    }

    [TestClass]
    public class GrpcSectionShimSourceTests
    {
        sealed class StubInvoker(Func<CorrectPointsRequest, CorrectPointsResponse> respond) : CallInvoker
        {
            public CorrectPointsRequest Request { get; private set; }

            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
                Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
            {
                Request = (CorrectPointsRequest)(object)request;
                CorrectPointsResponse response;
                try
                {
                    response = respond(Request);
                }
                catch (RpcException ex)
                {
                    return new AsyncUnaryCall<TResponse>(Task.FromException<TResponse>(ex),
                        Task.FromResult(new Metadata()), () => ex.Status, () => new Metadata(), () => { });
                }

                return new AsyncUnaryCall<TResponse>(Task.FromResult((TResponse)(object)response),
                    Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
            }

            public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request) => throw new NotSupportedException();
            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request) => throw new NotSupportedException();
            public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options) => throw new NotSupportedException();
            public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options) => throw new NotSupportedException();
        }

        static readonly (double X, double Y)[] Points = [(0, 0), (10, 10)];

        [TestMethod]
        public async Task AveragesOnlyTrustedSamples_AndKeysTheSetByDatabaseAndStosGroup()
        {
            var invoker = new StubInvoker(req =>
            {
                var section = new CorrectedSection { Z = 7, FoundSection = true };
                section.Points.Add(new CorrectedXY { Dx = 10, Dy = 4, Trusted = true });
                section.Points.Add(new CorrectedXY { Dx = 30, Dy = 8, Trusted = true });
                section.Points.Add(new CorrectedXY { Dx = 9000, Dy = 9000, Trusted = false });
                var response = new CorrectPointsResponse();
                response.Sections.Add(section);
                return response;
            });
            var source = new GrpcSectionShimSource(_ => invoker);

            ShimSample? sample = await source.GetShimAsync(new Uri("https://c/"), "RC1db", "grid", 7, Points, CancellationToken.None);

            Assert.AreEqual(20, sample!.Value.DxNm, 1e-9);
            Assert.AreEqual(6, sample.Value.DyNm, 1e-9);
            Assert.AreEqual("RC1db", invoker.Request.VolumeName);
            Assert.AreEqual("grid", invoker.Request.StosGroup);
            Assert.AreEqual(2, invoker.Request.Sections[0].Points.Count);
        }

        [TestMethod]
        public async Task UnknownSetOrNoTrustedSupport_YieldsNoShim()
        {
            var notFound = new GrpcSectionShimSource(_ => new StubInvoker(_ => throw new RpcException(new Status(StatusCode.NotFound, "no set"))));
            Assert.IsNull(await notFound.GetShimAsync(new Uri("https://c/"), "RC1db", "grid", 7, Points, CancellationToken.None));

            var untrusted = new GrpcSectionShimSource(_ => new StubInvoker(_ =>
            {
                var section = new CorrectedSection { Z = 7, FoundSection = true };
                section.Points.Add(new CorrectedXY { Dx = 5, Dy = 5, Trusted = false });
                var response = new CorrectPointsResponse();
                response.Sections.Add(section);
                return response;
            }));
            Assert.IsNull(await untrusted.GetShimAsync(new Uri("https://c/"), "RC1db", "grid", 7, Points, CancellationToken.None));
        }
    }
}
