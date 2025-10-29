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
        /// Converts a protobuf Polygon to a GridPolygon by transforming viewport pixel coordinates to world coordinates
        /// </summary>
        /// <param name="protoPolygon">The protobuf polygon to convert</param>
        /// <param name="viewportBounds">The world-space bounds of the viewport</param>
        /// <param name="viewportWidth">Width of the viewport in pixels</param>
        /// <param name="viewportHeight">Height of the viewport in pixels</param>
        /// <returns>A GridPolygon in world coordinates</returns>
        public static GridPolygon ToGridPolygon(
            this SegmentationServiceTypes.Polygon protoPolygon,
            GridRectangle viewportBounds,
            int viewportWidth,
            int viewportHeight)
        {
            if (protoPolygon == null || protoPolygon.Points.Count < 3)
                return null;

            // Transform each point from viewport pixel coordinates to world coordinates
            List<GridVector2> worldPoints = new List<GridVector2>(protoPolygon.Points.Count);
            
            GridVector2 topLeft = viewportBounds.LowerLeft;
            GridVector2 bottomRight = viewportBounds.UpperRight;

            foreach (var point in protoPolygon.Points)
            {
                // Convert pixel coordinates to normalized coordinates (0-1)
                double normalizedX = (double)point.X / viewportWidth;
                double normalizedY = (double)point.Y / viewportHeight;

                // Transform normalized coordinates to world coordinates
                GridVector2 worldPoint = new GridVector2(
                    topLeft.X + normalizedX * (bottomRight.X - topLeft.X),
                    topLeft.Y + normalizedY * (bottomRight.Y - topLeft.Y)
                );

                worldPoints.Add(worldPoint);
            }

            return new GridPolygon(worldPoints.EnsureClosedRing().RemoveAdjacentDuplicates());
        }
    }
}

