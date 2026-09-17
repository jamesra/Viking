using Geometry;
using System;
using System.Collections.Generic;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>
    /// Eligibility, hit-test, and line-width helpers for auto-polygonize proposals.
    /// Circles must sit inside an inset of the viewport and meet the min on-screen area.
    /// </summary>
    internal static class AutoPolygonizeSelection
    {
        public const double EdgeMarginFraction = 0.10;
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
        /// Shrinks the viewport so circles near the edge are not auto-segmented.
        /// </summary>
        public static GridRectangle InsetBounds(GridRectangle viewBounds, double marginFraction = EdgeMarginFraction)
        {
            double marginX = viewBounds.Width * marginFraction;
            double marginY = viewBounds.Height * marginFraction;
            return new GridRectangle(
                viewBounds.Left + marginX,
                viewBounds.Right - marginX,
                viewBounds.Bottom + marginY,
                viewBounds.Top - marginY);
        }

        /// <summary>
        /// True for a circle whose volume center is inside the inset bounds.
        /// </summary>
        public static bool IsEligibleCircle(LocationType typeCode, GridVector2 volumeCenter, GridRectangle inset)
        {
            return typeCode == LocationType.CIRCLE && inset.Contains(volumeCenter);
        }

        /// <summary>
        /// Eligible when the center is inset and the on-screen area meets <paramref name="minScreenAreaPercent"/>.
        /// </summary>
        public static bool IsEligibleCircle(
            LocationType typeCode,
            GridVector2 volumeCenter,
            GridRectangle inset,
            double mosaicRadius,
            double screenPixelSizeInVolume,
            double viewportWidth,
            double viewportHeight,
            double minScreenAreaPercent)
        {
            return IsEligibleCircle(typeCode, volumeCenter, inset) &&
                   MeetsMinScreenArea(mosaicRadius, screenPixelSizeInVolume, viewportWidth, viewportHeight, minScreenAreaPercent);
        }

        /// <summary>
        /// True when the circle's on-screen area is at least <paramref name="minScreenAreaPercent"/> of the viewport.
        /// 0% accepts any positive radius.
        /// </summary>
        public static bool MeetsMinScreenArea(
            double mosaicRadius,
            double screenPixelSizeInVolume,
            double viewportWidth,
            double viewportHeight,
            double minScreenAreaPercent)
        {
            if (mosaicRadius <= 0)
                return false;
            if (minScreenAreaPercent <= 0)
                return true;
            if (screenPixelSizeInVolume <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
                return true;

            double screenRadius = mosaicRadius / screenPixelSizeInVolume;
            double screenArea = Math.PI * screenRadius * screenRadius;
            double viewportArea = viewportWidth * viewportHeight;
            return screenArea >= (minScreenAreaPercent / 100.0) * viewportArea;
        }

        /// <summary>
        /// Shortest distance to the exterior or any hole ring. Used for hollow-line hit testing.
        /// </summary>
        public static double DistanceToAnyRing(GridPolygon polygon, GridVector2 worldPosition)
        {
            double distance = polygon.Distance(worldPosition);
            if (polygon.InteriorPolygons is null)
                return distance;

            foreach (GridPolygon hole in polygon.InteriorPolygons)
            {
                double holeDistance = hole.Distance(worldPosition);
                if (holeDistance < distance)
                    distance = holeDistance;
            }

            return distance;
        }

        /// <summary>
        /// World-space Douglas-Peucker after polygonize. Tolerance is in world units
        /// (typically <c>PenSimplifyThreshold * downsample</c>, i.e. screen pixels).
        /// </summary>
        public static GridPolygon SimplifyProposal(GridPolygon polygon, double tolerance)
            => SegmentationMaskPolygonizer.SimplifyRings(polygon, tolerance);
    }

    /// <summary>
    /// Remembers which circles already have a proposal or were dismissed at a given LastModified.
    /// A hitch-view proposal must not be remembered, or the circle will never retry.
    /// </summary>
    internal sealed class AutoPolygonizeCache
    {
        private readonly Dictionary<long, (DateTime LastModified, LocationType TypeCode)> proposals = [];
        private readonly Dictionary<long, DateTime> dismissed = [];

        /// <summary>
        /// False when the circle is not a circle, was dismissed at this LastModified, or already has a matching proposal.
        /// </summary>
        public bool ShouldProcess(long locationId, DateTime lastModified, LocationType typeCode)
        {
            if (typeCode != LocationType.CIRCLE)
                return false;

            if (dismissed.TryGetValue(locationId, out DateTime dismissedAt) && dismissedAt == lastModified)
                return false;

            if (proposals.TryGetValue(locationId, out var existing) &&
                existing.LastModified == lastModified &&
                existing.TypeCode == typeCode)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Records a published proposal so the same LastModified is not segmented again.
        /// </summary>
        public void RememberProposal(long locationId, DateTime lastModified, LocationType typeCode)
        {
            dismissed.Remove(locationId);
            proposals[locationId] = (lastModified, typeCode);
        }

        /// <summary>
        /// Suppresses the circle until LastModified changes (user edit or server update).
        /// </summary>
        public void Dismiss(long locationId, DateTime lastModified)
        {
            proposals.Remove(locationId);
            dismissed[locationId] = lastModified;
        }

        public void Remove(long locationId)
        {
            proposals.Remove(locationId);
            dismissed.Remove(locationId);
        }

        public void Clear()
        {
            proposals.Clear();
            dismissed.Clear();
        }
    }
}
