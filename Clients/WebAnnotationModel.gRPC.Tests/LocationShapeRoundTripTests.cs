using System.Threading.Tasks;
using Geometry;
using NUnit.Framework;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.gRPC.Converters;
using WebAnnotationModel.Objects;
using ProtoShape = Viking.gRPC.Geometry.V1.Shape;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// A client location of every annotation type through the gRPC converters and back (as the client saves it and
    /// as a refresh applies the server's copy): the shapes, type and width come back as sent, the WKT equals what the
    /// client wrote before the shapes moved to <c>viking.geometry.v1.Shape</c>, and a circle keeps its volume radius.
    /// Runs without a server.
    /// </summary>
    [TestFixture]
    public class LocationShapeRoundTripTests
    {
        static readonly Vector2[] Wiggle = [new(100.25, 200.5), new(110.75, 205.125), new(120.5, 199.875), new(131, 210)];

        static Polygon SquareWithHole()
        {
            Vector2[] exterior = [new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(0, 0)];
            var polygon = new Polygon(exterior);
            Vector2[] hole = [new(2, 2), new(4, 2), new(4, 4), new(2, 4), new(2, 2)];
            polygon.AddInteriorRing(hole);
            return polygon;
        }

        static async Task<LocationObj> RoundTrip(LocationObj sent)
        {
            var proto = new LocationClientToServerConverter().Convert(sent);
            proto.LastModified = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(new System.DateTime(2026, 1, 2, 0, 0, 0, System.DateTimeKind.Utc));

            var received = new LocationObj(sent.ID, 2) { DBAction = DBACTION.NONE };
            await new LocationServerToClientUpdater().Update(received, proto);
            return received;
        }

        static LocationObj Location(LocationType type, IShape2D mosaic, IShape2D volume, double? width = null)
        {
            var obj = new LocationObj(5, 2)
            {
                TypeCode = type,
                MosaicShape = mosaic,
                VolumeShape = volume,
            };
            if (width.HasValue)
                obj.Width = width;
            return obj;
        }

        static object[] Cases() =>
        [
            new object[] { LocationType.POINT, new Vector2(1, 2), new Vector2(3, 4), null },
            new object[] { LocationType.POLYLINE, new Polyline(Wiggle), new Polyline(Wiggle), 4.5 },
            new object[] { LocationType.OPENCURVE, new Polyline(Wiggle), new Polyline(Wiggle.CalculateCurvePoints(3, false)), 16.0 },
            new object[] { LocationType.CLOSEDCURVE, new Polyline((Vector2[])[.. Wiggle, Wiggle[0]]), new Polyline(Wiggle.CalculateCurvePoints(10, true)), 8.0 },
            new object[] { LocationType.POLYGON, SquareWithHole(), SquareWithHole(), null },
            new object[] { LocationType.CURVEPOLYGON, SquareWithHole(), SquareWithHole(), null },
        ];

        [TestCaseSource(nameof(Cases))]
        public async Task EveryType_RoundTripsThroughTheConverters(LocationType type, IShape2D mosaic, IShape2D volume, double? width)
        {
            var sent = Location(type, mosaic, volume, width);

            var received = await RoundTrip(sent);

            Assert.That(received.TypeCode, Is.EqualTo(type));
            Assert.That(received.MosaicShape.ToWKT(), Is.EqualTo(mosaic.ToWKT()));
            Assert.That(received.VolumeShape.ToWKT(), Is.EqualTo(volume.ToWKT()));
            if (width.HasValue)
                Assert.That(received.Width, Is.EqualTo(width));
        }

        [Test]
        public async Task Circle_KeepsItsVolumeRadius()
        {
            var sent = Location(LocationType.CIRCLE, new Circle(10.5, 20.5, 8), new Circle(13, 25, 10));

            var proto = new LocationClientToServerConverter().Convert(sent);
            var received = await RoundTrip(sent);

            Assert.That(proto.MosaicGeometry.KindCase, Is.EqualTo(ProtoShape.KindOneofCase.Circle));
            Assert.That(proto.VolumeGeometry.Circle.Radius, Is.EqualTo(10));
            Assert.That(received.TypeCode, Is.EqualTo(LocationType.CIRCLE));
            Assert.That(((ICircle2D)received.MosaicShape).Radius, Is.EqualTo(8));
            Assert.That(((ICircle2D)received.VolumeShape).Radius, Is.EqualTo(10));
            Assert.That(((ICircle2D)received.VolumeShape).Center.ToVector2(), Is.EqualTo(new Vector2(13, 25)));
        }
    }
}
