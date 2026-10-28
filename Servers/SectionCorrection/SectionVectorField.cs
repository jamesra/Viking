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
        /// Always returns an offset. Trusted is true inside compact support. <see cref="LatticeInterpolation.Sample"/>, the
        /// function the clients' <c>SectionLatticeField</c> uses too, so a client sampling a downloaded lattice gets what
        /// <c>CorrectPoints</c> returns.
        /// </summary>
        public (Vector2 Offset, bool Trusted) Sample(Vector2 xy) =>
            LatticeInterpolation.Sample(xy, PitchNm, KernelRadiusNm, TryGetOffset);

        bool TryGetOffset(int gx, int gy, out Vector2 offset)
        {
            bool found = _nodes.TryGetValue((gx, gy), out LatticeNode node);
            offset = found ? node.Offset : Vector2.Zero;
            return found;
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
