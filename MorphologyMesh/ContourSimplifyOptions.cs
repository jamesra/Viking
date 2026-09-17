using System;
using Geometry;

namespace MorphologyMesh
{
    /// <summary>
    /// Density-gated simplification for closed polygon contours cached by <see cref="SliceGraph"/>.
    /// Open polylines are never simplified. Morphology geometry is in nanometres after volume scale is applied.
    /// </summary>
    public readonly struct ContourSimplifyOptions
    {
        /// <summary>
        /// When <see cref="AdaptiveHullSpacing"/> is false: simplify when denser than one unique vertex per
        /// this many nm of length. When adaptive: this is the <em>maximum</em> spacing threshold used for
        /// nearly-convex rings (hull-area ratio ≥ <see cref="AdaptiveHighHullRatio"/>).
        /// Values &lt;= 0 disable density-based simplify.
        /// Use <see cref="double.PositiveInfinity"/> to always simplify at <see cref="ToleranceNm"/>.
        /// </summary>
        public double MinNmPerVertex { get; }

        /// <summary>
        /// Max distance (nm) from the simplified curve to the original when density simplify runs.
        /// </summary>
        public double ToleranceNm { get; }

        /// <summary>
        /// When true, the density-gate spacing interpolates from <see cref="AdaptiveMinSpacingNm"/> at low
        /// hull-area ratio up to <see cref="MinNmPerVertex"/> at high hull-area ratio.
        /// </summary>
        public bool AdaptiveHullSpacing { get; }

        /// <summary>Hull-area ratio at/above which adaptive spacing reaches <see cref="MinNmPerVertex"/> (default 0.95).</summary>
        public const double AdaptiveHighHullRatio = 0.95;

        /// <summary>Hull-area ratio at/below which adaptive spacing reaches <see cref="AdaptiveMinSpacingNm"/> (default 0.70).</summary>
        public const double AdaptiveLowHullRatio = 0.70;

        /// <summary>Conservative spacing for convoluted rings under adaptive mode (default 20 nm).</summary>
        public const double AdaptiveMinSpacingNm = 20.0;

        public ContourSimplifyOptions(double minNmPerVertex, double toleranceNm, bool adaptiveHullSpacing = false)
        {
            MinNmPerVertex = minNmPerVertex;
            ToleranceNm = toleranceNm;
            AdaptiveHullSpacing = adaptiveHullSpacing;
        }

        /// <summary>
        /// Default: hull-adaptive density gate (convex ≈ 50 nm spacing, convoluted ≈ 20 nm), simplify within 10 nm.
        /// </summary>
        public static ContourSimplifyOptions Default { get; } = new(50.0, 10.0, adaptiveHullSpacing: true);

        /// <summary>Never density-simplify (legacy huge-ring ToPolygon safety still uses tolerance 0).</summary>
        public static ContourSimplifyOptions Disabled { get; } = new(0.0, 0.0);

        /// <summary>Always run curve simplify at <paramref name="toleranceNm"/> (legacy Create(double) behavior).</summary>
        public static ContourSimplifyOptions Always(double toleranceNm) =>
            new(double.PositiveInfinity, toleranceNm);

        /// <summary>
        /// Tolerance to pass into ToPolygon / Simplify for this closed polygon, or 0 when the density gate does not fire.
        /// </summary>
        public double ToleranceFor(Polygon poly)
        {
            if (!IsActive)
                return 0;
            if (double.IsPositiveInfinity(MinNmPerVertex))
                return ToleranceNm;
            if (poly is null)
                return 0;

            if (RingExceedsDensity(poly.ExteriorRing, SpacingForRing(poly.ExteriorRing)))
                return ToleranceNm;
            foreach (Vector2[] ring in poly.InteriorRings)
            {
                if (RingExceedsDensity(ring, SpacingForRing(ring)))
                    return ToleranceNm;
            }

            return 0;
        }

        /// <summary>
        /// Density-gate spacing (nm/vert threshold) for a closed ring under this options instance.
        /// </summary>
        public double SpacingForRing(Vector2[] ring)
        {
            if (!AdaptiveHullSpacing || !IsActive || double.IsPositiveInfinity(MinNmPerVertex))
                return MinNmPerVertex;

            return SpacingForHullRatio(HullAreaRatio(ring), MinNmPerVertex);
        }

        /// <summary>
        /// Linear map: hull-area ratio ≤ <see cref="AdaptiveLowHullRatio"/> → <see cref="AdaptiveMinSpacingNm"/>;
        /// ratio ≥ <see cref="AdaptiveHighHullRatio"/> → <paramref name="maxSpacingNm"/>; lerp between.
        /// </summary>
        public static double SpacingForHullRatio(double hullAreaRatio, double maxSpacingNm = 50.0)
        {
            double maxSpacing = maxSpacingNm > AdaptiveMinSpacingNm ? maxSpacingNm : AdaptiveMinSpacingNm;
            if (hullAreaRatio >= AdaptiveHighHullRatio)
                return maxSpacing;
            if (hullAreaRatio <= AdaptiveLowHullRatio)
                return AdaptiveMinSpacingNm;

            double t = (hullAreaRatio - AdaptiveLowHullRatio) / (AdaptiveHighHullRatio - AdaptiveLowHullRatio);
            return AdaptiveMinSpacingNm + (t * (maxSpacing - AdaptiveMinSpacingNm));
        }

        /// <summary>
        /// Exterior (or ring) area divided by its convex-hull area. Near 1 = convex; lower = more invaginated.
        /// Degenerate rings return 1 so they do not force aggressive simplify.
        /// </summary>
        public static double HullAreaRatio(Vector2[] ring)
        {
            if (ring is null || ring.Length < 3)
                return 1.0;

            double area = Math.Abs(ring.PolygonArea());
            if (area <= Tolerance.Epsilon)
                return 1.0;

            Vector2[] hull = ring.ConvexHull();
            if (hull is null || hull.Length < 3)
                return 1.0;

            double hullArea = Math.Abs(hull.PolygonArea());
            if (hullArea <= Tolerance.Epsilon)
                return 1.0;

            double ratio = area / hullArea;
            if (ratio > 1.0)
                return 1.0;
            if (ratio < 0.0)
                return 0.0;
            return ratio;
        }

        bool IsActive => MinNmPerVertex > 0 && ToleranceNm > 0;

        /// <summary>True when Douglas–Peucker pre-simplify for huge rings should use <see cref="ToleranceNm"/>.</summary>
        internal bool IsActiveForPreSimplify => ToleranceNm > 0;

        /// <summary>
        /// Closed contour: unique verts exclude the duplicate closing vertex; length is the closed perimeter.
        /// </summary>
        internal static bool RingExceedsDensity(Vector2[] ring, double minNmPerVertex)
        {
            if (ring is null || ring.Length < 3 || minNmPerVertex <= 0)
                return false;

            bool closed = ring[0] == ring[^1];
            int uniqueVerts = closed ? ring.Length - 1 : ring.Length;
            if (uniqueVerts < 3)
                return false;

            double lengthNm = ring.PerimeterLength();
            if (lengthNm <= 1e-9)
                return false;

            return uniqueVerts / lengthNm > 1.0 / minNmPerVertex;
        }
    }
}
