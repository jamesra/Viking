using Grpc.Core;
using WebAnnotationModel.ServerInterface;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using WebAnnotationModel.Objects;
using Grpc.Net.Client;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.gRPC;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class StructureTypeConverterExtensions
    {
        public static IServiceCollection AddStructureTypeServer(this IServiceCollection service)
        {
            service.AddSingleton<IServerAnnotationsClientFactory<IStructureTypesRepository>, StructureTypesClientFactory>();
            service.AddSingleton<IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructureType, IStructureType, IStructureType>>, StructureTypesClientFactory>();
            return service;
        }
    }
}

namespace WebAnnotationModel.gRPC
{
    public class StructureTypesClientFactory :
        IServerAnnotationsClientFactory<IStructureTypesRepository>,
        IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructureType, IStructureType, IStructureType>>
    {
        private readonly IGrpcChannelManager _channelManager;
        private readonly GrpcRepositorySettings _config;
        private readonly IObjectConverter<StructureTypeObj, StructureType> _clientObjConverter;

        public StructureTypesClientFactory(
            IGrpcChannelManager channelManager,
            IOptions<GrpcRepositorySettings> config,
            IObjectConverter<StructureTypeObj, StructureType> clientObjConverter)
        {
            _channelManager = channelManager;
            _config = config.Value;
            _clientObjConverter = clientObjConverter;
        }

        public IStructureTypesRepository GetOrCreate()
        {
            return CreateClient();
        }

        IServerAnnotationsClient<long, IStructureType, IStructureType, IStructureType>
            IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructureType, IStructureType, IStructureType>>.GetOrCreate()
        {
            return CreateClient();
        }

        private StructureTypesClient CreateClient()
        {
            var channel = _channelManager.GetOrCreate(_config.Endpoint);
            return new StructureTypesClient(channel, _clientObjConverter);
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
        private readonly IObjectConverter<StructureTypeObj, StructureType> ClientObjConverter;

        public StructureTypesClient(GrpcChannel channel, IObjectConverter<StructureTypeObj, StructureType> clientObjConverter)
        {
            ClientObjConverter = clientObjConverter;
            Client = new AnnotateStructureTypes.AnnotateStructureTypesClient(channel);
        }

        private StructureType ToProto(IStructureType obj)
        {
            if (obj is StructureType concrete)
                return concrete;
            if (obj is StructureTypeObj clientObj)
                return ClientObjConverter.Convert(clientObj);
            throw new ArgumentException(
                $"Unsupported {nameof(IStructureType)} implementation {obj?.GetType().FullName ?? "null"}",
                nameof(obj));
        }

        public async Task<IStructureType> Create(IStructureType obj, CancellationToken token)
        {
            CreateStructureTypeRequest request = new CreateStructureTypeRequest()
            {
                // Store.Create converts StructureTypeObj → StructureType before calling here.
                Obj = ToProto(obj)
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
            // Store.Save converts client objs to protos first; accept either form.
            foreach (var o in objs)
            {
                var change = (StructureTypeChangeRequest)ToProto(o);
                if (change != null)
                    request.Objs.Add(change);
            }

            if (request.Objs.Count == 0)
                return new UpdateResults<long, IStructureType>();

            var response = await Client.UpdateAsync(request, cancellationToken: token);

            return CollectResults(response);
        }

        private UpdateResults<long, IStructureType> CollectResults(UpdateStructureTypesResponse response)
        {
            var added = new List<IStructureType>();
            var updated = new List<IStructureType>();
            var deleted = new List<long>();
            foreach (var ro in response.Results)
            {
                switch (ro.ActionCase)
                {
                    case StructureTypeChangeResponse.ActionOneofCase.None:
                        break;
                    case StructureTypeChangeResponse.ActionOneofCase.Created:
                        added.Add(ro.Created);
                        break;
                    case StructureTypeChangeResponse.ActionOneofCase.Updated:
                        updated.Add(ro.Updated);
                        break;
                    case StructureTypeChangeResponse.ActionOneofCase.DeletedId:
                        deleted.Add(ro.DeletedId);
                        break;
                }
            }

            return new UpdateResults<long, IStructureType>(added.ToArray(), updated.ToArray(), deleted.ToArray());
        }

        public async Task<IStructureType[]> GetAll()
        {
            var request = new GetStructureTypesRequest();
            var results = await Client.GetStructureTypesAsync(request);
            return results.Results.Cast<IStructureType>().ToArray();
        }
    }
}
