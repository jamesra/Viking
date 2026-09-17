using Geometry;
using System.Collections.Generic;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Extension methods for segmentation-related conversions
    /// </summary>
    public static class SegmentationExtensions
    {
        /// <summary>
        /// Converts a protobuf Polygon to a Polygon by transforming viewport pixel coordinates to world coordinates
        /// </summary>
        /// <param name="protoPolygon">The protobuf polygon to convert</param>
        /// <param name="viewportBounds">The world-space bounds of the viewport</param>
        /// <param name="viewportWidth">Width of the viewport in pixels</param>
        /// <param name="viewportHeight">Height of the viewport in pixels</param>
        /// <returns>A Polygon in world coordinates</returns>
        public static Polygon ToGridPolygon(
            this SegmentationServiceTypes.Polygon protoPolygon,
            Rectangle viewportBounds,
            int viewportWidth,
            int viewportHeight)
        {
            if (protoPolygon is null || protoPolygon.Points.Count < 3)
                return null;

            // Transform each point from viewport pixel coordinates to world coordinates
            List<Vector2> worldPoints = new(protoPolygon.Points.Count);

            Vector2 topLeft = viewportBounds.LowerLeft;
            Vector2 bottomRight = viewportBounds.UpperRight;

            foreach (var point in protoPolygon.Points)
            {
                // Convert pixel coordinates to normalized coordinates (0-1)
                double normalizedX = (double)point.X / viewportWidth;
                double normalizedY = (double)point.Y / viewportHeight;

                // Transform normalized coordinates to world coordinates
                Vector2 worldPoint = new(
                    topLeft.X + normalizedX * (bottomRight.X - topLeft.X),
                    topLeft.Y + normalizedY * (bottomRight.Y - topLeft.Y)
                );

                worldPoints.Add(worldPoint);
            }

            return new Polygon(worldPoints.EnsureClosedRing().RemoveAdjacentDuplicates());
        }
    }
}

