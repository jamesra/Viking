using Grpc.Core;
using gRPCAnnotationService.Protos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Viking.DataModel.Annotation;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;

namespace gRPCAnnotationService
{
    /// <summary>
    /// Structure-type table. Clients load it in full at startup (GetStructureTypes); there is no
    /// incremental DeletedIds API. ParentId 0 in proto is "no parent" — converters use HasParentId.
    /// </summary>
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
                var ct = context.CancellationToken;
                var row = request.Obj.ToStructureType();
                row.Username = AnnotationRpc.CallerName(context);
                row.Created = DateTime.UtcNow;
                row.LastModified = row.Created;

                var added = await _context.StructureTypes.AddAsync(row, ct);
                await _context.SaveChangesAsync(ct);

                return new CreateStructureTypeResponse { Result = added.Entity.ToProtobufMessage() };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "{Operation} failed", nameof(CreateStructureType));
                throw new RpcException(new Status(StatusCode.Unknown, nameof(CreateStructureType), e));
            }
        }

        public override async Task<GetStructureTypeByIDResponse> GetStructureTypeByID(GetStructureTypeByIDRequest request, ServerCallContext context)
        {
            var obj = await _context.StructureTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == request.Id, context.CancellationToken);
            if (obj == null)
                throw new RpcException(new Status(StatusCode.NotFound, $"Structure Type ID {request.Id} not found"));

            return new GetStructureTypeByIDResponse { Result = obj.ToProtobufMessage() };
        }

        /// <summary>Entire type table. Clients must CallOnCollectionChanged so RootObjects/Children wire.</summary>
        public override async Task<GetStructureTypesResponse> GetStructureTypes(GetStructureTypesRequest request, ServerCallContext context)
        {
            try
            {
                var rows = await _context.StructureTypes.AsNoTracking().ToListAsync(context.CancellationToken);
                var response = new GetStructureTypesResponse();
                response.Results.AddRange(rows.Select(t => t.ToProtobufMessage()));
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "{Operation} failed", nameof(GetStructureTypes));
                throw new RpcException(new Status(StatusCode.Unknown, nameof(GetStructureTypes), e));
            }
        }

        public override async Task<GetStructureTypesByIDsResponse> GetStructureTypesByIDs(GetStructureTypesByIDsRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var response = new GetStructureTypesByIDsResponse();
                foreach (var chunk in request.Ids.ToArray().Chunk())
                {
                    var rows = await _context.StructureTypes.AsNoTracking()
                        .Where(t => chunk.Contains(t.Id)).ToListAsync(ct);
                    response.Results.AddRange(rows.Select(t => t.ToProtobufMessage()));
                }

                return response;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "{Operation} failed", nameof(GetStructureTypesByIDs));
                throw new RpcException(new Status(StatusCode.Unknown, nameof(GetStructureTypesByIDs), e));
            }
        }

        /// <summary>
        /// One SaveChanges for the whole batch. A failed row sets Success=false and does not roll back the others.
        /// </summary>
        public override async Task<UpdateStructureTypesResponse> Update(UpdateStructureTypesRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var username = AnnotationRpc.CallerName(context);
                var response = new UpdateStructureTypesResponse();

                foreach (var req in request.Objs)
                {
                    var row_response = new StructureTypeChangeResponse();

                    switch (req.ActionCase)
                    {
                        case StructureTypeChangeRequest.ActionOneofCase.Create:
                            var ef_obj = req.Create.ToStructureType();
                            ef_obj.Username = username;
                            ef_obj.Created = DateTime.UtcNow;
                            ef_obj.LastModified = ef_obj.Created;
                            var insertResult = await _context.StructureTypes.AddAsync(ef_obj, ct);
                            row_response.Success = true;
                            row_response.Created = insertResult.Entity.ToProtobufMessage();
                            break;
                        case StructureTypeChangeRequest.ActionOneofCase.Update:
                            var obj = await _context.StructureTypes.FirstOrDefaultAsync(t => t.Id == req.Update.Id, ct);
                            if (obj != null)
                            {
                                req.Update.Sync(ref obj);
                                obj.Username = username;
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
                            var EF_remove_row = await _context.StructureTypes.FirstOrDefaultAsync(t => t.Id == req.Delete, ct);
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

                await _context.SaveChangesAsync(ct);

                return response;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "{Operation} failed", nameof(Update));
                throw new RpcException(new Status(StatusCode.Unknown, nameof(Update), e));
            }
        }
    }
}
