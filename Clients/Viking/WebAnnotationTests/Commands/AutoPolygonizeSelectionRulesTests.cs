using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Data.SqlTypes;
using WebAnnotation.UI.AutoPolygonize;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// The two selection rules of auto-polygonize that decide what the user sees: which part of a split
    /// outline survives (<see cref="AutoPolygonizeSelection.TrySelectPolygonPart"/>) and when two proposals
    /// are merged into one group (<see cref="AutoPolygonizeSelection.ShouldGroupSiblingPolygons"/>).
    /// Every expected value is the hand-computed area or the corner coordinates of the squares below.
    /// </summary>
    [TestClass]
    public class AutoPolygonizeSelectionRulesTests
    {
        private static SqlGeometry Wkt(string text) =>
            SqlGeometry.STGeomFromText(new SqlChars(text), 0);

        private static Polygon Square(double left, double bottom, double size) => Rect(left, bottom, size, size);

        private static Polygon Rect(double left, double bottom, double width, double height) =>
            new([
                new Vector2(left, bottom),
                new Vector2(left + width, bottom),
                new Vector2(left + width, bottom + height),
                new Vector2(left, bottom + height),
                new Vector2(left, bottom)
            ]);

        // ---- TrySelectPolygonPart ----

        // Parts used below: BIG is 10x10 at the origin (area 100), SMALL is 3x3 at x=20 (area 9).
        private const string BigAndSmall =
            "MULTIPOLYGON(((0 0, 10 0, 10 10, 0 10, 0 0)), ((20 0, 23 0, 23 3, 20 3, 20 0)))";

        [TestMethod]
        public void KeepPointInTheSmallerPartWinsOverTheLargerPart()
        {
            Polygon? chosen = AutoPolygonizeSelection.TrySelectPolygonPart(Wkt(BigAndSmall), new Vector2(21, 1));

            Assert.IsNotNull(chosen);
            Assert.AreEqual(9, chosen!.Area, 1e-9);
            Assert.AreEqual(20, chosen.BoundingBox.Left, 1e-9);
        }

        [TestMethod]
        public void KeepPointInTheLargerPartReturnsTheLargerPart()
        {
            Polygon? chosen = AutoPolygonizeSelection.TrySelectPolygonPart(Wkt(BigAndSmall), new Vector2(5, 5));

            Assert.IsNotNull(chosen);
            Assert.AreEqual(100, chosen!.Area, 1e-9);
        }

        [TestMethod]
        public void NoKeepPointOrOneOutsideEveryPartReturnsTheLargestPart()
        {
            Polygon? withoutKeep = AutoPolygonizeSelection.TrySelectPolygonPart(Wkt(BigAndSmall), null);
            Polygon? outside = AutoPolygonizeSelection.TrySelectPolygonPart(Wkt(BigAndSmall), new Vector2(50, 50));
            Polygon? inGap = AutoPolygonizeSelection.TrySelectPolygonPart(Wkt(BigAndSmall), new Vector2(15, 1));

            Assert.AreEqual(100, withoutKeep!.Area, 1e-9);
            Assert.AreEqual(100, outside!.Area, 1e-9);
            Assert.AreEqual(100, inGap!.Area, 1e-9);
        }

        [TestMethod]
        public void LargestPartWinsWhateverTheListedOrder()
        {
            const string smallFirst =
                "MULTIPOLYGON(((20 0, 23 0, 23 3, 20 3, 20 0)), ((0 0, 10 0, 10 10, 0 10, 0 0)))";

            Polygon? chosen = AutoPolygonizeSelection.TrySelectPolygonPart(Wkt(smallFirst), null);

            Assert.AreEqual(100, chosen!.Area, 1e-9);
        }

        [TestMethod]
        public void KeepPointOnThePartBoundaryCountsAsInsideThePart()
        {
            // (20, 1) lies on the left edge of the 3x3 part, not in its interior.
            Polygon? onEdge = AutoPolygonizeSelection.TrySelectPolygonPart(Wkt(BigAndSmall), new Vector2(20, 1));
            // (23, 3) is its top-right corner.
            Polygon? onCorner = AutoPolygonizeSelection.TrySelectPolygonPart(Wkt(BigAndSmall), new Vector2(23, 3));

            Assert.AreEqual(9, onEdge!.Area, 1e-9);
            Assert.AreEqual(9, onCorner!.Area, 1e-9);
        }

        [TestMethod]
        public void EqualAreaPartsWithoutAKeepPointKeepTheFirstListedPart()
        {
            // Two 10x10 parts; the sort by area is stable, so the part listed first wins.
            const string twins = "MULTIPOLYGON(((0 0, 10 0, 10 10, 0 10, 0 0)), ((30 0, 40 0, 40 10, 30 10, 30 0)))";

            Polygon? chosen = AutoPolygonizeSelection.TrySelectPolygonPart(Wkt(twins), null);

            Assert.AreEqual(100, chosen!.Area, 1e-9);
            Assert.AreEqual(0, chosen.BoundingBox.Left, 1e-9);
        }

        [TestMethod]
        public void KeepPointStillPicksTheSecondOfTwoEqualAreaParts()
        {
            const string twins = "MULTIPOLYGON(((0 0, 10 0, 10 10, 0 10, 0 0)), ((30 0, 40 0, 40 10, 30 10, 30 0)))";

            Polygon? chosen = AutoPolygonizeSelection.TrySelectPolygonPart(Wkt(twins), new Vector2(35, 5));

            Assert.AreEqual(30, chosen!.BoundingBox.Left, 1e-9);
        }

        [TestMethod]
        public void PartsAtOrBelowTheMinimumOverlapAreaAreIgnored()
        {
            // Sliver 0.125 x 0.0625 = 0.0078125 < 1e-2 (both exact in binary). A keep point inside it must not
            // select it: it is dropped before the keep-point test.
            const string withSliver =
                "MULTIPOLYGON(((0 0, 10 0, 10 10, 0 10, 0 0)), ((20 0, 20.125 0, 20.125 0.0625, 20 0.0625, 20 0)))";

            Polygon? chosen = AutoPolygonizeSelection.TrySelectPolygonPart(Wkt(withSliver), new Vector2(20.05, 0.03));

            Assert.AreEqual(100, chosen!.Area, 1e-9);
        }

        [TestMethod]
        public void AGeometryWhoseOnlyPartIsASliverYieldsNothing()
        {
            Polygon? chosen = AutoPolygonizeSelection.TrySelectPolygonPart(
                Wkt("POLYGON((20 0, 20.125 0, 20.125 0.0625, 20 0.0625, 20 0))"), new Vector2(20.05, 0.03));

            Assert.IsNull(chosen);
        }

        [TestMethod]
        public void APartJustAboveTheMinimumOverlapAreaIsKept()
        {
            // 0.125 x 0.125 = 0.015625 > 1e-2: a keep point inside selects it over the 100-unit part.
            const string withSmallPart =
                "MULTIPOLYGON(((0 0, 10 0, 10 10, 0 10, 0 0)), ((20 0, 20.125 0, 20.125 0.125, 20 0.125, 20 0)))";

            Polygon? chosen = AutoPolygonizeSelection.TrySelectPolygonPart(Wkt(withSmallPart), new Vector2(20.05, 0.05));

            Assert.AreEqual(0.015625, chosen!.Area, 1e-9);
        }

        [TestMethod]
        public void ASinglePartIsReturnedEvenWhenTheKeepPointIsElsewhere()
        {
            Polygon? chosen = AutoPolygonizeSelection.TrySelectPolygonPart(
                Wkt("POLYGON((0 0, 10 0, 10 10, 0 10, 0 0))"), new Vector2(500, 500));

            Assert.AreEqual(100, chosen!.Area, 1e-9);
        }

        [TestMethod]
        public void PartAreaSubtractsItsHoleWhenChoosingTheLargest()
        {
            // Part A: 10x10 with a 6x6 hole -> 100 - 36 = 64. Part B: 9x9 solid -> 81. B is smaller by bounding
            // box but larger by area, so B wins.
            const string holey =
                "MULTIPOLYGON(((0 0, 10 0, 10 10, 0 10, 0 0), (2 2, 8 2, 8 8, 2 8, 2 2)), ((30 0, 39 0, 39 9, 30 9, 30 0)))";

            Polygon? chosen = AutoPolygonizeSelection.TrySelectPolygonPart(Wkt(holey), null);

            Assert.AreEqual(81, chosen!.Area, 1e-9);
        }

        [TestMethod]
        public void KeepPointInsideAHoleDoesNotSelectThatPartButOneInItsRingDoes()
        {
            const string holey =
                "MULTIPOLYGON(((0 0, 10 0, 10 10, 0 10, 0 0), (2 2, 8 2, 8 8, 2 8, 2 2)), ((30 0, 39 0, 39 9, 30 9, 30 0)))";

            Polygon? inHole = AutoPolygonizeSelection.TrySelectPolygonPart(Wkt(holey), new Vector2(5, 5));
            Polygon? inRing = AutoPolygonizeSelection.TrySelectPolygonPart(Wkt(holey), new Vector2(1, 1));

            Assert.AreEqual(81, inHole!.Area, 1e-9, "the hole is not part of the polygon, so it falls back to the largest");
            Assert.AreEqual(64, inRing!.Area, 1e-9);
        }

        [TestMethod]
        public void GeometryCollectionPartsAreFlattened()
        {
            const string collection =
                "GEOMETRYCOLLECTION(POLYGON((0 0, 10 0, 10 10, 0 10, 0 0)), POLYGON((20 0, 23 0, 23 3, 20 3, 20 0)))";

            Polygon? chosen = AutoPolygonizeSelection.TrySelectPolygonPart(Wkt(collection), new Vector2(21, 1));

            Assert.AreEqual(9, chosen!.Area, 1e-9);
        }

        [TestMethod]
        public void NonPolygonOrEmptyGeometryYieldsNothing()
        {
            Assert.IsNull(AutoPolygonizeSelection.TrySelectPolygonPart(Wkt("POINT(1 1)"), null));
            Assert.IsNull(AutoPolygonizeSelection.TrySelectPolygonPart(Wkt("POLYGON EMPTY"), new Vector2(1, 1)));
            Assert.IsNull(AutoPolygonizeSelection.TrySelectPolygonPart(SqlGeometry.Null, null));
        }

        // ---- ShouldGroupSiblingPolygons ----

        [TestMethod]
        public void SquaresSharingOnlyAnEdgeAreNotGrouped()
        {
            Assert.IsFalse(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Square(0, 0, 10), Square(10, 0, 10)));
            Assert.IsFalse(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Square(10, 0, 10), Square(0, 0, 10)));
        }

        [TestMethod]
        public void SquaresSharingPartOfAnEdgeAreNotGrouped()
        {
            // x = 10 is shared for y in 2..8 only.
            Assert.IsFalse(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Square(0, 0, 10), Rect(10, 2, 10, 6)));
        }

        [TestMethod]
        public void SquaresSharingOnlyACornerAreNotGrouped()
        {
            Assert.IsFalse(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Square(0, 0, 10), Square(10, 10, 10)));
        }

        [TestMethod]
        public void SquaresWithOverlappingAreaAreGroupedInEitherOrder()
        {
            // Overlap is x 5..10, y 0..10: area 50.
            Assert.IsTrue(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Square(0, 0, 10), Square(5, 0, 10)));
            Assert.IsTrue(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Square(5, 0, 10), Square(0, 0, 10)));
        }

        [TestMethod]
        public void CrossingRectanglesWithNoVertexInsideTheOtherAreGrouped()
        {
            // A plus sign: each rectangle crosses the other through its interior (overlap 4 x 4 = 16 at the centre).
            Assert.IsTrue(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Rect(0, 8, 20, 4), Rect(8, 0, 4, 20)));
        }

        [TestMethod]
        public void ContainedPolygonIsGroupedInEitherOrder()
        {
            Polygon outer = Square(0, 0, 20);
            Polygon inner = Square(6, 6, 8);

            Assert.IsTrue(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(outer, inner));
            Assert.IsTrue(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(inner, outer));
        }

        [TestMethod]
        public void ContainedPolygonThatSharesBoundaryEdgesWithTheOuterIsStillGrouped()
        {
            // The inner square shares the outer's left and bottom edges but has area inside it.
            Assert.IsTrue(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Square(0, 0, 10), Square(0, 0, 5)));
            Assert.IsTrue(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Square(0, 0, 5), Square(0, 0, 10)));
        }

        [TestMethod]
        public void IdenticalRingsAreGrouped()
        {
            // Two circles in one blob can get the same SAM2 mask and so the same ring; the overlap is the whole area 100.
            Assert.IsTrue(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Square(0, 0, 10), Square(0, 0, 10)));
        }

        [TestMethod]
        public void SameRingStartingAtAnotherVertexOrReversedIsGrouped()
        {
            Polygon rotated = new([
                new Vector2(10, 10), new Vector2(0, 10), new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10)]);
            Polygon reversed = new([
                new Vector2(0, 0), new Vector2(0, 10), new Vector2(10, 10), new Vector2(10, 0), new Vector2(0, 0)]);

            Assert.IsTrue(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Square(0, 0, 10), rotated));
            Assert.IsTrue(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(rotated, Square(0, 0, 10)));
            Assert.IsTrue(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Square(0, 0, 10), reversed));
            Assert.IsTrue(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(reversed, Square(0, 0, 10)));
        }

        [TestMethod]
        public void HalfOfASquareWithEveryVertexOnItsBoundaryIsGroupedInEitherOrder()
        {
            // The triangle below the diagonal covers area 50 of the square; all three corners are square corners.
            Polygon triangle = new([new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 0)]);

            Assert.IsTrue(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Square(0, 0, 10), triangle));
            Assert.IsTrue(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(triangle, Square(0, 0, 10)));
        }

        [TestMethod]
        public void RingAndASlightlyLargerRingAroundItAreGrouped()
        {
            // 0.5 wider on every side: the smaller ring is wholly inside, overlap area 100.
            Assert.IsTrue(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Square(0, 0, 10), Square(-0.5, -0.5, 11)));
            Assert.IsTrue(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Square(-0.5, -0.5, 11), Square(0, 0, 10)));
        }

        [TestMethod]
        public void DisjointPolygonsAreNotGrouped()
        {
            Assert.IsFalse(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Square(0, 0, 10), Square(100, 100, 10)));
            // A gap of 0.5 between the facing edges.
            Assert.IsFalse(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Square(0, 0, 10), Square(10.5, 0, 10)));
        }

        [TestMethod]
        public void ANullPolygonIsNeverGrouped()
        {
            Polygon square = Square(0, 0, 10);

            Assert.IsFalse(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(null!, square));
            Assert.IsFalse(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(square, null!));
            Assert.IsFalse(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(null!, null!));
        }

        [TestMethod]
        public void PolygonInsideAnotherPolygonsHoleIsNotGroupedButOneCrossingTheHoleEdgeIs()
        {
            // 20x20 ring with a hole 5..15. A 4x4 square at 8..12 sits wholly in the hole (no shared area).
            Polygon ring = new(
                [new Vector2(0, 0), new Vector2(20, 0), new Vector2(20, 20), new Vector2(0, 20), new Vector2(0, 0)],
                [[new Vector2(5, 5), new Vector2(15, 5), new Vector2(15, 15), new Vector2(5, 15), new Vector2(5, 5)]]);

            Assert.IsFalse(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(ring, Square(8, 8, 4)));
            Assert.IsFalse(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(Square(8, 8, 4), ring));
            // 10..18 spans the hole edge at x = 15, so it covers ring area x 15..18.
            Assert.IsTrue(AutoPolygonizeSelection.ShouldGroupSiblingPolygons(ring, Square(10, 8, 8)));
        }
    }
}
