using System;
using System.Collections.Generic;
using System.Linq;
using Viking.AnnotationServiceTypes;
using VikingXNAGraphics;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotation.View;
using WebAnnotation.ViewModel;

namespace WebAnnotation.UI
{
    /// <summary>
    /// Annotations the user hid with Del for a while, so an annotation beneath them (often one from an adjacent section)
    /// can be reached. A hidden annotation is neither drawn nor hit-tested until its time runs out or Insert restores it.
    /// During the last <see cref="FadeFraction"/> of the time it fades back in (a quarter sine wave, from invisible to fully
    /// opaque) but is still not clickable, so the user sees it return before it can be picked. Thread-safe: drawing, mouse
    /// handling and the redraw timer all read it.
    /// Every view is hidden under the key of what it shows (<see cref="KeyOf"/>), so one entry hides every view of it: a
    /// location by its ID (on this section or an adjacent one), a location link by its <see cref="LocationLinkKey"/> (a line,
    /// or an arrow marker inside a location for an overlapping location on an adjacent section), a structure link by its
    /// <see cref="StructureLinkKey"/>, and an auto-segmentation ring by its <see cref="RingKey"/>. A link of either kind is
    /// also hidden while one of its locations is, since it would point at nothing.
    /// </summary>
    internal sealed class TemporarilyHiddenAnnotations(Func<DateTime> utcNow)
    {
        /// <summary>Share of the hide time, at its end, spent fading the annotation back in.</summary>
        public const double FadeFraction = 0.05;

        /// <summary>How often the viewer redraws while an annotation fades in.</summary>
        public static readonly TimeSpan FadeFrameInterval = TimeSpan.FromMilliseconds(33);

        /// <summary>The key an auto-segmentation ring is hidden under: its lowest source location, which survives the ring being rebuilt.</summary>
        public readonly record struct RingKey(long LocationId);

        private readonly record struct Entry(DateTime FadeStart, DateTime Expiry, TimeSpan FadeLength);

        private readonly object gate = new();
        private readonly Dictionary<object, Entry> entryByKey = [];

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
                    return entryByKey.Count == 0;
                }
            }
        }

        /// <summary>
        /// The key <paramref name="view"/> is hidden under, or null when Del cannot hide it. A key passes through unchanged,
        /// so every method here takes either a view or its key.
        /// </summary>
        public static object? KeyOf(object? view) => view switch
        {
            long or LocationLinkKey or StructureLinkKey or RingKey => view,
            LocationCanvasView location => location.ID,
            IViewLocationLink link => link.Key,
            IViewStructureLink link => link.Key,
            AutoPolygonizeProposal ring => new RingKey(ring.LocationIds.Count > 0 ? ring.LocationIds.Min() : ring.LocationId),
            _ => null,
        };

        /// <summary>The locations whose hiding also hides <paramref name="view"/>: the two ends of a link.</summary>
        private static (long A, long B)? EndsOf(object view) => view switch
        {
            LocationLinkKey key => (key.A, key.B),
            IViewLocationLink link => (link.Key.A, link.Key.B),
            StructureLinkViewModelBase { SourceLocation: not null, TargetLocation: not null } link => (link.SourceLocation.ID, link.TargetLocation.ID),
            _ => null,
        };

        /// <summary>
        /// Hides <paramref name="view"/> for <paramref name="duration"/>, restarting the time if it is already hidden. It is
        /// invisible until the last <see cref="FadeFraction"/> of that time, then fades back in.
        /// </summary>
        /// <returns>False when Del cannot hide this kind of view.</returns>
        public bool Hide(object view, TimeSpan duration)
        {
            object? key = KeyOf(view);
            if (key is null)
                return false;

            TimeSpan fadeLength = TimeSpan.FromTicks((long)(duration.Ticks * FadeFraction));
            DateTime expiry = utcNow() + duration;
            lock (gate)
            {
                entryByKey[key] = new Entry(expiry - fadeLength, expiry, fadeLength);
            }

            return true;
        }

        /// <summary>Hides the location <paramref name="locationId"/>; see <see cref="Hide(object, TimeSpan)"/>.</summary>
        public void Hide(long locationId, TimeSpan duration) => Hide((object)locationId, duration);

        /// <summary>
        /// Starts bringing <paramref name="view"/> back now: it fades in over the usual fade length and is clickable when
        /// that ends. False when it was not hidden itself, even if it is hidden because one of its locations is. One already
        /// fading in is left to finish.
        /// </summary>
        public bool Restore(object view)
        {
            object? key = KeyOf(view);
            if (key is null)
                return false;

            lock (gate)
            {
                Prune();
                if (!entryByKey.TryGetValue(key, out Entry entry))
                    return false;

                DateTime now = utcNow();
                if (now < entry.FadeStart)
                    entryByKey[key] = entry with { FadeStart = now, Expiry = now + entry.FadeLength };

                return true;
            }
        }

        /// <summary>Brings back the location <paramref name="locationId"/>; see <see cref="Restore(object)"/>.</summary>
        public bool Restore(long locationId) => Restore((object)locationId);

        /// <summary>
        /// True while <paramref name="view"/> cannot be picked: hidden itself, or one of its locations is, or still fading
        /// back in. It is clickable again when the fade ends.
        /// </summary>
        public bool IsHidden(object view)
        {
            object? key = KeyOf(view);
            if (key is null)
                return false;

            lock (gate)
            {
                return IsHiddenItself(key) || (EndsOf(view) is (long a, long b) && (IsHiddenItself(a) || IsHiddenItself(b)));
            }
        }

        /// <summary>True while the location <paramref name="locationId"/> cannot be picked; see <see cref="IsHidden(object)"/>.</summary>
        public bool IsHidden(long locationId) => IsHidden((object)locationId);

        private bool IsHiddenItself(object key) => entryByKey.TryGetValue(key, out Entry entry) && entry.Expiry > utcNow();

        /// <summary>
        /// How opaque <paramref name="view"/> is drawn: 1 when it is not hidden, 0 before its fade starts, then the first
        /// quarter of a sine wave (0 rising to 1, steepest at the start) over the fade. A link takes the least of its own fade
        /// and its two locations', so it reappears with them.
        /// </summary>
        public float FadeIn(object view)
        {
            object? key = KeyOf(view);
            if (key is null)
                return 1f;

            lock (gate)
            {
                DateTime now = utcNow();
                float fade = OwnFade(key, now);
                if (EndsOf(view) is (long a, long b))
                    fade = Math.Min(fade, Math.Min(OwnFade(a, now), OwnFade(b, now)));

                return fade;
            }
        }

        /// <summary>How opaque the location <paramref name="locationId"/> is drawn; see <see cref="FadeIn(object)"/>.</summary>
        public float FadeIn(long locationId) => FadeIn((object)locationId);

        private float OwnFade(object key, DateTime now) => entryByKey.TryGetValue(key, out Entry entry) ? FadeOf(entry, now) : 1f;

        private static float FadeOf(Entry entry, DateTime now)
        {
            if (entry.Expiry <= now)
                return 1f;

            if (now <= entry.FadeStart)
                return 0f;

            double progress = (now - entry.FadeStart).TotalMilliseconds / entry.FadeLength.TotalMilliseconds;
            return (float)Math.Sin(Math.Min(progress, 1.0) * Math.PI / 2.0);
        }

        /// <summary>
        /// How long until the viewer next needs a redraw because of a hidden annotation, or null when none is hidden:
        /// the start of the next fade, or one animation frame while any annotation is fading.
        /// </summary>
        public TimeSpan? TimeUntilNextRedraw()
        {
            lock (gate)
            {
                Prune();
                if (entryByKey.Count == 0)
                    return null;

                DateTime now = utcNow();
                TimeSpan wait = TimeSpan.MaxValue;
                foreach (Entry entry in entryByKey.Values)
                {
                    TimeSpan next = now < entry.FadeStart
                        ? entry.FadeStart - now
                        : TimeSpan.FromTicks(Math.Min(FadeFrameInterval.Ticks, (entry.Expiry - now).Ticks + TimeSpan.TicksPerMillisecond * 10));
                    if (next < wait)
                        wait = next;
                }

                return wait;
            }
        }

        /// <summary>The hits that are not hidden.</summary>
        public List<HitTestResult> Exclude(IEnumerable<HitTestResult> hits)
        {
            if (IsEmpty)
                return [.. hits];

            return [.. hits.Where(h => !IsHidden(h.obj))];
        }

        /// <summary>
        /// The views to draw: not hidden, or fading back in. A view without an alpha (<see cref="IColorView"/>) cannot fade,
        /// so it appears when the fade is half done.
        /// </summary>
        public List<T> VisibleToDraw<T>(IEnumerable<T> views) where T : class
        {
            if (IsEmpty)
                return [.. views];

            return [.. views.Where(v =>
            {
                float fade = FadeIn(v);
                return v is IColorView ? fade > 0f : fade >= 0.5f;
            })];
        }

        /// <summary>
        /// Scales the alpha of each view in <paramref name="views"/> that is fading back in by its <see cref="FadeIn(object)"/>.
        /// Dispose the result after drawing the views and their labels to put the alphas back.
        /// </summary>
        public IDisposable ApplyFade<T>(IEnumerable<T> views) where T : class
        {
            List<(IColorView View, float Alpha)> originals = [];
            if (!IsEmpty)
            {
                foreach (T view in views)
                {
                    if (view is not IColorView colored)
                        continue;

                    float fade = FadeIn(view);
                    if (fade >= 1f)
                        continue;

                    originals.Add((colored, colored.Alpha));
                    colored.Alpha *= fade;
                }
            }

            return new AlphaRestorer(originals);
        }

        private sealed class AlphaRestorer(List<(IColorView View, float Alpha)> originals) : IDisposable
        {
            public void Dispose()
            {
                foreach ((IColorView view, float alpha) in originals)
                    view.Alpha = alpha;
            }
        }

        /// <summary>
        /// What Del hides: whatever a click at <paramref name="position"/> would pick (the ranking hover uses, so this
        /// section's annotations come before adjacent ones and a link wins over a larger circle it is closer to), looking
        /// inside a location for the arrow marker under the mouse. Null when the mouse is over nothing Del can hide.
        /// </summary>
        /// <param name="hits">Every hit at the mouse position, hidden or not.</param>
        /// <param name="position">The mouse position (volume space) the hits were taken at.</param>
        public object? PickToHide(IEnumerable<HitTestResult> hits, int sectionNumber, Geometry.Vector2 position)
        {
            HitTestResult? best = Exclude(hits).NearestObjectOnCurrentSectionThenAdjacent(sectionNumber, preferCloserLocationLinks: true);
            if (best is null)
                return null;

            HitTestResult? nested = best.ExpandICanvasViewContainer(position);
            HitTestResult picked = nested is not null && !IsHidden(nested.obj) ? nested : best;
            return picked.Distance <= 1.0 && KeyOf(picked.obj) is not null ? picked.obj : null;
        }

        /// <summary>
        /// What Insert restores: among the views hidden themselves (not only through a hidden location) that the mouse is
        /// over, the one nearest its centre. A hidden view is not hit-tested normally, so <paramref name="hits"/> must
        /// include hidden ones; arrow markers are looked up inside the hit locations and <paramref name="markerHosts"/>.
        /// </summary>
        /// <param name="position">The mouse position (volume space) the hits were taken at.</param>
        /// <param name="markerHosts">
        /// Further locations whose markers may be under the mouse: a marker can sit outside its polygon's outline, where the
        /// polygon is not hit once the marker is hidden.
        /// </param>
        public object? PickToRestore(IEnumerable<HitTestResult> hits, Geometry.Vector2 position, IEnumerable<IOverlappedLinkHost>? markerHosts = null)
        {
            List<HitTestResult> hitList = [.. hits];
            IEnumerable<(object View, double Distance)> markers = hitList
                .Select(h => h.obj)
                .OfType<IOverlappedLinkHost>()
                .Concat(markerHosts ?? [])
                .Distinct()
                .SelectMany(host => host.OverlappedLinkView?.LinkViewsAt(position) ?? [])
                .Select(m => ((object)m, m.DistanceFromCenterNormalized(position)));

            lock (gate)
            {
                return hitList.Select(h => (View: (object)h.obj, h.Distance))
                    .Concat(markers)
                    .Where(c => KeyOf(c.View) is object key && IsHiddenItself(key))
                    .OrderBy(c => c.Distance)
                    .Select(c => c.View)
                    .FirstOrDefault();
            }
        }

        private void Prune()
        {
            DateTime now = utcNow();
            foreach (object key in entryByKey.Where(e => e.Value.Expiry <= now).Select(e => e.Key).ToList())
                entryByKey.Remove(key);
        }
    }
}
