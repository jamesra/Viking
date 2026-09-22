using System;

namespace Geometry
{
    /// <summary>
    /// Shared numeric tolerance for Core geometry. Equality uses <see cref="Epsilon"/>;
    /// hash codes round coordinates to <see cref="SignificantDigits"/> so values equal
    /// within epsilon share a hash except at rounding-bin boundaries.
    /// Comparers remain exact (see <see cref="Vector2ComparerXY"/>).
    /// </summary>
    public static class Tolerance
    {
        public const double Epsilon = 0.001;
        public const double EpsilonSquared = Epsilon * Epsilon;
        public const int SignificantDigits = 3;
        public const int TransformSignificantDigits = 3;

        public static bool AreClose(double a, double b) => Math.Abs(a - b) <= Epsilon;

        public static double Round(double value) => Math.Round(value, SignificantDigits);
    }
}
