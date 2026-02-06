using Geometry;
using Microsoft.SqlServer.Types;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
#if NET48
using System.Data.Entity;
#endif

namespace SqlGeometryUtils
{
    /// <summary>
    /// An enumeration of the SQL geometry types supported in Viking
    /// </summary>
    public enum SupportedGeometryType
    {
        /// <summary>
        /// A SQL Point
        /// </summary>
        POINT,
        /// <summary>
        /// Circles are represented as CurvePolygons in the SQL database, a CURVEPOLYGON is currently always a circle
        /// </summary>
        CURVEPOLYGON,
        /// <summary>
        /// A SQL Polygon
        /// </summary>
        POLYGON,
        /// <summary>
        /// A SQL Polyline
        /// </summary>
        POLYLINE
    };

    public static class SqlToMyGeometryConverters
    {
        public static GridPolygon ToPolygon(this SqlGeometry shape)
        {
            if (shape.GeometryType() != SupportedGeometryType.POLYGON && shape.GeometryType() != SupportedGeometryType.CURVEPOLYGON)
                throw new ArgumentException("SqlGeometry must be a polygon type");

            GridVector2[] ExteriorRing = shape.ToPoints();
            ICollection<GridVector2[]> InteriorRings = shape.InteriorRingPoints();

            try
            {
                return new GridPolygon(ExteriorRing, InteriorRings);
            }
            catch (ArgumentException e)
            {
                return new GridPolygon([.. ExteriorRing.RemoveAdjacentDuplicates()], InteriorRings.Select(ir => ir.RemoveAdjacentDuplicates().ToArray()));
            }
        }

        public static SqlGeometry ToSqlGeometry(this GridPolygon shape) => shape.ExteriorRing.ToPolygon([.. shape.InteriorRings]);

        public static GridPolyline ToPolyLine(this SqlGeometry shape)
        {
            if (shape.GeometryType() != SupportedGeometryType.POLYLINE)
                throw new ArgumentException("SqlGeometry must be a polygon type");

            GridVector2[] points = shape.ToPoints();
            return new GridPolyline(points.Cast<IPoint2D>());
        }

        public static GridCircle ToCircle(this SqlGeometry shape)
        {
            var current_type = shape.GeometryType();
            if (current_type != SupportedGeometryType.CURVEPOLYGON && current_type != SupportedGeometryType.POLYLINE)
                throw new ArgumentException("SqlGeometry must be a polygon type");

            GridRectangle bbox = shape.BoundingBox();
            //System.Diagnostics.Debug.Assert(Math.Floor(bbox.Width) == Math.Floor(bbox.Height)); //Make sure our optimization is really getting a circle
            return new GridCircle(bbox.Center, Math.Max(bbox.Width, bbox.Height) / 2.0);
        }


        public static IShape2D ToShape2D(this SqlGeometry shape)
        {
            return shape.GeometryType() switch
            {
                SupportedGeometryType.POINT => new GridVector2(shape.STX.Value, shape.STY.Value),
                SupportedGeometryType.POLYGON => shape.ToPolygon(),
                SupportedGeometryType.POLYLINE => shape.ToPolyLine(),
                SupportedGeometryType.CURVEPOLYGON => shape.ToCircle(),
                _ => throw new ArgumentException("Unknown SQL Geometry Type"),
            };
        }
    }

    public static class Extensions
    {
        private static readonly int RoundingDigits = 2;

        private const int nCircleCardinalPoints = 8;
        /// <summary>
        /// A unit circle with points along the East, North, points...
        /// </summary>
        private static readonly GridVector2[] circleCardinalPoints;

        static Extensions()
        {
            circleCardinalPoints = CalculateCircleCardinalPoints(nCircleCardinalPoints);
        }

        public static SupportedGeometryType GeometryType(this SqlGeometry geometry)
        {
            return geometry.STGeometryType().Value.ToUpper() switch
            {
                "POINT" => SupportedGeometryType.POINT,
                "CURVEPOLYGON" => SupportedGeometryType.CURVEPOLYGON,
                "LINESTRING" => SupportedGeometryType.POLYLINE,
                "POLYGON" => SupportedGeometryType.POLYGON,
                _ => throw new ArgumentException("Unexpected geometry type: " + geometry.STGeometryType().Value),
            };
        }

        public static System.Data.SqlTypes.SqlString ToSqlString(this string str) => new System.Data.SqlTypes.SqlString(str);

        public static System.Data.SqlTypes.SqlChars ToSqlChars(this string str) => new SqlChars(str.ToCharArray());

        public static bool SpatialEquals(this SqlGeometry geom, SqlGeometry other)
        {
            if (object.ReferenceEquals(geom, other))
                return true;

            return geom.STEquals(other).Value;
        }

        public static void ThrowIfInvalid(this SqlGeometry geom)
        {
            if (geom.STIsValid().IsFalse)
            {
                throw new ArgumentException(string.Format("Geometry invalid\n{0}\n{1}", geom.ToString(), geom.IsValidDetailed()));
            }
        }


        public static Microsoft.SqlServer.Types.SqlGeometry ToSqlGeometry(this GridVector2 p)
        {
            return Microsoft.SqlServer.Types.SqlGeometry.Point(Math.Round(p.X, RoundingDigits),
                                                               Math.Round(p.Y, RoundingDigits), 0);
        }

        public static Microsoft.SqlServer.Types.SqlGeometry ToGeometryPoint(double X, double Y)
        {
            return Microsoft.SqlServer.Types.SqlGeometry.Point(Math.Round(X, RoundingDigits),
                                                               Math.Round(Y, RoundingDigits), 0);
        }

#if NET48
        public static GridVector2 Centroid(this System.Data.Entity.Spatial.DbGeometry geometry)
        {
            System.Data.Entity.Spatial.DbGeometry centroid = geometry.Centroid;
            if (centroid != null)
                return new GridVector2(centroid.XCoordinate.Value, centroid.YCoordinate.Value);
            else
                return geometry.ToSqlGeometry().Centroid();
            //throw new ArgumentException("Calling centroid on geometry type without centroid, dimension is " + geometry.Dimension.ToString() + " shape is " + geometry.ToString());
        }
#endif

        public static SqlGeometry ToSqlGeometry(this byte[] WellKnownBinary, int SRID = 0) => SqlGeometry.STGeomFromWKB(new SqlBytes(WellKnownBinary), SRID);

#if NET48
        public static Microsoft.SqlServer.Types.SqlGeometry ToSqlGeometry(this System.Data.Entity.Spatial.DbGeometry geometry)
        {
            if (geometry.WellKnownValue.WellKnownBinary != null)
                return Microsoft.SqlServer.Types.SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes(geometry.WellKnownValue.WellKnownBinary), geometry.CoordinateSystemId);
            else
            {
                //return SqlGeometry.STGeomFromWKB(new SqlBytes(geometry.AsBinary()), geometry.CoordinateSystemId);
                return Microsoft.SqlServer.Types.SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(geometry.WellKnownValue.WellKnownText), geometry.CoordinateSystemId);
            }
        }
#endif

#if NET48
        public static System.Data.Entity.Spatial.DbGeometry ToDbGeometry(this Microsoft.SqlServer.Types.SqlGeometry geometry) => System.Data.Entity.Spatial.DbGeometry.FromBinary(geometry.STAsBinary().Buffer, geometry.STSrid.Value);
#endif


        public static SqlGeometry ToSqlGeometry(this GridCircle circle, double Z = 0)
        {
            return ToCircle(circle.Center.X,
                            circle.Center.Y,
                            Z,
                            circle.Radius);
        }

        public static byte[] AsBinary(this SqlGeometry geom) => geom.STAsBinary().Value;

        public static SqlGeometry ToSqlGeometry(this GridLineSegment line) => new GridVector2[] { line.A, line.B }.ToSqlGeometry();

        /// <summary>
        /// Create a linestring from a polyline
        /// </summary>
        /// <param name="polyline"></param>
        /// <param name="Z"></param>
        /// <returns></returns>
        public static SqlGeometry ToSqlGeometry(this GridPolyline polyline) => ToSqlGeometry(polyline.Points);

        /// <summary>
        /// Create a LineString from an array of points
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static SqlGeometry ToSqlGeometry(this IReadOnlyList<IPoint2D> points)
        {
            SqlGeometryBuilder builder = new();
            builder.SetSrid(0);
            builder.BeginGeometry(OpenGisGeometryType.LineString);
            builder.BeginFigure(points[0].X, points[0].Y);
            for (int i = 1; i < points.Count; i++)
            {
                builder.AddLine(points[i].X, points[i].Y);
            }
            builder.EndFigure();
            builder.EndGeometry();
            builder.ConstructedGeometry.ThrowIfInvalid();
            return builder.ConstructedGeometry;
        }

        /// <summary>
        /// Create a LineString from an array of points
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static SqlGeometry ToSqlGeometry(this IReadOnlyList<GridVector2> points)
        {
            SqlGeometryBuilder builder = new();
            builder.SetSrid(0);
            builder.BeginGeometry(OpenGisGeometryType.LineString);
            builder.BeginFigure(points[0].X, points[0].Y);
            for (int i = 1; i < points.Count; i++)
            {
                builder.AddLine(points[i].X, points[i].Y);
            }
            builder.EndFigure();
            builder.EndGeometry();
            builder.ConstructedGeometry.ThrowIfInvalid();
            return builder.ConstructedGeometry;
        }

        public static SqlGeometry ToSqlGeometry(this IShape2D shape)
        {
            if (shape is GridPolygon polygon)
            {
                return polygon.ToSqlGeometry();
            }
            else if (shape is GridPolyline polyline)
            {
                return polyline.ToSqlGeometry();
            }
            else if (shape is GridCircle circle)
            {
                return circle.ToSqlGeometry();
            }

            throw new NotImplementedException($"Missing ToSqlGeometry implementation for {shape.ShapeType}");
        }

        public static IShape2D ToIShape2D(this SqlGeometry shape)
        {
            return shape.GeometryType() switch
            {
                SupportedGeometryType.POINT => throw new NotImplementedException("Point cannot be converted to IShape2D"),
                SupportedGeometryType.POLYLINE => shape.ToPolyLine(),
                SupportedGeometryType.POLYGON => shape.ToPolygon(),
                SupportedGeometryType.CURVEPOLYGON => shape.ToPolygon(),
                _ => throw new NotImplementedException(string.Format("shape cannot be converted to IShape2D {0}", shape)),
            };
        }

        public static SqlGeometry ToPolygon(this GridVector2[] points, ICollection<GridVector2[]> InteriorRings = null)
        {
            SqlGeometryBuilder builder = new();
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

            SqlGeometry polygon = builder.ConstructedGeometry;

            polygon.ThrowIfInvalid();

            return polygon;

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
                points = [.. points.AsEnumerable().Reverse()];

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
                points = [.. points.AsEnumerable().Reverse()];

            if (points.First() != points.Last())
            {
                List<GridVector2> listPoints = [.. points, points[0]];
                points = [.. listPoints];
            }

            return points.ToPolygon().CalculateInscribedCircle(points).ToSqlGeometry(0);
        }

        /// <summary>
        /// Create a closed object where the first point in the array is added again at the end
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static string ToSqlClosedCoordinateList(this GridVector2[] points) => points.ToSqlCoordinateList(true);

        public static string ToSqlCoordinateList(this GridVector2[] points, bool closed = false)
        {
            StringBuilder sb = new();
            sb.Append('(');
            for (int i = 0; i < points.Length; i++)
            {
                if (i != 0)
                    sb.AppendFormat(", ");

                sb.AppendFormat("{0:F2} {1:F2}", points[i].X, points[i].Y);
            }

            if (closed && points[0] != points.Last())
                sb.AppendFormat(", {0:F2} {1:F2}", points[0].X, points[0].Y);

            sb.Append(')');

            return sb.ToString();
        }



        private static GridVector2[] CalculateCircleCardinalPoints(int nPoints)
        {
            //Place points around the circle
            const double tau = Math.PI * 2.0;
            GridVector2[] points = new GridVector2[nPoints + 1];
            for (int i = 0; i < nPoints; i++)
            {
                double fraction = (double)i / nPoints;
                double angle = fraction * tau;
                points[i] = new GridVector2(Math.Cos(angle), Math.Sin(angle));
            }

            points[nPoints] = points[0];

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

            SqlGeometryBuilder builder = new();
            builder.SetSrid(0);
            builder.BeginGeometry(OpenGisGeometryType.CurvePolygon);
            builder.BeginFigure(X + Radius, Y, Z, null);            // East
            builder.AddCircularArc(X, Y + Radius, Z, null,          // North (arc midpoint)
                                   X - Radius, Y, Z, null);          // West (arc endpoint)
            builder.AddCircularArc(X, Y - Radius, Z, null,          // South (arc midpoint)
                                   X + Radius, Y, Z, null);          // East (arc endpoint, closing)
            builder.EndFigure();
            builder.EndGeometry();

            return builder.ConstructedGeometry;
        }


        public static SqlGeometry ToCurvePolygon(this GridVector2[] points)
        {
            StringBuilder PolyStringBuilder = new();
            System.Diagnostics.Debug.Assert(points.Length == 4);
            PolyStringBuilder.Append("CURVEPOLYGON(CIRCULARSTRING");
            PolyStringBuilder.Append(points.ToSqlCoordinateList());
            PolyStringBuilder.Append(')');
            return SqlGeometry.STGeomFromText(PolyStringBuilder.ToString().ToSqlChars(), 0);
        }

        /// <summary>
        /// For some insane reason STPointN and STGeometryN starts indexing at 1 instead of zero.  This
        /// helper function avoids that madness
        /// </summary>
        /// <param name="geometry"></param>
        /// <returns></returns>
        public static SqlGeometry GetPoint(this Microsoft.SqlServer.Types.SqlGeometry geometry, int i) => geometry.STPointN(i + 1);

        /// <summary>
        /// For some insane reason STPointN and STGeometryN starts indexing at 1 instead of zero.  This
        /// helper function avoids that madness
        /// </summary>
        /// <param name="geometry"></param>
        /// <returns></returns>
        public static SqlGeometry GetGeometry(this Microsoft.SqlServer.Types.SqlGeometry geometry, int i) => geometry.STGeometryN(i + 1);

        /// <summary>
        /// For some insane reason STInteriorRingN starts indexing at 1 instead of zero.  This
        /// helper function avoids that madness
        /// </summary>
        /// <param name="geometry"></param>
        /// <returns></returns>
        public static SqlGeometry GetInteriorRing(this Microsoft.SqlServer.Types.SqlGeometry geometry, int i) => geometry.STInteriorRingN(i + 1);

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

        public static GridRectangle BoundingBox(this SqlGeometry geometry) => GridRectangle.GetBoundingBox(geometry.STEnvelope().ToPoints());

#if NET48
        public static GridRectangle BoundingBox(this System.Data.Entity.Spatial.DbGeometry geometry)
        {
            System.Data.Entity.Spatial.DbGeometry envelope = geometry.Envelope;
            return GridRectangle.GetBoundingBox(envelope.ToPoints());
        }
#endif

        public static bool Intersects(this SqlGeometry geometry, GridVector2 point)
        {
            SqlGeometry p = point.ToSqlGeometry();
            bool intersects = geometry.STIntersects(p).IsTrue;
            return intersects;
            //return geometry.STIntersects(point.ToGeometryPoint()).IsTrue;
        }

        public static bool Intersects(this SqlGeometry geometry, GridLineSegment line)
        {
            SqlGeometry p = line.ToSqlGeometry();
            bool intersects = geometry.STIntersects(p).IsTrue;
            return intersects;
            //return geometry.STIntersects(point.ToGeometryPoint()).IsTrue;
        }

        public static double Distance(this SqlGeometry geometry, GridVector2 point) => geometry.STDistance(point.ToSqlGeometry()).Value;

#if NET48
        /// <summary>
        /// Return the points for the geometry, if it is a polygon return the rings around the exterior
        /// </summary>
        /// <param name="geometry"></param>
        /// <returns></returns>
        public static GridVector2[] ToPoints(this System.Data.Entity.Spatial.DbGeometry geometry)
        {
            if (!geometry.PointCount.HasValue)
                return [];

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
#endif

        /// <summary>
        /// Return the points for the geometry, if it is a polygon return the rings around the exterior
        /// </summary>
        /// <param name="geometry"></param>
        /// <returns></returns>
        public static GridVector2[] ToPoints(this Microsoft.SqlServer.Types.SqlGeometry geometry)
        {
            if (!geometry.HasInteriorRings())
            {
                SupportedGeometryType type = geometry.GeometryType();

                if (type != SupportedGeometryType.CURVEPOLYGON)
                {
                    GridVector2[] points = new GridVector2[geometry.STNumPoints().Value];
                    for (int i = 0; i < points.Length; i++)
                    {
                        SqlGeometry point = geometry.GetPoint(i);
                        points[i] = new GridVector2(point.STX.Value, point.STY.Value);
                    }

                    return points;
                }
                else if (type == SupportedGeometryType.CURVEPOLYGON)
                {
                    GridVector2[] points = new GridVector2[nCircleCardinalPoints];
                    GridCircle circle = geometry.ToCircle();

                    return [.. circleCardinalPoints.Select(p => (p * circle.Radius) + circle.Center)];
                }

                throw new NotImplementedException("Unexpected geometry type passed to Points");
            }
            else
            {
                return geometry.STExteriorRing().ToPoints();
            }
        }

        /// <summary>
        /// Return the points for the geometry, if it is a polygon return the rings around the exterior
        /// </summary>
        /// <param name="geometry"></param>
        /// <returns></returns>
        public static List<GridVector2[]> InteriorRingPoints(this Microsoft.SqlServer.Types.SqlGeometry geometry)
        {
            if (!geometry.HasInteriorRings())
            {
                return [];
            }

            List<GridVector2[]> innerRings = new(geometry.NumInteriorRings());
            for (int iRing = 0; iRing < geometry.NumInteriorRings(); iRing++)
            {
                SqlGeometry innerRing = geometry.GetInteriorRing(iRing);
                innerRings.Add(innerRing.ToPoints());
            }

            return innerRings;
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
                        points = [.. points.AsEnumerable().Reverse()];
                    TypeString += "( " + points.ToSqlCoordinateList(true) + ")";
                    return TypeString;
                default:
                    return GeometryType.Value + points.ToSqlCoordinateList();
            }
        }

        public static string ToGeometryString(SqlString GeometryType, string[] contents)
        {
            StringBuilder output = new(GeometryType.Value + '(');
            for (int i = 0; i < contents.Length; i++)
            {
                if (i != 0)
                    output.Append(',');

                output.Append(contents[i]);
            }

            output.Append(')');
            return output.ToString();
        }

        public static SqlGeometry ToGeometry(SupportedGeometryType GeometryType, GridVector2[] points, ICollection<GridVector2[]> innerRings = null)
        {
            switch (GeometryType)
            {
                case SupportedGeometryType.POINT:
                    System.Diagnostics.Debug.Assert(points.Length == 1, "Only expect one point when converting to geometry point type");
                    return ToSqlGeometry(points.First());
                case SupportedGeometryType.POLYLINE:
                    return ToSqlGeometry(points);
                case SupportedGeometryType.CURVEPOLYGON:
                    return ToCircle(points);
                case SupportedGeometryType.POLYGON:
                    return ToPolygon(points, innerRings);
                default:
                    throw new ArgumentException("Unexpected geometry type " + GeometryType.ToString());
            }

            /*
            SqlGeometry obj = SqlGeometry.STGeomFromText(ToGeometryString(GeometryType, points).ToSqlChars(), 0);
            if (obj.STIsValid().IsFalse)
            {
                throw new ArgumentException(obj.IsValidDetailed());
            }
            return obj;
            */
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
        public static SqlGeometry Scale(this SqlGeometry geometry, UnitsAndScale.IScale scale)
        {
            if (geometry.HasInteriorRings())
            {
                return ScaleShapeWithInnerRings(geometry, scale);
            }
            else
            {
                GridVector2[] points = geometry.ToPoints();
                GridVector2[] scaled_p = [.. points.Select(p => new GridVector2(p.X * scale.X.Value, p.Y * scale.Y.Value))];
                return ToGeometry(geometry.GeometryType(), scaled_p);
            }

        }

        private static SqlGeometry ScaleShapeWithInnerRings(this SqlGeometry geometry, UnitsAndScale.IScale scale)
        {
            System.Diagnostics.Debug.Assert(geometry.GeometryType() == SupportedGeometryType.POLYGON);

            int NumInteriorRings = geometry.NumInteriorRings();
            List<GridVector2[]> InteriorRings = new(NumInteriorRings);
            GridVector2[] ExteriorRing = geometry.ToPoints();

            GridVector2[] ScaledExteriorRing = [.. ExteriorRing.Select(p => new GridVector2(p.X * scale.X.Value, p.Y * scale.Y.Value))];

            for (int iRing = 0; iRing < NumInteriorRings; iRing++)
            {
                GridVector2[] InteriorRing = geometry.GetInteriorRing(iRing).ToPoints();
                GridVector2[] ScaledInteriorRing = [.. InteriorRing.Select(p => new GridVector2(p.X * scale.X.Value, p.Y * scale.Y.Value))];
                InteriorRings.Add(ScaledInteriorRing);
            }

            return ToGeometry(geometry.GeometryType(), ScaledExteriorRing, InteriorRings);
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

            if (geometry.HasInteriorRings())
            {
                return TranslateShapeWithInnerRings(geometry, offset);
            }
            else
            {
                return TranslateShapeWithoutInnerRings(geometry, offset);
            }
        }

        private static SqlGeometry TranslateShapeWithInnerRings(SqlGeometry geometry, GridVector2 offset)
        {
            System.Diagnostics.Debug.Assert(geometry.GeometryType() == SupportedGeometryType.POLYGON);

            int NumInteriorRings = geometry.NumInteriorRings();
            List<GridVector2[]> InteriorRings = new(NumInteriorRings);
            GridVector2[] ExteriorRing = geometry.ToPoints();

            GridVector2[] TranslatedExteriorRing = [.. ExteriorRing.Translate(offset)];

            for (int iRing = 0; iRing < NumInteriorRings; iRing++)
            {
                GridVector2[] InteriorRing = geometry.GetInteriorRing(iRing).ToPoints();
                GridVector2[] TranslatedInteriorRing = [.. InteriorRing.Translate(offset)];
                InteriorRings.Add(TranslatedInteriorRing);
            }

            return ToGeometry(geometry.GeometryType(), TranslatedExteriorRing, InteriorRings);
        }

        private static SqlGeometry TranslateShapeWithoutInnerRings(SqlGeometry geometry, GridVector2 offset)
        {
            GridVector2[] translated_points = [.. geometry.ToPoints().Select(p => p + offset)];

            return ToGeometry(geometry.GeometryType(), translated_points);
        }

        public static GridCircle CalculateInscribedCircle(this SqlGeometry shape)
        {
            GridVector2[] ControlPoints = shape.ToPoints();
            return shape.CalculateInscribedCircle(ControlPoints);
        }

        /// <summary>
        /// Determines the centroid of the shape to find center of circle and nearest point to centroid to determine radius
        /// </summary>
        /// <param name="shape"></param>
        /// <param name="ControlPoints"></param>
        /// <returns></returns>
        public static GridCircle CalculateInscribedCircle(this SqlGeometry shape, ICollection<GridVector2> ControlPoints)
        {
            GridVector2 center = shape.Centroid();
            double Radius = ControlPoints.Select(p => GridVector2.Distance(center, p)).Min();
            return new GridCircle(center, Radius);
        }

        public static SqlGeometry AddInteriorPolygon(this SqlGeometry shape, GridVector2[] NewInteriorRing)
        {
            List<GridVector2[]> inner_rings = shape.InteriorRingPoints();
            inner_rings.Add(NewInteriorRing);

            GridVector2[] exteriorRing = shape.ToPoints();

            return exteriorRing.ToPolygon(inner_rings.AsReadOnly());
        }
    }
}
