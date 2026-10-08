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
        /// Segments from highest to lowest score, or empty when the response has none.
        /// Tie-breaking matches <see cref="Enumerable.OrderByDescending{TSource, TKey}(IEnumerable{TSource}, Func{TSource, TKey})"/>.
        /// </summary>
        internal static IEnumerable<SegmentationServiceTypes.SegmentResult> GetSegmentsByDescendingScore(
            this SegmentationServiceTypes.SegmentationResponse? response)
        {
            if (response?.Segments is null || response.Segments.Count == 0)
                yield break;

            foreach (SegmentationServiceTypes.SegmentResult segment in response.Segments.OrderByDescending(s => s.Score))
                yield return segment;
        }

        /// <summary>
        /// Returns the segment with the highest <see cref="SegmentationServiceTypes.SegmentResult.Score"/>,
        /// or null when the response has no segments. Matches the previous
        /// <c>OrderByDescending(s =&gt; s.Score).First()</c> choice (stable tie-break on list order).
        /// </summary>
        internal static SegmentationServiceTypes.SegmentResult? GetHighestScoringSegment(
            this SegmentationServiceTypes.SegmentationResponse? response)
            => response.GetSegmentsByDescendingScore().FirstOrDefault();

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

        /// <summary>
        /// Converts a protobuf Polygon to a Polygon by transforming viewport pixel coordinates to world coordinates
        /// </summary>
        /// <param name="protoPolygon">The protobuf polygon to convert</param>
        /// <param name="viewportBounds">The world-space bounds of the viewport</param>
        /// <param name="viewportWidth">Width of the viewport in pixels</param>
        /// <param name="viewportHeight">Height of the viewport in pixels</param>
        /// <returns>A Polygon in world coordinates</returns>
        public static Polygon ToPolygon(
            this SegmentationServiceTypes.Polygon protoPolygon,
            Rectangle viewportBounds,
            int viewportWidth,
            int viewportHeight)
        {
            if (protoPolygon is null || protoPolygon.Points.Count < 3)
                return null;

            List<Vector2> worldPoints = new(protoPolygon.Points.Count);

            foreach (var point in protoPolygon.Points)
            {
                worldPoints.Add(MapViewportPixelToWorld(
                    point.X,
                    point.Y,
                    viewportBounds,
                    viewportWidth,
                    viewportHeight));
            }

            return new Polygon(worldPoints.EnsureClosedRing().RemoveAdjacentDuplicates());
        }
    }
}

