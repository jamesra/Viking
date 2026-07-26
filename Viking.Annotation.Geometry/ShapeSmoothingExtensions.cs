using Viking.AnnotationServiceTypes.Interfaces;
using Geometry; 
using System;
using System.Collections.Generic;
using System.Linq;

namespace Viking.Annotation.Geometry
{

    public static class ShapeSmoothingExtensions
    {
        public static uint NumOpenCurveInterpolationPoints = 3;
        public static uint NumClosedCurveInterpolationPoints = 10;

        public static IShape2D GetShape(this LocationType shapeType, IPoint2D[] points, ICollection<IPoint2D[]> innerRingPoints = null)
        {  
            switch (shapeType)
            {
                case LocationType.POINT:
                    return points[0];
                case LocationType.CIRCLE:
                    return points.ToCircle();
                case LocationType.OPENCURVE:
                case LocationType.POLYLINE:
                    return new GridPolyline(points);
                case LocationType.CLOSEDCURVE:
                case LocationType.POLYGON:
                case LocationType.CURVEPOLYGON:
                    return new GridPolygon(points, innerRingPoints);
                default:
                    throw new ArgumentException("Unexpected location type " + shapeType.ToString());
            }
        }

        public static IShape2D GetShape(this LocationType shapeType, GridVector2[] points, ICollection<GridVector2[]> innerRingPoints = null)
        {  
            switch (shapeType)
            {
                case LocationType.POINT:
                    return points[0];
                case LocationType.CIRCLE:
                    return points.ToCircle();
                case LocationType.OPENCURVE:
                case LocationType.POLYLINE:
                    return new GridPolyline(points);
                case LocationType.CLOSEDCURVE:
                case LocationType.POLYGON:
                case LocationType.CURVEPOLYGON:
                    return new GridPolygon(points, innerRingPoints);
                default:
                    throw new ArgumentException("Unexpected location type " + shapeType.ToString());
            }
        }

        /// <summary>
        /// Smooth the shape, but only if a smoothed LocationType is requested
        /// </summary>
        /// <param name="shapeType"></param>
        /// <param name="shape"></param>
        /// <returns></returns>
        public static IShape2D GetSmoothedShape(this LocationType shapeType, IShape2D shape)
        {
            switch (shapeType)
            {
                case LocationType.POINT:
                case LocationType.CIRCLE:
                case LocationType.POLYLINE:
                case LocationType.POLYGON:
                    return shape;
                case LocationType.OPENCURVE:
                    if (shape is IPolyLine2D curve)
                    {
                        return new GridPolyline(curve.Points.ToGridVector2().CalculateCurvePoints(ShapeSmoothingExtensions.NumOpenCurveInterpolationPoints, false).ToArray());
                    }

                    throw new NotImplementedException();
                case LocationType.CLOSEDCURVE:
                    if (shape is IPolygon2D closedCurve)
                    {
                        return new GridPolygon(closedCurve.ExteriorRing.ToGridVector2()
                            .CalculateCurvePoints(ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints, true));
                    }
                    throw new NotImplementedException();
                case LocationType.CURVEPOLYGON:
                    if (shape is IPolygon2D poly)
                    {
                        List<GridVector2[]> curved_innerRingPoints =
                            InnerRingPointsToCurvedRingPoints(poly.InteriorRings);
                        GridVector2[] curved_outerRing = poly.ExteriorRing.ToGridVector2()
                            .CalculateCurvePoints(ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints, true)
                            .ToArray();
                        return new GridPolygon(curved_outerRing, curved_innerRingPoints);
                    }
                    throw new NotImplementedException();
                default:
                    throw new ArgumentException("Unexpected location type " + shapeType.ToString());
            }
        }

        public static IShape2D GetSmoothedShape(this LocationType shapeType, IPoint2D[] points,
            ICollection<IPoint2D[]> innerRingPoints = null)
        {
            return shapeType.GetSmoothedShape(points.ToGridVector2(), innerRingPoints?.Select(p => p.ToGridVector2()).ToArray());
        }

        /// <summary>
        /// Build the requested shape type from the points provided
        /// </summary>
        /// <param name="shapeType"></param>
        /// <param name="points"></param>
        /// <param name="innerRingPoints"></param>
        /// <returns></returns>
        public static IShape2D GetSmoothedShape(this LocationType shapeType, GridVector2[] points, ICollection<GridVector2[]> innerRingPoints = null)
        {
            if (points is null) throw new ArgumentNullException(nameof(points));
            if (points.Length == 0) throw new ArgumentException($"{nameof(points)} requires at least one point");

            switch (shapeType)
            {
                case LocationType.POINT:
                    return points[0];
                case LocationType.CIRCLE:
                    return points.ToCircle();
                case LocationType.OPENCURVE:
                    return new GridPolyline(points.CalculateCurvePoints(ShapeSmoothingExtensions.NumOpenCurveInterpolationPoints, false));
                case LocationType.CLOSEDCURVE:
                    return new GridPolygon(points.CalculateCurvePoints(ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints,
                        true));
                case LocationType.POLYLINE:
                    return new GridPolyline(points);
                case LocationType.POLYGON:
                    return new GridPolygon(points, innerRingPoints);
                case LocationType.CURVEPOLYGON:
                    ICollection<GridVector2[]> curved_innerRingPoints = InnerRingPointsToCurvedRingPoints(innerRingPoints);
                    GridVector2[] curved_outerRing = points.CalculateCurvePoints(ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints, true);
                    return new GridPolygon(curved_outerRing, curved_innerRingPoints);
                default:
                    throw new ArgumentException("Unexpected location type " + shapeType.ToString());
            }
        }

        private static List<GridVector2[]> InnerRingPointsToCurvedRingPoints(ICollection<GridVector2[]> innerRingPoints)
        {
            if (innerRingPoints == null)
                return null;

            List<GridVector2[]> curved_innerRingPoints = new List<GridVector2[]>(innerRingPoints.Count);
            foreach (var ringPoints in innerRingPoints)
            {
                curved_innerRingPoints.Add(ringPoints.CalculateCurvePoints(ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints, true).ToArray());
            }

            return curved_innerRingPoints;
        }

        private static List<GridVector2[]> InnerRingPointsToCurvedRingPoints(IReadOnlyCollection<IPoint2D[]> innerRingPoints)
        {
            if (innerRingPoints == null)
                return null;

            List<GridVector2[]> curved_innerRingPoints = new List<GridVector2[]>(innerRingPoints.Count);
            foreach (var ringPoints in innerRingPoints)
            {
                curved_innerRingPoints.Add(ringPoints.CalculateCurvePoints(ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints, true).ToArray());
            }

            return curved_innerRingPoints;
        }
    }
}
