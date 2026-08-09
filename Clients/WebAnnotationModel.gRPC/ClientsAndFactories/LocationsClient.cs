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
using Viking.AnnotationServiceTypes;
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
            service.AddSingleton<IServerAnnotationsClientFactory<IServerSpatialAnnotationsClient<long, ILocation>>, LocationsClientFactory>();
            service.AddSingleton<IServerSpatialAnnotationsClient<long, ILocation>, LocationsClient>();
            service.AddSingleton<IServerAnnotationsBySectionClient<long, ILocation[]>, LocationsClient>();
            service.AddSingleton<IServerAnnotationsClient<long, ILocation, ILocation, ILocation>, LocationsClient>();
            service.AddSingleton<IServerSpatialAnnotationsClient<long, AnnotationSet>, LocationsClient>();
            service.AddSingleton<IServerAnnotationsClientFactory<IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink>>, LocationsClientFactory>();
            return service;
        }
    }
}

namespace WebAnnotationModel.gRPC
{
    public class LocationsClientFactory : IServerAnnotationsClientFactory<ILocationsClient>,
        IServerAnnotationsClientFactory<IServerAnnotationsBySectionClient<long, ILocation[]>>,
        IServerAnnotationsClientFactory<IServerAnnotationsClient<long, ILocation, ILocation, ILocation>>,
        IServerAnnotationsClientFactory<IServerSpatialAnnotationsClient<long, AnnotationSet>>,
        IServerAnnotationsClientFactory<IServerSpatialAnnotationsClient<long, ILocation>>,
        IServerAnnotationsClientFactory<IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink>>
    {
        private readonly IObjectConverter<ILocation, Location> _clientObjConverter;
        private readonly GrpcRepositorySettings _config;
        private readonly IGrpcChannelManager _channelManager;


        public LocationsClientFactory(IGrpcChannelManager channelManager,
            IObjectConverter<ILocation, Location> clientObjConverter,
            IOptions<GrpcRepositorySettings> config)
        {
            _channelManager = channelManager;
            _clientObjConverter = clientObjConverter;
            _config = config.Value;
        }

        private GrpcChannel Channel => _channelManager.GetOrCreate(_config.Endpoint);

        public ILocationsClient GetOrCreate()
        { 
            return new LocationsClient(Channel, _clientObjConverter);
        }

        IServerAnnotationsBySectionClient<long, ILocation[]> IServerAnnotationsClientFactory<IServerAnnotationsBySectionClient<long, ILocation[]>>.GetOrCreate()
        { 
            return new LocationsClient(Channel, _clientObjConverter);
        }

        IServerAnnotationsClient<long, ILocation, ILocation, ILocation> IServerAnnotationsClientFactory<IServerAnnotationsClient<long, ILocation, ILocation, ILocation>>.GetOrCreate()
        {
            return new LocationsClient(Channel, _clientObjConverter);
        }

        IServerSpatialAnnotationsClient<long, AnnotationSet> IServerAnnotationsClientFactory<IServerSpatialAnnotationsClient<long, AnnotationSet>>.GetOrCreate()
        {
            return new LocationsClient(Channel, _clientObjConverter);
        }

        IServerSpatialAnnotationsClient<long, ILocation> IServerAnnotationsClientFactory<IServerSpatialAnnotationsClient<long, ILocation>>.GetOrCreate()
        {
            return new LocationsClient(Channel, _clientObjConverter);
        }

        IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink> IServerAnnotationsClientFactory<IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink>>.GetOrCreate()
        {
            return new LocationsClient(Channel, _clientObjConverter);
        }
    }

    public interface ILocationsClient : IServerAnnotationsClient<long, ILocation, ILocation, ILocation>
    {
        Task<ILocation[]> GetStructureLocations(long structureID);

        Task<ILocation> GetLastModifiedLocation();
    }

    public class LocationsClient : ILocationsClient, IServerSpatialAnnotationsClient<long, ILocation>, IServerAnnotationsBySectionClient<long, ILocation[]>, IServerAnnotationsClient<long, ILocation, ILocation, ILocation>, IServerSpatialAnnotationsClient<long, AnnotationSet>,
        IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink>
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

        /// <summary>
        /// Region load. Optional <paramref name="onChunk"/> is awaited per stream batch for progressive UI merge.
        /// </summary>
        public Task<ServerUpdate<long, ILocation[]>> GetAsync(
            long Z,
            string geometryWellKnownText,
            double screenPixelSizeInVolume,
            DateTime? modifiedAfter,
            CancellationToken token,
            Func<ServerUpdate<long, ILocation[]>, Task> onChunk) =>
            GetRegionAsync(Z, geometryWellKnownText, screenPixelSizeInVolume, modifiedAfter, token, onChunk);

        public Task<ServerUpdate<long, ILocation[]>> GetAsync(long Z, string geometryWellKnownText, double screenPixelSizeInVolume, DateTime? modifiedAfter, CancellationToken token) =>
            GetRegionAsync(Z, geometryWellKnownText, screenPixelSizeInVolume, modifiedAfter, token, onChunk: null);

        private async Task<ServerUpdate<long, ILocation[]>> GetRegionAsync(
            long Z,
            string geometryWellKnownText,
            double screenPixelSizeInVolume,
            DateTime? modifiedAfter,
            CancellationToken token,
            Func<ServerUpdate<long, ILocation[]>, Task> onChunk)
        {
            var region = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry
            {
                Text = geometryWellKnownText
            };

            var request = new GetLocationChangesInMosaicRegionRequest()
            {
                MinRadius = screenPixelSizeInVolume,
                Region = region,
                Z = Z
            };
            if (modifiedAfter.HasValue)
                request.ModifiedAfterThisUtcTime = Timestamp.FromDateTime(DateTime.SpecifyKind(modifiedAfter.Value, DateTimeKind.Utc));

            try
            {
                using (var call = Client.StreamLocationChangesInMosaicRegion(request, cancellationToken: token))
                {
                    DateTime? queryTime = null;
                    var locations = new List<ILocation>();
                    var deletedIds = new List<long>();
                    var sawLast = false;

                    while (await call.ResponseStream.MoveNext(token).ConfigureAwait(false))
                    {
                        var chunk = call.ResponseStream.Current;
                        if (chunk.QueryExecutedTime != null)
                            queryTime = chunk.QueryExecutedTime.ToDateTime();

                        var chunkLocations = chunk.Locations.Cast<ILocation>().ToArray();
                        var chunkDeleted = chunk.DeletedIds.ToArray();
                        locations.AddRange(chunkLocations);
                        deletedIds.AddRange(chunkDeleted);

                        if (onChunk != null)
                        {
                            await onChunk(new ServerUpdate<long, ILocation[]>(
                                queryTime ?? DateTime.UtcNow, chunkLocations, chunkDeleted))
                                .ConfigureAwait(false);
                        }

                        if (chunk.IsLast)
                            sawLast = true;
                    }

                    if (!sawLast && queryTime == null)
                        throw new RpcException(new Status(StatusCode.Internal, "Location region stream ended without chunks"));

                    return new ServerUpdate<long, ILocation[]>(
                        queryTime ?? DateTime.UtcNow, locations.ToArray(), deletedIds.ToArray());
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unimplemented)
            {
                var response = await Client.GetLocationChangesInMosaicRegionAsync(request, cancellationToken: token);
                var update = new ServerUpdate<long, ILocation[]>(
                    response.QueryExecutedTime.ToDateTime(), response.Results.Cast<ILocation>().ToArray(), response.DeletedIds.ToArray());
                if (onChunk != null)
                    await onChunk(update).ConfigureAwait(false);
                return update;
            }
        }

        async Task<ServerUpdate<long, AnnotationSet[]>> IServerSpatialAnnotationsClient<long, AnnotationSet>.GetAsync(long Z, string geometryWellKnownText, double screenPixelSizeInVolume, DateTime? modifiedAfter, CancellationToken token)
        {
            var region = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry
            {
                Text = geometryWellKnownText
            };

            var request = new GetAnnotationsInMosaicRegionRequest()
            {
                MinRadius = screenPixelSizeInVolume,
                Region = region,
                Z = Z
            };
            if (modifiedAfter.HasValue)
                request.ModifiedAfterThisUtcTime = Timestamp.FromDateTime(DateTime.SpecifyKind(modifiedAfter.Value, DateTimeKind.Utc));

            try
            {
                using (var call = Client.StreamAnnotationsInMosaicRegion(request, cancellationToken: token))
                {
                    DateTime? queryTime = null;
                    var merged = new AnnotationSet();
                    var deletedIds = new List<long>();
                    var sawLast = false;

                    while (await call.ResponseStream.MoveNext(token).ConfigureAwait(false))
                    {
                        var chunk = call.ResponseStream.Current;
                        if (chunk.QueryExecutedTime != null)
                            queryTime = chunk.QueryExecutedTime.ToDateTime();
                        if (chunk.Partial != null)
                        {
                            merged.Locations.AddRange(chunk.Partial.Locations);
                            merged.Structures.AddRange(chunk.Partial.Structures);
                        }
                        deletedIds.AddRange(chunk.DeletedIds);
                        if (chunk.IsLast)
                            sawLast = true;
                    }

                    if (!sawLast && queryTime == null)
                        throw new RpcException(new Status(StatusCode.Internal, "Annotation region stream ended without chunks"));

                    return new ServerUpdate<long, AnnotationSet[]>(
                        queryTime ?? DateTime.UtcNow, new AnnotationSet[] { merged }, deletedIds.ToArray());
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unimplemented)
            {
                var response = await Client.GetAnnotationsInMosaicRegionAsync(request, cancellationToken: token);
                return new ServerUpdate<long, AnnotationSet[]>(response.QueryExecutedTime.ToDateTime(), new AnnotationSet[] { response.Result },
                    response.DeletedIds.ToArray());
            }
        }

        public async Task<ServerUpdate<long, ILocation[]>> GetAsync(long Z, DateTime? modifiedAfter, CancellationToken token)
        {
            var request = new GetLocationChangesRequest() { Section = Z };
            if (modifiedAfter.HasValue)
                request.ModifiedAfterThisUtcTime = Timestamp.FromDateTime(DateTime.SpecifyKind(modifiedAfter.Value, DateTimeKind.Utc));

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
            var added = new List<ILocation>();
            var updated = new List<ILocation>();
            var deleted = new List<long>();
            foreach (var ro in response.Results)
            {
                switch (ro.ActionCase)
                {
                    case LocationChangeResponse.ActionOneofCase.None:
                        break;
                    case LocationChangeResponse.ActionOneofCase.Created:
                        added.Add(ro.Created);
                        break;
                    case LocationChangeResponse.ActionOneofCase.Updated:
                        updated.Add(ro.Updated);
                        break;
                    case LocationChangeResponse.ActionOneofCase.DeletedId:
                        deleted.Add(ro.DeletedId);
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            return new UpdateResults<long, ILocation>(added.ToArray(), updated.ToArray(), deleted.ToArray());
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

        async Task<ILocationLink> IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink>.Create(ILocationLink obj, CancellationToken token)
        {
            var request = new CreateLocationLinkRequest { SourceId = (long)obj.A, TargetId = (long)obj.B };
            await Client.CreateLocationLinkAsync(request, cancellationToken: token);
            return new LocationLink { SourceId = (long)obj.A, TargetId = (long)obj.B };
        }

        async Task<LocationLinkKey?> IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink>.Delete(LocationLinkKey key, CancellationToken token)
        {
            var request = new DeleteLocationLinkRequest { SourceId = key.A, TargetId = key.B };
            await Client.DeleteLocationLinkAsync(request, cancellationToken: token);
            return key;
        }

        async Task<ILocationLink> IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink>.GetAsync(LocationLinkKey key, CancellationToken token)
        {
            var request = new GetLinkedLocationsRequest { Id = key.A };
            var response = await Client.GetLinkedLocationsAsync(request, cancellationToken: token);
            if (!response.Results.Contains(key.B))
                return null;

            return new LocationLink { SourceId = key.A, TargetId = key.B };
        }

        async Task<IList<ILocationLink>> IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink>.GetAsync(IEnumerable<LocationLinkKey> keys, CancellationToken token)
        {
            var linkClient = (IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink>)this;
            var results = new List<ILocationLink>();
            foreach (var key in keys)
            {
                var link = await linkClient.GetAsync(key, token);
                if (link != null)
                    results.Add(link);
            }

            return results;
        }

        Task<UpdateResults<LocationLinkKey, ILocationLink>> IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink>.UpdateAsync(ILocationLink obj, CancellationToken token)
        {
            //Location links have no mutable properties beyond their endpoints; there is nothing to update once created.
            return Task.FromResult(new UpdateResults<LocationLinkKey, ILocationLink>());
        }

        Task<UpdateResults<LocationLinkKey, ILocationLink>> IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink>.UpdateAsync(IEnumerable<ILocationLink> objs, CancellationToken token)
        {
            return Task.FromResult(new UpdateResults<LocationLinkKey, ILocationLink>());
        }
    }
}