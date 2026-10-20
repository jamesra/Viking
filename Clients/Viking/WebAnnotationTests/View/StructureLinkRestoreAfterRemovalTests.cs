using AnnotationService.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotationTests.View
{
    /// <summary>
    /// Pins which structures get their link views rebuilt after locations are deleted
    /// (<see cref="SectionStructureLinkAnnotationsViewModel.StructureIDsToRefreshAfterRemoval"/>). Deleting one location
    /// drops the view of every link of its structure, so without the rebuild a structure link vanished from the section
    /// while both structures still had locations there. The view itself needs a loaded section, so only the selection
    /// is tested here; locations are built from plain data so no store or server is touched.
    /// </summary>
    [TestClass]
    public class StructureLinkRestoreAfterRemovalTests
    {
        private const int Section = 250;

        private static LocationObj Loc(long id, long parent, int section) =>
            new(new Location { ID = id, ParentID = parent, Position = new AnnotationPoint(0, 0, section) });

        [TestMethod]
        public void EachStructureOfTheRemovedLocationsOnTheSectionIsRefreshedOnce()
        {
            LocationObj[] removed = [Loc(1, 100, Section), Loc(2, 100, Section), Loc(3, 200, Section), Loc(4, 100, Section)];

            CollectionAssert.AreEquivalent(new long[] { 100, 200 },
                SectionStructureLinkAnnotationsViewModel.StructureIDsToRefreshAfterRemoval(removed, Section).ToArray());
        }

        /// <summary>
        /// A location deleted on an adjacent section does not drop this section's structure links, so its structure
        /// needs no rebuild here.
        /// </summary>
        [TestMethod]
        public void LocationsOnOtherSectionsAreSkipped()
        {
            LocationObj[] removed = [Loc(1, 100, Section + 1), Loc(2, 200, Section), Loc(3, 300, Section - 1)];

            CollectionAssert.AreEqual(new long[] { 200 },
                SectionStructureLinkAnnotationsViewModel.StructureIDsToRefreshAfterRemoval(removed, Section).ToArray());
        }
    }
}
