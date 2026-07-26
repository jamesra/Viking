using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    public class StructureTypeStore : StoreBaseWithKeyAndParent<long, StructureTypeObj,
                                        IStructureType, IStructureType, IStructureType>,
                                        IStructureTypeStore
    {
        private IServerAnnotationsClientFactory<IStructureTypesRepository> _structureTypeClientFactory;

        public StructureTypeStore(IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructureType, IStructureType, IStructureType>> clientFactory,
            IServerAnnotationsClientFactory<IStructureTypesRepository> structureTypeClientFactory,
            IStoreServerQueryResultsHandler<long, StructureTypeObj,
                IStructureType> queryResultsHandler,
            IObjectConverter<StructureTypeObj, IStructureType> objToServerObjConverter,
            IObjectConverter<IStructureType, StructureTypeObj> serverObjToObjConverter,
            IObjectUpdater<StructureTypeObj, IStructureType> objUpdater = null) : base(clientFactory, queryResultsHandler, objToServerObjConverter, serverObjToObjConverter)
        {
            _structureTypeClientFactory = structureTypeClientFactory;
        }

        protected override Task Init()
        {
            return GetAll();
        }


        public async Task<StructureTypeObj> Create(StructureTypeObj new_type, CancellationToken token)
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
        /// At startup we load the entire structure types table since it is fairly static
        /// </summary>
        public async Task<ICollection<StructureTypeObj>> GetAll()
        {
            var client = _structureTypeClientFactory.GetOrCreate();
            
            try
            {
                var response = await client.GetAll();
                var changes = await ServerQueryResultsHandler.ProcessServerUpdate(new ServerUpdate<long, IStructureType[]>(DateTime.UtcNow, response.ToArray(), Array.Empty<long>()));
                CallOnCollectionChanged(changes);
                return changes.ObjectsInStore;
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                return Array.Empty<StructureTypeObj>();
            }
            finally
            {
                
            }
        } 
    }
}
