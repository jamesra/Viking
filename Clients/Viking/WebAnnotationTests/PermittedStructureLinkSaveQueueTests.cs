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

        /// <summary>
        /// Remove then re-add of a saved link before Apply: the queued DELETE is cancelled and the new link is left
        /// NONE, so Save sends nothing and the server row stays. Before, the DELETE kept the slot, the INSERT was
        /// dropped, and Apply deleted the row while the editor still showed the link.
        /// </summary>
        [DataTestMethod]
        [DataRow(3L, 7L, false, DisplayName = "Directional")]
        [DataRow(9L, 2L, true, DisplayName = "Bidirectional, higher ID first")]
        public void ReAddAfterRemovingSavedLink_CancelsDelete(long source, long target, bool bidirectional)
        {
            PermittedStructureLinkObj saved = new(new PermittedStructureLink
            {
                SourceTypeID = bidirectional ? System.Math.Min(source, target) : source,
                TargetTypeID = bidirectional ? System.Math.Max(source, target) : target,
                Bidirectional = bidirectional
            });
            saved.DBAction = DBACTION.DELETE;
            var queue = NewQueue();
            queue.TryAdd(saved.ID, saved);

            PermittedStructureLinkObj readded = new(source, target, bidirectional);
            PermittedStructureLinkStore.QueueUnsavedLinks([readded], queue);

            Assert.AreEqual(0, queue.Count);
            Assert.AreEqual(DBACTION.NONE, readded.DBAction);
        }

        /// <summary>A queued DELETE for a different direction of the same types is a different key and is kept.</summary>
        [TestMethod]
        public void AddWithOtherDirection_KeepsDeleteOfOriginal()
        {
            PermittedStructureLinkObj saved = new(new PermittedStructureLink { SourceTypeID = 1, TargetTypeID = 2, Bidirectional = false });
            saved.DBAction = DBACTION.DELETE;
            var queue = NewQueue();
            queue.TryAdd(saved.ID, saved);

            PermittedStructureLinkObj added = new(1, 2, true);
            PermittedStructureLinkStore.QueueUnsavedLinks([added], queue);

            Assert.AreEqual(2, queue.Count);
            Assert.AreEqual(DBACTION.INSERT, added.DBAction);
            Assert.AreSame(saved, queue[saved.ID]);
        }

        /// <summary>Removing a link added since the last Save cancels its INSERT, so Apply sends nothing for it.</summary>
        [TestMethod]
        public void RemoveUnsavedLink_CancelsInsert()
        {
            PermittedStructureLinkObj added = new(6, 8, false);
            var queue = NewQueue();
            PermittedStructureLinkStore.QueueUnsavedLinks([added], queue);

            Assert.IsTrue(PermittedStructureLinkStore.CancelUnsavedInsert(added, queue));
            Assert.AreEqual(0, queue.Count);
        }

        /// <summary>Removing a saved link is not cancelled here; the store queues its DELETE.</summary>
        [TestMethod]
        public void RemoveSavedLink_IsNotCancelled()
        {
            PermittedStructureLinkObj saved = new(new PermittedStructureLink { SourceTypeID = 4, TargetTypeID = 5 });
            PermittedStructureLinkObj other = new(6, 8, false);
            var queue = NewQueue();
            queue.TryAdd(other.ID, other);

            Assert.IsFalse(PermittedStructureLinkStore.CancelUnsavedInsert(saved, queue));
            Assert.AreEqual(1, queue.Count);
        }

        /// <summary>
        /// Add, remove, re-add before Apply: the first INSERT is cancelled, so the re-added link is queued as INSERT
        /// (not turned NONE as if a server row existed) and Apply creates it.
        /// </summary>
        [TestMethod]
        public void AddRemoveReAdd_QueuesInsert()
        {
            var queue = NewQueue();
            PermittedStructureLinkObj first = new(6, 8, false);
            PermittedStructureLinkStore.QueueUnsavedLinks([first], queue);
            PermittedStructureLinkStore.CancelUnsavedInsert(first, queue);

            PermittedStructureLinkObj second = new(6, 8, false);
            PermittedStructureLinkStore.QueueUnsavedLinks([second], queue);

            Assert.AreEqual(DBACTION.INSERT, second.DBAction);
            Assert.AreSame(second, queue[second.ID]);
        }
    }
}
