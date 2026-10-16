using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using Viking.SectionCorrectionBuilder;

namespace GrpcSectionCorrectionService.Tests
{
    [TestClass]
    public class SectionRangesAndFiltersTests
    {
        [TestMethod]
        public void ToRanges_MergesAdjacentSections()
        {
            CollectionAssert.AreEqual(
                new[] { (1L, 3L), (7L, 7L), (10L, 11L) },
                SectionRanges.ToRanges([3, 1, 2, 7, 11, 10, 2]));
        }

        [TestMethod]
        public void Widen_ReturnsAscendingNonOverlappingRanges()
        {
            List<(long Lo, long Hi)> ranges = SectionRanges.Widen([30, 0, 10, 12], 2);

            Assert.AreEqual((-2L, 2L), ranges[0]);
            Assert.AreEqual((8L, 14L), ranges[1], "10 and 12 widen to 8..12 and 10..14, which merge");
            Assert.AreEqual((28L, 32L), ranges[2]);
        }

        [TestMethod]
        public void Batches_SplitsWithoutLosingValues()
        {
            List<List<long>> batches = [.. SectionRanges.Batches(new List<long> { 1, 2, 3, 4, 5 }, 2)];

            Assert.AreEqual(3, batches.Count);
            CollectionAssert.AreEqual(new long[] { 1, 2, 3, 4, 5 }, batches.SelectMany(b => b).ToArray());
        }

        [TestMethod]
        public void ODataFilters_AreInvariantCultureAndJoinedWithOr()
        {
            Assert.AreEqual("(Z ge 1 and Z le 3) or (Z ge 9 and Z le 9)", ODataMorphologyLoader.RangeFilter([(1, 3), (9, 9)]));
            Assert.AreEqual("ParentID eq 1000 or ParentID eq 2", ODataMorphologyLoader.ParentFilter([1000, 2]));
        }
    }
}
