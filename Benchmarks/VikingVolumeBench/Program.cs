using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Viking.VolumeModel;

namespace Viking.Benchmarks.VolumeBench
{
    /// <summary>
    /// Entry point. Exit codes: 0 success, 1 fingerprint differs from the baseline, 2 prime could not take an annotation
    /// snapshot, 3 bad arguments or a failed run.
    /// </summary>
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            BenchOptions options;
            try
            {
                options = BenchOptions.Parse(args);
            }
            catch (ArgumentException e)
            {
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine(BenchOptions.Usage);
                return 3;
            }

            Console.WriteLine($"VikingVolumeBench {typeof(Program).Assembly.GetName().Version}  root {options.Root}");

            try
            {
                return options.Command switch
                {
                    "prime" => await Primer.RunAsync(options).ConfigureAwait(false),
                    "run" => await RunAsync(options).ConfigureAwait(false),
                    "compare" => Compare(options),
                    _ => Usage(),
                };
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return 3;
            }
        }

        private static int Usage()
        {
            Console.Error.WriteLine(BenchOptions.Usage);
            return 3;
        }

        private static async Task<int> RunAsync(BenchOptions options)
        {
            AppDomain.MonitoringIsEnabled = true;
            SqlServerTypesLoader.Loader.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);

            int mathThreads = Math.Max(1, Environment.ProcessorCount - 1);
            MathNet.Numerics.Control.MaxDegreeOfParallelism = mathThreads;
            //The parameterless overload exists in every branch; Control.Describe() reports which provider ended up active.
            bool mkl = Geometry.Global.TryUseNativeMKL();
            string mklReport = MathNet.Numerics.Control.Describe();
            Console.WriteLine($"MathNet native linear algebra {(mkl ? "loaded" : "NOT loaded")}, MaxDegreeOfParallelism={mathThreads}");

            ResultsFile results = new() { Meta = BuildMetadata(options, mkl, mklReport) };
            Recorder recorder = new();
            Fingerprint fingerprint = new();

            using MirrorServer server = new(options);
            server.Start();
            Console.WriteLine($"Mirror serving {options.MirrorRoot} at {server.VolumeUrl}");

            BenchRunner runner = new(options, server, recorder, fingerprint);
            LoadStageTimings.StageCompleted += runner.OnStage;
            try
            {
                results.RunWallMs = await runner.RunAsync().ConfigureAwait(false);
            }
            finally
            {
                LoadStageTimings.StageCompleted -= runner.OnStage;
            }

            results.PeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64;
            results.Counts = runner.Counts;
            foreach (var miss in server.Misses)
                results.MirrorMisses[miss.Key] = miss.Value;
            Report.Summarize(results, recorder);

            string outPath = options.OutputPath ?? Path.Combine(options.ResultsRoot,
                DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + (options.Label is null ? "" : "-" + Sanitize(options.Label)) + ".json");
            results.Save(outPath);
            fingerprint.Save(ResultsFile.FingerprintPathFor(outPath));

            ResultsFile baseline = options.BaselinePath is null ? null : ResultsFile.Load(options.BaselinePath);
            Report.Print(results, baseline, options.HighlightThreshold);
            Console.WriteLine();
            Console.WriteLine($"Results: {outPath}");
            Console.WriteLine($"Counts: {string.Join(", ", results.Counts.Select(c => $"{c.Key}={c.Value}"))}");

            if (baseline is null)
                return 0;

            Fingerprint baseFingerprint = Fingerprint.Load(ResultsFile.FingerprintPathFor(options.BaselinePath));
            return Report.PrintFingerprintComparison(baseFingerprint, fingerprint) ? 0 : 1;
        }

        private static int Compare(BenchOptions options)
        {
            if (options.BaselinePath is null || options.CurrentPath is null)
            {
                Console.Error.WriteLine("compare needs --baseline <file> and --current <file>");
                return 3;
            }

            ResultsFile baseline = ResultsFile.Load(options.BaselinePath);
            ResultsFile current = ResultsFile.Load(options.CurrentPath);
            Report.Print(current, baseline, options.HighlightThreshold);

            Fingerprint baseFingerprint = Fingerprint.Load(ResultsFile.FingerprintPathFor(options.BaselinePath));
            Fingerprint currentFingerprint = Fingerprint.Load(ResultsFile.FingerprintPathFor(options.CurrentPath));
            return Report.PrintFingerprintComparison(baseFingerprint, currentFingerprint) ? 0 : 1;
        }

        private static string Sanitize(string s) => new([.. s.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '_' : c)]);

        private static RunMetadata BuildMetadata(BenchOptions o, bool mkl, string mklReport)
        {
            (string commit, int dirty) = GitState();
            return new RunMetadata
            {
                Label = o.Label,
                TimestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                GitCommit = commit,
                GitDirtyFiles = dirty,
                Machine = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount,
                Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
#if DEBUG
                Configuration = "Debug",
#else
                Configuration = "Release",
#endif
                MklLoaded = mkl,
                MklReport = mklReport,
                Warm = o.Warm,
                VolumeUrl = o.VolumeUrl,
                VolumeTransform = o.VolumeTransform,
                Sections = o.Sections,
                Downsamples = o.Downsamples,
                PositionsPerLevel = o.PositionsPerLevel,
                SceneRepetitions = o.SceneRepetitions,
                VolumeRepetitions = o.VolumeRepetitions,
                PanFrames = o.PanFrames,
                Seed = o.Seed,
            };
        }

        /// <summary>Commit and number of changed files of the checkout the harness was built from, or "unknown".</summary>
        private static (string Commit, int Dirty) GitState()
        {
            DirectoryInfo dir = new(AppDomain.CurrentDomain.BaseDirectory);
            // .git is a file, not a directory, in a git worktree.
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")) && !File.Exists(Path.Combine(dir.FullName, ".git")))
                dir = dir.Parent;
            if (dir is null)
                return ("unknown", 0);

            string Git(string arguments)
            {
                ProcessStartInfo info = new("git", arguments)
                {
                    WorkingDirectory = dir.FullName,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using Process p = Process.Start(info);
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return output;
            }

            try
            {
                string commit = Git("rev-parse --short HEAD").Trim();
                int dirty = Git("status --porcelain --untracked-files=no").Split(['\n'], StringSplitOptions.RemoveEmptyEntries).Length;
                return (commit, dirty);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return ("unknown", 0);
            }
        }
    }
}
