using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using Viking.AnnotationServiceTypes;
using WebAnnotation.ViewModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// When a location on an adjacent section becomes hidden under (or visible from under) a location it links to on the
    /// viewed section, the section's other links to it are rebuilt so they end on what is drawn. Pins which links those are.
    /// </summary>
    [TestClass]
    public class LocationLinkRepointTests
    {
        private const int Section = 10;

        // Hidden adjacent location 1 (section 11) overlaps 2 on this section; 3 on this section and 4 on section 9 also link to 1.
        private static readonly Dictionary<long, (double Z, long[] Links)> Locations = new()
        {
            [1] = (11, [2, 3, 4]),
            [2] = (10, [1]),
            [3] = (10, [1]),
            [4] = (9, [1]),
        };

        private static List<LocationLinkKey> Repoint(LocationLinkKey changed, params LocationLinkKey[] known) =>
            [.. SectionLocationLinkAnnotationsViewModel.LinksToRepoint(changed, Section,
                id => Locations.TryGetValue(id, out var l) ? l : null,
                key => known.Contains(key))];

        [TestMethod]
        public void TheOtherKnownLinksOfTheAdjacentEndAreRebuilt()
        {
            List<LocationLinkKey> repoint = Repoint(new LocationLinkKey(1, 2), new(1, 2), new(1, 3), new(1, 4));

            CollectionAssert.AreEquivalent(new[] { new LocationLinkKey(1, 3), new LocationLinkKey(1, 4) }, repoint);
        }

        [TestMethod]
        public void LinksTheSectionDoesNotTrackAreLeftAlone() =>
            CollectionAssert.AreEquivalent(new[] { new LocationLinkKey(1, 3) }, Repoint(new LocationLinkKey(1, 2), new(1, 2), new(1, 3)));

        /// <summary>Only the end off this section changes how it is drawn; links of the end on this section stay.</summary>
        [TestMethod]
        public void TheEndOnThisSectionIsNotAFactor() =>
            Assert.IsFalse(Repoint(new LocationLinkKey(1, 2), new(1, 2), new(1, 3), new(1, 4)).Any(k => k.A == 2 || k.B == 2));

        /// <summary>Equals(object) used to call System.Object.Equals(this, obj), which recursed until the stack overflowed.</summary>
        [TestMethod]
        public void ALinkKeyComparesAsAnObject()
        {
            object key = new LocationLinkKey(1, 2);
            Assert.IsTrue(key.Equals(new LocationLinkKey(2, 1)));
            Assert.IsFalse(key.Equals(new LocationLinkKey(1, 3)));
            Assert.IsFalse(key.Equals(null));
        }

        [TestMethod]
        public void AnEndThatIsNotLoadedRebuildsNothing() =>
            Assert.AreEqual(0, Repoint(new LocationLinkKey(5, 2), new LocationLinkKey(5, 2)).Count);
    }
}
