using System.Collections.Generic;
using Geometry;
using gRPCAnnotationService.Protos;
using NetTopologySuite.IO;
using Viking.AnnotationServiceTypes.gRPC.V1;
using Viking.AnnotationServiceTypes.Interfaces;
using Xunit;
using ProtoLocation = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Every non-circle annotation type from the client's WKT, over the wire to the database geometry and back: what
    /// the server stores equals what it stored when the client sent WKT, and the client gets its shape and type back.
    /// Circles are covered by <see cref="LocationCircleConversionTests"/>.
    /// </summary>
    public class ServerShapeRoundTripTests
    {
        public static IEnumerable<object[]> Cases() =>
        [
            [LocationType.POINT, "POINT (10.25 20.5)", "POINT (10.25 20.5)", null],
            [LocationType.POLYLINE, "LINESTRING (0 0, 5 5.5, 10 0)", "LINESTRING (0 0, 5 5, 10 0)", 4.5],
            [LocationType.OPENCURVE, "LINESTRING (0 0, 5 5.5, 10 0, 12 3)", "LINESTRING (0 0, 1 1, 2 1.5, 5 5, 10 0, 12 3)", 16.0],
            [LocationType.CLOSEDCURVE, "LINESTRING (0 0, 5 5.5, 10 0, 0 0)", "LINESTRING (0 0, 1 1, 5 5, 9 1, 10 0, 0 0)", 8.0],
            [LocationType.POLYGON, "POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0), (2 2, 4 2, 4 4, 2 4, 2 2))", "POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0), (2 2, 4 2, 4 4, 2 4, 2 2))", null],
            [LocationType.CURVEPOLYGON, "POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0), (2 2, 4 2, 4 4, 2 4, 2 2))", "POLYGON ((0 0, 5 -1, 10 0, 10 10, 0 10, 0 0))", null],
        ];

        [Theory]
        [MemberData(nameof(Cases))]
        public void ClientShape_ToDatabase_AndBack(LocationType type, string mosaicWkt, string volumeWkt, double? width)
        {
            var proto = new ProtoLocation
            {
                Section = 3,
                MosaicGeometry = TestShapes.Mosaic(mosaicWkt, type, width),
                VolumeGeometry = TestShapes.Volume(volumeWkt, type, width),
            };

            var entity = proto.ToLocation();

            // What the server stored when the client sent WKT: the same geometry.
            var reader = new WKTReader();
            Assert.True(reader.Read(mosaicWkt).EqualsExact(entity.MosaicShape), entity.MosaicShape.ToText());
            Assert.True(reader.Read(volumeWkt).EqualsExact(entity.VolumeShape), entity.VolumeShape.ToText());
            Assert.Equal((short)type, entity.TypeCode);
            Assert.Equal(width, entity.Width);
            Assert.Equal(type == LocationType.CLOSEDCURVE, entity.Closed);

            entity.Username = string.Empty;
            var back = entity.ToProtobufMessage();

            Assert.Equal(proto.MosaicGeometry, back.MosaicGeometry);
            Assert.Equal(proto.VolumeGeometry, back.VolumeGeometry);
            Assert.Equal(type, back.MosaicGeometry.LocationTypeOf());
            Assert.Equal(mosaicWkt.ParseWKTForTest(), back.MosaicGeometry.ToShape2D().ToWKT());
        }
    }

    static class WktTestExtensions
    {
        /// <summary>The client's WKT for the shape the client parses from <paramref name="wkt"/>.</summary>
        public static string ParseWKTForTest(this string wkt) => global::Geometry.FromWKT.ParseWKT(wkt).ToWKT();
    }
}
