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
        /// Returns false when the length is not positive and finite; otherwise writes the rounded bar distance.
        /// </summary>
        public static bool TryRoundReadableLengthToBarDistance(double readableLength, out double measureBarDistance)
        {
            measureBarDistance = 0;
            if (readableLength <= 0 || double.IsNaN(readableLength) || double.IsInfinity(readableLength))
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
