using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Viking.SectionCorrection
{
    /// <summary>
    /// Sparse residual lattice for one Z. Node world XY is (gx * PitchNm, gy * PitchNm).
    /// </summary>
    public sealed class SectionVectorField
    {
        readonly Dictionary<(int Gx, int Gy), LatticeNode> _nodes;

        public SectionVectorField(
            long z,
            double pitchNm,
            double kernelRadiusNm,
            IEnumerable<LatticeNode> nodes)
        {
            Z = z;
            PitchNm = pitchNm > 0 ? pitchNm : 2000.0;
            KernelRadiusNm = kernelRadiusNm > 0 ? kernelRadiusNm : 8000.0;
            _nodes = [];
            if (nodes != null)
            {
                foreach (LatticeNode node in nodes)
                    _nodes[(node.Gx, node.Gy)] = node;
            }
        }

        public long Z { get; }
        public double PitchNm { get; }
        public double KernelRadiusNm { get; }
        public int NodeCount => _nodes.Count;
        public IEnumerable<LatticeNode> Nodes => _nodes.Values;

        public bool TryGet(int gx, int gy, out LatticeNode node) => _nodes.TryGetValue((gx, gy), out node);

        /// <summary>
        /// Always returns an offset. Trusted is true inside compact support.
        /// </summary>
        public (Vector2 Offset, bool Trusted) Sample(Vector2 xy)
        {
            double fx = xy.X / PitchNm;
            double fy = xy.Y / PitchNm;
            int gx0 = (int)Math.Floor(fx);
            int gy0 = (int)Math.Floor(fy);
            double tx = fx - gx0;
            double ty = fy - gy0;

            if (TryGet(gx0, gy0, out LatticeNode n00) &&
                TryGet(gx0 + 1, gy0, out LatticeNode n10) &&
                TryGet(gx0, gy0 + 1, out LatticeNode n01) &&
                TryGet(gx0 + 1, gy0 + 1, out LatticeNode n11))
            {
                Vector2 a = n00.Offset * (1 - tx) + n10.Offset * tx;
                Vector2 b = n01.Offset * (1 - tx) + n11.Offset * tx;
                return (a * (1 - ty) + b * ty, true);
            }

            int reach = (int)Math.Ceiling(KernelRadiusNm / PitchNm) + 1;
            double weightSum = 0;
            Vector2 weighted = Vector2.Zero;
            const double eps2 = 1.0;
            double r2 = KernelRadiusNm * KernelRadiusNm;

            for (int dx = -reach; dx <= reach; dx++)
            {
                for (int dy = -reach; dy <= reach; dy++)
                {
                    if (!TryGet(gx0 + dx, gy0 + dy, out LatticeNode n))
                        continue;

                    double cx = (n.Gx + 0.5) * PitchNm;
                    double cy = (n.Gy + 0.5) * PitchNm;
                    double d2 = (xy.X - cx) * (xy.X - cx) + (xy.Y - cy) * (xy.Y - cy);
                    if (d2 > r2)
                        continue;

                    double w = 1.0 / (d2 + eps2);
                    weighted += n.Offset * w;
                    weightSum += w;
                }
            }

            if (weightSum <= Tolerance.Epsilon)
                return (Vector2.Zero, false);

            return (weighted * (1.0 / weightSum), true);
        }

        public IEnumerable<(Vector2 Origin, Vector2 Offset)> EnumerateTrustedDisplaySamples(double displayPitchNm)
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
                    (Vector2 offset, bool trusted) = Sample(new Vector2(x, y));
                    if (!trusted || offset.Magnitude <= 1e-6)
                        continue;
                    yield return (new Vector2(x, y), offset);
                }
            }
        }

        public (double OriginX, double OriginY, int Width, int Height) OccupiedRasterBounds()
        {
            if (_nodes.Count == 0)
                return (0, 0, 0, 0);

            int gxMin = _nodes.Keys.Min(k => k.Gx);
            int gxMax = _nodes.Keys.Max(k => k.Gx);
            int gyMin = _nodes.Keys.Min(k => k.Gy);
            int gyMax = _nodes.Keys.Max(k => k.Gy);
            return (gxMin * PitchNm, gyMin * PitchNm, gxMax - gxMin + 1, gyMax - gyMin + 1);
        }
    }

    public readonly struct LatticeNode(int gx, int gy, double dx, double dy, int voteCount)
    {
        public int Gx { get; } = gx;
        public int Gy { get; } = gy;
        public double Dx { get; } = dx;
        public double Dy { get; } = dy;
        public int VoteCount { get; } = voteCount;
        public Vector2 Offset => new(Dx, Dy);
    }
}
