using System;
using Google.Protobuf.WellKnownTypes;
using gRPCAnnotationService.Protos;
using NetTopologySuite.Geometries;
using Viking.AnnotationServiceTypes.gRPC.V1;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Xunit;
using EfLocation = Viking.DataModel.Annotation.Location;
using EfLocationLink = Viking.DataModel.Annotation.LocationLink;
using ProtoGeometry = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry;
using ProtoLocation = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins the non-circle Location proto-to-EF conversions used on every location save and fetch: shape
    /// fallbacks to the position fields, blank attributes, nullable width, timestamps and link direction.
    /// Circle behavior is covered by <see cref="LocationCircleConversionTests"/>.
    /// </summary>
    public class LocationConversionTests
    {
        private static ProtoLocation PolygonProto() => new ProtoLocation
        {
            Id = 7,
            ParentId = 3,
            Section = 12,
            TypeCode = AnnotationType.Polygon,
            MosaicPosition = new AnnotationPoint { X = 1.5, Y = 2.5 },
            VolumePosition = new AnnotationPoint { X = 100.25, Y = 200.75 },
            Username = "u",
        };

        [Fact]
        public void ToLocation_WithoutShapes_FallsBackToPositionPoints()
        {
            var entity = PolygonProto().ToLocation();

            Assert.Equal(1.5, entity.MosaicShape.Coordinate.X);
            Assert.Equal(2.5, entity.MosaicShape.Coordinate.Y);
            Assert.Equal(100.25, entity.VolumeShape.Coordinate.X);
            Assert.Equal(200.75, entity.VolumeShape.Coordinate.Y);
        }

        [Fact]
        public void ToLocation_WithoutVolumeShapeOrPosition_UsesMosaicShapeForVolume()
        {
            var proto = PolygonProto();
            proto.VolumePosition = null;

            var entity = proto.ToLocation();

            Assert.Equal(1.5, entity.VolumeShape.Coordinate.X);
            Assert.Equal(2.5, entity.VolumeShape.Coordinate.Y);
        }

        [Fact]
        public void ToLocation_WktShapeWinsOverPosition()
        {
            var proto = PolygonProto();
            proto.MosaicShape = new ProtoGeometry { Text = "POINT (9 8)" };

            var entity = proto.ToLocation();

            Assert.Equal(9, entity.MosaicShape.Coordinate.X);
            Assert.Equal(8, entity.MosaicShape.Coordinate.Y);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ToLocation_BlankAttributesBecomeNull(string attributes)
        {
            var proto = PolygonProto();
            proto.Attributes = attributes;

            Assert.Null(proto.ToLocation().Tags);
        }

        [Fact]
        public void ToLocation_CopiesScalarFields()
        {
            var proto = PolygonProto();
            proto.Attributes = "<Attributes />";
            proto.Closed = true;
            proto.Terminal = true;
            proto.OffEdge = true;
            proto.Width = 4.5;

            var entity = proto.ToLocation();

            Assert.Equal(7L, entity.Id);
            Assert.Equal(3L, entity.ParentId);
            Assert.Equal(12L, entity.Z);
            Assert.Equal((short)AnnotationType.Polygon, entity.TypeCode);
            Assert.Equal("<Attributes />", entity.Tags);
            Assert.True(entity.Closed);
            Assert.True(entity.Terminal);
            Assert.True(entity.OffEdge);
            Assert.Equal(4.5, entity.Width);
            Assert.Equal("u", entity.Username);
        }

        [Fact]
        public void ToLocation_MissingLastModifiedIsDefault()
        {
            Assert.Equal(default(DateTime), PolygonProto().ToLocation().LastModified);
        }

        [Fact]
        public void ToLocation_LastModifiedIsCarriedAsUtc()
        {
            var proto = PolygonProto();
            proto.LastModified = Timestamp.FromDateTime(new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc));

            var entity = proto.ToLocation();

            Assert.Equal(new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc), entity.LastModified);
            Assert.Equal(DateTimeKind.Utc, entity.LastModified.Kind);
        }

        private static EfLocation PolygonEntity() => new EfLocation
        {
            Id = 7,
            ParentId = 3,
            Z = 12,
            TypeCode = (short)AnnotationType.Polygon,
            X = 1.5,
            Y = 2.5,
            VolumeX = 100.25,
            VolumeY = 200.75,
            MosaicShape = new Point(1.5, 2.5),
            VolumeShape = new Point(100.25, 200.75),
            LastModified = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Unspecified),
            Username = "u",
        };

        [Fact]
        public void ToProtobufMessage_NullTagsBecomeEmptyAttributes()
        {
            var entity = PolygonEntity();
            entity.Tags = null;

            Assert.Equal(string.Empty, entity.ToProtobufMessage().Attributes);
        }

        [Fact]
        public void ToProtobufMessage_UnspecifiedLastModifiedIsTreatedAsUtc()
        {
            var proto = PolygonEntity().ToProtobufMessage();

            Assert.Equal(new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc), proto.LastModified.ToDateTime());
        }

        [Fact]
        public void ToProtobufMessage_CarriesPositionsAndScalars()
        {
            var entity = PolygonEntity();
            entity.Width = 4.5;
            entity.Closed = true;

            var proto = entity.ToProtobufMessage();

            Assert.Equal(7L, proto.Id);
            Assert.Equal(3L, proto.ParentId);
            Assert.Equal(12L, proto.Section);
            Assert.Equal(AnnotationType.Polygon, proto.TypeCode);
            Assert.Equal(1.5, proto.MosaicPosition.X);
            Assert.Equal(200.75, proto.VolumePosition.Y);
            Assert.Equal(4.5, proto.Width);
            Assert.True(proto.Closed);
        }

        [Fact]
        public void ToProtobufMessage_ShapesAreWkbThatReadBackAsTheSameGeometry()
        {
            var proto = PolygonEntity().ToProtobufMessage();

            Assert.Equal(ProtoGeometry.EncodingOneofCase.Binary, proto.MosaicShape.EncodingCase);
            var back = proto.VolumeShape.ToNetTopologyGeometry();
            Assert.Equal(100.25, back.Coordinate.X);
            Assert.Equal(200.75, back.Coordinate.Y);
        }

        [Fact]
        public void ToProtobufMessage_LinksListTheOtherEndOfEachLink()
        {
            var entity = PolygonEntity();
            entity.LocationLinkANavigations.Add(new EfLocationLink { A = 7, B = 20 });
            entity.LocationLinkBNavigations.Add(new EfLocationLink { A = 30, B = 7 });

            var proto = entity.ToProtobufMessage();

            Assert.Equal(new long[] { 20, 30 }, proto.Links);
        }
    }
}
