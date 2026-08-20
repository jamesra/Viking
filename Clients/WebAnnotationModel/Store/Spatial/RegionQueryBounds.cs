using System;
using Geometry;

namespace WebAnnotationModel
{
    /// <summary>
    /// Shared visible-region padding and LOD compare for region streams.
    /// LocationStore (gRPC) and RegionLoader (cell pyramid) both use this so a pan/zoom
    /// wait token never defines which streams stay alive.
    /// </summary>
    public static class RegionQueryBounds
    {
        /// <summary>
        /// Extra viewport sizes kept around the visible screen before a region stream is cancelled.
        /// 0.5 retains half a screen on each side.
        /// </summary>
        public const double VisiblePadFactor = 0.5;

        /// <summary>
        /// Seconds a completed region query remains valid before the same covered view hits the server again.
        /// </summary>
        public const double RefreshIntervalSeconds = 180;

        public static Rectangle PadVisible(in Rectangle visible)
        {
            double padX = Math.Abs(visible.Width) * VisiblePadFactor;
            double padY = Math.Abs(visible.Height) * VisiblePadFactor;
            return new Rectangle(visible.Left - padX, visible.Right + padX, visible.Bottom - padY, visible.Top + padY);
        }

        /// <summary>
        /// True when two pixel sizes map to the same annotation LOD (less than a 2× downsample step).
        /// </summary>
        public static bool SameLod(double pixelA, double pixelB)
        {
            if (pixelA <= 0 || pixelB <= 0)
                return pixelA == pixelB;
            double ratio = pixelA > pixelB ? pixelA / pixelB : pixelB / pixelA;
            return ratio < 2.0;
        }
    }
}
