using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using Viking.VolumeModel;
using WebAnnotationModel;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// SAM2 prompt points for a circle. Context-menu Segment to Polygon and auto-polygonize
    /// share this helper so both send the same seventeen foreground clicks and other-structure
    /// avoid marks.
    /// </summary>
    internal static class CircleSegmentationPrompts
    {
        /// <summary>Points on each concentric octagon.</summary>
        public const int ForegroundRingPointCount = 8;

        /// <summary>Inner ring stays well inside the disk.</summary>
        public const double InnerRingRadiusFraction = 0.5;

        /// <summary>Outer ring near the mosaic boundary so SAM2 fills the cell, not just the core.</summary>
        public const double OuterRingRadiusFraction = 0.8;

        /// <summary>
        /// Center plus two octagons (half-radius and 80% radius) in mosaic space. Seventeen points total.
        /// </summary>
        public static IReadOnlyList<GridVector2> CreateMosaicForegroundPoints(GridCircle mosaicCircle)
        {
            List<GridVector2> foregroundPoints = [mosaicCircle.Center];
            AddRing(foregroundPoints, mosaicCircle.Center, mosaicCircle.Radius * InnerRingRadiusFraction);
            AddRing(foregroundPoints, mosaicCircle.Center, mosaicCircle.Radius * OuterRingRadiusFraction);
            return foregroundPoints;
        }

        /// <summary>
        /// Drops points the section-to-volume transform fails to map.
        /// </summary>
        public static IReadOnlyList<GridVector2> ToVolumePoints(
            IReadOnlyList<GridVector2> mosaicPoints,
            IVolumeToSectionTransform transform)
        {
            if (mosaicPoints is null || mosaicPoints.Count == 0 || transform is null)
                return [];

            bool[] success = transform.TrySectionToVolume([.. mosaicPoints], out GridVector2[] volumePoints);
            return [.. volumePoints.Where((p, i) => i < success.Length && success[i])];
        }

        /// <summary>
        /// False for the location being segmented and for other locations of the same structure.
        /// True for every other annotation, including other types (AC on a GC).
        /// </summary>
        public static bool IsOtherStructure(
            long candidateId,
            long? candidateParentId,
            IReadOnlyCollection<long>? excludeLocationIds,
            long? excludeStructureId)
        {
            if (excludeLocationIds is not null && excludeLocationIds.Contains(candidateId))
                return false;
            if (excludeStructureId.HasValue && candidateParentId == excludeStructureId)
                return false;
            return true;
        }

        /// <summary>
        /// Visible annotations that are not <paramref name="excludeLocationIds"/> and not
        /// members of <paramref name="excludeStructureId"/>.
        /// </summary>
        public static IEnumerable<LocationObj> OtherStructureAnnotations(
            IEnumerable<LocationObj> annotations,
            IReadOnlyCollection<long>? excludeLocationIds,
            long? excludeStructureId)
        {
            if (annotations is null)
                return [];

            return annotations.Where(loc => loc is not null &&
                IsOtherStructure(loc.ID, loc.ParentID, excludeLocationIds, excludeStructureId));
        }

        /// <summary>
        /// Representative points of other annotations, mapped to volume as SAM2 background prompts.
        /// Polygon avoid marks are the Delaunay of MosaicShape (unsmoothed control
        /// points), not VolumeShape (Catmull-Rom smoothed for CURVEPOLYGON).
        /// </summary>
        public static IReadOnlyList<GridVector2> CreateBackgroundVolumePoints(
            IEnumerable<LocationObj> otherAnnotations,
            IVolumeToSectionTransform transform)
        {
            IReadOnlyList<GridVector2> mosaicPoints = AnnotationPointExtensions.GetAnnotationRepresentativePoints(otherAnnotations);
            return ToVolumePoints(mosaicPoints, transform);
        }

        /// <summary>
        /// Avoid marks for every visible annotation that is not the target location or the
        /// same structure. Drops points that sit on a foreground click.
        /// </summary>
        public static IReadOnlyList<GridVector2> CreateOtherStructureBackgroundVolumePoints(
            IEnumerable<LocationObj> visible,
            IVolumeToSectionTransform transform,
            IReadOnlyList<GridVector2> foreground,
            double minDistance,
            IReadOnlyCollection<long>? excludeLocationIds,
            long? excludeStructureId)
        {
            return ExceptNearForeground(
                CreateBackgroundVolumePoints(
                    OtherStructureAnnotations(visible, excludeLocationIds, excludeStructureId),
                    transform),
                foreground,
                minDistance);
        }

        /// <summary>
        /// Volume-space SAM2 clicks from overlapping proposal polygons: each centroid (when
        /// inside) plus a subsampled exterior so one SegmentImage can cover the group.
        /// Degenerate or empty rings are skipped.
        /// </summary>
        public static IReadOnlyList<GridVector2> CreateForegroundPointsFromPolygons(
            IEnumerable<GridPolygon> polygons,
            int maxRingPointsPerPolygon = 16)
        {
            if (polygons is null || maxRingPointsPerPolygon <= 0)
                return [];

            List<GridVector2> points = [];
            foreach (GridPolygon polygon in polygons)
            {
                if (polygon?.ExteriorRing is null || polygon.ExteriorRing.Length < 4)
                    continue;

                GridVector2 centroid = polygon.Centroid;
                if (polygon.Contains(centroid))
                    points.Add(centroid);

                foreach (GridVector2 vertex in SubsampleClosedRing(polygon.ExteriorRing, maxRingPointsPerPolygon))
                    points.Add(vertex);
            }

            return points;
        }

        /// <summary>
        /// Drops avoid prompts that sit on a foreground point. Adjacent-section siblings of the
        /// same structure share XY with the selected circle; a red mark there cancels the center click.
        /// </summary>
        public static IReadOnlyList<GridVector2> ExceptNearForeground(
            IReadOnlyList<GridVector2> background,
            IReadOnlyList<GridVector2> foreground,
            double minDistance)
        {
            if (background is null || background.Count == 0)
                return [];

            if (foreground is null || foreground.Count == 0 || minDistance <= 0)
                return background;

            double minDistanceSquared = minDistance * minDistance;
            return [.. background.Where(bg => foreground.All(fg =>
                GridVector2.DistanceSquared(bg, fg) >= minDistanceSquared))];
        }

        /// <summary>
        /// Evenly spaced vertices from a closed ring (first==last). Fewer than 3 unique points yield nothing.
        /// </summary>
        private static IEnumerable<GridVector2> SubsampleClosedRing(GridVector2[] ring, int maxPoints)
        {
            int count = ring.Length;
            if (count > 1 && ring[0] == ring[count - 1])
                count--;
            if (count < 3)
                yield break;

            int step = Math.Max(1, (int)Math.Ceiling(count / (double)maxPoints));
            for (int i = 0; i < count; i += step)
                yield return ring[i];
        }

        private static void AddRing(List<GridVector2> points, GridVector2 center, double radius)
        {
            for (int i = 0; i < ForegroundRingPointCount; i++)
            {
                double angle = (2.0 * Math.PI * i) / ForegroundRingPointCount;
                points.Add(new GridVector2(
                    center.X + radius * Math.Cos(angle),
                    center.Y + radius * Math.Sin(angle)));
            }
        }
    }
}
