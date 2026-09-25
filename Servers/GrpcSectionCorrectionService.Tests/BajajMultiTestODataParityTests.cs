using AnnotationVizLib;
using Geometry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Grpc.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Viking.Common;
using Viking.GrpcSectionCorrectionService;
using Viking.SectionCorrection;
using Viking.SectionCorrectionBuilder;
using Viking.SectionCorrectionServiceTypes.gRPC.V1.Protos;
using Path = System.IO.Path;

namespace GrpcSectionCorrectionService.Tests
{
    /// <summary>
    /// BajajMultiTest load of RC1 structure 180 versus the section-correction service.
    /// Oracle A–C compare CorrectPoints. Leave-one-out neighbor correction is Oracle B (median cap, not bit equality).
    /// <see cref="CorrectStructuresMatchesBajajCenters"/> compares final centers after the published field and curve fit.
    /// </summary>
    [TestClass]
    public class BajajMultiTestODataParityTests
    {
        const long StructureId = 180;
        const double NeighborRadiusNm = 2000;
        const string VolumeName = "RC1";
        const string StosGroup = "SliceToVolume";
        const double OracleBMedianCapNm = 250;
        const int MinTrustedSamples = 10;

        static readonly Uri Rc1Endpoint = ODataEndpointCatalog.EndpointMap[Endpoint.RC1];

        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(600000)]
        public async Task OracleA_CorrectPointsMatchesGlobalODataField()
        {
            MorphologyGraph graph;
            MorphologyGraph neighbors;
            try
            {
                graph = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync(
                    [StructureId], include_children: true, Rc1Endpoint);
                neighbors = await AnnotationVizLib.OData.ODataMorphologyFactory.LoadNeighborHopSourcesAsync(
                    graph, Rc1Endpoint, NeighborRadiusNm);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TimeoutException)
            {
                Assert.Inconclusive($"RC1 OData unreachable: {ex.Message}");
                return;
            }

            List<MorphologyGraph> cells = ResidualCells(graph, neighbors);
            if (cells.Count < 2)
                Assert.Inconclusive("Need at least two cells in the OData residual corpus.");

            NeighborResidualField field = NeighborResidualField.Build(NeighborResidualField.CollectResiduals(cells));
            PublishedCorrectionSet published = CorrectionPublisher.FromResidualField(
                StosGroup, DateTime.UtcNow, field, MorphologyGraph.ResidualWindowOptions.Default, "");

            string root = Path.Combine(Path.GetTempPath(), "odata-corr-" + Guid.NewGuid().ToString("N"));
            string dest = Path.Combine(root, VolumeName, StosGroup);
            Directory.CreateDirectory(dest);
            try
            {
                published.WriteDirectory(dest);
                using VolumeCorrectionCatalog catalog = new(root);
                catalog.Load();
                SectionCorrectionsService service = CreateService(catalog);

                List<(int Z, Vector2 Xy)> queries = TargetCentroids(graph);
                Assert.IsTrue(queries.Count > 0, "structure 180 had no location centroids");

                List<IGrouping<int, (int Z, Vector2 Xy)>> grouped =
                    [.. queries.GroupBy(q => q.Z).OrderBy(g => g.Key)];
                CorrectPointsResponse response = await service.CorrectPoints(ToRequest(queries), null);
                Assert.AreEqual(grouped.Count, response.Sections.Count);
                for (int s = 0; s < grouped.Count; s++)
                {
                    List<(int Z, Vector2 Xy)> group = [.. grouped[s]];
                    CorrectedSection section = response.Sections[s];
                    Assert.AreEqual(group.Count, section.Points.Count);
                    for (int p = 0; p < group.Count; p++)
                    {
                        (int z, Vector2 xy) = group[p];
                        CorrectedXY actual = section.Points[p];
                        (Vector2 expected, bool trusted) = field.SampleAlways(xy, z);
                        Assert.AreEqual(trusted, actual.Trusted, $"trusted mismatch z={z} {xy}");
                        Assert.AreEqual(expected.X, actual.Dx, 1e-6, $"dx mismatch z={z} {xy}");
                        Assert.AreEqual(expected.Y, actual.Dy, 1e-6, $"dy mismatch z={z} {xy}");
                    }
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(600000)]
        public async Task OracleB_LeaveOneOutOffsetsStayNearGlobalField()
        {
            MorphologyGraph graph;
            MorphologyGraph neighbors;
            try
            {
                graph = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync(
                    [StructureId], include_children: true, Rc1Endpoint);
                neighbors = await AnnotationVizLib.OData.ODataMorphologyFactory.LoadNeighborHopSourcesAsync(
                    graph, Rc1Endpoint, NeighborRadiusNm);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TimeoutException)
            {
                Assert.Inconclusive($"RC1 OData unreachable: {ex.Message}");
                return;
            }

            List<MorphologyGraph> cells = ResidualCells(graph, neighbors);
            if (cells.Count < 2)
                Assert.Inconclusive("Need at least two cells in the OData residual corpus.");

            NeighborResidualField global = NeighborResidualField.Build(NeighborResidualField.CollectResiduals(cells));

            List<(ulong Id, int Z, Vector2 Before)> before = [];
            foreach (MorphologyGraph cell in ResidualCells(graph, null))
            {
                foreach (MorphologyNode node in cell.Nodes.Values)
                    before.Add((node.Key, (int)Math.Round(node.UnscaledZ), node.Center.XY()));
            }

            NeighborCorrectionResult applied = MorphologyGraph.ApplyNeighborCorrection(
                graph,
                neighbors?.Subgraphs.Values);
            if (!applied.Applied)
                Assert.Inconclusive("ApplyNeighborCorrection skipped (corpus too small).");

            List<double> magnitudes = [];
            foreach ((ulong id, int z, Vector2 xy) in before)
            {
                if (!applied.HandledLocationIds.Contains(id))
                    continue;
                MorphologyNode node = FindNode(graph, id);
                if (node is null)
                    continue;
                Vector2 loo = node.Center.XY() - xy;
                (Vector2 published, bool trusted) = global.SampleAlways(xy, z);
                if (!trusted)
                    continue;
                magnitudes.Add((loo - published).Magnitude);
            }

            Assert.IsTrue(magnitudes.Count >= MinTrustedSamples,
                $"too few trusted LOO samples ({magnitudes.Count}); need {MinTrustedSamples}");
            magnitudes.Sort();
            double median = magnitudes[magnitudes.Count / 2];
            Assert.IsTrue(median <= OracleBMedianCapNm,
                $"median |LOO − global field| {median:F1} nm exceeds {OracleBMedianCapNm} nm cap");
        }

        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(600000)]
        public async Task OracleC_PublishedCatalogRmseWhenCorrectionsRootSet()
        {
            string correctionsRoot = Environment.GetEnvironmentVariable("CORRECTIONS_ROOT");
            if (string.IsNullOrWhiteSpace(correctionsRoot) || !Directory.Exists(correctionsRoot))
            {
                Assert.Inconclusive("Set CORRECTIONS_ROOT to an RC1 published catalog to compare SQL-built fields.");
                return;
            }

            MorphologyGraph graph;
            try
            {
                graph = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync(
                    [StructureId], include_children: true, Rc1Endpoint);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TimeoutException)
            {
                Assert.Inconclusive($"RC1 OData unreachable: {ex.Message}");
                return;
            }

            using VolumeCorrectionCatalog catalog = new(correctionsRoot);
            catalog.Load();
            if (catalog.Sets.Count == 0)
                Assert.Inconclusive($"No volume folders under {correctionsRoot}");

            string volumeName = catalog.TryGet(VolumeName, StosGroup, out _)
                ? VolumeName
                : catalog.Sets.Select(s => s.VolumeName).First();
            string stos = catalog.TryGet(volumeName, StosGroup, out _)
                ? StosGroup
                : catalog.Sets.First(s => s.VolumeName == volumeName).Set.StosGroup;

            SectionCorrectionsService service = CreateService(catalog);
            List<(int Z, Vector2 Xy)> queries = TargetCentroids(graph);
            CorrectPointsResponse response = await service.CorrectPoints(ToRequest(queries, volumeName, stos), null);

            List<double> sq = [];
            foreach (CorrectedSection section in response.Sections)
            {
                foreach (CorrectedXY actual in section.Points)
                {
                    if (!actual.Trusted)
                        continue;
                    sq.Add(actual.Dx * actual.Dx + actual.Dy * actual.Dy);
                }
            }

            Assert.IsTrue(sq.Count >= MinTrustedSamples, $"too few trusted catalog samples ({sq.Count})");
            double rmse = Math.Sqrt(sq.Average());
            Console.WriteLine($"Oracle C RMSE of published offsets on structure 180 centroids: {rmse:F1} nm (n={sq.Count})");
            Assert.IsTrue(rmse < 5000, $"published RMSE {rmse:F1} nm exceeds 5000 nm sanity bound");
        }

        /// <summary>
        /// Published-field registration the way BajajMultiTest does with --corrections-dir, versus CorrectStructures.
        /// Centers are volume nm. Polygons can differ by the warp of a centroid versus the centroid of a warped outline.
        /// </summary>
        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(600000)]
        public async Task CorrectStructuresMatchesBajajCenters()
        {
            string correctionsRoot = Environment.GetEnvironmentVariable("CORRECTIONS_ROOT");
            string annotationConnection = Environment.GetEnvironmentVariable("ANNOTATION_CONNECTION");
            string vikingXmlUrl = Environment.GetEnvironmentVariable("VIKINGXML_URL");
            if (string.IsNullOrWhiteSpace(correctionsRoot) || !Directory.Exists(correctionsRoot))
            {
                Assert.Inconclusive("Set CORRECTIONS_ROOT to the published catalog the correction service serves.");
                return;
            }

            if (string.IsNullOrWhiteSpace(annotationConnection) || string.IsNullOrWhiteSpace(vikingXmlUrl))
            {
                Assert.Inconclusive("Set ANNOTATION_CONNECTION and VIKINGXML_URL for the RC1 annotation database and VikingXML endpoint.");
                return;
            }

            MorphologyGraph graph;
            try
            {
                graph = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync(
                    [StructureId], include_children: true, Rc1Endpoint);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TimeoutException)
            {
                Assert.Inconclusive($"RC1 OData unreachable: {ex.Message}");
                return;
            }

            string stosGroup = Environment.GetEnvironmentVariable("STOS_GROUP");
            if (string.IsNullOrWhiteSpace(stosGroup))
                stosGroup = StosGroup;

            using VolumeCorrectionCatalog catalog = new(correctionsRoot);
            catalog.Load();
            if (!catalog.TryGet(VolumeName, stosGroup, out PublishedCorrectionSet published))
            {
                Assert.Inconclusive($"No published set '{VolumeName}' / '{stosGroup}' under {correctionsRoot}");
                return;
            }

            double pitch = published.Provenance?.PitchNm > 0 ? published.Provenance.PitchNm : NeighborResidualField.GridSizeNm;
            double kernel = published.Provenance?.KernelRadiusNm > 0 ? published.Provenance.KernelRadiusNm : NeighborResidualField.KernelRadiusNm;
            NeighborResidualField field = NeighborResidualField.FromLattice(published.EnumerateLattice(), pitch, kernel);
            MorphologyGraph.ApplyResidualField(graph, field);
            MorphologyGraph.CurveFitProcesses(graph, MorphologyGraph.CurveFitOptions.Default);

            Dictionary<long, (long Z, Vector2 Xy)> bajaj = [];
            CollectCenters(graph, bajaj);
            Assert.IsTrue(bajaj.Count > 0, "structure 180 had no locations after Bajaj registration");

            SectionCorrectionsService service = CreateService(
                catalog,
                new StaticIdentityVolumeSource([new IdentityVolumeRow(VolumeName, vikingXmlUrl, "")]),
                new AnnotationConnectionResolver(new Dictionary<string, string> { [VolumeName] = annotationConnection }, null));
            CorrectStructuresResponse response = await service.CorrectStructures(
                new CorrectStructuresRequest
                {
                    VolumeName = VolumeName,
                    StosGroup = stosGroup,
                    StructureIds = { StructureId },
                    IncludeChildren = true
                },
                new TestCallContext());

            Dictionary<long, CorrectedLocation> serviceById = [];
            foreach (CorrectedLocation location in response.Locations)
                serviceById[location.LocationId] = location;

            List<long> missing = [.. bajaj.Keys.Where(id => !serviceById.ContainsKey(id)).OrderBy(id => id)];
            List<(long Id, long Z, double Magnitude, Vector2 Bajaj, Vector2 Service)> deltas = [];
            foreach ((long id, (long z, Vector2 xy)) in bajaj)
            {
                if (!serviceById.TryGetValue(id, out CorrectedLocation corrected))
                    continue;
                Vector2 serviceXy = new(corrected.X, corrected.Y);
                deltas.Add((id, z, (xy - serviceXy).Magnitude, xy, serviceXy));
            }

            deltas.Sort((a, b) => a.Magnitude.CompareTo(b.Magnitude));
            double median = deltas.Count == 0 ? double.NaN : deltas[deltas.Count / 2].Magnitude;
            double p95 = deltas.Count == 0 ? double.NaN : deltas[(int)Math.Ceiling(deltas.Count * 0.95) - 1].Magnitude;
            double max = deltas.Count == 0 ? double.NaN : deltas[^1].Magnitude;
            string worst = FormatWorst(deltas);

            Console.WriteLine(
                $"CorrectStructures vs Bajaj centers: bajaj={bajaj.Count} service={serviceById.Count} matched={deltas.Count} missing={missing.Count} " +
                $"median={median:F1} nm p95={p95:F1} nm max={max:F1} nm");
            if (missing.Count > 0)
                Console.WriteLine("Missing location ids: " + string.Join(", ", missing.Take(20)));
            Console.WriteLine(worst);

            string detail = $"missing={missing.Count} matched={deltas.Count} median={median:F1} nm p95={p95:F1} nm max={max:F1} nm\n{worst}";
            Assert.AreEqual(0, missing.Count, "OData locations absent from CorrectStructures. " + detail);
            Assert.IsTrue(deltas.Count > 0 && median <= OracleBMedianCapNm,
                $"median |Bajaj − CorrectStructures| {median:F1} nm exceeds {OracleBMedianCapNm} nm. {detail}");
        }

        static void CollectCenters(MorphologyGraph graph, Dictionary<long, (long Z, Vector2 Xy)> into)
        {
            if (graph is null)
                return;
            foreach (MorphologyNode node in graph.Nodes.Values)
                into[(long)node.Key] = (node.Location.UnscaledZ, node.Center.XY());
            foreach (MorphologyGraph child in graph.Subgraphs.Values)
                CollectCenters(child, into);
        }

        static string FormatWorst(List<(long Id, long Z, double Magnitude, Vector2 Bajaj, Vector2 Service)> deltas)
        {
            if (deltas.Count == 0)
                return "(no matched locations)";
            return string.Join("\n", deltas.TakeLast(10).Reverse().Select(d =>
                $"location {d.Id} z={d.Z} |d|={d.Magnitude:F1} nm bajaj=({d.Bajaj.X:F1},{d.Bajaj.Y:F1}) service=({d.Service.X:F1},{d.Service.Y:F1})"));
        }

        static SectionCorrectionsService CreateService(
            VolumeCorrectionCatalog catalog,
            IIdentityVolumeSource volumes = null,
            AnnotationConnectionResolver connections = null) =>
            new(catalog,
                volumes ?? new StaticIdentityVolumeSource([]),
                connections ?? new AnnotationConnectionResolver(new Dictionary<string, string>(), null),
                new ConfigurationBuilder().Build(),
                new RebuildStatusStore(),
                NullLogger<SectionCorrectionsService>.Instance);

        static List<MorphologyGraph> ResidualCells(MorphologyGraph root, MorphologyGraph neighbors)
        {
            List<MorphologyGraph> cells = [.. root.Subgraphs.Values.Where(sg => sg.StructureID != 0)];
            if (cells.Count == 0 && root.StructureID != 0)
                cells.Add(root);
            if (neighbors != null)
            {
                HashSet<ulong> seen = [.. cells.Select(c => c.StructureID)];
                foreach (MorphologyGraph n in neighbors.Subgraphs.Values)
                {
                    if (n.StructureID != 0 && seen.Add(n.StructureID))
                        cells.Add(n);
                }
            }

            return cells;
        }

        static List<(int Z, Vector2 Xy)> TargetCentroids(MorphologyGraph root)
        {
            List<(int Z, Vector2 Xy)> points = [];
            IEnumerable<MorphologyGraph> cells = root.Subgraphs.Values.Where(sg => sg.StructureID != 0);
            if (!cells.Any() && root.StructureID != 0)
                cells = [root];
            foreach (MorphologyGraph cell in cells)
            {
                foreach (MorphologyNode node in cell.Nodes.Values)
                    points.Add(((int)Math.Round(node.UnscaledZ), node.Center.XY()));
            }

            return points;
        }

        static CorrectPointsRequest ToRequest(
            List<(int Z, Vector2 Xy)> queries,
            string volume = VolumeName,
            string stos = StosGroup)
        {
            CorrectPointsRequest request = new() { VolumeName = volume, StosGroup = stos };
            foreach (IGrouping<int, (int Z, Vector2 Xy)> group in queries.GroupBy(q => q.Z).OrderBy(g => g.Key))
            {
                SectionPoints section = new() { Z = group.Key };
                foreach ((int _, Vector2 xy) in group)
                    section.Points.Add(new VolumeXY { X = xy.X, Y = xy.Y });
                request.Sections.Add(section);
            }

            return request;
        }

        static MorphologyNode FindNode(MorphologyGraph root, ulong id)
        {
            if (root.Nodes.TryGetValue(id, out MorphologyNode node))
                return node;
            foreach (MorphologyGraph cell in root.Subgraphs.Values)
            {
                if (cell.Nodes.TryGetValue(id, out node))
                    return node;
            }

            return null;
        }

        sealed class TestCallContext : ServerCallContext
        {
            protected override string MethodCore => "AnnotateSectionCorrections/CorrectStructures";
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
