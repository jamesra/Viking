using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>
    /// Runs single-circle refreshes one per location at a time and at most a fixed number overall.
    /// A request that arrives while its location is already refreshing replaces any earlier waiting request and
    /// runs after the current one, so the last edit always wins and no request is dropped.
    /// </summary>
    /// <remarks>
    /// Why it exists: dragging a circle fires one geometry change per frame, and each used to start its own viewport
    /// capture and SegmentImage. All state is guarded by <see cref="gate"/>, which is never held across an await.
    /// The refresh delegate runs on whatever thread the previous await resumed on; it must not assume the UI thread.
    /// </remarks>
    internal sealed class LocationRefreshQueue
    {
        private readonly Func<long, int, int, Task> refresh;
        private readonly Func<bool> isEnabled;
        private readonly SemaphoreSlim slots;
        private readonly object gate = new();
        private readonly HashSet<long> inFlight = [];
        private readonly Dictionary<long, (int SectionNumber, int Generation)> pending = [];

        /// <param name="refresh">Performs one refresh for (location id, section number, cache generation).</param>
        /// <param name="isEnabled">False stops waiting requests from starting and drops any queued follow-up.</param>
        /// <param name="maxConcurrent">How many different locations may refresh at once.</param>
        public LocationRefreshQueue(Func<long, int, int, Task> refresh, Func<bool> isEnabled, int maxConcurrent)
        {
            this.refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
            this.isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
            if (maxConcurrent < 1)
                throw new ArgumentOutOfRangeException(nameof(maxConcurrent));

            slots = new SemaphoreSlim(maxConcurrent);
        }

        /// <summary>
        /// Requests a refresh. Completes when this call has nothing left to do: immediately when the location is
        /// already being refreshed (its worker will run the newest request), otherwise after the run and any follow-ups.
        /// </summary>
        public async Task RefreshAsync(long locationId, int sectionNumber, int generation)
        {
            lock (gate)
            {
                if (!inFlight.Add(locationId))
                {
                    pending[locationId] = (sectionNumber, generation);
                    return;
                }
            }

            while (true)
            {
                await slots.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (isEnabled())
                        await refresh(locationId, sectionNumber, generation).ConfigureAwait(false);
                }
                finally
                {
                    slots.Release();
                }

                lock (gate)
                {
                    if (isEnabled() && pending.TryGetValue(locationId, out (int SectionNumber, int Generation) next))
                    {
                        pending.Remove(locationId);
                        sectionNumber = next.SectionNumber;
                        generation = next.Generation;
                        continue;
                    }

                    pending.Remove(locationId);
                    inFlight.Remove(locationId);
                    return;
                }
            }
        }
    }
}
