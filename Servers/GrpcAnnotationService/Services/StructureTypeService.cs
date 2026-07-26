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
                GetStructureTypesResponse response = new GetStructureTypesResponse();

                response.Results.AddRange(_context.StructureTypes.Select(t => t.ToProtobufMessage()));

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
            GetStructureTypesByIDsResponse response = new GetStructureTypesByIDsResponse();
            foreach (var chunk in request.Id.ToArray().Chunk())
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

            return response;
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
                    var ef_obj = req.Result.ToStructureType();

                    StructureTypeChangeResponse row_response = new StructureTypeChangeResponse() { Action = req.Action };

                    switch (req.Action)
                    {
                        case DBAction.None:
                            row_response.Sucess = true;
                            break;
                        case DBAction.Insert:
                            var insertResult = await _context.StructureTypes.AddAsync(ef_obj);
                            row_response.Sucess = true;
                            row_response.Result = insertResult.Entity.ToProtobufMessage();
                            break;
                        case DBAction.Update:
                            var obj = _context.StructureTypes.FirstOrDefault(t => t.Id == ef_obj.Id);
                            if (obj != null)
                            {
                                req.Result.Sync(ref obj);
                                var EF_Result = _context.StructureTypes.Update(obj);
                                row_response.Sucess = true;
                                row_response.Result = EF_Result.Entity.ToProtobufMessage();
                            }
                            else
                            {
                                row_response.Sucess = false;
                            }
                            break;
                        case DBAction.Delete:
                            var EF_remove_row = _context.StructureTypes.FirstOrDefault(t => t.Id == ef_obj.Id);
                            if (EF_remove_row != null)
                            {
                                _context.StructureTypes.Remove(EF_remove_row);
                                row_response.Sucess = true;
                            }
                            else
                            {
                                row_response.Sucess = false;
                            }
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