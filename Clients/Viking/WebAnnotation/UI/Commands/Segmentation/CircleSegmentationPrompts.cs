using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// SAM2 prompts for a circle: the circle itself as the mask (<see cref=\"StartingPrompt.Seed\"/>) and four axis clicks
    /// (<see cref="TryCreateStartingPrompt"/>), shared by auto-polygonize and the circle context menu,
    /// plus the other-structure avoid marks.
    /// </summary>
    internal static class CircleSegmentationPrompts
    {
        /// <summary>
        /// Distance of the four axis points from the center, as a fraction of the radius. They sit
        /// just inside the circle's edge, outside the inscribed square's edges (which are at 0.707),
        /// so SAM2 is told how far the object reaches as well as that the square is inside it.
        /// </summary>
        public const double StartingPointRadiusFraction = 0.95;

        /// <summary>The box and clicks that start a circle's segmentation, in volume space.</summary>
        internal readonly struct StartingPrompt(Rectangle box, IReadOnlyList<Vector2> points, Circle circle)
        {
            /// <summary>The square inscribed in the circle, axis-aligned.</summary>
            public Rectangle Box { get; } = box;

            /// <summary>Four foreground clicks: east, north, west, south of the center at <see cref="StartingPointRadiusFraction"/> of the radius.</summary>
            public IReadOnlyList<Vector2> Points { get; } = points;

            /// <summary>The circle in volume space, averaged over the transformed radius points.</summary>
            public Circle Circle { get; } = circle;

            /// <summary>The circle as the seed sent to the server: its disc, not <see cref="Box"/>.</summary>
            public SegmentationSeed Seed => SegmentationSeed.FromCircle(Circle);

            /// <summary>Center of <see cref="Box"/>, which is the circle's center.</summary>
            public Vector2 Center => new((Box.Left + Box.Right) / 2.0, (Box.Bottom + Box.Top) / 2.0);
        }

        /// <summary>
        /// The circle's center followed by the east, north, west and south ends of its radius, in mosaic
        /// space. Map all five with <see cref="ToVolumePoints"/> and give the result to
        /// <see cref="TryCreateStartingPrompt"/>, which builds the box and the clicks in volume space.
        /// </summary>
        public static IReadOnlyList<Vector2> CreateMosaicRadiusPoints(Circle mosaicCircle)
        {
            double radius = mosaicCircle.Radius;
            Vector2 center = mosaicCircle.Center;
            return
            [
                center,
                new Vector2(center.X + radius, center.Y),
                new Vector2(center.X, center.Y + radius),
                new Vector2(center.X - radius, center.Y),
                new Vector2(center.X, center.Y - radius),
            ];
        }

        /// <summary>
        /// Builds the starting prompt in volume space from the five points of <see cref="CreateMosaicRadiusPoints"/>
        /// after the section-to-volume transform. The transformed center and the mean distance from it to the four
        /// radius ends give the circle in volume space. The box and the four clicks are then built from that circle
        /// there: the box is the axis-aligned square inscribed in it, and the clicks lie exactly east, north, west and
        /// south of its center at <see cref="StartingPointRadiusFraction"/> of its radius. The prompt is therefore
        /// aligned to the axes of the image SAM2 sees whatever the transform does to the section. A transform
        /// that stretches the axes unequally makes the circle an ellipse, which the mean radius only approximates.
        /// False when a point failed to map or the radius is not positive.
        /// </summary>
        public static bool TryCreateStartingPrompt(IReadOnlyList<Vector2> volumeCenterAndRadiusPoints, out StartingPrompt prompt)
        {
            prompt = default;
            if (volumeCenterAndRadiusPoints is null || volumeCenterAndRadiusPoints.Count != 1 + AxisPointCount)
                return false;

            Vector2 center = volumeCenterAndRadiusPoints[0];
            double radius = volumeCenterAndRadiusPoints.Skip(1).Average(point => Vector2.Distance(center, point));
            if (!(radius > 0))
                return false;

            Rectangle circleAabb = CenteredAxisAlignedSquare(center, radius);
            Rectangle? seedBox = InscribedSeedBox(circleAabb, center);
            if (seedBox is null)
                return false;

            double reach = radius * StartingPointRadiusFraction;
            prompt = new StartingPrompt(
                seedBox.Value,
                [
                    new Vector2(center.X + reach, center.Y),
                    new Vector2(center.X, center.Y + reach),
                    new Vector2(center.X - reach, center.Y),
                    new Vector2(center.X, center.Y - reach),
                ],
                new Circle(center, radius));
            return true;
        }

        private const int AxisPointCount = 4;

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
        internal static long? StoreStructureParent(long structureId) =>
            Store.Structures.GetCachedObject(structureId)?.ParentID;

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
        /// The other structures' annotations as one background mask, in volume space: the disc of
        /// each circle (the same square <see cref="TryCreateStartingPrompt"/> seeds a circle with) and the
        /// polygon, with its holes, of each polygon annotation. Same annotation filter as
        /// <see cref="CreateOtherStructureBackgroundVolumePoints"/>.
        /// </summary>
        /// <remarks>
        /// "Everything inside is background, and the engine should grow it": a circle that is some other structure
        /// is background throughout its disc, and so is every polygon. Annotations whose points fail
        /// to map to volume space are skipped. The server takes one mask per request, so these are summed here.
        /// </remarks>
        public static SegmentationMaskUnion CreateOtherStructureBackgroundMask(
            IEnumerable<LocationObj> visible,
            IVolumeToSectionTransform transform,
            IReadOnlyCollection<long>? excludeLocationIds,
            long? excludeStructureId,
            Func<long, long?>? structureParentLookup = null)
        {
            SegmentationMaskUnion union = new();
            if (visible is null || transform is null)
                return union;

            foreach (LocationObj other in OtherStructureAnnotations(visible, excludeLocationIds, excludeStructureId, structureParentLookup))
            {
                switch (other.TypeCode)
                {
                    case LocationType.CIRCLE:
                        if (TryCreateStartingPrompt(
                                ToVolumePoints(CreateMosaicRadiusPoints(new Circle(other.Position, other.Radius)), transform),
                                out StartingPrompt square))
                        {
                            union.Add(square.Seed);
                        }

                        break;
                    case LocationType.POLYGON:
                    case LocationType.CURVEPOLYGON:
                        if (TryToVolumeRings(other, transform, out List<Vector2[]> rings))
                            union.Add(rings);
                        break;
                }
            }

            return union;
        }

        /// <summary>
        /// A polygon annotation's unsmoothed mosaic exterior ring and holes mapped to volume space. False when the
        /// shape is not a polygon, a ring is too short, or any vertex fails to map (a partial ring would misdraw the mask).
        /// </summary>
        private static bool TryToVolumeRings(LocationObj location, IVolumeToSectionTransform transform, out List<Vector2[]> rings)
        {
            rings = [];
            try
            {
                if (location.MosaicShape is null)
                    return false;

                Polygon mosaic = location.MosaicShape.ToPolygon();
                foreach (Vector2[] mosaicRing in new[] { mosaic.ExteriorRing }.Concat(mosaic.InteriorRings))
                {
                    if (mosaicRing is null || mosaicRing.Length < 3)
                        return false;

                    IReadOnlyList<Vector2> volumeRing = ToVolumePoints(mosaicRing, transform);
                    if (volumeRing.Count != mosaicRing.Length)
                        return false;

                    rings.Add([.. volumeRing]);
                }

                return rings.Count > 0;
            }
            catch (ArgumentException)
            {
                return false;
            }
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
        /// Prompt for several overlapping polygons of one structure. <paramref name="Seeds"/> are the polygons
        /// themselves (exterior rings and holes), sent summed into the foreground mask: everything inside them is
        /// object and the segmentation grows beyond. <paramref name="Foreground"/> holds one interior click per
        /// polygon, in the same order as <paramref name="Seeds"/>.
        /// </summary>
        public readonly record struct GroupPrompt(IReadOnlyList<Vector2> Foreground, IReadOnlyList<SegmentationSeed> Seeds);

        /// <summary>
        /// One seed and one click per polygon. Many boundary clicks in one prompt (the old 17 per polygon) made SAM2
        /// return a smaller mask than fewer clicks did. Polygons with no interior point, or degenerate rings, are skipped.
        /// </summary>
        public static GroupPrompt CreateGroupPromptFromPolygons(IEnumerable<Polygon> polygons)
        {
            if (polygons is null)
                return new GroupPrompt([], []);

            List<Vector2> clicks = [];
            List<SegmentationSeed> seeds = [];
            foreach (Polygon polygon in polygons)
            {
                if (polygon?.ExteriorRing is null || polygon.ExteriorRing.Length < 4)
                    continue;

                Vector2? click = InteriorPoint(polygon);
                if (click is null)
                    continue;

                clicks.Add(click.Value);
                seeds.Add(SegmentationSeed.FromPolygon(polygon, click.Value));
            }

            return new GroupPrompt(clicks, seeds);
        }

        /// <summary>
        /// Axis-aligned square centered on <paramref name="center"/> with the same size ratio as
        /// <see cref="TryCreateStartingPrompt"/>'s inscribed square for a circle whose AABB is
        /// <paramref name="boundingBox"/> (half-side = min(width,height) / (2√2)).
        /// </summary>
        internal static Rectangle? InscribedSeedBox(Rectangle boundingBox, Vector2 center)
        {
            double half = Math.Min(boundingBox.Width, boundingBox.Height) / (2.0 * Math.Sqrt(2.0));
            if (!(half > 0))
                return null;

            return CenteredAxisAlignedSquare(center, half);
        }

        /// <summary>
        /// Axis-aligned square centered on <paramref name="center"/> with half-side <paramref name="halfSide"/>.
        /// </summary>
        private static Rectangle CenteredAxisAlignedSquare(Vector2 center, double halfSide)
            => new(
                new Vector2(center.X - halfSide, center.Y - halfSide),
                new Vector2(center.X + halfSide, center.Y + halfSide));

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
    }
}
