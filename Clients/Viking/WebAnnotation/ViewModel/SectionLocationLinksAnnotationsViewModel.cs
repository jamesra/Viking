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

        protected void AddLocationLink(LocationLinkKey key, bool subscribe)
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

            // KnownLinks is the source of truth for membership. This action runs under its write lock,
            // so it must not Debug.Assert (a modal dialog here blocks every reader of the tracker and
            // freezes the UI) and must not leave a half-added entry: KeyTracker rolls the key back when
            // the action throws, but LocationLinks is ours to clean up. A leftover entry would otherwise
            // make every later add of the same link fail.
            try
            {
                KnownLinks.TryAdd(key, () =>
                {
                    LocationLinks[key] = lv;

                    try
                    {
                        if (lv.LinksOverlap())
                        {
                            OverlappedLinkKeys.TryAdd(key, () =>
                            {
                                OverlappedAdjacentLocationIDs.AddRef(key.A);
                                OverlappedAdjacentLocationIDs.AddRef(key.B);
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
                });
            }
            catch (KeyNotFoundException)
            {
                // An endpoint was deleted after the TryGetValue checks above (LinksOverlap re-reads the store).
                // The tracker and LocationLinks are already rolled back, and the link is gone with its location,
                // so skip it rather than abort the caller's loop over the remaining links.
            }
        }

        protected void RemoveLocationLink(LocationLinkKey key, bool unsubscribe)
        {
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
                    });
                }

                if (NonOverlappedLinksSearch.Contains(key))
                {
                    NonOverlappedLinksSearch.Delete(key, out LocationLinkKey removedKey);
                }
            });
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
        private List<LocationLinkView> KeysToViews(ICollection<LocationLinkKey> listKeys)
        {
            List<LocationLinkView> listLocLinkView = new(listKeys.Count);
            foreach (LocationLinkKey linkKey in listKeys)
            {
                if (LocationLinks.TryGetValue(linkKey, out LocationLinkView locLinkView))
                {
                    listLocLinkView.Add(locLinkView);
                }
            }

            return listLocLinkView;
        }

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
