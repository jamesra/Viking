using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.SectionCorrectionServiceTypes.gRPC.V2.Protos;
using GridCell = Viking.gRPC.Geometry.V1.GridCell;
using Vector2 = Viking.gRPC.Geometry.V1.Vector2;

namespace Viking.SectionCorrectionClient.Tests
{
    /// <summary>
    /// Edge cases of <see cref="GrpcSectionShimSource"/> against a fake <see cref="CallInvoker"/> (no network): the
    /// per-set kernel radius cache, null StosGroup handling, the version token format and missing provenance.
    /// </summary>
    [TestClass]
    public class GrpcSectionShimSourceEdgeTests
    {
        /// <summary>Answers each unary request through a delegate; an <see cref="RpcException"/> becomes a failed call.</summary>
        sealed class FakeInvoker(Func<object, object> respond) : CallInvoker
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

        static ListCorrectionSetsResponse SetsOf(params CorrectionSet[] sets)
        {
            var response = new ListCorrectionSetsResponse();
            response.Sets.AddRange(sets);
            return response;
        }

        static CorrectionSet SetOf(string database, string stos, double kernel = 0, DateTime? built = null) => new()
        {
            Id = new CorrectionSetId { VolumeName = database, StosGroup = stos },
            KernelRadiusNm = kernel,
            BuiltUtc = built is DateTime b ? Timestamp.FromDateTime(b) : null
        };

        static SectionCorrection SectionWithNodes(double pitch, params (int Gx, int Gy, double Dx, double Dy)[] nodes)
        {
            var message = new SectionCorrection { PitchNm = pitch };
            foreach (var n in nodes)
                message.Nodes.Add(new LatticeNode { Node = new GridCell { Row = n.Gy, Col = n.Gx }, Offset = new Vector2 { X = n.Dx, Y = n.Dy } });
            return message;
        }

        [TestMethod]
        public async Task Lattice_MapsEveryNodeCoordinateAndOffset()
        {
            var invoker = new FakeInvoker(_ => SectionWithNodes(2500, (3, -2, 12.5, -7.25), (-1, 4, 0, 99)));
            var source = new GrpcSectionShimSource(_ => invoker);

            SectionLatticeField field = await source.GetLatticeAsync(Url, "db", "g", 1, CancellationToken.None);

            Assert.AreEqual(2500, field.PitchNm);
            Assert.AreEqual(2, field.NodeCount);
            // The sparse fallback weights nodes by distance to their cell centre; with the two nodes 10000 nm apart
            // (beyond the default kernel) sampling at a centre isolates one node, so gx/gy and dx/dy cannot be swapped.
            var a = field.Sample(3.5 * 2500, -1.5 * 2500);
            Assert.AreEqual(12.5, a.Dx, 1e-9);
            Assert.AreEqual(-7.25, a.Dy, 1e-9);
            var b = field.Sample(-0.5 * 2500, 4.5 * 2500);
            Assert.AreEqual(0, b.Dx, 1e-9);
            Assert.AreEqual(99, b.Dy, 1e-9);
        }

        [TestMethod]
        public async Task Lattice_NullStosGroupIsSentAsEmpty_AndSharesTheKernelWithTheEmptyGroup()
        {
            var invoker = new FakeInvoker(request => request switch
            {
                ListCorrectionSetsRequest => SetsOf(SetOf("db", "", 3333)),
                Empty => new RebuildStatus(),
                GetSectionCorrectionRequest => SectionWithNodes(1000, (0, 0, 1, 1)),
                _ => throw new NotSupportedException()
            });
            var source = new GrpcSectionShimSource(_ => invoker);
            await source.GetVersionAsync(Url, "db", null, CancellationToken.None);

            SectionLatticeField field = await source.GetLatticeAsync(Url, "db", "", 1, CancellationToken.None);
            SectionLatticeField nullGroupField = await source.GetLatticeAsync(Url, "db", null, 1, CancellationToken.None);

            Assert.AreEqual(3333, field.KernelRadiusNm);
            Assert.AreEqual(3333, nullGroupField.KernelRadiusNm);
            Assert.IsTrue(invoker.Requests.OfType<GetSectionCorrectionRequest>().All(r => r.Set.StosGroup == string.Empty));
        }

        [TestMethod]
        public async Task Lattice_KernelRadiusIsKeptPerDatabaseAndStosGroup()
        {
            var invoker = new FakeInvoker(request => request switch
            {
                ListCorrectionSetsRequest l => l.VolumeName == "a"
                    ? SetsOf(SetOf("a", "x", 1000), SetOf("a", "y", 2000))
                    : SetsOf(SetOf("b", "x", 3000)),
                Empty => new RebuildStatus(),
                GetSectionCorrectionRequest => SectionWithNodes(1000, (0, 0, 1, 1)),
                _ => throw new NotSupportedException()
            });
            var source = new GrpcSectionShimSource(_ => invoker);
            foreach ((string db, string stos) in new[] { ("a", "x"), ("a", "y"), ("b", "x") })
                await source.GetVersionAsync(Url, db, stos, CancellationToken.None);

            Assert.AreEqual(1000, (await source.GetLatticeAsync(Url, "a", "x", 1, CancellationToken.None)).KernelRadiusNm);
            Assert.AreEqual(2000, (await source.GetLatticeAsync(Url, "a", "y", 1, CancellationToken.None)).KernelRadiusNm);
            Assert.AreEqual(3000, (await source.GetLatticeAsync(Url, "b", "x", 1, CancellationToken.None)).KernelRadiusNm);
        }

        [TestMethod]
        public async Task Lattice_BeforeAnyVersionRead_UsesTheServiceDefaultKernelRadius()
        {
            var source = new GrpcSectionShimSource(_ => new FakeInvoker(_ => SectionWithNodes(1000, (0, 0, 1, 1))));

            SectionLatticeField field = await source.GetLatticeAsync(Url, "db", "g", 1, CancellationToken.None);

            Assert.AreEqual(SectionLatticeField.DefaultKernelRadiusNm, field.KernelRadiusNm);
        }

        [TestMethod]
        public async Task NonNotFoundFailuresPropagate()
        {
            var source = new GrpcSectionShimSource(_ => new FakeInvoker(_ => throw new RpcException(new Status(StatusCode.Unavailable, "down"))));

            var ex = await Assert.ThrowsExactlyAsync<RpcException>(
                () => source.GetLatticeAsync(Url, "db", "g", 1, CancellationToken.None));
            Assert.AreEqual(StatusCode.Unavailable, ex.StatusCode);
            ex = await Assert.ThrowsExactlyAsync<RpcException>(
                () => source.GetVersionAsync(Url, "db", "g", CancellationToken.None));
            Assert.AreEqual(StatusCode.Unavailable, ex.StatusCode);
        }

        [TestMethod]
        public async Task Version_IsBuildTicksThenBarThenServiceVersion()
        {
            DateTime built = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
            var invoker = new FakeInvoker(request => request switch
            {
                ListCorrectionSetsRequest => SetsOf(SetOf("db", "g", built: built)),
                Empty => new RebuildStatus { ServiceVersion = "2.5.1" },
                _ => throw new NotSupportedException()
            });
            var source = new GrpcSectionShimSource(_ => invoker);

            string version = await source.GetVersionAsync(Url, "db", "g", CancellationToken.None);

            Assert.AreEqual(built.Ticks + "|2.5.1", version);
        }

        [TestMethod]
        public async Task Version_WithoutProvenance_HasEmptyBuildPartAndKeepsServiceVersion()
        {
            var invoker = new FakeInvoker(request => request switch
            {
                ListCorrectionSetsRequest => SetsOf(SetOf("db", "g")),
                Empty => new RebuildStatus { ServiceVersion = "9" },
                _ => throw new NotSupportedException()
            });
            var source = new GrpcSectionShimSource(_ => invoker);

            Assert.AreEqual("|9", await source.GetVersionAsync(Url, "db", "g", CancellationToken.None));
        }

        [TestMethod]
        public async Task Version_WithoutProvenance_ResetsTheKernelRadiusToTheDefault()
        {
            bool hasProvenance = true;
            var invoker = new FakeInvoker(request => request switch
            {
                ListCorrectionSetsRequest => SetsOf(SetOf("db", "g", hasProvenance ? 1234 : 0)),
                Empty => new RebuildStatus(),
                GetSectionCorrectionRequest => SectionWithNodes(1000, (0, 0, 1, 1)),
                _ => throw new NotSupportedException()
            });
            var source = new GrpcSectionShimSource(_ => invoker);
            await source.GetVersionAsync(Url, "db", "g", CancellationToken.None);
            Assert.AreEqual(1234, (await source.GetLatticeAsync(Url, "db", "g", 1, CancellationToken.None)).KernelRadiusNm);

            hasProvenance = false;
            await source.GetVersionAsync(Url, "db", "g", CancellationToken.None);

            Assert.AreEqual(SectionLatticeField.DefaultKernelRadiusNm,
                (await source.GetLatticeAsync(Url, "db", "g", 1, CancellationToken.None)).KernelRadiusNm);
        }

        [TestMethod]
        public async Task Version_NotFound_YieldsNullAndLeavesTheKernelRadiusUntouched()
        {
            bool notFound = false;
            var invoker = new FakeInvoker(request => request switch
            {
                ListCorrectionSetsRequest => notFound ? SetsOf() : SetsOf(SetOf("db", "g", 777)),
                Empty => new RebuildStatus(),
                GetSectionCorrectionRequest => SectionWithNodes(1000, (0, 0, 1, 1)),
                _ => throw new NotSupportedException()
            });
            var source = new GrpcSectionShimSource(_ => invoker);
            await source.GetVersionAsync(Url, "db", "g", CancellationToken.None);

            notFound = true;
            Assert.IsNull(await source.GetVersionAsync(Url, "db", "g", CancellationToken.None));

            Assert.AreEqual(777, (await source.GetLatticeAsync(Url, "db", "g", 1, CancellationToken.None)).KernelRadiusNm);
        }

        [TestMethod]
        public void Constructor_RejectsNullFactory()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new GrpcSectionShimSource(null));
        }
    }
}
