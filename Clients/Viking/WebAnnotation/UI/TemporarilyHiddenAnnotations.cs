using System;
using System.Collections.Generic;
using System.Linq;
using WebAnnotation.View;

namespace WebAnnotation.UI
{
    /// <summary>
    /// Annotations on the section being viewed that the user hid with Del for a while, so an annotation beneath them
    /// (usually one from an adjacent section) can be reached. A hidden annotation is neither drawn nor hit-tested until
    /// its time runs out or Insert restores it. Thread-safe: drawing, mouse handling and the expiry timer all read it.
    /// </summary>
    internal sealed class TemporarilyHiddenAnnotations(Func<DateTime> utcNow)
    {
        private readonly object gate = new();
        private readonly Dictionary<long, DateTime> expiryByLocationId = [];

        public TemporarilyHiddenAnnotations() : this(() => DateTime.UtcNow)
        {
        }

        /// <summary>True when no annotation is hidden, so callers can skip filtering.</summary>
        public bool IsEmpty
        {
            get
            {
                lock (gate)
                {
                    Prune();
                    return expiryByLocationId.Count == 0;
                }
            }
        }

        /// <summary>Hides <paramref name="locationId"/> for <paramref name="duration"/>, restarting the time if it is already hidden.</summary>
        public void Hide(long locationId, TimeSpan duration)
        {
            lock (gate)
            {
                expiryByLocationId[locationId] = utcNow() + duration;
            }
        }

        /// <summary>Brings <paramref name="locationId"/> back now. False when it was not hidden.</summary>
        public bool Restore(long locationId)
        {
            lock (gate)
            {
                Prune();
                return expiryByLocationId.Remove(locationId);
            }
        }

        /// <summary>True while <paramref name="locationId"/> is hidden and its time has not run out.</summary>
        public bool IsHidden(long locationId)
        {
            lock (gate)
            {
                return expiryByLocationId.TryGetValue(locationId, out DateTime expiry) && expiry > utcNow();
            }
        }

        /// <summary>
        /// How long until the next hidden annotation reappears, or null when none is hidden. A redraw is due then.
        /// </summary>
        public TimeSpan? TimeUntilNextExpiry()
        {
            lock (gate)
            {
                Prune();
                if (expiryByLocationId.Count == 0)
                    return null;

                return expiryByLocationId.Values.Min() - utcNow();
            }
        }

        /// <summary>The hits that are not a hidden annotation.</summary>
        public List<HitTestResult> Exclude(IEnumerable<HitTestResult> hits)
        {
            if (IsEmpty)
                return [.. hits];

            return [.. hits.Where(h => !(h.obj is LocationCanvasView view && IsHidden(view.ID)))];
        }

        /// <summary>The locations that are not hidden.</summary>
        public List<LocationCanvasView> Exclude(IEnumerable<LocationCanvasView> locations)
        {
            if (IsEmpty)
                return [.. locations];

            return [.. locations.Where(l => !IsHidden(l.ID))];
        }

        /// <summary>
        /// The annotation Del hides: the one on <paramref name="sectionNumber"/> that the mouse is over and that sits on top
        /// (the same ranking a click uses), ignoring annotations already hidden and anything on another section.
        /// </summary>
        /// <param name="hits">Every hit at the mouse position, hidden or not.</param>
        public LocationCanvasView? PickToHide(IEnumerable<HitTestResult> hits, int sectionNumber)
        {
            List<HitTestResult> onSection = [.. hits.Where(h => h.obj is LocationCanvasView view && h.Z == sectionNumber && !IsHidden(view.ID))];
            HitTestResult? best = onSection.NearestObjectOnCurrentSectionThenAdjacent(sectionNumber);
            return best is { Z: var z } && z == sectionNumber && best.Distance <= 1.0 ? best.obj as LocationCanvasView : null;
        }

        /// <summary>
        /// The hidden annotation Insert restores: among the hidden annotations on <paramref name="sectionNumber"/> that the
        /// mouse is over, the one nearest its centre. A hidden annotation is not hit-tested normally, so
        /// <paramref name="hits"/> must include hidden ones.
        /// </summary>
        public LocationCanvasView? PickToRestore(IEnumerable<HitTestResult> hits, int sectionNumber)
        {
            return hits
                .Where(h => h.obj is LocationCanvasView view && h.Z == sectionNumber && IsHidden(view.ID))
                .OrderBy(h => h.Distance)
                .Select(h => h.obj as LocationCanvasView)
                .FirstOrDefault();
        }

        private void Prune()
        {
            DateTime now = utcNow();
            foreach (long id in expiryByLocationId.Where(e => e.Value <= now).Select(e => e.Key).ToList())
                expiryByLocationId.Remove(id);
        }
    }
}
