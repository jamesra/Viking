using System;

namespace MeasurementExtension
{
    /// <summary>
    /// Rounds a readable scale-bar length to a 1× or 5× power of ten in display units.
    /// Used by <see cref="MeasureOverlay"/> before converting back to pixels.
    /// </summary>
    public static class ScaleBarLayout
    {
        private static readonly double LogFive = Math.Log(5);

        /// <summary>
        /// True when <paramref name="value"/> is positive, finite, and safe to use as a scale length or units-per-pixel divisor.
        /// </summary>
        public static bool IsPositiveFinite(double value) =>
            value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);

        /// <summary>
        /// Returns false when the length is not positive and finite; otherwise writes the rounded bar distance.
        /// </summary>
        public static bool TryRoundReadableLengthToBarDistance(double readableLength, out double measureBarDistance)
        {
            measureBarDistance = 0;
            if (!IsPositiveFinite(readableLength))
                return false;

            double log10 = Math.Log10(readableLength);
            int numDigits = Convert.ToInt32(Math.Ceiling(log10));
            measureBarDistance = Math.Pow(10, numDigits);

            if (log10 - Math.Floor(log10) > LogFive - 1)
                measureBarDistance *= 5;

            return true;
        }
    }
}
