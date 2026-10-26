using System;
using System.Linq;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.AnnotationServiceTypes.gRPC.V1;
using Viking.AnnotationServiceTypes.Interfaces;
using ProtoCoordinateSpace = Viking.gRPC.Geometry.V1.CoordinateSpace;
using ProtoCurveMethod = Viking.gRPC.Geometry.V1.CurveMethod;
using ProtoLocation = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location;
using ProtoShape = Viking.gRPC.Geometry.V1.Shape;

namespace gRPC_Tests
{
    /// <summary>
    /// Every annotation type through <c>viking.geometry.v1.Shape</c> and <see cref="AnnotationShapes"/> and back: the
    /// shape the client gets is the one it sent, its WKT equals the WKT the client wrote before the migration, and the
    /// type and width follow from the shape. No server needed.
    /// </summary>
    [TestClass]
    public class AnnotationShapeRoundTripTests
    {
        static readonly Vector2[] Square = [new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(0, 0)];
        static readonly Vector2[] Hole = [new(2, 2), new(4, 2), new(4, 4), new(2, 4), new(2, 2)];
        static readonly Vector2[] Wiggle = [new(100.25, 200.5), new(110.75, 205.125), new(120.5, 199.875), new(131, 210)];

        /// <summary>Converts to the wire and back, checks the type, and returns the shape the client would hold.</summary>
        static IShape2D RoundTrip(IShape2D shape, LocationType type, double? width, ProtoCoordinateSpace space, out ProtoShape wire)
        {
            wire = shape.ToShape(type, space, width);
            if (space == ProtoCoordinateSpace.Mosaic)
                Assert.AreEqual(type, wire.LocationTypeOf());
            return wire.ToShape2D();
        }

        /// <summary>The WKT the client writes for a shape, as today's client sent it.</summary>
        static void AssertSameWkt(IShape2D expected, IShape2D actual) => Assert.AreEqual(expected.ToWKT(), actual.ToWKT());

        [TestMethod]
        public void Point_RoundTrips()
        {
            var point = new Vector2(10.25, 20.5);

            var back = RoundTrip(point, LocationType.POINT, null, ProtoCoordinateSpace.Mosaic, out var wire);

            Assert.AreEqual(ProtoShape.KindOneofCase.Point, wire.KindCase);
            Assert.AreEqual(point, back);
            Assert.IsNull(wire.WidthOf());
            AssertSameWkt(point, back);
        }

        [TestMethod]
        public void Circle_RoundTripsWithEachSpacesOwnRadius()
        {
            var mosaic = new Circle(1000.5, 2000.25, 8);

            // The volume circle as the client derives it: the centre and the four axis rim points mapped, the radius
            // scaled by the mean of the rim scales (here a rotation by 30 degrees, scale 1.25, and a shift).
            static Vector2 Map(Vector2 p)
            {
                double a = Math.PI / 6, s = 1.25;
                return new Vector2((s * ((Math.Cos(a) * p.X) - (Math.Sin(a) * p.Y))) + 50, (s * ((Math.Sin(a) * p.X) + (Math.Cos(a) * p.Y))) - 20);
            }

            Vector2 c = mosaic.Center.ToVector2();
            Vector2 mappedCenter = Map(c);
            Vector2[] rims = [new(c.X + 8, c.Y), new(c.X, c.Y + 8), new(c.X - 8, c.Y), new(c.X, c.Y - 8)];
            double meanScale = rims.Average(r => Vector2.Distance(Map(r), mappedCenter) / 8);
            var volume = new Circle(mappedCenter, 8 * meanScale);

            var mosaicBack = RoundTrip(mosaic, LocationType.CIRCLE, null, ProtoCoordinateSpace.Mosaic, out var mosaicWire);
            var volumeBack = RoundTrip(volume, LocationType.CIRCLE, null, ProtoCoordinateSpace.Volume, out var volumeWire);

            Assert.AreEqual(8, mosaicWire.Circle.Radius);
            Assert.AreEqual(10, volumeWire.Circle.Radius, 1e-9);
            Assert.AreEqual(mappedCenter.X, volumeWire.Circle.Center.X);
            Assert.AreEqual(mosaic, (Circle)mosaicBack);
            Assert.AreEqual(volume, (Circle)volumeBack);
            AssertSameWkt(mosaic, mosaicBack);
            AssertSameWkt(volume, volumeBack);
        }

        [TestMethod]
        public void Ellipse_TypeFromShape_ButNoClientShape()
        {
            var wire = new ProtoShape
            {
                Ellipse = new Viking.gRPC.Geometry.V1.Ellipse
                {
                    Center = new Viking.gRPC.Geometry.V1.Vector2 { X = 1, Y = 2 },
                    SemiMajor = 4,
                    SemiMinor = 2,
                },
            };

            Assert.AreEqual(LocationType.ELLIPSE, wire.LocationTypeOf());
            Assert.ThrowsException<NotSupportedException>(() => wire.ToShape2D());
        }

        [TestMethod]
        public void Ellipse_RowIsSentAsTheGeometryItStores()
        {
            var stored = new Polygon(Square);

            var back = RoundTrip(stored, LocationType.ELLIPSE, null, ProtoCoordinateSpace.Volume, out var wire);

            Assert.AreEqual(ProtoShape.KindOneofCase.Polygon, wire.KindCase);
            AssertSameWkt(stored, back);
        }

        [TestMethod]
        public void Polyline_RoundTripsWithWidth()
        {
            var line = new Polyline(Wiggle);

            var back = RoundTrip(line, LocationType.POLYLINE, 4.5, ProtoCoordinateSpace.Mosaic, out var wire);

            Assert.IsFalse(wire.Line.Closed);
            Assert.AreEqual(ProtoCurveMethod.None, wire.Line.Fit?.Method ?? ProtoCurveMethod.None);
            Assert.AreEqual(4.5, wire.WidthOf());
            AssertSameWkt(line, back);
        }

        [TestMethod]
        public void TwoPointPolyline_ComesBackAsTheSegmentTheWktParserBuilds()
        {
            var segment = new LineSegment(new Vector2(1, 2), new Vector2(3, 4));

            var back = RoundTrip(segment, LocationType.POLYLINE, 2, ProtoCoordinateSpace.Mosaic, out _);

            Assert.IsInstanceOfType(back, typeof(LineSegment));
            AssertSameWkt(segment, back);
        }

        [TestMethod]
        public void OpenCurve_SendsControlPointsWithACatmullRomFit()
        {
            var curve = new Polyline(Wiggle);

            var back = RoundTrip(curve, LocationType.OPENCURVE, 16, ProtoCoordinateSpace.Mosaic, out var wire);

            Assert.IsFalse(wire.Line.Closed);
            Assert.AreEqual(ProtoCurveMethod.CatmullRom, wire.Line.Fit.Method);
            Assert.AreEqual(0u, wire.Line.Fit.Interpolations);
            Assert.AreEqual(Wiggle.Length, wire.Line.Points.Count);
            Assert.AreEqual(16, wire.WidthOf());
            AssertSameWkt(curve, back);
        }

        [TestMethod]
        public void OpenCurve_VolumeOutlineIsSentWithoutAFit()
        {
            var outline = new Polyline(Wiggle.CalculateCurvePoints(3, false));

            var back = RoundTrip(outline, LocationType.OPENCURVE, 16, ProtoCoordinateSpace.Volume, out var wire);

            Assert.IsNull(wire.Line.Fit);
            Assert.AreEqual(LocationType.POLYLINE, wire.LocationTypeOf(), "The type follows from the mosaic shape only");
            AssertSameWkt(outline, back);
        }

        [TestMethod]
        public void ClosedCurve_IsAClosedLineWithoutTheRepeatedVertex()
        {
            Vector2[] ring = [.. Wiggle, Wiggle[0]];
            var curve = new Polyline(ring);

            var back = RoundTrip(curve, LocationType.CLOSEDCURVE, 8, ProtoCoordinateSpace.Mosaic, out var wire);

            Assert.IsTrue(wire.Line.Closed);
            Assert.AreEqual(ProtoCurveMethod.CatmullRom, wire.Line.Fit.Method);
            Assert.AreEqual(Wiggle.Length, wire.Line.Points.Count);
            Assert.AreEqual(8, wire.WidthOf());
            AssertSameWkt(curve, back);
        }

        [TestMethod]
        public void ClosedCurve_HeldAsAPolygon_BecomesTheSameClosedLine()
        {
            Vector2[] ring = [new(0, 0), new(10, 0), new(5, 8), new(0, 0)];

            var fromPolygon = new Polygon(ring).ToShape(LocationType.CLOSEDCURVE, ProtoCoordinateSpace.Mosaic, 8);
            var fromLine = new Polyline(ring).ToShape(LocationType.CLOSEDCURVE, ProtoCoordinateSpace.Mosaic, 8);

            Assert.AreEqual(fromLine, fromPolygon);
        }

        [TestMethod]
        public void PolygonWithHoles_RoundTrips()
        {
            var polygon = new Polygon(Square);
            polygon.AddInteriorRing(Hole);

            var back = RoundTrip(polygon, LocationType.POLYGON, 3, ProtoCoordinateSpace.Mosaic, out var wire);

            Assert.AreEqual(4, wire.Polygon.Exterior.Points.Count, "The ring does not repeat its first vertex");
            Assert.AreEqual(1, wire.Polygon.Holes.Count);
            Assert.IsNull(wire.Polygon.Fit);
            Assert.IsNull(wire.WidthOf(), "Polygons have no width");
            AssertSameWkt(polygon, back);
        }

        [TestMethod]
        public void CurvePolygon_SendsControlRingsWithAFit()
        {
            var polygon = new Polygon(Square);
            polygon.AddInteriorRing(Hole);

            var back = RoundTrip(polygon, LocationType.CURVEPOLYGON, null, ProtoCoordinateSpace.Mosaic, out var wire);

            Assert.AreEqual(ProtoCurveMethod.CatmullRom, wire.Polygon.Fit.Method);
            Assert.AreEqual(0u, wire.Polygon.Fit.Interpolations);
            AssertSameWkt(polygon, back);
        }

        [TestMethod]
        public void Rectangle_RoundTrips()
        {
            var r = new Rectangle(left: -1.5, right: 10.25, bottom: 2, top: 30);

            var wire = r.ToProtoRectangle();

            Assert.AreEqual(-1.5, wire.XMin);
            Assert.AreEqual(2, wire.YMin);
            Assert.AreEqual(10.25, wire.XMax);
            Assert.AreEqual(30, wire.YMax);
            Assert.AreEqual(r, wire.ToRectangle());
        }

        [TestMethod]
        public void ProtoLocation_AsILocation_ReadsTypeWidthRadiusAndWkt()
        {
            ILocation line = new ProtoLocation { MosaicGeometry = new Polyline(Wiggle).ToShape(LocationType.OPENCURVE, ProtoCoordinateSpace.Mosaic, 6) };
            Assert.AreEqual(LocationType.OPENCURVE, line.TypeCode);
            Assert.AreEqual(6, line.Width);
            Assert.AreEqual(new Polyline(Wiggle).Length / 2, line.Radius, 1e-9);
            Assert.IsFalse(line.Closed);

            ILocation circle = new ProtoLocation { MosaicGeometry = AnnotationShapes.CircleShape(10, 20, 4) };
            Assert.AreEqual(LocationType.CIRCLE, circle.TypeCode);
            Assert.AreEqual(4, circle.Radius);
            Assert.IsNull(circle.Width);
            Assert.AreEqual(GeometryExtensions.ToCircleWKT(10, 20, 4), circle.MosaicGeometryWKT);

            circle.TypeCode = LocationType.POINT;
            Assert.AreEqual(LocationType.CIRCLE, circle.TypeCode, "A circle shape stays a circle: the type follows the shape");
        }
    }
}
