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

        /// <summary>
        /// A change is published only once the newest annotation edit is at least this old, so an editing session is not
        /// rebuilt halfway through. Zero publishes at once. <see cref="Force"/> and a first build ignore it.
        /// </summary>
        public TimeSpan QuietPeriod { get; set; }

        /// <summary>
        /// Largest share of the published sections an incremental update may recompute before the whole group is rebuilt
        /// instead. Zero or less means the default of 0.3.
        /// </summary>
        public double MaxIncrementalFraction { get; set; } = 0.3;

        public Action<string> Log { get; set; }
    }

    public sealed class CorrectionPublishResult
    {
        public int ExitCode { get; init; }
        public bool Skipped { get; init; }
        public int SectionsPublished { get; init; }
        public string Destination { get; init; }

        /// <summary>Sections recomputed by an incremental publish; zero for a full rebuild or a skipped run.</summary>
        public int SectionsRecomputed { get; init; }

        /// <summary>True when the run rebuilt the whole group rather than only the changed sections.</summary>
        public bool Full { get; init; }
    }

    /// <summary>
    /// Builds residual fields from annotation SQL + VikingXML and atomically publishes
    /// {output}/{StosGroup}/manifest.json. Called by the CLI and the gRPC rebuild host.
    /// </summary>
    /// <remarks>
    /// A group that already has a published set built by this builder version, with the same parameters, is updated
    /// incrementally: per-section fingerprints in the manifest say which sections changed, and only those and the sections
    /// their structures' chains reach are recomputed. Anything else rebuilds the whole group.
    /// </remarks>
    public static class CorrectionPublisher
    {
        /// <summary>
        /// Version of the build algorithm stamped into manifests. Bump it whenever a change to the residual or pooling code
        /// would alter published values, so existing sets are rebuilt in full.
        /// </summary>
        public const int BuilderVersion = 1;

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
                new SqlMorphologySource(db),
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
                loader,
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
            IMorphologySource source,
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

            // Loaded on the first group that needs a full rebuild, then remapped for the others.
            SqlMorphologyLoader.LoadResult firstLoad = null;
            async Task<SqlMorphologyLoader.LoadResult> FullLoadAsync(string group)
            {
                firstLoad ??= await loadMorphology(volume, groups[0]).ConfigureAwait(false);
                return firstLoad.StosGroup == group
                    ? firstLoad
                    : await SqlMorphologyLoader.RemapAsync(firstLoad, volume, scale, group).ConfigureAwait(false);
            }

            foreach (string group in groups)
            {
                CorrectionPublishResult result = await PublishGroupAsync(
                    options, volume, scale, group, source, () => FullLoadAsync(group), cancellationToken).ConfigureAwait(false);
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
            IMorphologySource source,
            Func<Task<SqlMorphologyLoader.LoadResult>> loadFull,
            CancellationToken cancellationToken)
        {
            string dest = Path.Combine(options.OutputDirectory, stosGroup);
            int minLocations = options.MinLocations < 1 ? 3 : options.MinLocations;
            MorphologyGraph.ResidualWindowOptions window = MorphologyGraph.ResidualWindowOptions.Default;

            // Read before any row, so an edit made while this build runs is seen as a change by the next one.
            Dictionary<long, SectionFingerprintDto> current = StosFingerprint.Apply(
                await source.LoadFingerprintsAsync(cancellationToken).ConfigureAwait(false), volume, stosGroup);
            DateTime watermark = SectionFingerprintDiff.Newest(current.Values);
            Log(options, $"{stosGroup}: {current.Count} sections with annotations, newest edit {watermark:u}");

            CorrectionManifestDto manifest = null;
            string fullReason = null;
            if (options.Force)
            {
                fullReason = "forced";
            }
            else if (!File.Exists(Path.Combine(dest, PublishedCorrectionSet.ManifestFileName)))
            {
                fullReason = "nothing published yet";
            }
            else
            {
                try
                {
                    manifest = PublishedCorrectionSet.LoadManifest(dest);
                    fullReason = FullRebuildReason(manifest, minLocations, window);
                }
                catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidDataException)
                {
                    fullReason = $"could not read existing manifest ({ex.Message})";
                }
            }

            if (fullReason is null)
            {
                SortedSet<long> dirty = SectionFingerprintDiff.DirtySections(manifest.SectionFingerprints, current);
                if (dirty.Count == 0)
                    return UnchangedGroup(options, volume, scale, stosGroup, dest, watermark);

                if (options.QuietPeriod > TimeSpan.Zero && DateTime.UtcNow - watermark < options.QuietPeriod)
                {
                    Log(options, $"{stosGroup}: {dirty.Count} sections changed but the newest edit ({watermark:u}) is within the " +
                        $"{options.QuietPeriod.TotalMinutes:0} minute quiet period; waiting.");
                    return new CorrectionPublishResult { ExitCode = 0, Skipped = true, Destination = dest };
                }

                double fraction = options.MaxIncrementalFraction > 0 ? options.MaxIncrementalFraction : 0.3;
                int maxAffected = (int)Math.Max(1, Math.Ceiling(fraction * Math.Max(current.Count, manifest.Z?.Count ?? 0)));
                IncrementalResult incremental = null;
                if (dirty.Count <= maxAffected)
                {
                    Log(options, $"{stosGroup}: {dirty.Count} changed sections ({SectionList(dirty)}); updating incrementally");
                    incremental = await IncrementalCorrectionBuilder.BuildAsync(
                        source, volume, scale, stosGroup, dirty, minLocations, window, maxAffected,
                        message => Log(options, message), cancellationToken).ConfigureAwait(false);
                }

                if (incremental is not null)
                    return PublishIncremental(options, volume, scale, stosGroup, dest, manifest, incremental, current, watermark, minLocations);

                fullReason = $"{dirty.Count} changed sections are too many to update incrementally";
            }

            Log(options, $"{stosGroup}: full rebuild ({fullReason})");
            return await PublishFullAsync(
                options, volume, scale, stosGroup, dest, current, watermark, minLocations, window, loadFull).ConfigureAwait(false);
        }

        /// <summary>
        /// Why the published set cannot be updated section by section, or null when it can. Anything that changes what the
        /// build computes from the same rows invalidates every section.
        /// </summary>
        internal static string FullRebuildReason(
            CorrectionManifestDto manifest,
            int minLocations,
            MorphologyGraph.ResidualWindowOptions window)
        {
            if (manifest.BuilderVersion != BuilderVersion)
                return $"builder version {manifest.BuilderVersion} -> {BuilderVersion}";
            if (manifest.SectionFingerprints is null)
                return "published set has no section fingerprints";

            CorrectionProvenanceDto p = manifest.Provenance;
            if (p is null)
                return "published set has no provenance";
            if (p.MinLocations != minLocations)
                return $"min locations {p.MinLocations} -> {minLocations}";
            if (p.PitchNm != NeighborResidualField.GridSizeNm || p.KernelRadiusNm != NeighborResidualField.KernelRadiusNm ||
                p.MinAnnotationVotes != NeighborResidualField.MinAnnotationVotes)
                return "lattice parameters changed";

            ResidualWindowDto w = p.ResidualWindow;
            if (w is null || w.MinBoth != window.MinBoth || w.MaxBoth != window.MaxBoth ||
                w.MinOne != window.MinOne || w.MaxOne != window.MaxOne)
                return "residual window changed";

            return null;
        }

        /// <summary>Nothing changed since the last publish: keep it, and fill in previews that are missing.</summary>
        static CorrectionPublishResult UnchangedGroup(
            CorrectionPublishOptions options,
            Volume volume,
            UnitsAndScale.IScale scale,
            string stosGroup,
            string dest,
            DateTime watermark)
        {
            if (options.SkipQuiver)
            {
                Log(options, $"{stosGroup}: unchanged since the last publish ({watermark:u}); skipping. Use --force to rebuild.");
                return new CorrectionPublishResult { ExitCode = 0, Skipped = true, Destination = dest };
            }

            PublishedCorrectionSet existing = PublishedCorrectionSet.LoadDirectory(dest);
            int written = QuiverRenderer.WriteMissingQuivers(
                Path.Combine(dest, PublishedCorrectionSet.PreviewFolderName),
                existing.Sections.Values,
                options.DisplayPitchNm,
                SectionBoundsNm(volume, scale, stosGroup, existing.Sections.Keys));
            Log(options, written == 0
                ? $"{stosGroup}: unchanged since the last publish ({watermark:u}); previews present. Use --force to rebuild."
                : $"{stosGroup}: unchanged since the last publish ({watermark:u}); wrote {written} missing previews.");
            return new CorrectionPublishResult { ExitCode = 0, Skipped = true, Destination = dest };
        }

        static async Task<CorrectionPublishResult> PublishFullAsync(
            CorrectionPublishOptions options,
            Volume volume,
            UnitsAndScale.IScale scale,
            string stosGroup,
            string dest,
            Dictionary<long, SectionFingerprintDto> fingerprints,
            DateTime watermark,
            int minLocations,
            MorphologyGraph.ResidualWindowOptions window,
            Func<Task<SqlMorphologyLoader.LoadResult>> loadFull)
        {
            Log(options, $"{stosGroup}: mapping mosaic → volume and collecting residuals");
            SqlMorphologyLoader.LoadResult load = await loadFull().ConfigureAwait(false);

            List<NeighborResidualField.ResidualSample> residuals =
                NeighborResidualField.CollectResiduals(load.Cells, window);
            Log(options, $"{stosGroup}: {residuals.Count} residuals from {load.Cells.Count} structures");

            NeighborResidualField field = NeighborResidualField.Build(residuals);
            foreach (string line in field.DescribeSections())
                Log(options, $"  {line}");

            PublishedCorrectionSet published = FromResidualField(
                stosGroup, watermark, field, window, options.PublicVolumeUrl ?? "", minLocations);
            published.Manifest.SectionFingerprints = fingerprints;
            Dictionary<long, SectionVectorField> sections = published.Sections as Dictionary<long, SectionVectorField>
                ?? new Dictionary<long, SectionVectorField>(published.Sections);

            string staging = dest + ".staging";
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

            SwapIntoPlace(options, stosGroup, staging, dest);

            Log(options, $"{stosGroup}: published {sections.Count} sections to {dest}");
            return new CorrectionPublishResult
            {
                ExitCode = 0,
                Skipped = false,
                SectionsPublished = sections.Count,
                Destination = dest,
                Full = true
            };
        }

        /// <summary>
        /// Writes the recomputed sections beside copies of the unchanged ones and swaps the directory, so a reader sees
        /// either the previous publish or the new one, never a mixture.
        /// </summary>
        static CorrectionPublishResult PublishIncremental(
            CorrectionPublishOptions options,
            Volume volume,
            UnitsAndScale.IScale scale,
            string stosGroup,
            string dest,
            CorrectionManifestDto manifest,
            IncrementalResult incremental,
            Dictionary<long, SectionFingerprintDto> fingerprints,
            DateTime watermark,
            int minLocations)
        {
            string staging = dest + ".staging";
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);

            SortedSet<long> published = [];
            foreach (long z in manifest.Z ?? [])
            {
                if (incremental.Affected.Contains(z))
                    continue;
                string source = Path.Combine(dest, PublishedCorrectionSet.SectionFileName(z));
                if (!File.Exists(source))
                    continue;
                File.Copy(source, Path.Combine(staging, PublishedCorrectionSet.SectionFileName(z)));
                published.Add(z);
            }

            foreach (KeyValuePair<long, SectionVectorField> kv in incremental.Sections)
            {
                PublishedCorrectionSet.WriteSectionNpz(Path.Combine(staging, PublishedCorrectionSet.SectionFileName(kv.Key)), kv.Value);
                published.Add(kv.Key);
            }

            manifest.Z = [.. published];
            manifest.BuilderVersion = BuilderVersion;
            manifest.SectionFingerprints = fingerprints;
            manifest.VolumeUrl = options.PublicVolumeUrl ?? manifest.VolumeUrl ?? "";
            manifest.Provenance.BuiltUtc = DateTime.UtcNow;
            manifest.Provenance.AnnotationWatermark = watermark;
            manifest.Provenance.MinLocations = minLocations;
            new PublishedCorrectionSet(manifest, new Dictionary<long, SectionVectorField>()).WriteManifest(staging);

            if (!options.SkipQuiver)
            {
                string previewSource = Path.Combine(dest, PublishedCorrectionSet.PreviewFolderName);
                string preview = Path.Combine(staging, PublishedCorrectionSet.PreviewFolderName);
                Directory.CreateDirectory(preview);
                if (Directory.Exists(previewSource))
                {
                    foreach (long z in published.Where(z => !incremental.Affected.Contains(z)))
                    {
                        string svg = z.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".quiver.svg";
                        if (File.Exists(Path.Combine(previewSource, svg)))
                            File.Copy(Path.Combine(previewSource, svg), Path.Combine(preview, svg));
                    }
                }

                // The previews of recomputed sections were not copied, so this draws exactly those, and the contact sheet.
                PublishedCorrectionSet staged = PublishedCorrectionSet.LoadDirectory(staging);
                Log(options, $"{stosGroup}: writing quiver previews for {incremental.Sections.Count} recomputed sections");
                QuiverRenderer.WriteMissingQuivers(
                    preview, staged.Sections.Values, options.DisplayPitchNm,
                    SectionBoundsNm(volume, scale, stosGroup, staged.Sections.Keys));
            }

            SwapIntoPlace(options, stosGroup, staging, dest);

            Log(options, $"{stosGroup}: published {published.Count} sections to {dest}; recomputed {incremental.Affected.Count} " +
                $"({incremental.Sections.Count} with data) from {incremental.StructuresLoaded} structures");
            return new CorrectionPublishResult
            {
                ExitCode = 0,
                Skipped = false,
                SectionsPublished = published.Count,
                SectionsRecomputed = incremental.Affected.Count,
                Destination = dest,
                Full = false
            };
        }

        static void SwapIntoPlace(CorrectionPublishOptions options, string stosGroup, string staging, string dest)
        {
            string previous = dest + ".old";
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
        }

        static string SectionList(SortedSet<long> sections)
        {
            const int shown = 8;
            string head = string.Join(",", sections.Take(shown));
            return sections.Count > shown ? head + ",…" : head;
        }

        /// <summary>
        /// Builds a publishable set from an in-memory residual field (builder and sampling tests).
        /// </summary>
        public static PublishedCorrectionSet FromResidualField(
            string stosGroup,
            DateTime watermark,
            NeighborResidualField field,
            MorphologyGraph.ResidualWindowOptions window,
            string volumeUrl,
            int minLocations = 0)
        {
            var manifest = new CorrectionManifestDto
            {
                StosGroup = stosGroup,
                VolumeUrl = volumeUrl ?? "",
                BuilderVersion = BuilderVersion,
                Provenance = new CorrectionProvenanceDto
                {
                    BuiltUtc = DateTime.UtcNow,
                    AnnotationWatermark = watermark,
                    PitchNm = field.PitchNm,
                    KernelRadiusNm = field.PublishedKernelRadiusNm,
                    MinAnnotationVotes = NeighborResidualField.MinAnnotationVotes,
                    MinLocations = minLocations,
                    ResidualWindow = new ResidualWindowDto
                    {
                        MinBoth = window.MinBoth,
                        MaxBoth = window.MaxBoth,
                        MinOne = window.MinOne,
                        MaxOne = window.MaxOne
                    }
                }
            };

            return new PublishedCorrectionSet(manifest, SectionsOf(field));
        }

        /// <summary>One <see cref="SectionVectorField"/> per section that has lattice nodes in <paramref name="field"/>.</summary>
        internal static Dictionary<long, SectionVectorField> SectionsOf(NeighborResidualField field)
        {
            Dictionary<long, SectionVectorField> sections = [];
            foreach (IGrouping<int, (int Z, int Gx, int Gy, Geometry.Vector2 Offset, int Votes)> group in
                     field.EnumerateNodes().GroupBy(n => n.Z))
            {
                List<LatticeNode> nodes = [.. group.Select(n => new LatticeNode(n.Gx, n.Gy, n.Offset.X, n.Offset.Y, n.Votes))];
                sections[group.Key] = new SectionVectorField(group.Key, field.PitchNm, field.PublishedKernelRadiusNm, nodes);
            }

            return sections;
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
