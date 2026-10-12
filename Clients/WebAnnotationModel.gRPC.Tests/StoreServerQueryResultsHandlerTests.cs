using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Pins how <see cref="StoreServerQueryResultsHandler{KEY, OBJECT, SERVER_OBJECT}"/> reports server deletions.
    /// Runs without a server; the store is a hand-written in-memory <see cref="IStoreEditor{KEY, OBJECT}"/>.
    /// </summary>
    [TestFixture]
    public class StoreServerQueryResultsHandlerTests
    {
        /// <summary>
        /// Dictionary-backed editor. Only <see cref="TryRemoveObject"/> is exercised by delete-only updates.
        /// </summary>
        private sealed class FakeEditor : IStoreEditor<long, StructureObj>
        {
            public readonly Dictionary<long, StructureObj> Items = new Dictionary<long, StructureObj>();

            public bool TryAddObject(StructureObj newObj) => Items.TryAdd(newObj.ID, newObj);

            public bool TryGetObject(long ID, out StructureObj obj) => Items.TryGetValue(ID, out obj);

            public StructureObj GetOrAdd(long key, Func<long, StructureObj> valueFactory) => throw new NotSupportedException();

            public StructureObj TryRemoveObject(long key) => Items.Remove(key, out var removed) ? removed : null;

            public Task EndBatch(ChangeInventory<StructureObj> changes) => Task.CompletedTask;
        }

        /// <summary>
        /// The server's deleted-ID list covers objects this client never loaded. Those IDs must not surface as
        /// null entries in <c>DeletedObjects</c>, which become null <c>OldItems</c> in the Remove event and
        /// crash subscribers that dereference each item (the WCF store skips them too).
        /// </summary>
        [Test]
        public async Task DeletedIdsNeverLoaded_AreNotReportedAsNullDeletedObjects()
        {
            var editor = new FakeEditor();
            var loaded = new StructureObj(1, 1);
            editor.Items[1] = loaded;
            var handler = new StoreServerQueryResultsHandler<long, StructureObj, IStructure>(editor, null);

            var inventory = await handler.ProcessServerUpdate(Array.Empty<IStructure>(), new long[] { 1, 2, 3 });

            Assert.That(inventory.DeletedObjects, Is.EqualTo(new[] { loaded }));
            Assert.That(editor.Items, Is.Empty);
        }

        /// <summary>All deleted IDs unknown to the client yields an empty deleted list, so no Remove event is raised.</summary>
        [Test]
        public async Task OnlyUnknownDeletedIds_YieldsEmptyDeletedObjects()
        {
            var handler = new StoreServerQueryResultsHandler<long, StructureObj, IStructure>(new FakeEditor(), null);

            var inventory = await handler.ProcessServerUpdate(Array.Empty<IStructure>(), new long[] { 7, 8 });

            Assert.That(inventory.DeletedObjects, Is.Empty);
        }
    }
}
