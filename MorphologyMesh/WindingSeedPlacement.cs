using Geometry;
using System;

namespace MorphologyMesh
{
    /// <summary>
    /// Named tolerances for ray-seeded winding. Barycentric and angular-cosine values are already
    /// normalized; world distances scale with the patch so tiny and huge meshes use the same geometry.
    /// </summary>
    internal static class WindingRayTolerances
    {
        /// <summary>Barycentric coordinate at or below this is an edge or vertex graze.</summary>
        public const double BarycentricGraze = 1e-7;

        /// <summary>Absolute |unit-normal · unit-direction| at or below this is a tangent skim.</summary>
        public const double AngularCosineTangent = 1e-9;

        /// <summary>World-space distance below which two crossings are the same cluster, or a seed sits on a plane.</summary>
        public static double WorldDistance(double componentScale) =>
            Math.Max(1e-12, componentScale * 1e-9);
    }

    /// <summary>
    /// Places a Delaunay-contained XY candidate inside the reconstructed slice band. A fixed fraction of
    /// slice thickness exits slanted walls; clearance and thickness together keep the origin inside and
    /// farther from the contour plane than the ray intersection tolerance.
    /// </summary>
    internal static class WindingSeedPlacement
    {
        public static Vector3 PlaceInsideReconstructedBand(
            Vector2 xy,
            IShape2D shape,
            double contourZ,
            double sliceCenterZ,
            double sliceThickness)
        {
            double clearance = BoundaryClearance(shape, xy);
            double towardCenter = sliceCenterZ - contourZ;
            double absToward = Math.Abs(towardCenter);
            double scale = Math.Max(Math.Max(clearance, absToward), Math.Abs(sliceThickness));
            double worldTol = WindingRayTolerances.WorldDistance(Math.Max(scale, 1.0));

            if (absToward <= worldTol)
                return xy.ToVector3(contourZ);

            // A Z travel larger than the XY clearance leaves a wall that slopes inward at 45° or steeper.
            double maxFromClearance = Math.Max(worldTol * 2.0, clearance * 0.5);
            double maxFromThickness = absToward * 0.5;
            double offset = Math.Min(maxFromClearance, maxFromThickness);
            if (offset < worldTol * 2.0)
                offset = Math.Min(maxFromThickness, Math.Max(offset, worldTol * 2.0));

            return xy.ToVector3(contourZ + (Math.Sign(towardCenter) * offset));
        }

        /// <summary>
        /// Distance from an interior point to the nearest polygon boundary, counting holes.
        /// <see cref="Polygon.Distance(Vector2)"/> only measures the exterior ring.
        /// </summary>
        public static double BoundaryClearance(IShape2D shape, Vector2 point)
        {
            if (shape is Polygon poly)
            {
                double clearance = poly.Distance(point);
                foreach (Polygon inner in poly.InteriorPolygons)
                    clearance = Math.Min(clearance, inner.Distance(point));
                return clearance;
            }

            if (shape is Circle circle)
                return circle.Radius - Vector2.Distance(circle.Center, point);

            return 0;
        }
    }
}
