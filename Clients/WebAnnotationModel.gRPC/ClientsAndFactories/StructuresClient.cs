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
    public static class StructureConverterExtensions
    {
        public static IServiceCollection AddStructureServer(this IServiceCollection service, string endpoint)
        {
            service.AddSingleton<IServerSpatialAnnotationsClient<long, IStructure>, StructuresClient>();
            service.AddSingleton<IServerAnnotationsBySectionClient<long, IStructure[]>, StructuresClient>();
            service.AddSingleton<IStructureRepository, StructuresClient>();
            service.AddSingleton<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>, StructuresClient>();
            return service;
        }
    }

    public interface IStructureRepository : IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>
    {
        Task<long> SplitStructureAtLocationLink(long KeepLocID, long SplitLocID);
          
        Task<long> MergeStructures(long KeepID, long MergeID);
         
        Task<IStructure[]> GetStructuresOfType(long StructureTypeID);

        Task<IStructure[]> GetAll();

        Task<IStructure[]> GetChildStructures(long ID);

        Task<long> NumberOfLocations(long ID);
    }

    public class StructuresClient : IStructureRepository, IServerAnnotationsBySectionClient<long, IStructure[]>, IServerSpatialAnnotationsClient<long, IStructure>
    {
        private readonly AnnotateStructures.AnnotateStructuresClient Client;
        private readonly IObjectConverter<IStructureReadOnly, Structure> ClientReadOnlyObjObjConverter;
        private readonly IObjectConverter<ILocationReadOnly, Location> ClientReadOnlyLocationObjConverter;
        private readonly IObjectConverter<IStructure, Structure> ClientObjConverter; 
        private readonly IStoreServerQueryResultsHandler<long, LocationObj, ILocation> LocationProcessor;
        private IStoreServerQueryResultsHandler<long, StructureObj, IStructure> StructureProcessor;

        StructuresClient(GrpcChannel channel,
            IStoreServerQueryResultsHandler<long, StructureObj, IStructure> structureProcessor,
            IStoreServerQueryResultsHandler<long, LocationObj, ILocation> locationProcessor,
            IObjectConverter<IStructureReadOnly, Structure> clientReadOnlyObjObjConverter,
            IObjectConverter<ILocationReadOnly, Location> clientReadOnlyLocationObjConverter,
            IObjectConverter<IStructure, Structure> clientObjConverter)
        {
            StructureProcessor = structureProcessor;
            LocationProcessor = locationProcessor;
            ClientObjConverter = clientObjConverter;
            ClientReadOnlyObjObjConverter = clientReadOnlyObjObjConverter;
            ClientReadOnlyLocationObjConverter = clientReadOnlyLocationObjConverter;
            Client = new AnnotateStructures.AnnotateStructuresClient(channel);
        }

        public async Task<ICreateStructureResponseParameter> Create(ICreateStructureAndLocationRequestParameter obj, CancellationToken token)
        {
            var result = await Client.CreateStructureAsync(new CreateStructureRequest()
            {
                NewStructure = ClientReadOnlyObjObjConverter.Convert(obj.Structure),
                NewAnnotation = ClientReadOnlyLocationObjConverter.Convert(obj.Location)
            }, cancellationToken: token);

            var structureChanges = await StructureProcessor.ProcessServerUpdate(new IStructure[] {result.NewStructure}, Array.Empty<long>());

            var locationChanges = await LocationProcessor.ProcessServerUpdate(new ILocation[] { result.NewAnnotation }, Array.Empty<long>());

            await StructureProcessor.EndBatch(structureChanges);
            await LocationProcessor.EndBatch(locationChanges);

            return new CreateStructureResponseParameter(result.NewStructure, result.NewAnnotation);
        }

        public async Task<long?> Delete(long key, CancellationToken token)
        {
            UpdateStructuresRequest request = new UpdateStructuresRequest();
            StructureChangeRequest change = new StructureChangeRequest();
            change.Delete = key;
            request.Objs.Add(change);

            var response = await Client.UpdateAsync(request, cancellationToken: token);
            if (!response.Results.Any())
                return default;

            var first_response = response.Results.First();
            var success = first_response.Success &&
                          first_response.ActionCase == StructureChangeResponse.ActionOneofCase.DeletedId &&
                          first_response.DeletedId == key;

            if (!success)
                return default;

            return first_response.DeletedId;
        }

        public async Task<ServerUpdate<long, IStructure[]>> GetAsync(long Z, string geometryWellKnownText, double screenPixelSizeInVolume,  DateTime? modifiedAfter, CancellationToken token)
        {
            var region = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry
            {
                Text = geometryWellKnownText
            };

            var request = new GetStructuresInMosaicRegionRequest() { Region = region, ModifiedAfterThisUtcTime = Timestamp.FromDateTime(modifiedAfter ?? DateTime.MinValue), Z = Z};
            var response = await Client.GetStructuresInMosaicRegionAsync(request, cancellationToken: token);

            return new ServerUpdate<long, IStructure[]>(response.QueryExecutedTime.ToDateTime(), response.Results.Cast<IStructure>().ToArray(), response.DeletedIds.ToArray());
        }
         
        public async Task<ServerUpdate<long, IStructure[]>> GetAsync(long Z, DateTime? modifiedAfter, CancellationToken token)
        {
            var request = new GetStructuresForSectionRequest() {
                Z = Z,
                ModifiedAfterThisUtcTime = (modifiedAfter ?? DateTime.MinValue).ToTimestamp()
            };

            var response = await Client.GetStructuresForSectionAsync(request, cancellationToken: token);

            return new ServerUpdate<long, IStructure[]>(response.QueryExecutedTime.ToDateTime(), response.Results.Cast<IStructure>().ToArray(), response.DeletedIds.ToArray()
                );
        }

        public async Task<IStructure> GetAsync(long key, CancellationToken token)
        {
            var request = new GetStructureByIDRequest()
            {
                Id = key
            };

            var response = await Client.GetStructureByIDAsync(request, cancellationToken: token);
            return response.Result;
        }

        public async Task<IList<IStructure>> GetAsync(IEnumerable<long> keys, CancellationToken token)
        {
            var request = new GetStructuresByIDRequest()
            {
                
            };

            request.Ids.AddRange(keys);

            var response = await Client.GetStructuresByIDAsync(request, cancellationToken: token);
            return response.Results.Cast<IStructure>().ToList();
        }
        public async Task<IStructure[]> GetAll()
        {
            var request = new GetStructuresRequest() { };
            var response = await Client.GetStructuresAsync(request);
            return response.Results.Cast<IStructure>().ToArray();
        }

        public async Task<IStructure[]> GetStructuresOfType(long StructureTypeID)
        {
            var request = new GetStructuresOfTypeRequest() { Id = StructureTypeID };
            var response = await Client.GetStructuresOfTypeAsync(request);
            return response.Results.Cast<IStructure>().ToArray();
        }

        public async Task<long> MergeStructures(long KeepID, long MergeID)
        {
            var request = new MergeRequest() { KeepId = KeepID, MergeId = MergeID };
            var response = await Client.MergeAsync(request);
            return response.KeptId;
        }

        public async Task<long> SplitStructureAtLocationLink(long KeepLocID, long SplitLocID)
        {
            var request = new SplitAtLocationLinkRequest()
                { LocationIdOfKeepStructure = KeepLocID, LocationIdOfSplitStructure = SplitLocID };
            var response = await Client.SplitAtLocationLinkAsync(request);
            return response.SplitStructureId;
        }

        public Task<UpdateResults<long, IStructure>> UpdateAsync(IStructure obj, CancellationToken token)
        {
            return UpdateAsync(new IStructure[] { obj }, token);
        }

        public async Task<UpdateResults<long, IStructure>> UpdateAsync(IEnumerable<IStructure> objs, CancellationToken token)
        {
            UpdateStructuresRequest request = new UpdateStructuresRequest();
            var serverObjs = objs.Select(o => ClientObjConverter.Convert(o));
            request.Objs.AddRange(serverObjs.Select(o => (StructureChangeRequest)o).Where(o => o != null));

            var response = await Client.UpdateAsync(request, cancellationToken: token);

            return CollectResults(response);
        }

        private UpdateResults<long, IStructure> CollectResults(UpdateStructuresResponse response)
        {
            var result = new UpdateResults<long, IStructure>();
            foreach (var ro in response.Results)
            {
                switch (ro.ActionCase)
                {
                    case StructureChangeResponse.ActionOneofCase.None:
                        break;
                    case StructureChangeResponse.ActionOneofCase.Created:
                        result.AddedObjects.Add(ro.Created);
                        break;
                    case StructureChangeResponse.ActionOneofCase.Updated:
                        result.UpdatedObjects.Add(ro.Updated);
                        break;
                    case StructureChangeResponse.ActionOneofCase.DeletedId:
                        result.DeletedIDs.Add(ro.DeletedId);
                        break;
                }
            }

            return result;
        }

        public async Task<IStructure[]> GetChildStructures(long ID)
        {
            var request = new GetChildStructuresRequest() { StructureId = ID };
            var response = await Client.GetChildStructuresAsync(request);
            return response.Results.Cast<IStructure>().ToArray();
        }

        public async Task<long> NumberOfLocations(long ID)
        {
            var request = new NumberOfLocationsRequest() { Id = ID };
            var response = await Client.NumberOfLocationsAsync(request);
            return response.Result;
        }
    }
}