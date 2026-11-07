using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Serialization;
using WebAnnotation.WPF.Forms;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins what closing the F7 Structure Type Manager with the title-bar X asks (decision D-StructureTypeCloseSemantics):
    /// nothing when no edit is pending, otherwise a Yes (save) / No (close, keep pending) question naming how many
    /// structure types and link rules are unsaved, read from the stores' <see cref="PermittedStructureLinkStore.PendingChangeCount"/>.
    /// </summary>
    [TestClass]
    public class StructureTypeManagerClosePromptTests
    {
        [TestMethod]
        public void NothingPending_ClosesWithoutAsking() => Assert.IsNull(StructureTypeManagerClosePrompt.Question(0, 0));

        [DataTestMethod]
        [DataRow(1, 0, "You have unsaved changes to 1 structure type.")]
        [DataRow(2, 0, "You have unsaved changes to 2 structure types.")]
        [DataRow(0, 1, "You have unsaved changes to 1 link rule.")]
        [DataRow(1, 1, "You have unsaved changes to 1 structure type and 1 link rule.")]
        [DataRow(3, 4, "You have unsaved changes to 3 structure types and 4 link rules.")]
        public void Pending_QuestionNamesCounts(int types, int links, string firstLine)
        {
            string? question = StructureTypeManagerClosePrompt.Question(types, links);

            Assert.IsNotNull(question);
            StringAssert.StartsWith(question, firstLine + "\n\n");
            StringAssert.Contains(question, "Yes saves them now");
            StringAssert.Contains(question, "No closes this window and keeps them pending");
        }

        /// <summary>Asks exactly when something is pending, and names only the kinds that are.</summary>
        [TestMethod]
        public void AsksIffPending_AndNamesOnlyPendingKinds()
        {
            Prop.ForAll(Gen.Choose(0, 100_000).ToArbitrary(), Gen.Choose(0, 100_000).ToArbitrary(), (types, links) =>
            {
                string? question = StructureTypeManagerClosePrompt.Question(types, links);
                if (types == 0 && links == 0)
                    return question is null;

                return question is not null &&
                       question.Contains(" structure type") == (types > 0) &&
                       question.Contains(" link rule") == (links > 0) &&
                       (types == 0 || question.Contains($" {types} structure type")) &&
                       (links == 0 || question.Contains($" {links} link rule"));
            }).QuickCheckThrowOnFailure();
        }

        /// <summary>
        /// The count the prompt reads is the store's dirty set: a link added in the editor is counted until saved. The
        /// store is built without its constructor, which needs a server endpoint.
        /// </summary>
        [TestMethod]
        public void PendingChangeCount_CountsTheDirtySet()
        {
            PermittedStructureLinkStore store = (PermittedStructureLinkStore)FormatterServices.GetUninitializedObject(typeof(PermittedStructureLinkStore));
            ConcurrentDictionary<PermittedStructureLinkKey, PermittedStructureLinkObj> dirty = new();
            typeof(PermittedStructureLinkStore).BaseType!
                .GetField("ChangedObjects", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(store, dirty);

            Assert.AreEqual(0, store.PendingChangeCount);

            PermittedStructureLinkStore.QueueUnsavedLinks([new PermittedStructureLinkObj(3, 7, false), new PermittedStructureLinkObj(9, 2, true)], dirty);
            Assert.AreEqual(2, store.PendingChangeCount);
        }
    }
}
