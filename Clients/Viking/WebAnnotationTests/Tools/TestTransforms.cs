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
}
