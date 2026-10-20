using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.ReviewFeed
{
    /// <summary>
    /// Single entry point that turns store locations into Review feed rows.
    /// Used by the visible-section poll, local CollectionChanged adds/replaces, and shape saves
    /// (e.g. accept autosegment) so every path shares the Review-access gate.
    /// </summary>
    public static class ReviewChangeFeedIngress
    {
        /// <summary>
        /// Forwards locations into <see cref="ReviewChangeFeed.Session"/> when the session has Review access.
        /// Does not apply the poll watermark — use for local creates/updates the user just made and
        /// for locations the store adds back after a refresh. A live row replaces a deleted row for
        /// the same id (<see cref="ReviewChangeFeed.ApplyLocalUpserts"/>).
        /// </summary>
        public static void PushUpserts(IEnumerable<LocationObj> locations)
        {
            PushUpserts(locations, requirePollCutoff: false);
        }

        /// <summary>
        /// Section-poll path: only rows at or after <see cref="ReviewChangeFeed.PollIngressCutoffUtc"/>
        /// (or the default lookback when no seed has run) enter the feed. Prevents a full-section
        /// dump from replacing the OData newest-N seed.
        /// </summary>
        public static void PushUpsertsFromPoll(IEnumerable<LocationObj> locations)
        {
            PushUpserts(locations, requirePollCutoff: true);
        }

        /// <summary>
        /// A location store CollectionChanged: removes become deleted rows, and local adds/replaces (new structures,
        /// accepted segmentations), which do not go through the section poll, become upserts. Viking's overlay and
        /// Jotunn's scene both forward the store's events here.
        /// </summary>
        public static void PushLocationStoreChange(NotifyCollectionChangedEventArgs e)
        {
            if (e is null)
                return;

            if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems is { Count: > 0 })
            {
                if (!VolumeAccessRoles.HasReviewAccess())
                    return;

                var ids = new List<long>(e.OldItems.Count);
                foreach (object item in e.OldItems)
                {
                    if (item is LocationObj loc)
                        ids.Add(loc.ID);
                }

                if (ids.Count > 0)
                    ReviewChangeFeed.Session.ApplyDeletes(ids, DateTime.UtcNow);
                return;
            }

            if ((e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace)
                && e.NewItems is { Count: > 0 })
            {
                var locations = new List<LocationObj>(e.NewItems.Count);
                foreach (object item in e.NewItems)
                {
                    if (item is LocationObj loc)
                        locations.Add(loc);
                }

                PushUpserts(locations);
            }
        }

        /// <summary>Convenience for a single saved or created location.</summary>
        public static void PushUpsert(LocationObj location)
        {
            if (location is null)
                return;
            PushUpserts(new[] { location }, requirePollCutoff: false);
        }

        static void PushUpserts(IEnumerable<LocationObj> locations, bool requirePollCutoff)
        {
            if (locations is null)
                return;
            if (!VolumeAccessRoles.HasReviewAccess())
                return;

            ReviewChangeFeed feed = ReviewChangeFeed.Session;
            var entries = new List<ReviewChangeEntry>();
            foreach (LocationObj loc in locations)
            {
                if (loc is null)
                    continue;
                if (requirePollCutoff && !feed.AllowsPollArrival(loc.LastModified))
                    continue;
                try
                {
                    entries.Add(ReviewChangeEntry.FromLocation(loc));
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Review feed skip location {loc.ID}: {ex.Message}", "WebAnnotation");
                }
            }

            if (entries.Count == 0)
                return;
            if (requirePollCutoff)
                feed.ApplyUpserts(entries);
            else
                feed.ApplyLocalUpserts(entries);
        }
    }
}
