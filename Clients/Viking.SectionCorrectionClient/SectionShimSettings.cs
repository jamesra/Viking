using System;

namespace Viking.SectionCorrectionClient
{
    /// <summary>
    /// How the correction shim fades in as the user zooms in. At low magnification the interpolated correction is
    /// not accurate enough to trust, so it has no effect at or above <see cref="FadeStartDownsample"/>, full effect at
    /// or below <see cref="FadeEndDownsample"/>, and scales linearly in between.
    /// </summary>
    public sealed class SectionShimSettings
    {
        /// <summary>Default <see cref="FadeStartDownsample"/>.</summary>
        public const double DefaultFadeStartDownsample = 8;

        /// <summary>Default <see cref="FadeEndDownsample"/>.</summary>
        public const double DefaultFadeEndDownsample = 4;

        /// <summary>
        /// Master switch. When false no offsets are requested and the view is not shifted, even at full magnification.
        /// Toggled from the status bar and saved with the annotation preferences.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Downsample (volume units per screen pixel) at and above which the shim is not applied.</summary>
        public double FadeStartDownsample { get; set; } = DefaultFadeStartDownsample;

        /// <summary>Downsample at and below which the shim is fully applied.</summary>
        public double FadeEndDownsample { get; set; } = DefaultFadeEndDownsample;

        /// <summary>
        /// Fraction of the shim to apply at <paramref name="downsample"/>, from 0 to 1. When the preferences are
        /// inverted or equal (start not above end) the fade degenerates to a step at <see cref="FadeEndDownsample"/>.
        /// </summary>
        public double Weight(double downsample)
        {
            if (double.IsNaN(downsample))
                return 0;
            if (FadeStartDownsample <= FadeEndDownsample)
                return downsample <= FadeEndDownsample ? 1 : 0;
            if (downsample >= FadeStartDownsample)
                return 0;
            if (downsample <= FadeEndDownsample)
                return 1;
            return (FadeStartDownsample - downsample) / (FadeStartDownsample - FadeEndDownsample);
        }
    }
}
