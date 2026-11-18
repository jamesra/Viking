#nullable enable

using System;
using System.Collections.Generic;
using Geometry;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>The two buttons drawn inside an auto-segmentation proposal.</summary>
    internal enum ProposalButtonKind
    {
        /// <summary>Green circle with a white check: saves the proposal as the cell's outline.</summary>
        Accept,

        /// <summary>Red rounded square with a black X: hides the proposal until its circle changes.</summary>
        Hide,
    }

    /// <summary>How the two buttons sit relative to each other.</summary>
    internal enum ProposalButtonArrangement
    {
        /// <summary>Side by side, accept on the left. Used whenever it fits.</summary>
        LeftToRight,

        /// <summary>One above the other, accept on top. Used when side by side does not fit.</summary>
        TopToBottom,
    }

    /// <summary>Sizes of the proposal buttons, from the auto-segmentation preferences.</summary>
    /// <param name="SizePixels">Button diameter in screen pixels at 1x, which is volume units: the size in the image.</param>
    /// <param name="MinDiameterPixels">Smallest button that can be clicked, on screen; smaller buttons fade out.</param>
    internal readonly record struct ProposalButtonOptions(double SizePixels, double MinDiameterPixels);

    /// <summary>Where a proposal's two buttons go, in volume (world) space, and how visible they are.</summary>
    /// <param name="Accept">Centre of the accept button.</param>
    /// <param name="Hide">Centre of the hide button.</param>
    /// <param name="Radius">Radius of both buttons, in world units.</param>
    /// <param name="Fade">0 to 1 at the current zoom: 1 at or above an easily clickable size, falling to 0 just below the smallest clickable size.</param>
    /// <param name="InsideCircle">True when there was no room outside the seed circle and the buttons sit inside it.</param>
    /// <param name="Arrangement">Side by side or one above the other.</param>
    internal sealed record ProposalButtonPlacement(Vector2 Accept, Vector2 Hide, double Radius, double Fade, bool InsideCircle,
        ProposalButtonArrangement Arrangement = ProposalButtonArrangement.LeftToRight)
    {
        /// <summary>Below this fade the buttons are too faint to click: a click passes through to what is under them.</summary>
        internal const double ClickableFade = 0.5;

        /// <summary>True when the buttons are large enough on screen to take clicks.</summary>
        public bool IsClickable => Fade >= ClickableFade;

        /// <summary>Centre of <paramref name="kind"/>.</summary>
        public Vector2 CenterOf(ProposalButtonKind kind) => kind == ProposalButtonKind.Accept ? Accept : Hide;

        /// <summary>The button under <paramref name="worldPosition"/>, or null; nothing is hit while the buttons are not clickable.</summary>
        public ProposalButtonKind? HitTest(Vector2 worldPosition)
        {
            if (!IsClickable)
                return null;

            if (Vector2.Distance(worldPosition, Accept) <= Radius)
                return ProposalButtonKind.Accept;
            if (Vector2.Distance(worldPosition, Hide) <= Radius)
                return ProposalButtonKind.Hide;
            return null;
        }
    }

    /// <summary>
    /// Places the accept and hide buttons of an auto-segmentation proposal in volume space, once: the layout does not
    /// depend on the zoom, so the buttons never move and keep their size in the image.
    /// </summary>
    /// <remarks>
    /// Both buttons go in one rectangle, side by side (accept on the left) when that fits inside the proposed polygon
    /// and outside the circle it grew from, otherwise one above the other (accept on top). The rectangle goes where the
    /// largest one of its shape fits (<see cref="PoleOfInaccessibility.MaximumClearCircle"/> with a rectangle-shaped
    /// clearance), and the buttons take <see cref="RoomFill"/> of that room, up to
    /// <see cref="ProposalButtonOptions.SizePixels"/>. If neither arrangement leaves a button of
    /// <see cref="ProposalButtonOptions.MinDiameterPixels"/> (at 1x), the pair goes side by side inside the circle's lower
    /// right. Only the fade follows the zoom (<see cref="FadeFor"/>).
    /// </remarks>
    internal static class ProposalButtonLayout
    {
        /// <summary>Fraction of the free room a button fills, so it never touches the outline or the circle.</summary>
        internal const double RoomFill = 0.8;

        /// <summary>Gap between the two buttons, as a fraction of the button radius.</summary>
        internal const double GapFraction = 0.3;

        /// <summary>Half the long side of the pair's rectangle, in button radii: two buttons and the gap.</summary>
        internal const double PairHalfLength = 2 + GapFraction / 2;

        /// <summary>The fade runs from the smallest clickable size up to this many times that size.</summary>
        internal const double FadeRange = 1.25;

        /// <summary>Button radius inside the seed circle, as a fraction of the circle's radius.</summary>
        internal const double InsideRadiusFraction = 0.22;

        /// <summary>Where the pair sits inside the seed circle, as a fraction of its radius right of and below its centre.</summary>
        internal const double InsideOffsetX = 0.3, InsideOffsetY = 0.5;

        /// <summary>
        /// The buttons for a proposal <paramref name="polygon"/> grown from <paramref name="seed"/> (null when the seed is
        /// unknown). <paramref name="downsample"/> (world units per screen pixel) only sets the returned
        /// <see cref="ProposalButtonPlacement.Fade"/>; the positions and size do not depend on it. Null when the polygon
        /// is null or the downsample is not positive.
        /// </summary>
        public static ProposalButtonPlacement? Place(Polygon polygon, Circle? seed, double downsample, ProposalButtonOptions options)
        {
            if (polygon is null || downsample <= 0 || double.IsNaN(downsample))
                return null;

            double maxRadius = options.SizePixels / 2.0;
            double minRadius = options.MinDiameterPixels / 2.0;

            ProposalButtonPlacement? best = null;
            foreach (ProposalButtonArrangement arrangement in new[] { ProposalButtonArrangement.LeftToRight, ProposalButtonArrangement.TopToBottom })
            {
                ProposalButtonPlacement? outside = PlaceOutsideSeed(polygon, seed, maxRadius, arrangement);
                if (outside is not null && outside.Radius >= minRadius)
                    return outside with { Fade = FadeFor(outside.Radius, downsample, options) };

                if (outside is not null && (best is null || outside.Radius > best.Radius))
                    best = outside;
            }

            if (seed is Circle circle && circle.Radius > 0)
            {
                ProposalButtonPlacement inside = PlaceInsideSeed(circle, maxRadius);
                return inside with { Fade = FadeFor(inside.Radius, downsample, options) };
            }

            return best is null ? null : best with { Fade = FadeFor(best.Radius, downsample, options) };
        }

        /// <summary>Screen fade for buttons of world <paramref name="radius"/>; see <see cref="ProposalButtonPlacement.Fade"/>.</summary>
        internal static double FadeFor(double radius, double downsample, ProposalButtonOptions options)
        {
            double diameterPixels = 2 * radius / downsample;
            double start = options.MinDiameterPixels;
            double full = options.MinDiameterPixels * FadeRange;
            if (diameterPixels >= full)
                return 1;
            if (diameterPixels <= start)
                return 0;
            return (diameterPixels - start) / (full - start);
        }

        /// <summary>The pair's rectangle half sizes, in button radii, for <paramref name="arrangement"/>.</summary>
        internal static (double X, double Y) HalfSize(ProposalButtonArrangement arrangement) =>
            arrangement == ProposalButtonArrangement.LeftToRight ? (PairHalfLength, 1) : (1, PairHalfLength);

        private static ProposalButtonPlacement? PlaceOutsideSeed(Polygon polygon, Circle? seed, double maxRadius, ProposalButtonArrangement arrangement)
        {
            (double halfX, double halfY) = HalfSize(arrangement);
            List<(Vector2 A, Vector2 B)> edges = EdgesOf(polygon);

            // The largest button radius r whose pair rectangle, centred at p, stays inside the polygon and clear of the seed.
            // It is 1-Lipschitz (the rectangle norm is never longer than the Euclidean one), as the search needs.
            double RectangleClearance(Vector2 p)
            {
                double clearance = double.MaxValue;
                foreach ((Vector2 a, Vector2 b) in edges)
                    clearance = Math.Min(clearance, RectangleNormDistance(p, a, b, halfX, halfY));

                if (seed is Circle c)
                    clearance = Math.Min(clearance, SeedClearance(p, c, halfX, halfY));

                return clearance;
            }

            Circle room;
            try
            {
                room = polygon.MaximumClearCircle(RectangleClearance);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (room.Radius <= 0)
                return null;

            double radius = Math.Min(maxRadius, room.Radius * RoomFill);
            double offset = radius * (1 + GapFraction / 2);

            // World y points up, so "on top" is the larger y.
            (Vector2 accept, Vector2 hide) = arrangement == ProposalButtonArrangement.LeftToRight
                ? (new Vector2(room.Center.X - offset, room.Center.Y), new Vector2(room.Center.X + offset, room.Center.Y))
                : (new Vector2(room.Center.X, room.Center.Y + offset), new Vector2(room.Center.X, room.Center.Y - offset));
            return new ProposalButtonPlacement(accept, hide, radius, Fade: 1, InsideCircle: false, arrangement);
        }

        private static ProposalButtonPlacement PlaceInsideSeed(Circle seed, double maxRadius)
        {
            double radius = Math.Min(maxRadius, seed.Radius * InsideRadiusFraction);
            double halfSpacing = radius * (1 + GapFraction / 2);

            // World y points up, so "below the centre" is a smaller y.
            Vector2 middle = new(seed.Center.X + seed.Radius * InsideOffsetX, seed.Center.Y - seed.Radius * InsideOffsetY);
            Vector2 accept = new(middle.X - halfSpacing, middle.Y);
            Vector2 hide = new(middle.X + halfSpacing, middle.Y);
            return new ProposalButtonPlacement(accept, hide, radius, Fade: 1, InsideCircle: true, ProposalButtonArrangement.LeftToRight);
        }

        private static List<(Vector2 A, Vector2 B)> EdgesOf(Polygon polygon)
        {
            List<(Vector2, Vector2)> edges = [];
            void AddRing(Vector2[] ring)
            {
                for (int i = 1; i < ring.Length; i++)
                    edges.Add((ring[i - 1], ring[i]));
            }

            AddRing(polygon.ExteriorRing);
            foreach (Vector2[] hole in polygon.InteriorRings)
                AddRing(hole);
            return edges;
        }

        /// <summary>
        /// Distance from <paramref name="p"/> to the segment <paramref name="a"/>-<paramref name="b"/> in the norm
        /// max(|dx| / halfX, |dy| / halfY): the largest scale of a halfX x halfY rectangle centred at p that the segment
        /// does not cross.
        /// </summary>
        internal static double RectangleNormDistance(Vector2 p, Vector2 a, Vector2 b, double halfX, double halfY)
        {
            // u(t), v(t): the scaled offsets of the segment point at t from p, both linear in t. max(|u|, |v|) is convex
            // in t, so its minimum on [0, 1] is at an end or where u = 0, v = 0 or |u| = |v|.
            double u0 = (a.X - p.X) / halfX, du = (b.X - a.X) / halfX;
            double v0 = (a.Y - p.Y) / halfY, dv = (b.Y - a.Y) / halfY;
            double Norm(double t) => Math.Max(Math.Abs(u0 + du * t), Math.Abs(v0 + dv * t));

            double best = Math.Min(Norm(0), Norm(1));
            void Try(double numerator, double denominator)
            {
                if (denominator == 0)
                    return;
                double t = numerator / denominator;
                if (t > 0 && t < 1)
                    best = Math.Min(best, Norm(t));
            }

            Try(-u0, du);
            Try(-v0, dv);
            Try(v0 - u0, du - dv);
            Try(-u0 - v0, du + dv);
            return best;
        }

        /// <summary>
        /// The largest button radius whose halfX x halfY (in radii) rectangle centred at <paramref name="p"/> stays clear
        /// of <paramref name="seed"/>; negative inside the seed so the search is pushed out of it.
        /// </summary>
        internal static double SeedClearance(Vector2 p, Circle seed, double halfX, double halfY)
        {
            double dx = Math.Abs(p.X - seed.Center.X), dy = Math.Abs(p.Y - seed.Center.Y);
            double centerDistance = Math.Sqrt(dx * dx + dy * dy);
            if (centerDistance <= seed.Radius)
                return centerDistance - seed.Radius;

            // Distance from the seed centre to the rectangle shrinks as the rectangle grows; find where it meets the radius.
            double DistanceToRectangle(double r)
            {
                double ox = Math.Max(dx - halfX * r, 0), oy = Math.Max(dy - halfY * r, 0);
                return Math.Sqrt(ox * ox + oy * oy);
            }

            double low = 0, high = Math.Max(dx / halfX, dy / halfY);
            for (int i = 0; i < 50; i++)
            {
                double mid = (low + high) / 2;
                if (DistanceToRectangle(mid) >= seed.Radius)
                    low = mid;
                else
                    high = mid;
            }

            return low;
        }
    }
}
