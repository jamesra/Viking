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

namespace WebAnnotationTests.Tools
{
    /// <summary>
    /// The inverse owner, <see cref="LocationVolumeGeometry.ToMosaic(LocationType, SqlGeometry, IVolumeToSectionTransform, double?)"/>:
    /// an edit saved through it draws back, through the forward owner, as the geometry the user edited (forward(inverse(v)) == v)
    /// under a scale, an affine rotate/scale/shear and a non-linear warp; and an edit with a point outside the transform is
    /// refused with nothing saved.
    /// </summary>
    [TestClass]
    public class LocationVolumeGeometryInverseTests
    {
        /// <summary>Volume units; the transforms are exact, so only the fixed-point solves and float rounding are left.</summary>
        private const double Tolerance = 1e-6;

        /// <summary>
        /// Volume units for circles: the forward owner reads a circle's centre and radius back from its saved shape
        /// (<c>LocationObj.Position</c> rounds the centroid, <c>Radius</c> comes from the area), which costs about 1e-3.
        /// </summary>
        private const double CircleTolerance = 0.01;

        private const int Samples = 20;

        /// <summary>The non-rigid transforms the round trip must hold under, by name.</summary>
        private static readonly (string Name, IVolumeToSectionTransform Transform)[] Transforms =
        [
            ("scale 1.25", new ScaleTransform(1.25, new Vector2(500, -200))),
            ("rotate, scale and shear", AffineTransform.RotateScaleShear()),
            ("non-linear warp", new WarpTransform()),
        ];

        /// <summary>A new, unsaved location of <paramref name="type"/> with a placeholder shape the edit replaces.</summary>
        private static LocationObj Blank(LocationType type, double? width = null) =>
            LocationVolumeGeometryTests.NewLocation(type, SqlGeometry.Point(0, 0, 0), width);

        private static LocationVolumeGeometry Saved(LocationType type, MosaicGeometry mosaic, IVolumeToSectionTransform transform, double? width = null)
        {
            Assert.IsTrue(mosaic.Succeeded, mosaic.Failure);
            LocationObj location = Blank(type, width);
            Assert.IsTrue(location.TryApply(mosaic, transform));
            return LocationVolumeGeometry.For(location, transform);
        }

        private static void AssertSamePoints(IReadOnlyList<Vector2> expected, IReadOnlyList<Vector2> actual, string because)
        {
            Assert.AreEqual(expected.Count, actual.Count, because);
            for (int i = 0; i < expected.Count; i++)
                Assert.AreEqual(0, Vector2.Distance(expected[i], actual[i]), Tolerance * Math.Max(1, expected[i].X), $"{because}: point {i}");
        }

        private static Vector2 RandomPoint(Random random) => new(1500 + (random.NextDouble() * 4000), 1500 + (random.NextDouble() * 4000));

        /// <summary>A closed, counterclockwise star-shaped ring around <paramref name="center"/>, valid as a polygon ring.</summary>
        private static Vector2[] Ring(Random random, Vector2 center, double radius, int corners)
        {
            Vector2[] ring = new Vector2[corners + 1];
            for (int i = 0; i < corners; i++)
            {
                double angle = 2 * Math.PI * i / corners;
                double r = radius * (0.7 + (0.3 * random.NextDouble()));
                ring[i] = new Vector2(center.X + (r * Math.Cos(angle)), center.Y + (r * Math.Sin(angle)));
            }

            ring[corners] = ring[0];
            return ring;
        }

        private static Vector2[] Curve(Random random, int count)
        {
            Vector2 p = RandomPoint(random);
            Vector2[] points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                points[i] = p;
                p += new Vector2(80 + (random.NextDouble() * 120), (random.NextDouble() - 0.5) * 200);
            }

            return points;
        }

        [TestMethod]
        public void ACircleSavedFromVolumeDrawsBackAsTheEditedCircle()
        {
            foreach (var (name, transform) in Transforms)
            {
                Random random = new(1);
                for (int s = 0; s < Samples; s++)
                {
                    Circle edited = new(RandomPoint(random), 20 + (random.NextDouble() * 300));
                    Circle drawn = Saved(LocationType.CIRCLE, LocationVolumeGeometry.ToMosaic(edited, transform), transform).Circle!.Value;

                    Assert.AreEqual(0, Vector2.Distance(edited.Center, drawn.Center), CircleTolerance, $"{name}: centre of sample {s}");
                    Assert.AreEqual(edited.Radius, drawn.Radius, CircleTolerance, $"{name}: radius of sample {s}");
                }
            }
        }

        [TestMethod]
        public void ACircleShapeGoesThroughTheCircleInverse()
        {
            IVolumeToSectionTransform transform = AffineTransform.RotateScaleShear();
            Circle edited = new(new Vector2(3000, 2500), 150);
            SqlGeometry volumeShape = SqlGeometryUtils.Extensions.ToCircle(edited.Center.X, edited.Center.Y, 0, edited.Radius);

            Circle drawn = Saved(LocationType.CIRCLE, LocationVolumeGeometry.ToMosaic(LocationType.CIRCLE, volumeShape, transform), transform).Circle!.Value;

            Assert.AreEqual(edited.Radius, drawn.Radius, CircleTolerance);
        }

        /// <summary>
        /// Why the circle inverse is solved against the forward rule: under shear the one-step inverse five-point mean is not
        /// the forward rule's inverse, so a circle saved that way would be drawn at another size than the user drew.
        /// </summary>
        [TestMethod]
        public void TheOneStepInverseFivePointMeanDoesNotRoundTripUnderShear()
        {
            IVolumeToSectionTransform transform = AffineTransform.RotateScaleShear();
            Circle edited = new(new Vector2(3000, 2500), 150);

            Circle oneStep = transform.TryMapShapeVolumeToSection(edited)!.Value;
            Circle redrawn = transform.TryMapShapeSectionToVolume(oneStep)!.Value;

            Assert.IsTrue(Math.Abs(redrawn.Radius - edited.Radius) > 1, $"one-step inverse redraws at {redrawn.Radius}, the edit was {edited.Radius}");
        }

        [TestMethod]
        public void ALineOrCurveSavedFromVolumeDrawsBackWithTheEditedControlPointsAndWidth()
        {
            foreach (LocationType type in new[] { LocationType.POLYLINE, LocationType.OPENCURVE, LocationType.CLOSEDCURVE })
            {
                foreach (var (name, transform) in Transforms)
                {
                    Random random = new(2);
                    for (int s = 0; s < Samples; s++)
                    {
                        Vector2[] edited = Curve(random, 3 + random.Next(5));
                        double width = 2 + (random.NextDouble() * 40);

                        MosaicGeometry mosaic = LocationVolumeGeometry.ToMosaic(type, edited, transform, width);
                        LocationVolumeGeometry drawn = Saved(type, mosaic, transform);

                        AssertSamePoints(edited, drawn.ControlPoints, $"{type} {name} sample {s}");
                        Assert.AreEqual(width, drawn.Width, 1e-6 * width, $"{type} {name} sample {s}: width");
                    }
                }
            }
        }

        [TestMethod]
        public void TheMosaicWidthIsTheVolumeWidthDividedByTheLocalScale()
        {
            ScaleTransform transform = new(2.0, new Vector2(10, 10));
            MosaicGeometry mosaic = LocationVolumeGeometry.ToMosaic(LocationType.POLYLINE, [new Vector2(100, 100), new Vector2(400, 300)], transform, 30);

            Assert.AreEqual(15, mosaic.Width!.Value, 1e-9, "30 volume units at scale 2 is 15 mosaic pixels, not 30");
        }

        /// <summary>
        /// Placing a curve (<c>SetShapeFromPointsInVolume</c>, as <c>AnnotationOverlay.PlaceFromVolume</c> calls it) with the
        /// 16-unit width the placement drew saves the mosaic width divided by the local scale, so the curve is drawn 16 wide
        /// and not 16 times the scale.
        /// </summary>
        [TestMethod]
        public void APlacedCurveSavesItsDrawnWidthDividedByTheLocalScale()
        {
            foreach (var (name, transform) in Transforms)
            {
                LocationObj location = Blank(LocationType.OPENCURVE);
                Vector2[] drawn = [new(2000, 2000), new(2300, 2100), new(2500, 1900)];

                location.SetShapeFromPointsInVolume(transform, drawn, null, 16);

                LocationVolumeGeometry geometry = LocationVolumeGeometry.For(location, transform);
                AssertSamePoints(drawn, geometry.ControlPoints, name);
                Assert.AreEqual(16, geometry.Width, 1e-6, $"{name}: drawn width");
                Assert.AreEqual(16 / geometry.Scale, location.Width!.Value, 1e-9, $"{name}: mosaic width");
            }

            LocationObj placed = Blank(LocationType.OPENCURVE);
            placed.SetShapeFromPointsInVolume(new ScaleTransform(2.0, Vector2.Zero), [new(100, 100), new(400, 300)], null, 16);
            Assert.AreEqual(8, placed.Width!.Value, 1e-9, "16 volume units at scale 2 is 8 mosaic pixels, not 16");
        }

        [TestMethod]
        public void APolygonWithHolesSavedFromVolumeDrawsBackAsTheEditedPolygon()
        {
            foreach (LocationType type in new[] { LocationType.POLYGON, LocationType.CURVEPOLYGON })
            {
                foreach (var (name, transform) in Transforms)
                {
                    Random random = new(3);
                    for (int s = 0; s < Samples; s++)
                    {
                        Vector2 center = RandomPoint(random);
                        Polygon edited = new(Ring(random, center, 600, 6 + random.Next(10)));
                        edited.AddInteriorRing(Enumerable.Reverse(Ring(random, center + new Vector2(-150, 0), 80, 5)).ToArray());
                        edited.AddInteriorRing(Enumerable.Reverse(Ring(random, center + new Vector2(180, 40), 60, 4)).ToArray());

                        Polygon drawn = Saved(type, LocationVolumeGeometry.ToMosaic(type, edited, transform), transform).ControlPolygon();

                        Assert.AreEqual(2, drawn.InteriorRings.Count, $"{type} {name} sample {s}: holes");
                        AssertSamePoints(edited.ExteriorRing.ToList(), drawn.ExteriorRing.ToList().RotateToNearest(edited.ExteriorRing[0]), $"{type} {name} sample {s}: exterior");
                    }
                }
            }
        }

        [TestMethod]
        public void AnEditWithAPointOutsideTheTransformIsRefusedAndNothingIsSaved()
        {
            IVolumeToSectionTransform transform = new ClippedTransform(new ScaleTransform(1, Vector2.Zero), new Rectangle(0, 1000, 0, 1000));
            LocationObj location = LocationVolumeGeometryTests.NewLocation(LocationType.POLYLINE,
                SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars("LINESTRING (100 100, 200 200)"), 0), width: 5);
            IShape2D before = location.MosaicShape;

            MosaicGeometry mosaic = LocationVolumeGeometry.ToMosaic(LocationType.POLYLINE, [new Vector2(100, 100), new Vector2(500, 500), new Vector2(1500, 500)], transform, 12);

            Assert.IsFalse(mosaic.Succeeded);
            Assert.IsNull(mosaic.Shape);
            StringAssert.Contains(mosaic.Failure, "1 of 3 points");
            Assert.IsFalse(location.TryApply(mosaic, transform));
            Assert.AreSame(before, location.MosaicShape);
            Assert.AreEqual(5, location.Width);
        }

        /// <summary>
        /// A circle needs its centre to map; a rim point outside is left out of the five-point mean, as the forward rule
        /// leaves it out, so a circle across the edge of the transform still saves.
        /// </summary>
        [TestMethod]
        public void ACircleWhoseCentreIsOutsideTheTransformIsRefused()
        {
            IVolumeToSectionTransform transform = new ClippedTransform(new ScaleTransform(1, Vector2.Zero), new Rectangle(0, 1000, 0, 1000));

            MosaicGeometry outside = LocationVolumeGeometry.ToMosaic(new Circle(new Vector2(1200, 500), 50), transform);
            Assert.IsFalse(outside.Succeeded);
            StringAssert.Contains(outside.Failure, "centre");
            Assert.IsTrue(LocationVolumeGeometry.ToMosaic(new Circle(new Vector2(500, 500), 50), transform).Succeeded, "inside");
            Assert.IsTrue(LocationVolumeGeometry.ToMosaic(new Circle(new Vector2(980, 500), 50), transform).Succeeded, "rim across the edge");
        }

        [TestMethod]
        public void SetShapeFromGeometryInSectionDerivesTheVolumeShapeFromTheForwardOwner()
        {
            IVolumeToSectionTransform transform = new WarpTransform();
            LocationObj location = Blank(LocationType.CURVEPOLYGON);
            SqlGeometry mosaic = new Vector2[] { new(2000, 2000), new(2600, 2000), new(2600, 2500), new(2000, 2500), new(2000, 2000) }.ToPolygon();

            location.SetShapeFromGeometryInSection(transform, mosaic);

            Assert.IsTrue(LocationVolumeGeometry.ToSqlGeometry(location.VolumeShape).STEquals(LocationVolumeGeometry.For(location, transform).Shape).IsTrue);
        }

        [TestMethod]
        public void SetShapeFromGeometryInSectionRefusesAShapeTheTransformCannotMapAndKeepsTheOldOne()
        {
            IVolumeToSectionTransform transform = new ClippedTransform(new ScaleTransform(1, Vector2.Zero), new Rectangle(0, 1000, 0, 1000));
            LocationObj location = LocationVolumeGeometryTests.NewLocation(LocationType.POLYGON,
                new Vector2[] { new(100, 100), new(300, 100), new(300, 300), new(100, 100) }.ToPolygon());
            IShape2D before = location.MosaicShape;

            Assert.ThrowsException<ArgumentException>(() => location.SetShapeFromGeometryInSection(transform,
                new Vector2[] { new(100, 100), new(1300, 100), new(300, 300), new(100, 100) }.ToPolygon()));

            Assert.AreSame(before, location.MosaicShape);
        }
    }

    internal static class RingExtensions
    {
        /// <summary>
        /// The closed ring <paramref name="ring"/> started at its point nearest <paramref name="start"/>: a polygon built from
        /// a ring may start it at another corner, and the same ring is the same polygon.
        /// </summary>
        public static List<Vector2> RotateToNearest(this List<Vector2> ring, Vector2 start)
        {
            List<Vector2> open = ring.Take(ring.Count - 1).ToList();
            int first = open.IndexOf(open.OrderBy(p => Vector2.Distance(p, start)).First());
            List<Vector2> rotated = [.. open.Skip(first), .. open.Take(first)];
            rotated.Add(rotated[0]);
            return rotated;
        }
    }
}
