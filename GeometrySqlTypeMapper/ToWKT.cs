using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry
{
    public static class WKTEncoder
    {
        public static string ToWKT(this IShape2D shape)
        {
            if (shape is IPoint2D point)
                return ToWKT(point);
            else if (shape is ILineSegment2D line)
                return ToWKT(line);
            else if (shape is IPolyLine2D polyline)
                return ToWKT(polyline);
            else if (shape is ICircle2D circle)
                return ToWKT(circle);
            else if (shape is IPolygon2D poly)
                return ToWKT(poly);

            throw new ArgumentException($"Unexpected shape {shape} cannot be converted to WKT");
        }

        public static string ToWKT(IPoint2D point)
        {
            return $"Point ({point.X} {point.Y})";
        }

        public static string ToWKT(ILineSegment2D line)
        {
            return $"LINESTRING ({line.A.X} {line.A.Y}, {line.B.X} {line.B.Y})";
        }

        public static string ToWKT(IPolyLine2D line)
        {
            return $"LINESTRING {ToSqlCoordinateList(line.Points, false)}";
        }

        public static string ToWKT(ICircle2D circle)
        {
            return $"CURVEPOLYGON ({CircleExtensions.ScaleAndTranslateCircle(CircleExtensions.circleFourCardinalPoints, circle.Center.X, circle.Center.Y, circle.Radius).ToSqlCoordinateList(true)})";
        }

        public static string ToWKT(IPolygon2D poly)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("POLYGON (");
            sb.Append(poly.ExteriorRing.ToSqlCoordinateList(true));
            foreach (var innerPoly in poly.InteriorRings)
            {
                sb.Append(',');
                sb.Append(innerPoly.ToSqlCoordinateList(true));
            }

            sb.Append(")");
            return sb.ToString();
        }

        public static string ToSqlCoordinateList(this GridVector2[] points, bool closed = false)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("(");
            for (int i = 0; i < points.Length; i++)
            {
                if (i != 0)
                    sb.AppendFormat(", ");

                sb.Append($"{points[i].X:F2} {points[i].Y:F2}");
            }

            if (closed && points[0] != points[points.Length - 1])
                sb.Append($", {points[0].X:F2} {points[0].Y:F2}");

            sb.Append(")");

            return sb.ToString();
        }

        public static string ToSqlCoordinateList(this IEnumerable<IPoint2D> points, bool closed = false)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("(");
            bool needComma = false;
            IPoint2D first = null;
            IPoint2D last = null;

            foreach (var p in points)
            {
                last = p;
                if (needComma)
                {
                    sb.AppendFormat(", ");
                }
                else
                {
                    needComma = true;
                    first = p;
                }

                sb.Append($"{p.X:F2} {p.Y:F2}");
            }

            if (first == null)
                throw new ArgumentException("points parameter must not be empty");

            if (closed && first.Equals(last) == false)
                sb.Append($", {first.X:F2} {first.Y:F2}");

            sb.Append(")");

            return sb.ToString();
        }
    }

    public static class CircleExtensions
    {
        const double tau = Math.PI * 2.0;
        /// <summary>
        /// A unit circle with points along the East, NorthEast, North, compass points...
        /// </summary>
        public static readonly GridVector2[] circleEightCardinalPoints = new GridVector2[]
        {
            new GridVector2(1, 0),
            new GridVector2(Math.Cos(1.0/8.0 * tau), Math.Sin(1.0/8.0 * tau)),
            new GridVector2(0, 1),
            new GridVector2(Math.Cos(3.0/8.0 * tau), Math.Sin(3.0/8.0 * tau)),
            new GridVector2(-1, 0),
            new GridVector2(Math.Cos(5.0/8.0 * tau), Math.Sin(5.0/8.0 * tau)),
            new GridVector2(0, -1),
            new GridVector2(Math.Cos(7.0/8.0 * tau), Math.Sin(7.0/8.0 * tau)),
        };

        /// <summary>
        /// A unit circle with points on the East, North, West, South 
        /// </summary>
        public static readonly GridVector2[] circleFourCardinalPoints = new GridVector2[]
        {
            new GridVector2(1, 0),
            new GridVector2(0, 1),
            new GridVector2(-1, 0),
            new GridVector2(0, -1)
        };

        /// <summary>
        /// 
        /// </summary>
        /// <param name="nPoints"></param>
        /// <returns>A circle with N points spaced evenly around the perimeter starting with (1x, 0y)</returns>
        public static GridVector2[] CalculateCircleWithNPoints(int nPoints)
        {
            //Place points around the circle
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="circleTemplate">Points along the unit circle</param>
        /// <param name="X">Center of desired circle</param>
        /// <param name="Y">Center of desired circle</param>
        /// <param name="Radius">Radius of desired circle</param>
        /// <returns></returns>
        internal static GridVector2[] ScaleAndTranslateCircle(GridVector2[] circleTemplate, double X, double Y, double Radius)
        {
            GridVector2[] points = new GridVector2[circleTemplate.Length];
            circleTemplate.CopyTo(points, 0);

            for (int i = 0; i < points.Length; i++)
            {
                points[i].X *= Radius;
                points[i].Y *= Radius;
                points[i].X += X;
                points[i].Y += Y;
            }

            return points;
        }
    }
}