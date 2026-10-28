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
    /// Pins <see cref="GrpcPublishedCorrectionLoader"/> against a fake invoker: StosGroup resolution,
    /// multi-section download, and CorrectPoints request shaping. No network.
    /// </summary>
    [TestClass]
    public class GrpcPublishedCorrectionLoaderTests
    {
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

        static readonly Uri Url = new("https://corrections.example/");

        static SectionCorrection Section(long z, double pitch, params (int Gx, int Gy, double Dx, double Dy, int Votes)[] nodes)
        {
            var message = new SectionCorrection { PitchNm = pitch };
            foreach (var n in nodes)
                message.Nodes.Add(new LatticeNode { Node = new GridCell { Col = n.Gx, Row = n.Gy }, Offset = new Vector2 { X = n.Dx, Y = n.Dy }, Votes = n.Votes });
            return message;
        }

        [TestMethod]
        public void MatchVolumeName_PrefersExactName_ThenODataPathSegment()
        {
            var volumes = new[]
            {
                new VolumeInfo
                {
                    VolumeName = "RC1_SQL",
                    AnnotationEndpoint = "http://websvc.codepharm.net/RC1/Annotation"
                },
                new VolumeInfo { VolumeName = "RPC1" }
            };

            Assert.AreEqual("RPC1", GrpcPublishedCorrectionLoader.MatchVolumeName(volumes, "rpc1", null));
            Assert.AreEqual(
                "RC1_SQL",
                GrpcPublishedCorrectionLoader.MatchVolumeName(
                    volumes, null, new Uri("http://websvc.codepharm.net/RC1/OData")));
            Assert.AreEqual(
                "RPC1",
                GrpcPublishedCorrectionLoader.MatchVolumeName(
                    volumes, null, new Uri("http://websvc.codepharm.net/RPC1/OData")));
        }

        [TestMethod]
        public async Task ResolveVolumeName_UsesListVolumesAgainstODataUri()
        {
            var invoker = new FakeInvoker(request => request switch
            {
                Empty => new ListVolumesResponse
                {
                    Volumes =
                    {
                        new VolumeInfo
                        {
                            VolumeName = "RabbitRC1",
                            AnnotationEndpoint = "http://websvc.codepharm.net/RC1/Annotation"
                        }
                    }
                },
                _ => throw new NotSupportedException()
            });
            var loader = new GrpcPublishedCorrectionLoader(_ => invoker);

            string name = await loader.ResolveVolumeNameAsync(
                Url, preferredName: null, annotationOrODataUri: new Uri("http://websvc.codepharm.net/RC1/OData"));

            Assert.AreEqual("RabbitRC1", name);
        }

        [TestMethod]
        public async Task Load_ResolvesFirstStosGroupAndDownloadsEverySetZ()
        {
            var invoker = new FakeInvoker(request => request switch
            {
                ListCorrectionSetsRequest => new ListCorrectionSetsResponse
                {
                    Sets =
                    {
                        Set("RC1", "zebra", 2000, 8000, 99),
                        Set("RC1", "alpha", 2000, 8000, 10, 11),
                        Set("Other", "nope", 2000, 8000, 98)
                    }
                },
                GetSectionCorrectionRequest r when r.Z == 10 => Section(10, 2000, (0, 0, 1, 2, 3)),
                GetSectionCorrectionRequest r when r.Z == 11 => Section(11, 2000, (1, 1, 4, 5, 6)),
                _ => throw new NotSupportedException(request.GetType().Name)
            });
            var loader = new GrpcPublishedCorrectionLoader(_ => invoker);

            DownloadedCorrectionSet set = await loader.LoadAsync(Url, "RC1", stosGroup: null, cancellationToken: CancellationToken.None);

            Assert.AreEqual("alpha", set.StosGroup);
            Assert.AreEqual(2, set.Sections.Count);
            Assert.AreEqual(2, set.EnumerateLatticeNodes().Count());
            // Cell centres isolate each sparse node under the inverse-distance fallback.
            Assert.AreEqual(1, set.Sections[10].Sample(0.5 * 2000, 0.5 * 2000).Dx, 1e-9);
            Assert.AreEqual(5, set.Sections[11].Sample(1.5 * 2000, 1.5 * 2000).Dy, 1e-9);
            Assert.IsTrue(invoker.Requests.OfType<GetSectionCorrectionRequest>().All(r => r.Set.StosGroup == "alpha"));
        }

        [TestMethod]
        public async Task Load_SkipsNotFoundSectionsWithoutFailingTheSet()
        {
            var invoker = new FakeInvoker(request => request switch
            {
                ListCorrectionSetsRequest => new ListCorrectionSetsResponse { Sets = { Set("RC1", "grid", 1000, 4000, 1, 2) } },
                GetSectionCorrectionRequest r when r.Z == 1 => Section(1, 1000, (0, 0, 9, 0, 1)),
                GetSectionCorrectionRequest => throw new RpcException(new Status(StatusCode.NotFound, "no map")),
                _ => throw new NotSupportedException()
            });
            var loader = new GrpcPublishedCorrectionLoader(_ => invoker);

            DownloadedCorrectionSet set = await loader.LoadAsync(Url, "RC1", "grid", cancellationToken: CancellationToken.None);

            Assert.AreEqual(1, set.Sections.Count);
            Assert.IsTrue(set.Sections.ContainsKey(1));
            Assert.IsFalse(set.Sections.ContainsKey(2));
        }

        [TestMethod]
        public async Task CorrectPoints_ForwardsSetAndSectionXY()
        {
            CorrectPointsResponse canned = new()
            {
                Sections =
                {
                    new SectionOffsets
                    {
                        Found = true,
                        Points = { new PointOffset { Offset = new Vector2 { X = 1, Y = 2 }, Trusted = true } }
                    }
                }
            };
            var invoker = new FakeInvoker(request => request switch
            {
                CorrectPointsRequest => canned,
                _ => throw new NotSupportedException()
            });
            var loader = new GrpcPublishedCorrectionLoader(_ => invoker);

            CorrectPointsResponse response = await loader.CorrectPointsAsync(
                Url,
                "RC1",
                "grid",
                [(5L, new List<(double, double)> { (10, 20) })],
                CancellationToken.None);

            CorrectPointsRequest sent = invoker.Requests.OfType<CorrectPointsRequest>().Single();
            Assert.AreEqual("RC1", sent.Set.VolumeName);
            Assert.AreEqual("grid", sent.Set.StosGroup);
            Assert.AreEqual(5, sent.Sections[0].Z);
            Assert.AreEqual(10, sent.Sections[0].Points[0].X);
            Assert.AreEqual(1, response.Sections[0].Points[0].Offset.X);
        }

        static CorrectionSet Set(string volume, string stos, double pitch, double kernel, params long[] z)
        {
            var set = new CorrectionSet
            {
                Id = new CorrectionSetId { VolumeName = volume, StosGroup = stos },
                PitchNm = pitch,
                KernelRadiusNm = kernel
            };
            set.Z.AddRange(z);
            return set;
        }
    }
}
