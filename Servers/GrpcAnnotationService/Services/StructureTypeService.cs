using Grpc.Core;
using gRPCAnnotationService.Protos;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using Viking.DataModel.Annotation;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;

namespace gRPCAnnotationService
{
    public class StructureTypeService : Viking.AnnotationServiceTypes.gRPC.V1.Protos.AnnotateStructureTypes.AnnotateStructureTypesBase
    {
        private readonly AnnotationContext _context;
        private readonly ILogger<StructureTypeService> _logger;
        public StructureTypeService(AnnotationContext context, ILogger<StructureTypeService> logger)
        {
            _logger = logger;
            _context = context;
        }

        public override Task<CreateStructureTypeResponse> CreateStructureType(CreateStructureTypeRequest request, ServerCallContext context)
        {
            return base.CreateStructureType(request, context);
        }

        public override async Task<GetStructureTypeByIDResponse> GetStructureTypeByID(GetStructureTypeByIDRequest request, ServerCallContext context)
        {
            try
            {
                var obj = await _context.StructureTypes.FindAsync(request.Id);
                if (obj == null)
                    throw new Grpc.Core.RpcException(new Status(StatusCode.NotFound, $"Structure Type ID {request.Id} not found"));

                GetStructureTypeByIDResponse response = new GetStructureTypeByIDResponse()
                {
                    Result = obj.ToProtobufMessage()
                };
                return response;
            }
            catch (System.ArgumentNullException e)
            {
                //This means there was no row with that ID; 
                _logger.LogInformation("Could not find requested location ID: " + request.Id.ToString());
                throw new Grpc.Core.RpcException(new Status(StatusCode.InvalidArgument, $"Structure Type ID {request.Id}", e));
            }
            catch (System.InvalidOperationException e)
            {
                //This means there was no row with that ID; 
                _logger.LogInformation("Could not find requested location ID: " + request.Id.ToString());
                throw new Grpc.Core.RpcException(new Status(StatusCode.InvalidArgument, $"Structure Type ID {request.Id}", e));
            }

        }

        public override Task<GetStructureTypesResponse> GetStructureTypes(GetStructureTypesRequest request, ServerCallContext context)
        {
            try
            {
                GetStructureTypesResponse response = new GetStructureTypesResponse();

                response.Results.AddRange(_context.StructureTypes.Select(t => t.ToProtobufMessage()));

                return Task.FromResult(response);
            }
            catch (System.Exception e)
            {  
                _logger.LogInformation($"{nameof(GetStructureTypes)}: {e}");
                throw new Grpc.Core.RpcException(new Status(StatusCode.Unknown, nameof(GetStructureTypes), e));
            }
        }

        public override Task<GetStructureTypesByIDsResponse> GetStructureTypesByIDs(GetStructureTypesByIDsRequest request, ServerCallContext context)
        {
            GetStructureTypesByIDsResponse response = new GetStructureTypesByIDsResponse();
            foreach (var chunk in request.Ids.ToArray().Chunk())
            {
                try
                {
                    response.Results.AddRange(_context.StructureTypes.Where(t => chunk.Contains(t.Id)).Select(t => t.ToProtobufMessage()));

                }
                catch (System.Exception e)
                {
                    _logger.LogInformation($"{nameof(GetStructureTypes)}: {e}");
                    throw new Grpc.Core.RpcException(new Status(StatusCode.Unknown, nameof(GetStructureTypes), e));
                }
            }

            return Task.FromResult(response);
        }

        public override async Task<UpdateStructureTypesResponse> Update(UpdateStructureTypesRequest request, ServerCallContext context)
        {
            try
            {
                UpdateStructureTypesResponse response = new UpdateStructureTypesResponse()
                {
                };

                foreach (var req in request.Objs)
                {
                    //var EF_Result = req..ToStructureType();

                    StructureTypeChangeResponse row_response = new StructureTypeChangeResponse();

                    switch (req.ActionCase)
                    {
                         
                        case StructureTypeChangeRequest.ActionOneofCase.Create:
                            var insert_result = await _context.StructureTypes.AddAsync(req.Create.ToStructureType());
                            row_response.Success = insert_result.State == Microsoft.EntityFrameworkCore.EntityState.Added;
                            row_response.Created = row_response.Success ? insert_result.Entity.ToProtobufMessage() : null;
                            break;
                        case StructureTypeChangeRequest.ActionOneofCase.Update:
                            var update_result = _context.StructureTypes.Update(req.Update.ToStructureType());
                            row_response.Success = update_result.State == Microsoft.EntityFrameworkCore.EntityState.Modified;
                            row_response.Updated = update_result.Entity.ToProtobufMessage();
                            break;
                        case StructureTypeChangeRequest.ActionOneofCase.Delete:
                            var del_row = await _context.StructureTypes.FindAsync(req.Delete);
                            var remove_result = _context.StructureTypes.Remove(del_row);
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
    }
}