using Annotation.Interfaces;
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

        public static Microsoft.SqlServer.Types.SqlGeometry GetShape(this LocationType shapeType, GridVector2[] points, ICollection<GridVector2[]> innerRingPoints = null)
        {
            Microsoft.SqlServer.Types.SqlGeometry shape = null;

            switch (shapeType)
            {
                case LocationType.POINT:
                    return points[0].ToSqlGeometry();
                case LocationType.CIRCLE:
                    return points.ToCircle();
                case LocationType.OPENCURVE:
                case LocationType.POLYLINE:
                case LocationType.CLOSEDCURVE:
                    return points.ToSqlGeometry();
                case LocationType.POLYGON:
                case LocationType.CURVEPOLYGON:
                    return points.ToPolygon(innerRingPoints);
                default:
                    throw new ArgumentException("Unexpected location type " + shapeType.ToString());
            }
        }

        public static Microsoft.SqlServer.Types.SqlGeometry GetSmoothedShape(this LocationType shapeType, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            GridVector2[] points = shape.ToPoints();

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
                    List<GridVector2[]> curved_innerRingPoints = InnerRingPointsToCurvedRingPoints(shape.InteriorRingPoints());
                    GridVector2[] curved_outerRing = points.CalculateCurvePoints(ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints, true).ToArray();
                    return curved_outerRing.ToPolygon(curved_innerRingPoints);
                default:
                    throw new ArgumentException("Unexpected location type " + shapeType.ToString());
            }
        }

        public static Microsoft.SqlServer.Types.SqlGeometry GetSmoothedShape(this LocationType shapeType, GridVector2[] points, ICollection<GridVector2[]> innerRingPoints = null)
        {
            Microsoft.SqlServer.Types.SqlGeometry shape = null;

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
                    ICollection<GridVector2[]> curved_innerRingPoints = InnerRingPointsToCurvedRingPoints(innerRingPoints);
                    GridVector2[] curved_outerRing = points.CalculateCurvePoints(ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints, true).ToArray();
                    return curved_outerRing.ToPolygon(curved_innerRingPoints);
                default:
                    throw new ArgumentException("Unexpected location type " + shapeType.ToString());
            }
        }

        private static List<GridVector2[]> InnerRingPointsToCurvedRingPoints(ICollection<GridVector2[]> innerRingPoints)
        {
            if (innerRingPoints == null)
                return null;

            List<GridVector2[]> curved_innerRingPoints = new List<GridVector2[]>(innerRingPoints.Count);
            foreach (GridVector2[] ringPoints in innerRingPoints)
            {
                curved_innerRingPoints.Add(ringPoints.CalculateCurvePoints(ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints, true).ToArray());
            }

            return curved_innerRingPoints;
        }
    }
}
