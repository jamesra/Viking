using AnnotationVizLib;
using CommandLine;
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
    static class Program
    {
        static async Task<int> Main(string[] args)
        {
            ParserResult<CommandLineOptions> parsed = Parser.Default.ParseArguments<CommandLineOptions>(args);
            return await parsed.MapResult(RunAsync, _ => Task.FromResult(1));
        }

        static async Task<int> RunAsync(CommandLineOptions options)
        {
            string connection = options.AnnotationConnection
                ?? Environment.GetEnvironmentVariable("ANNOTATION_CONNECTION")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__AnnotationConnection");
            if (string.IsNullOrWhiteSpace(connection))
            {
                Console.Error.WriteLine("Set --connection, ANNOTATION_CONNECTION, or ConnectionStrings__AnnotationConnection.");
                return 2;
            }

            if (string.IsNullOrWhiteSpace(options.VolumeUrl))
            {
                Console.Error.WriteLine("--volume is required.");
                return 2;
            }

            if (string.IsNullOrWhiteSpace(options.OutputDirectory))
            {
                Console.Error.WriteLine("--output is required.");
                return 2;
            }

            var dbOptions = new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(connection, sql => sql.UseNetTopologySuite())
                .Options;

            await using AnnotationContext db = new(dbOptions);
            UnitsAndScale.Scale scale = await SqlMorphologyLoader.LoadScaleAsync(db).ConfigureAwait(false);
            Console.WriteLine($"Scale XY={scale.X.Value} {scale.X.Units}  Z={scale.Z.Value} {scale.Z.Units}");

            string cache = options.CachePath ?? Path.Combine(Path.GetTempPath(), "SectionCorrectionBuilder");
            Directory.CreateDirectory(cache);
            Console.WriteLine($"Loading volume {options.VolumeUrl}");
            Volume volume = await Volume.CreateAsync(options.VolumeUrl, cache, null, CancellationToken.None).ConfigureAwait(false);
            await volume.Initialize(CancellationToken.None).ConfigureAwait(false);

            List<string> groups = ParseGroups(options.StosGroups, volume);
            DateTime watermark = await SqlMorphologyLoader.LoadWatermarkAsync(db).ConfigureAwait(false);
            Console.WriteLine($"Annotation watermark {watermark:u}");

            SqlMorphologyLoader.LoadResult morphology = await SqlMorphologyLoader.LoadAsync(
                db, volume, scale, groups[0], options.MinLocations).ConfigureAwait(false);

            foreach (string group in groups)
            {
                int code = await PublishGroupAsync(options, volume, scale, group, watermark, morphology).ConfigureAwait(false);
                if (code != 0)
                    return code;
            }

            return 0;
        }

        static async Task<int> PublishGroupAsync(
            CommandLineOptions options,
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
                    if (existing.Provenance?.AnnotationWatermark >= watermark)
                    {
                        if (options.SkipQuiver)
                        {
                            Console.WriteLine($"{stosGroup}: watermark unchanged ({watermark:u}); skipping. Use --force to rebuild.");
                            return 0;
                        }

                        int written = QuiverRenderer.WriteMissingQuivers(
                            Path.Combine(dest, PublishedCorrectionSet.PreviewFolderName),
                            existing.Sections.Values,
                            options.DisplayPitchNm,
                            SectionBoundsNm(volume, scale, stosGroup, existing.Sections.Keys));
                        Console.WriteLine(written == 0
                            ? $"{stosGroup}: watermark unchanged ({watermark:u}); previews present. Use --force to rebuild."
                            : $"{stosGroup}: watermark unchanged ({watermark:u}); wrote {written} missing previews.");
                        return 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{stosGroup}: could not read existing manifest ({ex.Message}); rebuilding.");
                }
            }

            Console.WriteLine($"{stosGroup}: mapping mosaic → volume and collecting residuals");
            SqlMorphologyLoader.LoadResult load = firstLoad.StosGroup == stosGroup
                ? firstLoad
                : await SqlMorphologyLoader.RemapAsync(firstLoad, volume, scale, stosGroup).ConfigureAwait(false);

            MorphologyGraph.ResidualWindowOptions window = MorphologyGraph.ResidualWindowOptions.Default;
            List<NeighborResidualField.ResidualSample> residuals =
                NeighborResidualField.CollectResiduals(load.Cells, window);
            Console.WriteLine($"{stosGroup}: {residuals.Count} residuals from {load.Cells.Count} structures");

            NeighborResidualField field = NeighborResidualField.Build(residuals);
            foreach (string line in field.DescribeSections())
                Console.WriteLine($"  {line}");

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
                VolumeUrl = options.PublicVolumeUrl ?? "",
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

            var published = new PublishedCorrectionSet(manifest, sections);
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
                Console.WriteLine($"{stosGroup}: writing quiver previews");
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
                    Console.WriteLine($"{stosGroup}: left previous publish at {previous}");
                }
            }

            Console.WriteLine($"{stosGroup}: published {sections.Count} sections to {dest}");
            return 0;
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

        static List<string> ParseGroups(string raw, Volume volume)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                string def = volume.DefaultVolumeTransform;
                if (string.IsNullOrWhiteSpace(def) || def == "None")
                    throw new InvalidOperationException("No --stos-groups and volume has no DefaultVolumeTransform.");
                return [def];
            }

            return [.. raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        }
    }

    class CommandLineOptions
    {
        [Option("connection", HelpText = "Annotation SQL connection string (or ANNOTATION_CONNECTION).")]
        public string AnnotationConnection { get; set; }

        [Option('v', "volume", Required = true, HelpText = "VikingXML URL or path.")]
        public string VolumeUrl { get; set; }

        [Option('o', "output", Required = true, HelpText = "Directory that will contain {StosGroup}/manifest.json.")]
        public string OutputDirectory { get; set; }

        [Option("stos-groups", HelpText = "Comma-separated StosGroup names. Default is the volume default transform.")]
        public string StosGroups { get; set; }

        [Option("cache", HelpText = "Local stos/section cache directory.")]
        public string CachePath { get; set; }

        [Option("force", Default = false, HelpText = "Rebuild even when the annotation watermark has not moved.")]
        public bool Force { get; set; }

        [Option("skip-quiver", Default = false, HelpText = "Do not write preview/*.quiver.svg or preview/index.png.")]
        public bool SkipQuiver { get; set; }

        [Option("min-locations", Default = 3, HelpText = "SQL prefilter: structure must have at least this many locations.")]
        public int MinLocations { get; set; }

        [Option("display-pitch-nm", Default = 1000.0, HelpText = "Quiver sampling pitch in nm.")]
        public double DisplayPitchNm { get; set; }

        [Option("volume-url", HelpText = "Optional REST URL stored on the manifest as volume_url.")]
        public string PublicVolumeUrl { get; set; }
    }
}
