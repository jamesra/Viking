using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using WebAnnotation.UI.AutoPolygonize;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Which proposals come back when one circle of a merged group changes.
    /// </summary>
    [TestClass]
    public class AutoPolygonizeDissolveTests
    {
        private static AutoPolygonizeProposal Proposal(long[] ids, IReadOnlyList<AutoPolygonizeProposal>? mergedFrom = null)
        {
            Polygon square = new([new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10), new Vector2(0, 0)]);
            return new AutoPolygonizeProposal(
                null!,
                ids[0],
                1,
                DateTime.UtcNow,
                5,
                square,
                [],
                locationIds: ids,
                mergedFrom: mergedFrom);
        }

        private static long[] Ids(IEnumerable<AutoPolygonizeProposal> proposals) =>
            [.. proposals.Select(item => item.LocationId).OrderBy(id => id)];

        [TestMethod]
        public void ChangingOneCircleOfAMergeRestoresTheOthers()
        {
            AutoPolygonizeProposal a = Proposal([1]);
            AutoPolygonizeProposal b = Proposal([2]);
            AutoPolygonizeProposal c = Proposal([3]);
            AutoPolygonizeProposal merged = Proposal([1, 2, 3], [a, b, c]);
            List<AutoPolygonizeProposal> survivors = [];

            AutoCirclePolygonizeController.CollectSurvivors(merged, 2, survivors);

            CollectionAssert.AreEqual(new long[] { 1, 3 }, Ids(survivors));
        }

        [TestMethod]
        public void ANestedMergeIsSearchedAndTheChangedCircleIsDropped()
        {
            AutoPolygonizeProposal a = Proposal([1]);
            AutoPolygonizeProposal c = Proposal([3]);
            AutoPolygonizeProposal pair = Proposal([1, 3], [a, c]);
            AutoPolygonizeProposal b = Proposal([2]);
            AutoPolygonizeProposal triple = Proposal([1, 2, 3], [pair, b]);
            List<AutoPolygonizeProposal> survivors = [];

            AutoCirclePolygonizeController.CollectSurvivors(triple, 3, survivors);

            CollectionAssert.AreEqual(new long[] { 1, 2 }, Ids(survivors), "the pair is taken apart; its other circle and the third come back");
        }

        [TestMethod]
        public void AMemberThatDoesNotContainTheChangedCircleComesBackWhole()
        {
            AutoPolygonizeProposal a = Proposal([1]);
            AutoPolygonizeProposal c = Proposal([3]);
            AutoPolygonizeProposal pair = Proposal([1, 3], [a, c]);
            AutoPolygonizeProposal b = Proposal([2]);
            AutoPolygonizeProposal triple = Proposal([1, 2, 3], [pair, b]);
            List<AutoPolygonizeProposal> survivors = [];

            AutoCirclePolygonizeController.CollectSurvivors(triple, 2, survivors);

            Assert.AreEqual(1, survivors.Count);
            Assert.AreSame(pair, survivors[0], "the earlier merge is restored as it was");
        }

        [TestMethod]
        public void AProposalThatCameFromOneRequestHasNothingToRestore()
        {
            AutoPolygonizeProposal single = Proposal([5]);
            List<AutoPolygonizeProposal> survivors = [];

            AutoCirclePolygonizeController.CollectSurvivors(single, 5, survivors);

            Assert.AreEqual(0, survivors.Count);
        }

        private static AutoPolygonizeSelection.CircleFootprint Circle(long id, long parentId, double x, double y, double radius, int section = 1) =>
            new(id, parentId, section, new Vector2(x, y), radius);

        private static Polygon Box(double left, double bottom, double size) =>
            new([new Vector2(left, bottom), new Vector2(left + size, bottom), new Vector2(left + size, bottom + size), new Vector2(left, bottom + size), new Vector2(left, bottom)]);

        [TestMethod]
        public void ACircleOfAnotherStructureThatOverlapsOrLiesInsideIsSubtracted()
        {
            Polygon proposed = Box(0, 0, 100);
            List<AutoPolygonizeSelection.CircleFootprint> circles =
            [
                Circle(1, 9, 50, 50, 10),     // inside the proposal
                Circle(2, 9, 105, 50, 10),    // straddles its edge
                Circle(3, 9, 400, 400, 10),   // far away
            ];

            List<Polygon> disks = AutoPolygonizeSelection.CollectOverlappingCircleDisks(circles, proposed, ownCenter: new Vector2(20, 20));

            Assert.AreEqual(2, disks.Count);
            Polygon? carved = AutoPolygonizeSelection.SubtractOverlappingPolygons(proposed, disks, new Vector2(20, 20));
            Assert.IsNotNull(carved);
            Assert.IsFalse(carved!.Contains(new Vector2(50, 50)), "the inner circle leaves a hole");
            Assert.IsFalse(carved.Contains(new Vector2(98, 50)), "the straddling circle bites the edge");
            Assert.IsTrue(carved.Contains(new Vector2(20, 20)));
        }

        [TestMethod]
        public void ACircleSmallerOrLargerStillWinsOverTheProposal()
        {
            Polygon proposed = Box(0, 0, 100);
            // A circle much larger than the proposal that overlaps it but does not enclose the proposal's own center.
            List<Polygon> disks = AutoPolygonizeSelection.CollectOverlappingCircleDisks(
                [Circle(1, 9, 200, 50, 160)],
                proposed,
                ownCenter: new Vector2(10, 50));

            Assert.AreEqual(1, disks.Count, "database annotations win whatever their size");
        }

        [TestMethod]
        public void ACircleThatEnclosesTheProposalsOwnCenterIsTheOuterCellAndIsLeftAlone()
        {
            List<Polygon> disks = AutoPolygonizeSelection.CollectOverlappingCircleDisks(
                [Circle(1, 9, 50, 50, 200)],
                Box(0, 0, 100),
                ownCenter: new Vector2(50, 50));

            Assert.AreEqual(0, disks.Count);
        }

        [TestMethod]
        public void SiblingsOwnLocationsOtherSectionsAndZeroRadiusAreSkipped()
        {
            Polygon proposed = Box(0, 0, 100);
            List<AutoPolygonizeSelection.CircleFootprint> circles =
            [
                Circle(1, 5, 50, 50, 10),                 // same structure
                Circle(2, 9, 50, 50, 10),                 // one of the proposal's own locations
                Circle(3, 9, 50, 50, 10, section: 2),     // another section
                Circle(4, 9, 50, 50, 0),                  // no size
            ];

            List<Polygon> disks = AutoPolygonizeSelection.CollectOverlappingCircleDisks(
                circles, proposed, excludeLocationIds: [2], sectionNumber: 1, excludeParentId: 5, ownCenter: new Vector2(10, 10));

            Assert.AreEqual(0, disks.Count);
        }

        [TestMethod]
        public void ADiskIsAClosedRingAroundItsCenter()
        {
            Polygon disk = AutoPolygonizeSelection.CircleToPolygon(new Vector2(10, 20), 5);

            Assert.IsTrue(disk.Contains(new Vector2(10, 20)));
            Assert.IsTrue(disk.Contains(new Vector2(13, 20)));
            Assert.IsFalse(disk.Contains(new Vector2(16, 20)));
            Assert.AreEqual(Math.PI * 25, disk.Area, 1.0);
        }
    }
}
