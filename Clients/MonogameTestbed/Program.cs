using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MathNet.Numerics;
using MorphologyMesh;
using VikingXNAGraphics;

namespace MonogameTestbed
{
    /// <summary>
    /// Independent registration fixes run before SliceGraph.Create. Combine with OR.
    /// Default is <see cref="All"/> (everything): residual field, then outlier curvefit.
    /// </summary>
    [Flags]
    public enum CorrectionMode
    {
        None = 0,

        /// <summary>
        /// Spatial mean displacement from neighboring process curve-residuals.
        /// Corrects smoothly varying leftover registration after Stos. Does not run curvefit.
        /// </summary>
        Neighbor = 1,

        /// <summary>
        /// Leave-one-out Catmull-Rom for large outliers the field cannot represent
        /// (fold, tear, a contour far from local consensus). Later than <see cref="Neighbor"/>.
        /// </summary>
        CurveFit = 2,

        /// <summary>
        /// Every fix, in order: <see cref="Neighbor"/> then <see cref="CurveFit"/>. CLI default.
        /// </summary>
        All = Neighbor | CurveFit,

        /// <summary>Alias of <see cref="All"/>.</summary>
        Everything = All,
    }

    /// <summary>
    /// The main class.
    /// </summary>
    public static partial class Program
    {
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool AllocConsole();

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool FreeConsole();

        private static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new(-4);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetProcessDpiAwarenessContext(IntPtr value);

        /// <summary>
        /// Opts the process into true pixel coordinates.  Without this Windows virtualizes every size and position
        /// we ask about, so a 3840x2160 monitor at 150% scaling reports itself as 2560x1440 and fullscreen captures
        /// come out at that reduced size.  Must run before any window exists for the choice to take effect.
        /// </summary>
        private static void EnablePerMonitorDpiAwareness()
        {
            try
            {
                SetProcessDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
            }
            catch (EntryPointNotFoundException)
            {
                //Present since Windows 10 1703; older builds simply stay scaled.
            }
        }


        static System.IO.StreamWriter DebugLogFile = null;

        public partial class CommandLineOptions
        {
            /// <summary>
            /// The raw StructureID arguments
            /// </summary>
            [Option('s', "SIDs", Required = false, HelpText = "Structure IDs", Separator = ' ')]
            public IEnumerable<string> StructureIDParams { get; set; }

            public List<ulong> StructureIDs { get; private set; }

            /// <summary>
            /// The raw LocationID arguments
            /// </summary>
            [Option('i', "LIDs", Required = false, HelpText = "Location IDs", Separator = ' ')]
            public IEnumerable<string> LocationIDParams { get; set; }

            public List<ulong> LocationIDs { get; private set; }

            [Option('e', "Endpoint", Required = false,
                HelpText =
                    "Endpoint, either URL or one of [TEST, RC1, RC2, RC3, TEMPORALMONKEY, INFERIORMONKEY, CPED, RPC1, RPC2, RPC3]",
                Separator = ' ')]
            public string EndpointParam { get; set; }

            Uri _endpointUri;
            bool _endpointUriResolved;

            public Uri EndpointUri
            {
                get
                {
                    if (_endpointUriResolved)
                        return _endpointUri;

                    _endpointUriResolved = true;
                    if (string.IsNullOrWhiteSpace(EndpointParam))
                        return null;

                    try
                    {
                        var endpoint = EndpointParam.ToEnum<Endpoint>();
                        if (DataSource.EndpointMap.TryGetValue(endpoint, out Uri mapped))
                        {
                            _endpointUri = mapped;
                            return _endpointUri;
                        }
                    }
                    catch
                    {
                    }

                    Console.WriteLine($"Could not convert {EndpointParam} to predefined Endpoint.  Trying as URI");
                    _endpointUri = new Uri(EndpointParam);
                    return _endpointUri;
                }
            }

            [Option('b', "boundaries", Required = false, HelpText = "TypeID's defining surfaces boundaries to include in output", Separator = ' ', Default = null)]
            public IEnumerable<ulong> BoundaryIDs { get; set; }

            /// <summary>
            /// When set, child structures of each -s ID are not loaded. Synapses, gap junctions, and rafts are included by default.
            /// </summary>
            [Option("exclude-children", Default = false, HelpText = "Do not load child structures of the IDs given with -s (alias: -xc / --xc). Children are included by default.")]
            public bool ExcludeChildren { get; set; }

            [Option("xc", Default = false, Hidden = true)]
            public bool ExcludeChildrenAlias { get; set; }

            /// <summary>
            /// True unless <see cref="ExcludeChildren"/> (or --xc) was passed. Passed to OData as include_children.
            /// </summary>
            public bool IncludeChildren => !ExcludeChildren;

            /// <summary>
            /// Reflect volume Z through the XY plane in the 3D view. Camera3D uses +Z as up, so unflipped
            /// section numbers appear inverted relative to a typical stack. Exported meshes keep volume Z.
            /// Toggle at runtime in BajajMultiTest with I.
            /// </summary>
            [Option("invert-z", Default = false, HelpText = "Negate Z in the 3D view (Camera3D is Z-up). Does not change exported meshes.")]
            public bool InvertZ { get; set; }

            /// <summary>
            /// Registration correction flags before SliceGraph.Create.
            /// Values: none, neighbor, curvefit, all/everything (default). Combine as "neighbor,curvefit".
            /// </summary>
            [Option("correction", Default = "all",
                HelpText = "Registration fixes (flags): none | neighbor (residual displacement field) | curvefit (outlier pass for folds/tears) | all/everything (neighbor then curvefit). Default all.")]
            public string CorrectionParam { get; set; }

            /// <summary>
            /// Parsed <see cref="CorrectionParam"/>. Omitted / blank is <see cref="CorrectionMode.All"/> (everything).
            /// </summary>
            public CorrectionMode Correction { get; private set; } = CorrectionMode.All;

            /// <summary>
            /// XY search radius in nanometres when neighbor correction runs. Pads the loaded cells' AABB
            /// to discover neighboring structures on each occupied section.
            /// </summary>
            [Option("correction-radius", Default = 2000.0,
                HelpText = "Neighbor search radius in nm for --correction neighbor|all (default 2000).")]
            public double CorrectionRadiusNm { get; set; }

            /// <summary>
            /// Optional cap on each registration translation magnitude in nm. Omitted = no ClampProcessOffset.
            /// </summary>
            [Option("correction-max-offset-nm", Required = false,
                HelpText = "Optional max XY translation in nm for neighbor/curvefit. When omitted, translations are not magnitude-clamped.")]
            public double? CorrectionMaxOffsetNm { get; set; }

            /// <summary>
            /// Leave-one-out Catmull-Rom half-window (neighbors below and above the peeled node).
            /// </summary>
            [Option("correction-curvefit-window", Default = 7,
                HelpText = "Curvefit leave-one-out half-window size (default 7).")]
            public int CorrectionCurveFitWindow { get; set; }

            [Option("corrections-dir", HelpText = "Published correction root ({StosGroup}/manifest.json). When set, --correction neighbor loads this field instead of OData neighbors.")]
            public string CorrectionsDirectory { get; set; }

            [Option("stos-group", HelpText = "StosGroup name under --corrections-dir (default: first set in the directory).")]
            public string CorrectionStosGroup { get; set; }

            [Option("show-correction-field", Default = false, HelpText = "Draw the published residual quiver on BajajTest 2D views.")]
            public bool ShowCorrectionField { get; set; }

            /// <summary>
            /// 0 = use env <c>VIKING_ODATA_MAX_CONCURRENT</c> or factory default (4).
            /// </summary>
            [Option("odata-concurrent", Default = 0,
                HelpText = "Max concurrent OData requests (default 4, or env VIKING_ODATA_MAX_CONCURRENT). Raise only when the host can take it.")]
            public int ODataConcurrent { get; set; }

            /// <summary>
            /// Max density-gate spacing (nm/vert) for closed polygons; with hull-adaptive mode this is the
            /// nearly-convex end (ratio ≥ 0.95). Convoluted rings lerp down to 20 nm. Default 50. Set &lt;= 0 to disable.
            /// </summary>
            [Option("contour-simplify-spacing-nm", Default = 50.0,
                HelpText = "Max density-gate spacing in nm/vert for closed polygons (hull-adaptive: 50 at ratio≥0.95, 20 at ratio≤0.70). Polylines never simplified. Default 50. Set <= 0 to disable.")]
            public double ContourSimplifySpacingNm { get; set; }

            /// <summary>
            /// Max distance (nm) from the simplified closed contour to the original when density simplify runs. Default 10.
            /// </summary>
            [Option("contour-simplify-tol-nm", Default = 10.0,
                HelpText = "Max distance in nm from simplified closed contour to original when density simplify runs. Polylines are never simplified. Default 10.")]
            public double ContourSimplifyTolNm { get; set; }

            /// <summary>
            /// Density-gated closed-contour simplify for BajajMulti / SliceGraph.Create. Geometry is already in nm.
            /// Uses hull-area–adaptive spacing (convex → spacing-nm, convoluted → 20 nm).
            /// </summary>
            public ContourSimplifyOptions ContourSimplify =>
                ContourSimplifySpacingNm <= 0 || ContourSimplifyTolNm <= 0
                    ? ContourSimplifyOptions.Disabled
                    : new ContourSimplifyOptions(ContourSimplifySpacingNm, ContourSimplifyTolNm, adaptiveHullSpacing: true);

            /// <summary>
            /// The output file or path name
            /// </summary>
            [Option('o', "output", Required = false, HelpText = "Output folder name", Separator = ' ', Default = null)]
            public string OutputPath { get; set; }

            /// <summary>
            /// Quit the program upon completion
            /// </summary>
            [Option('q', "quiet", Required = false,
                HelpText = "Quit program as soon as renders are generated and saved", Separator = ' ', Default = false)]
            public bool Quiet { get; set; }

            /// <summary>
            /// Prints additional information to the console
            /// </summary>
            [Option('v', "verbose", Required = false,
                HelpText = "Print additional information to the console", Separator = ' ', Default = false)]
            public bool Verbose { get; set; }

            /// <summary>
            /// Save a log file
            /// </summary>
            [Option('l', "log", Required = false,
                HelpText = "Write a log file", Separator = ' ', Default = false)]
            public bool Log { get; set; }

            /// <summary>
            /// Off by default so the timing hooks cost nothing in a normal run.
            /// Requires Server GC (MonogameTestbed sets ServerGarbageCollection=true); aborts otherwise.
            /// </summary>
            [Option("timings", Required = false,
                HelpText = "Accumulate and print mesh generation phase timings when the run finishes (requires Server GC)", Default = false)]
            public bool Timings { get; set; }

            /// <summary>
            /// Display help
            /// </summary>
            [Option('h', "help", Required = false, HelpText = "Show help", Separator = ' ', Default = false)]
            public bool ShowHelp { get; set; }

            [Option("mode", Required = false, HelpText = "Startup test mode, e.g. BajajTest or BajajMultiTest")]
            public string ModeParam { get; set; }

            internal TestMode? StartupTestMode { get; private set; }

            [Option("screenshots", Required = false, HelpText = "Dump view PNGs under the output folder (BAJAJTEST stage views, or a single frame for other modes)", Default = false)]
            public bool Screenshots { get; set; }

            [Option("repro", Required = false, HelpText = "BAJAJTEST ReproSet index, range, comma list, or 'all'")]
            public string ReproParam { get; set; }

            public bool ReproAll { get; private set; }

            public List<int> ReproIndices { get; private set; }

            [Option("repro-locations", Required = false, HelpText = "BAJAJTEST: mesh the slice spanning these LocationIDs instead of a ReproSet index")]
            public string ReproLocationsParam { get; set; }

            public List<ulong> ReproLocations { get; private set; }

            /// <summary>
            /// Text file with one failed slice per line (LocationIDs), typically written by BajajMultiTest as
            /// <c>bajajmultitest_failed_slices.txt</c>. Each non-comment line becomes an ad-hoc BajajTest case.
            /// </summary>
            [Option("repro-locations-file", Required = false,
                HelpText = "BAJAJTEST: text file with one slice per line of LocationIDs (from BajajMultiTest failed-slice report)")]
            public string ReproLocationsFile { get; set; }

            /// <summary>Parsed slices from <see cref="ReproLocationsFile"/>; a one-ID entry is an isolated annotation.</summary>
            public List<ulong[]> ReproLocationSlicesFromFile { get; private set; }

            /// <summary>
            /// Null (the default) builds ad-hoc repro slices with the same density-gated <see cref="ContourSimplify"/>
            /// BajajMultiTest uses, so a slice from its failed-slice list reproduces with the same vertices.  A value
            /// forces the legacy always-simplify path at that tolerance.
            /// </summary>
            [Option("repro-tolerance", Required = false, HelpText = "Always simplify --repro-locations polygons at this tolerance (nm). Default: the density-gated --contour-simplify-* settings BajajMultiTest uses.")]
            public double? ReproTolerance { get; set; }

            [Option("capture-request", Required = false, HelpText = "JSON file listing extra or replacement screenshot shots")]
            public string CaptureRequestPath { get; set; }

            /// <summary>
            /// Diagnosis runs on the 2D line/chord/index views; the shaded 3D renders cost the most capture time and
            /// only matter for final verification, so they are opt-in.
            /// </summary>
            [Option("3d", Required = false, HelpText = "BAJAJTEST screenshots: also capture the shaded 3D mesh renders (final verification). Default captures only the 2D chord, index, and region views", Default = false)]
            public bool Capture3D { get; set; }

            [Option("cameras", Required = false, HelpText = "Comma-separated 3D camera presets for every 3D screenshot (top, oblique, oblique-back, side, front, below). One PNG per camera. A capture request's cameras3D takes precedence.")]
            public string CamerasParam { get; set; }

            [Option("display", Required = false, HelpText = "Monitor to capture on: an index from --list-displays, or 'primary'. Defaults to a secondary monitor when capturing so the primary display is left alone.")]
            public string DisplayParam { get; set; }

            [Option("list-displays", Required = false, HelpText = "Print the attached monitors with their indices and exit", Default = false)]
            public bool ListDisplays { get; set; }

            public CaptureRequestFile CaptureRequest { get; private set; }


            private static readonly Regex IntegerRegex = MyRegex();
            private static readonly Regex IntegerRangeRegex = new(@"^(\d+)\-(\d+)$");
            private static readonly Regex IntegerOrIntegerRangeRegex = new(@"^(\d+-\d+|\d+)$");

            /// <summary>
            /// Convert a number string, or a string of two integers separated by a hyphen, to a list of integers.
            /// </summary>
            private static List<ulong> NumberRangeToList(string input)
            {
                if (IsInteger(input))
                    return [Convert.ToUInt64(input)];

                Match m = IntegerRangeRegex.Match(input);
                if (!m.Success)
                    throw new ArgumentException($"'{input}' is not an integer or integer range");

                ulong start = Convert.ToUInt64(m.Groups[1].Value);
                ulong end = Convert.ToUInt64(m.Groups[2].Value);
                if (start > end)
                    (start, end) = (end, start);

                var listNumbers = new List<ulong>((int)(end - start) + 1);
                for (ulong val = start; val <= end; val++)
                    listNumbers.Add(val);

                return listNumbers;
            }

            private static bool IsIntegerRange(string input) => IntegerRangeRegex.IsMatch(input);

            private static bool IsInteger(string input) => IntegerRegex.IsMatch(input);

            private static bool IsIntegerOrIntegerRange(string input) => IntegerOrIntegerRangeRegex.IsMatch(input);

            private static List<ulong> InputParameterListToIDs(IEnumerable<string> input) =>
                [.. (input ?? []).SelectMany(InputParameterListToIDs)];

            private static List<ulong> InputParameterListToIDs(string input)
            {
                List<ulong> listNumbers = [];

                foreach (string chunk in input.Split([',', ';']).Select(s => s.Trim())
                             .Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    if (IsIntegerOrIntegerRange(chunk))
                    {
                        if (IsInteger(chunk))
                        {
                            listNumbers.Add(Convert.ToUInt64(chunk));
                        }
                        else if (IsIntegerRange(chunk))
                        {
                            listNumbers.AddRange(NumberRangeToList(chunk));
                        }
                        else
                        {
                            throw new ArgumentException($"Unexpected argument in ID list {chunk}");
                        }
                    }
                    else
                    {
                        listNumbers.AddRange(ParseFile(chunk));
                    }
                }

                return listNumbers;
            }

            private static List<ulong> ParseFile(string filename)
            {
                List<ulong> results = [];
                if (!File.Exists(filename))
                    throw new ArgumentException($"File argument {filename} was not found, is it in the path?");

                try
                {
                    foreach (string line in File.ReadLines(filename))
                    {
                        results.AddRange(InputParameterListToIDs(line));
                    }
                }
                catch (Exception e) when (e is not ArgumentException)
                {
                    Console.WriteLine($"Failed to parse ID file {filename}: {e.Message}");
                    throw;
                }

                return results;
            }

            /// <summary>
            /// Parse a single line from an input file with IDs
            /// </summary>
            /// <param name="input"></param>
            /// <returns></returns>
            private static List<ulong> ParseFileLine(string input)
            {
                string data = input.Split('#').First(); //Anything to the right of a # is a comment and ignored
                return InputParameterListToIDs(data);
            }

            /// <summary>
            /// Convert links to files and number ranges into sets of numbers that programs can more easily access
            /// </summary>
            internal void ProcessStrings()
            {
                this.LocationIDs = InputParameterListToIDs(LocationIDParams ?? []);
                this.StructureIDs = InputParameterListToIDs(StructureIDParams ?? []);
                ExcludeChildren |= ExcludeChildrenAlias;
                ParseCorrection();
                ParseStartupMode();
                ParseReproParam();
                ParseReproLocations();
                ParseReproLocationsFile();
                LoadCaptureRequest();
                ApplyCameraPresets();
                MorphologyMesh.MeshPhaseTimings.Enabled = Timings;
                MorphologyMesh.ChordGenStats.Enabled = Timings;
                if (Timings)
                    MorphologyMesh.ChordGenStats.Reset();
                if (Timings && !System.Runtime.GCSettings.IsServerGC)
                {
                    throw new ArgumentException(
                        "--timings requires Server GC. Build MonogameTestbed with <ServerGarbageCollection>true</ServerGarbageCollection> " +
                        "(Release x64 is the supported benchmark config). Workstation GC pauses dominate wall clock and look like " +
                        "low CPU utilization; those runs are not comparable to Server GC timings.");
                }

                if (ODataConcurrent > 0)
                    AnnotationVizLib.OData.ODataMorphologyFactory.MaxConcurrentRequests = ODataConcurrent;
            }

            private void ParseCorrection()
            {
                if (string.IsNullOrWhiteSpace(CorrectionParam))
                {
                    Correction = CorrectionMode.All;
                    return;
                }

                string raw = CorrectionParam.Trim();
                if (raw.Equals("curvefit", StringComparison.OrdinalIgnoreCase))
                {
                    Correction = CorrectionMode.CurveFit;
                    return;
                }

                if (Enum.TryParse(raw, ignoreCase: true, out CorrectionMode mode))
                {
                    Correction = mode;
                    return;
                }

                string known = "none, neighbor, curvefit, all, everything";
                throw new ArgumentException($"Unknown --correction value '{CorrectionParam}'. Expected one of: {known} (flags may be combined, e.g. neighbor,curvefit).");
            }

            private void ParseStartupMode()
            {
                if (string.IsNullOrWhiteSpace(ModeParam))
                    return;

                if (Enum.TryParse(ModeParam, ignoreCase: true, out TestMode parsed))
                {
                    StartupTestMode = parsed;
                    return;
                }

                throw new ArgumentException($"Unknown test mode '{ModeParam}'. Use a TestMode name such as BajajTest or BajajMultiTest.");
            }

            private void ParseReproParam()
            {
                if (string.IsNullOrWhiteSpace(ReproParam))
                    return;

                if (ReproParam.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    ReproAll = true;
                    return;
                }

                ReproIndices = [];
                foreach (string chunk in ReproParam.Split([',', ';']).Select(s => s.Trim()).Where(s => s.Length > 0))
                {
                    if (int.TryParse(chunk, out int index))
                    {
                        ReproIndices.Add(index);
                        continue;
                    }

                    Match range = IntegerRangeRegex.Match(chunk);
                    if (!range.Success)
                        throw new ArgumentException($"'{chunk}' is not a ReproSet index, range, or 'all'");

                    int start = int.Parse(range.Groups[1].Value);
                    int end = int.Parse(range.Groups[2].Value);
                    if (start > end)
                        (start, end) = (end, start);
                    for (int i = start; i <= end; i++)
                        ReproIndices.Add(i);
                }
            }

            private void ParseReproLocations()
            {
                if (string.IsNullOrWhiteSpace(ReproLocationsParam))
                    return;

                ReproLocations = [];
                foreach (string chunk in ReproLocationsParam.Split([',', ';', ' ', '/'], StringSplitOptions.RemoveEmptyEntries))
                {
                    if (ulong.TryParse(chunk.Trim(), out ulong id) == false)
                        throw new ArgumentException($"'{chunk}' in --repro-locations is not a LocationID");

                    ReproLocations.Add(id);
                }

                //A single LocationID is an isolated annotation (its slice holds that one contour and a cap).
                if (ReproLocations.Count < 1)
                    throw new ArgumentException("--repro-locations needs at least one LocationID");
            }

            private void ParseReproLocationsFile()
            {
                if (string.IsNullOrWhiteSpace(ReproLocationsFile))
                    return;

                if (!File.Exists(ReproLocationsFile))
                    throw new FileNotFoundException($"Repro locations file was not found: {ReproLocationsFile}");

                ReproLocationSlicesFromFile = [];
                int lineNumber = 0;
                foreach (string rawLine in File.ReadLines(ReproLocationsFile))
                {
                    lineNumber++;
                    string line = rawLine;
                    int comment = line.IndexOf('#');
                    if (comment >= 0)
                        line = line[..comment];
                    line = line.Trim();
                    if (line.Length == 0)
                        continue;

                    List<ulong> ids = [];
                    foreach (string chunk in line.Split([',', ';', ' ', '/'], StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (ulong.TryParse(chunk.Trim(), out ulong id) == false)
                            throw new ArgumentException($"'{chunk}' on line {lineNumber} of --repro-locations-file is not a LocationID");
                        ids.Add(id);
                    }

                    if (ids.Count < 1)
                        throw new ArgumentException($"Line {lineNumber} of --repro-locations-file needs at least one LocationID");

                    ReproLocationSlicesFromFile.Add([.. ids]);
                }

                if (ReproLocationSlicesFromFile.Count == 0)
                    throw new ArgumentException($"--repro-locations-file '{ReproLocationsFile}' contained no LocationID slices");
            }

            private void LoadCaptureRequest()
            {
                if (string.IsNullOrWhiteSpace(CaptureRequestPath))
                    return;

                if (!File.Exists(CaptureRequestPath))
                    throw new FileNotFoundException($"Capture request file was not found: {CaptureRequestPath}");

                string json = File.ReadAllText(CaptureRequestPath);
                CaptureRequest = System.Text.Json.JsonSerializer.Deserialize<CaptureRequestFile>(json, CaptureRequestFile.JsonOptions)
                    ?? throw new ArgumentException($"Failed to parse capture request JSON: {CaptureRequestPath}");
            }

            private void ApplyCameraPresets()
            {
                List<CaptureCameraRequest> cameras = ScreenshotCapture.ParseCameraPresets(CamerasParam);
                if (cameras is null)
                    return;

                CaptureRequest ??= new CaptureRequestFile();
                if (CaptureRequest.Cameras3D is { Count: > 0 })
                    return;

                CaptureRequest.Cameras3D = cameras;
            }

            [GeneratedRegex(@"^(\d+)$")]
            private static partial Regex MyRegex();
        }

        /// <summary>
        /// CommandLineParser only binds long names with a double dash. Map the single-dash forms the CLI help advertises.
        /// </summary>
        private static string[] NormalizeChildStructureFlags(string[] args)
        {
            string[] mapped = new string[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                mapped[i] = args[i] switch
                {
                    "-xc" => "--xc",
                    "-exclude-children" => "--exclude-children",
                    _ => args[i]
                };
            }
            return mapped;
        }

        public static CommandLineOptions options;

        static string LogPath;

        /// <summary>
        /// Process start time.  The trace log and the BajajMultiTest failed-slice report both name their files
        /// with this stamp so the two can be matched up afterwards.
        /// </summary>
        public static readonly DateTime RunStarted = DateTime.Now;

        /// <summary>Timestamp shared by every per-run output file name.</summary>
        public static string RunStamp => RunStarted.ToString("MM.dd.yyyy HH.mm.ss");

        static readonly string LogFile = RunStamp + ".log";

        static string LogFullPath => System.IO.Path.Combine(LogPath, LogFile);

        static TextWriter SynchronizedLogWriter = null;
        static TextWriterTraceListener LogListener = null;
        static ConsoleTraceListener ConsoleListener = null;
        static ILoggerFactory LoggerFactory = null;
        static ILogger Logger = null;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {

            EnablePerMonitorDpiAwareness();

            bool HaveConsole = false;
            try
            {
                HaveConsole = AllocConsole();

#if DEBUG
                Console.WriteLine($"App Domain Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");
#endif

                var result = CommandLine.Parser.Default.ParseArguments<CommandLineOptions>(NormalizeChildStructureFlags(args));
                result
                    .WithParsed<CommandLineOptions>(o =>
                    {
                        try
                        {
                            o.ProcessStrings();
                            Program.options = o;
                        }
                        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or FormatException or JsonException)
                        {
                            //Same outcome as an unknown flag: print the error, the full option list, and quit.
                            AbortWithHelp(result, args, ex.Message);
                        }
                    })
                    .WithNotParsed(errors =>
                    {
                        AbortWithHelp(result, args, "Unable to parse command line arguments");
                    });

                if (result.Tag == CommandLine.ParserResultType.NotParsed || Program.options is null)
                {
                    //AbortWithHelp already exited; this covers the case where WithParsed never ran.
                    return;
                }

                // Build help text - we know parsing succeeded at this point
                HelpText helpText = HelpText.AutoBuild<CommandLineOptions>(result, null, null);


                //If no parameters were supplied or help was requested then print help
                if (Program.options.ShowHelp)
                {
                    helpText = HelpText.AutoBuild<CommandLineOptions>(result, null, null);
                    Console.WriteLine(helpText);
                    while (Console.Read() == 0)
                    {
                        Task.Delay(250);
                    }

                    //If help was requested, then quit afterword.
                    return;
                }

                if (Program.options.ListDisplays)
                {
                    MonoTestbed.PrintDisplays();
                    return;
                }

                ConfigureDiagnostics();


                if (args.Length == 0)
                    Console.WriteLine(helpText);


                InitializeMathnet();

                using MonoTestbed game = new();
                game.Run();
            }
            finally
            {
                if (HaveConsole)
                    FreeConsole();

                StopDiagnostics();
            }
        }

        /// <summary>
        /// Print the error, the same option list -h would show, and exit.  Used for unknown flags and for
        /// recognised options whose values fail validation in <see cref="CommandLineOptions.ProcessStrings"/>.
        /// </summary>
        private static void AbortWithHelp<T>(ParserResult<T> result, string[] args, string reason)
        {
            //The single-arg AutoBuild only accepts NotParsed; validation failures after a successful parse need
            //the configuring overload so the option list still prints.
            HelpText errorHelpText = HelpText.AutoBuild(result, h => HelpText.DefaultParsingErrorsHandler(result, h), e => e);
            errorHelpText.AddPreOptionsLine($"Aborting: {reason}");
            errorHelpText.AddPreOptionsLine($"Arguments: {string.Join(' ', args)}");
            errorHelpText.AddPreOptionsLine("");
            Console.WriteLine(errorHelpText);
#if DEBUG
            System.Diagnostics.Debugger.Break();
#endif
            Environment.Exit(1);
        }

        /// <summary>
        /// Initialize the Mathnet Numerics lib
        /// </summary>
        private static void InitializeMathnet()
        {
            int numMathProcs = Environment.ProcessorCount - 1;
            if (numMathProcs < 1)
                numMathProcs = 1;

            MathNet.Numerics.Control.MaxDegreeOfParallelism = numMathProcs;
            Geometry.Global.TryUseNativeMKL();
        }

        /// <summary>
        /// Attaches console and/or file listeners independently so -v and -l can be combined.
        /// Trace.WriteLine follows the same destinations as ILogger.
        /// </summary>
        private static void ConfigureDiagnostics()
        {
            //Installed regardless of the logging switches: it decides whether a failed Debug.Assert on a worker
            //thread ends the run or is recorded as one failed slice.
            AssertionExceptionTraceListener.Install();

            if (Program.options is null)
                return;

            bool logToFile = Program.options.Log;
            bool logToConsole = Program.options.Verbose;
            MorphologyMesh.BajajMeshGenerator.VerboseLogging = logToConsole;

            if (!logToFile && !logToConsole)
                return;

            // Trace.AutoFlush forces a flush after every WriteLine under Trace's global lock. That is only
            // useful for live console debugging (-v); for -l alone it turns every log line into a sync disk write.
            Trace.AutoFlush = logToConsole;

            if (logToFile)
            {
                LogPath = Program.options.OutputPath is null
                    ? Directory.GetCurrentDirectory()
                    : Path.Combine(Program.options.OutputPath, "Logs");

                if (!Directory.Exists(LogPath))
                    Directory.CreateDirectory(LogPath);

                DebugLogFile = File.CreateText(LogFullPath);
                // Buffer to the OS; StopDiagnostics flushes/closes. Much faster than per-line AutoFlush under
                // parallel face generation (and still far cheaper than console I/O).
                DebugLogFile.AutoFlush = false;

                SynchronizedLogWriter = TextWriter.Synchronized(DebugLogFile);
                LogListener = new TextWriterTraceListener(SynchronizedLogWriter, "MonogameTestbedLog");
                Trace.Listeners.Add(LogListener);
            }

            if (logToConsole)
            {
                ConsoleListener = new ConsoleTraceListener(false) { Name = "MonogameTestbedConsole" };
                Trace.Listeners.Add(ConsoleListener);
            }

            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                if (logToFile)
                    builder.AddDebug();
                if (logToConsole)
                    builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            Logger = LoggerFactory.CreateLogger("MonogameTestbed");

            if (logToConsole)
            {
                Logger.LogInformation("Displaying trace messages");
                Logger.LogDebug("Displaying debug messages");
            }
        }

        private static void StopDiagnostics()
        {
            if (ConsoleListener != null)
            {
                Trace.Listeners.Remove(ConsoleListener);
                ConsoleListener.Flush();
                ConsoleListener.Dispose();
                ConsoleListener = null;
            }

            if (LogListener != null)
            {
                Trace.Listeners.Remove(LogListener);
                LogListener.Flush();
                // Dispose closes the underlying TextWriter (SynchronizedLogWriter / DebugLogFile).
                LogListener.Dispose();
                LogListener = null;
            }

            SynchronizedLogWriter = null;
            DebugLogFile = null;

            if (LoggerFactory != null)
            {
                LoggerFactory.Dispose();
                LoggerFactory = null;
                Logger = null;
            }
        }


    }
}
