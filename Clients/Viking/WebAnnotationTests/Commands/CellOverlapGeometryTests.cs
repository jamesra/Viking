using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using WebAnnotation.UI.CellOverlap;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// The boolean geometry behind the cell overlap shading and the "give overlap to" action.
    /// </summary>
    [TestClass]
    public class CellOverlapGeometryTests
    {
        private static Polygon Box(double left, double bottom, double right, double top) =>
            new([new Vector2(left, bottom), new Vector2(right, bottom), new Vector2(right, top), new Vector2(left, top), new Vector2(left, bottom)]);

        [TestMethod]
        public void TwoOverlappingBoxesShareOneRegionOfTheOverlapArea()
        {
            var regions = CellOverlapGeometry.Intersection(Box(0, 0, 100, 100), Box(60, 20, 160, 80));

            Assert.AreEqual(1, regions.Count);
            Assert.AreEqual(40 * 60, regions[0].Area, 1e-6);
        }

        [TestMethod]
        public void BoxesThatOnlyTouchOrAreApartShareNothing()
        {
            Assert.AreEqual(0, CellOverlapGeometry.Intersection(Box(0, 0, 100, 100), Box(100, 0, 200, 100)).Count);
            Assert.AreEqual(0, CellOverlapGeometry.Intersection(Box(0, 0, 100, 100), Box(300, 0, 400, 100)).Count);
        }

        [TestMethod]
        public void AnOverlapSmallerThanTheMinimumIsIgnored()
        {
            Assert.AreEqual(0, CellOverlapGeometry.Intersection(Box(0, 0, 100, 100), Box(99, 99, 200, 200)).Count);
        }

        [TestMethod]
        public void SubtractingACornerLeavesOnePieceWithoutTheRegion()
        {
            Polygon outline = Box(0, 0, 100, 100);
            Polygon region = Box(60, 60, 100, 100);

            Assert.IsTrue(CellOverlapGeometry.TrySubtract(outline, region, new Vector2(10, 10), out Polygon? primary, out var extras));

            Assert.IsNotNull(primary);
            Assert.AreEqual(0, extras.Count);
            Assert.AreEqual((100 * 100) - (40 * 40), primary!.Area, 1e-6);
            Assert.IsFalse(primary.Covers(new Vector2(80, 80)));
            Assert.IsTrue(primary.Covers(new Vector2(10, 10)));
        }

        [TestMethod]
        public void SubtractingABandThatSplitsTheOutlineKeepsTheSeedsPartAndReturnsTheOther()
        {
            Polygon outline = Box(0, 0, 100, 100);
            Polygon band = Box(40, -10, 60, 110);

            Assert.IsTrue(CellOverlapGeometry.TrySubtract(outline, band, new Vector2(10, 50), out Polygon? primary, out var extras));

            Assert.IsNotNull(primary);
            Assert.IsTrue(primary!.Covers(new Vector2(10, 50)));
            Assert.AreEqual(1, extras.Count);
            Assert.IsTrue(extras.Single().Covers(new Vector2(90, 50)));
            Assert.AreEqual(2 * 40 * 100, primary.Area + extras.Single().Area, 1e-6);
        }

        [TestMethod]
        public void SubtractingARegionThatCoversTheWholeOutlineFailsAndLeavesNothing()
        {
            Assert.IsFalse(CellOverlapGeometry.TrySubtract(Box(10, 10, 50, 50), Box(0, 0, 100, 100), new Vector2(20, 20), out Polygon? primary, out var extras));

            Assert.IsNull(primary);
            Assert.AreEqual(0, extras.Count);
        }

        [TestMethod]
        public void AfterGivingTheOverlapToOneCellTheOutlinesNoLongerOverlap()
        {
            Polygon winner = Box(0, 0, 100, 100);
            Polygon loser = Box(60, 20, 160, 80);
            Polygon region = CellOverlapGeometry.Intersection(winner, loser).Single();

            Assert.IsTrue(CellOverlapGeometry.TrySubtract(loser, region, new Vector2(140, 50), out Polygon? remaining, out _));

            Assert.AreEqual(0, CellOverlapGeometry.Intersection(winner, remaining!).Count);
        }

        [TestMethod]
        public void TheRegionOfTwoCellsKnowsTheOtherCell()
        {
            CellOverlapRegion region = new(7, 9, Box(0, 0, 10, 10));

            Assert.AreEqual(9L, region.Other(7));
            Assert.AreEqual(7L, region.Other(9));
            Assert.IsNull(region.Other(8));
        }
    }
}
