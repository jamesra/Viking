using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Viking.AnnotationServiceTypes;
using VikingXNAGraphics;
using WebAnnotation;
using WebAnnotationModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the hit-test ranking: the three <see cref="HitTestResult"/> sorters and the choice order of
    /// <see cref="HitTestResultExtensions.NearestObjectOnCurrentSectionThenAdjacent"/> (current-section locations
    /// containing the point and structure links, then adjacent sections by distance, then the rest).
    /// The location link opt-in rule is pinned in <c>HitTestLocationLinkPreferenceTests</c>.
    /// </summary>
    [TestClass]
    public class HitTestSelectionTests
    {
        private const int Section = 5;

        // ---- Sorters ----

        [TestMethod]
        public void ZDistanceSorter_LowerZFirstThenNearer_IgnoresHeight()
        {
            var sorter = new HitTest_Z_Distance_Sorter();
            Assert.IsTrue(sorter.Compare(Loc(4, 0, 0.9), Loc(5, 0, 0.1)) < 0, "lower Z wins over distance");
            Assert.IsTrue(sorter.Compare(Loc(5, 0, 0.1), Loc(5, 0, 0.9)) < 0, "nearer wins at equal Z");
            Assert.AreEqual(0, sorter.Compare(Loc(5, 1, 0.5), Loc(5, 9, 0.5)), "visual height is ignored");
        }

        [TestMethod]
        public void ZDepthDistanceSorter_ZThenTallestThenNearest()
        {
            var sorter = new HitTest_Z_Depth_Distance_Sorter();
            Assert.IsTrue(sorter.Compare(Loc(4, 0, 0.9), Loc(5, 9, 0.1)) < 0, "lower Z wins over height");
            Assert.IsTrue(sorter.Compare(Loc(5, 9, 0.9), Loc(5, 1, 0.1)) < 0, "taller sorts first at equal Z");
            Assert.IsTrue(sorter.Compare(Loc(5, 3, 0.1), Loc(5, 3, 0.9)) < 0, "nearer wins at equal Z and height");
            Assert.AreEqual(0, sorter.Compare(Loc(5, 3, 0.5), Loc(5, 3, 0.5)));
        }

        [TestMethod]
        public void DistanceSorter_NearestThenTallest_IgnoresZ()
        {
            var sorter = new HitTest_Distance_Sorter();
            Assert.IsTrue(sorter.Compare(Loc(9, 0, 0.1), Loc(1, 9, 0.9)) < 0, "nearer wins over Z and height");
            Assert.IsTrue(sorter.Compare(Loc(1, 9, 0.5), Loc(1, 1, 0.5)) < 0, "taller sorts first at equal distance");
            Assert.AreEqual(0, sorter.Compare(Loc(1, 3, 0.5), Loc(9, 3, 0.5)), "Z is ignored");
        }

        [TestMethod]
        public void Sorters_AreAntisymmetric()
        {
            HitTestResult a = Loc(4, 2, 0.3);
            HitTestResult b = Loc(5, 7, 0.1);
            foreach (IComparer<HitTestResult> sorter in new IComparer<HitTestResult>[]
            {
                new HitTest_Z_Distance_Sorter(), new HitTest_Z_Depth_Distance_Sorter(), new HitTest_Distance_Sorter()
            })
            {
                Assert.AreEqual(-System.Math.Sign(sorter.Compare(a, b)), System.Math.Sign(sorter.Compare(b, a)));
            }
        }

        // ---- NearestObjectOnCurrentSectionThenAdjacent ----

        [TestMethod]
        public void EmptyList_ReturnsNull()
        {
            Assert.IsNull(new List<HitTestResult>().NearestObjectOnCurrentSectionThenAdjacent(Section));
        }

        [TestMethod]
        public void CurrentSection_TallerBeatsNearer()
        {
            HitTestResult tall = Loc(Section, 5, 0.9);
            HitTestResult near = Loc(Section, 1, 0.1);
            Assert.AreSame(tall, Pick(near, tall));
        }

        [TestMethod]
        public void CurrentSection_EqualHeightNearestWins()
        {
            HitTestResult near = Loc(Section, 2, 0.2);
            HitTestResult far = Loc(Section, 2, 0.8);
            Assert.AreSame(near, Pick(far, near));
        }

        [TestMethod]
        public void CurrentSection_DistanceExactlyOneStillCounts()
        {
            HitTestResult edge = Loc(Section, 0, 1.0);
            HitTestResult adjacent = Loc(Section + 1, 0, 0.1);
            Assert.AreSame(edge, Pick(adjacent, edge));
        }

        [TestMethod]
        public void ContainingCurrentSection_BeatsNearerAdjacentSection()
        {
            HitTestResult current = Loc(Section, 0, 0.9);
            HitTestResult adjacent = Loc(Section + 1, 9, 0.0);
            Assert.AreSame(current, Pick(adjacent, current));
        }

        [TestMethod]
        public void StructureLink_CompetesWithCurrentLocationsByHeight()
        {
            HitTestResult location = Loc(Section, 1, 0.5);
            HitTestResult taller = Link(Section, 4, 0.5);
            Assert.AreSame(taller, Pick(location, taller));

            HitTestResult shorter = Link(Section, 0, 0.1);
            Assert.AreSame(location, Pick(location, shorter));
        }

        [TestMethod]
        public void StructureLink_IsCandidateEvenWhenFarAndOnAnotherSection()
        {
            // Links are not filtered by distance or section: any link joins step 1 and beats the adjacent fallback.
            HitTestResult link = Link(Section + 3, 0, 5.0);
            HitTestResult adjacent = Loc(Section + 1, 0, 0.1);
            Assert.AreSame(link, Pick(adjacent, link));
        }

        [TestMethod]
        public void AdjacentFallback_NearestWinsThenTallest()
        {
            HitTestResult near = Loc(Section + 1, 0, 1.5);
            HitTestResult far = Loc(Section - 1, 9, 3.0);
            Assert.AreSame(near, Pick(far, near));

            HitTestResult tall = Loc(Section - 1, 9, 1.5);
            Assert.AreSame(tall, Pick(near, tall));
        }

        [TestMethod]
        public void AdjacentFallback_BeatsCurrentSectionLocationOutsideItsShape()
        {
            HitTestResult outside = Loc(Section, 9, 1.5);
            HitTestResult adjacent = Loc(Section + 1, 0, 8.0);
            Assert.AreSame(adjacent, Pick(outside, adjacent));
        }

        [TestMethod]
        public void OnlyCurrentSectionLocationsOutsideShape_UsesDepthOrder()
        {
            HitTestResult tall = Loc(Section, 5, 2.0);
            HitTestResult near = Loc(Section, 1, 1.1);
            Assert.AreSame(tall, Pick(near, tall));
        }

        [TestMethod]
        public void NoLocationsOrStructureLinks_FallsBackToZThenDistance()
        {
            HitTestResult low = Other(Section - 1, 9, 5.0);
            HitTestResult high = Other(Section, 0, 0.1);
            Assert.AreSame(low, Pick(high, low));

            HitTestResult nearer = Other(Section - 1, 0, 2.0);
            Assert.AreSame(nearer, Pick(low, nearer));
        }

        [TestMethod]
        public void ResultDoesNotDependOnInputOrder()
        {
            HitTestResult a = Loc(Section, 1, 0.5);
            HitTestResult b = Loc(Section, 3, 0.9);
            HitTestResult c = Link(Section, 2, 0.1);
            HitTestResult d = Loc(Section + 1, 0, 0.0);
            Assert.AreSame(b, Pick(a, b, c, d));
            Assert.AreSame(b, Pick(d, c, b, a));
        }

        private static HitTestResult Pick(params HitTestResult[] hits) =>
            new List<HitTestResult>(hits).NearestObjectOnCurrentSectionThenAdjacent(Section);

        private static HitTestResult Loc(int z, int height, double distance) => new(new StubLocation(), z, height, distance);

        private static HitTestResult Link(int z, int height, double distance) => new(new StubStructureLink(), z, height, distance);

        private static HitTestResult Other(int z, int height, double distance) => new(new StubOther(), z, height, distance);

        private class StubHit : IHitTesting
        {
            public Rectangle BoundingBox => default;

            public bool Contains(Vector2 Position) => true;
        }

        private sealed class StubLocation : StubHit, IViewLocation
        {
            public long ID => 1;
        }

        private sealed class StubStructureLink : StubHit, IViewStructureLink
        {
            public StructureLinkKey Key => new(1, 2, false);
        }

        private sealed class StubOther : StubHit
        {
        }
    }
}
