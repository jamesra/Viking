using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using WebAnnotationModel.Service;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;



namespace WebAnnotationModel
{
    public static class GeometryExtensions
    {
        public static System.Data.SqlTypes.SqlString ToSqlString(this string str)
        {
            return new System.Data.SqlTypes.SqlString(str);
        }

        public static System.Data.SqlTypes.SqlChars ToSqlChars(this string str)
        {
            return new SqlChars(str.ToCharArray());
        }

        public static GridRectangle ToGridRectangle(this WebAnnotationModel.Service.BoundingRectangle bbox)
        {
            return new GridRectangle(bbox.XMin, bbox.XMax, bbox.YMin, bbox.YMax);
        }

        public static WebAnnotationModel.Service.BoundingRectangle ToBoundingRectangle(this GridRectangle rect)
        {
            return new BoundingRectangle() { XMin = rect.Left, XMax = rect.Right, YMin = rect.Bottom, YMax = rect.Top };
        }

        public static GridVector2 ToCentroid(this System.Data.Entity.Spatial.DbGeometry geometry)
        {
            System.Data.Entity.Spatial.DbGeometry centroid = geometry.Centroid;
            return new GridVector2(centroid.XCoordinate.Value, centroid.YCoordinate.Value);
        }

        public static Microsoft.SqlServer.Types.SqlGeometry ToSqlGeometry(this System.Data.Entity.Spatial.DbGeometry geometry)
        {
            return Microsoft.SqlServer.Types.SqlGeometry.STGeomFromWKB( new System.Data.SqlTypes.SqlBytes(geometry.AsBinary()), 0 );
        }

        public static System.Data.Entity.Spatial.DbGeometry ToDbGeometry(this Microsoft.SqlServer.Types.SqlGeometry geometry)
        {
            return System.Data.Entity.Spatial.DbGeometry.FromBinary( geometry.STAsBinary().Buffer);
        }

        public static SqlGeometry ToPolyLine(this GridVector2[] points)
        { 
            StringBuilder PolyStringBuilder = new StringBuilder();
            PolyStringBuilder.Append("LINESTRING(");
            for (int i = 0; i < points.Length; i++)
            {
                if (i != 0)
                    PolyStringBuilder.AppendFormat(",");

                PolyStringBuilder.AppendFormat("{0} {1}", points[i].X, points[i].Y);
            }
            PolyStringBuilder.Append(")");
            return SqlGeometry.STLineFromText(PolyStringBuilder.ToString().ToSqlChars(), 0);
        }

        public static SqlGeometry ToCurvePolygon(double X, double Y, double Z, double Radius)
        {
            if (Radius == 0)
                throw new ArgumentException("Cannot create circle with a radius of zero");

            string circle_template = "CURVEPOLYGON(CIRCULARSTRING ({1} {3} {6}, " +
                                                                  "{0} {5} {6}, " +
                                                                  "{2} {3} {6}, " +
                                                                  "{0} {4} {6}, " +
                                                                  "{1} {3} {6}))";
            string circle_shape_string = string.Format(circle_template, new object[] { X, X - Radius, X + Radius, Y, Y - Radius, Y + Radius, Z });
            return SqlGeometry.STGeomFromText(circle_shape_string.ToSqlChars(), 0);
        }


        public static SqlGeometry ToCurvePolygon(this GridVector2[] points)
        {
            StringBuilder PolyStringBuilder = new StringBuilder();
            System.Diagnostics.Debug.Assert(points.Length == 4);
            PolyStringBuilder.Append("CURVEPOLYGON(CIRCULARSTRING (");
            for (int i = 0; i < points.Length; i++)
            {
                if (i != 0)
                    PolyStringBuilder.AppendFormat(",");

                PolyStringBuilder.AppendFormat("{0} {1}", points[i].X, points[i].Y);
            }
            PolyStringBuilder.Append(")");

            return SqlGeometry.STGeomFromText(PolyStringBuilder.ToString().ToSqlChars(),0);
        }

        public static GridVector2[] ToPoints(this Microsoft.SqlServer.Types.SqlGeometry geometry)
        {
            GridVector2[] points = new GridVector2[geometry.STNumPoints().Value];
            for(int i = 0; i < geometry.STNumPoints(); i++)
            { 
                SqlGeometry point = geometry.STPointN(i);
                points[i] = new GridVector2(point.STX.Value, point.STY.Value);
            }

            return points; 
        }

        public static SqlGeometry ToGeometry(GridVector2[] points, double Z, double radius, LocationType type)
        {
            switch (type)
            {
                case LocationType.CIRCLE:
                    if (points.Length == 4)
                        return points.ToCurvePolygon();
                    else
                        return GeometryExtensions.ToCurvePolygon(points[0].X, points[1].Y, Z, radius);
                case LocationType.OPENCURVE:
                    return points.ToPolyLine();
            }

            throw new ArgumentException(string.Format("Unknown location type {0}", type));
        }
    }
}
