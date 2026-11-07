using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.ViewModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins who may delete a structure from its context menu and what the confirmation says (decision
    /// D-StructureDeleteSave): the structure's own user, or anyone with Review access; the question names the structure
    /// and how many locations the server will erase with it.
    /// </summary>
    [TestClass]
    public class StructureDeletePromptTests
    {
        [DataTestMethod]
        [DataRow("jamesan", "jamesan", false, true)]
        [DataRow("JamesAn", "jamesan", false, true)]
        [DataRow(" jamesan ", "jamesan", false, true)]
        [DataRow("jamesan", "other", false, false)]
        [DataRow("jamesan", "other", true, true)]
        [DataRow(null, "jamesan", false, false)]
        [DataRow("", "", false, false)]
        [DataRow("   ", "   ", false, false)]
        [DataRow("jamesan", null, false, false)]
        [DataRow(null, null, true, true)]
        public void MayDelete_OwnStructureOrReviewAccess(string? structureUser, string? currentUser, bool review, bool expected) =>
            Assert.AreEqual(expected, StructureDeletePrompt.MayDelete(structureUser, currentUser, review));

        /// <summary>Review access always allows; without it only a matching, non-blank user does.</summary>
        [TestMethod]
        public void MayDelete_Property()
        {
            Prop.ForAll(Arb.From<string>(), Arb.From<string>(), Arb.From<bool>(), (structureUser, currentUser, review) =>
            {
                bool same = !string.IsNullOrWhiteSpace(structureUser) && !string.IsNullOrWhiteSpace(currentUser) &&
                            string.Equals(structureUser.Trim(), currentUser.Trim(), System.StringComparison.OrdinalIgnoreCase);
                return StructureDeletePrompt.MayDelete(structureUser, currentUser, review) == (review || same);
            }).QuickCheckThrowOnFailure();
        }

        [DataTestMethod]
        [DataRow("Cell", 180L, "AII", "Cell #180 \"AII\"")]
        [DataRow("Cell", 180L, null, "Cell #180")]
        [DataRow("Cell", 180L, "  ", "Cell #180")]
        [DataRow(null, 7L, null, "Structure #7")]
        public void Name_TypeIdAndLabel(string? type, long id, string? label, string expected) =>
            Assert.AreEqual(expected, StructureDeletePrompt.Name(type, id, label));

        [DataTestMethod]
        [DataRow(0L, "Delete Cell #180 and its 0 locations?")]
        [DataRow(1L, "Delete Cell #180 and its 1 location?")]
        [DataRow(2L, "Delete Cell #180 and its 2 locations?")]
        [DataRow(123456L, "Delete Cell #180 and its 123456 locations?")]
        public void Question_NamesStructureAndLocationCount(long count, string firstLine)
        {
            string question = StructureDeletePrompt.Question("Cell #180", count);

            StringAssert.StartsWith(question, firstLine + "\n\n");
            StringAssert.Contains(question, "cannot be undone");
        }

        [TestMethod]
        public void RefusalMessage_NamesOwnerAndReviewAccess()
        {
            string message = StructureDeletePrompt.RefusalMessage("Cell #180", "jamesan");

            StringAssert.StartsWith(message, "Cell #180 was last edited by jamesan.");
            StringAssert.Contains(message, "Review access");
        }

        [TestMethod]
        public void RefusalMessage_BlankOwner() =>
            StringAssert.StartsWith(StructureDeletePrompt.RefusalMessage("Cell #180", " "), "Cell #180 has no recorded user.");
    }
}
