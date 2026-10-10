using System;
using System.Collections.Generic;
using System.Diagnostics;
using Viking.Common;

namespace WebAnnotation.ReviewFeed
{
    /// <summary>
    /// Display preferences for the Review change feed. Persisted in user settings.
    /// The feed still stores arrivals; the WPF list applies this filter when binding.
    /// Empty <see cref="SectionsText"/> means every section in the volume (backfill is volume-wide).
    /// </summary>
    public sealed class ReviewChangeFeedFilter
    {
        static readonly object SessionGate = new object();
        static ReviewChangeFeedFilter _session;

        /// <summary>
        /// Shared session filter bound by the Review Changes tab.
        /// Lazy so settings upgrade runs before the first read (Velopack version folders
        /// otherwise reload factory defaults).
        /// </summary>
        public static ReviewChangeFeedFilter Session
        {
            get
            {
                if (_session != null)
                    return _session;
                lock (SessionGate)
                {
                    if (_session != null)
                        return _session;
                    Properties.Settings.UpgradeFromPreviousVersionIfNeeded();
                    _session = LoadFromSettings();
                    return _session;
                }
            }
        }

        /// <summary>Fired after <see cref="Apply"/> so the list can rebind and backfill can refresh.</summary>
        public event EventHandler Changed;

        /// <summary>When true and no watch list is set, hide rows from the signed-in user.</summary>
        public bool HideOwnChanges { get; private set; }

        /// <summary>
        /// When true, deleted location rows are left out of the card list.
        /// Defaults to true so a new session hides deletes until the user unchecks the box.
        /// </summary>
        public bool HideDeleted { get; private set; } = true;

        /// <summary>
        /// Comma/space-separated usernames to keep. Empty means everyone
        /// (subject to <see cref="HideOwnChanges"/>).
        /// </summary>
        public string WatchedUsersText { get; private set; } = "";

        /// <summary>
        /// Free-text structure id / label query. Empty means no structure filter.
        /// Parsed by <see cref="StructureOrLabelMatch"/>.
        /// </summary>
        public string StructureOrLabelText { get; private set; } = "";

        /// <summary>
        /// Section numbers/ranges (same syntax as volume-position update). Blank = all sections.
        /// </summary>
        public string SectionsText { get; private set; } = "";

        /// <summary>Last successful parse of <see cref="SectionsText"/>.</summary>
        public SectionRangeParse SectionsParse { get; private set; } =
            SectionRangeParser.Parse("");

        /// <summary>True when the sections box is blank (volume-wide).</summary>
        public bool IsAllSections => SectionsParse.IsAllSections;

        /// <summary>
        /// Updates preferences, optionally persists them, and raises <see cref="Changed"/>.
        /// </summary>
        public void Apply(
            bool hideOwnChanges,
            string watchedUsersText,
            string structureOrLabelText,
            string sectionsText = "",
            bool persist = true,
            bool hideDeleted = true)
        {
            HideOwnChanges = hideOwnChanges;
            HideDeleted = hideDeleted;
            WatchedUsersText = watchedUsersText?.Trim() ?? "";
            StructureOrLabelText = structureOrLabelText?.Trim() ?? "";
            SectionsText = sectionsText?.Trim() ?? "";
            SectionsParse = SectionRangeParser.Parse(SectionsText);
            if (persist)
                SaveToSettings();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// True when <paramref name="entry"/> should appear given these prefs and the signed-in user.
        /// </summary>
        public bool Allows(ReviewChangeEntry entry, string currentUsername)
        {
            if (entry is null)
                return false;

            if (HideDeleted && entry.IsDeleted)
                return false;

            if (!AllowsSection(entry))
                return false;

            if (!AllowsUser(entry.Username, currentUsername))
                return false;

            return StructureOrLabelMatch.Matches(
                entry.StructureId,
                entry.StructureLabel,
                StructureOrLabelText);
        }

        /// <summary>
        /// Section gate. Blank sections text accepts every row. A specific list requires a matching
        /// <see cref="ReviewChangeEntry.Section"/> (deleted rows with section 0 are hidden).
        /// </summary>
        public bool AllowsSection(ReviewChangeEntry entry)
        {
            if (entry is null)
                return false;

            SectionRangeParse parse = SectionsParse;
            if (!parse.Success)
                return false;

            if (parse.IsAllSections)
                return true;

            if (entry.Section <= 0)
                return false;

            foreach (long section in parse.Sections)
            {
                if (section == entry.Section)
                    return true;
            }

            return false;
        }

        bool AllowsUser(string entryUsername, string currentUsername)
        {
            IReadOnlyList<string> watched = StructureOrLabelMatch.Tokenize(WatchedUsersText);
            if (watched.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(entryUsername))
                    return false;
                foreach (string name in watched)
                {
                    if (string.Equals(name, entryUsername, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }

            if (!HideOwnChanges)
                return true;

            if (string.IsNullOrWhiteSpace(currentUsername) || string.IsNullOrWhiteSpace(entryUsername))
                return true;

            return !string.Equals(entryUsername, currentUsername, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Re-reads user.config into <see cref="Session"/> without writing.
        /// Called when the Review tab opens so a late settings upgrade is still visible.
        /// </summary>
        public static void ReloadSessionFromSettings()
        {
            lock (SessionGate)
            {
                Properties.Settings.UpgradeFromPreviousVersionIfNeeded();
                ReviewChangeFeedFilter loaded = LoadFromSettings();
                if (_session is null)
                {
                    _session = loaded;
                    return;
                }

                _session.Apply(
                    loaded.HideOwnChanges,
                    loaded.WatchedUsersText,
                    loaded.StructureOrLabelText,
                    loaded.SectionsText,
                    persist: false,
                    hideDeleted: loaded.HideDeleted);
            }
        }

        /// <summary>
        /// Builds a filter from <see cref="Properties.Settings.Default"/>.
        /// Tests and <see cref="Session"/> use this after settings upgrade.
        /// </summary>
        internal static ReviewChangeFeedFilter LoadFromSettings()
        {
            var filter = new ReviewChangeFeedFilter();
            try
            {
                Properties.Settings s = Properties.Settings.Default;
                filter.HideOwnChanges = s.ReviewFeedHideOwnChanges;
                filter.HideDeleted = s.ReviewFeedHideDeleted;
                filter.WatchedUsersText = s.ReviewFeedWatchedUsers ?? "";
                filter.StructureOrLabelText = s.ReviewFeedStructureOrLabel ?? "";
                filter.SectionsText = s.ReviewFeedSections ?? "";
                filter.SectionsParse = SectionRangeParser.Parse(filter.SectionsText);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(
                    $"Review feed filter load: {ex.Message}",
                    "WebAnnotation");
                // First run or missing keys — keep defaults (all sections).
                filter.SectionsParse = SectionRangeParser.Parse("");
            }

            return filter;
        }

        void SaveToSettings()
        {
            try
            {
                Properties.Settings s = Properties.Settings.Default;
                s.ReviewFeedHideOwnChanges = HideOwnChanges;
                s.ReviewFeedHideDeleted = HideDeleted;
                s.ReviewFeedWatchedUsers = WatchedUsersText;
                s.ReviewFeedStructureOrLabel = StructureOrLabelText;
                s.ReviewFeedSections = SectionsText;
                s.Save();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(
                    $"Review feed filter save: {ex.Message}",
                    "WebAnnotation");
                // Preferences still apply for this session if persist fails.
            }
        }
    }
}
