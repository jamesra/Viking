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

        public Action<string> Log { get; set; }

        /// <summary>Joint registration solve settings; null uses <see cref="JointRegistrationOptions"/> defaults.</summary>
        public JointRegistrationOptions Joint { get; set; }
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
    /// <remarks>
    /// Per-section fingerprints in the manifest say whether anything a group was built from has changed. The joint
    /// registration fit couples every section, so any change rebuilds the whole group; an unchanged group is left alone.
    /// </remarks>
    public static class CorrectionPublisher
    {
        /// <summary>
        /// Version of the build algorithm stamped into manifests. Bump it whenever a change to the fit or pooling code
        /// would alter published values, so existing sets are rebuilt.
        /// </summary>
        /// <remarks>
        /// 2: process triples no longer use a branch node as A, B or C (JointRegistrationSolver.IsBranchTriple).
        /// 3: damaged sections (the blocks' NonStosSectionNumbers) correct no other section; each gets its own shift
        /// fitted against the corrected sections either side.
        /// </remarks>
        public const int BuilderVersion = 3;

        /// <summary>Provenance method of sets built by <see cref="JointRegistrationSolver"/>.</summary>
        const string JointMethod = "joint-second-difference-v1";

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
                new SqlSectionFingerprintSource(db),
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
            ISectionFingerprintSource source,
            Func<Volume, string, Task<SqlMorphologyLoader.LoadResult>> loadMorphology,
            CancellationToken cancellationToken)
        {
            Log(options, $"Scale XY={scale.X.Value} {scale.X.Units}  Z={scale.Z.Value} {scale.Z.Units}");

            string cache = options.CachePath ?? Path.Combine(Path.GetTempPath(), "SectionCorrectionBuilder");
            Directory.CreateDirectory(cache);
            Log(options, $"Loading volume {volumeUrl}");
            Volume volume = await Volume.CreateAsync(volumeUrl, cache, null, cancellationToken).ConfigureAwait(false);
            await volume.Initialize(cancellationToken).ConfigureAwait(false);

            // Null when the volume files cannot be read: then every section is treated alike, as before builder 3.
            SortedSet<int> damaged = await DamagedSectionList.LoadAsync(volumeUrl, msg => Log(options, msg), cancellationToken,
                volumeName: volume.Name).ConfigureAwait(false);

            List<string> groups = ParseGroups(options.StosGroups, volume);
            if (string.IsNullOrWhiteSpace(options.StosGroups) &&
                (string.IsNullOrWhiteSpace(volume.DefaultVolumeTransform) || volume.DefaultVolumeTransform == "None"))
            {
                Log(options, $"{volume.Name}: mosaic-only; publishing section corrections through the identity transform.");
            }

            // Loaded on the first group that needs a rebuild, then remapped for the others.
            SqlMorphologyLoader.LoadResult firstLoad = null;
            async Task<SqlMorphologyLoader.LoadResult> LoadAsync(string group)
            {
                firstLoad ??= await loadMorphology(volume, groups[0]).ConfigureAwait(false);
                return firstLoad.StosGroup == group
                    ? firstLoad
                    : await SqlMorphologyLoader.RemapAsync(firstLoad, volume, scale, group).ConfigureAwait(false);
            }

            foreach (string group in groups)
            {
                CorrectionPublishResult result = await PublishGroupAsync(
                    options, volume, scale, group, source, () => LoadAsync(group), cancellationToken, damaged).ConfigureAwait(false);
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
            ISectionFingerprintSource source,
            Func<Task<SqlMorphologyLoader.LoadResult>> loadMorphology,
            CancellationToken cancellationToken,
            IReadOnlyCollection<int> damagedSections = null)
        {
            string dest = Path.Combine(options.OutputDirectory, stosGroup);
            int minLocations = options.MinLocations < 1 ? 3 : options.MinLocations;
            JointRegistrationOptions joint = (options.Joint ?? new JointRegistrationOptions()).Clone();
            joint.DamagedSections = damagedSections ?? [];

            // Read before any row, so an edit made while this build runs is seen as a change by the next one.
            Dictionary<long, SectionFingerprintDto> current = StosFingerprint.Apply(
                await source.LoadFingerprintsAsync(cancellationToken).ConfigureAwait(false), volume, stosGroup);
            DateTime watermark = SectionFingerprintDiff.Newest(current.Values);
            Log(options, $"{stosGroup}: {current.Count} sections with annotations, newest edit {watermark:u}");

            string reason;
            if (options.Force)
            {
                reason = "forced";
            }
            else if (!File.Exists(Path.Combine(dest, PublishedCorrectionSet.ManifestFileName)))
            {
                reason = "nothing published yet";
            }
            else
            {
                try
                {
                    CorrectionManifestDto manifest = PublishedCorrectionSet.LoadManifest(dest);
                    reason = RebuildReason(manifest, minLocations, joint, damagedSections);
                    if (reason is null)
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

                        reason = $"{dirty.Count} sections changed ({SectionList(dirty)})";
                    }
                }
                catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidDataException)
                {
                    reason = $"could not read existing manifest ({ex.Message})";
                }
            }

            Log(options, $"{stosGroup}: rebuilding ({reason})");
            return await PublishAsync(options, volume, scale, stosGroup, dest, current, watermark, minLocations, loadMorphology, joint,
                    damagedSections)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Why the published set must be rebuilt even if no annotation changed, or null when its fingerprints decide.
        /// Anything that changes what the build computes from the same rows invalidates the set.
        /// </summary>
        /// <param name="damagedSections">The volume's damaged sections now; null when they could not be read, which keeps
        /// the published set rather than rebuilding it without them.</param>
        internal static string RebuildReason(CorrectionManifestDto manifest, int minLocations, JointRegistrationOptions joint,
            IReadOnlyCollection<int> damagedSections = null)
        {
            if (manifest.BuilderVersion != BuilderVersion)
                return $"builder version {manifest.BuilderVersion} -> {BuilderVersion}";
            if (manifest.SectionFingerprints is null)
                return "published set has no section fingerprints";

            CorrectionProvenanceDto p = manifest.Provenance;
            if (p is null)
                return "published set has no provenance";
            // Sets built by the leave-one-out residual field are always rebuilt.
            if (p.Method != JointMethod)
                return "existing set predates the joint registration fit";
            if (p.MinLocations != minLocations)
                return $"min locations {p.MinLocations} -> {minLocations}";
            if (p.PitchNm != joint.PitchNm || p.KernelRadiusNm != joint.KernelRadiusNm)
                return "lattice parameters changed";
            if (damagedSections is not null && !new HashSet<int>(damagedSections).SetEquals(p.DamagedSections ?? []))
                return "damaged sections changed";

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

        static async Task<CorrectionPublishResult> PublishAsync(
            CorrectionPublishOptions options,
            Volume volume,
            UnitsAndScale.IScale scale,
            string stosGroup,
            string dest,
            Dictionary<long, SectionFingerprintDto> fingerprints,
            DateTime watermark,
            int minLocations,
            Func<Task<SqlMorphologyLoader.LoadResult>> loadMorphology,
            JointRegistrationOptions jointOptions,
            IReadOnlyCollection<int> damagedSections)
        {
            Log(options, $"{stosGroup}: mapping mosaic → volume and solving the joint registration field");
            SqlMorphologyLoader.LoadResult load = await loadMorphology().ConfigureAwait(false);

            // Joint fit over every section (JointRegistrationSolver). The former per-section leave-one-out residual
            // field measured each section against uncorrected neighbours and made held-out structures less smooth.
            MorphologyGraph.ResidualWindowOptions window = MorphologyGraph.ResidualWindowOptions.Default;
            JointRegistrationResult joint = JointRegistrationSolver.Solve(load.Cells, jointOptions, msg => Log(options, $"{stosGroup}: {msg}"));
            NeighborResidualField field = joint.Field;
            Log(options, $"{stosGroup}: {load.Cells.Count} structures, {joint.Triples} process triples, spatial {joint.ChosenSpatial} ridge {joint.ChosenRidge}, " +
                         $"held-out second difference {joint.HeldOutRawMedianNm:F1} -> {joint.HeldOutCorrectedMedianNm:F1} nm" +
                         (joint.Identity ? " (identity published)" : ""));
            foreach (string line in field.DescribeSections())
                Log(options, $"  {line}");

            PublishedCorrectionSet published = FromResidualField(
                stosGroup, watermark, field, window, options.PublicVolumeUrl ?? "", joint, jointOptions, minLocations);
            published.Manifest.SectionFingerprints = fingerprints;
            published.Manifest.Provenance.DamagedSections = damagedSections is null ? null : [.. damagedSections.OrderBy(z => z)];
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
            JointRegistrationResult joint = null,
            JointRegistrationOptions jointOptions = null,
            int minLocations = 0)
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
                    },
                    Method = joint is null ? null : JointMethod,
                    Joint = joint is null ? null : new JointFitDto
                    {
                        Ridge = joint.ChosenRidge,
                        SpatialWeight = joint.ChosenSpatial,
                        PinSection = joint.PinSection,
                        Identity = joint.Identity,
                        Triples = joint.Triples,
                        HeldOutTriples = joint.HeldOutTriples,
                        HeldOutRawMedianNm = Measured(joint.HeldOutRawMedianNm),
                        HeldOutCorrectedMedianNm = Measured(joint.HeldOutCorrectedMedianNm),
                        DamagedTriples = joint.DamagedTriples,
                        DamagedRawMedianNm = Measured(joint.DamagedRawMedianNm),
                        DamagedCorrectedMedianNm = Measured(joint.DamagedCorrectedMedianNm)
                    }
                }
            };

            return new PublishedCorrectionSet(manifest, sections);
        }

        /// <summary>A solver score, or null when it was not measured (NaN), which JSON cannot carry.</summary>
        static double? Measured(double value) => double.IsFinite(value) ? value : null;

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
