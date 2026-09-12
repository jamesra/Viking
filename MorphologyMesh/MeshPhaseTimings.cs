using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        RootFinalize,
        /// <summary>Building the gray in-progress contour / AABB overlays shown before a slice has a mesh.</summary>
        IncompleteViewContours,
        ODataFetch,
        SmoothProcesses,
        NeighborCorrection,
        AssemblyPlanCreate,
        /// <summary>Wall time of <c>InitializeTopologyAsync</c> for one structure (not the sum of per-slice topology).</summary>
        TopologyInit,
        /// <summary>Wall time of <c>ConvertToMesh</c> for one structure.</summary>
        FaceGenerationWall,
        /// <summary>Time from the last leaf mesh completion until the root composite is finalized.</summary>
        MergeTail,
        ManifoldValidate,
        Export,
        /// <summary>Time a face-generation worker spent parked on <see cref="MeshParallelism.FaceSlots"/> before
        /// starting.  Large values mean the slot count, not the algorithm, is the bottleneck.</summary>
        FaceSlotWait,
        /// <summary>Divide-and-conquer Delaunay triangulation of one slice's sites plus constrained-edge insertion.</summary>
        Delaunay,
        /// <summary>Region graph construction from face adjacency (<c>IdentifyRegionsViaFaces</c> + connection graph).</summary>
        RegionGraphBuild,
        /// <summary>Untiled-region closing via the medial axis (<c>MergeAndCloseRegionsPass</c> / <c>TryClosingUntiledRegion</c>), both passes.</summary>
        RegionClosing,
        /// <summary>OTV slice-chord candidate search (<c>FirstPassSliceChordGeneration</c> / <c>SliceChordGenerationPass</c>).</summary>
        ChordGeneration,
        /// <summary>Closing remaining triangles/quads along incomplete edges (<c>FirstPassFaceGeneration</c> / <c>CloseFaces</c>), both passes.</summary>
        FaceClosing,
        /// <summary>Rebuilding the region graph after the first chord/close pass to find what is still open.</summary>
        SecondPassRegionDetection
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
        /// <summary>Longest single call per phase, in ticks.  A phase whose max dwarfs its mean has a long-pole
        /// slice that will not shrink no matter how many cores are free at the end of a run.</summary>
        private static readonly long[] _maxTicks = new long[PhaseCount];

        private static long _peakThreadCount;
        private static long _peakWorkingSetBytes;
        private static int _gc0AtReset;
        private static int _gc1AtReset;
        private static int _gc2AtReset;
        private static TimeSpan _gcPauseAtReset;
        private static long _runStartTimestamp;
        /// <summary>System-wide free physical RAM at <see cref="Reset"/>.  Whole-cell runs are memory-hungry
        /// enough (multi-GB working sets, Server GC's per-core heaps) that available RAM at the start of a run
        /// is a confound worth recording alongside every benchmark: two runs of identical code can differ in
        /// wall clock and GC pause fraction simply because one ran with far less free RAM than the other
        /// (background processes, a previous run's process not fully released, etc.).</summary>
        private static long _systemFreeMemoryAtResetBytes;
        private static long _minSystemFreeMemoryBytes;
        private static long _systemTotalMemoryBytes;

        /// <summary>System-wide (not just this process) CPU busy % sampled at a few milestones: before the
        /// OData fetch starts, right after it finishes, and at the end of the run.  This is the whole point of
        /// separating "our process was idle" from "the machine was busy" - a run that looks slow or has an odd
        /// GC pause fraction may simply have shared the box with another heavy job (a Nornir build, another
        /// benchmark, etc.), and that is invisible to every other counter here since they only see this process.</summary>
        private static readonly List<(string Label, double CpuPercent)> _cpuSnapshots = [];
        private static readonly Lock _cpuSnapshotsLock = new();

        /// <summary>Periodic (default 5s) process CPU-core usage / free RAM / working set samples, meant to
        /// span the actual mesh-generation work (start after the OData fetch, stop once the last mesh is
        /// assembled).  The milestone snapshots above answer "was the machine busy before we started"; this
        /// answers "are we actually using the cores we have, the whole way through, or only in bursts" -
        /// a run that never reaches its `faceSlots` budget on this series is not achieving the parallelism the
        /// configured degree-of-parallelism implies, regardless of what the phase timers above say.</summary>
        private static readonly List<(double ElapsedSeconds, double CoresUsed, long SysFreeMB, long ProcessWorkingSetMB)> _continuousSamples = [];
        private static readonly Lock _continuousSamplesLock = new();
        private static CancellationTokenSource _samplingCts;
        private static Task _samplingTask;

        /// <summary>
        /// A <see cref="MeshPhase.FaceGeneration"/> call longer than this is recorded with slice identity so the
        /// run's serial tail can be attributed to a specific contour pair instead of an anonymous max(s).
        /// </summary>
        public static double SlowSliceThresholdSeconds { get; set; } = 10;

        private static readonly List<string> _slowSlices = [];
        private static readonly Lock _slowSlicesLock = new();

        /// <summary>
        /// When false every <see cref="Measure"/> is a couple of array reads and no timestamp, so the hooks can
        /// stay in the shipping code path.
        /// </summary>
        public static bool Enabled { get; set; }

        public static void Reset()
        {
            StopContinuousSampling();
            Array.Clear(_ticks);
            Array.Clear(_calls);
            Array.Clear(_items);
            Array.Clear(_maxTicks);
            Interlocked.Exchange(ref _peakThreadCount, 0);
            Interlocked.Exchange(ref _peakWorkingSetBytes, 0);
            _gc0AtReset = GC.CollectionCount(0);
            _gc1AtReset = GC.CollectionCount(1);
            _gc2AtReset = GC.CollectionCount(2);
            _gcPauseAtReset = GC.GetTotalPauseDuration();
            Interlocked.Exchange(ref _runStartTimestamp, Stopwatch.GetTimestamp());
            MeshParallelism.ResetWaitingStats();

            bool haveMemInfo = TryGetSystemPhysicalMemoryBytes(out long startFreeBytes, out long totalBytes);
            Interlocked.Exchange(ref _systemFreeMemoryAtResetBytes, haveMemInfo ? startFreeBytes : 0);
            Interlocked.Exchange(ref _minSystemFreeMemoryBytes, haveMemInfo ? startFreeBytes : long.MaxValue);
            Interlocked.Exchange(ref _systemTotalMemoryBytes, totalBytes);

            lock (_cpuSnapshotsLock)
                _cpuSnapshots.Clear();
            lock (_slowSlicesLock)
                _slowSlices.Clear();

            SampleProcessStats();
        }

        /// <summary>
        /// Record a slice whose face generation exceeded <see cref="SlowSliceThresholdSeconds"/>. Traced immediately
        /// and listed at the end of <see cref="Report"/> so a quiet run still names the long pole.
        /// </summary>
        public static void RecordSlowSlice(string line)
        {
            if (!Enabled)
                return;

            lock (_slowSlicesLock)
                _slowSlices.Add(line);
            Trace.WriteLine(line);
        }

        /// <summary>
        /// Samples system-wide CPU busy % over <paramref name="sampleWindow"/> and records it under
        /// <paramref name="label"/> for <see cref="Report"/>.  Call this at a handful of run milestones (before
        /// the OData fetch, after it, at the end) - not from a hot per-slice path, since it awaits the whole
        /// sample window before returning.
        /// </summary>
        public static async Task RecordCpuSnapshotAsync(string label, TimeSpan? sampleWindow = null)
        {
            if (!Enabled)
                return;

            TimeSpan window = sampleWindow ?? TimeSpan.FromMilliseconds(500);
            if (!TryGetSystemTimes(out ulong idle0, out ulong kernel0, out ulong user0))
                return;

            await Task.Delay(window).ConfigureAwait(false);

            if (!TryGetSystemTimes(out ulong idle1, out ulong kernel1, out ulong user1))
                return;

            //lpKernelTime already includes idle time on Windows, so total = kernel + user, and busy = total - idle.
            ulong idleDelta = idle1 - idle0;
            ulong totalDelta = (kernel1 - kernel0) + (user1 - user0);
            double busyPercent = totalDelta > 0 ? 100.0 * (1.0 - ((double)idleDelta / totalDelta)) : 0;

            lock (_cpuSnapshotsLock)
                _cpuSnapshots.Add((label, busyPercent));
        }

        /// <summary>
        /// Begins sampling this process's core usage, system free RAM, and working set every
        /// <paramref name="interval"/> (default 5s) on a background task. Call once mesh-generation work has
        /// actually started (e.g. right after the OData fetch); call <see cref="StopContinuousSampling"/> once
        /// it is done. Idempotent - a call while already sampling restarts with a clean series.
        /// </summary>
        public static void StartContinuousSampling(TimeSpan? interval = null)
        {
            if (!Enabled)
                return;

            StopContinuousSampling();

            lock (_continuousSamplesLock)
                _continuousSamples.Clear();

            TimeSpan period = interval ?? TimeSpan.FromSeconds(5);
            CancellationTokenSource cts = new();
            _samplingCts = cts;
            long runStart = Stopwatch.GetTimestamp();

            _samplingTask = Task.Run(async () =>
            {
                using Process proc = Process.GetCurrentProcess();
                TimeSpan lastCpuTime = proc.TotalProcessorTime;
                long lastTimestamp = Stopwatch.GetTimestamp();

                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(period, cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    proc.Refresh();
                    TimeSpan cpuTime = proc.TotalProcessorTime;
                    long timestamp = Stopwatch.GetTimestamp();

                    double wallSeconds = (timestamp - lastTimestamp) / (double)Stopwatch.Frequency;
                    double cpuSeconds = (cpuTime - lastCpuTime).TotalSeconds;
                    double coresUsed = wallSeconds > 0 ? cpuSeconds / wallSeconds : 0;

                    long sysFreeMB = TryGetSystemPhysicalMemoryBytes(out long freeBytes, out _) ? freeBytes / (1024 * 1024) : -1;
                    double elapsedSeconds = (timestamp - runStart) / (double)Stopwatch.Frequency;

                    lock (_continuousSamplesLock)
                        _continuousSamples.Add((elapsedSeconds, coresUsed, sysFreeMB, proc.WorkingSet64 / (1024 * 1024)));

                    lastCpuTime = cpuTime;
                    lastTimestamp = timestamp;
                }
            }, CancellationToken.None);
        }

        public static void StopContinuousSampling()
        {
            CancellationTokenSource cts = Interlocked.Exchange(ref _samplingCts, null);
            if (cts is null)
                return;

            cts.Cancel();
            try
            {
                _samplingTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception)
            {
                //Best-effort join; a slow or faulted sampling task should never hold up the real report.
            }
            finally
            {
                cts.Dispose();
                _samplingTask = null;
            }
        }

        private static bool TryGetSystemTimes(out ulong idle, out ulong kernel, out ulong user)
        {
            idle = kernel = user = 0;
            try
            {
                if (!GetSystemTimes(out FILETIME idleFt, out FILETIME kernelFt, out FILETIME userFt))
                    return false;

                idle = ToUInt64(idleFt);
                kernel = ToUInt64(kernelFt);
                user = ToUInt64(userFt);
                return true;
            }
            catch
            {
                //Best-effort: not available off Windows.
                return false;
            }
        }

        private static ulong ToUInt64(FILETIME ft) => ((ulong)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public int dwLowDateTime;
            public int dwHighDateTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

        private static bool TryGetSystemPhysicalMemoryBytes(out long freeBytes, out long totalBytes)
        {
            freeBytes = 0;
            totalBytes = 0;
            try
            {
                MEMORYSTATUSEX status = new() { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                if (!GlobalMemoryStatusEx(ref status))
                    return false;

                freeBytes = (long)status.ullAvailPhys;
                totalBytes = (long)status.ullTotalPhys;
                return true;
            }
            catch
            {
                //Best-effort: not available off Windows, or the P/Invoke can fail under restricted permissions.
                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        /// <summary>
        /// Record the current process thread count and working set if they exceed previous peaks.
        /// Cheap enough to call from phase scopes when timings are enabled.
        /// </summary>
        public static void SampleProcessStats()
        {
            if (!Enabled)
                return;

            try
            {
                using Process proc = Process.GetCurrentProcess();
                long threads = proc.Threads.Count;
                long ws = proc.WorkingSet64;
                long prevThreads;
                do
                {
                    prevThreads = Interlocked.Read(ref _peakThreadCount);
                    if (threads <= prevThreads)
                        break;
                } while (Interlocked.CompareExchange(ref _peakThreadCount, threads, prevThreads) != prevThreads);

                long prevWs;
                do
                {
                    prevWs = Interlocked.Read(ref _peakWorkingSetBytes);
                    if (ws <= prevWs)
                        break;
                } while (Interlocked.CompareExchange(ref _peakWorkingSetBytes, ws, prevWs) != prevWs);

                if (TryGetSystemPhysicalMemoryBytes(out long freeBytes, out _))
                {
                    long prevMin;
                    do
                    {
                        prevMin = Interlocked.Read(ref _minSystemFreeMemoryBytes);
                        if (freeBytes >= prevMin)
                            break;
                    } while (Interlocked.CompareExchange(ref _minSystemFreeMemoryBytes, freeBytes, prevMin) != prevMin);
                }
            }
            catch
            {
                //Process handle can fail during teardown; timings are best-effort.
            }
        }

        /// <summary>
        /// Record an already-measured interval (e.g. merge-tail wall time computed from two timestamps).
        /// </summary>
        public static void AddRawTicks(MeshPhase phase, long ticks, long itemCount = 1)
        {
            if (!Enabled || ticks <= 0)
                return;

            int i = (int)phase;
            Interlocked.Add(ref _ticks[i], ticks);
            Interlocked.Increment(ref _calls[i]);
            Interlocked.Add(ref _items[i], itemCount);
            RecordMax(i, ticks);
        }

        private static void RecordMax(int phaseIndex, long ticks)
        {
            long prevMax;
            do
            {
                prevMax = Interlocked.Read(ref _maxTicks[phaseIndex]);
                if (ticks <= prevMax)
                    return;
            } while (Interlocked.CompareExchange(ref _maxTicks[phaseIndex], ticks, prevMax) != prevMax);
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
                long elapsed = Stopwatch.GetTimestamp() - _startTimestamp;
                Interlocked.Add(ref _ticks[i], elapsed);
                Interlocked.Increment(ref _calls[i]);
                Interlocked.Add(ref _items[i], _scopeItems);
                RecordMax(i, elapsed);
                SampleProcessStats();
            }
        }

        /// <summary>
        /// Human readable table of every phase that recorded at least one sample.
        /// </summary>
        public static string Report()
        {
            if (!Enabled)
                return "Mesh phase timings: disabled.";

            SampleProcessStats();

            double freq = Stopwatch.Frequency;
            long runStart = Interlocked.Read(ref _runStartTimestamp);
            double runWallSeconds = runStart != 0 ? (Stopwatch.GetTimestamp() - runStart) / freq : 0;

            StringBuilder sb = new();
            sb.AppendLine("Mesh phase timings (phases overlap across concurrent structures; compare run to run, not to wall clock)");
            sb.AppendLine($"{"phase",-24}{"seconds",12}{"calls",12}{"items",14}{"us/call",12}{"us/item",12}{"max(s)",10}{"x-wall",9}");

            foreach (MeshPhase phase in Enum.GetValues<MeshPhase>())
            {
                long calls = Interlocked.Read(ref _calls[(int)phase]);
                if (calls == 0)
                    continue;

                long ticks = Interlocked.Read(ref _ticks[(int)phase]);
                long items = Interlocked.Read(ref _items[(int)phase]);
                long maxTicks = Interlocked.Read(ref _maxTicks[(int)phase]);

                double seconds = ticks / freq;
                double usPerCall = seconds * 1e6 / calls;
                double usPerItem = items > 0 ? seconds * 1e6 / items : double.NaN;
                double maxSeconds = maxTicks / freq;
                //"Effective concurrency" for this phase: total CPU-seconds spent divided by the run's wall clock.
                //A phase whose x-wall stays far below the achieved core count during that phase is not the
                //reason the run is CPU-bound; one whose x-wall tracks peak threads is scaling as expected.
                double xWall = runWallSeconds > 0 ? seconds / runWallSeconds : double.NaN;

                sb.AppendLine($"{phase,-24}{seconds,12:F3}{calls,12:N0}{items,14:N0}{usPerCall,12:F1}{usPerItem,12:F3}{maxSeconds,10:F3}{xWall,9:F2}");
            }

            long peakThreads = Interlocked.Read(ref _peakThreadCount);
            long peakWs = Interlocked.Read(ref _peakWorkingSetBytes);
            int gc0 = GC.CollectionCount(0) - _gc0AtReset;
            int gc1 = GC.CollectionCount(1) - _gc1AtReset;
            int gc2 = GC.CollectionCount(2) - _gc2AtReset;
            TimeSpan gcPause = GC.GetTotalPauseDuration() - _gcPauseAtReset;

            sb.AppendLine($"run wall={runWallSeconds:F1}s  peak threads={peakThreads}  peak WS={peakWs / (1024.0 * 1024.0):F0} MB  GC gen0/1/2={gc0}/{gc1}/{gc2}  GC pause={gcPause.TotalSeconds:F2}s  " +
                          $"faceSlots={MeshParallelism.DegreeOfParallelism}  peakSlicesQueuedForSlot={MeshParallelism.PeakWaitingCount}");

            long sysFreeAtReset = Interlocked.Read(ref _systemFreeMemoryAtResetBytes);
            long sysFreeMin = Interlocked.Read(ref _minSystemFreeMemoryBytes);
            long sysTotal = Interlocked.Read(ref _systemTotalMemoryBytes);
            if (sysTotal > 0)
            {
                //Record this alongside every timing run: two runs of identical code can differ in wall clock and
                //GC pause fraction simply because one machine had far less free RAM than the other at the start
                //(GC mode changes, in particular, are not comparable across runs with very different free RAM).
                sb.AppendLine($"system RAM: total={sysTotal / (1024.0 * 1024.0 * 1024.0):F1} GB  free at start={sysFreeAtReset / (1024.0 * 1024.0):F0} MB  min free during run={sysFreeMin / (1024.0 * 1024.0):F0} MB");
            }

            lock (_cpuSnapshotsLock)
            {
                if (_cpuSnapshots.Count > 0)
                {
                    //Whole-machine CPU busy %, not this process alone - a high reading before any real work has
                    //started means something else on the box (a Nornir build, another benchmark) is competing
                    //for cores and any wall-clock comparison against a different run is not apples-to-apples.
                    sb.AppendLine("system CPU busy % (whole machine, sampled at milestones): " +
                        string.Join("  ", _cpuSnapshots.ConvertAll(s => $"{s.Label}={s.CpuPercent:F0}%")));
                }
            }

            lock (_continuousSamplesLock)
            {
                if (_continuousSamples.Count > 0)
                {
                    sb.AppendLine($"Process core usage / free RAM during mesh generation (sampled periodically, {Environment.ProcessorCount} logical cores, faceSlots={MeshParallelism.DegreeOfParallelism}):");
                    sb.AppendLine($"{"t(s)",8}{"coresUsed",11}{"sysFreeMB",12}{"procWS(MB)",12}");
                    double minCores = double.MaxValue, maxCores = 0, sumCores = 0;
                    foreach (var s in _continuousSamples)
                    {
                        sb.AppendLine($"{s.ElapsedSeconds,8:F0}{s.CoresUsed,11:F1}{s.SysFreeMB,12:N0}{s.ProcessWorkingSetMB,12:N0}");
                        minCores = Math.Min(minCores, s.CoresUsed);
                        maxCores = Math.Max(maxCores, s.CoresUsed);
                        sumCores += s.CoresUsed;
                    }

                    sb.AppendLine($"coresUsed: min={minCores:F1} max={maxCores:F1} avg={sumCores / _continuousSamples.Count:F1} (of {Environment.ProcessorCount} logical cores, {MeshParallelism.DegreeOfParallelism} faceSlots)");
                }
            }

            lock (_slowSlicesLock)
            {
                if (_slowSlices.Count > 0)
                {
                    sb.AppendLine($"slow FaceGeneration (>{SlowSliceThresholdSeconds:F0}s): {_slowSlices.Count}");
                    foreach (string line in _slowSlices)
                        sb.AppendLine($"  {line}");
                }
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
