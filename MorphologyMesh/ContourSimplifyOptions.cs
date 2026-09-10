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
        /// Simplify when a closed contour has more than one unique vertex per this many nanometres of length
        /// (closing duplicate excluded). Values &lt;= 0 disable density-based simplify.
        /// Use <see cref="double.PositiveInfinity"/> to always simplify at <see cref="ToleranceNm"/>.
        /// </summary>
        public double MinNmPerVertex { get; }

        /// <summary>
        /// Max distance (nm) from the simplified curve to the original when density simplify runs.
        /// </summary>
        public double ToleranceNm { get; }

        public ContourSimplifyOptions(double minNmPerVertex, double toleranceNm)
        {
            MinNmPerVertex = minNmPerVertex;
            ToleranceNm = toleranceNm;
        }

        /// <summary>Default: denser than 1 vertex / 20 nm → simplify within 10 nm of the curve.</summary>
        public static ContourSimplifyOptions Default { get; } = new(20.0, 10.0);

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
            if (RingExceedsDensity(poly.ExteriorRing, MinNmPerVertex))
                return ToleranceNm;
            foreach (Vector2[] ring in poly.InteriorRings)
            {
                if (RingExceedsDensity(ring, MinNmPerVertex))
                    return ToleranceNm;
            }

            return 0;
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
