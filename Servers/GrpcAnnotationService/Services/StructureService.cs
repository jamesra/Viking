using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using gRPCAnnotationService.Protos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.DataModel.Annotation;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using EfLocation = Viking.DataModel.Annotation.Location;
using EfLocationLink = Viking.DataModel.Annotation.LocationLink;
using EfStructure = Viking.DataModel.Annotation.Structure;
using EfStructureLink = Viking.DataModel.Annotation.StructureLink;
using ProtoStructure = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Structure;
using ProtoStructureLink = Viking.AnnotationServiceTypes.gRPC.V1.Protos.StructureLink;

namespace gRPCAnnotationService
{
    /// <summary>
    /// Structure and structure-link RPCs. Links ride on Structure.Links (AttachStructureLinksAsync).
    /// Incremental section/region reads return volume-wide DeletedIds — a structure can span sections.
    /// </summary>
    public class StructureService : Viking.AnnotationServiceTypes.gRPC.V1.Protos.AnnotateStructures.AnnotateStructuresBase
    {
        private readonly AnnotationContext _context;
        private readonly ILogger<StructureService> _logger;

        public StructureService(AnnotationContext context, ILogger<StructureService> logger)
        {
            _logger = logger;
            _context = context;
        }

        private RpcException Failure(string operation, Exception e)
        {
            _logger.LogError(e, "{Operation} failed", operation);
            return new RpcException(new Status(StatusCode.Unknown, operation, e));
        }

        #region Reads

        public override async Task<GetStructureByIDResponse> GetStructureByID(GetStructureByIDRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var obj = await _context.Structures.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == request.Id, ct);
                if (obj == null)
                    throw new RpcException(new Status(StatusCode.NotFound, $"Structure ID {request.Id} not found"));

                var result = obj.ToProtobufMessage();
                await AttachStructureLinksAsync(new[] { result }, ct);
                return new GetStructureByIDResponse { Result = result };
            }
            catch (RpcException) { throw; }
            catch (Exception e) { throw Failure(nameof(GetStructureByID), e); }
        }

        /// <summary>
        /// Found rows only. IDs not in Results were deleted or never existed — no DeletedIds field.
        /// </summary>
        public override async Task<GetStructuresByIDResponse> GetStructuresByID(GetStructuresByIDRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var response = new GetStructuresByIDResponse();
                foreach (var chunk in request.Ids.ToArray().Chunk())
                {
                    var rows = await _context.Structures.AsNoTracking()
                        .Where(s => chunk.Contains(s.Id)).ToListAsync(ct);
                    response.Results.AddRange(rows.Select(s => s.ToProtobufMessage()));
                }

                await AttachStructureLinksAsync(response.Results, ct);
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetStructuresByID), e); }
        }

        public override async Task<GetStructuresResponse> GetStructures(GetStructuresRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var rows = await _context.Structures.AsNoTracking().ToListAsync(ct);
                var response = new GetStructuresResponse();
                response.Results.AddRange(rows.Select(s => s.ToProtobufMessage()));
                await AttachStructureLinksAsync(response.Results, ct);
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetStructures), e); }
        }

        /// <summary>
        /// Structures that have any location on Z. DeletedIds is every structure deleted after the
        /// watermark, not just those that touched this section.
        /// </summary>
        public override async Task<GetStructuresForSectionResponse> GetStructuresForSection(GetStructuresForSectionRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var queryStart = DateTime.UtcNow;
                var modifiedAfter = TimestampFilters.ModifiedAfterOrNull(request.ModifiedAfterThisUtcTime);

                var rows = await StructuresOnSection(request.Z, modifiedAfter).ToListAsync(ct);

                var response = new GetStructuresForSectionResponse
                {
                    QueryExecutedTime = Timestamp.FromDateTime(queryStart)
                };
                response.Results.AddRange(rows.Select(s => s.ToProtobufMessage()));
                await AttachStructureLinksAsync(response.Results, ct);
                response.DeletedIds.AddRange(await DeletedStructureIdsSince(modifiedAfter, ct));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetStructuresForSection), e); }
        }

        public override async Task<GetStructuresInMosaicRegionResponse> GetStructuresInMosaicRegion(GetStructuresInMosaicRegionRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var queryStart = DateTime.UtcNow;
                var modifiedAfter = TimestampFilters.ModifiedAfterOrNull(request.ModifiedAfterThisUtcTime);
                var rows = await StructuresInRegion(request.Z, request.Region, request.MinRadius,
                    modifiedAfter, useVolumeCoordinates: false, ct);

                var response = new GetStructuresInMosaicRegionResponse
                {
                    QueryExecutedTime = Timestamp.FromDateTime(queryStart)
                };
                response.Results.AddRange(rows.Select(s => s.ToProtobufMessage()));
                await AttachStructureLinksAsync(response.Results, ct);
                response.DeletedIds.AddRange(await DeletedStructureIdsSince(modifiedAfter, ct));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetStructuresInMosaicRegion), e); }
        }

        public override async Task<GetStructuresInVolumeRegionResponse> GetStructuresInVolumeRegion(GetStructuresInVolumeRegionRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var queryStart = DateTime.UtcNow;
                var modifiedAfter = TimestampFilters.ModifiedAfterOrNull(request.ModifiedAfterThisUtcTime);
                var rows = await StructuresInRegion(request.Z, request.Region, request.MinRadius,
                    modifiedAfter, useVolumeCoordinates: true, ct);

                var response = new GetStructuresInVolumeRegionResponse
                {
                    QueryExecutedTime = Timestamp.FromDateTime(queryStart)
                };
                response.Results.AddRange(rows.Select(s => s.ToProtobufMessage()));
                await AttachStructureLinksAsync(response.Results, ct);
                response.DeletedIds.AddRange(await DeletedStructureIdsSince(modifiedAfter, ct));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetStructuresInVolumeRegion), e); }
        }

        public override async Task<GetStructuresOfTypeResponse> GetStructuresOfType(GetStructuresOfTypeRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var rows = await _context.Structures.AsNoTracking()
                    .Where(s => s.TypeId == request.Id).ToListAsync(ct);

                var response = new GetStructuresOfTypeResponse();
                response.Results.AddRange(rows.Select(s => s.ToProtobufMessage()));
                await AttachStructureLinksAsync(response.Results, ct);
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetStructuresOfType), e); }
        }

        public override async Task<GetChildStructuresResponse> GetChildStructures(GetChildStructuresRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var rows = await _context.Structures.AsNoTracking()
                    .Where(s => s.ParentId == request.StructureId).ToListAsync(ct);

                var response = new GetChildStructuresResponse();
                response.Results.AddRange(rows.Select(s => s.ToProtobufMessage()));
                await AttachStructureLinksAsync(response.Results, ct);
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetChildStructures), e); }
        }

        public override async Task<GetLinkedStructuresResponse> GetLinkedStructures(GetLinkedStructuresRequest request, ServerCallContext context)
        {
            try
            {
                var links = await _context.StructureLinks.AsNoTracking()
                    .Where(l => l.SourceId == request.Id || l.TargetId == request.Id)
                    .ToListAsync(context.CancellationToken);

                var response = new GetLinkedStructuresResponse();
                response.Results.AddRange(links.Select(ToProtobufMessage));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetLinkedStructures), e); }
        }

        public override async Task<NumberOfLocationsResponse> NumberOfLocations(NumberOfLocationsRequest request, ServerCallContext context)
        {
            try
            {
                var count = await _context.Locations.AsNoTracking()
                    .CountAsync(l => l.ParentId == request.Id, context.CancellationToken);
                return new NumberOfLocationsResponse { Result = count };
            }
            catch (Exception e) { throw Failure(nameof(NumberOfLocations), e); }
        }

        public override async Task<GetNetworkedStructuresResponse> GetNetworkedStructures(GetNetworkedStructuresRequest request, ServerCallContext context)
        {
            try
            {
                var rows = await _context.Procedures.SelectNetworkStructureIDsAsync(
                    IdTable(request.Ids), request.NumHops, cancellationToken: context.CancellationToken);

                var response = new GetNetworkedStructuresResponse();
                response.Results.AddRange(rows.Select(r => r.ID));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetNetworkedStructures), e); }
        }

        public override async Task<GetChildStructuresInNetworkResponse> GetChildStructuresInNetwork(GetChildStructuresInNetworkRequest request, ServerCallContext context)
        {
            try
            {
                var rows = await _context.Procedures.SelectNetworkChildStructuresAsync(
                    IdTable(request.Ids), request.NumHops, cancellationToken: context.CancellationToken);

                var response = new GetChildStructuresInNetworkResponse();
                foreach (var r in rows)
                {
                    var structure = new ProtoStructure
                    {
                        Id = r.ID,
                        TypeId = r.TypeID,
                        Notes = r.Notes ?? string.Empty,
                        Verified = r.Verified,
                        Attributes = r.Tags ?? string.Empty,
                        Confidence = r.Confidence,
                        Label = r.Label ?? string.Empty,
                        Username = r.Username ?? string.Empty,
                        Created = Timestamp.FromDateTime(DateTime.SpecifyKind(r.Created, DateTimeKind.Utc)),
                        LastModified = Timestamp.FromDateTime(DateTime.SpecifyKind(r.LastModified, DateTimeKind.Utc))
                    };

                    // parent_id is an optional field: leave it unset for root structures rather
                    // than sending zero, which is a valid structure ID.
                    if (r.ParentID.HasValue)
                        structure.ParentId = r.ParentID.Value;

                    response.Results.Add(structure);
                }

                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetChildStructuresInNetwork), e); }
        }

        public override async Task<GetStructureLinksInNetworkResponse> GetStructureLinksInNetwork(GetStructureLinksInNetworkRequest request, ServerCallContext context)
        {
            try
            {
                var rows = await _context.Procedures.SelectNetworkStructureLinksAsync(
                    IdTable(request.Ids), request.NumHops, cancellationToken: context.CancellationToken);

                var response = new GetStructureLinksInNetworkResponse();
                response.Results.AddRange(rows.Select(r => new ProtoStructureLink
                {
                    SourceId = r.SourceID,
                    TargetId = r.TargetID,
                    Bidirectional = r.Bidirectional,
                    Tags = r.Tags ?? string.Empty,
                    Username = r.Username ?? string.Empty
                }));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetStructureLinksInNetwork), e); }
        }

        public override async Task<GetUnfinishedLocationsResponse> GetUnfinishedLocations(GetUnfinishedLocationsRequest request, ServerCallContext context)
        {
            try
            {
                var rows = await _context.Procedures.SelectUnfinishedStructureBranchesAsync(
                    request.Id, cancellationToken: context.CancellationToken);
                var response = new GetUnfinishedLocationsResponse();
                response.Results.AddRange(rows.Select(r => r.ID));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetUnfinishedLocations), e); }
        }

        public override async Task<GetUnfinishedLocationsWithPositionResponse> GetUnfinishedLocationsWithPosition(GetUnfinishedLocationsWithPositionRequest request, ServerCallContext context)
        {
            try
            {
                var rows = await _context.Procedures.SelectUnfinishedStructureBranchesWithPositionAsync(
                    request.Id, cancellationToken: context.CancellationToken);
                var response = new GetUnfinishedLocationsWithPositionResponse();
                response.Results.AddRange(rows.Select(r => new LocationPositionOnly
                {
                    Id = r.ID,
                    Position = new AnnotationPoint { X = r.X, Y = r.Y, Z = r.Z },
                    Radius = r.Radius ?? 0
                }));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetUnfinishedLocationsWithPosition), e); }
        }

        public override Task<GetStructureChangeLogResponse> GetStructureChangeLog(GetStructureChangeLogRequest request, ServerCallContext context)
        {
            // SelectStructureChangeLogResult was scaffolded with no columns, so the procedure's
            // result set cannot be materialised. The WCF service disabled this endpoint for the
            // same reason. Say so rather than returning an empty list that reads as "no changes".
            throw new RpcException(new Status(StatusCode.Unimplemented,
                $"{nameof(GetStructureChangeLog)} needs SelectStructureChangeLogResult to be scaffolded with its columns before it can return rows."));
        }

        #endregion

        #region Writes

        /// <summary>
        /// Inserts the structure, then its first location (optional). Circle WKT is written after
        /// the location has an identity, same as CreateLocation.
        /// </summary>
        public override async Task<CreateStructureResponse> CreateStructure(CreateStructureRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var username = AnnotationRpc.CallerName(context);

                var structure = request.NewStructure.ToStructure();
                structure.Username = username;
                structure.Created = DateTime.UtcNow;
                structure.LastModified = structure.Created;

                await using var tx = await _context.Database.BeginTransactionAsync(ct);

                var addedStructure = await _context.Structures.AddAsync(structure, ct);
                await _context.SaveChangesAsync(ct);

                EfLocation addedLocation = null;
                if (request.NewAnnotation != null)
                {
                    addedLocation = request.NewAnnotation.ToLocation();
                    addedLocation.ParentId = addedStructure.Entity.Id;
                    addedLocation.Username = username;
                    addedLocation.Created = DateTime.UtcNow;
                    addedLocation.LastModified = addedLocation.Created;
                    await _context.Locations.AddAsync(addedLocation, ct);
                    await _context.SaveChangesAsync(ct);
                    if (request.NewAnnotation.TypeCode == AnnotationType.Circle)
                    {
                        var (mosaicWkt, volumeWkt) = request.NewAnnotation.CircleShapeWkt();
                        await _context.PersistCircleShapesAsync(addedLocation, mosaicWkt, volumeWkt, ct);
                    }

                    await PersistLocationProtoLinksAsync(request.NewAnnotation.Links, addedLocation.Id, username, ct);
                    await _context.SaveChangesAsync(ct);
                }

                await tx.CommitAsync(ct);

                var response = new CreateStructureResponse
                {
                    NewStructure = addedStructure.Entity.ToProtobufMessage()
                };
                if (addedLocation != null)
                    response.NewAnnotation = addedLocation.ToProtobufMessage();

                return response;
            }
            catch (Exception e) { throw Failure(nameof(CreateStructure), e); }
        }

        public override async Task<UpdateStructuresResponse> Update(UpdateStructuresRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var response = new UpdateStructuresResponse();
                var username = AnnotationRpc.CallerName(context);
                var pendingCreates = new List<(StructureChangeResponse Response, EfStructure Entity)>();

                await using var tx = await _context.Database.BeginTransactionAsync(ct);

                foreach (var change in request.Objs)
                {
                    var rowResponse = new StructureChangeResponse();

                    switch (change.ActionCase)
                    {
                        case StructureChangeRequest.ActionOneofCase.Create:
                            var toInsert = change.Create.ToStructure();
                            toInsert.Username = username;
                            toInsert.Created = DateTime.UtcNow;
                            toInsert.LastModified = toInsert.Created;
                            var inserted = await _context.Structures.AddAsync(toInsert, ct);
                            pendingCreates.Add((rowResponse, inserted.Entity));
                            rowResponse.Success = true;
                            break;

                        case StructureChangeRequest.ActionOneofCase.Update:
                            var existing = await _context.Structures.FirstOrDefaultAsync(s => s.Id == change.Update.Id, ct);
                            if (existing == null)
                            {
                                rowResponse.Success = false;
                                break;
                            }

                            ApplyUpdate(change.Update, existing, username);
                            rowResponse.Success = true;
                            rowResponse.Updated = existing.ToProtobufMessage();
                            break;

                        case StructureChangeRequest.ActionOneofCase.Delete:
                            // DeepDeleteStructure removes the locations, links and child structures
                            // that would otherwise block the delete on a foreign key.
                            await _context.Procedures.DeepDeleteStructureAsync(change.Delete, cancellationToken: ct);
                            rowResponse.Success = true;
                            rowResponse.DeletedId = change.Delete;
                            break;

                        default:
                            rowResponse.Success = false;
                            break;
                    }

                    response.Results.Add(rowResponse);
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                foreach (var (rowResponse, entity) in pendingCreates)
                    rowResponse.Created = entity.ToProtobufMessage();

                return response;
            }
            catch (Exception e) { throw Failure(nameof(Update), e); }
        }

        public override async Task<CreateStructureLinkResponse> CreateStructureLink(CreateStructureLinkRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var link = request.NewLink;
                if (link.SourceId == link.TargetId)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "A structure cannot link to itself"));

                var row = new EfStructureLink
                {
                    SourceId = link.SourceId,
                    TargetId = link.TargetId,
                    Bidirectional = link.Bidirectional,
                    Tags = link.Tags,
                    Username = AnnotationRpc.CallerName(context),
                    Created = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                };

                var added = await _context.StructureLinks.AddAsync(row, ct);
                await _context.SaveChangesAsync(ct);

                return new CreateStructureLinkResponse { Result = ToProtobufMessage(added.Entity) };
            }
            catch (RpcException) { throw; }
            catch (Exception e) { throw Failure(nameof(CreateStructureLink), e); }
        }

        public override async Task<UpdateStructureLinksResponse> UpdateLinks(UpdateStructureLinksRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var username = AnnotationRpc.CallerName(context);

                foreach (var link in request.Objs)
                {
                    var existing = await _context.StructureLinks.FirstOrDefaultAsync(
                        l => l.SourceId == link.SourceId && l.TargetId == link.TargetId, ct);

                    if (existing == null)
                    {
                        await _context.StructureLinks.AddAsync(new EfStructureLink
                        {
                            SourceId = link.SourceId,
                            TargetId = link.TargetId,
                            Bidirectional = link.Bidirectional,
                            Tags = link.Tags,
                            Username = username,
                            Created = DateTime.UtcNow,
                            LastModified = DateTime.UtcNow
                        }, ct);
                        continue;
                    }

                    existing.Bidirectional = link.Bidirectional;
                    existing.Tags = link.Tags;
                    existing.Username = username;
                    existing.LastModified = DateTime.UtcNow;
                    _context.StructureLinks.Update(existing);
                }

                await _context.SaveChangesAsync(ct);
                return new UpdateStructureLinksResponse();
            }
            catch (Exception e) { throw Failure(nameof(UpdateLinks), e); }
        }

        public override async Task<DeleteStructureLinkResponse> DeleteStructureLink(DeleteStructureLinkRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var existing = await _context.StructureLinks.FirstOrDefaultAsync(
                    l => l.SourceId == request.SourceId && l.TargetId == request.TargetId, ct);
                if (existing == null)
                    throw new RpcException(new Status(StatusCode.NotFound,
                        $"StructureLink {request.SourceId}->{request.TargetId} not found"));

                _context.StructureLinks.Remove(existing);
                await _context.SaveChangesAsync(ct);
                return new DeleteStructureLinkResponse();
            }
            catch (RpcException) { throw; }
            catch (Exception e) { throw Failure(nameof(DeleteStructureLink), e); }
        }

        /// <summary>
        /// MergeId is absorbed into KeepId. Clients must drop MergeId locally; the RPC returns only KeptId.
        /// </summary>
        public override async Task<MergeResponse> Merge(MergeRequest request, ServerCallContext context)
        {
            try
            {
                await _context.Procedures.MergeStructuresAsync(
                    request.KeepId, request.MergeId, cancellationToken: context.CancellationToken);
                return new MergeResponse { KeptId = request.KeepId };
            }
            catch (Exception e) { throw Failure(nameof(Merge), e); }
        }

        /// <summary>
        /// New structure starts at FirstLocationIdOfSplitStructure. The original keeps the rest.
        /// </summary>
        public override async Task<SplitResponse> Split(SplitRequest request, ServerCallContext context)
        {
            try
            {
                var splitId = new OutputParameter<long?>();
                await _context.Procedures.SplitStructureAsync(
                    request.FirstLocationIdOfSplitStructure, splitId, cancellationToken: context.CancellationToken);

                if (splitId.Value.HasValue == false)
                    throw new RpcException(new Status(StatusCode.FailedPrecondition,
                        $"Splitting structure {request.Id} at location {request.FirstLocationIdOfSplitStructure} produced no new structure"));

                return new SplitResponse { SplitStructureId = splitId.Value.Value };
            }
            catch (RpcException) { throw; }
            catch (Exception e) { throw Failure(nameof(Split), e); }
        }

        /// <summary>
        /// Cuts the location link. LocationIdOfKeepStructure stays on the original; the other side becomes the new structure.
        /// </summary>
        public override async Task<SplitAtLocationLinkResponse> SplitAtLocationLink(SplitAtLocationLinkRequest request, ServerCallContext context)
        {
            try
            {
                var splitId = new OutputParameter<long?>();
                await _context.Procedures.SplitStructureAtLocationLinkAsync(
                    request.LocationIdOfKeepStructure, request.LocationIdOfSplitStructure, splitId,
                    cancellationToken: context.CancellationToken);

                if (splitId.Value.HasValue == false)
                    throw new RpcException(new Status(StatusCode.FailedPrecondition,
                        "Splitting at the given location link produced no new structure"));

                return new SplitAtLocationLinkResponse { SplitStructureId = splitId.Value.Value };
            }
            catch (RpcException) { throw; }
            catch (Exception e) { throw Failure(nameof(SplitAtLocationLink), e); }
        }

        #endregion

        #region Helpers

        private static ProtoStructureLink ToProtobufMessage(EfStructureLink src) => new ProtoStructureLink
        {
            SourceId = src.SourceId,
            TargetId = src.TargetId,
            Bidirectional = src.Bidirectional,
            Tags = src.Tags ?? string.Empty,
            Username = src.Username ?? string.Empty
        };

        /// <summary>
        /// Section/region structure queries do not Include StructureLink navigations (the EF
        /// Structure entity has none). Batch-load links touching the returned IDs so clients
        /// can hydrate StructureLinkStore from Structure.Links.
        /// </summary>
        private async Task AttachStructureLinksAsync(IList<ProtoStructure> structures, CancellationToken ct)
        {
            if (structures.Count == 0)
                return;

            var ids = structures.Select(s => s.Id).ToArray();
            var links = new List<EfStructureLink>();
            var seen = new HashSet<(long Source, long Target)>();
            foreach (var chunk in ids.Chunk())
            {
                var rows = await _context.StructureLinks.AsNoTracking()
                    .Where(l => chunk.Contains(l.SourceId) || chunk.Contains(l.TargetId))
                    .ToListAsync(ct);
                foreach (var link in rows)
                {
                    if (seen.Add((link.SourceId, link.TargetId)))
                        links.Add(link);
                }
            }

            if (links.Count == 0)
                return;

            var byId = structures.ToDictionary(s => s.Id);
            foreach (var link in links)
            {
                if (byId.TryGetValue(link.SourceId, out var source))
                    source.Links.Add(ToProtobufMessage(link));
                if (link.TargetId != link.SourceId && byId.TryGetValue(link.TargetId, out var target))
                    target.Links.Add(ToProtobufMessage(link));
            }
        }

        private async Task PersistLocationProtoLinksAsync(IEnumerable<long> peerIds, long locationId, string username, CancellationToken ct)
        {
            foreach (var peerId in peerIds)
            {
                if (peerId == 0 || peerId == locationId)
                    continue;

                var (a, b) = peerId < locationId ? (peerId, locationId) : (locationId, peerId);
                var exists = await _context.LocationLinks.AnyAsync(l => l.A == a && l.B == b, ct);
                if (exists)
                    continue;

                await _context.LocationLinks.AddAsync(new EfLocationLink
                {
                    A = a,
                    B = b,
                    Username = username,
                    Created = DateTime.UtcNow
                }, ct);
            }
        }

        /// <summary>Builds the [dbo].[integer_list] table valued parameter the network procedures take.</summary>
        private static DataTable IdTable(IEnumerable<long> ids)
        {
            var table = new DataTable();
            table.Columns.Add("ID", typeof(long));
            foreach (var id in ids)
                table.Rows.Add(id);
            return table;
        }

        /// <summary>
        /// Structure deletes are volume-wide: a structure can occupy many sections, so every
        /// section client must drop the ID. Location/link watermarks are section-scoped instead.
        /// </summary>
        private async Task<List<long>> DeletedStructureIdsSince(DateTime? modifiedAfter, CancellationToken ct)
        {
            if (modifiedAfter.HasValue == false)
                return new List<long>();

            return await _context.DeletedStructures.AsNoTracking()
                .Where(d => d.DeletedOn > modifiedAfter.Value)
                .Select(d => d.Id)
                .ToListAsync(ct);
        }

        private IQueryable<EfStructure> StructuresOnSection(long z, DateTime? modifiedAfter)
        {
            // A structure belongs to a section when any of its locations sit on it.
            var query = _context.Structures.AsNoTracking()
                .Where(s => s.Locations.Any(l => l.Z == z));

            if (modifiedAfter.HasValue)
                query = query.Where(s => s.LastModified > modifiedAfter.Value);

            return query;
        }

        private async Task<List<EfStructure>> StructuresInRegion(long z, Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry region,
            double minRadius, DateTime? modifiedAfter, bool useVolumeCoordinates, CancellationToken ct)
        {
            var bounds = BoundsOf(region);

            // Compare against the persisted centroid columns rather than the geometry, so the
            // query translates to SQL and never parses a CurvePolygon.
            var query = _context.Structures.AsNoTracking()
                .Where(s => s.Locations.Any(l =>
                    l.Z == z && l.Radius >= minRadius &&
                    (useVolumeCoordinates
                        ? l.VolumeX >= bounds.MinX && l.VolumeX <= bounds.MaxX && l.VolumeY >= bounds.MinY && l.VolumeY <= bounds.MaxY
                        : l.X >= bounds.MinX && l.X <= bounds.MaxX && l.Y >= bounds.MinY && l.Y <= bounds.MaxY)));

            if (modifiedAfter.HasValue)
                query = query.Where(s => s.LastModified > modifiedAfter.Value);

            return await query.ToListAsync(ct);
        }

        private static (double MinX, double MinY, double MaxX, double MaxY) BoundsOf(Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry region)
        {
            var geometry = region?.ToNetTopologyGeometry();
            if (geometry == null)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "A region is required"));

            var envelope = geometry.EnvelopeInternal;
            return (envelope.MinX, envelope.MinY, envelope.MaxX, envelope.MaxY);
        }

        private static void ApplyUpdate(ProtoStructure src, EfStructure dst, string username)
        {
            var updated = src.ToStructure();

            // Proto3 defaults missing TypeId to 0; never clobber an existing FK with that.
            if (updated.TypeId != 0)
                dst.TypeId = updated.TypeId;
            dst.Notes = updated.Notes;
            dst.Verified = updated.Verified;
            dst.Tags = updated.Tags;
            dst.Confidence = updated.Confidence;
            dst.ParentId = updated.ParentId;
            dst.Label = updated.Label;
            dst.Username = username;
            dst.LastModified = DateTime.UtcNow;
        }

        #endregion
    }
}
