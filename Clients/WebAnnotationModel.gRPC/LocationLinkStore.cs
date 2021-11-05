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

    internal class LocationLinkStore : StoreBaseWithKey<LocationLinkKey, LocationLinkObj, ILocationLink, ILocationLink, ILocationLink>, ILocationLinkStore
    { 
        public LocationLinkStore(
            IServerAnnotationsClientFactory<IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink>> clientFactory,
            IStoreServerQueryResultsHandler<LocationLinkKey, LocationLinkObj, ILocationLink> serverQueryResultsHandler,
            IObjectConverter<LocationLinkObj, ILocationLink> objToServerObjConverter,
            IObjectConverter<ILocationLink, LocationLinkObj> serverObjToObjConverter,
            IQueryLogger log) : base(clientFactory, serverQueryResultsHandler, objToServerObjConverter, serverObjToObjConverter, log)
        {
        }

        protected override Task Init() => Task.CompletedTask;
        
        public Task<StructureLinkObj[]> GetLinks(long structureId)
        {
            throw new NotImplementedException();
        }
         
    }
}
