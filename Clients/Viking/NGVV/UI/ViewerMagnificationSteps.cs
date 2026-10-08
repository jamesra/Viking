using System;

namespace Viking.UI
{
    /// <summary>
    /// Discrete zoom steps for the viewer toolbar. Four Zoom In or Zoom Out clicks
    /// multiply magnification (downsample) by exactly 2 or 1/2; Home snaps to the
    /// nearest power of two. Used by <see cref="Controls.SectionViewerControl"/>
    /// toolbar buttons and unit tests — keep UI out of this type.
    /// </summary>
    public static class ViewerMagnificationSteps
    {
        /// <summary>
        /// Default downsample when Home cannot infer a valid level from the current value.
        /// </summary>
        private const double DefaultDownsample = 0.5;

        /// <summary>
        /// Per-click factor so four Zoom Out clicks double downsample (halve detail)
        /// and four Zoom In clicks do the inverse.
        /// </summary>
        public static readonly double StepFactor = Math.Pow(2.0, 0.25);

        /// <summary>
        /// True when <paramref name="downsample"/> is positive and finite — safe for zoom math.
        /// </summary>
        public static bool IsValidDownsample(double downsample) =>
            downsample > 0 && !double.IsNaN(downsample) && !double.IsInfinity(downsample);

        /// <summary>
        /// One Zoom In click: lower downsample by <see cref="StepFactor"/> (more detail).
        /// Non-finite or non-positive input is returned unchanged so corrupt state does not spread.
        /// </summary>
        public static double ZoomIn(double downsample) =>
            IsValidDownsample(downsample) ? downsample / StepFactor : downsample;

        /// <summary>
        /// One Zoom Out click: raise downsample by <see cref="StepFactor"/> (less detail).
        /// Non-finite or non-positive input is returned unchanged so corrupt state does not spread.
        /// </summary>
        public static double ZoomOut(double downsample) =>
            IsValidDownsample(downsample) ? downsample * StepFactor : downsample;

        /// <summary>
        /// Nearest power of two for the current downsample (e.g. 3 → 4, 0.7 → 0.5).
        /// Non-finite or non-positive values map to <see cref="DefaultDownsample"/> so Home never produces an invalid camera distance.
        /// </summary>
        public static double NearestPowerOfTwo(double downsample)
        {
            if (!IsValidDownsample(downsample))
                return DefaultDownsample;

            double exponent = Math.Round(Math.Log(downsample, 2.0));
            return Math.Pow(2.0, exponent);
        }
    }
}
