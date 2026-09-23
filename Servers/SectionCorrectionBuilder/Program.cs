using CommandLine;
using System;
using System.Threading;
using System.Threading.Tasks;

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

            return await CorrectionPublisher.PublishVolumeAsync(
                connection,
                options.VolumeUrl,
                new CorrectionPublishOptions
                {
                    OutputDirectory = options.OutputDirectory,
                    CachePath = options.CachePath,
                    Force = options.Force,
                    SkipQuiver = options.SkipQuiver,
                    MinLocations = options.MinLocations,
                    DisplayPitchNm = options.DisplayPitchNm,
                    PublicVolumeUrl = options.PublicVolumeUrl,
                    StosGroups = options.StosGroups,
                    Log = Console.WriteLine
                },
                CancellationToken.None).ConfigureAwait(false);
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
