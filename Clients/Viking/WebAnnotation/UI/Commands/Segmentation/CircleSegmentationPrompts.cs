using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using Viking.VolumeModel;
using WebAnnotationModel;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// SAM2 prompt points for a circle: center plus two concentric rings, plus nearby annotations as background.
    /// Mosaic points are transformed to volume before upload.
    /// </summary>
    internal static class CircleSegmentationPrompts
    {
        /// <summary>
        /// Center, half-radius ring, and 3/4-radius ring in mosaic space.
        /// </summary>
        public static IReadOnlyList<GridVector2> CreateMosaicForegroundPoints(GridCircle mosaicCircle)
        {
            List<GridVector2> foregroundPoints = [mosaicCircle.Center];

            AddRing(foregroundPoints, mosaicCircle.Center, mosaicCircle.Radius / 2.0);
            AddRing(foregroundPoints, mosaicCircle.Center, 3.0 * mosaicCircle.Radius / 4.0);

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
        /// Representative points of other annotations, mapped to volume as SAM2 background prompts.
        /// </summary>
        public static IReadOnlyList<GridVector2> CreateBackgroundVolumePoints(
            IEnumerable<LocationObj> otherAnnotations,
            IVolumeToSectionTransform transform)
        {
            IReadOnlyList<GridVector2> mosaicPoints = AnnotationPointExtensions.GetAnnotationRepresentativePoints(otherAnnotations);
            return ToVolumePoints(mosaicPoints, transform);
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

        private static void AddRing(List<GridVector2> points, GridVector2 center, double radius)
        {
            for (int i = 0; i < 8; i++)
            {
                double angle = (2.0 * Math.PI * i) / 8.0;
                points.Add(new GridVector2(
                    center.X + radius * Math.Cos(angle),
                    center.Y + radius * Math.Sin(angle)));
            }
        }
    }
}
