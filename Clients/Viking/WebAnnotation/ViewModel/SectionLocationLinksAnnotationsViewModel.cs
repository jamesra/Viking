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

            try
            {
                KnownLinks.TryAdd(key, () =>
                {
                    LocationLinkView lv = new(key, Section.Number, Section.VolumeViewModel);
                    bool added = LocationLinks.TryAdd(key, lv);
                    Debug.Assert(added);

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
                });
            }
            catch (System.ArgumentOutOfRangeException e)
            {
                //This can occur when the point cannot be mapped
                System.Diagnostics.Trace.WriteLine($"Exception adding location link {key}\n{e}");
            }

        }

        protected void RemoveLocationLink(LocationLinkKey key, bool unsubscribe)
        {
            KnownLinks.TryRemove(key, () =>
            {
                Debug.Assert(LocationLinks.ContainsKey(key));
                bool removed = LocationLinks.TryRemove(key, out LocationLinkView lv);
                Debug.Assert(removed);

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

        public List<HitTestResult> GetAnnotationsAtPosition(GridVector2 WorldPosition)
        {
            IEnumerable<LocationLinkKey> intersecting_IDs = NonOverlappedLinksSearch.Intersects(WorldPosition.ToRTreeRect(Section.Number));
            IEnumerable<LocationLinkView> intersecting_objs = intersecting_IDs.Select(id => LocationLinks[id]).Where(l => l.Contains(WorldPosition));

            return [.. intersecting_objs.Select(l => new HitTestResult(l, Section.Number, ((ICanvasView)l).VisualHeight, l.DistanceFromCenterNormalized(WorldPosition)))];
        }

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

        public ICollection<LocationLinkView> NonOverlappedLinksInRegion(GridRectangle region)
        {
            List<LocationLinkKey> listKeys = NonOverlappedLinksSearch.Intersects(region.ToRTreeRect(Section.Number));
            return KeysToViews(listKeys);
        }

        public ICollection<LocationLinkView> GetLocationLinks(GridVector2 point)
        {
            List<LocationLinkKey> intersectingIDs = NonOverlappedLinksSearch.Intersects(point.ToRTreeRect((float)Section.Number));
            return [.. intersectingIDs.Select(id =>
            {
                if (LocationLinks.ContainsKey(id))
                {
                    return LocationLinks[id];
                }

                return null;
            }
            ).Where(l => l != null && l.Contains(point))];
        }

        public ICollection<LocationLinkView> GetLocationLinks(GridLineSegment line)
        {
            List<LocationLinkKey> intersectingIDs = NonOverlappedLinksSearch.Intersects(line.BoundingBox.ToRTreeRect((float)Section.Number));
            return [.. intersectingIDs.Select(id =>
            {
                if (LocationLinks.ContainsKey(id))
                {
                    return LocationLinks[id];
                }

                return null;
            }
            ).Where(l => l != null && l.Intersects(line))];
        }

        public ICollection<LocationLinkView> GetLocationLinks(GridRectangle rect)
        {
            List<LocationLinkKey> intersectingIDs = NonOverlappedLinksSearch.Intersects(rect.ToRTreeRect((float)Section.Number));
            return [.. intersectingIDs.Select(id =>
            {
                if (LocationLinks.ContainsKey(id))
                {
                    return LocationLinks[id];
                }

                return null;
            }
            ).Where(l => l != null)];
        }
    }
}
