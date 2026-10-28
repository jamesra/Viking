using System;

namespace Geometry
{
    /// <summary>Looks up the offset stored at lattice node (<paramref name="gx"/>, <paramref name="gy"/>).</summary>
    /// <returns>False when the lattice has no node there.</returns>
    public delegate bool TryGetLatticeOffset(int gx, int gy, out Vector2 offset);

    /// <summary>
    /// Samples a sparse square lattice of 2D offsets, such as a section correction field. The one implementation shared by
    /// the correction service (<c>SectionVectorField</c>, <c>NeighborResidualField</c>) and the clients that download a
    /// section's lattice (<c>SectionLatticeField</c>), so a client sampling the lattice gets what the service would return.
    /// </summary>
    public static class LatticeInterpolation
    {
        /// <summary>Added to the squared distance in the inverse-distance weights, so a sample on a node is finite.</summary>
        public const double DistanceEpsilonSquared = 1.0;

        /// <summary>Below this, the corners present in a partly published cell carry no weight.</summary>
        const double PartialCellWeightEpsilon = 1e-6;

        /// <summary>
        /// The offset at <paramref name="point"/>. Node (gx, gy) sits at (gx * pitch, gy * pitch).
        /// <list type="number">
        /// <item>All four corners of the cell holding the point exist: bilinear interpolation.</item>
        /// <item>Some corners exist: bilinear over those present, the weights rescaled to sum to one.</item>
        /// <item>None exist: inverse-distance weighting, 1 / (d² + <see cref="DistanceEpsilonSquared"/>), over the nodes
        /// within <paramref name="kernelRadius"/>.</item>
        /// </list>
        /// </summary>
        /// <param name="pitch">Lattice spacing, in the units of <paramref name="point"/>.</param>
        /// <param name="kernelRadius">Reach of the inverse-distance fallback.</param>
        /// <param name="tryGet">Node lookup. Return false for a node that is absent or not trusted.</param>
        /// <returns>
        /// The offset and whether the point has support. Without support (no node within the kernel radius) the offset is
        /// zero and must not be applied.
        /// </returns>
        public static (Vector2 Offset, bool Trusted) Sample(Vector2 point, double pitch, double kernelRadius, TryGetLatticeOffset tryGet)
        {
            double fx = point.X / pitch;
            double fy = point.Y / pitch;
            int gx0 = (int)Math.Floor(fx);
            int gy0 = (int)Math.Floor(fy);
            double tx = fx - gx0;
            double ty = fy - gy0;

            bool has00 = tryGet(gx0, gy0, out Vector2 t00);
            bool has10 = tryGet(gx0 + 1, gy0, out Vector2 t10);
            bool has01 = tryGet(gx0, gy0 + 1, out Vector2 t01);
            bool has11 = tryGet(gx0 + 1, gy0 + 1, out Vector2 t11);

            if (has00 && has10 && has01 && has11)
            {
                Vector2 a = t00 * (1 - tx) + t10 * tx;
                Vector2 b = t01 * (1 - tx) + t11 * tx;
                return (a * (1 - ty) + b * ty, true);
            }

            double cornerWeights = 0;
            Vector2 cornerSum = Vector2.Zero;
            if (has00) { double q = (1 - tx) * (1 - ty); cornerSum += t00 * q; cornerWeights += q; }
            if (has10) { double q = tx * (1 - ty); cornerSum += t10 * q; cornerWeights += q; }
            if (has01) { double q = (1 - tx) * ty; cornerSum += t01 * q; cornerWeights += q; }
            if (has11) { double q = tx * ty; cornerSum += t11 * q; cornerWeights += q; }
            if (cornerWeights > PartialCellWeightEpsilon)
                return (cornerSum * (1.0 / cornerWeights), true);

            int reach = (int)Math.Ceiling(kernelRadius / pitch) + 1;
            double r2 = kernelRadius * kernelRadius;
            double weightSum = 0;
            Vector2 weighted = Vector2.Zero;
            for (int dx = -reach; dx <= reach; dx++)
            {
                for (int dy = -reach; dy <= reach; dy++)
                {
                    int gx = gx0 + dx;
                    int gy = gy0 + dy;
                    if (!tryGet(gx, gy, out Vector2 offset))
                        continue;

                    double cx = gx * pitch;
                    double cy = gy * pitch;
                    double d2 = (point.X - cx) * (point.X - cx) + (point.Y - cy) * (point.Y - cy);
                    if (d2 > r2)
                        continue;

                    double w = 1.0 / (d2 + DistanceEpsilonSquared);
                    weighted += offset * w;
                    weightSum += w;
                }
            }

            if (weightSum <= Tolerance.Epsilon)
                return (Vector2.Zero, false);

            return (weighted * (1.0 / weightSum), true);
        }
    }
}
