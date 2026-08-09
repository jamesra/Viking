using Grpc.Core;
using gRPCAnnotationService.Protos;
using Microsoft.EntityFrameworkCore;
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

        public override async Task<CreateStructureTypeResponse> CreateStructureType(CreateStructureTypeRequest request, ServerCallContext context)
        {
            try
            {
                var row = request.Obj.ToStructureType();
                row.Username = context.GetHttpContext()?.User?.Identity?.Name ?? "unknown";
                row.Created = System.DateTime.UtcNow;
                row.LastModified = row.Created;

                var added = await _context.StructureTypes.AddAsync(row);
                await _context.SaveChangesAsync();

                return new CreateStructureTypeResponse { Result = added.Entity.ToProtobufMessage() };
            }
            catch (System.Exception e)
            {
                _logger.LogError(e, "{Operation} failed", nameof(CreateStructureType));
                throw new Grpc.Core.RpcException(new Status(StatusCode.Unknown, nameof(CreateStructureType), e));
            }
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

        public override async Task<GetStructureTypesResponse> GetStructureTypes(GetStructureTypesRequest request, ServerCallContext context)
        {
            try
            {
                var rows = await _context.StructureTypes.AsNoTracking().ToListAsync();
                var response = new GetStructureTypesResponse();
                response.Results.AddRange(rows.Select(t => t.ToProtobufMessage()));
                return response;
            }
            catch (System.Exception e)
            {
                _logger.LogInformation($"{nameof(GetStructureTypes)}: {e}");
                throw new Grpc.Core.RpcException(new Status(StatusCode.Unknown, nameof(GetStructureTypes), e));
            }
        }

        public override async Task<GetStructureTypesByIDsResponse> GetStructureTypesByIDs(GetStructureTypesByIDsRequest request, ServerCallContext context)
        {
            try
            {
                var response = new GetStructureTypesByIDsResponse();
                foreach (var chunk in request.Ids.ToArray().Chunk())
                {
                    var rows = await _context.StructureTypes.AsNoTracking()
                        .Where(t => chunk.Contains(t.Id)).ToListAsync();
                    response.Results.AddRange(rows.Select(t => t.ToProtobufMessage()));
                }

                return response;
            }
            catch (System.Exception e)
            {
                _logger.LogInformation($"{nameof(GetStructureTypesByIDs)}: {e}");
                throw new Grpc.Core.RpcException(new Status(StatusCode.Unknown, nameof(GetStructureTypesByIDs), e));
            }
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
                    StructureTypeChangeResponse row_response = new StructureTypeChangeResponse();

                    switch (req.ActionCase)
                    {
                        case StructureTypeChangeRequest.ActionOneofCase.Create:
                            var ef_obj = req.Create.ToStructureType();
                            var insertResult = await _context.StructureTypes.AddAsync(ef_obj);
                            row_response.Success = true;
                            row_response.Created = insertResult.Entity.ToProtobufMessage();
                            break;
                        case StructureTypeChangeRequest.ActionOneofCase.Update:
                            var obj = _context.StructureTypes.FirstOrDefault(t => t.Id == req.Update.Id);
                            if (obj != null)
                            {
                                req.Update.Sync(ref obj);
                                var EF_Result = _context.StructureTypes.Update(obj);
                                row_response.Success = true;
                                row_response.Updated = EF_Result.Entity.ToProtobufMessage();
                            }
                            else
                            {
                                row_response.Success = false;
                            }
                            break;
                        case StructureTypeChangeRequest.ActionOneofCase.Delete:
                            var EF_remove_row = _context.StructureTypes.FirstOrDefault(t => t.Id == req.Delete);
                            if (EF_remove_row != null)
                            {
                                _context.StructureTypes.Remove(EF_remove_row);
                                row_response.Success = true;
                                row_response.DeletedId = req.Delete;
                            }
                            else
                            {
                                row_response.Success = false;
                            }
                            break;
                        default:
                            row_response.Success = false;
                            break;
                    }

                    response.Results.Add(row_response);
                }

                await _context.SaveChangesAsync();

                return response;
            }
            catch (System.Exception e)
            {
                _logger.LogInformation($"{nameof(Update)}: {e}");
                throw new Grpc.Core.RpcException(new Status(StatusCode.Unknown, nameof(Update), e));
            }
        }
    }
}