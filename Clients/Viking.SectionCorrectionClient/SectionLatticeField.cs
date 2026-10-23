using System;
using System.Collections.Generic;

namespace Viking.SectionCorrectionClient
{
    /// <summary>
    /// The correction field of one section: a sparse lattice of offsets, sampled locally so the shim needs no
    /// round trip per camera position.
    /// </summary>
    /// <remarks>
    /// A port of the service's <c>SectionVectorField.Sample</c>; <c>CorrectPoints</c> in Neighbor mode returns exactly
    /// what <see cref="Sample"/> returns here, so the two must stay in step (including the half-pitch centre used by
    /// the weighted fallback). Instances are immutable and safe to share between threads.
    /// </remarks>
    public sealed class SectionLatticeField
    {
        /// <summary>Pitch the service uses when a set does not state one.</summary>
        public const double DefaultPitchNm = 2000.0;

        /// <summary>Kernel radius the service uses when a set does not state one.</summary>
        public const double DefaultKernelRadiusNm = 8000.0;

        const double WeightEpsilon = 0.001;
        const double DistanceEpsilonSquared = 1.0;

        readonly Dictionary<(int Gx, int Gy), (double Dx, double Dy)> _nodes;

        /// <param name="pitchNm">Lattice spacing; node (gx, gy) sits at (gx * pitch, gy * pitch). Non-positive means the default.</param>
        /// <param name="kernelRadiusNm">Reach of the weighted fallback outside fully surrounded cells. Non-positive means the default.</param>
        /// <param name="nodes">Absolute lattice nodes with their offsets in nanometres. A repeated node keeps the last value.</param>
        public SectionLatticeField(double pitchNm, double kernelRadiusNm, IEnumerable<(int Gx, int Gy, double Dx, double Dy)> nodes)
        {
            PitchNm = pitchNm > 0 ? pitchNm : DefaultPitchNm;
            KernelRadiusNm = kernelRadiusNm > 0 ? kernelRadiusNm : DefaultKernelRadiusNm;
            _nodes = new Dictionary<(int, int), (double, double)>();
            if (nodes == null)
                return;
            foreach ((int gx, int gy, double dx, double dy) in nodes)
                _nodes[(gx, gy)] = (dx, dy);
        }

        /// <summary>Lattice spacing in nanometres.</summary>
        public double PitchNm { get; }

        /// <summary>Reach of the weighted fallback in nanometres.</summary>
        public double KernelRadiusNm { get; }

        /// <summary>Number of lattice nodes held.</summary>
        public int NodeCount => _nodes.Count;

        /// <summary>
        /// Axis-aligned lattice bounds in nanometres: origin at the minimum node cell, width/height in cell counts.
        /// Empty fields return a zero-size box at the origin.
        /// </summary>
        public (double OriginX, double OriginY, int Width, int Height) OccupiedRasterBounds()
        {
            if (_nodes.Count == 0)
                return (0, 0, 0, 0);

            int gxMin = int.MaxValue, gxMax = int.MinValue, gyMin = int.MaxValue, gyMax = int.MinValue;
            foreach ((int gx, int gy) in _nodes.Keys)
            {
                if (gx < gxMin) gxMin = gx;
                if (gx > gxMax) gxMax = gx;
                if (gy < gyMin) gyMin = gy;
                if (gy > gyMax) gyMax = gy;
            }

            return (gxMin * PitchNm, gyMin * PitchNm, gxMax - gxMin + 1, gyMax - gyMin + 1);
        }

        /// <summary>
        /// Even display grid of trusted samples, matching the SectionCorrectionBuilder quiver preview.
        /// </summary>
        public IEnumerable<(double OriginX, double OriginY, double Dx, double Dy)> EnumerateTrustedDisplaySamples(double displayPitchNm)
        {
            (double originX, double originY, int width, int height) = OccupiedRasterBounds();
            if (width <= 0 || height <= 0)
                yield break;

            double extentX = width * PitchNm;
            double extentY = height * PitchNm;
            double pitch = displayPitchNm > 0 ? displayPitchNm : PitchNm * 0.5;
            for (double y = originY; y <= originY + extentY + 0.5 * pitch; y += pitch)
            {
                for (double x = originX; x <= originX + extentX + 0.5 * pitch; x += pitch)
                {
                    (double dx, double dy, bool trusted) = Sample(x, y);
                    if (!trusted)
                        continue;
                    double mag = Math.Sqrt(dx * dx + dy * dy);
                    if (mag <= 1e-6)
                        continue;
                    yield return (x, y, dx, dy);
                }
            }
        }

        /// <summary>
        /// Offset at a point in volume space. Bilinear when the four surrounding nodes exist, otherwise inverse-distance
        /// weighted over the nodes within the kernel radius.
        /// </summary>
        /// <param name="xNm">Volume X in nanometres.</param>
        /// <param name="yNm">Volume Y in nanometres.</param>
        /// <returns>
        /// The offset in nanometres, and whether the point has support. Untrusted points return a zero offset and must not
        /// contribute to a shim.
        /// </returns>
        public (double Dx, double Dy, bool Trusted) Sample(double xNm, double yNm)
        {
            double fx = xNm / PitchNm;
            double fy = yNm / PitchNm;
            int gx0 = (int)Math.Floor(fx);
            int gy0 = (int)Math.Floor(fy);
            double tx = fx - gx0;
            double ty = fy - gy0;

            if (_nodes.TryGetValue((gx0, gy0), out var n00) &&
                _nodes.TryGetValue((gx0 + 1, gy0), out var n10) &&
                _nodes.TryGetValue((gx0, gy0 + 1), out var n01) &&
                _nodes.TryGetValue((gx0 + 1, gy0 + 1), out var n11))
            {
                double ax = n00.Dx * (1 - tx) + n10.Dx * tx;
                double ay = n00.Dy * (1 - tx) + n10.Dy * tx;
                double bx = n01.Dx * (1 - tx) + n11.Dx * tx;
                double by = n01.Dy * (1 - tx) + n11.Dy * tx;
                return (ax * (1 - ty) + bx * ty, ay * (1 - ty) + by * ty, true);
            }

            int reach = (int)Math.Ceiling(KernelRadiusNm / PitchNm) + 1;
            double r2 = KernelRadiusNm * KernelRadiusNm;
            double weightSum = 0;
            double sumX = 0;
            double sumY = 0;
            for (int ox = -reach; ox <= reach; ox++)
            {
                for (int oy = -reach; oy <= reach; oy++)
                {
                    int gx = gx0 + ox;
                    int gy = gy0 + oy;
                    if (!_nodes.TryGetValue((gx, gy), out var n))
                        continue;

                    double cx = (gx + 0.5) * PitchNm;
                    double cy = (gy + 0.5) * PitchNm;
                    double d2 = (xNm - cx) * (xNm - cx) + (yNm - cy) * (yNm - cy);
                    if (d2 > r2)
                        continue;

                    double w = 1.0 / (d2 + DistanceEpsilonSquared);
                    sumX += n.Dx * w;
                    sumY += n.Dy * w;
                    weightSum += w;
                }
            }

            if (weightSum <= WeightEpsilon)
                return (0, 0, false);

            return (sumX / weightSum, sumY / weightSum, true);
        }
    }
}
