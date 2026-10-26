using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.GrpcSectionCorrectionService;
using Viking.SectionCorrection;
using Viking.SectionCorrectionServiceTypes.gRPC.V2.Protos;
using FitMode = AnnotationVizLib.CorrectionMode;
using GeoVector2 = Geometry.Vector2;
using GridCell = Viking.gRPC.Geometry.V1.GridCell;
using Vector2 = Viking.gRPC.Geometry.V1.Vector2;
using Path = System.IO.Path;

namespace GrpcSectionCorrectionService.Tests
{
    /// <summary>
    /// Protocol V2 (<see cref="SectionCorrectionsServiceV2"/>) against a published set on disk.  No SQL or network.
    /// </summary>
    [TestClass]
    public sealed class SectionCorrectionsServiceV2Tests
    {
        const string Volume = "RC1";
        const string Stos = "SliceToVolume";
        static readonly DateTime Built = new(2026, 10, 23, 22, 40, 0, DateTimeKind.Utc);

        string _root;
        VolumeCorrectionCatalog _catalog;
        List<Viking.SectionCorrection.LatticeNode> _nodes;

        [TestInitialize]
        public void Publish()
        {
            _root = Path.Combine(Path.GetTempPath(), "grpc-corr-v2-" + Guid.NewGuid().ToString("N"));
            string dest = Path.Combine(_root, Volume, Stos);
            Directory.CreateDirectory(dest);
            _nodes = [new(0, 0, 12, -8, 3), new(1, 0, 12, -8, 3), new(0, 1, 12, -8, 3), new(1, 1, 20, -4, 2)];
            new PublishedCorrectionSet(
                new CorrectionManifestDto
                {
                    StosGroup = Stos,
                    Provenance = new CorrectionProvenanceDto { BuiltUtc = Built, PitchNm = 2000, KernelRadiusNm = 8000 }
                },
                new Dictionary<long, SectionVectorField>
                {
                    [5] = new SectionVectorField(5, 2000, 8000, _nodes),
                    [6] = new SectionVectorField(6, 2000, 8000, _nodes)
                }).WriteDirectory(dest);
            _catalog = new VolumeCorrectionCatalog(_root);
            _catalog.Load();
        }

        [TestCleanup]
        public void Remove()
        {
            _catalog.Dispose();
            Directory.Delete(_root, true);
        }

        SectionCorrectionsServiceV2 Service() => new(
            _catalog,
            new StaticIdentityVolumeSource([]),
            new AnnotationConnectionResolver(new Dictionary<string, string> { ["Mapped"] = "Server=unused" }, null),
            new ConfigurationBuilder().Build(),
            new RebuildStatusStore(),
            NullLogger<SectionCorrectionsServiceV2>.Instance);

        static CorrectionSetId Set(string volume = Volume) => new() { VolumeName = volume, StosGroup = Stos };

        [TestMethod]
        public async Task ListCorrectionSets_CarriesProvenanceAndSections()
        {
            ListCorrectionSetsResponse response = await Service().ListCorrectionSets(new ListCorrectionSetsRequest(), null);

            CorrectionSet set = response.Sets.Single();
            Assert.AreEqual(Volume, set.Id.VolumeName);
            Assert.AreEqual(Stos, set.Id.StosGroup);
            Assert.AreEqual(Built, set.BuiltUtc.ToDateTime());
            Assert.AreEqual(2000, set.PitchNm);
            Assert.AreEqual(8000, set.KernelRadiusNm);
            CollectionAssert.AreEqual(new long[] { 5, 6 }, set.Z.ToArray());
        }

        [TestMethod]
        public async Task GetSectionCorrection_NodesCarryOffsetVectors()
        {
            SectionCorrection section = await Service().GetSectionCorrection(new GetSectionCorrectionRequest { Set = Set(), Z = 5 }, null);

            Assert.AreEqual(2000, section.PitchNm);
            Assert.AreEqual(_nodes.Count, section.Nodes.Count);
            Viking.SectionCorrectionServiceTypes.gRPC.V2.Protos.LatticeNode corner = section.Nodes.Single(n => n.Node.Equals(new GridCell { Row = 1, Col = 1 }));
            Assert.AreEqual(20, corner.Offset.X);
            Assert.AreEqual(-4, corner.Offset.Y);
            Assert.AreEqual(2, corner.Votes);
        }

        [TestMethod]
        public async Task GetSectionCorrection_SectionWithoutMap_NotFound()
        {
            RpcException ex = await Assert.ThrowsExactlyAsync<RpcException>(
                () => Service().GetSectionCorrection(new GetSectionCorrectionRequest { Set = Set(), Z = 99 }, null));
            Assert.AreEqual(StatusCode.NotFound, ex.StatusCode);
        }

        /// <summary>Offsets equal the field's own sample; the response repeats neither points nor section numbers.</summary>
        [TestMethod]
        public async Task CorrectPoints_OffsetsMatchTheField_InRequestOrder()
        {
            AnnotationVizLib.NeighborResidualField field = AnnotationVizLib.NeighborResidualField.FromLattice(
                _nodes.Select(n => (5, n.Gx, n.Gy, n.Offset, n.VoteCount)), 2000, 8000);
            GeoVector2[] queries = [new(1000, 1000), new(0, 0), new(2500, 500), new(1e6, 1e6)];

            CorrectPointsRequest request = new() { Set = Set() };
            SectionPoints mapped = new() { Z = 5 };
            mapped.Points.AddRange(queries.Select(q => new Vector2 { X = q.X, Y = q.Y }));
            request.Sections.Add(mapped);
            request.Sections.Add(new SectionPoints { Z = 7, Points = { new Vector2 { X = 1000, Y = 1000 } } });

            CorrectPointsResponse response = await Service().CorrectPoints(request, null);

            Assert.AreEqual(2, response.Sections.Count);
            Assert.IsTrue(response.Sections[0].Found);
            for (int i = 0; i < queries.Length; i++)
            {
                (GeoVector2 expected, bool trusted) = field.SampleAlways(queries[i], 5);
                PointOffset actual = response.Sections[0].Points[i];
                Assert.AreEqual(expected.X, actual.Offset.X, 1e-6, $"x at {queries[i]}");
                Assert.AreEqual(expected.Y, actual.Offset.Y, 1e-6, $"y at {queries[i]}");
                Assert.AreEqual(trusted, actual.Trusted, $"trusted at {queries[i]}");
            }

            Assert.IsFalse(response.Sections[1].Found, "section 7 has no map");
            PointOffset absent = response.Sections[1].Points.Single();
            Assert.AreEqual(0, absent.Offset.X);
            Assert.AreEqual(0, absent.Offset.Y);
            Assert.IsFalse(absent.Trusted);
        }

        [TestMethod]
        public async Task CorrectPoints_UnknownSet_NotFound()
        {
            RpcException ex = await Assert.ThrowsExactlyAsync<RpcException>(() => Service().CorrectPoints(new CorrectPointsRequest
            {
                Set = Set("missing"),
                Sections = { new SectionPoints { Z = 5, Points = { new Vector2() } } }
            }, null));
            Assert.AreEqual(StatusCode.NotFound, ex.StatusCode);
        }

        [TestMethod]
        public async Task CorrectPoints_RepeatedSection_InvalidArgument()
        {
            RpcException ex = await Assert.ThrowsExactlyAsync<RpcException>(() => Service().CorrectPoints(new CorrectPointsRequest
            {
                Set = Set(),
                Sections =
                {
                    new SectionPoints { Z = 5, Points = { new Vector2() } },
                    new SectionPoints { Z = 5, Points = { new Vector2() } }
                }
            }, null));
            Assert.AreEqual(StatusCode.InvalidArgument, ex.StatusCode);
        }

        /// <summary>The zero value is All, so an unset mode corrects fully without a presence flag.</summary>
        [TestMethod]
        public void CorrectionMode_MapsEveryValue_AndZeroIsAll()
        {
            Assert.AreEqual(CorrectionMode.All, new CorrectStructuresRequest().Correction);
            Assert.AreEqual(FitMode.All, SectionCorrectionsServiceV2.ToFitMode(CorrectionMode.All));
            Assert.AreEqual(FitMode.None, SectionCorrectionsServiceV2.ToFitMode(CorrectionMode.None));
            Assert.AreEqual(FitMode.Neighbor, SectionCorrectionsServiceV2.ToFitMode(CorrectionMode.Neighbor));
            Assert.AreEqual(FitMode.CurveFit, SectionCorrectionsServiceV2.ToFitMode(CorrectionMode.CurveFit));
            Assert.AreEqual(StatusCode.InvalidArgument,
                Assert.ThrowsExactly<RpcException>(() => SectionCorrectionsServiceV2.ToFitMode((CorrectionMode)9)).StatusCode);
        }

        [TestMethod]
        public async Task GetAnswerStamp_EmptyStructureIds_InvalidArgument()
        {
            RpcException ex = await Assert.ThrowsExactlyAsync<RpcException>(
                () => Service().GetAnswerStamp(new CorrectStructuresRequest { Set = Set("Mapped") }, new CallContext()));
            Assert.AreEqual(StatusCode.InvalidArgument, ex.StatusCode);
        }

        [TestMethod]
        public async Task GetAnswerStamp_UnmappedVolume_FailedPrecondition()
        {
            RpcException ex = await Assert.ThrowsExactlyAsync<RpcException>(
                () => Service().GetAnswerStamp(new CorrectStructuresRequest { Set = Set("Unmapped"), StructureIds = { 1L } }, new CallContext()));
            Assert.AreEqual(StatusCode.FailedPrecondition, ex.StatusCode);
        }

        [TestMethod]
        public async Task GetRebuildStatus_ReportsServiceVersion()
        {
            RebuildStatus status = await Service().GetRebuildStatus(new Empty(), null);
            Assert.AreEqual(RebuildStatusStore.ServiceVersion, status.ServiceVersion);
            Assert.IsFalse(status.InProgress);
            Assert.IsNull(status.LastStartedUtc);
        }

        sealed class CallContext : ServerCallContext
        {
            protected override string MethodCore => "AnnotateSectionCorrections/GetAnswerStamp";
            protected override string HostCore => "localhost";
            protected override string PeerCore => "127.0.0.1";
            protected override DateTime DeadlineCore => DateTime.MaxValue;
            protected override Metadata RequestHeadersCore { get; } = [];
            protected override CancellationToken CancellationTokenCore => CancellationToken.None;
            protected override Metadata ResponseTrailersCore { get; } = [];
            protected override Status StatusCore { get; set; }
            protected override WriteOptions WriteOptionsCore { get; set; }
            protected override AuthContext AuthContextCore => null;

            protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions options) =>
                throw new NotSupportedException();

            protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
        }
    }
}
