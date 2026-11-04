using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Viking.SegmentationPrompts.Geometry
{
    /// <summary>
    /// Reads a location's MosaicShape WKT (POLYGON or MULTIPOLYGON) into rings, as the training exporter does (nornir
    /// <c>segmentationtraining/wkt.py</c>): each polygon is its exterior then its holes, each ring closed (first point
    /// repeated last) as the WKT stores it.
    /// </summary>
    internal static class MosaicWkt
    {
        private static readonly Regex Coordinate =
            new Regex(@"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.CultureInvariant);

        /// <summary>The polygons of a POLYGON or MULTIPOLYGON WKT.</summary>
        /// <exception cref="FormatException">Another WKT type, unbalanced parentheses, or a ring under three points.</exception>
        public static List<List<List<Point>>> ParsePolygons(string wkt)
        {
            string stripped = (wkt ?? "").Trim();
            string upper = stripped.ToUpperInvariant();
            if (upper.StartsWith("MULTIPOLYGON", StringComparison.Ordinal))
            {
                // Each part keeps its own outer parentheses, ((exterior),(hole),...); strip them so its rings split
                // like a POLYGON body.
                return SplitTopLevel(ParenBody(stripped, "MULTIPOLYGON"))
                    .Select(part => ParsePolygonBody(part.Substring(1, part.Length - 2)))
                    .ToList();
            }
            if (upper.StartsWith("POLYGON", StringComparison.Ordinal))
                return new List<List<List<Point>>> { ParsePolygonBody(ParenBody(stripped, "POLYGON")) };
            throw new FormatException($"Unsupported WKT type: {Abbreviate(stripped)}");
        }

        private static string ParenBody(string wkt, string keyword)
        {
            int start = wkt.ToUpperInvariant().IndexOf(keyword, StringComparison.Ordinal);
            int open = wkt.IndexOf('(', start);
            if (open < 0)
                throw new FormatException("WKT missing opening parenthesis");
            int depth = 0;
            for (int i = open; i < wkt.Length; i++)
            {
                if (wkt[i] == '(')
                    depth++;
                else if (wkt[i] == ')' && --depth == 0)
                    return wkt.Substring(open + 1, i - open - 1);
            }
            throw new FormatException("WKT is not balanced");
        }

        private static List<string> SplitTopLevel(string body)
        {
            var parts = new List<string>();
            int depth = 0, start = 0;
            for (int i = 0; i < body.Length; i++)
            {
                if (body[i] == '(')
                {
                    if (depth == 0)
                        start = i;
                    depth++;
                }
                else if (body[i] == ')' && --depth == 0)
                {
                    parts.Add(body.Substring(start, i - start + 1));
                }
            }
            return parts;
        }

        private static List<List<Point>> ParsePolygonBody(string body)
        {
            List<string> rings = SplitTopLevel(body);
            if (rings.Count == 0)
                throw new FormatException("POLYGON has no rings");
            return rings.Select(ParseRing).ToList();
        }

        private static List<Point> ParseRing(string ring)
        {
            string inner = ring.Trim();
            if (inner.StartsWith("(", StringComparison.Ordinal) && inner.EndsWith(")", StringComparison.Ordinal))
                inner = inner.Substring(1, inner.Length - 2);
            double[] numbers = Coordinate.Matches(inner).Cast<Match>()
                .Select(match => double.Parse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture))
                .ToArray();
            if (numbers.Length < 6 || numbers.Length % 2 != 0)
                throw new FormatException("Ring does not contain coordinate pairs");
            var points = new List<Point>(numbers.Length / 2);
            for (int i = 0; i < numbers.Length; i += 2)
                points.Add(new Point(numbers[i], numbers[i + 1]));
            return points;
        }

        private static string Abbreviate(string text) => text.Length <= 40 ? text : text.Substring(0, 40);
    }
}
