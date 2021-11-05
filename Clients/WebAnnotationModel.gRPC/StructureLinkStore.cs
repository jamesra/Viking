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
            IStoreServerQueryResultsHandler<StructureLinkKey, StructureLinkObj, IStructureLink> serverQueryResultsHandler,
            IObjectConverter<StructureLinkObj, IStructureLink> objToServerObjConverter,
            IObjectConverter<IStructureLink, StructureLinkObj> serverObjToObjConverter,
            IObjectUpdater<StructureLinkObj, IStructureLink> objUpdater = null) : base(clientFactory, serverQueryResultsHandler, objToServerObjConverter, serverObjToObjConverter)
        {
        }

        protected override Task Init() => Task.CompletedTask;
        
        public Task<StructureLinkObj[]> GetLinks(long structureId)
        {
            throw new NotImplementedException();
        }
         
    }
}
