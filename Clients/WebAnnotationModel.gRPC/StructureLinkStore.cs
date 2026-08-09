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
            CallOnCollectionChanged(changes);
            return changes.ObjectsInStore.ToArray();
        }

        /// <summary>
        /// Synchronous wrapper so exceptions surface to the caller's try/catch, matching legacy WCF-store semantics.
        /// Creates the link on the server immediately rather than deferring to Save(), since there is no
        /// dedicated delete RPC to reconcile against on a later save.
        /// </summary>
        public StructureLinkObj Create(StructureLinkObj obj)
        {
            var client = ClientFactory.GetOrCreate();
            var serverResult = client.Create(obj, CancellationToken.None).Result;
            return Add(ServerObjConverter.Convert(serverResult)).Result;
        }
    }
}
