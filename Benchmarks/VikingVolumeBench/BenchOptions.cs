using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Viking.Benchmarks.VolumeBench
{
    /// <summary>
    /// Command line options. Parsed by hand so the harness has no dependencies beyond the projects it measures.
    /// </summary>
    internal sealed class BenchOptions
    {
        public const string DefaultVolumeUrl = "http://rogue1.codepharm.net/RC2/SliceToVolume.VikingXML";

        /// <summary>Default benchmark sections: the reference section 645 plus a spread across the volume, more than the 6 sections <c>SectionTransformsCache</c> keeps.</summary>
        public static readonly int[] DefaultSections = [645, 1, 100, 300, 500, 643, 646, 800, 1000, 1200, 1454];

        public static readonly int[] DefaultDownsamples = [1, 2, 4, 8, 16, 32, 64, 128];

        /// <summary><c>prime</c>, <c>run</c> or <c>compare</c>.</summary>
        public string Command = "run";

        public string VolumeUrl = DefaultVolumeUrl;

        /// <summary>Root of the mirror, the Viking cache and the annotation snapshot. Never the user's Viking cache.</summary>
        public string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VikingVolumeBench");

        /// <summary>Stos group used as the volume transform. RC2's default depends on which group finishes loading first, so it is fixed here.</summary>
        public string VolumeTransform = "SliceToVolume1";

        public string MosaicChannel = "TEM.Leveled.Pyramid";
        public string MosaicTransform = "Grid";
        public string TilesetChannel = "TEM.Leveled";

        public int[] Sections = DefaultSections;
        public int[] Downsamples = DefaultDownsamples;

        /// <summary>Seeded scene positions per section and downsample.</summary>
        public int PositionsPerLevel = 8;

        public int SceneRepetitions = 5;
        public int VolumeRepetitions = 3;
        public int PanFrames = 120;
        public int ViewportWidth = 1920;
        public int ViewportHeight = 1080;
        public int Seed = 20261005;

        /// <summary>When false (the default) computed caches are deleted before each volume load so every run does the work being optimized.</summary>
        public bool Warm;

        /// <summary>Also time a live annotation fetch per section. Reported, never compared.</summary>
        public bool LiveAnnotations;

        public string OutputPath;
        public string BaselinePath;
        public string CurrentPath;
        public string Label;

        /// <summary>Change in a measurement's median, as a fraction, above which compare output highlights it.</summary>
        public double HighlightThreshold = 0.05;

        public string MirrorRoot => Path.Combine(Root, "mirror");
        public string VikingCacheRoot => Path.Combine(Root, "viking-cache");
        public string AnnotationRoot => Path.Combine(Root, "annotations");
        public string ResultsRoot => Path.Combine(Root, "results");

        public Uri VolumeUri => new(VolumeUrl);

        /// <summary>Volume directory on the server, for example <c>http://rogue1.codepharm.net/RC2</c>.</summary>
        public string VolumeHost => VolumeUrl.Substring(0, VolumeUrl.LastIndexOf('/'));

        /// <summary>Volume directory relative to the server root, for example <c>RC2</c>. The mirror serves it under the same path.</summary>
        public string VolumeServerPath => VolumeUri.AbsolutePath.Substring(1, VolumeUri.AbsolutePath.LastIndexOf('/') - 1);

        public string VolumeFileName => Path.GetFileName(VolumeUri.AbsolutePath);

        public static BenchOptions Parse(string[] args)
        {
            BenchOptions o = new();
            Queue<string> q = new(args);
            if (q.Count > 0 && !q.Peek().StartsWith("--", StringComparison.Ordinal))
                o.Command = q.Dequeue().ToLowerInvariant();

            while (q.Count > 0)
            {
                string arg = q.Dequeue();
                string Next() => q.Count > 0 ? q.Dequeue() : throw new ArgumentException($"{arg} needs a value");
                switch (arg.ToLowerInvariant())
                {
                    case "--volume": o.VolumeUrl = Next(); break;
                    case "--root": o.Root = Next(); break;
                    case "--volume-transform": o.VolumeTransform = Next(); break;
                    case "--sections": o.Sections = ParseInts(Next()); break;
                    case "--downsamples": o.Downsamples = ParseInts(Next()); break;
                    case "--positions": o.PositionsPerLevel = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--reps": o.SceneRepetitions = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--volume-reps": o.VolumeRepetitions = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--pan-frames": o.PanFrames = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--seed": o.Seed = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--warm": o.Warm = true; break;
                    case "--cold": o.Warm = false; break;
                    case "--live-annotations": o.LiveAnnotations = true; break;
                    case "--out": o.OutputPath = Next(); break;
                    case "--baseline": o.BaselinePath = Next(); break;
                    case "--current": o.CurrentPath = Next(); break;
                    case "--label": o.Label = Next(); break;
                    case "--threshold": o.HighlightThreshold = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--quick":
                        o.Sections = [645, 646, 1];
                        o.Downsamples = [4, 32];
                        o.PositionsPerLevel = 2;
                        o.SceneRepetitions = 1;
                        o.VolumeRepetitions = 1;
                        o.PanFrames = 10;
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument {arg}");
                }
            }

            return o;
        }

        private static int[] ParseInts(string s) =>
            [.. s.Split([','], StringSplitOptions.RemoveEmptyEntries).Select(v => int.Parse(v.Trim(), CultureInfo.InvariantCulture))];

        public const string Usage = @"VikingVolumeBench <command> [options]

Commands:
  prime     Download the VikingXML, stos zips and benchmark-section mosaics into the local mirror, and save a read-only
            annotation snapshot for the benchmark sections. Run once; later runs need no network.
  run       Run phases A-F against the local mirror and write a results JSON plus a fingerprint JSON.
  compare   Compare two results files: --baseline <file> --current <file>.

Options:
  --root <dir>             Working directory (default %LOCALAPPDATA%\VikingVolumeBench)
  --volume <url>           VikingXML URL (default RC2)
  --volume-transform <n>   Stos group to use (default SliceToVolume1)
  --sections a,b,c         Benchmark sections (default 645,1,100,300,500,643,646,800,1000,1200,1454)
  --downsamples a,b,c      Scene downsample levels (default 1,2,4,8,16,32,64,128)
  --positions <n>          Seeded positions per section and level (default 8)
  --reps <n>               Repetitions per scene (default 5)
  --volume-reps <n>        Volume loads (default 3)
  --pan-frames <n>         Frames in the pan sequence (default 120)
  --cold | --warm          Delete computed caches before each volume load (default) or keep them
  --live-annotations       Also time a live annotation fetch per section (not compared)
  --out <file>             Results file (default results\<timestamp>.json under --root)
  --baseline <file>        With run: compare against this results file afterwards. With compare: the baseline.
  --current <file>         With compare: the new results.
  --label <text>           Free text stored with the results
  --threshold <fraction>   Highlight changes larger than this (default 0.05)
  --quick                  Small smoke-test configuration

Environment:
  VIKINGBENCH_USER, VIKINGBENCH_PASSWORD   Annotation login for prime. Falls back to the anonymous login.";
    }
}
