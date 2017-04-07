using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using WebAnnotationModel.Service;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils; 



namespace WebAnnotationModel
{
    public static class Extensions
    {
        public static AnnotationService.Types.BoundingRectangle ToBoundingRectangle(this GridRectangle rect)
        {
            return new AnnotationService.Types.BoundingRectangle() { XMin = rect.Left, XMax = rect.Right, YMin = rect.Bottom, YMax = rect.Top };
        }

        public static GridRectangle ToGridRectangle(this AnnotationService.Types.BoundingRectangle bbox)
        {
            return new GridRectangle(bbox.XMin, bbox.XMax, bbox.YMin, bbox.YMax);
        }

        public static SqlGeometry ToGeometry(GridVector2[] points, double Z, double radius, LocationType type)
        {
            switch (type)
            {
                case LocationType.CIRCLE:
                    if (points.Length == 4)
                        return points.ToCurvePolygon();
                    else
                        return SqlGeometryUtils.Extensions.ToCircle(points[0].X, points[1].Y, Z, radius);
                case LocationType.OPENCURVE:
                    return points.ToPolyLine();
            }

            throw new ArgumentException(string.Format("Unknown location type {0}", type));
        }
    }
}
