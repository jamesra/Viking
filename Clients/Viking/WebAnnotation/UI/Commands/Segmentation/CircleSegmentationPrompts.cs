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
        public static IReadOnlyList<Vector2> CreateMosaicForegroundPoints(Circle mosaicCircle)
        {
            List<Vector2> foregroundPoints = [mosaicCircle.Center];
            AddRing(foregroundPoints, mosaicCircle.Center, mosaicCircle.Radius * InnerRingRadiusFraction);
            AddRing(foregroundPoints, mosaicCircle.Center, mosaicCircle.Radius * OuterRingRadiusFraction);
            return foregroundPoints;
        }

        /// <summary>
        /// Drops points the section-to-volume transform fails to map.
        /// </summary>
        public static IReadOnlyList<Vector2> ToVolumePoints(
            IReadOnlyList<Vector2> mosaicPoints,
            IVolumeToSectionTransform transform)
        {
            if (mosaicPoints is null || mosaicPoints.Count == 0 || transform is null)
                return [];

            bool[] success = transform.TrySectionToVolume([.. mosaicPoints], out Vector2[] volumePoints);
            return [.. volumePoints.Where((p, i) => i < success.Length && success[i])];
        }

        /// <summary>
        /// False for the location being segmented, for other locations of the same structure, and
        /// for locations of any descendant (child, grandchild, ...) structure of that structure.
        /// True for every other annotation, including other types (AC on a GC).
        /// </summary>
        /// <param name="structureParentLookup">
        /// Maps a structure id to its parent structure id, or null when it has none or is not loaded.
        /// Null means descendants are not recognised and only the same-structure rule applies.
        /// </param>
        public static bool IsOtherStructure(
            long candidateId,
            long? candidateParentId,
            IReadOnlyCollection<long>? excludeLocationIds,
            long? excludeStructureId,
            Func<long, long?>? structureParentLookup = null)
        {
            if (excludeLocationIds is not null && excludeLocationIds.Contains(candidateId))
                return false;
            if (excludeStructureId.HasValue && candidateParentId == excludeStructureId)
                return false;
            if (excludeStructureId.HasValue && candidateParentId.HasValue && structureParentLookup is not null &&
                IsDescendantStructure(candidateParentId.Value, excludeStructureId.Value, structureParentLookup))
                return false;
            return true;
        }

        /// <summary>
        /// True when <paramref name="ancestorId"/> appears above <paramref name="structureId"/> in the
        /// structure parent chain. A structure is not its own descendant.
        /// </summary>
        /// <remarks>
        /// The walk stops at a structure with no parent or one the lookup cannot resolve, so a child whose
        /// ancestors are not loaded yet reads as unrelated until they arrive. The visited set guards a
        /// corrupt parent cycle from looping forever.
        /// </remarks>
        public static bool IsDescendantStructure(
            long structureId,
            long ancestorId,
            Func<long, long?> structureParentLookup)
        {
            if (structureParentLookup is null)
                throw new ArgumentNullException(nameof(structureParentLookup));

            HashSet<long> visited = [structureId];
            long? current = structureParentLookup(structureId);
            while (current.HasValue)
            {
                if (current.Value == ancestorId)
                    return true;
                if (!visited.Add(current.Value))
                    return false;
                current = structureParentLookup(current.Value);
            }

            return false;
        }

        /// <summary>
        /// Parent structure id from the local annotation store without a server fetch.
        /// Null when the structure has no parent or is not cached.
        /// </summary>
        private static long? StoreStructureParent(long structureId) =>
            Store.Structures.GetObjectByID(structureId, false)?.ParentID;

        /// <summary>
        /// Visible annotations that are not <paramref name="excludeLocationIds"/>, not members of
        /// <paramref name="excludeStructureId"/>, and not members of one of its descendant structures.
        /// </summary>
        /// <param name="structureParentLookup">Defaults to the local annotation store.</param>
        public static IEnumerable<LocationObj> OtherStructureAnnotations(
            IEnumerable<LocationObj> annotations,
            IReadOnlyCollection<long>? excludeLocationIds,
            long? excludeStructureId,
            Func<long, long?>? structureParentLookup = null)
        {
            if (annotations is null)
                return [];

            Func<long, long?> lookup = structureParentLookup ?? StoreStructureParent;
            return annotations.Where(loc => loc is not null &&
                IsOtherStructure(loc.ID, loc.ParentID, excludeLocationIds, excludeStructureId, lookup));
        }

        /// <summary>
        /// Representative points of other annotations, mapped to volume as SAM2 background prompts.
        /// Polygon avoid marks are the centroid of the largest MosaicShape Delaunay
        /// triangle, not VolumeShape (Catmull-Rom smoothed for CURVEPOLYGON).
        /// </summary>
        public static IReadOnlyList<Vector2> CreateBackgroundVolumePoints(
            IEnumerable<LocationObj> otherAnnotations,
            IVolumeToSectionTransform transform)
        {
            IReadOnlyList<Vector2> mosaicPoints = AnnotationPointExtensions.GetAnnotationRepresentativePoints(otherAnnotations);
            return ToVolumePoints(mosaicPoints, transform);
        }

        /// <summary>
        /// Avoid marks for every visible annotation that is not the target location, the
        /// same structure, or a child structure of it. Child structures sit inside or on the
        /// target, so a red mark there would carve them out of the mask. Drops points that sit on
        /// a foreground click.
        /// </summary>
        public static IReadOnlyList<Vector2> CreateOtherStructureBackgroundVolumePoints(
            IEnumerable<LocationObj> visible,
            IVolumeToSectionTransform transform,
            IReadOnlyList<Vector2> foreground,
            double minDistance,
            IReadOnlyCollection<long>? excludeLocationIds,
            long? excludeStructureId,
            Func<long, long?>? structureParentLookup = null)
        {
            return ExceptNearForeground(
                CreateBackgroundVolumePoints(
                    OtherStructureAnnotations(visible, excludeLocationIds, excludeStructureId, structureParentLookup),
                    transform),
                foreground,
                minDistance);
        }

        /// <summary>
        /// Volume-space SAM2 clicks from overlapping proposal polygons: each centroid (when
        /// inside) plus a subsampled exterior so one SegmentImage can cover the group.
        /// Degenerate or empty rings are skipped.
        /// </summary>
        public static IReadOnlyList<Vector2> CreateForegroundPointsFromPolygons(
            IEnumerable<Polygon> polygons,
            int maxRingPointsPerPolygon = 16)
        {
            if (polygons is null || maxRingPointsPerPolygon <= 0)
                return [];

            List<Vector2> points = [];
            foreach (Polygon polygon in polygons)
            {
                if (polygon?.ExteriorRing is null || polygon.ExteriorRing.Length < 4)
                    continue;

                Vector2 centroid = polygon.Centroid;
                if (polygon.Contains(centroid))
                    points.Add(centroid);

                foreach (Vector2 vertex in SubsampleClosedRing(polygon.ExteriorRing, maxRingPointsPerPolygon))
                    points.Add(vertex);
            }

            return points;
        }

        /// <summary>
        /// Drops avoid prompts that sit on a foreground point. Adjacent-section siblings of the
        /// same structure share XY with the selected circle; a red mark there cancels the center click.
        /// </summary>
        public static IReadOnlyList<Vector2> ExceptNearForeground(
            IReadOnlyList<Vector2> background,
            IReadOnlyList<Vector2> foreground,
            double minDistance)
        {
            if (background is null || background.Count == 0)
                return [];

            if (foreground is null || foreground.Count == 0 || minDistance <= 0)
                return background;

            double minDistanceSquared = minDistance * minDistance;
            return [.. background.Where(bg => foreground.All(fg =>
                Vector2.DistanceSquared(bg, fg) >= minDistanceSquared))];
        }

        /// <summary>
        /// Evenly spaced vertices from a closed ring (first==last). Fewer than 3 unique points yield nothing.
        /// </summary>
        private static IEnumerable<Vector2> SubsampleClosedRing(Vector2[] ring, int maxPoints)
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

        private static void AddRing(List<Vector2> points, Vector2 center, double radius)
        {
            for (int i = 0; i < ForegroundRingPointCount; i++)
            {
                double angle = (2.0 * Math.PI * i) / ForegroundRingPointCount;
                points.Add(new Vector2(
                    center.X + radius * Math.Cos(angle),
                    center.Y + radius * Math.Sin(angle)));
            }
        }
    }
}
