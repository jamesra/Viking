using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using Viking.VolumeModel;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// SAM2 foreground clicks for a polygon. Resegment and linked-polygon placement both call
    /// <see cref="CreateMosaicForegroundPoints"/> so they send the same points: centroids of the
    /// unsmoothed control-point ring, thinned so a large triangle drops nearby smaller ones.
    /// Pass <c>MosaicShape</c> or a translated copy of it. <c>VolumeShape</c> on a CURVEPOLYGON
    /// is the smoothed curve and multiplies the mesh.
    /// </summary>
    internal static class PolygonSegmentationPrompts
    {
        /// <summary>
        /// Mosaic-space clicks. Called while a linked polygon is previewed and again from the
        /// transformed ring on release, and by Resegment before mapping into volume.
        /// </summary>
        public static IReadOnlyList<Vector2> CreateMosaicForegroundPoints(Polygon controlPointRing)
            => DecimateCentroids(AnnotationPointExtensions.GetPolygonTriangleCentroidSamples(controlPointRing));

        /// <summary>
        /// Same clicks as <see cref="CreateMosaicForegroundPoints"/>, mapped with
        /// <see cref="CircleSegmentationPrompts.ToVolumePoints"/>. Unmapped points are dropped.
        /// </summary>
        public static IReadOnlyList<Vector2> CreateVolumeForegroundPoints(
            Polygon controlPointRing,
            IVolumeToSectionTransform transform)
            => CircleSegmentationPrompts.ToVolumePoints(CreateMosaicForegroundPoints(controlPointRing), transform);

        /// <summary>
        /// Keeps large-triangle centroids and drops strictly smaller ones inside a radius whose
        /// circle has half the largest triangle's area. Equal areas both stay. A centroid already
        /// removed does not remove anyone else. Survivors are returned largest area first.
        /// One sample, including the mesh-failure centroid, is returned unchanged.
        /// </summary>
        internal static IReadOnlyList<Vector2> DecimateCentroids(IReadOnlyList<PolygonTriangleCentroid> centroids)
        {
            if (centroids is null || centroids.Count == 0)
                return [];

            if (centroids.Count == 1)
                return [centroids[0].Point];

            List<PolygonTriangleCentroid> ordered = [.. centroids.OrderByDescending(sample => sample.Area)];
            double largestArea = ordered[0].Area;
            double removalRadius = largestArea <= 0 ? 0 : Math.Sqrt(largestArea / (2 * Math.PI));
            double radiusSquared = removalRadius * removalRadius;
            bool[] removed = new bool[ordered.Count];
            List<Vector2> kept = [];

            for (int i = 0; i < ordered.Count; i++)
            {
                if (removed[i])
                    continue;

                kept.Add(ordered[i].Point);
                for (int j = i + 1; j < ordered.Count; j++)
                {
                    if (removed[j] || ordered[j].Area >= ordered[i].Area)
                        continue;

                    if (Vector2.DistanceSquared(ordered[i].Point, ordered[j].Point) < radiusSquared)
                        removed[j] = true;
                }
            }

            return kept;
        }
    }
}
