using Grpc.Core;
using gRPCAnnotationService.Protos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.DataModel.Annotation;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using EfPermittedStructureLink = Viking.DataModel.Annotation.PermittedStructureLink;
using ProtoPermittedStructureLink = Viking.AnnotationServiceTypes.gRPC.V1.Protos.PermittedStructureLink;

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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(GetPermittedStructureLinks), e, context); }
        }

        /// <summary>Inserts one permitted link and returns the stored row.</summary>
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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(CreatePermittedStructureLink), e, context); }
        }

        /// <summary>
        /// Applies a batch of insert, update, and delete changes in order, in one save. Each change sees
        /// the changes before it: <c>Sucess</c> is false, and only that row is skipped, for an insert whose
        /// row (keyed by source and target type) already exists or is queued, and for an update or delete
        /// whose row does not exist or was deleted earlier in the batch. Only <c>Bidirectional</c> can
        /// change on update. A delete echoes the request key.
        /// </summary>
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
                            if (await FindLiveAsync(r.Result, ct) != null)
                            {
                                row_response.Sucess = false;
                                break;
                            }
                            var inserted = await _context.PermittedStructureLinks.AddAsync(r.Result.ToPermittedStructureLink(), ct);
                            row_response.Sucess = true;
                            row_response.Result = inserted.Entity.ToProtobufMessage();
                            break;
                        case DBAction.Update:
                            var psl = await FindLiveAsync(r.Result, ct);
                            if (psl == null)
                            {
                                row_response.Sucess = false;
                                break;
                            }
                            // Tracked (stored, or queued by this batch), so change tracking saves the new value.
                            psl.Bidirectional = r.Result.Bidirectional;
                            row_response.Sucess = true;
                            row_response.Result = psl.ToProtobufMessage();
                            break;
                        case DBAction.Delete:
                            var EF_remove_row = await FindLiveAsync(r.Result, ct);
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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(UpdatePermittedStructureLinks), e, context); }
        }

        /// <summary>
        /// The link with this key as the batch so far leaves it: Find looks in the change tracker before the
        /// database, so a row queued by an earlier insert is found, and one queued for delete counts as absent.
        /// </summary>
        private async Task<EfPermittedStructureLink> FindLiveAsync(ProtoPermittedStructureLink key, CancellationToken ct)
        {
            var row = await _context.PermittedStructureLinks.FindAsync(new object[] { key.SourceTypeId, key.TargetTypeId }, ct);
            return row == null || _context.Entry(row).State == EntityState.Deleted ? null : row;
        }
    }
}
