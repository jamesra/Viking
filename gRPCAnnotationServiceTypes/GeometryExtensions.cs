using System;

namespace Viking.AnnotationServiceTypes.gRPC.V1
{

    /// <summary>
    /// This is workaround for NetTopologySuite not supporting curved geometry
    /// </summary>
    public static class GeometryExtensions
    {
        /// <summary>
        /// The SQL Server WKT Viking stores for a circle: a CURVEPOLYGON of one CIRCULARSTRING through the west, north,
        /// east and south rim points, starting and ending at the west point. Numbers use the invariant culture.
        /// </summary>
        public static string ToCircleWKT(in double X, in double Y, in double radius)
        {
            return FormattableString.Invariant(
                $"CURVEPOLYGON (CIRCULARSTRING ({X - radius} {Y}, {X} {Y + radius}, {X + radius} {Y}, {X} {Y - radius}, {X - radius} {Y}))");
        }
    }
}
