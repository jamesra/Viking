using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Geometry;
using System.Threading.Tasks;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;

namespace SqlGeometryUtils
{
    public static class GeometryExtensions
    {
        private static readonly int RoundingDigits = 2;

        public static System.Data.SqlTypes.SqlString ToSqlString(this string str)
        {
            return new System.Data.SqlTypes.SqlString(str);
        }

        public static System.Data.SqlTypes.SqlChars ToSqlChars(this string str)
        {
            return new SqlChars(str.ToCharArray());
        }

        public static Microsoft.SqlServer.Types.SqlGeometry ToGeometryPoint(this GridVector2 p)
        {
            return Microsoft.SqlServer.Types.SqlGeometry.Point(Math.Round(p.X, RoundingDigits),
                                                               Math.Round(p.Y, RoundingDigits), 0);
        }

        public static GridVector2 Centroid(this System.Data.Entity.Spatial.DbGeometry geometry)
        {
            System.Data.Entity.Spatial.DbGeometry centroid = geometry.Centroid;
            if (centroid != null)
                return new GridVector2(centroid.XCoordinate.Value, centroid.YCoordinate.Value);
            else
                return geometry.ToSqlGeometry().Centroid();
                //throw new ArgumentException("Calling centroid on geometry type without centroid, dimension is " + geometry.Dimension.ToString() + " shape is " + geometry.ToString());
        }

        public static Microsoft.SqlServer.Types.SqlGeometry ToSqlGeometry(this System.Data.Entity.Spatial.DbGeometry geometry)
        {
            return Microsoft.SqlServer.Types.SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes(geometry.AsBinary()), geometry.CoordinateSystemId);
        }

        public static System.Data.Entity.Spatial.DbGeometry ToDbGeometry(this Microsoft.SqlServer.Types.SqlGeometry geometry)
        {
            return System.Data.Entity.Spatial.DbGeometry.FromBinary(geometry.STAsBinary().Buffer, geometry.STSrid.Value);
        }

        public static SqlGeometry ToSqlGeometry(this GridCircle circle, double Z)
        {
            return ToCircle(circle.Center.X,
                            circle.Center.Y,
                            Z,
                            circle.Radius);
        }

        public static SqlGeometry ToPolyLine(this GridLineSegment line)
        {
            return new GridVector2[] { line.A, line.B }.ToPolyLine();
        }

        public static SqlGeometry ToPolyLine(this GridVector2[] points)
        {
            StringBuilder PolyStringBuilder = new StringBuilder();
            PolyStringBuilder.Append("LINESTRING");
            PolyStringBuilder.Append(points.ToSqlCoordinateList());
            return SqlGeometry.STLineFromText(PolyStringBuilder.ToString().ToSqlChars(), 0);
        }

        public static SqlGeometry ToPolygon(this GridVector2[] points)
        {
            if(points.Length < 3)
            {
                throw new ArgumentException("Polygon must be created with three points or more");
            }

            if (points.AreClockwise())
                points = points.Reverse().ToArray();

            if (points.First() != points.Last())
            {
                List<GridVector2> listPoints = new List<GridVector2>(points);
                listPoints.Add(points[0]);
                points = listPoints.ToArray();
            }

            StringBuilder PolyStringBuilder = new StringBuilder();
            
            PolyStringBuilder.Append("POLYGON( ");
            PolyStringBuilder.Append(points.ToSqlCoordinateList());
            PolyStringBuilder.Append(")");
            return SqlGeometry.STPolyFromText(PolyStringBuilder.ToString().ToSqlChars(), 0);
        }

        public static SqlGeometry ToCircle(this GridVector2[] points)
        {
            if (points.Length < 3)
            {
                throw new ArgumentException("Polygon must be created with three points or more");
            }

            if (points.AreClockwise())
                points = points.Reverse().ToArray();

            if (points.First() != points.Last())
            {
                List<GridVector2> listPoints = new List<GridVector2>(points);
                listPoints.Add(points[0]);
                points = listPoints.ToArray();
            }

            return points.ToPolygon().CalculateInscribedCircle(points).ToSqlGeometry(0);
        }

        /// <summary>
        /// Create a closed object where the first point in the array is added again at the end
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static string ToSqlClosedCoordinateList(this GridVector2[] points)
        {
            return points.ToSqlCoordinateList(true);
        }

        public static string ToSqlCoordinateList(this GridVector2[] points, bool closed = false)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("(");
            for (int i = 0; i < points.Length; i++)
            {
                if (i != 0)
                    sb.AppendFormat(", ");

                sb.AppendFormat("{0:F2} {1:F2}", points[i].X, points[i].Y);
            }

            if(closed && points[0] != points.Last())
                sb.AppendFormat(", {0:F2} {1:F2}", points[0].X, points[0].Y);

            sb.Append(")");

            return sb.ToString();
        }

        public static SqlGeometry ToCircle(double X, double Y, double Z, double Radius)
        {
            if (Radius == 0)
                throw new ArgumentException("Cannot create circle with a radius of zero");

            string circle_template = "CURVEPOLYGON(CIRCULARSTRING ({1:F2} {3:F2} {6:D}, " +
                                                                  "{0:F2} {5:F2} {6:D}, " +
                                                                  "{2:F2} {3:F2} {6:D}, " +
                                                                  "{0:F2} {4:F2} {6:D}, " +
                                                                  "{1:F2} {3:F2} {6:D}))";
            string circle_shape_string = string.Format(circle_template, new object[] { X, X - Radius, X + Radius, Y, Y - Radius, Y + Radius, (int)Z });
            return SqlGeometry.STGeomFromText(circle_shape_string.ToSqlChars(), 0);
        }


        public static SqlGeometry ToCurvePolygon(this GridVector2[] points)
        {
            StringBuilder PolyStringBuilder = new StringBuilder();
            System.Diagnostics.Debug.Assert(points.Length == 4);
            PolyStringBuilder.Append("CURVEPOLYGON(CIRCULARSTRING");
            PolyStringBuilder.Append(points.ToSqlCoordinateList());
            PolyStringBuilder.Append(")");
            return SqlGeometry.STGeomFromText(PolyStringBuilder.ToString().ToSqlChars(), 0);
        }

        /// <summary>
        /// For some insane reason STPointN and STGeometryN starts indexing at 1 instead of zero.  This
        /// helper function avoids that madness
        /// </summary>
        /// <param name="geometry"></param>
        /// <returns></returns>
        public static SqlGeometry GetPoint(this Microsoft.SqlServer.Types.SqlGeometry geometry, int i)
        {
            return geometry.STPointN(i + 1);
        }

        /// <summary>
        /// For some insane reason STPointN and STGeometryN starts indexing at 1 instead of zero.  This
        /// helper function avoids that madness
        /// </summary>
        /// <param name="geometry"></param>
        /// <returns></returns>
        public static SqlGeometry GetGeometry(this Microsoft.SqlServer.Types.SqlGeometry geometry, int i)
        {
            return geometry.STGeometryN(i + 1);
        }

        public static GridRectangle BoundingBox(this SqlGeometry geometry)
        {
            return GridRectangle.GetBoundingBox(geometry.STEnvelope().ToPoints());
        }

        public static GridRectangle BoundingBox(this System.Data.Entity.Spatial.DbGeometry geometry)
        {
            System.Data.Entity.Spatial.DbGeometry envelope = geometry.Envelope;
            return GridRectangle.GetBoundingBox(envelope.ToPoints());
        }

        public static bool Intersects(this SqlGeometry geometry, GridVector2 point)
        {
            SqlGeometry p = point.ToGeometryPoint();
            bool intersects = geometry.STIntersects(p).IsTrue;
            return intersects;
            //return geometry.STIntersects(point.ToGeometryPoint()).IsTrue;
        }

        public static double Distance(this SqlGeometry geometry, GridVector2 point)
        {
            return geometry.STDistance(point.ToGeometryPoint()).Value;
        }

        public static GridVector2[] ToPoints(this System.Data.Entity.Spatial.DbGeometry geometry)
        {
            if (!geometry.PointCount.HasValue)
                return new GridVector2[0];

            GridVector2[] points = new GridVector2[geometry.PointCount.Value];
            for (int i = 0; i < points.Length; i++)
            {
                System.Data.Entity.Spatial.DbGeometry point = geometry.PointAt(i+1);
                points[i] = new GridVector2(point.XCoordinate.Value, point.YCoordinate.Value);
            }

            return points;
        }

        public static GridVector2[] ToPoints(this Microsoft.SqlServer.Types.SqlGeometry geometry)
        {
            GridVector2[] points = new GridVector2[geometry.STNumPoints().Value];
            for (int i = 0; i < points.Length; i++)
            {
                SqlGeometry point = geometry.GetPoint(i);
                points[i] = new GridVector2(point.STX.Value, point.STY.Value);
            }

            return points;
        }

        public static GridVector2 Centroid(this Microsoft.SqlServer.Types.SqlGeometry geometry)
        {
            SqlGeometry center = geometry.STCentroid();
            if(!center.IsNull)
                return new GridVector2(System.Math.Round(center.STX.Value, RoundingDigits),
                                       System.Math.Round(center.STY.Value, RoundingDigits));

            if (center.STNumPoints() == 1)
                return new GridVector2(System.Math.Round(geometry.STX.Value, RoundingDigits),
                                       System.Math.Round(geometry.STY.Value, RoundingDigits));

            return geometry.STEnvelope().Centroid();
        }

        public static SqlGeometry ToGeometry(SqlString GeometryType, GridVector2[] points)
        {
            SqlGeometry obj = SqlGeometry.STGeomFromText(ToGeometryString(GeometryType, points).ToSqlChars(),0);
            if (obj.STIsValid().IsFalse)
            {
                throw new ArgumentException(obj.IsValidDetailed());
            }
            return obj;
        }

        public static string ToGeometryString(SqlString GeometryType, GridVector2[] points )
        {
           

            string TypeString = GeometryType.Value; 
            switch(TypeString.ToUpper())
            {
                case "CURVEPOLYGON":
                    TypeString += "( CIRCULARSTRING " + points.ToSqlCoordinateList() + ")";
                    return TypeString;
                case "POLYGON":
                    if (points.AreClockwise())
                        points = points.Reverse().ToArray();
                    TypeString += "( " + points.ToSqlCoordinateList(true) + ")";
                    return TypeString;
                default:
                    return GeometryType.Value + points.ToSqlCoordinateList();
            }
        }

        public static string ToGeometryString(SqlString GeometryType, string[] contents)
        {
            StringBuilder output = new StringBuilder(GeometryType.Value + '(');
            for (int i = 0; i < contents.Length; i++)
            {
                if (i != 0)
                    output.Append(',');

                output.Append(contents[i]);
            }

            output.Append(')');
            return output.ToString();
        }

        /// <summary>
        /// Move the geometry objects centroid to the given coordinates
        /// </summary>
        /// <param name="geometry"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static SqlGeometry MoveTo(this SqlGeometry geometry, GridVector2 offset)
        {
            GridVector2 center = geometry.Centroid();
            return SqlGeometry.STGeomFromText(TranslateString(geometry, offset - center).ToSqlChars(), geometry.STSrid.Value);
        }

        /// <summary>
        /// Scale the geometry object using the scale object
        /// </summary>
        /// <param name="geometry"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public static SqlGeometry Scale(this SqlGeometry geometry, Scale scale)
        {
            GridVector2[] points = geometry.ToPoints();
            GridVector2[] scaled_p = points.Select(p => new GridVector2(p.X * scale.X.Value, p.Y * scale.Y.Value)).ToArray();
            return ToGeometry(geometry.STGeometryType(), scaled_p);
        }

        /// <summary>
        /// Move the geometry objects centroid by the provided offset
        /// </summary>
        /// <param name="geometry"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static SqlGeometry Translate(this SqlGeometry geometry, GridVector2 offset)
        {
            return SqlGeometry.STGeomFromText(TranslateString(geometry, offset).ToSqlChars(), geometry.STSrid.Value);
        }

        public static string TranslateString(SqlGeometry geometry, GridVector2 offset)
        { 
            GridVector2[] translated_points = geometry.ToPoints().Select(p => p + offset).ToArray();

            StringBuilder geometryStringBuilder = new StringBuilder();
            if (translated_points.Length > 0)
            {
                geometryStringBuilder.Append(ToGeometryString(geometry.STGeometryType(), translated_points));
            }

            /* We aren't doing nested geometries.  Seems Microsoft.SqlServer.Types is a mess. */
            /*
            if (!geometry.STNumGeometries().IsNull)
            {
                string[] subgeom_strings = new string[geometry.STNumGeometries().Value];
                for (int iGeom = 0; iGeom < subgeom_strings.Length; iGeom++)
                {
                    if (iGeom != 0)
                        geometryStringBuilder.Append(',');

                    SqlGeometry subgeom = geometry.GetGeometry(iGeom);
                    subgeom_strings[iGeom] = MoveToString(subgeom, offset);
                }

                geometryStringBuilder.Append(ToGeometryString(geometry.STGeometryType(), subgeom_strings));
            }*/

            return geometryStringBuilder.ToString();
        }

        public static GridCircle CalculateInscribedCircle(this SqlGeometry shape)
        {
            GridVector2[] ControlPoints = shape.ToPoints();
            return shape.CalculateInscribedCircle(ControlPoints);
        }

        public static GridCircle CalculateInscribedCircle(this SqlGeometry shape, ICollection<GridVector2> ControlPoints)
        { 
            GridVector2 center = shape.Centroid();
            double Radius = ControlPoints.Select(p => GridVector2.Distance(center, p)).Min();
            return new GridCircle(center, Radius);
        }
    }
}
