using Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Viking.AnnotationServiceTypes;
using Viking.Common;
using Viking.VolumeModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.ViewModel
{
    /// <summary>
    /// Track location links for a section
    /// </summary>
    internal class SectionLocationLinkAnnotationsViewModel(int sectionNumber, IVolumeTransformProvider transforms, Func<long, bool> onScreenLocationHasView)
    {
        protected readonly KeyTracker<LocationLinkKey> KnownLinks = new();

        /// <summary>
        /// Links that arrived before both endpoints were in the store or the on-screen
        /// location had a canvas view. Retried by <see cref="RetryPendingLinks"/>.
        /// </summary>
        private readonly ConcurrentDictionary<LocationLinkKey, byte> PendingLinks = new();

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
        public readonly int SectionNumber = sectionNumber;

        public readonly IVolumeTransformProvider Transforms = transforms;

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
        /// Re-attempt links held because an endpoint or on-screen view was not ready.
        /// Called after location views are created or parent structures arrive.
        /// </summary>
        public void RetryPendingLinks()
        {
            foreach (LocationLinkKey key in PendingLinks.Keys)
            {
                if (TryAddLocationLinkNow(key, subscribe: true))
                    PendingLinks.TryRemove(key, out _);
            }
        }

        protected void AddLocationLink(LocationLinkKey key, bool subscribe)
        {
            if (TryAddLocationLinkNow(key, subscribe))
            {
                PendingLinks.TryRemove(key, out _);
                return;
            }

            PendingLinks.TryAdd(key, 0);
        }

        /// <summary>
        /// Creates the canvas link when both endpoints are in the store, at least one
        /// is on this section, and the on-section location already has a view.
        /// </summary>
        private bool TryAddLocationLinkNow(LocationLinkKey key, bool subscribe)
        {
            if (KnownLinks.Contains(key))
                return true;

            if (!Store.Locations.TryGetObjectByID(key.A, out LocationObj AOBj))
                return false;

            if (!Store.Locations.TryGetObjectByID(key.B, out LocationObj BOBj))
                return false;

            if (!(AOBj.Z == SectionNumber || BOBj.Z == SectionNumber))
                return false;

            if (AOBj.Z == SectionNumber && !onScreenLocationHasView(AOBj.ID))
                return false;

            if (BOBj.Z == SectionNumber && !onScreenLocationHasView(BOBj.ID))
                return false;

            if (!LocationLinkView.TryCreate(key, SectionNumber, Transforms, out LocationLinkView? lv) || lv is null)
                return false;

            KnownLinks.TryAdd(key, () =>
            {
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

            return KnownLinks.Contains(key);
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

        public List<HitTestResult> GetAnnotationsAtPosition(Vector2 WorldPosition)
        {
            IEnumerable<LocationLinkKey> intersecting_IDs = NonOverlappedLinksSearch.Intersects(WorldPosition.ToRTreeRect(SectionNumber));
            IEnumerable<LocationLinkView> intersecting_objs = intersecting_IDs.Select(id => LocationLinks[id]).Where(l => l.Contains(WorldPosition));

            return [.. intersecting_objs.Select(l => new HitTestResult(l, SectionNumber, ((ICanvasView)l).VisualHeight, l.DistanceFromCenterNormalized(WorldPosition)))];
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

        public ICollection<LocationLinkView> NonOverlappedLinksInRegion(Rectangle region)
        {
            List<LocationLinkKey> listKeys = NonOverlappedLinksSearch.Intersects(region.ToRTreeRect(SectionNumber));
            return KeysToViews(listKeys);
        }

        public ICollection<LocationLinkView> GetLocationLinks(Vector2 point)
        {
            List<LocationLinkKey> intersectingIDs = NonOverlappedLinksSearch.Intersects(point.ToRTreeRect((float)SectionNumber));
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

        public ICollection<LocationLinkView> GetLocationLinks(LineSegment line)
        {
            List<LocationLinkKey> intersectingIDs = NonOverlappedLinksSearch.Intersects(line.BoundingBox.ToRTreeRect((float)SectionNumber));
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

        public ICollection<LocationLinkView> GetLocationLinks(Rectangle rect)
        {
            List<LocationLinkKey> intersectingIDs = NonOverlappedLinksSearch.Intersects(rect.ToRTreeRect((float)SectionNumber));
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
