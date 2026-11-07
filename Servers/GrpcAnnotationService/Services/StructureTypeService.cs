using Grpc.Core;
using gRPCAnnotationService.Protos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        /// <summary>
        /// Inserts one type, stamping the caller name and Created/LastModified server-side.
        /// The response carries the row with its database-assigned Id. Throws InvalidArgument when the request carries
        /// no structure type, and NotFound, naming the ParentId, without saving when the parent type does not exist;
        /// FK_StructureType_StructureType would otherwise fail the save as a generic Unknown failure.
        /// </summary>
        public override async Task<CreateStructureTypeResponse> CreateStructureType(CreateStructureTypeRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var row = (request.Obj
                    ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "A structure type is required"))).ToStructureType();
                if (row.ParentId.HasValue && await FindLiveAsync(row.ParentId.Value, ct) == null)
                    throw new RpcException(new Status(StatusCode.NotFound, $"Structure Type ID {row.ParentId} not found"));
                row.Username = AnnotationRpc.CallerName(context);
                row.Created = DateTime.UtcNow;
                row.LastModified = row.Created;

                var added = await _context.StructureTypes.AddAsync(row, ct);
                await _context.SaveChangesAsync(ct);

                return new CreateStructureTypeResponse { Result = added.Entity.ToProtobufMessage() };
            }
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(CreateStructureType), e, context); }
        }

        /// <summary>Single type by Id; throws RpcException NotFound (not Internal) when it does not exist.</summary>
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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(GetStructureTypes), e, context); }
        }

        /// <summary>Types for the requested Ids, queried in chunks; Ids that do not exist are silently omitted.</summary>
        public override async Task<GetStructureTypesByIDsResponse> GetStructureTypesByIDs(GetStructureTypesByIDsRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var response = new GetStructureTypesByIDsResponse();
                foreach (var chunk in request.Ids.DistinctChunk())
                {
                    var rows = await _context.StructureTypes.AsNoTracking()
                        .Where(t => chunk.Contains(t.Id)).ToListAsync(ct);
                    response.Results.AddRange(rows.Select(t => t.ToProtobufMessage()));
                }

                return response;
            }
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(GetStructureTypesByIDs), e, context); }
        }

        /// <summary>
        /// One SaveChanges for the whole batch. A failed row sets Success=false and does not roll back the others.
        /// Each change sees the changes before it: an update or delete of an Id that does not exist, or that the
        /// batch already deleted, fails its own row. A delete of a type that is still in use (see
        /// <see cref="TypesInUseAsync"/>), or that an earlier create or update of the batch made a parent, also fails
        /// its own row. A create, or an update that changes ParentId, naming a parent that does not exist or that the
        /// batch already deleted fails its own row with no Updated (see <see cref="LiveParentIdsAsync"/>), and so does an
        /// update that moves a type under itself or one of its own descendants (see <see cref="IsSelfOrDescendantAsync"/>).
        /// Created rows are reported after the save, so each carries the Id the database assigned.
        /// </summary>
        public override async Task<UpdateStructureTypesResponse> Update(UpdateStructureTypesRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var username = AnnotationRpc.CallerName(context);
                var response = new UpdateStructureTypesResponse();
                var pendingCreates = new List<(StructureTypeChangeResponse Response, Viking.DataModel.Annotation.StructureType Entity)>();
                var inUse = await TypesInUseAsync(request.Objs
                    .Where(c => c.ActionCase == StructureTypeChangeRequest.ActionOneofCase.Delete)
                    .Select(c => c.Delete).ToHashSet(), ct);
                var liveParents = await LiveParentIdsAsync(request, ct);
                var newParents = new HashSet<long>();

                foreach (var req in request.Objs)
                {
                    var row_response = new StructureTypeChangeResponse();

                    switch (req.ActionCase)
                    {
                        case StructureTypeChangeRequest.ActionOneofCase.Create:
                            if (req.Create.HasParentId && !liveParents.Contains(req.Create.ParentId))
                            {
                                row_response.Success = false;
                                break;
                            }

                            var ef_obj = req.Create.ToStructureType();
                            ef_obj.Username = username;
                            ef_obj.Created = DateTime.UtcNow;
                            ef_obj.LastModified = ef_obj.Created;
                            var insertResult = await _context.StructureTypes.AddAsync(ef_obj, ct);
                            row_response.Success = true;
                            pendingCreates.Add((row_response, insertResult.Entity));
                            if (ef_obj.ParentId.HasValue)
                                newParents.Add(ef_obj.ParentId.Value);
                            break;
                        case StructureTypeChangeRequest.ActionOneofCase.Update:
                            var obj = await FindLiveAsync(req.Update.Id, ct);
                            var moves = obj != null && req.Update.HasParentId && req.Update.ParentId != obj.ParentId;
                            if (obj != null && (!moves || (liveParents.Contains(req.Update.ParentId)
                                                           && !await IsSelfOrDescendantAsync(req.Update.ParentId, obj.Id, ct))))
                            {
                                if (moves)
                                    newParents.Add(req.Update.ParentId);

                                // Tracked, so change tracking saves the new values.
                                req.Update.Sync(ref obj);
                                obj.Username = username;
                                row_response.Success = true;
                                row_response.Updated = obj.ToProtobufMessage();
                            }
                            else
                            {
                                row_response.Success = false;
                            }
                            break;
                        case StructureTypeChangeRequest.ActionOneofCase.Delete:
                            var EF_remove_row = await FindLiveAsync(req.Delete, ct);
                            if (EF_remove_row != null && !inUse.Contains(req.Delete) && !newParents.Contains(req.Delete))
                            {
                                _context.StructureTypes.Remove(EF_remove_row);
                                liveParents.Remove(req.Delete);
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

                foreach (var (rowResponse, entity) in pendingCreates)
                    rowResponse.Created = entity.ToProtobufMessage();

                return response;
            }
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(Update), e, context); }
        }

        /// <summary>
        /// The delete Ids that SaveChanges could not remove, because none of the foreign keys into StructureType
        /// cascade: types named by a structure, a structure template or a permitted structure link, and parents of a
        /// child type the batch keeps (a child the batch deletes keeps its parent too when the child is in use).
        /// Without this check the failed DELETE would roll back the whole batch. A batch with no delete costs no query.
        /// </summary>
        private async Task<HashSet<long>> TypesInUseAsync(HashSet<long> deleteIds, CancellationToken ct)
        {
            var inUse = new HashSet<long>();
            if (deleteIds.Count == 0)
                return inUse;

            var children = new List<(long Id, long ParentId)>();
            foreach (var chunk in deleteIds.DistinctChunk())
            {
                inUse.UnionWith(await _context.Structures
                    .Where(s => chunk.Contains(s.TypeId)).Select(s => s.TypeId).Distinct().ToListAsync(ct));
                inUse.UnionWith(await _context.StructureTemplates
                    .Where(t => chunk.Contains(t.StructureTypeId)).Select(t => t.StructureTypeId).Distinct().ToListAsync(ct));
                inUse.UnionWith(await _context.PermittedStructureLinks
                    .Where(p => chunk.Contains(p.SourceTypeId)).Select(p => p.SourceTypeId).Distinct().ToListAsync(ct));
                inUse.UnionWith(await _context.PermittedStructureLinks
                    .Where(p => chunk.Contains(p.TargetTypeId)).Select(p => p.TargetTypeId).Distinct().ToListAsync(ct));
                children.AddRange((await _context.StructureTypes
                    .Where(t => chunk.Contains(t.ParentId.Value)).Select(t => new { t.Id, ParentId = t.ParentId.Value })
                    .ToListAsync(ct)).Select(c => (c.Id, c.ParentId)));
            }

            bool grew;
            do
            {
                grew = false;
                foreach (var (id, parentId) in children)
                {
                    if ((!deleteIds.Contains(id) || inUse.Contains(id)) && inUse.Add(parentId))
                        grew = true;
                }
            } while (grew);

            return inUse;
        }

        /// <summary>
        /// The stored types, among the ParentIds the batch's creates and updates name, that exist. FK_StructureType_StructureType
        /// does not cascade, so saving a type under any other parent would fail and roll back the whole batch; that row
        /// fails instead. Update removes a type from the set when a row deletes it. A batch naming no parent costs no query.
        /// </summary>
        private async Task<HashSet<long>> LiveParentIdsAsync(UpdateStructureTypesRequest request, CancellationToken ct)
        {
            var named = request.Objs
                .Where(c => c.ActionCase == StructureTypeChangeRequest.ActionOneofCase.Create && c.Create.HasParentId).Select(c => c.Create.ParentId)
                .Concat(request.Objs
                    .Where(c => c.ActionCase == StructureTypeChangeRequest.ActionOneofCase.Update && c.Update.HasParentId)
                    .Select(c => c.Update.ParentId)).ToArray();

            var live = new HashSet<long>();
            if (named.Length == 0)
                return live;

            foreach (var chunk in named.DistinctChunk())
                live.UnionWith(await _context.StructureTypes.AsNoTracking()
                    .Where(t => chunk.Contains(t.Id)).Select(t => t.Id).ToListAsync(ct));

            return live;
        }

        /// <summary>
        /// True when <paramref name="id"/> is <paramref name="ancestorId"/> or lies below it in the type tree as the batch
        /// so far leaves it, so making <paramref name="id"/> the parent of <paramref name="ancestorId"/> would close a
        /// cycle that FK_StructureType_StructureType does not prevent. The walk follows ParentId upward through
        /// <see cref="FindLiveAsync"/>, one lookup per ancestor not yet tracked, and stops at a root, at a type the batch
        /// deleted, or at a type it has already visited (a cycle already stored that does not pass through
        /// <paramref name="ancestorId"/>).
        /// </summary>
        private async Task<bool> IsSelfOrDescendantAsync(long id, long ancestorId, CancellationToken ct)
        {
            var visited = new HashSet<long>();
            long? current = id;
            while (current.HasValue && visited.Add(current.Value))
            {
                if (current.Value == ancestorId)
                    return true;
                current = (await FindLiveAsync(current.Value, ct))?.ParentId;
            }

            return false;
        }

        /// <summary>
        /// The type with this Id as the batch so far leaves it: Find looks in the change tracker before the
        /// database, and a type queued for delete counts as absent.
        /// </summary>
        private async Task<Viking.DataModel.Annotation.StructureType> FindLiveAsync(long id, CancellationToken ct)
        {
            var row = await _context.StructureTypes.FindAsync(new object[] { id }, ct);
            return row == null || _context.Entry(row).State == EntityState.Deleted ? null : row;
        }
    }
}
