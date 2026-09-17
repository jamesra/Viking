using Viking.AnnotationServiceTypes.Interfaces;
using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Viking.VolumeModel
{

    public static class ShapeSmoothingExtensions
    {
        public static uint NumOpenCurveInterpolationPoints = 3;
        public static uint NumClosedCurveInterpolationPoints = 10;

        public static Microsoft.SqlServer.Types.SqlGeometry GetShape(this LocationType shapeType, Vector2[] points, ICollection<Vector2[]> innerRingPoints = null)
        {
            return shapeType switch
            {
                LocationType.POINT => points[0].ToSqlGeometry(),
                LocationType.CIRCLE => points.ToCircle(),
                LocationType.OPENCURVE or LocationType.POLYLINE or LocationType.CLOSEDCURVE => points.ToSqlGeometry(),
                LocationType.POLYGON or LocationType.CURVEPOLYGON => points.ToPolygon(innerRingPoints),
                _ => throw new ArgumentException("Unexpected location type " + shapeType.ToString()),
            };
        }

        public static Microsoft.SqlServer.Types.SqlGeometry GetSmoothedShape(this LocationType shapeType, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            Vector2[] points = shape.ToPoints();

            switch (shapeType)
            {
                case LocationType.POINT:
                    return points[0].ToSqlGeometry();
                case LocationType.CIRCLE:
                    return points.ToCircle();
                case LocationType.OPENCURVE:
                    return points.CalculateCurvePoints(ShapeSmoothingExtensions.NumOpenCurveInterpolationPoints, false).ToArray().ToSqlGeometry();
                case LocationType.POLYLINE:
                    return points.ToSqlGeometry();
                case LocationType.POLYGON:
                    return points.ToPolygon(shape.InteriorRingPoints());
                case LocationType.CLOSEDCURVE:
                    return points.CalculateCurvePoints(ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints, true).ToArray().ToSqlGeometry();
                case LocationType.CURVEPOLYGON:
                    List<Vector2[]> curved_innerRingPoints = InnerRingPointsToCurvedRingPoints(shape.InteriorRingPoints());
                    Vector2[] curved_outerRing = [.. points.CalculateCurvePoints(ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints, true)];
                    return curved_outerRing.ToPolygon(curved_innerRingPoints);
                default:
                    throw new ArgumentException("Unexpected location type " + shapeType.ToString());
            }
        }

        public static Microsoft.SqlServer.Types.SqlGeometry GetSmoothedShape(this LocationType shapeType, Vector2[] points, ICollection<Vector2[]> innerRingPoints = null)
        {
            switch (shapeType)
            {
                case LocationType.POINT:
                    return points[0].ToSqlGeometry();
                case LocationType.CIRCLE:
                    return points.ToCircle();
                case LocationType.OPENCURVE:
                    return points.CalculateCurvePoints(ShapeSmoothingExtensions.NumOpenCurveInterpolationPoints, false).ToArray().ToSqlGeometry();
                case LocationType.CLOSEDCURVE:
                    return points.CalculateCurvePoints(ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints, true).ToArray().ToSqlGeometry();
                case LocationType.POLYLINE:
                    return points.ToSqlGeometry();
                case LocationType.POLYGON:
                    return points.ToPolygon(innerRingPoints);
                case LocationType.CURVEPOLYGON:
                    ICollection<Vector2[]> curved_innerRingPoints = InnerRingPointsToCurvedRingPoints(innerRingPoints);
                    Vector2[] curved_outerRing = [.. points.CalculateCurvePoints(ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints, true)];
                    return curved_outerRing.ToPolygon(curved_innerRingPoints);
                default:
                    throw new ArgumentException("Unexpected location type " + shapeType.ToString());
            }
        }

        private static List<Vector2[]> InnerRingPointsToCurvedRingPoints(ICollection<Vector2[]> innerRingPoints)
        {
            if (innerRingPoints is null)
                return null;

            List<Vector2[]> curved_innerRingPoints = new(innerRingPoints.Count);
            foreach (Vector2[] ringPoints in innerRingPoints)
            {
                curved_innerRingPoints.Add([.. ringPoints.CalculateCurvePoints(ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints, true)]);
            }

            return curved_innerRingPoints;
        }
    }
}
