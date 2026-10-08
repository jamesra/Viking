namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Coalesces interactive SegmentImage attempts to one in-flight RPC plus at most one follow-up.
    /// Extra clicks while busy set <see cref="MarkDirty"/>; <see cref="OnFinishedShouldRetry"/> then
    /// starts a single request with the live prompt lists. Generations discard stale overlays.
    /// </summary>
    /// <remarks>
    /// Thread safety: every field is read and written under <see cref="gate"/>. The UI thread calls
    /// <see cref="TryStart"/> and <see cref="MarkDirty"/> while the attempt finishes on a worker thread via
    /// <see cref="OnFinishedShouldRetry"/>. Without one lock around busy and pending, a click that saw
    /// busy and recorded its follow-up just after the worker read "nothing pending" was lost.
    /// The critical sections are a few field accesses; nothing awaits or calls out while holding the lock.
    /// </remarks>
    internal sealed class SegmentationRequestCoalescer
    {
        private readonly object gate = new();
        private int requestGeneration;
        private int appliedGeneration;
        private bool isBusy;
        private bool pendingRefresh;

        /// <summary>
        /// True while a SegmentImage attempt owns the coalescer. Status chips read it from the UI thread
        /// while the attempt finishes on a worker thread.
        /// </summary>
        public bool IsBusy
        {
            get
            {
                lock (gate)
                    return isBusy;
            }
        }

        /// <summary>True when a later click should run after the current attempt finishes.</summary>
        public bool PendingRefresh
        {
            get
            {
                lock (gate)
                    return pendingRefresh;
            }
        }

        /// <summary>Generation of the latest started attempt, including one that is still in flight.</summary>
        public int CurrentGeneration
        {
            get
            {
                lock (gate)
                    return requestGeneration;
            }
        }

        /// <summary>
        /// Starts a new attempt and returns its generation. Returns false when already busy
        /// and records a follow-up instead.
        /// </summary>
        public bool TryStart(out int generation)
        {
            lock (gate)
            {
                if (isBusy)
                {
                    pendingRefresh = true;
                    generation = requestGeneration;
                    return false;
                }

                isBusy = true;
                generation = ++requestGeneration;
                return true;
            }
        }

        /// <summary>
        /// Records that prompts changed while upload or another attempt is in flight.
        /// </summary>
        public void MarkDirty()
        {
            lock (gate)
                pendingRefresh = true;
        }

        /// <summary>
        /// Clears busy. Returns true once if a follow-up should start with the current points.
        /// </summary>
        public bool OnFinishedShouldRetry()
        {
            lock (gate)
            {
                isBusy = false;
                if (!pendingRefresh)
                    return false;

                pendingRefresh = false;
                return true;
            }
        }

        /// <summary>
        /// Drops a scheduled follow-up without touching the in-flight busy flag.
        /// </summary>
        public void CancelPending()
        {
            lock (gate)
                pendingRefresh = false;
        }

        /// <summary>
        /// Viewport change or command teardown: no follow-up, and in-flight results must not draw.
        /// </summary>
        public void Invalidate()
        {
            lock (gate)
            {
                pendingRefresh = false;
                appliedGeneration = requestGeneration;
            }
        }

        /// <summary>
        /// True when this generation is still allowed to replace the overlay.
        /// </summary>
        public bool ShouldApply(int generation)
        {
            lock (gate)
                return generation > appliedGeneration;
        }

        /// <summary>
        /// Claims the overlay for <paramref name="generation"/>. False if a newer result already won.
        /// </summary>
        public bool TryApply(int generation)
        {
            lock (gate)
            {
                if (generation <= appliedGeneration)
                    return false;

                appliedGeneration = generation;
                return true;
            }
        }
    }
}
