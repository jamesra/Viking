using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MathNet.Numerics;
using VikingXNAGraphics;

namespace MonogameTestbed
{
#if WINDOWS || LINUX

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

            public Uri EndpointUri
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(EndpointParam))
                    {
                        return null;
                    }

                    Uri Endpoint_uri;

                    try
                    {
                        var endpoint = EndpointParam.ToEnum<Endpoint>();
                        if (DataSource.EndpointMap.TryGetValue(endpoint, out Endpoint_uri))
                        {
                            return Endpoint_uri;
                        }
                    }
                    catch
                    {

                    }

                    Console.WriteLine($"Could not convert {EndpointParam} to predefined Endpoint.  Trying as URI");

                    Endpoint_uri = new Uri(EndpointParam);
                    return Endpoint_uri;
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
            /// Display help
            /// </summary>
            [Option('h', "help", Required = false, HelpText = "Show help", Separator = ' ', Default = false)]
            public bool ShowHelp { get; set; }

            [Option("mode", Required = false, HelpText = "Startup test mode, e.g. BajajTest or BajajMultiTest")]
            public string ModeParam { get; set; }

            internal TestMode? StartupTestMode { get; private set; }

            [Option("screenshots", Required = false, HelpText = "Dump BAJAJTEST view PNGs under the output folder", Default = false)]
            public bool Screenshots { get; set; }

            [Option("repro", Required = false, HelpText = "BAJAJTEST ReproSet index, range, comma list, or 'all'")]
            public string ReproParam { get; set; }

            public bool ReproAll { get; private set; }

            public List<int> ReproIndices { get; private set; }

            [Option("capture-request", Required = false, HelpText = "JSON file listing extra or replacement screenshot shots")]
            public string CaptureRequestPath { get; set; }

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
                ParseStartupMode();
                ParseReproParam();
                LoadCaptureRequest();
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

        static readonly string LogFile = DateTime.Now.ToString("MM.dd.yyyy HH.mm.ss") + ".log";

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
                        o.ToString();
                        o.ProcessStrings();
                        Program.options = o;
                    })
                    .WithNotParsed(errors =>
                    {
                        // Create a new help text with error information
                        HelpText errorHelpText = HelpText.AutoBuild<CommandLineOptions>(result);
                        errorHelpText.AddPreOptionsLine("Aborting: Unable to parse command line arguments");
                        errorHelpText.AddPreOptionsLine($"Arguments: {string.Join(' ', args)}");
                        errorHelpText.AddPreOptionsLine("");
                        Console.WriteLine(errorHelpText);
#if DEBUG
                        System.Diagnostics.Debugger.Break();
#endif
                        // Exit with error code
                        Environment.Exit(1);
                    });

                if (result.Tag == CommandLine.ParserResultType.NotParsed)
                {
                    // If parsing failed, we exit
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
            if (Program.options is null)
                return;

            bool logToFile = Program.options.Log;
            bool logToConsole = Program.options.Verbose;

            if (!logToFile && !logToConsole)
                return;

            Trace.AutoFlush = true;

            if (logToFile)
            {
                LogPath = Program.options.OutputPath is null
                    ? Directory.GetCurrentDirectory()
                    : Path.Combine(Program.options.OutputPath, "Logs");

                if (!Directory.Exists(LogPath))
                    Directory.CreateDirectory(LogPath);

                DebugLogFile = File.CreateText(LogFullPath);
                DebugLogFile.AutoFlush = true;

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
                LogListener.Dispose();
                LogListener = null;
            }

            if (LoggerFactory != null)
            {
                LoggerFactory.Dispose();
                LoggerFactory = null;
                Logger = null;
            }

            SynchronizedLogWriter?.Close();
            SynchronizedLogWriter = null;
            DebugLogFile = null;
        }


    }
#endif
}
