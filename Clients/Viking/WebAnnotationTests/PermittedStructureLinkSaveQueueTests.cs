using AnnotationService.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Concurrent;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins which permitted links the store queues for its next Save. The structure type editor (F7) builds a new
    /// link as INSERT before adding it to the store, which only watches DBAction changes made after the add, so the
    /// store must queue it on add or Apply never creates it on the server. Links loaded from the server are not queued.
    /// </summary>
    [TestClass]
    public class PermittedStructureLinkSaveQueueTests
    {
        private static ConcurrentDictionary<PermittedStructureLinkKey, PermittedStructureLinkObj> NewQueue() => new();

        /// <summary>A link made in the editor (directional or bidirectional) is INSERT and is queued under its key.</summary>
        [DataTestMethod]
        [DataRow(3L, 7L, false, DisplayName = "Directional")]
        [DataRow(9L, 2L, true, DisplayName = "Bidirectional, higher ID first")]
        public void NewEditorLink_IsQueued(long source, long target, bool bidirectional)
        {
            PermittedStructureLinkObj link = new(source, target, bidirectional);
            var queue = NewQueue();

            PermittedStructureLinkStore.QueueUnsavedLinks([link], queue);

            Assert.AreEqual(DBACTION.INSERT, link.DBAction);
            Assert.AreEqual(1, queue.Count);
            Assert.AreSame(link, queue[new PermittedStructureLinkKey(source, target, bidirectional)]);
        }

        /// <summary>A link loaded from the server (DBAction NONE) is not queued, so loading types sends nothing.</summary>
        [TestMethod]
        public void ServerLink_IsNotQueued()
        {
            PermittedStructureLinkObj link = new(new PermittedStructureLink { SourceTypeID = 1, TargetTypeID = 2, Bidirectional = false });
            var queue = NewQueue();

            PermittedStructureLinkStore.QueueUnsavedLinks([link], queue);

            Assert.AreEqual(DBACTION.NONE, link.DBAction);
            Assert.AreEqual(0, queue.Count);
        }

        /// <summary>A mix queues only the unsaved links; an entry already queued for the same key is kept.</summary>
        [TestMethod]
        public void Mixed_QueuesOnlyUnsaved_AndKeepsExistingEntry()
        {
            PermittedStructureLinkObj loaded = new(new PermittedStructureLink { SourceTypeID = 4, TargetTypeID = 5 });
            PermittedStructureLinkObj added = new(6, 8, false);
            PermittedStructureLinkObj alreadyQueued = new(1, 2, true);
            PermittedStructureLinkObj sameKey = new(2, 1, true);
            var queue = NewQueue();
            queue.TryAdd(alreadyQueued.ID, alreadyQueued);

            PermittedStructureLinkStore.QueueUnsavedLinks([loaded, added, sameKey], queue);

            Assert.AreEqual(2, queue.Count);
            Assert.AreSame(added, queue[added.ID]);
            Assert.AreSame(alreadyQueued, queue[alreadyQueued.ID]);
            Assert.IsFalse(queue.ContainsKey(loaded.ID));
        }
    }
}
