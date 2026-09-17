using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MorphologyMesh
{
    /// <summary>
    /// Shared degree-of-parallelism budget for whole-cell meshing. Structure pipelines and per-slice face
    /// generation used to each claim <see cref="Environment.ProcessorCount"/> slots independently, so a parent
    /// with children oversubscribed the machine. One budget keeps total concurrent heavy work near the core count.
    /// </summary>
    public static class MeshParallelism
    {
        static int _degree = Math.Max(1, Environment.ProcessorCount);
        static SemaphoreSlim _faceSlots = new(_degree, _degree);
        static long _waitingCount;
        static long _peakWaitingCount;

        /// <summary>
        /// Maximum concurrent face-generation or structure-pipeline workers. Defaults to logical processor count.
        /// </summary>
        public static int DegreeOfParallelism
        {
            get => Volatile.Read(ref _degree);
            set
            {
                int next = Math.Max(1, value);
                Volatile.Write(ref _degree, next);
                //Replace the semaphore so a mid-run change cannot leave a stale capacity; rare outside tests.
                SemaphoreSlim previous = Interlocked.Exchange(ref _faceSlots, new SemaphoreSlim(next, next));
                previous.Dispose();
            }
        }

        /// <summary>
        /// Process-wide face-generation slots. Prefer <see cref="SemaphoreSlim.WaitAsync()"/> so waiting workers
        /// yield instead of parking a thread-pool thread (the failure mode of the old blocking Wait pattern).
        /// </summary>
        public static SemaphoreSlim FaceSlots => Volatile.Read(ref _faceSlots)!;

        /// <summary>
        /// Highest number of slices simultaneously queued for a face slot since the last <see cref="MeshPhaseTimings.Reset"/>.
        /// A run where this stays near zero has enough slots for the offered work; one where it grows unbounded
        /// means slices are arriving faster than <see cref="DegreeOfParallelism"/> can drain them.
        /// </summary>
        public static long PeakWaitingCount => Interlocked.Read(ref _peakWaitingCount);

        public static void ResetWaitingStats() => Interlocked.Exchange(ref _peakWaitingCount, 0);

        public static async Task RunWithFaceSlotAsync(Func<CancellationToken, ValueTask> work, CancellationToken cancellationToken = default)
        {
            SemaphoreSlim slots = FaceSlots;

            long waiting = Interlocked.Increment(ref _waitingCount);
            RecordPeakWaiting(waiting);

            using (MeshPhaseTimings.Measure(MeshPhase.FaceSlotWait))
            {
                await slots.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            Interlocked.Decrement(ref _waitingCount);

            try
            {
                await work(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                slots.Release();
            }
        }

        static void RecordPeakWaiting(long waiting)
        {
            long prevPeak;
            do
            {
                prevPeak = Interlocked.Read(ref _peakWaitingCount);
                if (waiting <= prevPeak)
                    return;
            } while (Interlocked.CompareExchange(ref _peakWaitingCount, waiting, prevPeak) != prevPeak);
        }
    }
}
