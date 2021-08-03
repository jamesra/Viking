using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

[assembly:InternalsVisibleTo("GeometryOGCMapperTest")]

namespace Geometry
{
    public static class WKT
    {
        static readonly string float_pattern = @"-?[0-9]*(?:\.[0-9]*)?";
        static readonly string single_coord_pattern = @"\s*(?<X>" + float_pattern + @"){1}\s+(?<Y>" + float_pattern + @"){1}\s*";

        private static readonly Regex single_coord_regex = new Regex(@"\A" + single_coord_pattern + @"\Z", RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);

        static readonly string coord_list_pattern = @"(?:(?<coords>" + single_coord_pattern + @"),)*" +
                                                     @"(?<coords>" + single_coord_pattern + @"){1}";

        private static readonly Regex coord_list_regex =
            new Regex(coord_list_pattern, RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);

        /// <summary>
        /// A matched set of parenthesis
        /// </summary>
        private static readonly string matched_parenthesis_pattern = 
            @"(?:
               \(                           #First '('
                    (?<matched_parenthesis>
                        (?:
                            [^()]           #Match all non-braces
                            |
                            (?<open> \( )   #Match '(' and capture into 'open'
                            |
                            (?<-open> \) )  #Match ')' and delete the 'open' capture
                        )+
                        (?(open)(?!))   #Fails if the 'open' stack isn't empty
                    )
                \)                  # Last ')' 
            )*";

        /// <summary>
        /// A comma delimited list of matched parenthesis
        /// </summary>
        private static readonly string parenthesis_list_pattern = @"(?:\s*" + matched_parenthesis_pattern + @"\s*,)*" +
                                                                  @"(?:\s*" + matched_parenthesis_pattern + @"){1}\s*";


        private static readonly Regex parenthesis_list_regex = new Regex(parenthesis_list_pattern, RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);

        private static readonly string WKT_pattern = @"\s*(?<type>[\p{Ll}\p{Lu}\p{Lt}]+)\s*" +
                                                     matched_parenthesis_pattern +
                                                     @"\s*\Z";

        private static readonly Regex WKT_regex =
            new Regex(WKT_pattern, RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);


        public static IShape2D ParseWKT(this string input)
        {
            if (input == null)
                throw new FormatException("Input WKT must not be null");
               
            var m = WKT_regex.Match(input);
            if (false == m.Success)
                throw new FormatException($"Unable to parse WKT {input}");

            string coords = m.Groups["matched_parenthesis"].Value;

            switch (m.Groups["type"].Value.ToUpper())
            {
                case "POINT":
                    return ParsePointParameters(coords); 
                case "LINESTRING":
                    return ParsePolylineParameters(coords);
                case "POLYGON":
                    return ParsePolygonParameters(coords);
                case "CURVEPOLYGON":
                    return ParseCurvePolygonParameters(coords);
                default:
                    throw new FormatException($"Unable to parse WKT {input}");
            }

            throw new FormatException($"Unable to parse WKT {input}"); ;
        }

        internal static GridVector2 ParsePointParameters(string coords)
        { 
            var m = single_coord_regex.Match(coords );
            if (m.Success == false)
                throw new FormatException($"Cannot parse WKT to point {coords}");

            double X = System.Convert.ToDouble(m.Groups["X"].Value);
            double Y = System.Convert.ToDouble(m.Groups["Y"].Value);
            return new GridVector2(X, Y);
        }

        internal static List<GridVector2> ParsePointsFromParameters(string coords)
        { 
            var m = coord_list_regex.Match(coords);

            if (m.Success == false)
                throw new FormatException($"Cannot parse WKT to point {coords}");

            List<GridVector2> points = new List<GridVector2>(m.Groups["coords"].Captures.Count);
            foreach (var val in m.Groups["coords"].Captures)
            {
                var p = ParsePointParameters(val.ToString());
                points.Add(p);
            } 
             
            return points;
        }

        internal static string[] ParseParenListFromParameters(string input)
        {
            var m = parenthesis_list_regex.Match(input);
            if (m.Success == false)
                throw new FormatException($"Cannot parse WKT parenthesized parameters to a list {input}");

            var captures = m.Groups["matched_parenthesis"].Captures;
            string[] matchedParenthesis = new string[captures.Count];
            for (int i = 0; i < matchedParenthesis.Length; i++)
                matchedParenthesis[i] = captures[i].ToString(); 

            return matchedParenthesis;
        }

        internal static GridPolyline ParsePolylineParameters(string coords)
        {
            var points = ParsePointsFromParameters(coords);
            return new GridPolyline(points);
        }

        internal static GridPolygon ParsePolygonParameters(string coords)
        {
            var matchedParenthesis = ParseParenListFromParameters(coords);
            GridPolygon poly = null;

            foreach (var coordList in matchedParenthesis)
            {
                var p = ParsePointsFromParameters(coordList);
                if (poly == null)
                    poly = new GridPolygon(p);
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
        internal static GridCircle ParseCurvePolygonParameters(string coords)
        {
            var matchedParenthesis = ParseParenListFromParameters(coords); 

            foreach (var coordList in matchedParenthesis)
            {
                var p = ParsePointsFromParameters(coordList);
                var bRect = p.BoundingBox();
                return new GridCircle(bRect.Center, bRect.Width / 2.0);
            }

            return default;
        }
    }
}
