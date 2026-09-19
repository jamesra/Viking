using System.Threading;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Coalesces interactive SegmentImage attempts to one in-flight RPC plus at most one follow-up.
    /// Extra clicks while busy set <see cref="MarkDirty"/>; <see cref="OnFinishedShouldRetry"/> then
    /// starts a single request with the live prompt lists. Generations discard stale overlays.
    /// </summary>
    internal sealed class SegmentationRequestCoalescer
    {
        private int requestGeneration;
        private int appliedGeneration;

        /// <summary>True while a SegmentImage attempt owns the coalescer.</summary>
        public bool IsBusy { get; private set; }

        /// <summary>True when a later click should run after the current attempt finishes.</summary>
        public bool PendingRefresh { get; private set; }

        /// <summary>Generation of the latest started attempt, including one that is still in flight.</summary>
        public int CurrentGeneration => requestGeneration;

        /// <summary>
        /// Starts a new attempt and returns its generation. Returns false when already busy
        /// and records a follow-up instead.
        /// </summary>
        public bool TryStart(out int generation)
        {
            if (IsBusy)
            {
                PendingRefresh = true;
                generation = requestGeneration;
                return false;
            }

            IsBusy = true;
            generation = Interlocked.Increment(ref requestGeneration);
            return true;
        }

        /// <summary>
        /// Records that prompts changed while upload or another attempt is in flight.
        /// </summary>
        public void MarkDirty()
        {
            PendingRefresh = true;
        }

        /// <summary>
        /// Clears busy. Returns true once if a follow-up should start with the current points.
        /// </summary>
        public bool OnFinishedShouldRetry()
        {
            IsBusy = false;
            if (!PendingRefresh)
                return false;

            PendingRefresh = false;
            return true;
        }

        /// <summary>
        /// Drops a scheduled follow-up without touching the in-flight busy flag.
        /// </summary>
        public void CancelPending()
        {
            PendingRefresh = false;
        }

        /// <summary>
        /// Viewport change or command teardown: no follow-up, and in-flight results must not draw.
        /// </summary>
        public void Invalidate()
        {
            PendingRefresh = false;
            appliedGeneration = requestGeneration;
        }

        /// <summary>
        /// True when this generation is still allowed to replace the overlay.
        /// </summary>
        public bool ShouldApply(int generation) => generation > appliedGeneration;

        /// <summary>
        /// Claims the overlay for <paramref name="generation"/>. False if a newer result already won.
        /// </summary>
        public bool TryApply(int generation)
        {
            if (generation <= appliedGeneration)
                return false;

            appliedGeneration = generation;
            return true;
        }
    }
}
