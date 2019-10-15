using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using SqlGeometryUtils;

namespace Viking.VolumeModel
{

    public static class ShapeSmoothingExtensions
    {
        public static uint NumOpenCurveInterpolationPoints = 3;
        public static uint NumClosedCurveInterpolationPoints = 10;

        public static Microsoft.SqlServer.Types.SqlGeometry GetShape(this WebAnnotationModel.LocationType shapeType, GridVector2[] points, ICollection<GridVector2[]> innerRingPoints = null)
        {
            Microsoft.SqlServer.Types.SqlGeometry shape = null;

            switch (shapeType)
            {
                case WebAnnotationModel.LocationType.POINT:
                    return points[0].ToSqlGeometry();
                case WebAnnotationModel.LocationType.CIRCLE:
                    return points.ToCircle();
                case WebAnnotationModel.LocationType.OPENCURVE:
                case WebAnnotationModel.LocationType.POLYLINE:
                case WebAnnotationModel.LocationType.CLOSEDCURVE:
                    return points.ToSqlGeometry();
                case WebAnnotationModel.LocationType.POLYGON:
                case WebAnnotationModel.LocationType.CURVEPOLYGON:
                    return points.ToPolygon(innerRingPoints);
                default:
                    throw new ArgumentException("Unexpected location type " + shapeType.ToString());
            }
        }

        public static Microsoft.SqlServer.Types.SqlGeometry GetSmoothedShape(this WebAnnotationModel.LocationType shapeType, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            GridVector2[] points = shape.ToPoints();

            switch (shapeType)
            {
                case WebAnnotationModel.LocationType.POINT:
                    return points[0].ToSqlGeometry();
                case WebAnnotationModel.LocationType.CIRCLE:
                    return points.ToCircle();
                case WebAnnotationModel.LocationType.OPENCURVE:
                    return points.CalculateCurvePoints(ShapeSmoothingExtensions.NumOpenCurveInterpolationPoints, false).ToArray().ToSqlGeometry();
                case WebAnnotationModel.LocationType.POLYLINE:
                    return points.ToSqlGeometry();
                case WebAnnotationModel.LocationType.POLYGON:
                    return points.ToPolygon(shape.InteriorRingPoints());
                case WebAnnotationModel.LocationType.CLOSEDCURVE:
                    return points.CalculateCurvePoints(ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints, true).ToArray().ToSqlGeometry();
                case WebAnnotationModel.LocationType.CURVEPOLYGON:
                    List<GridVector2[]> curved_innerRingPoints = InnerRingPointsToCurvedRingPoints(shape.InteriorRingPoints());
                    GridVector2[] curved_outerRing = points.CalculateCurvePoints(ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints, true).ToArray();
                    return curved_outerRing.ToPolygon(curved_innerRingPoints);
                default:
                    throw new ArgumentException("Unexpected location type " + shapeType.ToString());
            }
        }

        public static Microsoft.SqlServer.Types.SqlGeometry GetSmoothedShape(this WebAnnotationModel.LocationType shapeType, GridVector2[] points, ICollection<GridVector2[]> innerRingPoints = null)
        {
            Microsoft.SqlServer.Types.SqlGeometry shape = null;

            switch (shapeType)
            {
                case WebAnnotationModel.LocationType.POINT:
                    return points[0].ToSqlGeometry();
                case WebAnnotationModel.LocationType.CIRCLE:
                    return points.ToCircle();
                case WebAnnotationModel.LocationType.OPENCURVE:
                    return points.CalculateCurvePoints(ShapeSmoothingExtensions.NumOpenCurveInterpolationPoints, false).ToArray().ToSqlGeometry();
                case WebAnnotationModel.LocationType.CLOSEDCURVE:
                    return points.CalculateCurvePoints(ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints, true).ToArray().ToSqlGeometry();
                case WebAnnotationModel.LocationType.POLYLINE:
                    return points.ToSqlGeometry();
                case WebAnnotationModel.LocationType.POLYGON:
                    return points.ToPolygon(innerRingPoints);
                case WebAnnotationModel.LocationType.CURVEPOLYGON:
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
