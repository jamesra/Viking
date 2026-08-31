using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace MorphologyMesh
{
    /// <summary>
    /// Phases of mesh generation worth timing separately.  Each is a stage that can be optimized on its own,
    /// so a change that speeds one up and slows another down is visible rather than hidden in a total.
    /// </summary>
    public enum MeshPhase
    {
        SliceGraphCreate = 0,
        SliceTopology,
        FaceGeneration,
        MergeAddSlice,
        MergeCombine,
        MergeNormals,
        RootFinalize
    }

    /// <summary>
    /// Process-wide accumulator for mesh generation phase timings.
    ///
    /// Deliberately in-memory: mesh generation runs one pipeline per structure with hundreds of structures in
    /// flight, so anything that touches the file system per sample would serialize the very code it measures.
    /// Samples are lock-free counters; the report is produced once at the end of a run.
    ///
    /// Timings overlap by design.  Phases run concurrently across structures, so the sum of phase times will
    /// exceed wall clock time on a multi-core machine.  Compare phases against each other and against the same
    /// phase in a previous run, not against wall clock.
    /// </summary>
    public static class MeshPhaseTimings
    {
        private static readonly int PhaseCount = Enum.GetValues<MeshPhase>().Length;

        private static readonly long[] _ticks = new long[PhaseCount];
        private static readonly long[] _calls = new long[PhaseCount];
        private static readonly long[] _items = new long[PhaseCount];

        /// <summary>
        /// When false every <see cref="Measure"/> is a couple of array reads and no timestamp, so the hooks can
        /// stay in the shipping code path.
        /// </summary>
        public static bool Enabled { get; set; }

        public static void Reset()
        {
            Array.Clear(_ticks);
            Array.Clear(_calls);
            Array.Clear(_items);
        }

        /// <summary>
        /// Times the enclosing scope against <paramref name="phase"/>.
        /// </summary>
        /// <param name="itemCount">
        /// Units of work in this scope (vertices, slices, faces).  Reported as a per-item cost so a phase that
        /// grew because the mesh grew can be told apart from one that grew because it got slower.
        /// </param>
        public static Scope Measure(MeshPhase phase, long itemCount = 1) => new(phase, itemCount);

        public readonly struct Scope : IDisposable
        {
            private readonly long _startTimestamp;
            private readonly MeshPhase _scopePhase;
            private readonly long _scopeItems;

            internal Scope(MeshPhase phase, long itemCount)
            {
                _scopePhase = phase;
                _scopeItems = itemCount;
                _startTimestamp = Enabled ? Stopwatch.GetTimestamp() : 0;
            }

            public void Dispose()
            {
                if (_startTimestamp == 0)
                    return;

                int i = (int)_scopePhase;
                Interlocked.Add(ref _ticks[i], Stopwatch.GetTimestamp() - _startTimestamp);
                Interlocked.Increment(ref _calls[i]);
                Interlocked.Add(ref _items[i], _scopeItems);
            }
        }

        /// <summary>
        /// Human readable table of every phase that recorded at least one sample.
        /// </summary>
        public static string Report()
        {
            if (!Enabled)
                return "Mesh phase timings: disabled.";

            StringBuilder sb = new();
            sb.AppendLine("Mesh phase timings (phases overlap across concurrent structures; compare run to run, not to wall clock)");
            sb.AppendLine($"{"phase",-18}{"seconds",12}{"calls",12}{"items",14}{"us/call",12}{"us/item",12}");

            double freq = Stopwatch.Frequency;
            foreach (MeshPhase phase in Enum.GetValues<MeshPhase>())
            {
                long calls = Interlocked.Read(ref _calls[(int)phase]);
                if (calls == 0)
                    continue;

                long ticks = Interlocked.Read(ref _ticks[(int)phase]);
                long items = Interlocked.Read(ref _items[(int)phase]);

                double seconds = ticks / freq;
                double usPerCall = seconds * 1e6 / calls;
                double usPerItem = items > 0 ? seconds * 1e6 / items : double.NaN;

                sb.AppendLine($"{phase,-18}{seconds,12:F3}{calls,12:N0}{items,14:N0}{usPerCall,12:F1}{usPerItem,12:F3}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Machine readable form, so two runs can be diffed without eyeballing a table.
        /// </summary>
        public static IReadOnlyDictionary<string, double> SecondsByPhase()
        {
            Dictionary<string, double> result = new(PhaseCount);
            double freq = Stopwatch.Frequency;

            foreach (MeshPhase phase in Enum.GetValues<MeshPhase>())
            {
                long calls = Interlocked.Read(ref _calls[(int)phase]);
                if (calls == 0)
                    continue;

                result[phase.ToString()] = Interlocked.Read(ref _ticks[(int)phase]) / freq;
            }

            return result;
        }
    }
}
