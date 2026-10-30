using Geometry;
using System;
using System.Linq;
using Viking.VolumeModel;

namespace WebAnnotationTests.Tools
{
    /// <summary>
    /// volume = A · mosaic + t for a 2x2 matrix A, mapped both ways and everywhere. Used to check that what is drawn follows
    /// the transform's local scale, rotation and shear (see <see cref="LocationVolumeGeometry"/>).
    /// </summary>
    internal class AffineTransform : IVolumeToSectionTransform
    {
        private readonly double a, b, c, d;
        private readonly Vector2 offset;
        private readonly double det;

        /// <param name="a">Row 1, column 1 of A.</param>
        /// <param name="b">Row 1, column 2 of A.</param>
        /// <param name="c">Row 2, column 1 of A.</param>
        /// <param name="d">Row 2, column 2 of A.</param>
        /// <param name="offset">The translation t.</param>
        public AffineTransform(double a, double b, double c, double d, Vector2 offset)
        {
            this.a = a; this.b = b; this.c = c; this.d = d;
            this.offset = offset;
            det = (a * d) - (b * c);
            if (Math.Abs(det) < 1e-12)
                throw new ArgumentException("A must be invertible");
        }

        /// <summary>Rotates by 30 degrees, scales x by 1.3 and y by 0.8, and shears x by 0.2 y, then translates.</summary>
        public static AffineTransform RotateScaleShear()
        {
            double angle = Math.PI / 6;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            // A = R · S · H, H = [[1, 0.2], [0, 1]], S = diag(1.3, 0.8)
            double s00 = 1.3, s01 = 1.3 * 0.2, s10 = 0, s11 = 0.8;
            return new AffineTransform(
                (cos * s00) - (sin * s10), (cos * s01) - (sin * s11),
                (sin * s00) + (cos * s10), (sin * s01) + (cos * s11),
                new Vector2(1500, -700));
        }

        /// <summary>The five-point mean scale <see cref="CircleMapping"/> gives for A: the mean length of A's columns.</summary>
        public double FivePointScale => (Math.Sqrt((a * a) + (c * c)) + Math.Sqrt((b * b) + (d * d))) / 2.0;

        public long ID => 0;
        public Rectangle? SectionBounds => null;
        public Rectangle? VolumeBounds => null;

        public Vector2 Forward(Vector2 p) => new((a * p.X) + (b * p.Y) + offset.X, (c * p.X) + (d * p.Y) + offset.Y);

        public Vector2 Inverse(Vector2 p)
        {
            double x = p.X - offset.X, y = p.Y - offset.Y;
            return new Vector2(((d * x) - (b * y)) / det, ((-c * x) + (a * y)) / det);
        }

        public bool TrySectionToVolume(Vector2 P, out Vector2 transformedP) { transformedP = Forward(P); return true; }
        public bool TryVolumeToSection(Vector2 P, out Vector2 transformedP) { transformedP = Inverse(P); return true; }
        public bool[] TrySectionToVolume(in Vector2[] Points, out Vector2[] transformedP) { transformedP = [.. Points.Select(Forward)]; return [.. Points.Select(_ => true)]; }
        public bool[] TryVolumeToSection(in Vector2[] Points, out Vector2[] transformedP) { transformedP = [.. Points.Select(Inverse)]; return [.. Points.Select(_ => true)]; }
        public Vector2 SectionToVolume(Vector2 P) => Forward(P);
        public Vector2 VolumeToSection(Vector2 P) => Inverse(P);
        public Vector2[] SectionToVolume(Vector2[] Points) => [.. Points.Select(Forward)];
        public Vector2[] VolumeToSection(Vector2[] Points) => [.. Points.Select(Inverse)];
    }

    /// <summary>volume = scale * mosaic + offset, in both directions.</summary>
    internal sealed class ScaleTransform(double scale, Vector2 offset) : AffineTransform(scale, 0, 0, scale, offset);

    /// <summary>
    /// A smooth non-linear warp with an exact inverse, so its scale, rotation and shear change from point to point as a stos
    /// warp's do: volume = R(25 degrees) · (1.2 x + 2e-5 y², 0.85 y) + (800, -300).
    /// </summary>
    internal sealed class WarpTransform : IVolumeToSectionTransform
    {
        private const double Ax = 1.2, By = 0.85, K = 2e-5;
        private static readonly double Cos = Math.Cos(25 * Math.PI / 180), Sin = Math.Sin(25 * Math.PI / 180);
        private static readonly Vector2 Offset = new(800, -300);

        public long ID => 1;
        public Rectangle? SectionBounds => null;
        public Rectangle? VolumeBounds => null;

        public static Vector2 Forward(Vector2 p)
        {
            double u = (Ax * p.X) + (K * p.Y * p.Y), v = By * p.Y;
            return new Vector2((Cos * u) - (Sin * v) + Offset.X, (Sin * u) + (Cos * v) + Offset.Y);
        }

        public static Vector2 Inverse(Vector2 p)
        {
            double x = p.X - Offset.X, y = p.Y - Offset.Y;
            double u = (Cos * x) + (Sin * y), v = (-Sin * x) + (Cos * y);
            double my = v / By;
            return new Vector2((u - (K * my * my)) / Ax, my);
        }

        public bool TrySectionToVolume(Vector2 P, out Vector2 transformedP) { transformedP = Forward(P); return true; }
        public bool TryVolumeToSection(Vector2 P, out Vector2 transformedP) { transformedP = Inverse(P); return true; }
        public bool[] TrySectionToVolume(in Vector2[] Points, out Vector2[] transformedP) { transformedP = [.. Points.Select(Forward)]; return [.. Points.Select(_ => true)]; }
        public bool[] TryVolumeToSection(in Vector2[] Points, out Vector2[] transformedP) { transformedP = [.. Points.Select(Inverse)]; return [.. Points.Select(_ => true)]; }
        public Vector2 SectionToVolume(Vector2 P) => Forward(P);
        public Vector2 VolumeToSection(Vector2 P) => Inverse(P);
        public Vector2[] SectionToVolume(Vector2[] Points) => [.. Points.Select(Forward)];
        public Vector2[] VolumeToSection(Vector2[] Points) => [.. Points.Select(Inverse)];
    }

    /// <summary>
    /// <paramref name="inner"/> where a volume point lies inside <paramref name="volumeRegion"/> (and a mosaic point maps
    /// into it); everywhere else the checked mappings fail and the unchecked ones throw, like a stos outside its hull.
    /// </summary>
    internal sealed class ClippedTransform(IVolumeToSectionTransform inner, Rectangle volumeRegion) : IVolumeToSectionTransform
    {
        public long ID => inner.ID;
        public Rectangle? SectionBounds => null;
        public Rectangle? VolumeBounds => volumeRegion;

        public bool TrySectionToVolume(Vector2 P, out Vector2 transformedP)
        {
            transformedP = inner.SectionToVolume(P);
            return volumeRegion.Contains(transformedP);
        }

        public bool TryVolumeToSection(Vector2 P, out Vector2 transformedP)
        {
            transformedP = volumeRegion.Contains(P) ? inner.VolumeToSection(P) : P;
            return volumeRegion.Contains(P);
        }

        public bool[] TrySectionToVolume(in Vector2[] Points, out Vector2[] transformedP)
        {
            Vector2[] mapped = new Vector2[Points.Length];
            bool[] ok = new bool[Points.Length];
            for (int i = 0; i < Points.Length; i++)
                ok[i] = TrySectionToVolume(Points[i], out mapped[i]);

            transformedP = mapped;
            return ok;
        }

        public bool[] TryVolumeToSection(in Vector2[] Points, out Vector2[] transformedP)
        {
            Vector2[] mapped = new Vector2[Points.Length];
            bool[] ok = new bool[Points.Length];
            for (int i = 0; i < Points.Length; i++)
                ok[i] = TryVolumeToSection(Points[i], out mapped[i]);

            transformedP = mapped;
            return ok;
        }

        public Vector2 SectionToVolume(Vector2 P) => TrySectionToVolume(P, out Vector2 v) ? v : throw new ArgumentOutOfRangeException(nameof(P));
        public Vector2 VolumeToSection(Vector2 P) => TryVolumeToSection(P, out Vector2 m) ? m : throw new ArgumentOutOfRangeException(nameof(P));
        public Vector2[] SectionToVolume(Vector2[] Points) => [.. Points.Select(SectionToVolume)];
        public Vector2[] VolumeToSection(Vector2[] Points) => [.. Points.Select(VolumeToSection)];
    }
}
