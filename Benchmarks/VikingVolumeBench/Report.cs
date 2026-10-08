using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Viking.Benchmarks.VolumeBench
{
    /// <summary>
    /// Builds the results file from a recorder, prints it as one table per phase, and compares two results files.
    /// </summary>
    internal static class Report
    {
        public static void Summarize(ResultsFile results, Recorder recorder)
        {
            results.Measurements = [.. recorder.Measurements.Select(MeasurementResult.From)];
            results.Phases.Clear();
            foreach (var phase in results.Measurements.GroupBy(m => m.Phase).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                MeasurementResult total = phase.FirstOrDefault(m => m.Kind == nameof(MeasurementKind.Total));
                double steps = phase.Where(m => m.Kind == nameof(MeasurementKind.Step)).Sum(m => m.ValueMs);
                results.Phases.Add(new PhaseResult
                {
                    Phase = phase.Key,
                    Name = BenchRunner.PhaseNames.TryGetValue(phase.Key, out string name) ? name : phase.Key,
                    TotalMs = total?.ValueMs ?? steps,
                    StepsMs = steps,
                    UnattributedMs = (total?.ValueMs ?? steps) - steps,
                    AllocatedBytes = total?.AllocatedBytes ?? 0,
                    Gen2 = total?.Gen2 ?? 0,
                });
            }

            results.GrandTotalMs = results.Phases.Sum(p => p.TotalMs);
        }

        public static void Print(ResultsFile r, ResultsFile baseline = null, double threshold = 0.05)
        {
            Dictionary<string, MeasurementResult> b = baseline?.Measurements.ToDictionary(m => m.Id) ?? [];
            Dictionary<string, PhaseResult> bp = baseline?.Phases.ToDictionary(p => p.Phase) ?? [];

            Console.WriteLine();
            Console.WriteLine($"Run {r.Meta.TimestampUtc}  commit {r.Meta.GitCommit} ({r.Meta.GitDirtyFiles} changed files)  {(r.Meta.Warm ? "warm" : "cold")}  MKL {(r.Meta.MklLoaded ? "loaded" : "NOT loaded")}  {r.Meta.Label}");
            if (baseline != null)
                Console.WriteLine($"Baseline {baseline.Meta.TimestampUtc}  commit {baseline.Meta.GitCommit} ({baseline.Meta.GitDirtyFiles} changed files)  {baseline.Meta.Label}");

            foreach (PhaseResult phase in r.Phases)
            {
                Console.WriteLine();
                Console.WriteLine($"Phase {phase.Phase}: {phase.Name}");
                string header = baseline is null
                    ? $"  {"Measurement",-34} {"Median ms",11} {"Min ms",11} {"P95 ms",11} {"Alloc MB",9} {"Gen2",5} {"n",6}"
                    : $"  {"Measurement",-34} {"Base ms",11} {"Median ms",11} {"Change",8} {"Min ms",11} {"P95 ms",11} {"Alloc MB",9} {"Base MB",9} {"Gen2",5}";
                Console.WriteLine(header);

                IEnumerable<MeasurementResult> rows = r.Measurements.Where(m => m.Phase == phase.Phase)
                    .OrderBy(m => m.Kind == nameof(MeasurementKind.Total) ? 2 : 0)
                    .ThenBy(m => m.Id, StringComparer.Ordinal);
                foreach (MeasurementResult m in rows)
                {
                    if (m.Kind == nameof(MeasurementKind.Total))
                    {
                        PrintRow("(unattributed)", phase.UnattributedMs, null, null, null, -1, -1, 0, 0, baseline != null,
                            bp.TryGetValue(phase.Phase, out var bph) ? bph.UnattributedMs : (double?)null, threshold, highlight: false);
                    }

                    string label = m.Kind == nameof(MeasurementKind.Breakdown) ? "  " + m.Id : m.Id;
                    b.TryGetValue(m.Id, out MeasurementResult bm);
                    PrintRow(label, m.ValueMs, m.MinMs, m.P95Ms, m.Samples, m.AllocatedBytes, bm?.AllocatedBytes ?? -1, m.Gen2,
                        m.Instances, baseline != null, bm?.ValueMs, threshold, highlight: m.Kind != nameof(MeasurementKind.Breakdown));
                }
            }

            Console.WriteLine();
            Console.WriteLine("Totals");
            foreach (PhaseResult phase in r.Phases)
            {
                bp.TryGetValue(phase.Phase, out PhaseResult bph);
                Console.WriteLine($"  {phase.Phase} {phase.Name,-45} {phase.TotalMs,12:N1} ms{Change(bph?.TotalMs, phase.TotalMs, threshold)}");
            }
            Console.WriteLine($"  {"Grand total",-47} {r.GrandTotalMs,12:N1} ms{Change(baseline?.GrandTotalMs, r.GrandTotalMs, threshold)}");
            Console.WriteLine($"  {"Run wall time",-47} {r.RunWallMs,12:N1} ms{Change(baseline?.RunWallMs, r.RunWallMs, threshold)}");
            Console.WriteLine($"  {"Peak working set",-47} {r.PeakWorkingSetBytes / 1048576.0,12:N1} MB");

            if (r.MirrorMisses.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"WARNING: the mirror was missing {r.MirrorMisses.Count} files; rerun prime for these sections. First few:");
                foreach (var miss in r.MirrorMisses.Take(5))
                    Console.WriteLine($"  {miss.Key}");
            }
        }

        private static void PrintRow(string label, double value, double? min, double? p95, int? samples, long alloc, long baseAlloc,
            int gen2, int instances, bool comparing, double? baseValue, double threshold, bool highlight)
        {
            string allocText = alloc >= 0 ? (alloc / 1048576.0).ToString("N1", CultureInfo.InvariantCulture) : "-";
            string baseAllocText = baseAlloc >= 0 ? (baseAlloc / 1048576.0).ToString("N1", CultureInfo.InvariantCulture) : "-";
            string minText = min.HasValue ? min.Value.ToString("N1", CultureInfo.InvariantCulture) : "";
            string p95Text = p95.HasValue ? p95.Value.ToString("N1", CultureInfo.InvariantCulture) : "";
            string gen2Text = samples.HasValue ? gen2.ToString(CultureInfo.InvariantCulture) : "";

            if (!comparing)
            {
                string n = samples.HasValue ? $"{samples}" : "";
                Console.WriteLine($"  {Trim(label, 34),-34} {value,11:N1} {minText,11} {p95Text,11} {allocText,9} {gen2Text,5} {n,6}");
                return;
            }

            string baseText = baseValue.HasValue ? baseValue.Value.ToString("N1", CultureInfo.InvariantCulture) : "new";
            string change = ChangeText(baseValue, value);
            bool flag = highlight && baseValue.HasValue && baseValue.Value > 1.0 && Math.Abs(value - baseValue.Value) / baseValue.Value > threshold;
            Console.WriteLine($"{(flag ? "* " : "  ")}{Trim(label, 34),-34} {baseText,11} {value,11:N1} {change,8} {minText,11} {p95Text,11} {allocText,9} {baseAllocText,9} {gen2Text,5}");
        }

        private static string Trim(string s, int width) => s.Length <= width ? s : s.Substring(0, width - 1) + "~";

        private static string ChangeText(double? baseline, double current)
        {
            if (!baseline.HasValue || baseline.Value <= 0)
                return "";
            double change = (current - baseline.Value) / baseline.Value;
            return change.ToString("+0.0%;-0.0%;0.0%", CultureInfo.InvariantCulture);
        }

        private static string Change(double? baseline, double current, double threshold)
        {
            if (!baseline.HasValue || baseline.Value <= 0)
                return "";
            double change = (current - baseline.Value) / baseline.Value;
            string flag = Math.Abs(change) > threshold ? " *" : "";
            return $"  (baseline {baseline.Value:N1} ms, {change.ToString("+0.0%;-0.0%;0.0%", CultureInfo.InvariantCulture)}){flag}";
        }

        /// <summary>Prints the fingerprint comparison. Returns true when the fingerprints match.</summary>
        public static bool PrintFingerprintComparison(Fingerprint baseline, Fingerprint current)
        {
            List<string> diffs = Fingerprint.Compare(baseline, current);
            Console.WriteLine();
            StringBuilder summary = new();
            summary.Append($"Fingerprint: {current.Scenes.Count} scene tile sets, {current.Tiles.Count} tiles, ");
            summary.Append($"{current.Probes.Sum(p => p.Value.Count)} probe points, {current.Annotations.Count} annotations");
            Console.WriteLine(summary.ToString());
            if (diffs.Count == 0)
            {
                Console.WriteLine("Fingerprint matches the baseline.");
                return true;
            }

            Console.WriteLine("FINGERPRINT DIFFERS from the baseline:");
            foreach (string d in diffs)
                Console.WriteLine("  " + d);
            return false;
        }
    }
}
