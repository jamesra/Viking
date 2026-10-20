using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("GeometryOGCMapperTest")]

namespace Geometry
{
    public static class FromWKT
    {
        public static IShape2D ToShape2D(this string input) => ParseWKT(input);

        /// <summary>
        /// Parses "TYPE (parameters)" for POINT, LINESTRING, MULTILINESTRING, POLYGON and CURVEPOLYGON (a circle).
        /// Scanned by hand rather than with regular expressions; the client parses a WKT ring for every location it loads.
        /// </summary>
        public static IShape2D ParseWKT(this string input)
        {
            if (input is null)
                throw new FormatException("Input WKT must not be null");

            int i = SkipWhitespace(input, 0, input.Length);
            int typeStart = i;
            while (i < input.Length && char.IsLetter(input[i]))
                i++;
            if (i == typeStart)
                throw new FormatException($"Unable to parse WKT {input}");

            string type = input.Substring(typeStart, i - typeStart).ToUpperInvariant();

            i = SkipWhitespace(input, i, input.Length);
            if (!TryReadParenthesized(input, ref i, input.Length, out string coords) ||
                SkipWhitespace(input, i, input.Length) != input.Length)
            {
                throw new FormatException($"Unable to parse WKT {input}");
            }

            return type switch
            {
                "POINT" => ParsePointParameters(coords),
                "LINESTRING" => ParsePolylineParameters(coords),
                "MULTILINESTRING" => ParseMultiPolylineParameters(coords),
                "POLYGON" => ParsePolygonParameters(coords),
                "CURVEPOLYGON" => ParseCurvePolygonParameters(coords),
                _ => throw new FormatException($"Unable to parse WKT {input}"),
            };
        }

        /// <summary>
        /// Reads a balanced "( ... )" group starting at <paramref name="i"/> and returns its non-empty inner text. Nested
        /// parentheses are kept in the inner text. Leaves <paramref name="i"/> after the closing parenthesis.
        /// </summary>
        private static bool TryReadParenthesized(string s, ref int i, int end, out string inner)
        {
            inner = null;
            if (i >= end || s[i] != '(')
                return false;

            int depth = 0;
            for (int j = i; j < end; j++)
            {
                if (s[j] == '(')
                {
                    depth++;
                }
                else if (s[j] == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        if (j == i + 1)
                            return false;

                        inner = s.Substring(i + 1, j - i - 1);
                        i = j + 1;
                        return true;
                    }
                }
            }

            return false;
        }

        internal static Vector2 ParsePointParameters(string coords)
        {
            if (coords is null)
                throw new FormatException("Cannot parse WKT to point: no coordinates");

            return ParsePoint(coords, 0, coords.Length);
        }

        /// <summary>
        /// Parses a comma separated list of "X Y" coordinates. A hand-written scanner rather than a regex: region loads
        /// parse thousands of rings, and matching every point with captures dominated the client's ingest cost.
        /// </summary>
        internal static List<Vector2> ParsePointsFromParameters(string coords)
        {
            if (coords is null)
                throw new FormatException("Cannot parse WKT coordinate list: no coordinates");

            int count = 1;
            foreach (char c in coords)
            {
                if (c == ',')
                    count++;
            }

            List<Vector2> points = new(count);
            int start = 0;
            while (true)
            {
                int comma = coords.IndexOf(',', start);
                int end = comma < 0 ? coords.Length : comma;
                points.Add(ParsePoint(coords, start, end));
                if (comma < 0)
                    break;

                start = comma + 1;
            }

            return points;
        }

        /// <summary>
        /// Parses exactly two numbers separated by whitespace from <paramref name="s"/>[<paramref name="start"/>,
        /// <paramref name="end"/>), with optional surrounding whitespace. Anything else is a <see cref="FormatException"/>.
        /// </summary>
        private static Vector2 ParsePoint(string s, int start, int end)
        {
            int i = SkipWhitespace(s, start, end);
            int xStart = i;
            i = SkipNonWhitespace(s, i, end);
            int xEnd = i;

            int afterX = i;
            i = SkipWhitespace(s, i, end);
            if (i == afterX)
                throw new FormatException($"Cannot parse WKT to point {s.Substring(start, end - start)}");

            int yStart = i;
            i = SkipNonWhitespace(s, i, end);
            int yEnd = i;

            if (SkipWhitespace(s, i, end) != end || xEnd == xStart || yEnd == yStart)
                throw new FormatException($"Cannot parse WKT to point {s.Substring(start, end - start)}");

            return new Vector2(ParseNumber(s, xStart, xEnd), ParseNumber(s, yStart, yEnd));
        }

        private static double ParseNumber(string s, int start, int end)
        {
            if (!double.TryParse(s.Substring(start, end - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw new FormatException($"Cannot parse WKT coordinate {s.Substring(start, end - start)}");

            return value;
        }

        private static int SkipWhitespace(string s, int i, int end)
        {
            while (i < end && char.IsWhiteSpace(s[i]))
                i++;
            return i;
        }

        private static int SkipNonWhitespace(string s, int i, int end)
        {
            while (i < end && !char.IsWhiteSpace(s[i]))
                i++;
            return i;
        }

        /// <summary>
        /// Splits "(a), (b), ..." into the inner text of each group. Whitespace around groups and commas is allowed.
        /// </summary>
        internal static string[] ParseParenListFromParameters(string input)
        {
            if (input is null)
                throw new FormatException("Cannot parse WKT parenthesized parameters: no input");

            List<string> groups = [];
            int i = SkipWhitespace(input, 0, input.Length);
            while (true)
            {
                if (!TryReadParenthesized(input, ref i, input.Length, out string inner))
                    throw new FormatException($"Cannot parse WKT parenthesized parameters to a list {input}");

                groups.Add(inner);
                i = SkipWhitespace(input, i, input.Length);
                if (i == input.Length)
                    return groups.ToArray();

                if (input[i] != ',')
                    throw new FormatException($"Cannot parse WKT parenthesized parameters to a list {input}");

                i = SkipWhitespace(input, i + 1, input.Length);
            }
        }

        internal static IShape2D ParsePolylineParameters(string coords)
        {
            var points = ParsePointsFromParameters(coords);
            if (points.Count == 2)
                return new LineSegment(points[0], points[1]);

            return new Polyline(points);
        }

        internal static IShape2D ParseMultiPolylineParameters(string coords)
        {
            var matchedParenthesis = ParseParenListFromParameters(coords);

            Shape2DCollection lineCollection = new(matchedParenthesis.Length);

            foreach (var coordList in matchedParenthesis)
            {
                var p = ParsePolylineParameters(coordList);
                lineCollection.Add(p);
            }

            return lineCollection;
        }

        internal static Polygon ParsePolygonParameters(string coords)
        {
            var matchedParenthesis = ParseParenListFromParameters(coords);
            Polygon poly = null;

            foreach (var coordList in matchedParenthesis)
            {
                var p = ParsePointsFromParameters(coordList);
                if (poly is null)
                    poly = new Polygon(p);
                else
                    poly.AddInteriorRing(p);
            }

            return poly;
        }

        /// <summary>
        /// This is a special case for the Viking database, which uses CurvePolygons to
        /// encode circles only.  Each CurvePolygon is expected to have 5 points, and the
        /// bounding box is used to define the circle
        /// </summary>
        /// <param name="coords"></param>
        /// <returns></returns>
        internal static Circle ParseCurvePolygonParameters(string coords)
        {
            var matchedParenthesis = ParseParenListFromParameters(coords);

            foreach (var coordList in matchedParenthesis)
            {
                var p = ParsePointsFromParameters(coordList);
                var bRect = p.BoundingBox();
                return new Circle(bRect.Center, bRect.Width / 2.0);
            }

            return default;
        }
    }
}
