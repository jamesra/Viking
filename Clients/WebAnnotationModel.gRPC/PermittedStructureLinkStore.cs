using Viking.AnnotationServiceTypes.Interfaces;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes;
using System;
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
