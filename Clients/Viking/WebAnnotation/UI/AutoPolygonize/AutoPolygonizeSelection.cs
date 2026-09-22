using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation.UI.Commands.Segmentation;
using WebAnnotationModel;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>
    /// Eligibility, hit-test, line-width, and overlap-carve helpers for auto-polygonize
    /// proposals. Circles must sit inside a 5% inset, lie entirely on the visible view, and
    /// meet the min radius in nanometers. Fitting in the scene is the only large-annotation
    /// gate. After SAM2 polygonize, other-structure POLYGON/CURVEPOLYGON annotations are
    /// subtracted from the proposed ring. Same-cell siblings are grouped and resubmitted
    /// instead of carved, including already-saved polygons of that structure.
    /// </summary>
    internal static class AutoPolygonizeSelection
    {
        public const double EdgeMarginFraction = 0.05;
        public const double HitTestPixels = 10.0;

        /// <summary>
        /// Matches <c>LocationCircleView</c> resize grab: SCALE when distance is at least 7/8 of radius,
        /// so the detection band is radius/8. Hollow proposal lines use half of that band.
        /// </summary>
        public const double CircleResizeBandFraction = 1.0 / 8.0;
        public const double LineWidthVsResizeBand = 0.5;
        public const double OriginalLineWidthPixels = 2.0;
        public const double MinLineWidthMultiplier = 2.0;
        public const double MaxLineWidthMultiplier = 6.0;

        /// <summary>
        /// First group resubmit plus one expansion if a later same-cell proposal overlaps the group.
        /// </summary>
        public const int MaxOverlapResubmitRound = 2;

        /// <summary>
        /// Ignore boundary-only touches when deciding whether an existing annotation occupies
        /// area of the proposal. Matches SqlGeometry coordinate rounding (two decimal places).
        /// </summary>
        public const double MinOverlapArea = 1e-2;

        /// <summary>
        /// World-space polyline width: half the circle-resize grab band, clamped to
        /// 2×–6× the original hollow-line width (<c>downsample * 2</c>).
        /// </summary>
        public static double ProposalLineWidth(double circleRadius, double downsample)
        {
            double safeDownsample = downsample > 0 ? downsample : 1.0;
            double originalWidth = safeDownsample * OriginalLineWidthPixels;
            double minWidth = originalWidth * MinLineWidthMultiplier;
            double maxWidth = originalWidth * MaxLineWidthMultiplier;
            double target = Math.Max(0, circleRadius) * CircleResizeBandFraction * LineWidthVsResizeBand;
            if (target < minWidth)
                return minWidth;
            if (target > maxWidth)
                return maxWidth;
            return target;
        }

        /// <summary>
        /// Shrinks the viewport by <see cref="EdgeMarginFraction"/> on each side so a circle
        /// whose center is closer than 5% to an edge is not auto-segmented.
        /// </summary>
        public static Rectangle InsetBounds(Rectangle viewBounds, double marginFraction = EdgeMarginFraction)
        {
            double marginX = viewBounds.Width * marginFraction;
            double marginY = viewBounds.Height * marginFraction;
            return new Rectangle(
                viewBounds.Left + marginX,
                viewBounds.Right - marginX,
                viewBounds.Bottom + marginY,
                viewBounds.Top - marginY);
        }

        /// <summary>
        /// True for a circle whose volume center is inside the inset bounds.
        /// </summary>
        public static bool IsEligibleCircle(LocationType typeCode, Vector2 volumeCenter, Rectangle inset)
        {
            return typeCode == LocationType.CIRCLE && inset.Covers(volumeCenter);
        }

        /// <summary>
        /// True when the disk of <paramref name="radius"/> around <paramref name="volumeCenter"/>
        /// lies entirely inside <paramref name="bounds"/>. Used so a clipped circle is not submitted.
        /// </summary>
        public static bool IsCircleEntirelyInside(Vector2 volumeCenter, double radius, Rectangle bounds)
        {
            if (radius < 0)
                return false;

            return bounds.Covers(new Rectangle(volumeCenter, radius));
        }

        /// <summary>
        /// Eligible when the center is at least 5% from each view edge, the disk is fully on
        /// <paramref name="viewBounds"/>, and the radius is at least
        /// <paramref name="minRadiusNanometers"/>. A circle that fits in the visible scene
        /// is sent regardless of how much of the capture it covers.
        /// <paramref name="mosaicRadius"/> and <paramref name="viewBounds"/> must share
        /// the same world space as <see cref="IsCircleEntirelyInside"/>.
        /// </summary>
        public static bool IsEligibleCircle(
            LocationType typeCode,
            Vector2 volumeCenter,
            Rectangle inset,
            Rectangle viewBounds,
            double mosaicRadius,
            double nanometersPerWorldUnit,
            double minRadiusNanometers)
        {
            return IsEligibleCircle(typeCode, volumeCenter, inset) &&
                   IsCircleEntirelyInside(volumeCenter, mosaicRadius, viewBounds) &&
                   MeetsMinRadiusNanometers(mosaicRadius, nanometersPerWorldUnit, minRadiusNanometers);
        }

        /// <summary>
        /// True when <paramref name="mosaicRadius"/> × <paramref name="nanometersPerWorldUnit"/>
        /// is at least <paramref name="minRadiusNanometers"/>. 0 accepts any positive radius.
        /// </summary>
        public static bool MeetsMinRadiusNanometers(
            double mosaicRadius,
            double nanometersPerWorldUnit,
            double minRadiusNanometers)
        {
            if (mosaicRadius <= 0)
                return false;
            if (minRadiusNanometers <= 0)
                return true;
            if (nanometersPerWorldUnit <= 0)
                return true;

            return mosaicRadius * nanometersPerWorldUnit >= minRadiusNanometers;
        }

        /// <summary>
        /// Shortest distance to the exterior or any hole ring. Used for hollow-line hit testing.
        /// </summary>
        public static double DistanceToAnyRing(Polygon polygon, Vector2 worldPosition)
        {
            double distance = polygon.Distance(worldPosition);
            if (polygon.InteriorPolygons is null)
                return distance;

            foreach (Polygon hole in polygon.InteriorPolygons)
            {
                double holeDistance = hole.Distance(worldPosition);
                if (holeDistance < distance)
                    distance = holeDistance;
            }

            return distance;
        }

        /// <summary>
        /// World-space Douglas-Peucker after polygonize, then Catmull-Rom control-point fit
        /// so MosaicShape stores curve-friendly vertices. Tolerance is in world units
        /// (typically <c>PenSimplifyThreshold * downsample</c>, i.e. screen pixels).
        /// Falls back to the DP ring when the fit self-intersects. Must not run the Catmull
        /// fit on a raw marching-squares staircase.
        /// </summary>
        public static Polygon SimplifyProposal(Polygon polygon, double tolerance)
        {
            Polygon simplified = SegmentationMaskPolygonizer.SimplifyRings(polygon, tolerance);
            return FitCurveControlPoints(simplified, tolerance);
        }

        /// <summary>
        /// Replaces Douglas-Peucker vertices with Catmull-Rom control points. Called after
        /// staircase collapse; fitting the raw marching-squares ring preserves dense stairs.
        /// </summary>
        internal static Polygon FitCurveControlPoints(Polygon polygon, double tolerance)
        {
            if (polygon is null || tolerance <= 0)
                return polygon;

            try
            {
                Polygon fitted = polygon.Simplify(tolerance);
                if (fitted.ExteriorSegments.SelfIntersects(LineSetOrdering.Closed))
                    return polygon;

                return fitted;
            }
            catch (ArgumentException)
            {
                return polygon;
            }
        }

        /// <summary>
        /// True when live downsample moved by 2× or more versus the cached upload.
        /// Same rule as annotation reload on camera change.
        /// </summary>
        public static bool DownsampleChangedByFactorOfTwo(double liveDownsample, double cachedDownsample)
        {
            if (cachedDownsample <= 0 || liveDownsample <= 0)
                return true;

            return liveDownsample >= 2 * cachedDownsample || liveDownsample <= cachedDownsample / 2;
        }

        /// <summary>
        /// True when the cached SAM2 image can be reused for a single-ID refresh:
        /// id present, zoom not 2× away, and the circle center still inside the uploaded world rectangle.
        /// </summary>
        public static bool CanReuseUploadedImage(
            in AutoPolygonizeUploadContext context,
            double liveDownsample,
            Vector2 volumeCenter)
        {
            if (!context.IsUsable)
                return false;
            if (DownsampleChangedByFactorOfTwo(liveDownsample, context.Downsample))
                return false;
            return context.WorldBounds.Covers(volumeCenter);
        }

        /// <summary>
        /// Squared-distance compare so batch SegmentImage can run nearest-to-farthest from the view center.
        /// Negative when <paramref name="a"/> is closer to <paramref name="center"/>.
        /// </summary>
        public static int CompareDistanceFromCenter(Vector2 a, Vector2 b, Vector2 center)
        {
            return Vector2.DistanceSquared(a, center).CompareTo(Vector2.DistanceSquared(b, center));
        }

        /// <summary>
        /// Stable nearest-first order around <paramref name="center"/>. Used by CollectEligibleCircles.
        /// Equal distances keep input order.
        /// </summary>
        public static List<T> OrderByDistanceFromCenter<T>(
            IEnumerable<T> items,
            Func<T, Vector2> position,
            Vector2 center)
        {
            return [.. items.OrderBy(item => Vector2.DistanceSquared(position(item), center))];
        }

        /// <summary>
        /// MosaicShape, VolumeShape, Position, Radius, or an empty name from a bulk update.
        /// </summary>
        public static bool IsGeometryProperty(string? propertyName)
        {
            return string.IsNullOrEmpty(propertyName) ||
                   propertyName is nameof(LocationObj.MosaicShape) or nameof(LocationObj.VolumeShape)
                       or nameof(LocationObj.Position) or nameof(LocationObj.Radius);
        }

        /// <summary>
        /// Same-cell, same-section proposals that nested-contain or cross <paramref name="seed"/>,
        /// including A–B–C chains. Edge-only contact is not grouped. Orphans without
        /// <see cref="OverlapCandidate.ParentID"/> stay a singleton.
        /// Called after a proposal is published to decide whether to resubmit as one SAM2 request.
        /// </summary>
        public static List<OverlapCandidate> CollectOverlappingSameCellComponent(
            OverlapCandidate seed,
            IReadOnlyList<OverlapCandidate> candidates)
        {
            if (seed.Polygon is null)
                return [];
            if (!seed.ParentID.HasValue)
                return [seed];

            List<OverlapCandidate> result = [seed];
            HashSet<long> seenIds = [.. seed.LocationIds];
            Queue<OverlapCandidate> pending = new();
            pending.Enqueue(seed);

            IReadOnlyList<OverlapCandidate> pool = candidates ?? [];
            while (pending.Count > 0)
            {
                OverlapCandidate current = pending.Dequeue();
                foreach (OverlapCandidate other in pool)
                {
                    if (other.Polygon is null || !other.ParentID.HasValue)
                        continue;
                    if (other.ParentID != seed.ParentID || other.SectionNumber != seed.SectionNumber)
                        continue;
                    if (other.LocationIds.All(seenIds.Contains))
                        continue;
                    if (!ShouldGroupSiblingPolygons(current.Polygon, other.Polygon))
                        continue;

                    foreach (long id in other.LocationIds)
                        seenIds.Add(id);
                    result.Add(other);
                    pending.Enqueue(other);
                }
            }

            return result;
        }

        /// <summary>
        /// Merge when one ring nested-contains the other or their interiors cross.
        /// Bare <see cref="ShapeRelation.Touching"/> (shared edge, no overlap) is not a sibling group.
        /// Both directions are required because <see cref="ShapeRelation.Contained"/> is one-sided.
        /// Used by <see cref="CollectOverlappingSameCellComponent"/>.
        /// </summary>
        internal static bool ShouldGroupSiblingPolygons(Polygon a, Polygon b)
        {
            if (a is null || b is null)
                return false;

            return ShouldGroupSiblingRelation(a.GetRelation(b)) || ShouldGroupSiblingRelation(b.GetRelation(a));
        }

        private static bool ShouldGroupSiblingRelation(ShapeRelation relation) =>
            (relation & (ShapeRelation.Contained | ShapeRelation.Intersecting)) != 0;

        /// <summary>
        /// Volume-space polygon for a saved POLYGON or CURVEPOLYGON. Circles and curves are
        /// skipped. Prefers MosaicShape mapped through <paramref name="transform"/> so the
        /// cut matches what LocationPolygonView draws when VolumeShape is stale.
        /// </summary>
        public static bool TryGetVolumePolygon(
            LocationObj? location,
            IVolumeToSectionTransform? transform,
            out Polygon? polygon)
        {
            polygon = null;
            if (location is null || !location.TypeCode.AllowsInteriorHoles())
                return false;

            try
            {
                if (transform is not null &&
                    location.MosaicShape is not null &&
                    !location.MosaicShape.IsNull)
                {
                    SqlGeometry mapped = transform.TryMapShapeSectionToVolume(location.MosaicShape);
                    if (mapped is not null && !mapped.IsNull)
                    {
                        polygon = mapped.ToPolygon();
                        return polygon is not null;
                    }
                }

                if (location.VolumeShape is null || location.VolumeShape.IsNull)
                    return false;

                polygon = location.VolumeShape.ToPolygon();
                return polygon is not null;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Same-section POLYGON/CURVEPOLYGON annotations whose area overlaps
        /// <paramref name="proposed"/>. Used before publishing or accepting a proposal.
        /// Pass <paramref name="excludeParentId"/> so same-cell siblings are merged, not carved.
        /// </summary>
        public static List<Polygon> CollectOverlappingExistingPolygons(
            IEnumerable<LocationObj>? annotations,
            Polygon proposed,
            IReadOnlyCollection<long>? excludeLocationIds = null,
            IVolumeToSectionTransform? transform = null,
            int? sectionNumber = null,
            long? excludeParentId = null)
        {
            List<Polygon> result = [];
            if (proposed is null || annotations is null)
                return result;

            foreach (LocationObj location in annotations)
            {
                if (location is null)
                    continue;
                if (excludeLocationIds is not null && excludeLocationIds.Contains(location.ID))
                    continue;
                if (excludeParentId.HasValue && location.ParentID == excludeParentId)
                    continue;
                if (sectionNumber.HasValue && location.Section != sectionNumber.Value)
                    continue;
                if (!TryGetVolumePolygon(location, transform, out Polygon? existing) || existing is null)
                    continue;
                if (!proposed.Intersects(existing))
                    continue;

                result.Add(existing);
            }

            return result;
        }

        /// <summary>
        /// Saved POLYGON/CURVEPOLYGON locations of one cell on one section, as overlap
        /// candidates. Called by overlap resubmit so already-accepted siblings join the
        /// group instead of remaining as stacked fills. IDs in
        /// <paramref name="excludeLocationIds"/> (in-flight proposals) are skipped.
        /// </summary>
        public static List<OverlapCandidate> CollectSavedSameCellPolygonCandidates(
            IEnumerable<LocationObj>? annotations,
            long parentId,
            int sectionNumber,
            IReadOnlyCollection<long>? excludeLocationIds = null,
            IVolumeToSectionTransform? transform = null)
        {
            List<OverlapCandidate> result = [];
            if (annotations is null)
                return result;

            foreach (LocationObj location in annotations)
            {
                if (location is null || location.ParentID != parentId || location.Section != sectionNumber)
                    continue;
                if (excludeLocationIds is not null && excludeLocationIds.Contains(location.ID))
                    continue;
                if (!TryGetVolumePolygon(location, transform, out Polygon? polygon) || polygon is null)
                    continue;

                result.Add(new OverlapCandidate([location.ID], parentId, sectionNumber, polygon));
            }

            return result;
        }

        /// <summary>
        /// Every same-cell overlap component with at least two location IDs.
        /// Used to find stacked saved siblings when no new circle proposal exists.
        /// </summary>
        public static List<List<OverlapCandidate>> CollectOverlappingSameCellComponents(
            IReadOnlyList<OverlapCandidate> candidates)
        {
            List<List<OverlapCandidate>> components = [];
            if (candidates is null || candidates.Count == 0)
                return components;

            HashSet<long> assigned = [];
            foreach (OverlapCandidate seed in candidates)
            {
                if (seed.LocationIds.All(assigned.Contains))
                    continue;

                List<OverlapCandidate> component = CollectOverlappingSameCellComponent(seed, candidates);
                foreach (long id in component.SelectMany(item => item.LocationIds))
                    assigned.Add(id);

                if (component.SelectMany(item => item.LocationIds).Distinct().Count() >= 2)
                    components.Add(component);
            }

            return components;
        }

        /// <summary>
        /// Boolean difference: proposed minus every overlapping existing polygon.
        /// When difference splits the ring, keeps the part that contains
        /// <paramref name="keepPoint"/>, otherwise the largest leftover.
        /// Returns the original instance when nothing occupies area; null when
        /// the existing annotations cover the proposal. STDifference failures
        /// leave the last successful remainder so a preview is not dropped.
        /// </summary>
        public static Polygon? SubtractOverlappingPolygons(
            Polygon? proposed,
            IEnumerable<Polygon>? existingPolygons,
            Vector2? keepPoint = null)
        {
            if (proposed is null)
                return null;

            IEnumerable<Polygon> existing = existingPolygons ?? [];
            SqlGeometry remaining;
            try
            {
                remaining = EnsureValid(proposed.ToSqlGeometry());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Auto polygonize subtract skipped: proposed is not valid SQL geometry ({ex.Message})");
                return proposed;
            }

            if (remaining is null || remaining.IsNull || remaining.STIsEmpty().IsTrue)
                return proposed;

            Polygon lastGood = proposed;
            try
            {
                foreach (Polygon existingPolygon in existing)
                {
                    if (existingPolygon is null)
                        continue;

                    SqlGeometry other = EnsureValid(existingPolygon.ToSqlGeometry());
                    if (other is null || other.IsNull || other.STIsEmpty().IsTrue)
                        continue;
                    if (remaining.STIntersects(other).IsFalse)
                        continue;
                    if (!HasPositiveAreaOverlap(remaining, other))
                        continue;

                    remaining = EnsureValid(remaining.STDifference(other));
                    if (remaining is null || remaining.IsNull || remaining.STIsEmpty().IsTrue)
                        return null;

                    Polygon? converted = TrySelectPolygonPart(remaining, keepPoint);
                    if (converted is null)
                        return null;

                    lastGood = converted;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Auto polygonize subtract failed: {ex.Message}");
                return lastGood;
            }

            return lastGood;
        }

        private static bool HasPositiveAreaOverlap(SqlGeometry a, SqlGeometry b)
        {
            try
            {
                SqlGeometry intersection = a.STIntersection(b);
                if (intersection is null || intersection.IsNull || intersection.STIsEmpty().IsTrue)
                    return false;

                return intersection.STArea().Value > MinOverlapArea;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static SqlGeometry EnsureValid(SqlGeometry geometry)
        {
            if (geometry is null || geometry.IsNull)
                return geometry;
            if (geometry.STIsValid().IsTrue)
                return geometry;
            return geometry.MakeValid();
        }

        /// <summary>
        /// Flattens MultiPolygon / GeometryCollection and picks the keep-point part, else the largest.
        /// </summary>
        internal static Polygon? TrySelectPolygonPart(SqlGeometry geometry, Vector2? keepPoint)
        {
            List<(Polygon Shape, double Area)> parts = [];
            foreach (SqlGeometry part in EnumeratePolygons(geometry))
            {
                try
                {
                    Polygon shape = part.ToPolygon();
                    double area = part.STArea().Value;
                    if (area <= MinOverlapArea)
                        continue;
                    parts.Add((shape, area));
                }
                catch (ArgumentException)
                {
                }
            }

            if (parts.Count == 0)
                return null;
            if (parts.Count == 1)
                return parts[0].Shape;

            if (keepPoint.HasValue)
            {
                foreach ((Polygon shape, double _) in parts)
                {
                    if (shape.Covers(keepPoint.Value))
                        return shape;
                }
            }

            return parts.OrderByDescending(part => part.Area).First().Shape;
        }

        private static IEnumerable<SqlGeometry> EnumeratePolygons(SqlGeometry geometry)
        {
            if (geometry is null || geometry.IsNull || geometry.STIsEmpty().IsTrue)
                yield break;

            string type = geometry.STGeometryType().Value;
            if (type.Equals("Polygon", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("CurvePolygon", StringComparison.OrdinalIgnoreCase))
            {
                yield return geometry;
                yield break;
            }

            if (!type.Equals("MultiPolygon", StringComparison.OrdinalIgnoreCase) &&
                !type.Equals("GeometryCollection", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            int count = geometry.STNumGeometries().Value;
            for (int i = 0; i < count; i++)
            {
                foreach (SqlGeometry child in EnumeratePolygons(geometry.GetGeometry(i)))
                    yield return child;
            }
        }
    }

    /// <summary>
    /// One published overlay (single location or already-grouped siblings) for overlap search.
    /// </summary>
    internal readonly struct OverlapCandidate
    {
        public OverlapCandidate(
            IReadOnlyList<long> locationIds,
            long? parentId,
            int sectionNumber,
            Polygon polygon)
        {
            LocationIds = locationIds ?? [];
            ParentID = parentId;
            SectionNumber = sectionNumber;
            Polygon = polygon;
        }

        public IReadOnlyList<long> LocationIds { get; }
        public long? ParentID { get; }
        public int SectionNumber { get; }
        public Polygon Polygon { get; }
    }
}
