using System;
using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// Carries a circle between mosaic and volume space. A section-to-volume transform scales, rotates and shears, so a circle
    /// is not a circle afterwards and its radius changes by the local scale. VikingAU's rule, kept here for every caller:
    /// map five points (the centre and the east, north, west and south rim points), estimate the scale from each rim point that
    /// could be mapped, and average those scales to get the radius in the other space. See <c>.cursor/rules/circle-mapping-scale.mdc</c>.
    /// </summary>
    public static class CircleMapping
    {
        /// <summary>Number of points mapped: the centre, then east, north, west, south.</summary>
        public const int PointCount = 5;

        /// <summary>The centre followed by the east, north, west and south rim points, in the order <see cref="TryCircleFromMappedPoints"/> expects.</summary>
        public static Vector2[] SamplePoints(Vector2 center, double radius) =>
        [
            center,
            new Vector2(center.X + radius, center.Y),
            new Vector2(center.X, center.Y + radius),
            new Vector2(center.X - radius, center.Y),
            new Vector2(center.X, center.Y - radius),
        ];

        /// <summary>
        /// The circle in the other space from the mapped <see cref="SamplePoints"/>. The centre must have mapped. The radius is
        /// <paramref name="sourceRadius"/> times the mean of |mapped rim - mapped centre| / <paramref name="sourceRadius"/> over the rim
        /// points that mapped (not a median, a bounding box, or the source radius unchanged).
        /// </summary>
        /// <param name="sourceRadius">Radius of the circle before mapping, in its own space.</param>
        /// <param name="mapped">The five mapped points, same order as <see cref="SamplePoints"/>. Entries that failed to map are ignored.</param>
        /// <param name="mappedOk">Whether each of the five points mapped.</param>
        /// <param name="circle">The mapped circle, or default when this returns false.</param>
        /// <returns>False when the source radius is not positive, the centre did not map, or no rim point mapped.</returns>
        public static bool TryCircleFromMappedPoints(double sourceRadius, IReadOnlyList<Vector2> mapped, IReadOnlyList<bool> mappedOk, out Circle circle)
        {
            circle = default;
            if (mapped is null || mappedOk is null || mapped.Count != PointCount || mappedOk.Count != PointCount)
                throw new ArgumentException($"Exactly {PointCount} mapped points and flags are required");

            if (!(sourceRadius > 0) || double.IsInfinity(sourceRadius) || !mappedOk[0])
                return false;

            Vector2 center = mapped[0];
            double scaleSum = 0;
            int rimCount = 0;
            for (int i = 1; i < PointCount; i++)
            {
                if (!mappedOk[i])
                    continue;

                scaleSum += Vector2.Distance(center, mapped[i]) / sourceRadius;
                rimCount++;
            }

            if (rimCount == 0)
                return false;

            double radius = sourceRadius * (scaleSum / rimCount);
            if (!(radius > 0) || double.IsInfinity(radius))
                return false;

            circle = new Circle(center, radius);
            return true;
        }
    }
}
