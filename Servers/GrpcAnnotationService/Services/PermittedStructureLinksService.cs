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
    /// Allowed type-to-type link rules. Composite key is SourceTypeId+TargetTypeId, not a long ID.
    /// Clients load this after StructureTypes so the updater can attach rules to types already in cache.
    /// </summary>
    public class PermittedStructureLinksService : Viking.AnnotationServiceTypes.gRPC.V1.Protos.PermittedStructureLinks.PermittedStructureLinksBase
    {
        private readonly AnnotationContext _context;
        private readonly ILogger<PermittedStructureLinksService> _logger;
        public PermittedStructureLinksService(AnnotationContext context, ILogger<PermittedStructureLinksService> logger)
        {
            _logger = logger;
            _context = context;
        }

        /// <summary>Full table. No incremental watermark.</summary>
        public override async Task<GetPermittedStructureLinksResponse> GetPermittedStructureLinks(GetPermittedStructureLinksRequest request, ServerCallContext context)
        {
            try
            {
                var rows = await _context.PermittedStructureLinks.AsNoTracking()
                    .ToListAsync(context.CancellationToken);
                var response = new GetPermittedStructureLinksResponse();
                response.PermittedLinks.AddRange(rows.Select(p => p.ToProtobufMessage()));
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "{Operation} failed", nameof(GetPermittedStructureLinks));
                throw new RpcException(new Status(StatusCode.Unknown, nameof(GetPermittedStructureLinks), e));
            }
        }

        public override async Task<CreatePermittedStructureLinkResponse> CreatePermittedStructureLink(CreatePermittedStructureLinkRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var new_obj = request.NewObj.ToPermittedStructureLink();
                var ef_result = await _context.PermittedStructureLinks.AddAsync(new_obj, ct);
                await _context.SaveChangesAsync(ct);

                return new CreatePermittedStructureLinkResponse
                {
                    Result = ef_result.Entity.ToProtobufMessage()
                };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "{Operation} failed", nameof(CreatePermittedStructureLink));
                throw new RpcException(new Status(StatusCode.Unknown, nameof(CreatePermittedStructureLink), e));
            }
        }

        public override async Task<UpdatePermittedStructureLinksResponse> UpdatePermittedStructureLinks(UpdatePermittedStructureLinksRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var response = new UpdatePermittedStructureLinksResponse();

                foreach (var r in request.Changes)
                {
                    var row_response = new PermittedStructureLinkChangeResponse() { Action = r.Action };

                    switch (r.Action)
                    {
                        case DBAction.None:
                            row_response.Sucess = true;
                            break;
                        case DBAction.Insert:
                            var inserted = await _context.PermittedStructureLinks.AddAsync(r.Result.ToPermittedStructureLink(), ct);
                            row_response.Sucess = true;
                            row_response.Result = inserted.Entity.ToProtobufMessage();
                            break;
                        case DBAction.Update:
                            var psl = await _context.PermittedStructureLinks.FirstOrDefaultAsync(
                                row => row.SourceTypeId == r.Result.SourceTypeId && row.TargetTypeId == r.Result.TargetTypeId, ct);
                            if (psl == null)
                            {
                                row_response.Sucess = false;
                                break;
                            }
                            psl.Bidirectional = r.Result.Bidirectional;
                            var EF_Result = _context.PermittedStructureLinks.Update(psl);
                            row_response.Sucess = true;
                            row_response.Result = EF_Result.Entity.ToProtobufMessage();
                            break;
                        case DBAction.Delete:
                            var EF_remove_row = await _context.PermittedStructureLinks.FirstOrDefaultAsync(
                                row => row.SourceTypeId == r.Result.SourceTypeId && row.TargetTypeId == r.Result.TargetTypeId, ct);
                            if (EF_remove_row == null)
                            {
                                row_response.Sucess = false;
                                break;
                            }
                            _context.PermittedStructureLinks.Remove(EF_remove_row);
                            row_response.Sucess = true;
                            // Echo the request key so clients can reconcile DeletedIDs without a row read.
                            row_response.Result = r.Result;
                            break;
                    }

                    response.Changes.Add(row_response);
                }

                await _context.SaveChangesAsync(ct);

                return response;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "{Operation} failed", nameof(UpdatePermittedStructureLinks));
                throw new RpcException(new Status(StatusCode.Unknown, nameof(UpdatePermittedStructureLinks), e));
            }
        }
    }
}
