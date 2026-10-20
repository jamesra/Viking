using Viking.AnnotationServiceTypes.Interfaces;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;
using System.Threading.Tasks;
using System.Threading;

namespace WebAnnotationModel.gRPC
{ 
    /// <summary>
    /// Allowed type-to-type link rules. Loaded after StructureTypes so the updater
    /// can attach each rule to the type objects already in the store.
    /// </summary>
    public class PermittedStructureLinkStore : StoreBaseWithKey<PermittedStructureLinkKey, PermittedStructureLinkObj, IPermittedStructureLink, PermittedStructureLinkObj, IPermittedStructureLink>, IPermittedStructureLinkStore
    {
        public PermittedStructureLinkStore(IServerAnnotationsClientFactory<IServerAnnotationsClient<PermittedStructureLinkKey, IPermittedStructureLink, PermittedStructureLinkObj, IPermittedStructureLink>> clientFactory,
            IObjectConverter<PermittedStructureLinkObj, IPermittedStructureLink> objToServerObjConverter,
            IObjectConverter<IPermittedStructureLink, PermittedStructureLinkObj> serverObjToObjConverter,
            IQueryLogger log) : base(clientFactory, null, objToServerObjConverter, serverObjToObjConverter, log)
        {
        }

        /// <summary>
        /// Load the full permitted-link table at startup (small, fairly static) so structure-type
        /// UI has relation rules without an extra round-trip.
        /// </summary>
        protected override async Task Init()
        {
            var client = ClientFactory.GetOrCreate();
            if (!(client is PermittedStructureLinksClient concrete))
                return;

            var all = await concrete.GetAllAsync(CancellationToken.None).ConfigureAwait(false);
            if (all.Count == 0)
                return;

            var changes = await ServerQueryResultsHandler.ProcessServerUpdate(
                new ServerUpdate<PermittedStructureLinkKey, IPermittedStructureLink[]>(
                    DateTime.UtcNow, all.Cast<IPermittedStructureLink>().ToArray(), Array.Empty<PermittedStructureLinkKey>()))
                .ConfigureAwait(false);
            await CallOnCollectionChanged(changes).ConfigureAwait(false);
        }

        /// <summary>
        /// Queues a link that is not on the server yet (DBAction other than NONE) for the next Save. A link built with the
        /// editor's constructor is already INSERT before the store watches its DBAction.
        /// </summary>
        protected override void TrackForSaveIfPending(PermittedStructureLinkObj obj) =>
            QueueUnsavedLinks(new[] { obj }, ChangedObjects);

        /// <summary>
        /// Puts links that are not on the server yet (DBAction other than NONE) on the dirty set so the next
        /// <c>Save()</c> sends them. Links loaded from the server arrive with DBAction NONE and are skipped.
        /// Re-adding a link whose removal is still queued cancels that DELETE and leaves the new link NONE, because
        /// the server row still exists (a DELETE is only queued for saved links; see <see cref="Remove(PermittedStructureLinkKey)"/>).
        /// </summary>
        /// <param name="addedLinks">Links that were newly added to the store.</param>
        /// <param name="changedObjects">The store's dirty set, keyed by link.</param>
        internal static void QueueUnsavedLinks(IEnumerable<PermittedStructureLinkObj> addedLinks,
                                               ConcurrentDictionary<PermittedStructureLinkKey, PermittedStructureLinkObj> changedObjects)
        {
            foreach (PermittedStructureLinkObj link in addedLinks)
            {
                if (link.DBAction == DBACTION.NONE)
                    continue;

                if (link.DBAction == DBACTION.INSERT &&
                    changedObjects.TryGetValue(link.ID, out PermittedStructureLinkObj queued) &&
                    queued.DBAction == DBACTION.DELETE &&
                    changedObjects.TryRemove(link.ID, out _))
                {
                    link.DBAction = DBACTION.NONE;
                    continue;
                }

                changedObjects.TryAdd(link.ID, link);
            }
        }

        /// <summary>
        /// Removes a link from the store. A link added since the last Save (DBAction INSERT) is not on the server,
        /// so its queued INSERT is cancelled and nothing is sent; a saved link is queued for DELETE as usual.
        /// </summary>
        public override async Task<PermittedStructureLinkObj> Remove(PermittedStructureLinkKey ID)
        {
            if (IDToObject.TryGetValue(ID, out PermittedStructureLinkObj link) && CancelUnsavedInsert(link, ChangedObjects))
            {
                PermittedStructureLinkObj deleted = InternalDelete(ID);
                if (deleted != null)
                    CallOnCollectionChangedForDelete(deleted);
                return deleted;
            }

            return await base.Remove(ID).ConfigureAwait(false);
        }

        /// <inheritdoc cref="Remove(PermittedStructureLinkKey)"/>
        public override async Task<bool> Remove(PermittedStructureLinkObj obj) =>
            obj != null && await Remove(obj.ID).ConfigureAwait(false) != null;

        /// <summary>
        /// Drops a never-saved link's pending INSERT from the dirty set. Sending a DELETE instead would hit whatever
        /// server row shares its source and target (the server key ignores Bidirectional), or nothing at all.
        /// </summary>
        /// <returns>True if the link was unsaved (INSERT) and is no longer queued; false for a saved link.</returns>
        internal static bool CancelUnsavedInsert(PermittedStructureLinkObj link,
                                                 ConcurrentDictionary<PermittedStructureLinkKey, PermittedStructureLinkObj> changedObjects)
        {
            if (link.DBAction != DBACTION.INSERT)
                return false;

            changedObjects.TryRemove(link.ID, out _);
            return true;
        }

        /// <summary>
        /// Create uses a dedicated RPC; delete is Update+DBACTION.DELETE. Route Save the same way
        /// StructureLinkStore does so Remove()+Save() persists without relying on Update INSERT.
        /// </summary>
        protected override async Task<bool> Save(List<PermittedStructureLinkObj> changedObjects, CancellationToken token)
        {
            if (changedObjects.Count == 0)
                return true;

            var client = ClientFactory.GetOrCreate();
            foreach (var obj in changedObjects)
            {
                switch (obj.DBAction)
                {
                    case DBACTION.DELETE:
                        await client.Delete(obj.ID, token).ConfigureAwait(false);
                        break;
                    case DBACTION.INSERT:
                        await client.Create(obj, token).ConfigureAwait(false);
                        break;
                    case DBACTION.UPDATE:
                        await client.UpdateAsync(ClientObjConverter.Convert(obj), token).ConfigureAwait(false);
                        break;
                    case DBACTION.NONE:
                        break;
                    default:
                        throw new NotSupportedException($"Unexpected permitted-structure-link DBAction {obj.DBAction}");
                }

                obj.DBAction = DBACTION.NONE;
            }

            return true;
        }
    }
}
