using Geometry;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Viking.GrpcSectionCorrectionService;
using Viking.SectionCorrection;
using Viking.SectionCorrectionServiceTypes.gRPC.V1.Protos;
using Path = System.IO.Path;

namespace GrpcSectionCorrectionService.Tests
{
    [TestClass]
    public class CorrectPointsSamplingTests
    {
        static List<LatticeNode> FourCorners(double dx = 12, double dy = -8) =>
        [
            new(0, 0, dx, dy, 3),
            new(1, 0, dx, dy, 3),
            new(0, 1, dx, dy, 3),
            new(1, 1, dx, dy, 3)
        ];

        static CorrectionProvenanceDto Provenance() => new()
        {
            BuiltUtc = DateTime.UtcNow,
            AnnotationWatermark = DateTime.UtcNow,
            PitchNm = 2000,
            KernelRadiusNm = 8000,
            MinAnnotationVotes = 3
        };

        [TestMethod]
        public async Task CorrectPoints_MatchesNeighborResidualFieldSample()
        {
            const string volume = "RC1";
            const string stos = "SliceToVolume";
            string root = Path.Combine(Path.GetTempPath(), "grpc-corr-" + Guid.NewGuid().ToString("N"));
            string dest = Path.Combine(root, volume, stos);
            Directory.CreateDirectory(dest);
            try
            {
                List<LatticeNode> nodes = FourCorners();
                new PublishedCorrectionSet(
                    new CorrectionManifestDto { StosGroup = stos, Provenance = Provenance() },
                    new Dictionary<long, SectionVectorField>
                    {
                        [5] = new SectionVectorField(5, 2000, 8000, nodes)
                    }).WriteDirectory(dest);

                using VolumeCorrectionCatalog catalog = new(root);
                catalog.Load();
                SectionCorrectionsService service = new(
                    catalog,
                    new StaticIdentityVolumeSource([]),
                    new AnnotationConnectionResolver(new Dictionary<string, string>(), null),
                    new ConfigurationBuilder().Build(),
                    new RebuildStatusStore(),
                    NullLogger<SectionCorrectionsService>.Instance);

                AnnotationVizLib.NeighborResidualField neighbor = AnnotationVizLib.NeighborResidualField.FromLattice(
                    nodes.Select(n => (5, n.Gx, n.Gy, n.Offset, n.VoteCount)),
                    2000,
                    8000);

                Vector2[] queries =
                [
                    new(1000, 1000),
                    new(0, 0),
                    new(2500, 500),
                    new(1e6, 1e6)
                ];

                CorrectPointsRequest request = new()
                {
                    VolumeName = volume,
                    StosGroup = stos
                };
                SectionPoints section = new() { Z = 5 };
                foreach (Vector2 q in queries)
                    section.Points.Add(new VolumeXY { X = q.X, Y = q.Y });
                request.Sections.Add(section);

                CorrectPointsResponse response = await service.CorrectPoints(request, null);
                Assert.AreEqual(volume, response.VolumeName);
                Assert.AreEqual(1, response.Sections.Count);
                Assert.IsTrue(response.Sections[0].FoundSection);
                Assert.AreEqual(queries.Length, response.Sections[0].Points.Count);

                for (int i = 0; i < queries.Length; i++)
                {
                    (Vector2 expected, bool trusted) = neighbor.SampleAlways(queries[i], 5);
                    CorrectedXY actual = response.Sections[0].Points[i];
                    Assert.AreEqual(trusted, actual.Trusted, $"trusted mismatch at {queries[i]}");
                    Assert.AreEqual(expected.X, actual.Dx, 1e-6, $"dx mismatch at {queries[i]}");
                    Assert.AreEqual(expected.Y, actual.Dy, 1e-6, $"dy mismatch at {queries[i]}");
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task CorrectPoints_UnknownVolume_NotFound()
        {
            using VolumeCorrectionCatalog catalog = new(Path.Combine(Path.GetTempPath(), "empty-corr-" + Guid.NewGuid().ToString("N")));
            catalog.Load();
            SectionCorrectionsService service = new(
                catalog,
                new StaticIdentityVolumeSource([]),
                new AnnotationConnectionResolver(new Dictionary<string, string>(), null),
                new ConfigurationBuilder().Build(),
                new RebuildStatusStore(),
                NullLogger<SectionCorrectionsService>.Instance);

            try
            {
                await service.CorrectPoints(new CorrectPointsRequest
                {
                    VolumeName = "missing",
                    StosGroup = "SliceToVolume",
                    Sections = { new SectionPoints { Z = 1, Points = { new VolumeXY { X = 0, Y = 0 } } } }
                }, null);
                Assert.Fail("expected RpcException");
            }
            catch (RpcException ex)
            {
                Assert.AreEqual(StatusCode.NotFound, ex.StatusCode);
            }
        }
    }
}
