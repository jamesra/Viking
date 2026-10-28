using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using WebAnnotationModel.Objects;

namespace WebAnnotation.ViewModel
{
    /// <summary>
    /// The views a section draws for its locations: at most one view per location ID, the spatial index over their
    /// bounding boxes, and the <see cref="LocationObj"/> each view was built from and whose change events are heard.
    /// <see cref="SectionAnnotationsView"/> owns one; it is generic over the view type so tests can use a plain view.
    /// </summary>
    /// <remarks>
    /// The store, read through the <c>currentObject</c> delegate, is the truth. A view is always built from the object the
    /// store holds for the ID now, and the subscription follows that object. The store can hold a new object for an ID (a
    /// section evicted and loaded again, while a region load hands back the object it collected before the eviction). When
    /// views, subscriptions and "known" IDs were all keyed by ID alone, the view kept the old object and the new one was
    /// never subscribed: moving the location saved it but left it drawn at its old place for good.
    /// <para>
    /// Thread safe: store events and region loads arrive on pool threads, property changes on the UI thread. The view
    /// factory, the subscribe and unsubscribe delegates and the added/removed callbacks run under the index's lock, so
    /// they must not wait on another thread that takes it.
    /// </para>
    /// </remarks>
    internal sealed class LocationViewIndex<TView> where TView : class
    {
        private readonly object sync = new();

        /// <summary>The object each view in <see cref="Views"/> was built from.</summary>
        private readonly Dictionary<long, LocationObj> builtFrom = [];

        /// <summary>The object whose events are heard for each ID; it can exist without a view (the view could not be built).</summary>
        private readonly Dictionary<long, LocationObj> subscribed = [];

        private readonly Func<long, LocationObj?> currentObject;
        private readonly Func<LocationObj, TView?> createView;
        private readonly Func<TView, RTree.Rectangle> bounds;
        private readonly Action<LocationObj> subscribe;
        private readonly Action<LocationObj> unsubscribe;
        private readonly Action<LocationObj, TView>? onAdded;
        private readonly Action<LocationObj, TView>? onRemoved;

        /// <summary>The view for each location ID. Read freely; change it only through this index.</summary>
        public readonly ConcurrentDictionary<long, TView> Views = new();

        /// <summary>Bounding boxes of <see cref="Views"/>, keyed by location ID. Read freely; change it only through this index.</summary>
        public readonly RTree.RTree<long> Search = new();

        /// <param name="currentObject">The store's object for an ID now, or null when the store does not hold it.</param>
        /// <param name="createView">Builds a view; returns null when the location cannot be drawn (for example it does not map).</param>
        /// <param name="bounds">The view's box in <see cref="Search"/>.</param>
        /// <param name="subscribe">Starts hearing an object's property changes.</param>
        /// <param name="unsubscribe">Stops hearing an object's property changes.</param>
        /// <param name="onAdded">Called after a view is registered, with the object it was built from.</param>
        /// <param name="onRemoved">Called after a view is removed, with the object it was built from. Also undoes a partial <paramref name="onAdded"/>.</param>
        public LocationViewIndex(Func<long, LocationObj?> currentObject,
                                 Func<LocationObj, TView?> createView,
                                 Func<TView, RTree.Rectangle> bounds,
                                 Action<LocationObj> subscribe,
                                 Action<LocationObj> unsubscribe,
                                 Action<LocationObj, TView>? onAdded = null,
                                 Action<LocationObj, TView>? onRemoved = null)
        {
            this.currentObject = currentObject ?? throw new ArgumentNullException(nameof(currentObject));
            this.createView = createView ?? throw new ArgumentNullException(nameof(createView));
            this.bounds = bounds ?? throw new ArgumentNullException(nameof(bounds));
            this.subscribe = subscribe ?? throw new ArgumentNullException(nameof(subscribe));
            this.unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
            this.onAdded = onAdded;
            this.onRemoved = onRemoved;
        }

        /// <summary>True when <paramref name="id"/> has a view built from the store's current object for it.</summary>
        public bool HasCurrentView(long id)
        {
            lock (sync)
            {
                return builtFrom.TryGetValue(id, out LocationObj from) && ReferenceEquals(from, currentObject(id));
            }
        }

        /// <summary>
        /// Brings the view and subscription for <paramref name="loc"/>'s ID up to date with the store: builds the view when
        /// there is none or it was built from another object, and hears the store's object. <paramref name="loc"/> itself
        /// is only used for its ID, since a caller can hold an object the store has since replaced.
        /// </summary>
        /// <returns>True when a view was built.</returns>
        public bool Add(LocationObj loc)
        {
            lock (sync)
            {
                return SyncLocked(loc.ID, rebuild: false);
            }
        }

        /// <summary>Removes whatever view <paramref name="id"/> has and builds it again from the store's current object.</summary>
        /// <returns>True when a view was built.</returns>
        public bool Rebuild(long id)
        {
            lock (sync)
            {
                return SyncLocked(id, rebuild: true);
            }
        }

        /// <summary>Removes the view for <paramref name="id"/> and keeps hearing its object (its geometry is about to change).</summary>
        public void RemoveView(long id)
        {
            lock (sync)
            {
                RemoveViewLocked(id);
            }
        }

        /// <summary>
        /// The store dropped <paramref name="loc"/>: removes its view and stops hearing it, whether or not it had a view.
        /// When the store already holds a different object for the ID (its add was handled first), the view and
        /// subscription move to that object instead.
        /// </summary>
        public void Remove(LocationObj loc)
        {
            lock (sync)
            {
                LocationObj? current = currentObject(loc.ID);
                if (current is not null && !ReferenceEquals(current, loc))
                {
                    SyncLocked(loc.ID, rebuild: false);
                    return;
                }

                RemoveViewLocked(loc.ID);
                UnsubscribeLocked(loc.ID);
            }
        }

        private bool SyncLocked(long id, bool rebuild)
        {
            LocationObj? current = currentObject(id);
            if (current is null)
            {
                RemoveViewLocked(id);
                UnsubscribeLocked(id);
                return false;
            }

            SubscribeLocked(id, current);

            if (!rebuild && builtFrom.TryGetValue(id, out LocationObj from) && ReferenceEquals(from, current))
                return false;

            RemoveViewLocked(id);

            TView? view = createView(current);
            if (view is null)
                return false;

            Views[id] = view;
            builtFrom[id] = current;
            bool indexed = false;
            try
            {
                Search.Add(bounds(view), id);
                indexed = true;
                onAdded?.Invoke(current, view);
            }
            catch
            {
                // Leave nothing half registered: a view the spatial index or the "known" check misses is drawn but never
                // removed, and blocked every later rebuild of the location.
                Views.TryRemove(id, out _);
                builtFrom.Remove(id);
                Search.Delete(id, out _);
                if (indexed)
                    onRemoved?.Invoke(current, view);
                throw;
            }

            return true;
        }

        private void RemoveViewLocked(long id)
        {
            builtFrom.TryGetValue(id, out LocationObj? from);
            builtFrom.Remove(id);
            bool hadView = Views.TryRemove(id, out TView? view);

            // A miss is fine: the view may never have been built, or a failed add already took it out.
            Search.Delete(id, out _);

            if (hadView && from is not null && view is not null)
                onRemoved?.Invoke(from, view);
        }

        private void SubscribeLocked(long id, LocationObj current)
        {
            if (subscribed.TryGetValue(id, out LocationObj heard))
            {
                if (ReferenceEquals(heard, current))
                    return;

                subscribed.Remove(id);
                unsubscribe(heard);
            }

            subscribe(current);
            subscribed[id] = current;
        }

        private void UnsubscribeLocked(long id)
        {
            if (subscribed.TryGetValue(id, out LocationObj heard))
            {
                subscribed.Remove(id);
                unsubscribe(heard);
            }
        }
    }
}
