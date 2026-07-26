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
            IStoreServerQueryResultsHandler<PermittedStructureLinkKey, PermittedStructureLinkObj, IPermittedStructureLink> serverQueryResultsHandler,
            IObjectConverter<PermittedStructureLinkObj, IPermittedStructureLink> objToServerObjConverter,
            IObjectConverter<IPermittedStructureLink, PermittedStructureLinkObj> serverObjToObjConverter,
            IQueryLogger log) : base(clientFactory, serverQueryResultsHandler, objToServerObjConverter, serverObjToObjConverter, log)
        {
        }
         
    }
}
