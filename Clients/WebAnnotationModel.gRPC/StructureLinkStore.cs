using Geometry;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.AnnotationServiceTypes;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC
{

    internal class StructureLinkStore : StoreBaseWithKey<StructureLinkKey, StructureLinkObj, IStructureLink, StructureLinkObj, IStructureLink>, IStructureLinkStore 
    { 
        public StructureLinkStore(
            IServerAnnotationsClientFactory<IServerAnnotationsClient<StructureLinkKey, IStructureLink, StructureLinkObj, IStructureLink>> clientFactory,
            IObjectConverter<StructureLinkObj, IStructureLink> objToServerObjConverter,
            IObjectConverter<IStructureLink, StructureLinkObj> serverObjToObjConverter,
            IObjectUpdater<StructureLinkObj, IStructureLink> objUpdater = null) : base(clientFactory, null, objToServerObjConverter, serverObjToObjConverter)
        {
        }

        protected override Task Init() => Task.CompletedTask;
        
        public async Task<StructureLinkObj[]> GetLinks(long structureId)
        {
            var client = (StructureLinksClient)ClientFactory.GetOrCreate();
            var serverLinks = await client.GetLinksForStructureAsync(structureId, CancellationToken.None);
            var changes = await ServerQueryResultsHandler.ProcessServerUpdate(
                new ServerUpdate<StructureLinkKey, IStructureLink[]>(DateTime.UtcNow, serverLinks, Array.Empty<StructureLinkKey>()));
            await CallOnCollectionChanged(changes).ConfigureAwait(false);
            return changes.ObjectsInStore.ToArray();
        }

        public async Task MergeServerLinksAsync(IEnumerable<IStructureLink> links, DateTime? queryTime = null, CancellationToken token = default)
        {
            var arr = links?.Where(l => l != null).ToArray() ?? Array.Empty<IStructureLink>();
            if (arr.Length == 0)
                return;

            token.ThrowIfCancellationRequested();
            var changes = await ServerQueryResultsHandler.ProcessServerUpdate(
                new ServerUpdate<StructureLinkKey, IStructureLink[]>(
                    queryTime ?? DateTime.UtcNow, arr, Array.Empty<StructureLinkKey>()))
                .ConfigureAwait(false);
            await CallOnCollectionChanged(changes).ConfigureAwait(false);
        }

        /// <summary>
        /// Synchronous wrapper so exceptions surface to the caller's try/catch, matching legacy WCF-store semantics.
        /// Creates the link on the server immediately (UI flip/delete paths also call Save for deletes).
        /// </summary>
        public StructureLinkObj Create(StructureLinkObj obj)
        {
            var client = ClientFactory.GetOrCreate();
            var serverResult = client.Create(obj, CancellationToken.None).Result;
            return Add(ServerObjConverter.Convert(serverResult)).Result;
        }

        /// <summary>
        /// Structure-link RPCs are create / update-upsert / delete — not a single batched Update that
        /// understands DBACTION. Route each pending change to the matching RPC so Remove()+Save()
        /// (used by Viking's structure-link context menu) actually deletes on the server.
        /// </summary>
        protected override async Task<bool> Save(List<StructureLinkObj> changedObjects, CancellationToken token)
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
                        throw new NotSupportedException($"Unexpected structure-link DBAction {obj.DBAction}");
                }

                obj.DBAction = DBACTION.NONE;
            }

            return true;
        }
    }
}
