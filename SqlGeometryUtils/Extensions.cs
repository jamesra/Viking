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
        public static Polygon ToPolygon(this SqlGeometry shape)
        {
            if (shape.GeometryType() != SupportedGeometryType.POLYGON && shape.GeometryType() != SupportedGeometryType.CURVEPOLYGON)
                throw new ArgumentException("SqlGeometry must be a polygon type");

            Vector2[] ExteriorRing = shape.ToPoints();
            ICollection<Vector2[]> InteriorRings = shape.InteriorRingPoints();

            try
            {
                return new Polygon(ExteriorRing, InteriorRings);
            }
            catch (ArgumentException e)
            {
                return new Polygon([.. ExteriorRing.RemoveAdjacentDuplicates()], InteriorRings.Select(ir => ir.RemoveAdjacentDuplicates().ToArray()));
            }
        }

        public static SqlGeometry ToSqlGeometry(this Polygon shape) => shape.ExteriorRing.ToPolygon([.. shape.InteriorRings]);

        public static Polyline ToPolyLine(this SqlGeometry shape)
        {
            if (shape.GeometryType() != SupportedGeometryType.POLYLINE)
                throw new ArgumentException("SqlGeometry must be a polygon type");

            Vector2[] points = shape.ToPoints();
            return new Polyline(points.Cast<IPoint2D>());
        }

        public static Circle ToCircle(this SqlGeometry shape)
        {
            var current_type = shape.GeometryType();
            if (current_type != SupportedGeometryType.CURVEPOLYGON && current_type != SupportedGeometryType.POLYLINE)
                throw new ArgumentException("SqlGeometry must be a polygon type");

            Rectangle bbox = shape.BoundingBox();
            //System.Diagnostics.Debug.Assert(Math.Floor(bbox.Width) == Math.Floor(bbox.Height)); //Make sure our optimization is really getting a circle
            return new Circle(bbox.Center, Math.Max(bbox.Width, bbox.Height) / 2.0);
        }


        public static IShape2D ToShape2D(this SqlGeometry shape)
        {
            return shape.GeometryType() switch
            {
                SupportedGeometryType.POINT => new Vector2(shape.STX.Value, shape.STY.Value),
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
        private static readonly Vector2[] circleCardinalPoints;

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


        public static Microsoft.SqlServer.Types.SqlGeometry ToSqlGeometry(this Vector2 p)
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
        public static Vector2 Centroid(this System.Data.Entity.Spatial.DbGeometry geometry)
        {
            System.Data.Entity.Spatial.DbGeometry centroid = geometry.Centroid;
            if (centroid != null)
                return new Vector2(centroid.XCoordinate.Value, centroid.YCoordinate.Value);
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


        public static SqlGeometry ToSqlGeometry(this Circle circle, double Z = 0)
        {
            return ToCircle(circle.Center.X,
                            circle.Center.Y,
                            Z,
                            circle.Radius);
        }

        public static byte[] AsBinary(this SqlGeometry geom) => geom.STAsBinary().Value;

        public static SqlGeometry ToSqlGeometry(this LineSegment line) => new Vector2[] { line.A, line.B }.ToSqlGeometry();

        /// <summary>
        /// Create a linestring from a polyline
        /// </summary>
        /// <param name="polyline"></param>
        /// <param name="Z"></param>
        /// <returns></returns>
        public static SqlGeometry ToSqlGeometry(this Polyline polyline) => ToSqlGeometry(polyline.Points);

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
        public static SqlGeometry ToSqlGeometry(this IReadOnlyList<Vector2> points)
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
            return shape switch
            {
                Polygon polygon => polygon.ToSqlGeometry(),
                Polyline polyline => polyline.ToSqlGeometry(),
                Circle circle => circle.ToSqlGeometry(),
                LineSegment line => line.ToSqlGeometry(),
                ILineSegment2D segment => ToSqlGeometry(new[] { segment.A, segment.B }),
                IPolyLine2D poly => ToSqlGeometry(poly.Points),
                _ => throw new NotImplementedException($"Missing ToSqlGeometry implementation for {shape.ShapeType}")
            };
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

        public static SqlGeometry ToPolygon(this Vector2[] points, ICollection<Vector2[]> InteriorRings = null)
        {
            SqlGeometryBuilder builder = new();
            builder.SetSrid(0);
            builder.BeginGeometry(OpenGisGeometryType.Polygon);

            builder.AddPolygon(points);

            if (InteriorRings != null)
            {
                //Add the interior rings
                foreach (Vector2[] innerRing in InteriorRings)
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
        /// Add the Vector2 points to the polygon builder. 
        /// BeginGeometry(OpenGisGeometryType.Polygon) should already have been called
        /// </summary>
        /// <param name="points"></param>
        private static void AddPolygon(this SqlGeometryBuilder builder, Vector2[] points)
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
                Vector2[] pointsAppended = new Vector2[points.Length + 1];
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

        public static SqlGeometry ToCircle(this Vector2[] points)
        {
            if (points.Length < 3)
            {
                throw new ArgumentException("Polygon must be created with three points or more");
            }

            if (points.AreClockwise())
                points = [.. points.AsEnumerable().Reverse()];

            if (points.First() != points.Last())
            {
                List<Vector2> listPoints = [.. points, points[0]];
                points = [.. listPoints];
            }

            return points.ToPolygon().CalculateInscribedCircle(points).ToSqlGeometry(0);
        }

        /// <summary>
        /// Create a closed object where the first point in the array is added again at the end
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static string ToSqlClosedCoordinateList(this Vector2[] points) => points.ToSqlCoordinateList(true);

        public static string ToSqlCoordinateList(this Vector2[] points, bool closed = false)
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



        private static Vector2[] CalculateCircleCardinalPoints(int nPoints)
        {
            //Place points around the circle
            const double tau = Math.PI * 2.0;
            Vector2[] points = new Vector2[nPoints + 1];
            for (int i = 0; i < nPoints; i++)
            {
                double fraction = (double)i / nPoints;
                double angle = fraction * tau;
                points[i] = new Vector2(Math.Cos(angle), Math.Sin(angle));
            }

            points[nPoints] = points[0];

            return points;
        }

        private static Vector2[] ScaleAndTranslateCircleCardinalPoints(double X, double Y, double Radius)
        {
            Vector2[] points = new Vector2[circleCardinalPoints.Length];
            circleCardinalPoints.CopyTo(points, 0);

            for (int i = 0; i < points.Length; i++)
            {
                Vector2 p = points[i];
                points[i] = new Vector2((p.X * Radius) + X, (p.Y * Radius) + Y);
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


        public static SqlGeometry ToCurvePolygon(this Vector2[] points)
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

        public static Rectangle BoundingBox(this SqlGeometry geometry) => Rectangle.GetBoundingBox(geometry.STEnvelope().ToPoints());

#if NET48
        public static Rectangle BoundingBox(this System.Data.Entity.Spatial.DbGeometry geometry)
        {
            System.Data.Entity.Spatial.DbGeometry envelope = geometry.Envelope;
            return Rectangle.GetBoundingBox(envelope.ToPoints());
        }
#endif

        public static bool Intersects(this SqlGeometry geometry, Vector2 point)
        {
            SqlGeometry p = point.ToSqlGeometry();
            bool intersects = geometry.STIntersects(p).IsTrue;
            return intersects;
            //return geometry.STIntersects(point.ToGeometryPoint()).IsTrue;
        }

        public static bool Intersects(this SqlGeometry geometry, LineSegment line)
        {
            SqlGeometry p = line.ToSqlGeometry();
            bool intersects = geometry.STIntersects(p).IsTrue;
            return intersects;
            //return geometry.STIntersects(point.ToGeometryPoint()).IsTrue;
        }

        public static double Distance(this SqlGeometry geometry, Vector2 point) => geometry.STDistance(point.ToSqlGeometry()).Value;

#if NET48
        /// <summary>
        /// Return the points for the geometry, if it is a polygon return the rings around the exterior
        /// </summary>
        /// <param name="geometry"></param>
        /// <returns></returns>
        public static Vector2[] ToPoints(this System.Data.Entity.Spatial.DbGeometry geometry)
        {
            if (!geometry.PointCount.HasValue)
                return [];

            if (!geometry.InteriorRingCount.HasValue)
            {
                Vector2[] points = new Vector2[geometry.PointCount.Value];
                for (int i = 0; i < points.Length; i++)
                {
                    System.Data.Entity.Spatial.DbGeometry point = geometry.PointAt(i + 1);
                    points[i] = new Vector2(point.XCoordinate.Value, point.YCoordinate.Value);
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
        public static Vector2[] ToPoints(this Microsoft.SqlServer.Types.SqlGeometry geometry)
        {
            if (!geometry.HasInteriorRings())
            {
                SupportedGeometryType type = geometry.GeometryType();

                if (type != SupportedGeometryType.CURVEPOLYGON)
                {
                    Vector2[] points = new Vector2[geometry.STNumPoints().Value];
                    for (int i = 0; i < points.Length; i++)
                    {
                        SqlGeometry point = geometry.GetPoint(i);
                        points[i] = new Vector2(point.STX.Value, point.STY.Value);
                    }

                    return points;
                }
                else if (type == SupportedGeometryType.CURVEPOLYGON)
                {
                    Vector2[] points = new Vector2[nCircleCardinalPoints];
                    Circle circle = geometry.ToCircle();

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
        public static List<Vector2[]> InteriorRingPoints(this Microsoft.SqlServer.Types.SqlGeometry geometry)
        {
            if (!geometry.HasInteriorRings())
            {
                return [];
            }

            List<Vector2[]> innerRings = new(geometry.NumInteriorRings());
            for (int iRing = 0; iRing < geometry.NumInteriorRings(); iRing++)
            {
                SqlGeometry innerRing = geometry.GetInteriorRing(iRing);
                innerRings.Add(innerRing.ToPoints());
            }

            return innerRings;
        }

        public static Vector2 Centroid(this Microsoft.SqlServer.Types.SqlGeometry geometry)
        {
            SqlGeometry center = geometry.STCentroid();
            if (!center.IsNull)
                return new Vector2(System.Math.Round(center.STX.Value, RoundingDigits),
                                       System.Math.Round(center.STY.Value, RoundingDigits));

            if (center.STNumPoints() == 1)
                return new Vector2(System.Math.Round(geometry.STX.Value, RoundingDigits),
                                       System.Math.Round(geometry.STY.Value, RoundingDigits));

            return geometry.STEnvelope().Centroid();
        }

        public static string ToGeometryString(SqlString GeometryType, Vector2[] points)
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

        public static SqlGeometry ToGeometry(SupportedGeometryType GeometryType, Vector2[] points, ICollection<Vector2[]> innerRings = null)
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
        public static SqlGeometry MoveTo(this SqlGeometry geometry, Vector2 offset)
        {
            Vector2 center = geometry.Centroid();
            return geometry.Translate(offset - center);
            //return SqlGeometry.STGeomFromText(TranslateString(geometry, offset - center).ToSqlChars(), geometry.STSrid.Value);
        }

        /// <summary>
        /// Move the shape so its centroid is at the given absolute position.
        /// Implemented by round-tripping through SqlGeometry so all shape types (including circles/curves) are handled consistently.
        /// </summary>
        /// <param name="shape"></param>
        /// <param name="newPosition"></param>
        /// <returns></returns>
        public static IShape2D MoveTo(this IShape2D shape, Vector2 newPosition) => shape.ToSqlGeometry().MoveTo(newPosition).ToShape2D();

        /// <summary>
        /// The following IShape2D overloads round-trip through SqlGeometry so the large amount of legacy UI code
        /// written against SqlGeometry (before LocationObj.MosaicShape/VolumeShape became IShape2D) keeps working
        /// with minimal call site changes.
        /// </summary>
        public static Polygon ToPolygon(this IShape2D shape) => shape.ToSqlGeometry().ToPolygon();

        public static Vector2[] ToPoints(this IShape2D shape) =>
            shape is IHasControlPoints cps
                ? [.. cps.ControlPoints.Select(p => p.ToVector2())]
                : shape.ToSqlGeometry().ToPoints();

        public static Vector2 Centroid(this IShape2D shape) => shape.ToSqlGeometry().Centroid();

        public static Circle CalculateInscribedCircle(this IShape2D shape) => shape.ToSqlGeometry().CalculateInscribedCircle();

        public static Circle CalculateInscribedCircle(this IShape2D shape, ICollection<Vector2> ControlPoints) => shape.ToSqlGeometry().CalculateInscribedCircle(ControlPoints);

        public static SqlGeometry AddInteriorPolygon(this IShape2D shape, Vector2[] NewInteriorRing) => shape.ToSqlGeometry().AddInteriorPolygon(NewInteriorRing);

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
                Vector2[] points = geometry.ToPoints();
                Vector2[] scaled_p = [.. points.Select(p => new Vector2(p.X * scale.X.Value, p.Y * scale.Y.Value))];
                return ToGeometry(geometry.GeometryType(), scaled_p);
            }

        }

        private static SqlGeometry ScaleShapeWithInnerRings(this SqlGeometry geometry, UnitsAndScale.IScale scale)
        {
            System.Diagnostics.Debug.Assert(geometry.GeometryType() == SupportedGeometryType.POLYGON);

            int NumInteriorRings = geometry.NumInteriorRings();
            List<Vector2[]> InteriorRings = new(NumInteriorRings);
            Vector2[] ExteriorRing = geometry.ToPoints();

            Vector2[] ScaledExteriorRing = [.. ExteriorRing.Select(p => new Vector2(p.X * scale.X.Value, p.Y * scale.Y.Value))];

            for (int iRing = 0; iRing < NumInteriorRings; iRing++)
            {
                Vector2[] InteriorRing = geometry.GetInteriorRing(iRing).ToPoints();
                Vector2[] ScaledInteriorRing = [.. InteriorRing.Select(p => new Vector2(p.X * scale.X.Value, p.Y * scale.Y.Value))];
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
        public static SqlGeometry Translate(this SqlGeometry geometry, Vector2 offset)
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

        private static SqlGeometry TranslateShapeWithInnerRings(SqlGeometry geometry, Vector2 offset)
        {
            System.Diagnostics.Debug.Assert(geometry.GeometryType() == SupportedGeometryType.POLYGON);

            int NumInteriorRings = geometry.NumInteriorRings();
            List<Vector2[]> InteriorRings = new(NumInteriorRings);
            Vector2[] ExteriorRing = geometry.ToPoints();

            Vector2[] TranslatedExteriorRing = [.. ExteriorRing.Translate(offset)];

            for (int iRing = 0; iRing < NumInteriorRings; iRing++)
            {
                Vector2[] InteriorRing = geometry.GetInteriorRing(iRing).ToPoints();
                Vector2[] TranslatedInteriorRing = [.. InteriorRing.Translate(offset)];
                InteriorRings.Add(TranslatedInteriorRing);
            }

            return ToGeometry(geometry.GeometryType(), TranslatedExteriorRing, InteriorRings);
        }

        private static SqlGeometry TranslateShapeWithoutInnerRings(SqlGeometry geometry, Vector2 offset)
        {
            Vector2[] translated_points = [.. geometry.ToPoints().Select(p => p + offset)];

            return ToGeometry(geometry.GeometryType(), translated_points);
        }

        public static Circle CalculateInscribedCircle(this SqlGeometry shape)
        {
            Vector2[] ControlPoints = shape.ToPoints();
            return shape.CalculateInscribedCircle(ControlPoints);
        }

        /// <summary>
        /// Determines the centroid of the shape to find center of circle and nearest point to centroid to determine radius
        /// </summary>
        /// <param name="shape"></param>
        /// <param name="ControlPoints"></param>
        /// <returns></returns>
        public static Circle CalculateInscribedCircle(this SqlGeometry shape, ICollection<Vector2> ControlPoints)
        {
            Vector2 center = shape.Centroid();
            double Radius = ControlPoints.Select(p => Vector2.Distance(center, p)).Min();
            return new Circle(center, Radius);
        }

        public static SqlGeometry AddInteriorPolygon(this SqlGeometry shape, Vector2[] NewInteriorRing)
        {
            List<Vector2[]> inner_rings = shape.InteriorRingPoints();
            inner_rings.Add(NewInteriorRing);

            Vector2[] exteriorRing = shape.ToPoints();

            return exteriorRing.ToPolygon(inner_rings.AsReadOnly());
        }
    }
}
