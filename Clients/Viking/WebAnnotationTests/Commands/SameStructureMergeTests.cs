using System.Collections.Generic;
using System.Linq;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotationModel.Objects;
using WebAnnotationTests.Actions;
using WebAnnotationTests.Tools;
using static System.FormattableString;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Which locations <see cref="SameStructureMerge.FindGroup"/> merges with a polygon the user just accepted (Segment to
    /// Polygon, Resegment): same structure, same section, overlapping it directly or through a chain of overlaps.
    /// </summary>
    [TestClass]
    public class SameStructureMergeTests
    {
        private const long Structure = 7;

        private static readonly IVolumeToSectionTransform Identity = ChangeTypeActionTests.CreateBoundedTransform();

        private static string SquareWkt(double left, double bottom, double size) =>
            Invariant($"POLYGON(({left} {bottom}, {left + size} {bottom}, {left + size} {bottom + size}, {left} {bottom + size}, {left} {bottom}))");

        private static Polygon Square(double left, double bottom, double size) =>
            new([
                new Vector2(left, bottom),
                new Vector2(left + size, bottom),
                new Vector2(left + size, bottom + size),
                new Vector2(left, bottom + size),
                new Vector2(left, bottom),
            ]);

        private static LocationObj PolygonLocation(long id, double left, double bottom, double size, long parent = Structure, long section = 1) =>
            TestModel.Location(id, parent, LocationType.POLYGON, SquareWkt(left, bottom, size), section);

        private static LocationObj CircleLocation(long id, double x, double y, double radius, long parent = Structure) =>
            TestModel.Location(id, parent, LocationType.CIRCLE,
                Invariant($"CURVEPOLYGON(CIRCULARSTRING({x + radius} {y}, {x} {y + radius}, {x - radius} {y}, {x} {y - radius}, {x + radius} {y}))"));

        private static long[] Ids(IEnumerable<SameStructureMerge.Piece> group) => [.. group.Select(piece => piece.Location.ID).OrderBy(id => id)];

        [TestMethod]
        public void AnOverlappingPolygonOfTheSameStructureJoins()
        {
            LocationObj saved = PolygonLocation(1, 10, 10, 40);

            List<SameStructureMerge.Piece> group = SameStructureMerge.FindGroup(saved, Square(10, 10, 40), [PolygonLocation(2, 40, 10, 40)], Identity);

            CollectionAssert.AreEqual(new long[] { 1, 2 }, Ids(group));
            Assert.AreEqual(1, group[0].Location.ID, "the accepted location comes first");
        }

        [TestMethod]
        public void APolygonOverlappingOnlyAnotherSiblingJoinsThroughTheChain()
        {
            LocationObj saved = PolygonLocation(1, 10, 10, 40);
            LocationObj middle = PolygonLocation(2, 40, 10, 40);
            LocationObj far = PolygonLocation(3, 70, 10, 40);

            List<SameStructureMerge.Piece> group = SameStructureMerge.FindGroup(saved, Square(10, 10, 40), [far, middle], Identity);

            CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, Ids(group));
        }

        [TestMethod]
        public void EdgeContactAloneDoesNotJoin()
        {
            LocationObj saved = PolygonLocation(1, 10, 10, 40);

            List<SameStructureMerge.Piece> group = SameStructureMerge.FindGroup(saved, Square(10, 10, 40), [PolygonLocation(2, 50, 10, 40)], Identity);

            CollectionAssert.AreEqual(new long[] { 1 }, Ids(group));
        }

        [TestMethod]
        public void OtherStructuresAndOtherSectionsStayOut()
        {
            LocationObj saved = PolygonLocation(1, 10, 10, 40);
            LocationObj otherStructure = PolygonLocation(2, 20, 20, 40, parent: 99);
            LocationObj otherSection = PolygonLocation(3, 20, 20, 40, section: 2);

            List<SameStructureMerge.Piece> group = SameStructureMerge.FindGroup(saved, Square(10, 10, 40), [otherStructure, otherSection], Identity);

            CollectionAssert.AreEqual(new long[] { 1 }, Ids(group));
        }

        [TestMethod]
        public void ASameStructureCircleIsLeftAloneAfterSegmentation()
        {
            LocationObj saved = PolygonLocation(1, 10, 10, 40);

            List<SameStructureMerge.Piece> group = SameStructureMerge.FindGroup(saved, Square(10, 10, 40), [CircleLocation(2, 50, 30, 15)], Identity);

            CollectionAssert.AreEqual(new long[] { 1 }, Ids(group));
        }

        [TestMethod]
        public void SiblingsAreComparedByTheirMosaicShapeMappedIntoTheVolume()
        {
            // volume = 2 * mosaic. The sibling's mosaic square 65..90 lies at 130..180 in the volume, over the accepted
            // outline at 100..140. TestModel stores the mosaic WKT as the stale VolumeShape too, which would not overlap.
            CircleLocationScaleTests.ScaleTransform scale = new(2, new Vector2(0, 0));
            LocationObj saved = PolygonLocation(1, 50, 50, 20);
            LocationObj sibling = PolygonLocation(2, 65, 50, 25);

            List<SameStructureMerge.Piece> group = SameStructureMerge.FindGroup(saved, Square(100, 100, 40), [sibling], scale);

            CollectionAssert.AreEqual(new long[] { 1, 2 }, Ids(group));
            Assert.AreEqual(130, group[1].Polygon.BoundingBox.Left, 1e-6, "the sibling's outline is in volume space");
        }

        // ---- A pen loop drawn around a circle (MergeAfterPenLoopAsync: circles included, polygons asked about) ----

        [TestMethod]
        public void APenLoopTakesInTheStructuresCirclesItTouchesWithoutAsking()
        {
            LocationObj saved = PolygonLocation(1, 10, 10, 40);
            bool asked = false;

            List<SameStructureMerge.Piece> group = SameStructureMerge.FindGroup(saved, Square(10, 10, 40),
                [CircleLocation(2, 50, 30, 15), CircleLocation(3, 150, 150, 10)], Identity,
                includeCircles: true, confirmPolygons: _ => asked = true);

            CollectionAssert.AreEqual(new long[] { 1, 2 }, Ids(group), "the circle the loop touches joins, the far one does not");
            Assert.IsFalse(asked, "circles are merged without asking");
            Assert.IsTrue(group.Single(piece => piece.Location.ID == 2).IsCircle);
        }

        [TestMethod]
        public void APenLoopAsksBeforeTakingInTheStructuresPolygons()
        {
            LocationObj saved = PolygonLocation(1, 10, 10, 40);
            IReadOnlyList<LocationObj>? askedAbout = null;

            List<SameStructureMerge.Piece> group = SameStructureMerge.FindGroup(saved, Square(10, 10, 40),
                [PolygonLocation(2, 40, 10, 40), CircleLocation(3, 30, 55, 10)], Identity,
                includeCircles: true, confirmPolygons: polygons => { askedAbout = polygons; return true; });

            Assert.IsNotNull(askedAbout, "an overlapping polygon is asked about");
            CollectionAssert.AreEqual(new long[] { 2 }, askedAbout.Select(location => location.ID).ToArray(), "only polygons are asked about");
            CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, Ids(group));
        }

        [TestMethod]
        public void DecliningThePolygonMergeKeepsTheCirclesAndDropsWhatChainsOnlyThroughThePolygon()
        {
            LocationObj saved = PolygonLocation(1, 10, 10, 40);
            LocationObj polygon = PolygonLocation(2, 40, 10, 40);
            LocationObj circleOnPolygon = CircleLocation(3, 85, 30, 10);
            LocationObj circleOnLoop = CircleLocation(4, 30, 55, 10);

            List<SameStructureMerge.Piece> group = SameStructureMerge.FindGroup(saved, Square(10, 10, 40),
                [polygon, circleOnPolygon, circleOnLoop], Identity, includeCircles: true, confirmPolygons: _ => false);

            CollectionAssert.AreEqual(new long[] { 1, 4 }, Ids(group));
        }

        [TestMethod]
        public void ThePolygonMergeQuestionNamesTheLocations()
        {
            StringAssert.Contains(WebAnnotation.UI.Actions.ChangeToPolygonAction.PolygonMergeQuestion([PolygonLocation(12, 0, 0, 1)]), "location 12");
            StringAssert.Contains(WebAnnotation.UI.Actions.ChangeToPolygonAction.PolygonMergeQuestion([PolygonLocation(12, 0, 0, 1), PolygonLocation(13, 0, 0, 1)]),
                "2 other polygons of this structure (12, 13)");
        }

        [TestMethod]
        public void ACirclesDiskIsItsMosaicCircleMappedWithTheFivePointScale()
        {
            // volume = 2 * mosaic: the circle at mosaic (80, 60) radius 10 is at volume (160, 120) radius 20, reaching x = 140,
            // inside the loop's 100..145. Its mosaic radius (10) copied into the volume would stop at x = 150 and miss it.
            CircleLocationScaleTests.ScaleTransform scale = new(2, new Vector2(0, 0));
            LocationObj saved = PolygonLocation(1, 50, 50, 22.5);

            List<SameStructureMerge.Piece> group = SameStructureMerge.FindGroup(saved, Square(100, 100, 45),
                [CircleLocation(2, 80, 60, 10)], scale, includeCircles: true, confirmPolygons: _ => true);

            CollectionAssert.AreEqual(new long[] { 1, 2 }, Ids(group));
            Rectangle disk = group[1].Polygon.BoundingBox;
            Assert.AreEqual(140, disk.Left, 1e-3);
            Assert.AreEqual(180, disk.Right, 1e-3);
        }
    }
}
