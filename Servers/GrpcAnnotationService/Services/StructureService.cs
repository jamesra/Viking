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

        #region Reads

        /// <summary>
        /// One structure by ID, with its links attached. Throws NotFound when the ID does not exist.
        /// </summary>
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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(GetStructureByID), e, context); }
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
                foreach (var chunk in request.Ids.DistinctChunk())
                {
                    var rows = await _context.Structures.AsNoTracking()
                        .Where(s => chunk.Contains(s.Id)).ToListAsync(ct);
                    response.Results.AddRange(rows.Select(s => s.ToProtobufMessage()));
                }

                await AttachStructureLinksAsync(response.Results, ct);
                return response;
            }
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(GetStructuresByID), e, context); }
        }

        /// <summary>
        /// Every structure in the database, with links attached. Unfiltered, so large volumes return a large response.
        /// </summary>
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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(GetStructures), e, context); }
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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(GetStructuresForSection), e, context); }
        }

        /// <summary>
        /// Structures with a location in the region on section Z, tested in mosaic space. MinRadius is a location radius filter. DeletedIds is every structure deleted after the watermark.
        /// </summary>
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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(GetStructuresInMosaicRegion), e, context); }
        }

        /// <summary>
        /// As GetStructuresInMosaicRegion, but the region is tested in volume space.
        /// </summary>
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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(GetStructuresInVolumeRegion), e, context); }
        }

        /// <summary>
        /// All structures whose TypeId equals the request ID, with links attached.
        /// </summary>
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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(GetStructuresOfType), e, context); }
        }

        /// <summary>
        /// Direct children of StructureId (ParentId match), with links attached. Not recursive.
        /// </summary>
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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(GetChildStructures), e, context); }
        }

        /// <summary>
        /// Structure links in which the ID is source or target, in either direction. Returns the links, not the structures.
        /// </summary>
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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(GetLinkedStructures), e, context); }
        }

        /// <summary>
        /// Count of locations whose ParentId is the structure ID. Zero for an unknown ID.
        /// </summary>
        public override async Task<NumberOfLocationsResponse> NumberOfLocations(NumberOfLocationsRequest request, ServerCallContext context)
        {
            try
            {
                var count = await _context.Locations.AsNoTracking()
                    .CountAsync(l => l.ParentId == request.Id, context.CancellationToken);
                return new NumberOfLocationsResponse { Result = count };
            }
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(NumberOfLocations), e, context); }
        }

        /// <summary>
        /// IDs of structures within NumHops links of the requested IDs, via SelectNetworkStructureIDs.
        /// </summary>
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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(GetNetworkedStructures), e, context); }
        }

        /// <summary>
        /// Structures that are children of the networked structures within NumHops links, via SelectNetworkChildStructures. ParentId is left unset for root structures. Links are not attached.
        /// </summary>
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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(GetChildStructuresInNetwork), e, context); }
        }

        /// <summary>
        /// Structure links within NumHops of the requested IDs, via SelectNetworkStructureLinks.
        /// </summary>
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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(GetStructureLinksInNetwork), e, context); }
        }

        /// <summary>
        /// IDs of the unfinished branch locations of a structure, via SelectUnfinishedStructureBranches.
        /// </summary>
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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(GetUnfinishedLocations), e, context); }
        }

        /// <summary>
        /// As GetUnfinishedLocations, with each location's position and radius (0 when null).
        /// </summary>
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
                    Position = new Viking.gRPC.Geometry.V1.Vector2 { X = r.X, Y = r.Y },
                    Section = r.Z,
                    Radius = r.Radius ?? 0
                }));
                return response;
            }
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(GetUnfinishedLocationsWithPosition), e, context); }
        }

        /// <summary>
        /// Always throws Unimplemented; see the comment in the body.
        /// </summary>
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
        /// the location has an identity, same as CreateLocation. Throws NotFound, naming the missing ID, before anything is
        /// saved, when the structure's ParentId names a structure or its TypeId a structure type that does not exist, or
        /// when the first location's Links name a location that does not exist.
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
                if (structure.ParentId.HasValue)
                    await ThrowIfStructuresMissingAsync(new[] { structure.ParentId.Value }, ct);
                if (!(await StoredTypeIdsAsync(new[] { structure.TypeId }, ct)).Contains(structure.TypeId))
                    throw new RpcException(new Status(StatusCode.NotFound, $"Structure type {structure.TypeId} not found"));
                if (request.NewAnnotation != null)
                    await LocationService.ThrowIfLocationsMissingAsync(_context, request.NewAnnotation.Links.Where(peer => peer != 0), ct);

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
                    if (request.NewAnnotation.IsCircle())
                    {
                        var (mosaicWkt, volumeWkt) = request.NewAnnotation.CircleShapeWkt();
                        await _context.PersistCircleShapesAsync(addedLocation, mosaicWkt, volumeWkt, ct);
                    }

                    await LocationService.PersistProtoLinksAsync(_context, request.NewAnnotation.Links, addedLocation.Id, username, ct);
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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(CreateStructure), e, context); }
        }

        /// <summary>
        /// Batch of create, update, and delete changes in one transaction, one result per request row in order. An update of a missing ID reports Success = false instead of failing the batch. Delete runs DeepDeleteStructure, which also removes the structure's locations, links, and children.
        /// An edit of a structure that a later row deletes, directly or as a child, keeps its Success and the delete stands.
        /// A create or update whose ParentId names a structure that does not exist or that the batch deletes, directly or as a child, reports Success = false and is not saved.
        /// A create or update whose TypeId names no structure type reports Success = false and is not saved (see <see cref="LiveTypeIdsAsync"/>).
        /// An update that makes a structure its own parent or moves it under one of its own descendants, counting moves made earlier in the
        /// batch, reports Success = false and is not saved (see <see cref="IsSelfOrDescendantAsync"/>).
        /// </summary>
        public override async Task<UpdateStructuresResponse> Update(UpdateStructuresRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var response = new UpdateStructuresResponse();
                var username = AnnotationRpc.CallerName(context);
                var pendingCreates = new List<(StructureChangeResponse Response, EfStructure Entity)>();
                var pendingUpdates = new List<(StructureChangeResponse Response, EfStructure Entity)>();

                await using var tx = await _context.Database.BeginTransactionAsync(ct);
                var liveTypes = await LiveTypeIdsAsync(request, ct);

                foreach (var change in request.Objs)
                {
                    var rowResponse = new StructureChangeResponse();

                    switch (change.ActionCase)
                    {
                        case StructureChangeRequest.ActionOneofCase.Create:
                            if (!liveTypes.Contains(change.Create.TypeId))
                            {
                                rowResponse.Success = false;
                                break;
                            }

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
                            if (existing == null || (change.Update.TypeId != 0 && !liveTypes.Contains(change.Update.TypeId))
                                || (change.Update.HasParentId && change.Update.ParentId != existing.ParentId
                                    && await IsSelfOrDescendantAsync(change.Update.ParentId, existing.Id, ct)))
                            {
                                rowResponse.Success = false;
                                break;
                            }

                            ApplyUpdate(change.Update, existing, username);
                            pendingUpdates.Add((rowResponse, existing));
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

                await ForgetEditsOfMissingStructuresAsync(request, pendingCreates, pendingUpdates, ct);
                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                foreach (var (rowResponse, entity) in pendingCreates.Where(c => c.Response.Success))
                    rowResponse.Created = entity.ToProtobufMessage();

                return response;
            }
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(Update), e, context); }
        }

        /// <summary>
        /// Creates one link. Throws InvalidArgument when source and target are the same structure, NotFound, naming
        /// the missing IDs, when either structure does not exist, and then AlreadyExists, naming the pair and leaving
        /// the stored link unchanged, when a link with that source and target is stored; none of them saves anything.
        /// The reverse of a stored link is left to the database's reciprocal-link trigger, which refuses it with
        /// FailedPrecondition.
        /// </summary>
        public override async Task<CreateStructureLinkResponse> CreateStructureLink(CreateStructureLinkRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var link = request.NewLink;
                if (link.SourceId == link.TargetId)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "A structure cannot link to itself"));
                await ThrowIfStructuresMissingAsync(new[] { link.SourceId, link.TargetId }, ct);
                if (await _context.StructureLinks.FindAsync(new object[] { link.SourceId, link.TargetId }, ct) != null)
                    throw new RpcException(new Status(StatusCode.AlreadyExists,
                        $"Structure link already exists: {link.SourceId} -> {link.TargetId}"));

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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(CreateStructureLink), e, context); }
        }

        /// <summary>
        /// Upsert by (SourceId, TargetId): updates Bidirectional and Tags on an existing link, inserts a missing one. Returns an empty response.
        /// All or nothing: a row linking a structure to itself refuses the whole request with InvalidArgument before anything is read or saved,
        /// and a row to insert that names a missing structure refuses it with NotFound, naming the missing IDs, before anything is saved.
        /// When a pair appears more than once, its last row wins, as applying the rows in order would leave it.
        /// </summary>
        public override async Task<UpdateStructureLinksResponse> UpdateLinks(UpdateStructureLinksRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var username = AnnotationRpc.CallerName(context);

                var requested = new Dictionary<(long Source, long Target), ProtoStructureLink>();
                foreach (var link in request.Objs)
                {
                    if (link.SourceId == link.TargetId)
                        throw new RpcException(new Status(StatusCode.InvalidArgument, "A structure cannot link to itself"));
                    requested[(link.SourceId, link.TargetId)] = link;
                }

                // One query per chunk of pairs; sources x targets can over-fetch, so match each row to its pair.
                foreach (var pairs in requested.Keys.ToArray().Chunk(1000))
                {
                    var sources = pairs.Select(p => p.Source).Distinct().ToArray();
                    var targets = pairs.Select(p => p.Target).Distinct().ToArray();
                    var existingRows = await _context.StructureLinks
                        .Where(l => sources.Contains(l.SourceId) && targets.Contains(l.TargetId))
                        .ToListAsync(ct);

                    foreach (var existing in existingRows)
                    {
                        if (!requested.Remove((existing.SourceId, existing.TargetId), out var link))
                            continue;
                        existing.Bidirectional = link.Bidirectional;
                        existing.Tags = link.Tags;
                        existing.Username = username;
                        existing.LastModified = DateTime.UtcNow;
                    }
                }

                await ThrowIfStructuresMissingAsync(requested.Values.SelectMany(l => new[] { l.SourceId, l.TargetId }), ct);
                foreach (var link in requested.Values)
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
                }

                await _context.SaveChangesAsync(ct);
                return new UpdateStructureLinksResponse();
            }
            catch (RpcException) { throw; }
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(UpdateLinks), e, context); }
        }

        /// <summary>
        /// Deletes the link (SourceId, TargetId). Throws NotFound when it does not exist.
        /// </summary>
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
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(DeleteStructureLink), e, context); }
        }

        /// <summary>
        /// MergeId is absorbed into KeepId. Clients must drop MergeId locally; the RPC returns only KeptId.
        /// Throws InvalidArgument when KeepId and MergeId are the same structure, NotFound, naming the missing IDs,
        /// when either structure does not exist, and FailedPrecondition, naming both IDs, when KeepId lies below MergeId in
        /// the structure tree (see <see cref="IsSelfOrDescendantAsync"/>); none runs dbo.MergeStructures. Before DBVersion 94
        /// the procedure, which is not transactional, rewrote a self-merged structure's Notes to "Merged into structure ..."
        /// and then deleted it or failed on the Location foreign key, and moving MergeId's children to a descendant KeepId
        /// made KeepId its own parent or closed a parent cycle; since DBVersion 94 it refuses both with error 50000.
        /// </summary>
        public override async Task<MergeResponse> Merge(MergeRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                if (request.KeepId == request.MergeId)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "A structure cannot be merged into itself"));
                await ThrowIfStructuresMissingAsync(new[] { request.KeepId, request.MergeId }, ct);
                if (await IsSelfOrDescendantAsync(request.KeepId, request.MergeId, ct))
                    throw new RpcException(new Status(StatusCode.FailedPrecondition,
                        $"Structure {request.MergeId} cannot be merged into {request.KeepId}, which lies below it"));

                await _context.Procedures.MergeStructuresAsync(request.KeepId, request.MergeId, cancellationToken: ct);
                return new MergeResponse { KeptId = request.KeepId };
            }
            catch (RpcException) { throw; }
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(Merge), e, context); }
        }

        /// <summary>
        /// New structure starts at FirstLocationIdOfSplitStructure. The original keeps the rest.
        /// Throws NotFound, naming the location, when FirstLocationIdOfSplitStructure does not exist, without running
        /// dbo.SplitStructure, which would otherwise report it as "connected to the entire keep cell".
        /// </summary>
        public override async Task<SplitResponse> Split(SplitRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                await LocationService.ThrowIfLocationsMissingAsync(_context, new[] { request.FirstLocationIdOfSplitStructure }, ct);

                var splitId = new OutputParameter<long?>();
                await _context.Procedures.SplitStructureAsync(
                    request.FirstLocationIdOfSplitStructure, splitId, cancellationToken: ct);

                if (splitId.Value.HasValue == false)
                    throw new RpcException(new Status(StatusCode.FailedPrecondition,
                        $"Splitting structure {request.Id} at location {request.FirstLocationIdOfSplitStructure} produced no new structure"));

                return new SplitResponse { SplitStructureId = splitId.Value.Value };
            }
            catch (RpcException) { throw; }
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(Split), e, context); }
        }

        /// <summary>
        /// Cuts the location link. LocationIdOfKeepStructure stays on the original; the other side becomes the new structure.
        /// Throws InvalidArgument when both IDs name the same location, and NotFound, naming the missing IDs, when either
        /// location does not exist; neither runs dbo.SplitStructureAtLocationLink, which would otherwise report both as
        /// "must be linked".
        /// </summary>
        public override async Task<SplitAtLocationLinkResponse> SplitAtLocationLink(SplitAtLocationLinkRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                if (request.LocationIdOfKeepStructure == request.LocationIdOfSplitStructure)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "A location cannot be split from itself"));
                await LocationService.ThrowIfLocationsMissingAsync(_context, new[] { request.LocationIdOfKeepStructure, request.LocationIdOfSplitStructure }, ct);

                var splitId = new OutputParameter<long?>();
                await _context.Procedures.SplitStructureAtLocationLinkAsync(
                    request.LocationIdOfKeepStructure, request.LocationIdOfSplitStructure, splitId, cancellationToken: ct);

                if (splitId.Value.HasValue == false)
                    throw new RpcException(new Status(StatusCode.FailedPrecondition,
                        "Splitting at the given location link produced no new structure"));

                return new SplitAtLocationLinkResponse { SplitStructureId = splitId.Value.Value };
            }
            catch (RpcException) { throw; }
            catch (Exception e) { throw AnnotationRpc.Failure(_logger, nameof(SplitAtLocationLink), e, context); }
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

        /// <summary>
        /// DeepDeleteStructure deletes the structure and its child structures in SQL, which the change tracker
        /// does not see. A structure the batch edited before that delete would be saved as an UPDATE of a missing
        /// row, which EF reports as a concurrency failure and which would roll back the whole batch, so stop
        /// tracking every edited structure whose row is gone. The delete stands. A create whose parent is gone, or
        /// never existed, would fail the insert on FK_Structure_Structure the same way, so it stops being tracked
        /// and its row reports Success = false. An update that moves a live structure under such a parent would
        /// fail the same FK, so it stops being tracked and its row reports Success = false with no Updated. An
        /// update that keeps its stored parent is not looked up: that parent can only be gone if the batch deleted
        /// it, and then DeepDeleteStructure took the child too. A batch with no delete, no child create and no
        /// moved structure costs no query.
        /// </summary>
        private async Task ForgetEditsOfMissingStructuresAsync(UpdateStructuresRequest request,
            List<(StructureChangeResponse Response, EfStructure Entity)> pendingCreates,
            List<(StructureChangeResponse Response, EfStructure Entity)> pendingUpdates, CancellationToken ct)
        {
            var deletes = request.Objs.Any(c => c.ActionCase == StructureChangeRequest.ActionOneofCase.Delete);
            var edited = _context.ChangeTracker.Entries<EfStructure>()
                .Where(e => deletes && e.State == EntityState.Modified).ToList();
            var childCreates = pendingCreates.Where(c => c.Entity.ParentId.HasValue).ToList();
            var childUpdates = pendingUpdates.Where(u => u.Entity.ParentId.HasValue
                && _context.Entry(u.Entity).Property(s => s.ParentId).IsModified).ToList();
            if (edited.Count == 0 && childCreates.Count == 0 && childUpdates.Count == 0)
                return;

            var live = new HashSet<long>();
            var ids = edited.Select(e => e.Entity.Id)
                .Concat(childCreates.Concat(childUpdates).Select(c => c.Entity.ParentId.Value));
            foreach (var chunk in ids.DistinctChunk())
                live.UnionWith(await _context.Structures.AsNoTracking()
                    .Where(s => chunk.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct));

            foreach (var entry in edited.Where(e => !live.Contains(e.Entity.Id)))
                entry.State = EntityState.Detached;

            foreach (var (rowResponse, entity) in childCreates.Where(c => !live.Contains(c.Entity.ParentId.Value)))
            {
                _context.Entry(entity).State = EntityState.Detached;
                rowResponse.Success = false;
            }

            foreach (var (rowResponse, entity) in childUpdates.Where(u => (!deletes || live.Contains(u.Entity.Id)) && !live.Contains(u.Entity.ParentId.Value)))
            {
                _context.Entry(entity).State = EntityState.Detached;
                rowResponse.Success = false;
                rowResponse.Updated = null;
            }
        }

        /// <summary>
        /// True when <paramref name="id"/> is <paramref name="ancestorId"/> or lies below it in the structure tree as the
        /// batch so far leaves it, so making <paramref name="id"/> the parent of <paramref name="ancestorId"/> would close a
        /// cycle that FK_Structure_Structure does not prevent (Merge asks the same of KeepId and MergeId, since
        /// dbo.MergeStructures gives MergeId's children to KeepId). The walk follows ParentId upward through Find, which looks
        /// in the change tracker before the database, so moves made earlier in the batch count; it costs one lookup per
        /// ancestor not yet tracked and stops at a root, at a structure that is not stored, or at one it has already
        /// visited (a cycle already stored that does not pass through <paramref name="ancestorId"/>).
        /// </summary>
        private async Task<bool> IsSelfOrDescendantAsync(long id, long ancestorId, CancellationToken ct)
        {
            var visited = new HashSet<long>();
            long? current = id;
            while (current.HasValue && visited.Add(current.Value))
            {
                if (current.Value == ancestorId)
                    return true;
                current = (await _context.Structures.FindAsync(new object[] { current.Value }, ct))?.ParentId;
            }

            return false;
        }

        /// <summary>
        /// The structure types, among those the batch's creates and updates name, that exist. FK_StructureBase_StructureType
        /// would fail the save of a structure naming any other type and roll back the whole batch, so that row fails
        /// instead. An update's TypeId of 0 keeps the stored type (see <see cref="ApplyUpdate"/>) and is not looked up.
        /// A batch naming no type costs no query.
        /// </summary>
        private async Task<HashSet<long>> LiveTypeIdsAsync(UpdateStructuresRequest request, CancellationToken ct)
        {
            var named = request.Objs
                .Where(c => c.ActionCase == StructureChangeRequest.ActionOneofCase.Create).Select(c => c.Create.TypeId)
                .Concat(request.Objs
                    .Where(c => c.ActionCase == StructureChangeRequest.ActionOneofCase.Update && c.Update.TypeId != 0)
                    .Select(c => c.Update.TypeId)).ToArray();
            return await StoredTypeIdsAsync(named, ct);
        }

        /// <summary>
        /// The structure types among <paramref name="ids"/> that exist, one query per chunk; no IDs cost no query.
        /// FK_StructureBase_StructureType fails the save of a structure naming any other type.
        /// </summary>
        private async Task<HashSet<long>> StoredTypeIdsAsync(long[] ids, CancellationToken ct)
        {
            var live = new HashSet<long>();
            if (ids.Length == 0)
                return live;

            foreach (var chunk in ids.DistinctChunk())
                live.UnionWith(await _context.StructureTypes.AsNoTracking()
                    .Where(t => chunk.Contains(t.Id)).Select(t => t.Id).ToListAsync(ct));
            return live;
        }

        /// <summary>
        /// Throws NotFound, naming each of <paramref name="ids"/> that is not a stored structure, when there is one:
        /// FK_StructureLinkSource_StructureBaseID and FK_StructureLinkTarget_StructureBaseID would otherwise fail the
        /// save of a link naming it as a generic Unknown failure, FK_Structure_Structure the save of a new structure under
        /// it, and dbo.MergeStructures would fail on the Location foreign key or silently do nothing. No IDs cost no query.
        /// </summary>
        private async Task ThrowIfStructuresMissingAsync(IEnumerable<long> ids, CancellationToken ct)
        {
            var named = ids.Distinct().ToArray();
            if (named.Length == 0)
                return;

            var live = new HashSet<long>();
            foreach (var chunk in named.DistinctChunk())
                live.UnionWith(await _context.Structures.AsNoTracking()
                    .Where(s => chunk.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct));

            var missing = named.Where(id => !live.Contains(id)).ToArray();
            if (missing.Length > 0)
                throw new RpcException(new Status(StatusCode.NotFound, $"Structures not found: {string.Join(", ", missing)}"));
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

        private async Task<List<EfStructure>> StructuresInRegion(long z, Viking.gRPC.Geometry.V1.Rectangle region,
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

        /// <summary>The region's bounds; a missing region is an invalid argument.</summary>
        private static (double MinX, double MinY, double MaxX, double MaxY) BoundsOf(Viking.gRPC.Geometry.V1.Rectangle region)
        {
            if (region == null)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "A region is required"));

            return (region.XMin, region.YMin, region.XMax, region.YMax);
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
