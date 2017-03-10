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
    /// <summary>
    /// An enumeration of the geometry types supported in Viking
    /// </summary>
    public enum SupportedGeometryType
    {
        POINT,
        CURVEPOLYGON,
        POLYGON,
        LINE
    };

    public static class Extensions
    {
        private static readonly int RoundingDigits = 2;

        /// <summary>
        /// A unit circle with points along the East, North, points...
        /// </summary>
        static private GridVector2[] circleCardinalPoints;

        static Extensions()
        {
            circleCardinalPoints = CalculateCircleCardinalPoints();
        }

        public static SupportedGeometryType GeometryType(this SqlGeometry geometry)
        {
            switch (geometry.STGeometryType().Value.ToUpper())
            {
                case "POINT":
                    return SupportedGeometryType.POINT;
                case "CURVEPOLYGON":
                    return SupportedGeometryType.CURVEPOLYGON;
                case "LINESTRING":
                    return SupportedGeometryType.LINE;
                case "POLYGON":
                    return SupportedGeometryType.POLYGON;
                default:
                    throw new ArgumentException("Unexpected geometry type: " + geometry.STGeometryType().Value);
            }
        }

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
            if (geometry.WellKnownValue.WellKnownBinary != null)
                return Microsoft.SqlServer.Types.SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes(geometry.WellKnownValue.WellKnownBinary), geometry.CoordinateSystemId);
            else
            {
                return SqlGeometry.STGeomFromWKB(new SqlBytes(geometry.AsBinary()), geometry.CoordinateSystemId);
                //return Microsoft.SqlServer.Types.SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(geometry.WellKnownValue.WellKnownText), geometry.CoordinateSystemId);
            }
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
            SqlGeometryBuilder builder = new SqlGeometryBuilder();
            builder.SetSrid(0);
            builder.BeginGeometry(OpenGisGeometryType.LineString);
            builder.BeginFigure(points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++)
            {
                builder.AddLine(points[i].X, points[i].Y);
            }
            builder.EndFigure();
            builder.EndGeometry();
            return builder.ConstructedGeometry;
            /*
            StringBuilder PolyStringBuilder = new StringBuilder();
            PolyStringBuilder.Append("LINESTRING");
            PolyStringBuilder.Append(points.ToSqlCoordinateList());
            return SqlGeometry.STLineFromText(PolyStringBuilder.ToString().ToSqlChars(), 0);
            */
        }

        public static SqlGeometry ToPolygon(this GridVector2[] points, ICollection<GridVector2[]> InteriorRings = null)
        {
            SqlGeometryBuilder builder = new SqlGeometryBuilder();
            builder.SetSrid(0);
            builder.BeginGeometry(OpenGisGeometryType.Polygon);

            builder.AddPolygon(points);

            if (InteriorRings != null)
            {
                //Add the interior rings
                foreach (GridVector2[] innerRing in InteriorRings)
                {
                    builder.AddPolygon(innerRing);
                }
            }

            builder.EndGeometry();
            return builder.ConstructedGeometry;

            /*
            StringBuilder PolyStringBuilder = new StringBuilder();
            
            PolyStringBuilder.Append("POLYGON( ");
            PolyStringBuilder.Append(points.ToSqlCoordinateList());
            PolyStringBuilder.Append(")");
            return SqlGeometry.STPolyFromText(PolyStringBuilder.ToString().ToSqlChars(), 0);
            */
        }

        /// <summary>
        /// Add the GridVector2 points to the polygon builder. 
        /// BeginGeometry(OpenGisGeometryType.Polygon) should already have been called
        /// </summary>
        /// <param name="points"></param>
        private static void AddPolygon(this SqlGeometryBuilder builder, GridVector2[] points)
        {
            if (points.Length < 3)
            {
                throw new ArgumentException("Polygon must be created with three points or more");
            }

            if (points.AreClockwise())
                points = points.Reverse().ToArray();

            //Ensure the first and last element are the same
            if (points.First() != points.Last())
            {
                GridVector2[] pointsAppended = new GridVector2[points.Length + 1];
                points.CopyTo(pointsAppended, 0);
                pointsAppended[points.Length] = points[0];
                points = pointsAppended;
            }

            builder.BeginFigure(points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++)
            {
                builder.AddLine(points[i].X, points[i].Y);
            }

            builder.EndFigure();
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

            if (closed && points[0] != points.Last())
                sb.AppendFormat(", {0:F2} {1:F2}", points[0].X, points[0].Y);

            sb.Append(")");

            return sb.ToString();
        }

        

        private static GridVector2[] CalculateCircleCardinalPoints()
        {
            //Place points around the circle
            double tau = Math.PI * 2.0;
            GridVector2[] points = new GridVector2[5];
            for (int i = 0; i < 4; i++)
            {
                double fraction = (double)i / 4.0;
                double angle = fraction * tau;
                points[i] = new GridVector2(Math.Cos(angle), Math.Sin(angle));
            }

            points[4] = points[0];

            return points;
        }

        private static GridVector2[] ScaleAndTranslateCircleCardinalPoints(double X, double Y, double Radius)
        {
            GridVector2[] points = new GridVector2[circleCardinalPoints.Length];
            circleCardinalPoints.CopyTo(points, 0);

            for (int i = 0; i < points.Length; i++)
            {
                points[i].X *= Radius;
                points[i].Y *= Radius;
                points[i].X += X;
                points[i].Y += Y;
            }

            return points;
        }

        public static SqlGeometry ToCircle(double X, double Y, double Z, double Radius)
        {
            if (Radius == 0)
                throw new ArgumentException("Cannot create circle with a radius of zero");

            
            GridVector2[] points = ScaleAndTranslateCircleCardinalPoints(X, Y, Radius);

            SqlGeometryBuilder builder = new SqlGeometryBuilder();
            builder.SetSrid(0);
            builder.BeginGeometry(OpenGisGeometryType.CurvePolygon);
            builder.BeginFigure(points[0].X, points[0].Y, Z, null);

            for(int i = 1; i < points.Length; i += 2)
            {
                //builder.AddLine(points[i].X, points[i].Y, Z, null);
                builder.AddCircularArc(points[i].X, points[i].Y, Z, null,
                                       points[i + 1].X, points[i + 1].Y, Z, null);
            }
            
            builder.EndFigure();
            builder.EndGeometry();

            //GridVector2 radius_test = points[0] - points[2];

            SqlGeometry output = builder.ConstructedGeometry;
            /*
            
            string circle_template = "CURVEPOLYGON(CIRCULARSTRING ({1:F2} {3:F2} {6:D}, " +
                                                                  "{0:F2} {5:F2} {6:D}, " +
                                                                  "{2:F2} {3:F2} {6:D}, " +
                                                                  "{0:F2} {4:F2} {6:D}, " +
                                                                  "{1:F2} {3:F2} {6:D}))";
            string circle_shape_string = string.Format(circle_template, new object[] { X, X - Radius, X + Radius, Y, Y - Radius, Y + Radius, (int)Z });
            */
            return output;
            //return SqlGeometry.STGeomFromText(circle_shape_string.ToSqlChars(), 0);
            
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

        /// <summary>
        /// For some insane reason STInteriorRingN starts indexing at 1 instead of zero.  This
        /// helper function avoids that madness
        /// </summary>
        /// <param name="geometry"></param>
        /// <returns></returns>
        public static SqlGeometry GetInteriorRing(this Microsoft.SqlServer.Types.SqlGeometry geometry, int i)
        {
            return geometry.STInteriorRingN(i + 1);
        }

        public static int NumInteriorRings(this SqlGeometry geometry)
        {
            SqlInt32 numInteriorRings = geometry.STNumInteriorRing();
            if (numInteriorRings.IsNull)
                return 0;

            return numInteriorRings.Value;
        }

        public static bool HasInteriorRings(this SqlGeometry geometry)
        {
            SqlInt32 numInteriorRings = geometry.STNumInteriorRing();
            if (numInteriorRings.IsNull)
                return false;

            return numInteriorRings.Value > 0;
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

        /// <summary>
        /// Return the points for the geometry, if it is a polygon return the rings around the exterior
        /// </summary>
        /// <param name="geometry"></param>
        /// <returns></returns>
        public static GridVector2[] ToPoints(this System.Data.Entity.Spatial.DbGeometry geometry)
        {
            if (!geometry.PointCount.HasValue)
                return new GridVector2[0];

            if (!geometry.InteriorRingCount.HasValue)
            {
                GridVector2[] points = new GridVector2[geometry.PointCount.Value];
                for (int i = 0; i < points.Length; i++)
                {
                    System.Data.Entity.Spatial.DbGeometry point = geometry.PointAt(i + 1);
                    points[i] = new GridVector2(point.XCoordinate.Value, point.YCoordinate.Value);
                }

                return points;
            }
            else
            {
                return geometry.ExteriorRing.ToPoints();
            }

            
        }

        /// <summary>
        /// Return the points for the geometry, if it is a polygon return the rings around the exterior
        /// </summary>
        /// <param name="geometry"></param>
        /// <returns></returns>
        public static GridVector2[] ToPoints(this Microsoft.SqlServer.Types.SqlGeometry geometry)
        {
            if (!geometry.HasInteriorRings())
            {
                GridVector2[] points = new GridVector2[geometry.STNumPoints().Value];
                for (int i = 0; i < points.Length; i++)
                {
                    SqlGeometry point = geometry.GetPoint(i);
                    points[i] = new GridVector2(point.STX.Value, point.STY.Value);
                }

                return points;
            }
            else
            {
                return geometry.STExteriorRing().ToPoints();
            } 
        }

        public static GridVector2 Centroid(this Microsoft.SqlServer.Types.SqlGeometry geometry)
        {
            SqlGeometry center = geometry.STCentroid();
            if (!center.IsNull)
                return new GridVector2(System.Math.Round(center.STX.Value, RoundingDigits),
                                       System.Math.Round(center.STY.Value, RoundingDigits));

            if (center.STNumPoints() == 1)
                return new GridVector2(System.Math.Round(geometry.STX.Value, RoundingDigits),
                                       System.Math.Round(geometry.STY.Value, RoundingDigits));

            return geometry.STEnvelope().Centroid();
        }

        public static SqlGeometry ToGeometry(SqlString GeometryType, GridVector2[] points)
        {
            SqlGeometry obj = SqlGeometry.STGeomFromText(ToGeometryString(GeometryType, points).ToSqlChars(), 0);
            if (obj.STIsValid().IsFalse)
            {
                throw new ArgumentException(obj.IsValidDetailed());
            }
            return obj;
        }

        public static string ToGeometryString(SqlString GeometryType, GridVector2[] points)
        {  
            string TypeString = GeometryType.Value;
            switch (TypeString.ToUpper())
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
            return geometry.Translate(offset - center);
            //return SqlGeometry.STGeomFromText(TranslateString(geometry, offset - center).ToSqlChars(), geometry.STSrid.Value);
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
            //return SqlGeometry.STGeomFromText(TranslateString(geometry, offset).ToSqlChars(), geometry.STSrid.Value);

            Microsoft.SqlServer.Types.SqlGeometryBuilder builder = new SqlGeometryBuilder();
            builder.SetSrid(geometry.STSrid.Value);

            switch (geometry.GeometryType())
            {
                case SupportedGeometryType.POLYGON:
                    return TranslatePolygon(geometry, offset);
                default:
                    return SqlGeometry.STGeomFromText(TranslateString(geometry, offset).ToSqlChars(), geometry.STSrid.Value);
            }
        }

        private static SqlGeometry TranslatePolygon(SqlGeometry geometry, GridVector2 offset)
        {
            System.Diagnostics.Debug.Assert(geometry.GeometryType() == SupportedGeometryType.POLYGON);

            int NumInteriorRings = geometry.NumInteriorRings();
            List<GridVector2[]> InteriorRings = new List<GridVector2[]>(NumInteriorRings);
            GridVector2[] ExteriorRing = geometry.ToPoints();

            GridVector2[] TranslatedExteriorRing = ExteriorRing.Translate(offset).ToArray();

            for(int iRing = 0; iRing < NumInteriorRings; iRing++ )
            {
                GridVector2[] InteriorRing = geometry.GetInteriorRing(iRing).ToPoints();
                GridVector2[] TranslatedInteriorRing = InteriorRing.Translate(offset).ToArray();
                InteriorRings.Add(TranslatedInteriorRing);
            }

            return TranslatedExteriorRing.ToPolygon(InteriorRings);
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

        public static SqlGeometry AddInteriorPolygon(this SqlGeometry shape, ICollection<GridVector2> InteriorControlPoints)
        {
            throw new NotImplementedException();
        } 
    }
}
