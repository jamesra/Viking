using System;
using System.Collections.Generic;
using System.Text;
using Geometry;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;

namespace Viking.AnnotationServiceTypes.gRPC.V1
{

    /// <summary>
    /// This is workaround for NetTopologySuite not supporting curved geometry
    /// </summary>
    public static class GeometryExtensions
    {
        public static string ToMosaicCircleWKT(this Location loc)
        { 
            return ToCircleWKT(loc.MosaicPosition.X, loc.MosaicPosition.Y, loc.Radius);
        }

        public static string ToVolumeCircleWKT(this Location loc)
        {
            return ToCircleWKT(loc.VolumePosition.X, loc.VolumePosition.Y, loc.Radius);
        }

        internal static string ToCircleWKT(in double X, in double Y, in double radius)
        {
            return
                $"CURVEPOLYGON (CIRCULARSTRING ({ToPointWKT(X - radius, Y)}, {ToPointWKT(X, Y + radius)}, {ToPointWKT(X + radius, Y)}, {ToPointWKT(X, Y - radius)}, {ToPointWKT(X - radius, Y)}))";
        }

        internal static string ToPointWKT(in double X, in double Y)
        {
            return $"({X} {Y}";
        }
    }
}
