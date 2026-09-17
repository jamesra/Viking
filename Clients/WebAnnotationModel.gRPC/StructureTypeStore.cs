using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.Objects;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using WebAnnotationModel; 
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC
{
    /// <summary>
    /// Client cache of the structure-type table. Loaded in full at startup.
    /// </summary>
    public class StructureTypeStore : StoreBaseWithKeyAndParent<long, StructureTypeObj,
                                        IStructureType, IStructureType, IStructureType>,
                                        IStructureTypeStore
    {
        private IServerAnnotationsClientFactory<IStructureTypesRepository> _structureTypeClientFactory;

        public StructureTypeStore(IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructureType, IStructureType, IStructureType>> clientFactory,
            IServerAnnotationsClientFactory<IStructureTypesRepository> structureTypeClientFactory,
            IObjectConverter<StructureTypeObj, IStructureType> objToServerObjConverter,
            IObjectConverter<IStructureType, StructureTypeObj> serverObjToObjConverter,
            IObjectUpdater<StructureTypeObj, IStructureType> objUpdater = null) : base(clientFactory, null, objToServerObjConverter, serverObjToObjConverter)
        {
            _structureTypeClientFactory = structureTypeClientFactory;
        }

        protected override Task Init()
        {
            return GetAll();
        }


        public async Task<StructureTypeObj> Create(StructureTypeObj new_type, CancellationToken token = default)
        {
            var client = ClientFactory.GetOrCreate();

            StructureTypeObj createdStructureType = null;
            try
            { 
                if (token.IsCancellationRequested)
                    return null;

                var serverObj = ClientObjConverter.Convert(new_type);
                var createdType = await client.Create(serverObj, token);
                if (createdType == null)
                    return null;

                createdStructureType = ServerObjConverter.Convert(createdType);
                await Add(createdStructureType);

                return createdStructureType;
            }
            finally
            {
                
            } 
        }

        /// <summary>
        /// Loads the entire type table. Failures must surface — swallowing them leaves StructureObj.Type null.
        /// Uses this.CallOnCollectionChanged (not EndBatch) so RootObjects and Children are wired.
        /// </summary>
        public async Task<ICollection<StructureTypeObj>> GetAll()
        {
            var client = _structureTypeClientFactory.GetOrCreate();
            var response = await client.GetAll().ConfigureAwait(false) ?? Array.Empty<IStructureType>();
            var changes = await ServerQueryResultsHandler.ProcessServerUpdate(
                    new ServerUpdate<long, IStructureType[]>(DateTime.UtcNow, response, Array.Empty<long>()))
                .ConfigureAwait(false);
            // Virtual CallOnCollectionChanged (not IStoreEditor.EndBatch) so RootObjects/Children are wired.
            await CallOnCollectionChanged(changes).ConfigureAwait(false);
            Trace.WriteLine(
                $"Loaded {changes.ObjectsInStore.Count} structure types (server returned {response.Length})",
                "WebAnnotation");
            return changes.ObjectsInStore;
        } 
    }
}
