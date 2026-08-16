using System;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// Annotation-oriented curve smoothing and control-point reduction. Lives on the Geometry facade, not Geometry.Core.
    /// </summary>
    public static class PolygonCurveExtensions
    {
        public static Polygon Smooth(this Polygon poly, uint numInterpolationPoints)
        {
            Vector2[] smoothedCurve = poly.ExteriorRing.CalculateCurvePoints(numInterpolationPoints, true);
            Polygon smoothedPoly = new(smoothedCurve);

            foreach (Polygon innerPoly in poly.InteriorPolygons)
                smoothedPoly.AddInteriorRing(innerPoly.Smooth(numInterpolationPoints));

            return smoothedPoly;
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
