using Grpc.Core;
using WebAnnotationModel.ServerInterface;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using WebAnnotationModel.Objects;
using Grpc.Net.Client;
using System.Threading.Tasks;
using Geometry;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Viking.AnnotationServiceTypes.Interfaces;

namespace WebAnnotationModel.gRPC
{
    public static class StructureTypeConverterExtensions
    {
        public static IServiceCollection AddStructureTypeServer(this IServiceCollection service, string endpoint)
        { 
            service.AddSingleton<IServerAnnotationsClient<long, IStructureType, IStructureType, IStructureType>, StructureTypesClient>(); 
            return service;
        }
    } 

    public interface
        IStructureTypesRepository : IServerAnnotationsClient<long, IStructureType, IStructureType, IStructureType>
    {
        Task<IStructureType[]> GetAll();
    }

    public class StructureTypesClient : IStructureTypesRepository
    {
        private readonly AnnotateStructureTypes.AnnotateStructureTypesClient Client;
        private IObjectConverter<IStructureType, StructureType> ClientObjConverter;

        StructureTypesClient(GrpcChannel channel, IObjectConverter<IStructureType, StructureType> clientObjConverter)
        {
            ClientObjConverter = clientObjConverter;
            Client = new AnnotateStructureTypes.AnnotateStructureTypesClient(channel);
        }

        public async Task<IStructureType> Create(IStructureType obj, CancellationToken token)
        {
            CreateStructureTypeRequest request = new CreateStructureTypeRequest()
            {
                Obj = ClientObjConverter.Convert(obj)
            };

            var result = await Client.CreateStructureTypeAsync(request, cancellationToken: token);
            return result.Result;
        }

        public async Task<long?> Delete(long key, CancellationToken token)
        {
            UpdateStructureTypesRequest request = new UpdateStructureTypesRequest();
            StructureTypeChangeRequest change = new StructureTypeChangeRequest
            {
                Delete = key
            };
            request.Objs.Add(change);

            var response = await Client.UpdateAsync(request, cancellationToken: token);
            if (!response.Results.Any())
                return default;

            var first_response = response.Results.First();
            var success = first_response.Success &&
                          first_response.ActionCase == StructureTypeChangeResponse.ActionOneofCase.DeletedId &&
                          first_response.DeletedId == key;

            if (!success)
                return default;

            return first_response.DeletedId;
        }
          
        public async Task<IStructureType> GetAsync(long key, CancellationToken token)
        {
            var request = new GetStructureTypeByIDRequest()
            {
                Id = key
            };

            var response = await Client.GetStructureTypeByIDAsync(request, cancellationToken: token);
            return response.Result;
        }

        public async Task<IList<IStructureType>> GetAsync(IEnumerable<long> keys, CancellationToken token)
        {
            var request = new GetStructureTypesByIDsRequest()
            {
                
            };

            request.Ids.AddRange(keys);

            var response = await Client.GetStructureTypesByIDsAsync(request, cancellationToken: token);
            return response.Results.Cast<IStructureType>().ToList();
        }

        public Task<UpdateResults<long, IStructureType>> UpdateAsync(IStructureType obj, CancellationToken token)
        {
            return UpdateAsync(new IStructureType[] { obj }, token);
        }

        public async Task<UpdateResults<long, IStructureType>> UpdateAsync(IEnumerable<IStructureType> objs, CancellationToken token)
        {
            UpdateStructureTypesRequest request = new UpdateStructureTypesRequest();
            var serverObjs = objs.Select(o => ClientObjConverter.Convert(o));
            request.Objs.AddRange(serverObjs.Select(o => (StructureTypeChangeRequest)o).Where(o => o != null));

            var response = await Client.UpdateAsync(request, cancellationToken: token);

            return CollectResults(response);
        }

        private UpdateResults<long, IStructureType> CollectResults(UpdateStructureTypesResponse response)
        {
            var result = new UpdateResults<long, IStructureType>();
            foreach (var ro in response.Results)
            {
                switch (ro.ActionCase)
                {
                    case StructureTypeChangeResponse.ActionOneofCase.None:
                        break;
                    case StructureTypeChangeResponse.ActionOneofCase.Created:
                        result.AddedObjects.Add(ro.Created);
                        break;
                    case StructureTypeChangeResponse.ActionOneofCase.Updated:
                        result.UpdatedObjects.Add(ro.Updated);
                        break;
                    case StructureTypeChangeResponse.ActionOneofCase.DeletedId:
                        result.DeletedIDs.Add(ro.DeletedId);
                        break;
                }
            }

            return result;
        }

        public async Task<IStructureType[]> GetAll()
        {
            var request = new GetStructureTypesRequest();
            var results = await Client.GetStructureTypesAsync(request);
            return results.Results.Cast<IStructureType>().ToArray();
        }
    }
}