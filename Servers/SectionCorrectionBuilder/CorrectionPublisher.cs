using AnnotationVizLib;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.DataModel.Annotation;
using Viking.SectionCorrection;
using Viking.VolumeModel;

namespace Viking.SectionCorrectionBuilder
{
    /// <summary>
    /// Options for one volume publish. OutputDirectory is the parent of {StosGroup}/manifest.json
    /// (CLI: user --output; gRPC host: {CorrectionsRoot}/{VolumeName}).
    /// </summary>
    public sealed class CorrectionPublishOptions
    {
        public string OutputDirectory { get; set; }
        public string CachePath { get; set; }
        public bool Force { get; set; }
        public bool SkipQuiver { get; set; }
        public int MinLocations { get; set; } = 3;
        public double DisplayPitchNm { get; set; } = 1000.0;
        public string PublicVolumeUrl { get; set; }
        public string StosGroups { get; set; }
        public Action<string> Log { get; set; }
    }

    public sealed class CorrectionPublishResult
    {
        public int ExitCode { get; init; }
        public bool Skipped { get; init; }
        public int SectionsPublished { get; init; }
        public string Destination { get; init; }
    }

    /// <summary>
    /// Builds residual fields from annotation SQL + VikingXML and atomically publishes
    /// {output}/{StosGroup}/manifest.json. Called by the CLI and the gRPC rebuild host.
    /// </summary>
    public static class CorrectionPublisher
    {
        public static async Task<int> PublishVolumeAsync(
            string annotationConnection,
            string volumeUrl,
            CorrectionPublishOptions options,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(annotationConnection);
            ArgumentException.ThrowIfNullOrWhiteSpace(volumeUrl);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputDirectory);

            var dbOptions = new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(annotationConnection, sql => sql.UseNetTopologySuite())
                .Options;

            await using AnnotationContext db = new(dbOptions);
            UnitsAndScale.Scale scale = await SqlMorphologyLoader.LoadScaleAsync(db).ConfigureAwait(false);

            return await PublishCoreAsync(
                options,
                volumeUrl,
                scale,
                () => SqlMorphologyLoader.LoadWatermarkAsync(db),
                (volume, group) => SqlMorphologyLoader.LoadAsync(db, volume, scale, group, options.MinLocations),
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Same as <see cref="PublishVolumeAsync"/>, reading the annotations through the database's OData endpoint
        /// instead of SQL. Used when the correction service has no SQL connection for the database.
        /// </summary>
        /// <param name="odataEndpoint">Annotation OData root, e.g. <c>https://host/RC1/OData/</c>.</param>
        /// <param name="volumeUrl">VikingXML the stos transforms are loaded from.</param>
        /// <param name="options">Publish options.</param>
        /// <param name="cancellationToken">Cancels loading and publishing.</param>
        /// <returns>0 on success.</returns>
        public static async Task<int> PublishVolumeFromODataAsync(
            Uri odataEndpoint,
            string volumeUrl,
            CorrectionPublishOptions options,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(odataEndpoint);
            ArgumentException.ThrowIfNullOrWhiteSpace(volumeUrl);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputDirectory);

            ODataMorphologyLoader loader = new(odataEndpoint);
            UnitsAndScale.Scale scale = await loader.LoadScaleAsync(cancellationToken).ConfigureAwait(false);

            return await PublishCoreAsync(
                options,
                volumeUrl,
                scale,
                () => loader.LoadWatermarkAsync(cancellationToken),
                async (volume, group) =>
                {
                    (List<SqlMorphologyLoader.CandidateRow> candidates, List<SqlMorphologyLoader.LinkRow> links) =
                        await loader.LoadRowsAsync(options.MinLocations, cancellationToken).ConfigureAwait(false);
                    return SqlMorphologyLoader.FromRows(candidates, links, volume, scale, group);
                },
                cancellationToken).ConfigureAwait(false);
        }

        static async Task<int> PublishCoreAsync(
            CorrectionPublishOptions options,
            string volumeUrl,
            UnitsAndScale.Scale scale,
            Func<Task<DateTime>> loadWatermark,
            Func<Volume, string, Task<SqlMorphologyLoader.LoadResult>> loadMorphology,
            CancellationToken cancellationToken)
        {
            Log(options, $"Scale XY={scale.X.Value} {scale.X.Units}  Z={scale.Z.Value} {scale.Z.Units}");

            string cache = options.CachePath ?? Path.Combine(Path.GetTempPath(), "SectionCorrectionBuilder");
            Directory.CreateDirectory(cache);
            Log(options, $"Loading volume {volumeUrl}");
            Volume volume = await Volume.CreateAsync(volumeUrl, cache, null, cancellationToken).ConfigureAwait(false);
            await volume.Initialize(cancellationToken).ConfigureAwait(false);

            List<string> groups = ParseGroups(options.StosGroups, volume);
            if (string.IsNullOrWhiteSpace(options.StosGroups) &&
                (string.IsNullOrWhiteSpace(volume.DefaultVolumeTransform) || volume.DefaultVolumeTransform == "None"))
            {
                Log(options, $"{volume.Name}: mosaic-only; publishing section corrections through the identity transform.");
            }

            DateTime watermark = await loadWatermark().ConfigureAwait(false);
            Log(options, $"Annotation watermark {watermark:u}");

            SqlMorphologyLoader.LoadResult morphology = await loadMorphology(volume, groups[0]).ConfigureAwait(false);

            foreach (string group in groups)
            {
                CorrectionPublishResult result = await PublishGroupAsync(
                    options, volume, scale, group, watermark, morphology).ConfigureAwait(false);
                if (result.ExitCode != 0)
                    return result.ExitCode;
            }

            return 0;
        }

        internal static async Task<CorrectionPublishResult> PublishGroupAsync(
            CorrectionPublishOptions options,
            Volume volume,
            UnitsAndScale.IScale scale,
            string stosGroup,
            DateTime watermark,
            SqlMorphologyLoader.LoadResult firstLoad)
        {
            string dest = Path.Combine(options.OutputDirectory, stosGroup);
            string existingManifest = Path.Combine(dest, PublishedCorrectionSet.ManifestFileName);
            if (!options.Force && File.Exists(existingManifest))
            {
                try
                {
                    PublishedCorrectionSet existing = PublishedCorrectionSet.LoadDirectory(dest);
                    if (CorrectionPublishGate.ShouldSkip(existing.Provenance?.AnnotationWatermark, watermark, force: false))
                    {
                        if (options.SkipQuiver)
                        {
                            Log(options, $"{stosGroup}: watermark unchanged ({watermark:u}); skipping. Use --force to rebuild.");
                            return new CorrectionPublishResult { ExitCode = 0, Skipped = true, Destination = dest };
                        }

                        int written = QuiverRenderer.WriteMissingQuivers(
                            Path.Combine(dest, PublishedCorrectionSet.PreviewFolderName),
                            existing.Sections.Values,
                            options.DisplayPitchNm,
                            SectionBoundsNm(volume, scale, stosGroup, existing.Sections.Keys));
                        Log(options, written == 0
                            ? $"{stosGroup}: watermark unchanged ({watermark:u}); previews present. Use --force to rebuild."
                            : $"{stosGroup}: watermark unchanged ({watermark:u}); wrote {written} missing previews.");
                        return new CorrectionPublishResult { ExitCode = 0, Skipped = true, Destination = dest };
                    }
                }
                catch (Exception ex)
                {
                    Log(options, $"{stosGroup}: could not read existing manifest ({ex.Message}); rebuilding.");
                }
            }

            Log(options, $"{stosGroup}: mapping mosaic → volume and collecting residuals");
            SqlMorphologyLoader.LoadResult load = firstLoad.StosGroup == stosGroup
                ? firstLoad
                : await SqlMorphologyLoader.RemapAsync(firstLoad, volume, scale, stosGroup).ConfigureAwait(false);

            MorphologyGraph.ResidualWindowOptions window = MorphologyGraph.ResidualWindowOptions.Default;
            List<NeighborResidualField.ResidualSample> residuals =
                NeighborResidualField.CollectResiduals(load.Cells, window);
            Log(options, $"{stosGroup}: {residuals.Count} residuals from {load.Cells.Count} structures");

            NeighborResidualField field = NeighborResidualField.Build(residuals);
            foreach (string line in field.DescribeSections())
                Log(options, $"  {line}");

            PublishedCorrectionSet published = FromResidualField(
                stosGroup, watermark, field, window, options.PublicVolumeUrl ?? "");
            Dictionary<long, SectionVectorField> sections = published.Sections as Dictionary<long, SectionVectorField>
                ?? new Dictionary<long, SectionVectorField>(published.Sections);

            string staging = dest + ".staging";
            string previous = dest + ".old";
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);
            published.WriteDirectory(staging);

            if (!options.SkipQuiver)
            {
                string preview = Path.Combine(staging, PublishedCorrectionSet.PreviewFolderName);
                Directory.CreateDirectory(preview);
                Log(options, $"{stosGroup}: writing quiver previews");
                QuiverRenderer.WriteSectionQuivers(preview, sections.Values, options.DisplayPitchNm, SectionBoundsNm(volume, scale, stosGroup, sections.Keys));
            }

            if (Directory.Exists(previous))
                Directory.Delete(previous, true);
            if (Directory.Exists(dest))
                Directory.Move(dest, previous);
            Directory.Move(staging, dest);
            if (Directory.Exists(previous))
            {
                try
                {
                    Directory.Delete(previous, true);
                }
                catch (IOException)
                {
                    Log(options, $"{stosGroup}: left previous publish at {previous}");
                }
            }

            Log(options, $"{stosGroup}: published {sections.Count} sections to {dest}");
            return new CorrectionPublishResult
            {
                ExitCode = 0,
                Skipped = false,
                SectionsPublished = sections.Count,
                Destination = dest
            };
        }

        /// <summary>
        /// Builds a publishable set from an in-memory residual field (builder and sampling tests).
        /// </summary>
        public static PublishedCorrectionSet FromResidualField(
            string stosGroup,
            DateTime watermark,
            NeighborResidualField field,
            MorphologyGraph.ResidualWindowOptions window,
            string volumeUrl)
        {
            Dictionary<long, SectionVectorField> sections = [];
            foreach (IGrouping<int, (int Z, int Gx, int Gy, Geometry.Vector2 Offset, int Votes)> group in
                     field.EnumerateNodes().GroupBy(n => n.Z))
            {
                List<LatticeNode> nodes = [.. group.Select(n => new LatticeNode(n.Gx, n.Gy, n.Offset.X, n.Offset.Y, n.Votes))];
                sections[group.Key] = new SectionVectorField(group.Key, field.PitchNm, field.PublishedKernelRadiusNm, nodes);
            }

            var manifest = new CorrectionManifestDto
            {
                StosGroup = stosGroup,
                VolumeUrl = volumeUrl ?? "",
                Provenance = new CorrectionProvenanceDto
                {
                    BuiltUtc = DateTime.UtcNow,
                    AnnotationWatermark = watermark,
                    PitchNm = field.PitchNm,
                    KernelRadiusNm = field.PublishedKernelRadiusNm,
                    MinAnnotationVotes = NeighborResidualField.MinAnnotationVotes,
                    ResidualWindow = new ResidualWindowDto
                    {
                        MinBoth = window.MinBoth,
                        MaxBoth = window.MaxBoth,
                        MinOne = window.MinOne,
                        MaxOne = window.MaxOne
                    }
                }
            };

            return new PublishedCorrectionSet(manifest, sections);
        }

        /// <summary>
        /// Stos groups to publish. Mosaic-only volumes (no DefaultVolumeTransform, or "None"),
        /// such as NM, publish under "None" so VolumeTransformProvider uses an identity map.
        /// </summary>
        public static List<string> ParseGroups(string raw, Volume volume)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                string def = volume.DefaultVolumeTransform;
                if (string.IsNullOrWhiteSpace(def) || def == "None")
                    return ["None"];
                return [def];
            }

            return [.. raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        }

        static Dictionary<long, Geometry.Rectangle> SectionBoundsNm(
            Volume volume,
            UnitsAndScale.IScale scale,
            string stosGroup,
            IEnumerable<long> sectionZ)
        {
            VolumeTransformProvider provider = new(volume, stosGroup);
            Dictionary<long, Geometry.Rectangle> bounds = [];
            foreach (long z in sectionZ)
            {
                Geometry.Rectangle? volumeBounds = provider.GetSectionToVolumeTransform((int)z).VolumeBounds;
                if (volumeBounds is not Geometry.Rectangle box || box.Width <= 0 || box.Height <= 0)
                    continue;

                bounds[z] = new Geometry.Rectangle(
                    box.Left * scale.X.Value,
                    box.Right * scale.X.Value,
                    box.Bottom * scale.Y.Value,
                    box.Top * scale.Y.Value);
            }

            return bounds;
        }

        static void Log(CorrectionPublishOptions options, string message) =>
            (options.Log ?? Console.WriteLine)(message);
    }
}
