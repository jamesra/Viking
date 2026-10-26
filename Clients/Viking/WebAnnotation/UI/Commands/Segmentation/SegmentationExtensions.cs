using Geometry;
using System.Collections.Generic;
using System.Linq;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Extension methods for segmentation-related conversions
    /// </summary>
    public static class SegmentationExtensions
    {
        /// <summary>
        /// The islands of <paramref name="response"/> in island order (1, the largest trusted area, first), or empty when it has
        /// none. A server numbers its islands 1.. in the order it sends them; sorting keeps that order however they arrive, and
        /// the sort is stable so islands with the same number keep the order they were sent in.
        /// </summary>
        internal static IEnumerable<SegmentationServiceTypes.SegmentResult> GetSegmentsByIsland(
            this SegmentationServiceTypes.SegmentationResponse? response)
        {
            if (response?.Segments is null || response.Segments.Count == 0)
                return [];

            return response.Segments.OrderBy(segment => segment.Island);
        }

        /// <summary>
        /// Island 1 of <paramref name="response"/>: the island with the largest trusted area, the object a caller that keeps one
        /// shape keeps. The lowest island number when no island is numbered 1; null when the response has no segments.
        /// </summary>
        internal static SegmentationServiceTypes.SegmentResult? GetPrimaryIsland(
            this SegmentationServiceTypes.SegmentationResponse? response)
            => response.GetSegmentsByIsland().FirstOrDefault();

        /// <summary>
        /// Maps world coordinates to capture-pixel space without a Y flip. SAM2 mask Y is flipped elsewhere.
        /// </summary>
        internal static Vector2 MapWorldToViewportPixel(
            Vector2 worldPos,
            Rectangle viewportBounds,
            int viewportWidth,
            int viewportHeight)
        {
            Vector2 boundsMin = viewportBounds.LowerLeft;
            Vector2 boundsMax = viewportBounds.UpperRight;

            double normalizedX = (worldPos.X - boundsMin.X) / (boundsMax.X - boundsMin.X);
            double normalizedY = (worldPos.Y - boundsMin.Y) / (boundsMax.Y - boundsMin.Y);

            return new Vector2(
                normalizedX * viewportWidth,
                normalizedY * viewportHeight);
        }

        /// <summary>
        /// Inverse of <see cref="MapWorldToViewportPixel"/>; pixel Y is not flipped here.
        /// </summary>
        internal static Vector2 MapViewportPixelToWorld(
            double pixelX,
            double pixelY,
            Rectangle viewportBounds,
            int viewportWidth,
            int viewportHeight)
        {
            double normalizedX = pixelX / viewportWidth;
            double normalizedY = pixelY / viewportHeight;
            Vector2 boundsMin = viewportBounds.LowerLeft;
            Vector2 boundsMax = viewportBounds.UpperRight;
            return new Vector2(
                boundsMin.X + normalizedX * (boundsMax.X - boundsMin.X),
                boundsMin.Y + normalizedY * (boundsMax.Y - boundsMin.Y));
        }
    }
}
