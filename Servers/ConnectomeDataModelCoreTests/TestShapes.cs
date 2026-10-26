using Geometry;
using Viking.AnnotationServiceTypes.gRPC.V1;
using Viking.AnnotationServiceTypes.Interfaces;
using ProtoCoordinateSpace = Viking.gRPC.Geometry.V1.CoordinateSpace;
using ProtoShape = Viking.gRPC.Geometry.V1.Shape;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>Wire shapes for tests, written as WKT for readability.</summary>
    internal static class TestShapes
    {
        /// <summary>The wire shape of <paramref name="wkt"/> as a location of <paramref name="type"/> in mosaic space.</summary>
        public static ProtoShape Mosaic(string wkt, LocationType type = LocationType.POLYGON, double? width = null) =>
            wkt.ParseWKT().ToShape(type, ProtoCoordinateSpace.Mosaic, width);

        /// <summary>The wire shape of <paramref name="wkt"/> as a location of <paramref name="type"/> in volume space.</summary>
        public static ProtoShape Volume(string wkt, LocationType type = LocationType.POLYGON, double? width = null) =>
            wkt.ParseWKT().ToShape(type, ProtoCoordinateSpace.Volume, width);
    }
}
