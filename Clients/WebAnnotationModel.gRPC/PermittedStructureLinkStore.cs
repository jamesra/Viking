using Viking.AnnotationServiceTypes.Interfaces;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes;
using System;
using System.Collections.Generic;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;
using System.Threading.Tasks;
using System.Threading;

namespace WebAnnotationModel.gRPC
{ 
    public class PermittedStructureLinkStore : StoreBaseWithKey<PermittedStructureLinkKey, PermittedStructureLinkObj, IPermittedStructureLink, PermittedStructureLinkObj, IPermittedStructureLink>, IPermittedStructureLinkStore
    {
        public PermittedStructureLinkStore(IServerAnnotationsClientFactory<IServerAnnotationsClient<PermittedStructureLinkKey, IPermittedStructureLink, PermittedStructureLinkObj, IPermittedStructureLink>> clientFactory,
            IObjectConverter<PermittedStructureLinkObj, IPermittedStructureLink> objToServerObjConverter,
            IObjectConverter<IPermittedStructureLink, PermittedStructureLinkObj> serverObjToObjConverter,
            IQueryLogger log) : base(clientFactory, null, objToServerObjConverter, serverObjToObjConverter, log)
        {
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
