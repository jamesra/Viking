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
        /// Per-click factor so four Zoom Out clicks double downsample (halve detail)
        /// and four Zoom In clicks do the inverse.
        /// </summary>
        public static readonly double StepFactor = Math.Pow(2.0, 0.25);

        /// <summary>
        /// One Zoom In click: lower downsample by <see cref="StepFactor"/> (more detail).
        /// </summary>
        public static double ZoomIn(double downsample) =>
            downsample / StepFactor;

        /// <summary>
        /// One Zoom Out click: raise downsample by <see cref="StepFactor"/> (less detail).
        /// </summary>
        public static double ZoomOut(double downsample) =>
            downsample * StepFactor;

        /// <summary>
        /// Nearest power of two for the current downsample (e.g. 3 → 4, 0.7 → 0.5).
        /// Non-positive values map to 0.5 so Home never produces an invalid camera distance.
        /// </summary>
        public static double NearestPowerOfTwo(double downsample)
        {
            if (downsample <= 0)
                return 0.5;

            double exponent = Math.Round(Math.Log(downsample, 2.0));
            return Math.Pow(2.0, exponent);
        }
    }
}
