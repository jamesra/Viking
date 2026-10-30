using System;
using Google.Protobuf.WellKnownTypes;
using gRPCAnnotationService.Protos;
using NetTopologySuite.Geometries;
using Viking.AnnotationServiceTypes.gRPC.V1;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Xunit;
using EfLocation = Viking.DataModel.Annotation.Location;
using EfLocationLink = Viking.DataModel.Annotation.LocationLink;
using Viking.AnnotationServiceTypes.Interfaces;
using ProtoShape = Viking.gRPC.Geometry.V1.Shape;
using ProtoVector2 = Viking.gRPC.Geometry.V1.Vector2;
using ProtoLocation = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins the non-circle Location proto-to-EF conversions used on every location save and fetch: a point's shape
    /// from its position fields, the refusal of a location sent without its volume shape, blank attributes, width and
    /// closed from the shape, timestamps and link direction.
    /// Circle behavior is covered by <see cref="LocationCircleConversionTests"/>.
    /// </summary>
    public class LocationConversionTests
    {
        private static ProtoLocation PolygonProto() => new ProtoLocation
        {
            Id = 7,
            ParentId = 3,
            Section = 12,
            MosaicPosition = new ProtoVector2 { X = 1.5, Y = 2.5 },
            VolumePosition = new ProtoVector2 { X = 100.25, Y = 200.75 },
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

        /// <summary>
        /// The volume shape is the client's cache of the mosaic shape under the volume's default transform; the server
        /// cannot derive it. A point without a volume shape or position used to store its mosaic position as its volume
        /// position (section coordinates in volume space); it is refused.
        /// </summary>
        [Fact]
        public void ToLocation_PointWithoutVolumeShapeOrPosition_IsRefused()
        {
            var proto = PolygonProto();
            proto.VolumePosition = null;

            Assert.Throws<ArgumentException>(() => proto.ToLocation());
        }

        /// <summary>
        /// A polygon, line or curve sent without its volume shape used to store a point at the volume position (or the mosaic
        /// shape) as its volume shape, which OData and export then read as its volume geometry. It is refused, as a circle
        /// without its volume circle is.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ToLocation_ShapeWithoutVolumeShape_IsRefused(bool withVolumePosition)
        {
            var proto = PolygonProto();
            proto.MosaicGeometry = AnnotationShapes.PolygonShape(LocationType.POLYGON, Viking.gRPC.Geometry.V1.CoordinateSpace.Mosaic,
                new global::Geometry.Vector2[] { new(0, 0), new(10, 0), new(5, 5), new(0, 0) }, System.Array.Empty<global::Geometry.Vector2[]>(), null);
            if (!withVolumePosition)
                proto.VolumePosition = null;

            ArgumentException refused = Assert.Throws<ArgumentException>(() => proto.ToLocation());
            Assert.Contains("volume shape", refused.Message);
        }

        [Fact]
        public void ToLocation_ShapeWinsOverPosition()
        {
            var proto = PolygonProto();
            proto.MosaicGeometry = AnnotationShapes.PointShape(9, 8);

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
            proto.Terminal = true;
            proto.OffEdge = true;
            proto.MosaicGeometry = new global::Geometry.Vector2[] { new(0, 0), new(10, 0), new(5, 5) }.ToClosedCurve(4.5);
            proto.VolumeGeometry = proto.MosaicGeometry.Clone();

            var entity = proto.ToLocation();

            Assert.Equal(7L, entity.Id);
            Assert.Equal(3L, entity.ParentId);
            Assert.Equal(12L, entity.Z);
            Assert.Equal((short)LocationType.CLOSEDCURVE, entity.TypeCode);
            Assert.Equal("<Attributes />", entity.Tags);
            Assert.True(entity.Closed);
            Assert.True(entity.Terminal);
            Assert.True(entity.OffEdge);
            Assert.Equal(4.5, entity.Width);
            Assert.Equal("u", entity.Username);
        }

        [Fact]
        public void ToLocation_WidthAndClosedComeFromTheShape()
        {
            var proto = PolygonProto();
            proto.MosaicGeometry = AnnotationShapes.PolygonShape(LocationType.POLYGON, Viking.gRPC.Geometry.V1.CoordinateSpace.Mosaic,
                new global::Geometry.Vector2[] { new(0, 0), new(10, 0), new(5, 5), new(0, 0) }, System.Array.Empty<global::Geometry.Vector2[]>(), 4.5);
            proto.VolumeGeometry = proto.MosaicGeometry.Clone();

            var entity = proto.ToLocation();

            Assert.Equal((short)LocationType.POLYGON, entity.TypeCode);
            Assert.Null(entity.Width);
            Assert.False(entity.Closed);
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
            TypeCode = (short)LocationType.POLYGON,
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
            entity.TypeCode = (short)LocationType.POLYLINE;
            entity.MosaicShape = new LineString(new[] { new Coordinate(0, 0), new Coordinate(5, 5), new Coordinate(10, 0) });
            entity.VolumeShape = entity.MosaicShape.Copy();
            entity.Width = 4.5;

            var proto = entity.ToProtobufMessage();

            Assert.Equal(7L, proto.Id);
            Assert.Equal(3L, proto.ParentId);
            Assert.Equal(12L, proto.Section);
            Assert.Equal(LocationType.POLYLINE, proto.MosaicGeometry.LocationTypeOf());
            Assert.Equal(1.5, proto.MosaicPosition.X);
            Assert.Equal(200.75, proto.VolumePosition.Y);
            Assert.Equal(4.5, proto.MosaicGeometry.WidthOf());
        }

        [Fact]
        public void ToProtobufMessage_ShapesReadBackAsTheSameGeometry()
        {
            var proto = PolygonEntity().ToProtobufMessage();

            Assert.Equal(ProtoShape.KindOneofCase.Point, proto.MosaicGeometry.KindCase);
            var back = proto.VolumeGeometry.ToNetTopologyGeometry();
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

    static class LocationConversionTestShapes
    {
        /// <summary>A closed curve of <paramref name="controlPoints"/> with <paramref name="width"/>.</summary>
        public static ProtoShape ToClosedCurve(this global::Geometry.Vector2[] controlPoints, double width) =>
            AnnotationShapes.LineShape(LocationType.CLOSEDCURVE, Viking.gRPC.Geometry.V1.CoordinateSpace.Mosaic, controlPoints, width);
    }
}
