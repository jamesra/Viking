using Grpc.Core;
using gRPCAnnotationService.Protos;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Viking.DataModel.Annotation;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;

namespace gRPCAnnotationService
{
    public class StructureService : Viking.AnnotationServiceTypes.gRPC.V1.Protos.AnnotateStructures.AnnotateStructuresBase
    {
        private readonly AnnotationContext _context;
        private readonly ILogger<StructureService> _logger;
        public StructureService(AnnotationContext context, ILogger<StructureService> logger)
        {
            _logger = logger;
            _context = context;
        }

        public override Task<CreateStructureResponse> CreateStructure(CreateStructureRequest request, ServerCallContext context)
        {
            return base.CreateStructure(request, context);
        }

        public override Task<CreateStructureLinkResponse> CreateStructureLink(CreateStructureLinkRequest request, ServerCallContext context)
        {
            return base.CreateStructureLink(request, context);
        }

        public override Task<GetChildStructuresInNetworkResponse> GetChildStructuresInNetwork(GetChildStructuresInNetworkRequest request, ServerCallContext context)
        {
            return base.GetChildStructuresInNetwork(request, context);
        }

        public override Task<GetLinkedStructuresResponse> GetLinkedStructures(GetLinkedStructuresRequest request, ServerCallContext context)
        {
            return base.GetLinkedStructures(request, context);
        }

        public override Task<GetNetworkedStructuresResponse> GetNetworkedStructures(GetNetworkedStructuresRequest request, ServerCallContext context)
        {
            return base.GetNetworkedStructures(request, context);
        }

        public override async Task<GetStructureByIDResponse> GetStructureByID(GetStructureByIDRequest request, ServerCallContext context)
        {
            try
            { 
                var obj = await _context.Structures.FindAsync(request.Id);
                if (obj == null)
                    throw new Grpc.Core.RpcException(new Status(StatusCode.NotFound, $"Structure ID {request.Id} not found"));
                 
                GetStructureByIDResponse response = new GetStructureByIDResponse()
                {
                    Result = obj.ToProtobufMessage()
                };
                return response;
            }
            catch (System.ArgumentNullException e)
            {
                //This means there was no row with that ID; 
                _logger.LogInformation("Could not find requested location ID: " + request.Id.ToString());
                throw new Grpc.Core.RpcException(new Status(StatusCode.InvalidArgument, $"Structure ID {request.Id}", e));
            }
            catch (System.InvalidOperationException e)
            {
                //This means there was no row with that ID; 
                _logger.LogInformation("Could not find requested location ID: " + request.Id.ToString());
                throw new Grpc.Core.RpcException(new Status(StatusCode.InvalidArgument, $"Structure Type ID {request.Id}", e));
            } 
        }

        public override Task<GetStructureChangeLogResponse> GetStructureChangeLog(GetStructureChangeLogRequest request, ServerCallContext context)
        {
            return base.GetStructureChangeLog(request, context);
        }

        public override Task<GetStructureLinksInNetworkResponse> GetStructureLinksInNetwork(GetStructureLinksInNetworkRequest request, ServerCallContext context)
        {
            return base.GetStructureLinksInNetwork(request, context);
        }

        public override Task<GetStructuresResponse> GetStructures(GetStructuresRequest request, ServerCallContext context)
        {
            return base.GetStructures(request, context);
        }

        public override Task<GetStructuresByIDResponse> GetStructuresByID(GetStructuresByIDRequest request, ServerCallContext context)
        {
            return base.GetStructuresByID(request, context);
        }

        public override Task<GetStructuresForSectionResponse> GetStructuresForSection(GetStructuresForSectionRequest request, ServerCallContext context)
        {
            return base.GetStructuresForSection(request, context);
        }

        public override Task<GetStructuresInMosaicRegionResponse> GetStructuresInMosaicRegion(GetStructuresInMosaicRegionRequest request, ServerCallContext context)
        {
            return base.GetStructuresInMosaicRegion(request, context);
        }

        public override Task<GetStructuresInVolumeRegionResponse> GetStructuresInVolumeRegion(GetStructuresInVolumeRegionRequest request, ServerCallContext context)
        {
            return base.GetStructuresInVolumeRegion(request, context);
        }

        public override Task<GetStructuresOfTypeResponse> GetStructuresOfType(GetStructuresOfTypeRequest request, ServerCallContext context)
        {
            return base.GetStructuresOfType(request, context);
        }

        public override Task<GetUnfinishedLocationsResponse> GetUnfinishedLocations(GetUnfinishedLocationsRequest request, ServerCallContext context)
        {
            return base.GetUnfinishedLocations(request, context);
        }

        public override Task<GetUnfinishedLocationsWithPositionResponse> GetUnfinishedLocationsWithPosition(GetUnfinishedLocationsWithPositionRequest request, ServerCallContext context)
        {
            return base.GetUnfinishedLocationsWithPosition(request, context);
        }

        public override Task<MergeResponse> Merge(MergeRequest request, ServerCallContext context)
        {
            return base.Merge(request, context);
        }

        public override Task<NumberOfLocationsResponse> NumberOfLocations(NumberOfLocationsRequest request, ServerCallContext context)
        {
            return base.NumberOfLocations(request, context);
        }

        public override Task<SplitResponse> Split(SplitRequest request, ServerCallContext context)
        {
            return base.Split(request, context);
        }

        public override Task<SplitAtLocationLinkResponse> SplitAtLocationLink(SplitAtLocationLinkRequest request, ServerCallContext context)
        {
            return base.SplitAtLocationLink(request, context);
        }

        public override async Task<UpdateStructuresResponse> Update(UpdateStructuresRequest request, ServerCallContext context)
        {
            try
            {
                UpdateStructuresResponse response = new UpdateStructuresResponse()
                {
                };

                foreach (var req in request.Objs)
                {  
                    StructureChangeResponse row_response = new StructureChangeResponse();

                    switch (req.ActionCase)
                    {
                        case StructureChangeRequest.ActionOneofCase.Create:
                            var insert_result = await _context.Structures.AddAsync(req.Create.ToStructure());
                            row_response.Success = insert_result.State == Microsoft.EntityFrameworkCore.EntityState.Added;
                            row_response.Created = row_response.Success ? insert_result.Entity.ToProtobufMessage() : null;
                            break;
                        case StructureChangeRequest.ActionOneofCase.Update:
                            var update_result = _context.Structures.Update(req.Update.ToStructure());
                            row_response.Success = update_result.State == Microsoft.EntityFrameworkCore.EntityState.Modified;
                            row_response.Updated = update_result.Entity.ToProtobufMessage();
                            break;
                        case StructureChangeRequest.ActionOneofCase.Delete:
                            var del_row = await _context.Structures.FindAsync(req.Delete);
                            var remove_result = _context.Structures.Remove(del_row);
                            row_response.Success = remove_result.State == Microsoft.EntityFrameworkCore.EntityState.Deleted;
                            row_response.DeletedId = req.Delete;
                            break;
                    }

                    response.Results.Add(row_response);
                }

                await _context.SaveChangesAsync();

                return response;
            }
            catch (System.Exception e)
            {
                //This means there was no row with that ID; 
                _logger.LogInformation($"{nameof(Update)}: {e}");
                throw new Grpc.Core.RpcException(new Status(StatusCode.Unknown, nameof(Update), e));
            }
        }

        public override Task<UpdateStructureLinksResponse> UpdateLinks(UpdateStructureLinksRequest request, ServerCallContext context)
        {
            return base.UpdateLinks(request, context);
        }
    }
}