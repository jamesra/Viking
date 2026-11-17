#nullable enable

using System;
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

    /// <summary>Screen sizes and opacity of the proposal buttons, from the auto-segmentation preferences.</summary>
    /// <param name="MaxDiameterPixels">Largest button, in screen pixels.</param>
    /// <param name="MinDiameterPixels">Smallest button that can be clicked; smaller buttons fade out.</param>
    internal readonly record struct ProposalButtonOptions(double MaxDiameterPixels, double MinDiameterPixels);

    /// <summary>Where a proposal's two buttons go, in volume (world) space, and how visible they are.</summary>
    /// <param name="Accept">Centre of the accept button.</param>
    /// <param name="Hide">Centre of the hide button.</param>
    /// <param name="Radius">Radius of both buttons, in world units.</param>
    /// <param name="Fade">0 to 1: 1 at or above an easily clickable size, falling to 0 just below the smallest clickable size.</param>
    /// <param name="InsideCircle">True when there was no room outside the seed circle and the buttons sit inside it.</param>
    internal sealed record ProposalButtonPlacement(Vector2 Accept, Vector2 Hide, double Radius, double Fade, bool InsideCircle)
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
    /// Places the accept and hide buttons of an auto-segmentation proposal: inside the proposed polygon and outside the
    /// circle it grew from when they fit there at a clickable size, otherwise side by side in the circle's lower right.
    /// </summary>
    /// <remarks>
    /// The free spot is the centre of the largest circle inside the polygon and clear of the seed circle
    /// (<see cref="PoleOfInaccessibility.MaximumClearCircle"/>). The accept button goes there; the hide button goes at the
    /// best spot beside it, kept close so the pair reads as one control. Button size follows the room available up to
    /// <see cref="ProposalButtonOptions.MaxDiameterPixels"/>; a pair smaller than
    /// <see cref="ProposalButtonOptions.MinDiameterPixels"/> on screen fades out over the next quarter of that size.
    /// </remarks>
    internal static class ProposalButtonLayout
    {
        /// <summary>Fraction of the free room a button fills, so it never touches the outline or the circle.</summary>
        internal const double RoomFill = 0.8;

        /// <summary>Gap between the two buttons, as a fraction of the button radius.</summary>
        internal const double GapFraction = 0.3;

        /// <summary>
        /// Outer edge of the band the hide button is placed in, in button radii from the accept button's centre. The band
        /// (from the accept button plus the gap out to this edge) is a little wider than a free circle of
        /// radius / <see cref="RoomFill"/> needs, so where there is room the band does not make the hide button smaller,
        /// even with the search's tolerance.
        /// </summary>
        internal const double MaxPairSpacingRadii = 1 + GapFraction + 2.2 / RoomFill;

        /// <summary>The fade runs from the smallest clickable size up to this many times that size.</summary>
        internal const double FadeRange = 1.25;

        /// <summary>Button radius inside the seed circle, as a fraction of the circle's radius.</summary>
        internal const double InsideRadiusFraction = 0.22;

        /// <summary>Where the pair sits inside the seed circle, as a fraction of its radius right of and below its centre.</summary>
        internal const double InsideOffsetX = 0.3, InsideOffsetY = 0.5;

        /// <summary>
        /// The buttons for a proposal <paramref name="polygon"/> grown from <paramref name="seed"/> (null when the seed is
        /// unknown), at <paramref name="downsample"/> world units per screen pixel. Null when the polygon is empty or the
        /// downsample is not positive.
        /// </summary>
        public static ProposalButtonPlacement? Place(Polygon polygon, Circle? seed, double downsample, ProposalButtonOptions options)
        {
            if (polygon is null || downsample <= 0 || double.IsNaN(downsample))
                return null;

            double maxRadius = options.MaxDiameterPixels / 2.0 * downsample;
            double minRadius = options.MinDiameterPixels / 2.0 * downsample;

            ProposalButtonPlacement? outside = PlaceOutsideSeed(polygon, seed, maxRadius, downsample, options);
            if (outside is not null && (outside.Radius >= minRadius || seed is null))
                return outside;

            if (seed is Circle circle && circle.Radius > 0)
                return PlaceInsideSeed(circle, maxRadius, downsample, options);

            return outside;
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

        private static ProposalButtonPlacement? PlaceOutsideSeed(Polygon polygon, Circle? seed, double maxRadius, double downsample, ProposalButtonOptions options)
        {
            double SeedClearance(Vector2 p) => seed is Circle c ? Vector2.Distance(p, c.Center) - c.Radius : double.MaxValue;

            Circle first;
            try
            {
                first = polygon.MaximumClearCircle(SeedClearance);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (first.Radius <= 0)
                return null;

            double radius = Math.Min(maxRadius, first.Radius * RoomFill);
            double gap = radius * GapFraction;
            double reach = radius * MaxPairSpacingRadii;
            Vector2 acceptCenter = first.Center;

            // Clear of the seed and of the accept button, and close enough to it to read as a pair.
            double PairClearance(Vector2 p)
            {
                double fromAccept = Vector2.Distance(p, acceptCenter);
                return Math.Min(SeedClearance(p), Math.Min(fromAccept - radius - gap, reach - fromAccept));
            }

            Circle second = polygon.MaximumClearCircle(PairClearance);
            if (second.Radius <= 0)
                return null;

            radius = Math.Min(radius, second.Radius * RoomFill);
            return new ProposalButtonPlacement(acceptCenter, second.Center, radius, FadeFor(radius, downsample, options), InsideCircle: false);
        }

        private static ProposalButtonPlacement PlaceInsideSeed(Circle seed, double maxRadius, double downsample, ProposalButtonOptions options)
        {
            double radius = Math.Min(maxRadius, seed.Radius * InsideRadiusFraction);
            double halfSpacing = radius * (1 + GapFraction / 2);

            // World y points up, so "below the centre" is a smaller y.
            Vector2 middle = new(seed.Center.X + seed.Radius * InsideOffsetX, seed.Center.Y - seed.Radius * InsideOffsetY);
            Vector2 accept = new(middle.X - halfSpacing, middle.Y);
            Vector2 hide = new(middle.X + halfSpacing, middle.Y);
            return new ProposalButtonPlacement(accept, hide, radius, FadeFor(radius, downsample, options), InsideCircle: true);
        }
    }
}
