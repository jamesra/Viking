using System;
using System.Text.RegularExpressions;
using Geometry;
using gRPCAnnotationService.Protos;
using Viking.AnnotationServiceTypes.gRPC.V1;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Xunit;
using EfLocation = Viking.DataModel.Annotation.Location;
using ProtoGeometry = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry;
using ProtoLocation = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location;

namespace ConnectomeDataModelCoreTests
{
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
        }

        [Fact]
        public void CircleShapeWkt_RejectsVolumePoint()
        {
            var loc = CircleProto(mosaicRadius: 8, volumeRadius: 10);
            loc.VolumeShape = new ProtoGeometry { Text = "POINT (11 22)" };

            Assert.Throws<ArgumentException>(() => loc.CircleShapeWkt());
        }

        [Fact]
        public void ToLocation_UsesTemporaryPointsForCircleShapes()
        {
            var entity = CircleProto(mosaicRadius: 8, volumeRadius: 10).ToLocation();

            Assert.IsType<NetTopologySuite.Geometries.Point>(entity.MosaicShape);
            Assert.IsType<NetTopologySuite.Geometries.Point>(entity.VolumeShape);
            Assert.Equal(10.5, entity.MosaicShape.Coordinate.X);
            Assert.Equal(20.5, entity.MosaicShape.Coordinate.Y);
            Assert.Equal(11, entity.VolumeShape.Coordinate.X);
            Assert.Equal(22, entity.VolumeShape.Coordinate.Y);
        }

        [Fact]
        public void ToProtobufMessage_ReconstructsVolumeCircleWithMosaicRadiusPlaceholder()
        {
            var entity = new EfLocation
            {
                TypeCode = 1,
                X = 10.5,
                Y = 20.5,
                Radius = 8,
                VolumeX = 11,
                VolumeY = 22,
                Username = string.Empty,
            };

            var proto = entity.ToProtobufMessage();
            var mosaic = ParseCircle(proto.MosaicShape.Text);
            var volume = ParseCircle(proto.VolumeShape.Text);

            Assert.Equal(8, mosaic.Radius, 5);
            Assert.Equal(8, volume.Radius, 5);
            Assert.Equal(11, volume.Center.X, 5);
            Assert.Equal(22, volume.Center.Y, 5);
        }

        private static ICircle2D ParseCircle(string wkt)
        {
            var normalized = CircularStringKeyword.Replace(wkt, string.Empty);
            return Assert.IsAssignableFrom<ICircle2D>(normalized.ParseWKT());
        }

        private static ProtoLocation CircleProto(double mosaicRadius, double volumeRadius) =>
            new()
            {
                TypeCode = AnnotationType.Circle,
                MosaicPosition = new AnnotationPoint { X = 10.5, Y = 20.5 },
                VolumePosition = new AnnotationPoint { X = 11, Y = 22 },
                Radius = mosaicRadius,
                MosaicShape = new ProtoGeometry
                {
                    Text = GeometryExtensions.ToCircleWKT(10.5, 20.5, mosaicRadius)
                },
                VolumeShape = new ProtoGeometry
                {
                    Text = GeometryExtensions.ToCircleWKT(11, 22, volumeRadius)
                },
            };
    }
}
