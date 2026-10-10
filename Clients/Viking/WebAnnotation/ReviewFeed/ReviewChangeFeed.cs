using System;
using System.Collections.Generic;
using System.Linq;

namespace WebAnnotation.ReviewFeed
{
    /// <summary>
    /// Session list of visible-section location changes for Review users.
    /// Coalesces by location id (newer stamp replaces older) and keeps the list
    /// newest-to-oldest by <see cref="ReviewChangeEntry.LastModifiedUtc"/>, then caps length.
    /// Thread-safe; raise <see cref="Changed"/> after each batch so the WPF tab can rebind.
    /// </summary>
    public sealed class ReviewChangeFeed
    {
        public const int DefaultMaxEntries = 50;

        /// <summary>
        /// Suggested max age for <see cref="SelectRecentWindow"/>. The Review tab seed does not scan
        /// this window; it asks OData for the newest <see cref="DefaultMaxEntries"/> locations.
        /// </summary>
        public static readonly TimeSpan DefaultLookback = TimeSpan.FromHours(1);

        readonly object _gate = new();
        readonly List<ReviewChangeEntry> _entries = new();
        readonly int _maxEntries;
        DateTime? _pollIngressCutoffUtc;
        string _seedStatusMessage = "";

        /// <summary>
        /// Shared session feed. AnnotationOverlay pushes poll arrivals; the Review tab binds to it.
        /// </summary>
        public static ReviewChangeFeed Session { get; } = new();

        public ReviewChangeFeed(int maxEntries = DefaultMaxEntries)
        {
            if (maxEntries < 1)
                throw new ArgumentOutOfRangeException(nameof(maxEntries));
            _maxEntries = maxEntries;
        }

        /// <summary>
        /// Lowest <see cref="ReviewChangeEntry.LastModifiedUtc"/> accepted from the visible-section poll.
        /// Set by <see cref="SeedRecent"/> / <see cref="SetPollIngressCutoffUtc"/>. Null means no poll filter yet.
        /// Local edits still bypass this via <see cref="ReviewChangeFeedIngress.PushUpserts"/>.
        /// </summary>
        public DateTime? PollIngressCutoffUtc
        {
            get
            {
                lock (_gate)
                    return _pollIngressCutoffUtc;
            }
        }

        /// <summary>
        /// Last OData seed outcome for the Review tab banner. Empty when the last seed succeeded.
        /// Raised with <see cref="Changed"/> when the text updates.
        /// </summary>
        public string SeedStatusMessage
        {
            get
            {
                lock (_gate)
                    return _seedStatusMessage ?? "";
            }
        }

        /// <summary>
        /// Records a seed failure (or clears it with null/empty) so the Review tab can show why the list is empty/stale.
        /// Called by <see cref="ReviewChangeFeedBackfill"/>.
        /// </summary>
        public void SetSeedStatus(string message)
        {
            string next = message?.Trim() ?? "";
            lock (_gate)
            {
                if (string.Equals(_seedStatusMessage, next, StringComparison.Ordinal))
                    return;
                _seedStatusMessage = next;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Sets the poll floor explicitly (e.g. <c>UtcNow - DefaultLookback</c> when OData returned no rows).
        /// </summary>
        public void SetPollIngressCutoffUtc(DateTime cutoffUtc)
        {
            DateTime utc = cutoffUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(cutoffUtc, DateTimeKind.Utc)
                : cutoffUtc.ToUniversalTime();
            lock (_gate)
                _pollIngressCutoffUtc = utc;
        }

        /// <summary>
        /// True when a poll-sourced row with <paramref name="lastModifiedUtc"/> may enter the feed.
        /// Rows at or after the cutoff pass; when no cutoff is set yet, only the last
        /// <see cref="DefaultLookback"/> window is accepted so a first section dump cannot fill the list.
        /// </summary>
        public bool AllowsPollArrival(DateTime lastModifiedUtc)
        {
            DateTime modified = lastModifiedUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(lastModifiedUtc, DateTimeKind.Utc)
                : lastModifiedUtc.ToUniversalTime();

            lock (_gate)
            {
                DateTime cutoff = _pollIngressCutoffUtc
                    ?? DateTime.UtcNow - DefaultLookback;
                return modified >= cutoff;
            }
        }

        /// <summary>
        /// Keeps changes in <paramref name="maxAge"/> ending at <paramref name="utcNow"/>,
        /// coalesced by location id, newest first, at most <paramref name="maxCount"/>.
        /// "Last hour or last 50, whichever is smaller" is this filter then Take.
        /// </summary>
        public static IReadOnlyList<ReviewChangeEntry> SelectRecentWindow(
            IEnumerable<ReviewChangeEntry> candidates,
            DateTime utcNow,
            TimeSpan maxAge,
            int maxCount)
        {
            if (candidates is null)
                return Array.Empty<ReviewChangeEntry>();
            if (maxCount < 1)
                throw new ArgumentOutOfRangeException(nameof(maxCount));
            if (maxAge < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(maxAge));

            DateTime now = utcNow.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)
                : utcNow.ToUniversalTime();
            DateTime cutoff = now - maxAge;

            var byId = new Dictionary<long, ReviewChangeEntry>();
            foreach (ReviewChangeEntry entry in candidates)
            {
                if (entry is null || entry.LastModifiedUtc < cutoff)
                    continue;

                if (!byId.TryGetValue(entry.LocationId, out ReviewChangeEntry existing)
                    || entry.LastModifiedUtc >= existing.LastModifiedUtc)
                {
                    byId[entry.LocationId] = entry;
                }
            }

            return byId.Values
                .OrderByDescending(e => e.LastModifiedUtc)
                .ThenByDescending(e => e.LocationId)
                .Take(maxCount)
                .ToArray();
        }

        /// <summary>Fired after upserts or deletes change the ordered list. May run on a worker thread.</summary>
        public event EventHandler Changed;

        /// <summary>Newest-first snapshot for binding. Callers must not mutate the returned list.</summary>
        public IReadOnlyList<ReviewChangeEntry> Entries
        {
            get
            {
                lock (_gate)
                    return _entries.ToArray();
            }
        }

        public int Count
        {
            get
            {
                lock (_gate)
                    return _entries.Count;
            }
        }

        /// <summary>
        /// Inserts or replaces each entry, then sorts newest-to-oldest.
        /// A later LastModified for the same id replaces an older row; an equal or older stamp is
        /// ignored when a row already exists (stops visible-section polls from re-raising
        /// <see cref="Changed"/> for unchanged locations). Called by the poll path and
        /// by <see cref="ReviewChangeFeedIngress"/>.
        /// </summary>
        public void ApplyUpserts(IEnumerable<ReviewChangeEntry> incoming)
        {
            if (incoming is null)
                return;

            bool changed = false;
            lock (_gate)
            {
                foreach (ReviewChangeEntry entry in incoming)
                {
                    if (entry is null)
                        continue;
                    if (ApplyUpsertLocked(entry))
                        changed = true;
                }

                SortNewestFirstLocked();
                if (TrimLocked())
                    changed = true;
            }

            if (changed)
                Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Replaces the list with an OData seed (filter Apply / tab open). Empty seed clears the
        /// cards and sets the poll floor to <see cref="DefaultLookback"/>. Clears
        /// <see cref="SeedStatusMessage"/> on success.
        /// </summary>
        public void SeedRecent(IReadOnlyList<ReviewChangeEntry> newestFirst)
        {
            newestFirst ??= Array.Empty<ReviewChangeEntry>();

            DateTime? oldest = null;
            lock (_gate)
            {
                _entries.Clear();
                _seedStatusMessage = "";

                for (int i = newestFirst.Count - 1; i >= 0; i--)
                {
                    ReviewChangeEntry entry = newestFirst[i];
                    if (entry is null)
                        continue;
                    if (!oldest.HasValue || entry.LastModifiedUtc < oldest.Value)
                        oldest = entry.LastModifiedUtc;
                    ApplyUpsertLocked(entry);
                }

                _pollIngressCutoffUtc = oldest
                    ?? DateTime.UtcNow - DefaultLookback;

                SortNewestFirstLocked();
                TrimLocked();
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Records deletes (or refreshes an existing deleted row) and re-sorts newest-to-oldest.
        /// Used when the poll reports location ids removed since the section watermark.
        /// </summary>
        public void ApplyDeletes(IEnumerable<long> locationIds, DateTime deletedAtUtc)
        {
            if (locationIds is null)
                return;

            bool changed = false;
            lock (_gate)
            {
                foreach (long id in locationIds)
                {
                    if (id <= 0)
                        continue;
                    if (ApplyUpsertLocked(ReviewChangeEntry.Deleted(id, deletedAtUtc)))
                        changed = true;
                }

                SortNewestFirstLocked();
                if (TrimLocked())
                    changed = true;
            }

            if (changed)
                Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            lock (_gate)
            {
                if (_entries.Count == 0 && !_pollIngressCutoffUtc.HasValue && string.IsNullOrEmpty(_seedStatusMessage))
                    return;
                _entries.Clear();
                _pollIngressCutoffUtc = null;
                _seedStatusMessage = "";
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        bool ApplyUpsertLocked(ReviewChangeEntry entry)
        {
            int existingIndex = IndexOfLocked(entry.LocationId);
            if (existingIndex >= 0)
            {
                ReviewChangeEntry existing = _entries[existingIndex];
                // Equal stamps are no-ops: section polls re-deliver the same locations often.
                if (entry.LastModifiedUtc <= existing.LastModifiedUtc)
                    return false;

                _entries.RemoveAt(existingIndex);
            }

            _entries.Insert(0, entry);
            return true;
        }

        /// <summary>
        /// Orders the list newest-to-oldest. Ties break on location id descending, matching
        /// <see cref="SelectRecentWindow"/>. Called under <see cref="_gate"/> before trim so the
        /// cap drops the oldest rows rather than whichever arrived last.
        /// </summary>
        void SortNewestFirstLocked()
        {
            _entries.Sort(static (a, b) =>
            {
                int byTime = b.LastModifiedUtc.CompareTo(a.LastModifiedUtc);
                if (byTime != 0)
                    return byTime;
                return b.LocationId.CompareTo(a.LocationId);
            });
        }

        int IndexOfLocked(long locationId)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].LocationId == locationId)
                    return i;
            }

            return -1;
        }

        bool TrimLocked()
        {
            if (_entries.Count <= _maxEntries)
                return false;
            _entries.RemoveRange(_maxEntries, _entries.Count - _maxEntries);
            return true;
        }
    }
}
