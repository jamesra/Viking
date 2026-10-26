using System;
using System.Text.RegularExpressions;
using Geometry;
using gRPCAnnotationService.Protos;
using NetTopologySuite.Geometries;
using Viking.AnnotationServiceTypes.gRPC.V1;
using Viking.DataModel.Annotation;
using Xunit;
using EfLocation = Viking.DataModel.Annotation.Location;
using ProtoLocation = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location;
using ProtoShape = Viking.gRPC.Geometry.V1.Shape;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Circles between the proto and EF: EF holds each space's east rim point (NTS cannot hold a CurvePolygon), so the
    /// volume circle keeps its own radius rather than the mosaic one.
    /// </summary>
    public class LocationCircleConversionTests
    {
        private static readonly Regex CircularStringKeyword = new(
            @"CIRCULARSTRING\s*",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        [Fact]
        public void CircleShapeWkt_UsesVolumeShapeRadiusNotMosaicRadius()
        {
            var loc = CircleProto(mosaicRadius: 8, volumeRadius: 10);
            var (mosaicWkt, volumeWkt) = loc.CircleShapeWkt();

            var mosaic = ParseCircle(mosaicWkt);
            var volume = ParseCircle(volumeWkt);

            Assert.Equal(8, mosaic.Radius, 5);
            Assert.Equal(10, volume.Radius, 5);
            Assert.Equal(11, volume.Center.X, 5);
            Assert.Equal(22, volume.Center.Y, 5);
        }

        [Fact]
        public void CircleShapeWkt_RejectsVolumePoint()
        {
            var loc = CircleProto(mosaicRadius: 8, volumeRadius: 10);
            loc.VolumeGeometry = AnnotationShapes.PointShape(11, 22);

            Assert.Throws<ArgumentException>(() => loc.CircleShapeWkt());
        }

        [Fact]
        public void ToLocation_WithoutVolumeCircle_IsRejectedRatherThanCopyingTheMosaicRadius()
        {
            var loc = CircleProto(mosaicRadius: 8, volumeRadius: 10);
            loc.VolumeGeometry = null;

            Assert.Throws<ArgumentException>(() => loc.ToLocation());
        }

        [Fact]
        public void ToLocation_StoresEastRimPointsForCircleShapes()
        {
            var entity = CircleProto(mosaicRadius: 8, volumeRadius: 10).ToLocation();

            Assert.Equal((short)Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE, entity.TypeCode);
            var mosaic = Assert.IsType<Point>(entity.MosaicShape);
            var volume = Assert.IsType<Point>(entity.VolumeShape);
            Assert.Equal(18.5, mosaic.X);
            Assert.Equal(20.5, mosaic.Y);
            Assert.Equal(21, volume.X);
            Assert.Equal(22, volume.Y);
            Assert.Null(entity.Width);
        }

        [Fact]
        public void ToProtobufMessage_VolumeCircleKeepsItsOwnRadius()
        {
            var entity = new EfLocation
            {
                TypeCode = 1,
                X = 10.5,
                Y = 20.5,
                Radius = 8,
                VolumeX = 11,
                VolumeY = 22,
                // As the interceptor reads them: each space's east rim point.
                MosaicShape = new Point(18.5, 20.5),
                VolumeShape = new Point(21, 22),
                Username = string.Empty,
            };

            var proto = entity.ToProtobufMessage();

            Assert.Equal(ProtoShape.KindOneofCase.Circle, proto.MosaicGeometry.KindCase);
            Assert.Equal(8, proto.MosaicGeometry.Circle.Radius, 10);
            Assert.Equal(10.5, proto.MosaicGeometry.Circle.Center.X);
            Assert.Equal(10, proto.VolumeGeometry.Circle.Radius, 10);
            Assert.Equal(11, proto.VolumeGeometry.Circle.Center.X);
            Assert.Equal(22, proto.VolumeGeometry.Circle.Center.Y);
        }

        [Fact]
        public void ToProtobufMessage_MosaicRadiusFallsBackToTheRadiusColumn()
        {
            var entity = new EfLocation
            {
                TypeCode = 1,
                X = 10.5,
                Y = 20.5,
                Radius = 8,
                VolumeX = 11,
                VolumeY = 22,
                MosaicShape = new Point(10.5, 20.5),
                VolumeShape = new Point(21, 22),
                Username = string.Empty,
            };

            Assert.Equal(8, entity.ToProtobufMessage().MosaicGeometry.Circle.Radius, 10);
        }

        [Fact]
        public void ProtoToEfToProto_RoundTripsBothRadii()
        {
            var entity = CircleProto(mosaicRadius: 8, volumeRadius: 10).ToLocation();
            entity.X = 10.5;
            entity.Y = 20.5;
            entity.VolumeX = 11;
            entity.VolumeY = 22;

            var back = entity.ToProtobufMessage();

            Assert.Equal(8, back.MosaicGeometry.Circle.Radius, 10);
            Assert.Equal(10, back.VolumeGeometry.Circle.Radius, 10);
        }

        [Fact]
        public void SqlGeometryBytes_ReadsTheFirstPointOfACurvePolygon()
        {
            // SRID 0, version 2, properties V (valid), 5 points, then figures/shapes/segments (not read).
            var bytes = new byte[10 + (5 * 16)];
            BitConverter.GetBytes(0).CopyTo(bytes, 0);
            bytes[4] = 2;
            bytes[5] = 0x04;
            BitConverter.GetBytes(5u).CopyTo(bytes, 6);
            BitConverter.GetBytes(1.0).CopyTo(bytes, 10);
            BitConverter.GetBytes(22.0).CopyTo(bytes, 18);

            Assert.True(SqlGeometryBytes.TryReadFirstPoint(bytes, out double x, out double y));
            Assert.Equal(1.0, x);
            Assert.Equal(22.0, y);
        }

        [Fact]
        public void SqlGeometryBytes_ReadsASinglePoint()
        {
            var bytes = new byte[6 + 16];
            bytes[4] = 1;
            bytes[5] = 0x0C;
            BitConverter.GetBytes(3.5).CopyTo(bytes, 6);
            BitConverter.GetBytes(-4.5).CopyTo(bytes, 14);

            Assert.True(SqlGeometryBytes.TryReadFirstPoint(bytes, out double x, out double y));
            Assert.Equal(3.5, x);
            Assert.Equal(-4.5, y);
        }

        [Fact]
        public void SqlGeometryBytes_RejectsTruncatedInput()
        {
            Assert.False(SqlGeometryBytes.TryReadFirstPoint(new byte[] { 0, 0, 0, 0, 2, 4, 5, 0, 0, 0 }, out _, out _));
        }

        private static ICircle2D ParseCircle(string wkt)
        {
            var normalized = CircularStringKeyword.Replace(wkt, string.Empty);
            return Assert.IsAssignableFrom<ICircle2D>(normalized.ParseWKT());
        }

        private static ProtoLocation CircleProto(double mosaicRadius, double volumeRadius) =>
            new()
            {
                MosaicPosition = new Viking.gRPC.Geometry.V1.Vector2 { X = 10.5, Y = 20.5 },
                VolumePosition = new Viking.gRPC.Geometry.V1.Vector2 { X = 11, Y = 22 },
                MosaicGeometry = AnnotationShapes.CircleShape(10.5, 20.5, mosaicRadius),
                VolumeGeometry = AnnotationShapes.CircleShape(11, 22, volumeRadius),
            };
    }
}
