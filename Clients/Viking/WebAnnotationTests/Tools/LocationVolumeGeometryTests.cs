using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotationModel.Objects;
using WebAnnotation;
using WebAnnotation.View;

namespace WebAnnotationTests.Tools
{
    /// <summary>
    /// <see cref="LocationVolumeGeometry"/> is the one forward owner of a location's volume geometry, and every view draws
    /// and hit-tests exactly what it gives, under a pure scale and under a transform that rotates, scales and shears.
    /// </summary>
    [TestClass]
    public class LocationVolumeGeometryTests
    {
        private const double Tolerance = 1e-6;

        private Microsoft.Xna.Framework.Graphics.SpriteFont? savedFont;

        /// <summary>
        /// Views read Global's settings and a font for their labels (there is no content manager here; a font object with no
        /// glyphs is enough to build them). The fixtures have no parent, so no view reaches a store.
        /// </summary>
        [TestInitialize]
        public void BuildViewsWithoutContent()
        {
            SegmentationServiceSessionTests.EnsureVolumeCachePathAvailable();
            savedFont = VikingXNAGraphics.Global.TryGetDefaultFont();
            VikingXNAGraphics.Global.DefaultFont = savedFont ?? (Microsoft.Xna.Framework.Graphics.SpriteFont)
                System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Microsoft.Xna.Framework.Graphics.SpriteFont));
        }

        [TestCleanup]
        public void RestoreContent()
        {
            VikingXNAGraphics.Global.DefaultFont = savedFont!;
        }

        /// <summary>The transforms every property is checked under, by name.</summary>
        internal static readonly (string Name, AffineTransform Transform)[] Transforms =
        [
            ("scale 1.25", new ScaleTransform(1.25, new Vector2(500, -200))),
            ("rotate, scale and shear", AffineTransform.RotateScaleShear()),
        ];

        /// <summary>A saved location of <paramref name="type"/> with no parent, built from <paramref name="mosaic"/>.</summary>
        internal static LocationObj NewLocation(LocationType type, SqlGeometry mosaic, double? width = null, long id = 1, int section = 10) =>
            NewLocation(type, mosaic.ToShape2D(), width, id, section);

        /// <summary>
        /// A saved location of <paramref name="type"/> with no parent whose mosaic shape is <paramref name="mosaic"/>. Circles
        /// are built this way: a circle read back from SQL geometry takes its radius from a bounding box and carries rounding.
        /// </summary>
        internal static LocationObj NewLocation(LocationType type, IShape2D mosaic, double? width = null, long id = 1, int section = 10)
        {
            LocationObj location = new(id)
            {
                Section = section,
                TypeCode = type,
                Width = width,
                MosaicShape = mosaic,
            };
            location.DBAction = DBACTION.NONE;
            return location;
        }

        private static SqlGeometry Wkt(string wkt) => SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(wkt), 0);

        /// <summary>One location of each type a view is built for, at mosaic coordinates around (2000, 3000).</summary>
        internal static LocationObj Fixture(LocationType type) => type switch
        {
            LocationType.CIRCLE => NewLocation(type, new Circle(new Vector2(2000, 3000), 120)),
            LocationType.POINT => NewLocation(type, Wkt("POINT (2000 3000)")),
            LocationType.POLYGON or LocationType.CURVEPOLYGON => NewLocation(type, Wkt(
                "POLYGON ((1800 2800, 2300 2800, 2300 3200, 1800 3200, 1800 2800), (1900 2900, 2000 2900, 2000 3000, 1900 3000, 1900 2900))")),
            LocationType.POLYLINE => NewLocation(type, Wkt("LINESTRING (1800 2800, 1950 3050, 2100 2900, 2300 3150)"), width: 12),
            LocationType.OPENCURVE => NewLocation(type, Wkt("LINESTRING (1800 2800, 1950 3050, 2100 2900, 2300 3150)"), width: 10),
            LocationType.CLOSEDCURVE => NewLocation(type, Wkt("LINESTRING (1800 2800, 2300 2800, 2350 3200, 1850 3250)"), width: 16),
            _ => throw new ArgumentException($"No fixture for {type}"),
        };

        [TestMethod]
        public void ACircleIsCentredOnItsMappedXYWithTheFivePointMeanRadius()
        {
            foreach (var (name, transform) in Transforms)
            {
                LocationObj circle = Fixture(LocationType.CIRCLE);

                LocationVolumeGeometry geometry = LocationVolumeGeometry.For(circle, transform);

                Vector2 centre = transform.Forward(new Vector2(2000, 3000));
                Assert.AreEqual(centre.X, geometry.Circle!.Value.Center.X, Tolerance, name);
                Assert.AreEqual(centre.Y, geometry.Circle.Value.Center.Y, Tolerance, name);
                Assert.AreEqual(120 * transform.FivePointScale, geometry.Circle.Value.Radius, 1e-3, name);
                Assert.AreEqual(geometry.Circle.Value.Center, geometry.Center, name);
                Assert.IsTrue(geometry.IsExact, name);
            }
        }

        [TestMethod]
        public void TheShapeIsTheOneTheVolumePositionJobWrites()
        {
            foreach (var (name, transform) in Transforms)
            {
                foreach (LocationType type in ViewTypes)
                {
                    LocationObj location = Fixture(type);

                    SqlGeometry expected = LocationVolumeGeometry.MapShape(type, LocationVolumeGeometry.ToSqlGeometry(location.MosaicShape), transform);

                    Assert.IsTrue(expected.STEquals(LocationVolumeGeometry.For(location, transform).Shape).IsTrue, $"{type} under {name}");
                }
            }
        }

        [TestMethod]
        public void TheGeometryIsCachedUntilTheMosaicShapeOrTheTransformChanges()
        {
            AffineTransform transform = Transforms[0].Transform;
            LocationObj location = Fixture(LocationType.POLYGON);
            LocationVolumeGeometry first = LocationVolumeGeometry.For(location, transform);

            Assert.AreSame(first, LocationVolumeGeometry.For(location, transform), "same shape, same transform");

            LocationVolumeGeometry reloaded = LocationVolumeGeometry.For(location, new ScaleTransform(1.25, new Vector2(500, -200)));
            Assert.AreNotSame(first, reloaded, "a reloaded .stos is a new transform object");

            location.MosaicShape = Wkt("POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))").ToShape2D();
            LocationVolumeGeometry moved = LocationVolumeGeometry.For(location, transform);
            Assert.AreNotSame(first, moved, "the mosaic shape changed");
            Assert.AreEqual(transform.Forward(new Vector2(5, 5)).X, moved.Center.X, Tolerance);
        }

        [TestMethod]
        public void AnAdjacentPolygonMarkerIsTheInscribedCircleOfTheMappedShape()
        {
            ScaleTransform doubled = new(2, new Vector2(0, 0));
            LocationObj square = NewLocation(LocationType.POLYGON, Wkt("POLYGON ((0 0, 100 0, 100 100, 0 100, 0 0))"));

            var marker = (AdjacentLocationCircleView)AnnotationViewFactory.CreateAdjacent(square, doubled);

            Assert.AreEqual(100, marker.VolumeCircle.Radius, 1, "a 100 pixel square is 200 units across at scale 2");
            Assert.AreEqual(100, marker.VolumeCircle.Center.X, 1);
            Assert.AreEqual(100, marker.VolumeCircle.Center.Y, 1);
        }

        [TestMethod]
        public void ALineOrCurveIsAsWideAsItsWidthTimesTheFivePointMeanScale()
        {
            foreach (var (name, transform) in Transforms)
            {
                foreach (LocationType type in new[] { LocationType.POLYLINE, LocationType.OPENCURVE })
                {
                    LocationObj line = Fixture(type);

                    LocationVolumeGeometry geometry = LocationVolumeGeometry.For(line, transform);

                    Assert.AreEqual(line.Width!.Value * transform.FivePointScale, geometry.Width, 1e-6, $"{type} under {name}");
                    Assert.AreEqual(line.Radius * transform.FivePointScale, geometry.Radius, 1e-6, $"{type} under {name}");
                }
            }
        }

        [TestMethod]
        public void TheFallbackPolicyExtrapolatesCurvesAndMarkersButNotOnSectionPolygonsOrPolylines()
        {
            IVolumeToSectionTransform outside = new UnmappableTransform(new AffineTransform(3, 0, 0, 3, new Vector2(0, 0)));
            LocationVolumeGeometry circle = LocationVolumeGeometry.For(Fixture(LocationType.CIRCLE), outside);
            LocationVolumeGeometry polygon = LocationVolumeGeometry.For(Fixture(LocationType.POLYGON), outside);
            LocationVolumeGeometry polyline = LocationVolumeGeometry.For(Fixture(LocationType.POLYLINE), outside);
            LocationVolumeGeometry curve = LocationVolumeGeometry.For(Fixture(LocationType.OPENCURVE), outside);

            Assert.IsFalse(circle.IsExact);
            Assert.AreEqual(6000, circle.Circle!.Value.Center.X, Tolerance, "the centre is extrapolated");
            Assert.AreEqual(120, circle.Circle.Value.Radius, Tolerance, "the mosaic radius is kept");
            Assert.IsTrue(circle.CanDrawOnSection);
            Assert.IsFalse(polygon.CanDrawOnSection, "an extrapolated polygon is not drawn on its section");
            Assert.IsFalse(polyline.CanDrawOnSection, "nor is an extrapolated polyline");
            Assert.IsTrue(curve.CanDrawOnSection, "an extrapolated curve is drawn");
            Assert.AreEqual(1, curve.Scale, Tolerance);
            Assert.AreEqual(5400, curve.ControlPoints[0].X, Tolerance);
        }

        /// <summary>Every location type the views draw.</summary>
        internal static readonly LocationType[] ViewTypes =
        [
            LocationType.CIRCLE, LocationType.POINT, LocationType.POLYGON, LocationType.CURVEPOLYGON,
            LocationType.POLYLINE, LocationType.OPENCURVE, LocationType.CLOSEDCURVE,
        ];

        /// <summary>The (type, on an adjacent section) pairs <see cref="EveryViewDrawsAndHitTestsTheOwnersGeometry"/> covers.</summary>
        internal static IEnumerable<(LocationType Type, bool Adjacent)> CoveredCases =>
            from type in ViewTypes
            from adjacent in new[] { false, true }
            where RenderedGeometry.IsChecked(type, adjacent)
            select (type, adjacent);

        [TestMethod]
        public void EveryViewDrawsAndHitTestsTheOwnersGeometry()
        {
            foreach (var (name, transform) in Transforms)
            {
                foreach (var (type, adjacent) in CoveredCases)
                {
                    LocationObj location = Fixture(type);
                    LocationCanvasView view = adjacent
                        ? AnnotationViewFactory.CreateAdjacent(location, transform)
                        : AnnotationViewFactory.Create(location, transform);

                    RenderedGeometry.AssertMatchesOwner(view, LocationVolumeGeometry.For(location, transform), adjacent, $"{type} {(adjacent ? "adjacent" : "on section")} under {name}");
                }
            }
        }
    }

    /// <summary>
    /// Reads what a view draws and hit-tests and compares it with <see cref="LocationVolumeGeometry"/>. A view type it does
    /// not know fails, so a new view cannot skip the property test.
    /// </summary>
    internal static class RenderedGeometry
    {
        private const double Tolerance = 1e-6;

        /// <summary>True for the (type, adjacent) pairs whose view <see cref="AssertMatchesOwner"/> knows.</summary>
        public static bool IsChecked(LocationType type, bool adjacent) =>
            adjacent ? type != LocationType.POLYLINE : type is LocationType.CIRCLE or LocationType.POINT;

        public static void AssertMatchesOwner(LocationCanvasView view, LocationVolumeGeometry owner, bool adjacent, string message)
        {
            switch (view)
            {
                case AdjacentLocationCircleView marker:
                    Assert.IsTrue(adjacent, message);
                    AssertCircle(owner.AdjacentMarker(WebAnnotation.Global.AdjacentLocationRadiusScalar), marker.VolumeCircle, message);
                    AssertCircle(marker.VolumeCircle, marker.VolumeShapeAsRendered.ToCircle(), message + " (hit test)");
                    break;
                case LocationCircleView circle:
                    Assert.IsFalse(adjacent, message);
                    AssertCircle(owner.Circle!.Value, circle.VolumeCircle, message);
                    Assert.AreEqual(owner.Circle.Value.Radius, circle.Radius, Tolerance, message);
                    AssertCircle(circle.VolumeCircle, circle.VolumeShapeAsRendered.ToCircle(), message + " (hit test)");
                    break;
                default:
                    Assert.Fail($"{message}: {view.GetType().Name} is not checked against LocationVolumeGeometry");
                    break;
            }
        }

        /// <summary>Equal to 1e-5 of the radius, at least 1e-3: a circle read back from SQL geometry carries rounding.</summary>
        public static void AssertCircle(Circle expected, Circle actual, string message)
        {
            double tolerance = Math.Max(1e-3, expected.Radius * 1e-5);
            Assert.AreEqual(expected.Center.X, actual.Center.X, tolerance, message + " centre X");
            Assert.AreEqual(expected.Center.Y, actual.Center.Y, tolerance, message + " centre Y");
            Assert.AreEqual(expected.Radius, actual.Radius, tolerance, message + " radius");
        }
    }

    /// <summary>Wraps a transform so no strict (Try...) mapping succeeds while the unchecked mapping still answers.</summary>
    internal sealed class UnmappableTransform(IVolumeToSectionTransform inner) : IVolumeToSectionTransform
    {
        public long ID => inner.ID;
        public Rectangle? SectionBounds => null;
        public Rectangle? VolumeBounds => null;
        public bool TrySectionToVolume(Vector2 P, out Vector2 transformedP) { transformedP = default; return false; }
        public bool TryVolumeToSection(Vector2 P, out Vector2 transformedP) { transformedP = default; return false; }
        public bool[] TrySectionToVolume(in Vector2[] Points, out Vector2[] transformedP) { transformedP = new Vector2[Points.Length]; return new bool[Points.Length]; }
        public bool[] TryVolumeToSection(in Vector2[] Points, out Vector2[] transformedP) { transformedP = new Vector2[Points.Length]; return new bool[Points.Length]; }
        public Vector2 SectionToVolume(Vector2 P) => inner.SectionToVolume(P);
        public Vector2 VolumeToSection(Vector2 P) => inner.VolumeToSection(P);
        public Vector2[] SectionToVolume(Vector2[] Points) => inner.SectionToVolume(Points);
        public Vector2[] VolumeToSection(Vector2[] Points) => inner.VolumeToSection(Points);
    }
}
