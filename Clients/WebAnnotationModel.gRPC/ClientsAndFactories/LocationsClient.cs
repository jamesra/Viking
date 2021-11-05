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
using System.Net;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.gRPC;
using Geometry = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class LocationConverterExtensions
    {
        public static IServiceCollection AddGrpcLocationRepository(this IServiceCollection service,
            Action<GrpcChannelOptions> options)
        {
            service.Configure(options);
            //var _channel = GrpcChannel.ForAddress(endpointUri, channelOptions.Value);
            //service.AddSingleton<GrpcChannel>((_) => GrpcChannel.ForAddress(endpointUri, channelOptions.Value));
            service.AddSingleton<IServerAnnotationsClientFactory<ILocationsClient>, LocationsClientFactory>();
            service.AddSingleton<IServerSpatialAnnotationsClient<long, ILocation>, LocationsClient>();
            service.AddSingleton<IServerAnnotationsBySectionClient<long, ILocation[]>, LocationsClient>();
            service.AddSingleton<IServerAnnotationsClient<long, ILocation, ILocation, ILocation>, LocationsClient>();
            service.AddSingleton<IServerSpatialAnnotationsClient<long, AnnotationSet>, LocationsClient>();
            return service;
        }
    }
}

namespace WebAnnotationModel.gRPC
{
    public class LocationsClientFactory : IServerAnnotationsClientFactory<ILocationsClient>,
        IServerAnnotationsClientFactory<IServerAnnotationsBySectionClient<long, ILocation[]>>,
        IServerAnnotationsClientFactory<IServerAnnotationsClient<long, ILocation, ILocation, ILocation>>,
        IServerAnnotationsClientFactory<IServerSpatialAnnotationsClient<long, AnnotationSet>>
    {
        private readonly IObjectConverter<ILocation, Location> _clientObjConverter;
        private readonly GrpcRepositorySettings _config;
        private readonly GrpcChannel _channel;


        public LocationsClientFactory(IGrpcChannelManager channelManager,
            IObjectConverter<ILocation, Location> clientObjConverter,
            IOptions<GrpcRepositorySettings> config)
        {
            _channel = channelManager.GetOrCreate(config.Value.Endpoint);
            _clientObjConverter = clientObjConverter;
            _config = config.Value;
        }

        public ILocationsClient GetOrCreate()
        { 
            return new LocationsClient(_channel, _clientObjConverter);
        }

        IServerAnnotationsBySectionClient<long, ILocation[]> IServerAnnotationsClientFactory<IServerAnnotationsBySectionClient<long, ILocation[]>>.GetOrCreate()
        { 
            return new LocationsClient(_channel, _clientObjConverter);
        }

        IServerAnnotationsClient<long, ILocation, ILocation, ILocation> IServerAnnotationsClientFactory<IServerAnnotationsClient<long, ILocation, ILocation, ILocation>>.GetOrCreate()
        {
            return new LocationsClient(_channel, _clientObjConverter);
        }

        IServerSpatialAnnotationsClient<long, AnnotationSet> IServerAnnotationsClientFactory<IServerSpatialAnnotationsClient<long, AnnotationSet>>.GetOrCreate()
        {
            return new LocationsClient(_channel, _clientObjConverter);
        }
    }

    public interface ILocationsClient : IServerAnnotationsClient<long, ILocation, ILocation, ILocation>
    {
        Task<ILocation[]> GetStructureLocations(long structureID);

        Task<ILocation> GetLastModifiedLocation();
    }

    public class LocationsClient : ILocationsClient, IServerSpatialAnnotationsClient<long, ILocation>, IServerAnnotationsBySectionClient<long, ILocation[]>, IServerAnnotationsClient<long, ILocation, ILocation, ILocation>, IServerSpatialAnnotationsClient<long, AnnotationSet>
    {
        private readonly AnnotateLocations.AnnotateLocationsClient Client;
        private readonly IObjectConverter<ILocation, Location> ClientObjConverter;

        public LocationsClient(GrpcChannel channel, IObjectConverter<ILocation, Location> clientObjConverter)
        {
            ClientObjConverter = clientObjConverter;
            Client = new AnnotateLocations.AnnotateLocationsClient(channel);
        }

        public async Task<ILocation> Create(ILocation obj, CancellationToken token)
        {
            var request = new CreateLocationRequest() { Obj = ClientObjConverter.Convert(obj) }; 
            var response = await Client.CreateLocationAsync(request, cancellationToken: token);
            return response.Result;
        }

        public async Task<long?> Delete(long key, CancellationToken token)
        {
            UpdateLocationsRequest request = new UpdateLocationsRequest();
            LocationChangeRequest change = new LocationChangeRequest();
            change.Delete = key;
            request.Locations.Add(change);
             
            var response = await Client.UpdateAsync(request, cancellationToken: token);
            if (!response.Results.Any())
                return default;

            var first_response = response.Results.First();
            var success = first_response.Success &&
                   first_response.ActionCase == LocationChangeResponse.ActionOneofCase.DeletedId &&
                   first_response.DeletedId == key;

            if (!success)
                return default;

            return first_response.DeletedId;
        }

        public async Task<ServerUpdate<long, ILocation[]>> GetAsync(long Z, string geometryWellKnownText, double screenPixelSizeInVolume,  DateTime? modifiedAfter, CancellationToken token)
        {
            var region = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry
            {
                Text = geometryWellKnownText
            };

            var request = new GetLocationChangesInMosaicRegionRequest() { MinRadius = screenPixelSizeInVolume, Region = region, ModifiedAfterThisUtcTime = Timestamp.FromDateTime(modifiedAfter ?? DateTime.MinValue), Z = Z};
            var response = await Client.GetLocationChangesInMosaicRegionAsync(request, cancellationToken: token);

            return new ServerUpdate<long, ILocation[]>(
                response.QueryExecutedTime.ToDateTime(), response.Results.Cast<ILocation>().ToArray(), response.DeletedIds.ToArray());
        }

        async Task<ServerUpdate<long, AnnotationSet[]>> IServerSpatialAnnotationsClient<long, AnnotationSet>.GetAsync(long Z, string geometryWellKnownText, double screenPixelSizeInVolume, DateTime? modifiedAfter, CancellationToken token)
        {
            var region = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry
            {
                Text = geometryWellKnownText
            };

            var request = new GetAnnotationsInMosaicRegionRequest() { MinRadius = screenPixelSizeInVolume, Region = region, ModifiedAfterThisUtcTime = Timestamp.FromDateTime(modifiedAfter ?? DateTime.MinValue), Z = Z};
            var response = await Client.GetAnnotationsInMosaicRegionAsync(request, cancellationToken: token);

            return new ServerUpdate<long, AnnotationSet[]>(response.QueryExecutedTime.ToDateTime(), new AnnotationSet[] {response.Result},
                response.DeletedIds.ToArray()
                );
        }

        public async Task<ServerUpdate<long, ILocation[]>> GetAsync(long Z, DateTime? modifiedAfter, CancellationToken token)
        {
            var request = new GetLocationChangesRequest() {
                Section = Z,
                ModifiedAfterThisUtcTime = (modifiedAfter ?? DateTime.MinValue).ToTimestamp()
            };

            var response = await Client.GetLocationChangesAsync(request, cancellationToken: token);

            return new ServerUpdate<long, ILocation[]>(
                response.QueryExecutedTime.ToDateTime(), response.Results.Cast<ILocation>().ToArray(), response.DeletedIds.ToArray());
        }

        public async Task<ILocation> GetAsync(long key, CancellationToken token)
        {
            var request = new GetLocationByIDRequest()
            {
                Id = key
            };

            var response = await Client.GetLocationByIDAsync(request, cancellationToken: token);
            return response.Result;
        }

        public async Task<IList<ILocation>> GetAsync(IEnumerable<long> keys, CancellationToken token)
        {
            var request = new GetLocationsByIDRequest();

            request.Ids.AddRange(keys);

            var response = await Client.GetLocationsByIDAsync(request, cancellationToken: token);
            return response.Results.Cast<ILocation>().ToList();
        }

        public Task<UpdateResults<long, ILocation>> UpdateAsync(ILocation obj, CancellationToken token)
        {
            return UpdateAsync(new ILocation[] { obj }, token);
        }

        public async Task<UpdateResults<long, ILocation>> UpdateAsync(IEnumerable<ILocation> objs, CancellationToken token)
        {
            UpdateLocationsRequest request = new UpdateLocationsRequest();
            var serverObjs = objs.Select(o => ClientObjConverter.Convert(o));
            request.Locations.AddRange(serverObjs.Select(o => (LocationChangeRequest)o).Where(o => o != null));

            var response = await Client.UpdateAsync(request, cancellationToken: token);

            return CollectResults(response);
        }

        private UpdateResults<long, ILocation> CollectResults(UpdateLocationsResponse response)
        {
            var result = new UpdateResults<long, ILocation>();
            foreach (var ro in response.Results)
            {
                switch (ro.ActionCase)
                {
                    case LocationChangeResponse.ActionOneofCase.None:
                        break;
                    case LocationChangeResponse.ActionOneofCase.Created:
                        result.AddedObjects.Add(ro.Created);
                        break;
                    case LocationChangeResponse.ActionOneofCase.Updated:
                        result.UpdatedObjects.Add(ro.Updated);
                        break;
                    case LocationChangeResponse.ActionOneofCase.DeletedId:
                        result.DeletedIDs.Add(ro.DeletedId);
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            return result;
        }
          
        public async Task<ILocation> GetLastModifiedLocation()
        {
            var request = new GetLastModifiedLocationRequest();
            var response = await Client.GetLastModifiedLocationAsync(request);
            return response.Result;
        }

        public async Task<ILocation[]> GetStructureLocations(long structureID)
        {
            var request = new GetStructureLocationsRequest() { StructureId = structureID };
            var response = await Client.GetStructureLocationsAsync(request);
            return response.Results.ToArray();
        }
    }
}