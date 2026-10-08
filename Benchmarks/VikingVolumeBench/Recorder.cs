using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Viking.Benchmarks.VolumeBench
{
    /// <summary>
    /// How a measurement contributes to its phase total in reports.
    /// </summary>
    internal enum MeasurementKind
    {
        /// <summary>A step inside the phase. Steps are summed and compared with the phase total to show unattributed time.</summary>
        Step,

        /// <summary>The whole phase, timed directly. One per phase.</summary>
        Total,

        /// <summary>A re-slice of steps (by downsample, by annotation type, per frame). Excluded from sums.</summary>
        Breakdown,
    }

    /// <summary>
    /// One timed sample. Allocated bytes are process-wide (<see cref="AppDomain.MonitoringTotalAllocatedMemorySize"/>),
    /// so they are only meaningful when nothing else runs concurrently; -1 means not measured.
    /// </summary>
    internal readonly struct Sample(double ms, long allocatedBytes, int gen0, int gen1, int gen2)
    {
        public readonly double Ms = ms;
        public readonly long AllocatedBytes = allocatedBytes;
        public readonly int Gen0 = gen0;
        public readonly int Gen1 = gen1;
        public readonly int Gen2 = gen2;
    }

    /// <summary>
    /// A named measurement. Samples are grouped by instance (a section, a scene, a stos group); repeated samples of the
    /// same instance are repetitions. The reported value is the sum over instances of each instance's median, so a phase
    /// that visits 11 sections reports the time for all 11, and totals add up across the report.
    /// </summary>
    internal sealed class Measurement(string id, string phase, string description, MeasurementKind kind)
    {
        public string Id { get; } = id;
        public string Phase { get; } = phase;
        public string Description { get; } = description;
        public MeasurementKind Kind { get; } = kind;
        public Dictionary<string, List<Sample>> Instances { get; } = new(StringComparer.Ordinal);

        public void Add(string instance, Sample sample)
        {
            lock (Instances)
            {
                if (!Instances.TryGetValue(instance, out var list))
                    Instances.Add(instance, list = []);
                list.Add(sample);
            }
        }
    }

    /// <summary>
    /// Collects measurements for one run. Not thread safe for concurrent <see cref="Measure"/> scopes: the runner times
    /// one thing at a time so process-wide allocation counts stay attributable. <see cref="AddExternal"/> may be called
    /// from any thread.
    /// </summary>
    internal sealed class Recorder
    {
        private readonly Dictionary<string, Measurement> _measurements = new(StringComparer.Ordinal);
        private readonly List<string> _order = [];

        public IEnumerable<Measurement> Measurements
        {
            get
            {
                lock (_measurements)
                    return [.. _order.Select(id => _measurements[id])];
            }
        }

        public Measurement Get(string id, string phase, string description, MeasurementKind kind = MeasurementKind.Step)
        {
            lock (_measurements)
            {
                if (!_measurements.TryGetValue(id, out var m))
                {
                    m = new Measurement(id, phase, description, kind);
                    _measurements.Add(id, m);
                    _order.Add(id);
                }
                return m;
            }
        }

        /// <summary>Starts timing. Disposing the scope records one sample for <paramref name="instance"/>.</summary>
        public Scope Measure(string id, string phase, string description, string instance, MeasurementKind kind = MeasurementKind.Step) =>
            new(Get(id, phase, description, kind), instance);

        /// <summary>Records a sample timed elsewhere, for example by <c>LoadStageTimings</c>. Allocations are not known.</summary>
        public void AddExternal(string id, string phase, string description, string instance, TimeSpan elapsed, MeasurementKind kind = MeasurementKind.Step) =>
            Get(id, phase, description, kind).Add(instance, new Sample(elapsed.TotalMilliseconds, -1, 0, 0, 0));

        /// <summary>Records a sample whose time was accumulated across many short calls.</summary>
        public void AddAccumulated(string id, string phase, string description, string instance, long stopwatchTicks, MeasurementKind kind) =>
            Get(id, phase, description, kind).Add(instance, new Sample(stopwatchTicks * 1000.0 / Stopwatch.Frequency, -1, 0, 0, 0));

        public static long AllocatedBytesNow() => AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;

        /// <summary>Times one sample. Must be disposed on the thread that ends the timed work.</summary>
        internal readonly struct Scope : IDisposable
        {
            private readonly Measurement _measurement;
            private readonly string _instance;
            private readonly long _startTicks;
            private readonly long _startAllocated;
            private readonly int _gen0, _gen1, _gen2;

            public Scope(Measurement measurement, string instance)
            {
                _measurement = measurement;
                _instance = instance;
                _gen0 = GC.CollectionCount(0);
                _gen1 = GC.CollectionCount(1);
                _gen2 = GC.CollectionCount(2);
                _startAllocated = AllocatedBytesNow();
                _startTicks = Stopwatch.GetTimestamp();
            }

            public void Dispose()
            {
                long ticks = Stopwatch.GetTimestamp() - _startTicks;
                long allocated = AllocatedBytesNow() - _startAllocated;
                _measurement.Add(_instance, new Sample(
                    ticks * 1000.0 / Stopwatch.Frequency,
                    allocated,
                    GC.CollectionCount(0) - _gen0,
                    GC.CollectionCount(1) - _gen1,
                    GC.CollectionCount(2) - _gen2));
            }
        }
    }

    /// <summary>
    /// Summary statistics written to the results file and used by reports and comparisons.
    /// </summary>
    internal static class Stats
    {
        public static double Median(IReadOnlyList<double> values)
        {
            if (values.Count == 0)
                return 0;
            double[] sorted = [.. values.OrderBy(v => v)];
            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
        }

        /// <summary>Nearest-rank percentile, <paramref name="p"/> in [0, 1].</summary>
        public static double Percentile(IReadOnlyList<double> values, double p)
        {
            if (values.Count == 0)
                return 0;
            double[] sorted = [.. values.OrderBy(v => v)];
            int rank = (int)Math.Ceiling(p * sorted.Length);
            return sorted[Math.Max(0, Math.Min(sorted.Length - 1, rank - 1))];
        }
    }
}
