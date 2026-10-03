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
    /// share this helper so both send the same nine foreground clicks and other-structure
    /// avoid marks.
    /// </summary>
    internal static class CircleSegmentationPrompts
    {
        /// <summary>Points on each concentric ring.</summary>
        public const int ForegroundRingPointCount = 4;

        /// <summary>Inner ring stays well inside the disk.</summary>
        public const double InnerRingRadiusFraction = 0.5;

        /// <summary>Outer ring near the mosaic boundary so SAM2 fills the cell, not just the core.</summary>
        public const double OuterRingRadiusFraction = 0.8;

        /// <summary>
        /// Center plus two rings of four in mosaic space, nine points total. The outer ring (80% radius)
        /// sits on the axes and the inner ring (half radius) is rotated 45 degrees, so no ring point
        /// shares a ray from the center with another. Order is center, inner ring, outer ring.
        /// </summary>
        public static IReadOnlyList<Vector2> CreateMosaicForegroundPoints(Circle mosaicCircle)
        {
            List<Vector2> foregroundPoints = [mosaicCircle.Center];
            AddRing(foregroundPoints, mosaicCircle.Center, mosaicCircle.Radius * InnerRingRadiusFraction, Math.PI / 4.0);
            AddRing(foregroundPoints, mosaicCircle.Center, mosaicCircle.Radius * OuterRingRadiusFraction, 0.0);
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
        /// One diagnostic line for a segmentation request: which location and structure it is for, the
        /// foreground and background counts, the foreground center in volume space, and which visible
        /// annotations supplied or were withheld from the background.
        /// </summary>
        /// <remarks>
        /// Exists so a report that one spot segmented badly can be matched to a server session by the
        /// <c>fg0</c> volume coordinate and to the location by id. <c>bgFrom</c> lists id/structure of
        /// annotations that were eligible; <c>sameStructure</c> and <c>childStructure</c> list the ones
        /// the structure rules removed. An eligible annotation that yields no background point was
        /// dropped for sitting on a foreground click or failing to map to volume space, so
        /// <c>otherAnnotations</c> can exceed <c>bg</c>. Each id list is capped so the line stays one line.
        /// </remarks>
        /// <param name="source">Short name of the caller, such as auto-circle or context-menu.</param>
        public static string DescribePrompts(
            string source,
            IReadOnlyCollection<long>? locationIds,
            long? structureId,
            IEnumerable<LocationObj> visible,
            IReadOnlyList<Vector2> foreground,
            IReadOnlyList<Vector2> background,
            Func<long, long?>? structureParentLookup = null,
            int maxIds = 10)
        {
            Func<long, long?> lookup = structureParentLookup ?? StoreStructureParent;
            List<string> eligible = [];
            List<string> sameStructure = [];
            List<string> childStructure = [];
            int visibleCount = 0;

            foreach (LocationObj loc in visible ?? [])
            {
                if (loc is null)
                    continue;
                visibleCount++;

                if (locationIds is not null && locationIds.Contains(loc.ID))
                    continue;

                string label = $"{loc.ID}/{loc.ParentID?.ToString() ?? "-"}";
                if (IsOtherStructure(loc.ID, loc.ParentID, locationIds, structureId, lookup))
                    eligible.Add(label);
                else if (structureId.HasValue && loc.ParentID == structureId)
                    sameStructure.Add(label);
                else
                    childStructure.Add(label);
            }

            string fg0 = foreground is { Count: > 0 }
                ? $"({foreground[0].X:F0},{foreground[0].Y:F0})"
                : "none";
            string ids = locationIds is { Count: > 0 } ? string.Join(",", locationIds) : "-";

            return $"Prompts source={source} loc=[{ids}] structure={structureId?.ToString() ?? "-"} " +
                   $"fg={foreground?.Count ?? 0} fg0={fg0} bg={background?.Count ?? 0} visible={visibleCount} " +
                   $"otherAnnotations={eligible.Count} bgFrom=[{CapIds(eligible, maxIds)}] " +
                   $"sameStructure=[{CapIds(sameStructure, maxIds)}] childStructure=[{CapIds(childStructure, maxIds)}]";
        }

        private static string CapIds(List<string> ids, int max) =>
            ids.Count <= max
                ? string.Join(",", ids)
                : string.Join(",", ids.Take(max)) + $"+{ids.Count - max}";

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
        /// Prompt for several overlapping polygons of one structure: SAM2 takes one box per call.
        /// <paramref name="Box"/> is the bounding box of the largest polygon, or null when none qualifies.
        /// <paramref name="Foreground"/> holds one interior click per polygon, the largest included.
        /// </summary>
        public readonly record struct GroupPrompt(IReadOnlyList<Vector2> Foreground, Rectangle? Box);

        /// <summary>
        /// One SAM2 box and one click per polygon. The largest polygon (by area) supplies the box;
        /// every polygon, the largest included, supplies a single click inside it. Many boundary clicks
        /// in one prompt (the old 17 per polygon) made SAM2 return a smaller mask than fewer clicks did.
        /// Polygons with no interior point, or degenerate rings, are skipped.
        /// </summary>
        public static GroupPrompt CreateGroupPromptFromPolygons(IEnumerable<Polygon> polygons)
        {
            if (polygons is null)
                return new GroupPrompt([], null);

            List<Vector2> clicks = [];
            Rectangle? box = null;
            double largestArea = double.NegativeInfinity;
            foreach (Polygon polygon in polygons)
            {
                if (polygon?.ExteriorRing is null || polygon.ExteriorRing.Length < 4)
                    continue;

                Vector2? click = InteriorPoint(polygon);
                if (click is null)
                    continue;

                clicks.Add(click.Value);
                if (polygon.Area > largestArea)
                {
                    largestArea = polygon.Area;
                    box = polygon.BoundingBox;
                }
            }

            return new GroupPrompt(clicks, box);
        }

        /// <summary>
        /// The centroid when it lies inside the polygon. A concave ring can put it outside, so then the
        /// midpoint between the centroid and an exterior vertex is tried until one is inside.
        /// </summary>
        private static Vector2? InteriorPoint(Polygon polygon)
        {
            Vector2 centroid = polygon.Centroid;
            if (polygon.Contains(centroid))
                return centroid;

            foreach (Vector2 vertex in SubsampleClosedRing(polygon.ExteriorRing, 16))
            {
                Vector2 midpoint = (centroid + vertex) * 0.5f;
                if (polygon.Contains(midpoint))
                    return midpoint;
            }

            return null;
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

        private static void AddRing(List<Vector2> points, Vector2 center, double radius, double startAngle)
        {
            for (int i = 0; i < ForegroundRingPointCount; i++)
            {
                double angle = startAngle + (2.0 * Math.PI * i) / ForegroundRingPointCount;
                points.Add(new Vector2(
                    center.X + radius * Math.Cos(angle),
                    center.Y + radius * Math.Sin(angle)));
            }
        }
    }
}
