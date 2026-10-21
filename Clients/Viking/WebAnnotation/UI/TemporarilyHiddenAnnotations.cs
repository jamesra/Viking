using System;
using System.Collections.Generic;
using System.Linq;
using WebAnnotation.View;

namespace WebAnnotation.UI
{
    /// <summary>
    /// Annotations on the section being viewed that the user hid with Del for a while, so an annotation beneath them
    /// (usually one from an adjacent section) can be reached. A hidden annotation is neither drawn nor hit-tested until
    /// its time runs out or Insert restores it. During the last <see cref="FadeFraction"/> of the time it fades back in
    /// (a quarter sine wave, from invisible to fully opaque) but is still not clickable, so the user sees it return before
    /// it can be picked. Thread-safe: drawing, mouse handling and the redraw timer all read it.
    /// </summary>
    internal sealed class TemporarilyHiddenAnnotations(Func<DateTime> utcNow)
    {
        /// <summary>Share of the hide time, at its end, spent fading the annotation back in.</summary>
        public const double FadeFraction = 0.05;

        /// <summary>How often the viewer redraws while an annotation fades in.</summary>
        public static readonly TimeSpan FadeFrameInterval = TimeSpan.FromMilliseconds(33);

        private readonly record struct Entry(DateTime FadeStart, DateTime Expiry, TimeSpan FadeLength);

        private readonly object gate = new();
        private readonly Dictionary<long, Entry> entryByLocationId = [];

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
                    return entryByLocationId.Count == 0;
                }
            }
        }

        /// <summary>
        /// Hides <paramref name="locationId"/> for <paramref name="duration"/>, restarting the time if it is already hidden.
        /// It is invisible until the last <see cref="FadeFraction"/> of that time, then fades back in.
        /// </summary>
        public void Hide(long locationId, TimeSpan duration)
        {
            lock (gate)
            {
                TimeSpan fadeLength = TimeSpan.FromTicks((long)(duration.Ticks * FadeFraction));
                DateTime expiry = utcNow() + duration;
                entryByLocationId[locationId] = new Entry(expiry - fadeLength, expiry, fadeLength);
            }
        }

        /// <summary>
        /// Starts bringing <paramref name="locationId"/> back now: it fades in over the usual fade length and is clickable
        /// when that ends. False when it was not hidden. An annotation already fading in is left to finish.
        /// </summary>
        public bool Restore(long locationId)
        {
            lock (gate)
            {
                Prune();
                if (!entryByLocationId.TryGetValue(locationId, out Entry entry))
                    return false;

                DateTime now = utcNow();
                if (now < entry.FadeStart)
                    entryByLocationId[locationId] = entry with { FadeStart = now, Expiry = now + entry.FadeLength };

                return true;
            }
        }

        /// <summary>
        /// True while <paramref name="locationId"/> cannot be picked: hidden, or still fading back in. It is clickable again
        /// when the fade ends.
        /// </summary>
        public bool IsHidden(long locationId)
        {
            lock (gate)
            {
                return entryByLocationId.TryGetValue(locationId, out Entry entry) && entry.Expiry > utcNow();
            }
        }

        /// <summary>
        /// How opaque <paramref name="locationId"/> is drawn: 1 when it is not hidden, 0 before its fade starts, then the
        /// first quarter of a sine wave (0 rising to 1, steepest at the start) over the fade.
        /// </summary>
        public float FadeIn(long locationId)
        {
            lock (gate)
            {
                DateTime now = utcNow();
                if (!entryByLocationId.TryGetValue(locationId, out Entry entry) || entry.Expiry <= now)
                    return 1f;

                if (now <= entry.FadeStart)
                    return 0f;

                double progress = (now - entry.FadeStart).TotalMilliseconds / entry.FadeLength.TotalMilliseconds;
                return (float)Math.Sin(Math.Min(progress, 1.0) * Math.PI / 2.0);
            }
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
                if (entryByLocationId.Count == 0)
                    return null;

                DateTime now = utcNow();
                TimeSpan wait = TimeSpan.MaxValue;
                foreach (Entry entry in entryByLocationId.Values)
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

        /// <summary>The hits that are not a hidden annotation.</summary>
        public List<HitTestResult> Exclude(IEnumerable<HitTestResult> hits)
        {
            if (IsEmpty)
                return [.. hits];

            return [.. hits.Where(h => !(h.obj is LocationCanvasView view && IsHidden(view.ID)))];
        }

        /// <summary>
        /// The locations to draw: not hidden, or fading back in. A view without an alpha (a polyline) cannot fade, so it
        /// appears when the fade is half done.
        /// </summary>
        public List<LocationCanvasView> VisibleToDraw(IEnumerable<LocationCanvasView> locations)
        {
            if (IsEmpty)
                return [.. locations];

            return [.. locations.Where(l =>
            {
                float fade = FadeIn(l.ID);
                return CanFade(l) ? fade > 0f : fade >= 0.5f;
            })];
        }

        /// <summary>
        /// Scales the alpha of each fading view in <paramref name="locations"/> by <see cref="FadeIn"/>. Dispose the result
        /// after drawing to put the alphas back.
        /// </summary>
        public IDisposable ApplyFade(IEnumerable<LocationCanvasView> locations)
        {
            List<(LocationCanvasView View, float Alpha)> originals = [];
            if (!IsEmpty)
            {
                foreach (LocationCanvasView view in locations)
                {
                    float fade = FadeIn(view.ID);
                    if (fade >= 1f || !CanFade(view))
                        continue;

                    float original = AlphaOf(view);
                    originals.Add((view, original));
                    SetAlpha(view, original * fade);
                }
            }

            return new Restorer(originals);
        }

        private static bool CanFade(LocationCanvasView view) => view is LocationCircleView or LocationPolygonView or LocationCurveView;

        private static float AlphaOf(LocationCanvasView view) => view switch
        {
            LocationCircleView circle => circle.Alpha,
            LocationPolygonView polygon => polygon.Alpha,
            LocationCurveView curve => curve.Alpha,
            _ => 1f,
        };

        private static void SetAlpha(LocationCanvasView view, float alpha)
        {
            switch (view)
            {
                case LocationCircleView circle:
                    circle.Alpha = alpha;
                    break;
                case LocationPolygonView polygon:
                    polygon.Alpha = alpha;
                    break;
                case LocationCurveView curve:
                    curve.Alpha = alpha;
                    break;
            }
        }

        private sealed class Restorer(List<(LocationCanvasView View, float Alpha)> originals) : IDisposable
        {
            public void Dispose()
            {
                foreach ((LocationCanvasView view, float alpha) in originals)
                    SetAlpha(view, alpha);
            }
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
            foreach (long id in entryByLocationId.Where(e => e.Value.Expiry <= now).Select(e => e.Key).ToList())
                entryByLocationId.Remove(id);
        }
    }
}
