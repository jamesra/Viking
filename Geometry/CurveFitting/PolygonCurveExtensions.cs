using System;
using System.Collections.Generic;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// Annotation-oriented curve smoothing and control-point reduction. Lives on the Geometry facade, not Geometry.Core.
    /// </summary>
    public static class PolygonCurveExtensions
    {
        /// <summary>
        /// Catmull-Rom smooths the exterior ring and every interior ring.
        /// Control points closer together than the vector equality tolerance (pen jitter at the end of a
        /// stroke, or a loop that nearly retraces itself) can produce interpolated points that compare equal,
        /// which the <see cref="Polygon"/> constructor rejects. Those repeats are dropped from the smoothed ring.
        /// When the result is still not a valid ring the unsmoothed polygon is returned, so a display-only
        /// smoothing step never throws. Called while building pen action previews on the UI thread.
        /// </summary>
        /// <param name="minPointSpacing">
        /// The maximum error, in the polygon's own units (pixels), that the caller accepts. Control points
        /// closer than this to the previously kept point are dropped before the curve is fitted, so no two
        /// control points are nearer than the error the shape is allowed to have. Zero keeps every point.
        /// A ring that would be left with fewer than three points is smoothed unchanged.
        /// </param>
        public static Polygon Smooth(this Polygon poly, uint numInterpolationPoints, double minPointSpacing = 0)
        {
            Vector2[] controlPoints = minPointSpacing > 0
                ? EnforceMinimumSpacing(poly.ExteriorRing, minPointSpacing)
                : poly.ExteriorRing;
            Vector2[] smoothedCurve = controlPoints.CalculateCurvePoints(numInterpolationPoints, true);
            Polygon smoothedPoly;
            try
            {
                smoothedPoly = new(smoothedCurve);
            }
            catch (ArgumentException)
            {
                try
                {
                    smoothedPoly = new(RemoveRepeatedRingPoints(smoothedCurve));
                }
                catch (ArgumentException)
                {
                    return poly;
                }
            }

            foreach (Polygon innerPoly in poly.InteriorPolygons)
                smoothedPoly.AddInteriorRing(innerPoly.Smooth(numInterpolationPoints, minPointSpacing));

            return smoothedPoly;
        }

        /// <summary>
        /// Walks a closed ring keeping a point only when it is at least <paramref name="minSpacing"/> from the
        /// last kept point, and from the first point for the final ones, then closes the ring again.
        /// Returns <paramref name="closedRing"/> when fewer than three points would remain.
        /// </summary>
        private static Vector2[] EnforceMinimumSpacing(Vector2[] closedRing, double minSpacing)
        {
            List<Vector2> kept = [closedRing[0]];
            for (int i = 1; i < closedRing.Length - 1; i++)
            {
                if (Vector2.Distance(kept[kept.Count - 1], closedRing[i]) >= minSpacing)
                    kept.Add(closedRing[i]);
            }

            while (kept.Count > 3 && Vector2.Distance(kept[kept.Count - 1], kept[0]) < minSpacing)
                kept.RemoveAt(kept.Count - 1);

            if (kept.Count < 3)
                return closedRing;

            kept.Add(kept[0]);
            return [.. kept];
        }

        /// <summary>
        /// Keeps the first occurrence of each point of a closed ring and closes the ring again.
        /// </summary>
        private static Vector2[] RemoveRepeatedRingPoints(Vector2[] closedRing)
        {
            Vector2[] distinct = closedRing.Take(closedRing.Length - 1).ToArray().RemoveDuplicates();
            return [.. distinct, distinct[0]];
        }

        /// <summary>
        /// Drops exterior vertices that sit within <paramref name="tolerance"/> of the chord between neighbors.
        /// Surviving vertices keep their original coordinates. Interior rings are copied.
        /// A pen cut walks the smoothed display ring, which inserts several samples between each control point.
        /// This removes those samples before the ring is saved, without a Catmull-Rom refit of the far side.
        /// Returns <paramref name="polygon"/> when the shorter ring is not a valid polygon.
        /// </summary>
        public static Polygon ReduceExteriorRing(this Polygon polygon, double tolerance)
        {
            if (polygon is null)
                throw new ArgumentNullException(nameof(polygon));
            if (tolerance <= 0 || polygon.ExteriorRing.Length < 5)
                return polygon;

            List<Vector2> reduced = polygon.ExteriorRing.DouglasPeuckerReduction(tolerance);
            if (reduced.Count < 3 || reduced.Count >= polygon.ExteriorRing.Length)
                return polygon;

            try
            {
                Vector2[] ring = reduced.ToArray().EnsureClosedRing();
                Polygon output = new(ring);
                foreach (Polygon inner in polygon.InteriorPolygons)
                    output.AddInteriorRing(inner);

                return output.IsValid() ? output : polygon;
            }
            catch (ArgumentException)
            {
                return polygon;
            }
        }

        /// <summary>Returns an approximately equal polygon with fewer control points.</summary>
        public static Polygon SimplifyControlPoints(this Polygon poly, double maxDistanceFromSimplifiedToIdeal = 1.0)
        {
            Vector2[] simplerRing = [.. CatmullRomControlPointSimplification.IdentifyControlPoints(poly.ExteriorRing, maxDistanceFromSimplifiedToIdeal, true)];
            Polygon output = new(simplerRing);

            foreach (var innerRing in poly.InteriorRings)
            {
                var simplerInnerRing = CatmullRomControlPointSimplification.IdentifyControlPoints(innerRing, maxDistanceFromSimplifiedToIdeal, true);
                output.AddInteriorRing(simplerInnerRing);
            }

            return output;
        }

        public static Polyline Smooth(this Polyline polyline, uint numInterpolations) =>
            polyline.CalculateCurvePoints(numInterpolations);

        /// <summary>
        /// Predicts a vertex from the two vertices before and after it using a Catmull–Rom fit.
        /// </summary>
        public static Vector2 PredictPoint(this PolygonIndex cIndex, Polygon[] polygons)
        {
            var p1 = cIndex.Previous.Previous.Point(polygons);
            var p2 = cIndex.Previous.Point(polygons);
            var p3 = cIndex.Next.Point(polygons);
            var p4 = cIndex.Next.Next.Point(polygons);
            Vector2 vPos = cIndex.Point(polygons);

            var newPositions = CatmullRom.FitCurveSegment(p1, p2, p3, p4,
                [Vector2.Distance(vPos, p2) / (Vector2.Distance(p2, vPos) + Vector2.Distance(vPos, p3))]);

            return newPositions[0];
        }
    }
}
