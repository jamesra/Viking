using Geometry;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Viking.AnnotationServiceTypes;
using Viking.Common;
using Viking.ViewModels;
using WebAnnotationModel;

namespace WebAnnotation.ViewModel
{
    /// <summary>
    /// Track location links for a section
    /// </summary>
    internal class SectionLocationLinkAnnotationsViewModel(SectionViewModel section)
    {
        protected readonly KeyTracker<LocationLinkKey> KnownLinks = new();

        /// <summary>
        /// The ID's of locations on the adjacent section which we know are linked and overlapped.
        /// </summary>
        public readonly RefCountingKeyTracker<long> OverlappedAdjacentLocationIDs = new();

        protected readonly ConcurrentDictionary<LocationLinkKey, LocationLinkView> LocationLinks = new();

        public readonly KeyTracker<LocationLinkKey> OverlappedLinkKeys = new();
        public readonly RTree.RTree<LocationLinkKey> NonOverlappedLinksSearch = new();

        /// <summary>
        /// The section that we represent links on the canvas for
        /// </summary>
        public readonly SectionViewModel Section = section;

        public void AddLocationLinks(IEnumerable<LocationObj> locations) => locations.ForEach(loc => AddLocationLinks(loc, true));

        public void RemoveLocationLinks(IEnumerable<LocationObj> locations) => locations.ForEach(loc => RemoveLocationLinks(loc, true));

        public void AddLocationLinks(IEnumerable<LocationLinkKey> links) => links.ForEach(link => AddLocationLink(link, true));

        public void RemoveLocationLinks(IEnumerable<LocationLinkKey> links) => links.ForEach(link => RemoveLocationLink(link, true));

        protected void AddLocationLinks(LocationObj loc, bool subscribe)
        {
            foreach (long linkedID in loc.LinksCopy)
            {
                LocationLinkKey linkKey = new(loc.ID, linkedID);
                AddLocationLink(linkKey, subscribe);
            }
        }

        protected void RemoveLocationLinks(LocationObj loc, bool unsubscribe)
        {
            foreach (long linkedID in loc.LinksCopy)
            {
                LocationLinkKey linkKey = new(loc.ID, linkedID);
                RemoveLocationLink(linkKey, unsubscribe);
            }
        }

        /// <summary>
        /// Adds the link <paramref name="key"/> if one end is on this section. When the link's ends overlap, its adjacent
        /// end becomes hidden (drawn only as the up/down symbol), so with <paramref name="repointNeighbours"/> the other
        /// links to that location are rebuilt to end on the covering location (see <see cref="RepointLinksOfAdjacentEnds"/>).
        /// </summary>
        protected void AddLocationLink(LocationLinkKey key, bool subscribe, bool repointNeighbours = true)
        {
            if (!Store.Locations.TryGetValue(key.A, out LocationObj AOBj))
            {
                return;
            }

            if (!Store.Locations.TryGetValue(key.B, out LocationObj BOBj))
            {
                return;
            }

            if (!(AOBj.Z == Section.Number || BOBj.Z == Section.Number))
            {
                return;
            }

            if (!LocationLinkView.TryCreate(key, Section.Number, Section.VolumeViewModel, out LocationLinkView lv))
            {
                return;
            }

            // KnownLinks is the source of truth for membership. This callback runs under its write lock,
            // so it must not Debug.Assert (a modal dialog here blocks every reader of the tracker and
            // freezes the UI) and must not leave a half-added entry: the Func overload adds the key only when
            // the callback returns true (not when it returns false or throws), but LocationLinks is ours to
            // clean up. A leftover entry would otherwise make every later add of the same link fail.
            bool becameOverlapped = false;
            KnownLinks.TryAdd(key, () =>
            {
                // An endpoint can leave the store after the checks above (deleted, or evicted when another
                // section opens); the link goes with it, so it is skipped without an exception.
                if (!lv.TryLinksOverlap(out bool overlaps))
                {
                    return false;
                }

                LocationLinks[key] = lv;
                try
                {
                    if (overlaps)
                    {
                        OverlappedLinkKeys.TryAdd(key, () =>
                        {
                            OverlappedAdjacentLocationIDs.AddRef(key.A);
                            OverlappedAdjacentLocationIDs.AddRef(key.B);
                            becameOverlapped = true;
                        });
                    }
                    else
                    {
                        NonOverlappedLinksSearch.Add(lv.BoundingBox.ToRTreeRect(lv.Z), key);
                    }
                }
                catch (System.Exception ex)
                {
                    LocationLinks.TryRemove(key, out _);
                    Trace.WriteLine($"AddLocationLink {key.A}-{key.B} on section {Section.Number} failed and was rolled back: {ex}");
                    throw;
                }

                return true;
            });

            // Outside the tracker's lock: rebuilding adds and removes links through the same trackers.
            if (repointNeighbours && becameOverlapped)
                RepointLinksOfAdjacentEnds(key);
        }

        /// <summary>
        /// Removes the link <paramref name="key"/>. When it was an overlapped link, its adjacent end is drawn again, so with
        /// <paramref name="repointNeighbours"/> the other links to that location are rebuilt to end on it once more.
        /// </summary>
        protected void RemoveLocationLink(LocationLinkKey key, bool unsubscribe, bool repointNeighbours = true)
        {
            bool wasOverlapped = false;
            KnownLinks.TryRemove(key, () =>
            {
                // No Debug.Assert here: this runs under the tracker's write lock, and a stale or already-cleaned
                // entry is harmless on removal.
                LocationLinks.TryRemove(key, out _);

                if (OverlappedLinkKeys.Contains(key))
                {
                    OverlappedLinkKeys.TryRemove(key, () =>
                    {
                        OverlappedAdjacentLocationIDs.ReleaseRef(key.A);
                        OverlappedAdjacentLocationIDs.ReleaseRef(key.B);
                        wasOverlapped = true;
                    });
                }

                if (NonOverlappedLinksSearch.Contains(key))
                {
                    NonOverlappedLinksSearch.Delete(key, out LocationLinkKey removedKey);
                }
            });

            if (repointNeighbours && wasOverlapped)
                RepointLinksOfAdjacentEnds(key);
        }

        /// <summary>
        /// Rebuilds the links, other than <paramref name="changed"/>, that touch an end of <paramref name="changed"/> on an
        /// adjacent section, because that location just became hidden under, or visible from under, a location on this
        /// section. Each link view decides its ends when it is built (LocationLinkView.CoveringLocation), so rebuilding
        /// re-points it without changing section. Rebuilt links do not re-point their own neighbours, so this cannot loop.
        /// Call outside the trackers' locks.
        /// </summary>
        private void RepointLinksOfAdjacentEnds(LocationLinkKey changed)
        {
            IEnumerable<LocationLinkKey> neighbours = LinksToRepoint(changed, Section.Number,
                id => Store.Locations.TryGetValue(id, out LocationObj end) ? (end.Z, end.LinksCopy) : null,
                KnownLinks.Contains);

            foreach (LocationLinkKey neighbour in neighbours.ToList())
            {
                RemoveLocationLink(neighbour, false, repointNeighbours: false);
                AddLocationLink(neighbour, false, repointNeighbours: false);
            }
        }

        /// <summary>
        /// The known links, other than <paramref name="changed"/>, that touch an end of <paramref name="changed"/> lying off
        /// <paramref name="sectionNumber"/>. <paramref name="location"/> gives a location's section and linked IDs, or null
        /// when it is not loaded; <paramref name="isKnown"/> says whether this section tracks a link.
        /// </summary>
        internal static IEnumerable<LocationLinkKey> LinksToRepoint(LocationLinkKey changed, int sectionNumber,
            System.Func<long, (double Z, long[] Links)?> location, System.Func<LocationLinkKey, bool> isKnown)
        {
            foreach (long endID in new[] { changed.A, changed.B })
            {
                if (location(endID) is not { } end || end.Z == sectionNumber)
                    continue;

                foreach (long linkedID in end.Links)
                {
                    if (linkedID == endID)
                        continue;

                    LocationLinkKey neighbour = new(endID, linkedID);
                    if (!neighbour.Equals(changed) && isKnown(neighbour))
                        yield return neighbour;
                }
            }
        }

        public List<HitTestResult> GetAnnotationsAtPosition(Vector2 WorldPosition)
        {
            IEnumerable<LocationLinkView> intersecting_objs = KeysToViews(NonOverlappedLinksSearch.Intersects(WorldPosition.ToRTreeRect(Section.Number))).Where(l => l.Contains(WorldPosition));

            return [.. intersecting_objs.Select(l => new HitTestResult(l, Section.Number, ((ICanvasView)l).VisualHeight, l.DistanceFromCenterNormalized(WorldPosition)))];
        }

        /// <summary>
        /// Maps R-tree keys to their views, skipping keys whose link was removed since the R-tree was read.
        /// <see cref="RemoveLocationLink"/> drops the <see cref="LocationLinks"/> entry before the R-tree entry
        /// and readers take no lock, so a key from the tree can already be gone from the dictionary.
        /// </summary>
        private List<LocationLinkView> KeysToViews(ICollection<LocationLinkKey> listKeys) => LocationLinks.ValuesForKeys(listKeys);

        public ICollection<LocationLinkView> NonOverlappedLinks => KeysToViews(NonOverlappedLinksSearch.Items);

        public ICollection<LocationLinkView> NonOverlappedLinksInRegion(Rectangle region)
        {
            List<LocationLinkKey> listKeys = NonOverlappedLinksSearch.Intersects(region.ToRTreeRect(Section.Number));
            return KeysToViews(listKeys);
        }

        public ICollection<LocationLinkView> GetLocationLinks(Vector2 point)
        {
            return [.. KeysToViews(NonOverlappedLinksSearch.Intersects(point.ToRTreeRect((float)Section.Number))).Where(l => l.Contains(point))];
        }

        public ICollection<LocationLinkView> GetLocationLinks(LineSegment line)
        {
            return [.. KeysToViews(NonOverlappedLinksSearch.Intersects(line.BoundingBox.ToRTreeRect((float)Section.Number))).Where(l => l.Intersects(line))];
        }

        public ICollection<LocationLinkView> GetLocationLinks(Rectangle rect)
        {
            return KeysToViews(NonOverlappedLinksSearch.Intersects(rect.ToRTreeRect((float)Section.Number)));
        }
    }
}
