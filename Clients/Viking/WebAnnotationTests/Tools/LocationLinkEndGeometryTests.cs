using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotationTests.Tools
{
    /// <summary>
    /// A location link's ends meet the shapes their locations are drawn as, which come from
    /// <see cref="LocationVolumeGeometry"/>: the circle or adjacent marker, a polygon's outline, or a line's edge at its
    /// scaled width. Checked for every location type on and off the viewed section, under a scale and a
    /// rotate/scale/shear affine.
    /// </summary>
    [TestClass]
    public class LocationLinkEndGeometryTests
    {
        private const int ViewedSection = 10;
        private const int AdjacentSection = 11;

        /// <summary>Gives every section the same transform.</summary>
        private sealed class OneTransform(IVolumeToSectionTransform transform) : IVolumeTransformProvider
        {
            public IVolumeToSectionTransform GetSectionToVolumeTransform(int SectionNumber) => transform;
        }

        [TestInitialize]
        public void LinkColoursNeedAParentWithoutAStore()
        {
            SegmentationServiceSessionTests.EnsureVolumeCachePathAvailable();
            // Type 1 (cell) colours come from the structure ID alone, so no structure type store is needed.
            LocationObj.ParentLookup = id => new StructureObj(new AnnotationService.Types.Structure { ID = id, TypeID = 1 });
        }

        [TestCleanup]
        public void RestoreParentLookup() => LocationObj.ParentLookup = LocationObj.DefaultParentLookup;

        /// <summary>A circle far to the right of every fixture, on <paramref name="section"/>, as the other end.</summary>
        private static LocationObj Partner(int section) => LocationVolumeGeometryTests.NewLocation(
            LocationType.CIRCLE, SqlGeometryUtils.Extensions.ToCircle(5000, 3000, 0, 100), id: 2, section: section);

        private static LocationObj FixtureOn(LocationType type, int section)
        {
            LocationObj fixture = LocationVolumeGeometryTests.Fixture(type);
            return LocationVolumeGeometryTests.NewLocation(type, fixture.MosaicShape, fixture.Width, id: 1, section: section);
        }

        /// <summary>Calls LocationLinkView's end placement, which is private; the ends for (a, b) seen from the viewed section.</summary>
        private static (Circle A, Circle B) Ends(LocationObj a, LocationObj b, IVolumeToSectionTransform transform)
        {
            MethodInfo? place = typeof(LocationLinkView).GetMethod("TryMapEndpointPositions", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(place);
            object?[] args = [a, b, ViewedSection, new OneTransform(transform), null, null, null, null, null];
            Assert.IsTrue((bool)place.Invoke(null, args)!, "the ends were placed");
            return ((Circle)args[4]!, (Circle)args[5]!);
        }

        /// <summary>The point of <paramref name="end"/> nearest <paramref name="partner"/>, where the drawn link leaves it.</summary>
        private static Vector2 Front(Circle end, Circle partner)
        {
            Vector2 offset = partner.Center - end.Center;
            double length = Math.Sqrt((offset.X * offset.X) + (offset.Y * offset.Y));
            return end.Center + (offset / length * end.Radius);
        }

        private static double DistanceToLine(IReadOnlyList<Vector2> points, Vector2 p) =>
            Enumerable.Range(1, points.Count - 1).Min(i => new LineSegment(points[i - 1], points[i]).DistanceToPoint(p));

        [TestMethod]
        public void EveryLinkEndMeetsTheShapeItsLocationIsDrawnAs()
        {
            List<string> failures = [];
            foreach (var (name, transform) in LocationVolumeGeometryTests.Transforms)
            {
                foreach (LocationType type in LocationVolumeGeometryTests.ViewTypes)
                {
                    foreach (bool adjacent in new[] { false, true })
                    {
                        string message = $"{type} {(adjacent ? "adjacent" : "on section")} under {name}";
                        try
                        {
                            LocationObj end = FixtureOn(type, adjacent ? AdjacentSection : ViewedSection);
                            LocationObj partner = Partner(adjacent ? ViewedSection : AdjacentSection);
                            (Circle linkEnd, Circle partnerEnd) = Ends(end, partner, transform);
                            AssertMeetsDrawnShape(end, LocationVolumeGeometry.For(end, transform), adjacent, linkEnd, partnerEnd, message);
                        }
                        catch (AssertFailedException e)
                        {
                            failures.Add(e.Message);
                        }
                    }
                }
            }

            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        }

        private static void AssertMeetsDrawnShape(LocationObj end, LocationVolumeGeometry owner, bool adjacent, Circle linkEnd, Circle partnerEnd, string message)
        {
            double tolerance = 1e-3;
            Vector2 front = Front(linkEnd, partnerEnd);
            bool drawnAsLine = end.TypeCode == LocationType.POLYLINE || (end.TypeCode == LocationType.OPENCURVE && !adjacent);
            if (drawnAsLine)
            {
                IReadOnlyList<Vector2> drawn = end.TypeCode == LocationType.OPENCURVE ? owner.Shape.ToPoints() : owner.ControlPoints;
                Assert.AreEqual(owner.Width / 2.0, DistanceToLine(drawn, front), tolerance, message + ": leaves the line's edge");
            }
            else if (adjacent)
            {
                RenderedGeometry.AssertCircle(owner.AdjacentMarker(WebAnnotation.Global.AdjacentLocationRadiusScalar), linkEnd, message + ": the adjacent marker");
            }
            else if (owner.Circle is Circle circle)
            {
                RenderedGeometry.AssertCircle(circle, linkEnd, message + ": the drawn circle");
            }
            else
            {
                SqlGeometry boundary = owner.Outline!.STBoundary();
                double distance = boundary.STDistance(front.ToSqlGeometry()).Value;
                // SQL geometry and the segment intersections carry a few thousandths of a pixel.
                Assert.AreEqual(0, distance, 1e-2, message + ": leaves the drawn outline");
            }
        }
    }
}
