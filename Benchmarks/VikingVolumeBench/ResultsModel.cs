using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Viking.Benchmarks.VolumeBench
{
    /// <summary>
    /// The results JSON written by <c>run</c> and read by <c>compare</c>. Measurement IDs are stable (see README.md);
    /// comparisons match measurements by ID.
    /// </summary>
    internal sealed class ResultsFile
    {
        public RunMetadata Meta { get; set; } = new();
        public List<MeasurementResult> Measurements { get; set; } = [];
        public List<PhaseResult> Phases { get; set; } = [];

        /// <summary>Sum of the phase totals.</summary>
        public double GrandTotalMs { get; set; }

        /// <summary>Wall time from the start of phase A to the end of phase F.</summary>
        public double RunWallMs { get; set; }

        public long PeakWorkingSetBytes { get; set; }
        public SortedDictionary<string, long> Counts { get; set; } = [];

        /// <summary>Paths the mirror was asked for but did not have. Non-empty means the run is not comparable; rerun prime.</summary>
        public SortedDictionary<string, int> MirrorMisses { get; set; } = [];

        public static JsonSerializerOptions JsonOptions { get; } = new() { WriteIndented = true };

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }

        public static ResultsFile Load(string path) => JsonSerializer.Deserialize<ResultsFile>(File.ReadAllText(path), JsonOptions);

        public static string FingerprintPathFor(string resultsPath) =>
            Path.Combine(Path.GetDirectoryName(Path.GetFullPath(resultsPath)), Path.GetFileNameWithoutExtension(resultsPath) + ".fingerprint.json");
    }

    internal sealed class RunMetadata
    {
        public string Label { get; set; }
        public string TimestampUtc { get; set; }
        public string GitCommit { get; set; }
        public int GitDirtyFiles { get; set; }
        public string Machine { get; set; }
        public int ProcessorCount { get; set; }
        public string Framework { get; set; }
        public string Configuration { get; set; }
        public bool MklLoaded { get; set; }
        public string MklReport { get; set; }
        public bool Warm { get; set; }
        public string VolumeUrl { get; set; }
        public string VolumeTransform { get; set; }
        public int[] Sections { get; set; }
        public int[] Downsamples { get; set; }
        public int PositionsPerLevel { get; set; }
        public int SceneRepetitions { get; set; }
        public int VolumeRepetitions { get; set; }
        public int PanFrames { get; set; }
        public int Seed { get; set; }
    }

    internal sealed class MeasurementResult
    {
        public string Id { get; set; }
        public string Phase { get; set; }
        public string Description { get; set; }
        public string Kind { get; set; }
        public int Instances { get; set; }
        public int Samples { get; set; }

        /// <summary>Sum over instances of each instance's median.</summary>
        public double ValueMs { get; set; }

        /// <summary>Sum over instances of each instance's fastest sample.</summary>
        public double MinMs { get; set; }

        /// <summary>Sum over instances of each instance's 95th-percentile sample.</summary>
        public double P95Ms { get; set; }

        /// <summary>Sum over instances of each instance's median allocation, or -1 when the step was timed by a stage event.</summary>
        public long AllocatedBytes { get; set; }

        public int Gen0 { get; set; }
        public int Gen1 { get; set; }
        public int Gen2 { get; set; }
        public List<InstanceResult> PerInstance { get; set; } = [];

        public static MeasurementResult From(Measurement m)
        {
            MeasurementResult r = new()
            {
                Id = m.Id,
                Phase = m.Phase,
                Description = m.Description,
                Kind = m.Kind.ToString(),
                Instances = m.Instances.Count,
            };

            bool allocKnown = true;
            foreach (var pair in m.Instances.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                List<Sample> samples = pair.Value;
                double[] ms = [.. samples.Select(s => s.Ms)];
                InstanceResult i = new()
                {
                    Instance = pair.Key,
                    N = samples.Count,
                    MedianMs = Stats.Median(ms),
                    MinMs = ms.Min(),
                    P95Ms = Stats.Percentile(ms, 0.95),
                };

                if (samples.Any(s => s.AllocatedBytes < 0))
                {
                    allocKnown = false;
                    i.MedianAllocatedBytes = -1;
                }
                else
                {
                    i.MedianAllocatedBytes = (long)Stats.Median([.. samples.Select(s => (double)s.AllocatedBytes)]);
                }

                r.PerInstance.Add(i);
                r.Samples += samples.Count;
                r.ValueMs += i.MedianMs;
                r.MinMs += i.MinMs;
                r.P95Ms += i.P95Ms;
                r.AllocatedBytes += Math.Max(0, i.MedianAllocatedBytes);
                r.Gen0 += samples.Sum(s => s.Gen0);
                r.Gen1 += samples.Sum(s => s.Gen1);
                r.Gen2 += samples.Sum(s => s.Gen2);
            }

            if (!allocKnown)
                r.AllocatedBytes = -1;

            return r;
        }
    }

    internal sealed class InstanceResult
    {
        public string Instance { get; set; }
        public int N { get; set; }
        public double MedianMs { get; set; }
        public double MinMs { get; set; }
        public double P95Ms { get; set; }
        public long MedianAllocatedBytes { get; set; }
    }

    internal sealed class PhaseResult
    {
        public string Phase { get; set; }
        public string Name { get; set; }

        /// <summary>The phase's Total measurement value.</summary>
        public double TotalMs { get; set; }

        /// <summary>Sum of the phase's Step measurement values.</summary>
        public double StepsMs { get; set; }

        /// <summary><see cref="TotalMs"/> minus <see cref="StepsMs"/>: work in the phase that no step covers. Negative when steps overlap.</summary>
        public double UnattributedMs { get; set; }

        public long AllocatedBytes { get; set; }
        public int Gen2 { get; set; }
    }
}
