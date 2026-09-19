using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotation.UI.Commands.Segmentation;
using WebAnnotationModel;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>
    /// Eligibility, hit-test, and line-width helpers for auto-polygonize proposals.
    /// Circles must sit inside a 5% inset, lie entirely on the visible view, and meet the min radius in nanometers.
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
        /// True when the disk of <paramref name="radius"/> around <paramref name="volumeCenter"/>
        /// lies entirely inside <paramref name="bounds"/>. Used so a clipped circle is not submitted.
        /// </summary>
        public static bool IsCircleEntirelyInside(GridVector2 volumeCenter, double radius, GridRectangle bounds)
        {
            if (radius < 0)
                return false;

            return bounds.Contains(new GridRectangle(volumeCenter, radius));
        }

        /// <summary>
        /// Eligible when the center is at least 5% from each view edge, the disk is fully on
        /// <paramref name="viewBounds"/>, and the radius is at least
        /// <paramref name="minRadiusNanometers"/>.
        /// </summary>
        public static bool IsEligibleCircle(
            LocationType typeCode,
            GridVector2 volumeCenter,
            GridRectangle inset,
            GridRectangle viewBounds,
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
            GridVector2 volumeCenter)
        {
            if (!context.IsUsable)
                return false;
            if (DownsampleChangedByFactorOfTwo(liveDownsample, context.Downsample))
                return false;
            return context.WorldBounds.Contains(volumeCenter);
        }

        /// <summary>
        /// Squared-distance compare so batch SegmentImage can run nearest-to-farthest from the view center.
        /// Negative when <paramref name="a"/> is closer to <paramref name="center"/>.
        /// </summary>
        public static int CompareDistanceFromCenter(GridVector2 a, GridVector2 b, GridVector2 center)
        {
            return GridVector2.DistanceSquared(a, center).CompareTo(GridVector2.DistanceSquared(b, center));
        }

        /// <summary>
        /// Stable nearest-first order around <paramref name="center"/>. Used by CollectEligibleCircles.
        /// Equal distances keep input order.
        /// </summary>
        public static List<T> OrderByDistanceFromCenter<T>(
            IEnumerable<T> items,
            Func<T, GridVector2> position,
            GridVector2 center)
        {
            return [.. items.OrderBy(item => GridVector2.DistanceSquared(position(item), center))];
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
    }
}
