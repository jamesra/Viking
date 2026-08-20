using Google.Protobuf.WellKnownTypes;
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
using EfLocation = Viking.DataModel.Annotation.Location;
using EfLocationLink = Viking.DataModel.Annotation.LocationLink;
using ProtoLocation = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location;
using ProtoLocationLink = Viking.AnnotationServiceTypes.gRPC.V1.Protos.LocationLink;
using ProtoGeometry = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry;

namespace gRPCAnnotationService
{
    /// <summary>
    /// Location and location-link RPCs. Links ride on Location.Links (AttachLocationLinksAsync);
    /// by-ID reads have no DeletedIds — a requested ID absent from Results is missing.
    /// Incremental *Changes* RPCs stamp QueryExecutedTime before the read and return DeletedIds
    /// only when ModifiedAfter is set.
    /// </summary>
    public class LocationService : Viking.AnnotationServiceTypes.gRPC.V1.Protos.AnnotateLocations.AnnotateLocationsBase
    {
        private readonly AnnotationContext _context;
        private readonly ILogger<LocationService> _logger;

        public LocationService(AnnotationContext context, ILogger<LocationService> logger)
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

        public override async Task<GetLocationByIDResponse> GetLocationByID(GetLocationByIDRequest request, ServerCallContext context)
        {
            var ct = context.CancellationToken;
            var obj = await _context.Locations.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == request.Id, ct);
            if (obj == null)
                throw new RpcException(new Status(StatusCode.NotFound, $"Location ID {request.Id} not found"));

            var result = obj.ToProtobufMessage();
            await AttachLocationLinksAsync(new[] { result }, ct);
            return new GetLocationByIDResponse { Result = result };
        }

        /// <summary>
        /// Found rows only. IDs not in Results were deleted or never existed — there is no DeletedIds field.
        /// </summary>
        public override async Task<GetLocationsByIDResponse> GetLocationsByID(GetLocationsByIDRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var response = new GetLocationsByIDResponse();
                foreach (var chunk in request.Ids.ToArray().Chunk())
                {
                    var rows = await _context.Locations.AsNoTracking()
                        .Where(l => chunk.Contains(l.Id)).ToListAsync(ct);
                    response.Results.AddRange(rows.Select(l => l.ToProtobufMessage()));
                }

                await AttachLocationLinksAsync(response.Results, ct);
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetLocationsByID), e); }
        }

        /// <summary>
        /// Newest row by LastModified. Does not attach links — callers that need Links must fetch by ID.
        /// </summary>
        public override async Task<GetLastModifiedLocationResponse> GetLastModifiedLocation(GetLastModifiedLocationRequest request, ServerCallContext context)
        {
            try
            {
                var obj = await _context.Locations.AsNoTracking()
                    .OrderByDescending(l => l.LastModified)
                    .FirstOrDefaultAsync(context.CancellationToken);

                if (obj == null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The volume contains no locations"));

                return new GetLastModifiedLocationResponse { Result = obj.ToProtobufMessage() };
            }
            catch (RpcException) { throw; }
            catch (Exception e) { throw Failure(nameof(GetLastModifiedLocation), e); }
        }

        public override async Task<GetLinkedLocationsResponse> GetLinkedLocations(GetLinkedLocationsRequest request, ServerCallContext context)
        {
            try
            {
                var response = new GetLinkedLocationsResponse();
                response.Results.AddRange(await LinkedIdsOf(request.Id, context.CancellationToken));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetLinkedLocations), e); }
        }

        public override async Task<GetLocationsForSectionResponse> GetLocationsForSection(GetLocationsForSectionRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                // Stamp the time before reading so a caller polling for changes cannot miss a
                // write that lands while this query runs.
                var queryStart = DateTime.UtcNow;

                var rows = await _context.Locations.AsNoTracking()
                    .Where(l => l.Z == request.Section).ToListAsync(ct);

                var response = new GetLocationsForSectionResponse
                {
                    QueryExecutedTime = Timestamp.FromDateTime(queryStart)
                };
                response.Results.AddRange(rows.Select(l => l.ToProtobufMessage()));
                await AttachLocationLinksAsync(response.Results, ct);
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetLocationsForSection), e); }
        }

        public override async Task<GetStructureLocationsResponse> GetStructureLocations(GetStructureLocationsRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var rows = await _context.Locations.AsNoTracking()
                    .Where(l => l.ParentId == request.StructureId).ToListAsync(ct);

                var response = new GetStructureLocationsResponse();
                response.Results.AddRange(rows.Select(l => l.ToProtobufMessage()));
                await AttachLocationLinksAsync(response.Results, ct);
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetStructureLocations), e); }
        }

        /// <summary>
        /// Section delta. DeletedIds is empty when ModifiedAfter is unset (full section load).
        /// </summary>
        public override async Task<GetLocationChangesResponse> GetLocationChanges(GetLocationChangesRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var queryStart = DateTime.UtcNow;
                var modifiedAfter = TimestampFilters.ModifiedAfterOrNull(request.ModifiedAfterThisUtcTime);

                var query = _context.Locations.AsNoTracking().Where(l => l.Z == request.Section);
                if (modifiedAfter.HasValue)
                    query = query.Where(l => l.LastModified > modifiedAfter.Value);

                var rows = await query.ToListAsync(ct);

                var response = new GetLocationChangesResponse
                {
                    QueryExecutedTime = Timestamp.FromDateTime(queryStart)
                };
                response.Results.AddRange(rows.Select(l => l.ToProtobufMessage()));
                await AttachLocationLinksAsync(response.Results, ct);
                response.DeletedIds.AddRange(await DeletedIdsSince(request.Section, modifiedAfter, ct));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetLocationChanges), e); }
        }

        /// <summary>
        /// Mosaic-bbox delta. DeletedIds is section-scoped (plus rows with null Z so old watermarks stay complete).
        /// </summary>
        public override async Task<GetLocationChangesInMosaicRegionResponse> GetLocationChangesInMosaicRegion(GetLocationChangesInMosaicRegionRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var queryStart = DateTime.UtcNow;
                var modifiedAfter = TimestampFilters.ModifiedAfterOrNull(request.ModifiedAfterThisUtcTime);

                var rows = await MosaicRegionQuery(request.Z, request.Region, request.MinRadius, modifiedAfter)
                    .ToListAsync(ct);

                var response = new GetLocationChangesInMosaicRegionResponse
                {
                    QueryExecutedTime = Timestamp.FromDateTime(queryStart)
                };
                response.Results.AddRange(rows.Select(l => l.ToProtobufMessage()));
                await AttachLocationLinksAsync(response.Results, ct);
                response.DeletedIds.AddRange(await DeletedIdsSince(request.Z, modifiedAfter, ct));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetLocationChangesInMosaicRegion), e); }
        }

        /// <summary>
        /// Locations in the mosaic bbox plus their parent structures. DeletedIds are location IDs only.
        /// </summary>
        public override async Task<GetAnnotationsInMosaicRegionResponse> GetAnnotationsInMosaicRegion(GetAnnotationsInMosaicRegionRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var queryStart = DateTime.UtcNow;
                var modifiedAfter = TimestampFilters.ModifiedAfterOrNull(request.ModifiedAfterThisUtcTime);

                var locations = await MosaicRegionQuery(request.Z, request.Region, request.MinRadius, modifiedAfter)
                    .ToListAsync(ct);

                var parentIds = locations.Select(l => l.ParentId).Distinct().ToArray();

                var set = new AnnotationSet();
                set.Locations.AddRange(locations.Select(l => l.ToProtobufMessage()));
                await AttachLocationLinksAsync(set.Locations, ct);
                foreach (var chunk in parentIds.Chunk())
                {
                    var structures = await _context.Structures.AsNoTracking()
                        .Where(s => chunk.Contains(s.Id)).ToListAsync(ct);
                    set.Structures.AddRange(structures.Select(s => s.ToProtobufMessage()));
                }

                var response = new GetAnnotationsInMosaicRegionResponse
                {
                    Result = set,
                    QueryExecutedTime = Timestamp.FromDateTime(queryStart)
                };
                response.DeletedIds.AddRange(await DeletedIdsSince(request.Z, modifiedAfter, ct));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetAnnotationsInMosaicRegion), e); }
        }

        /// <summary>
        /// Same payload as GetLocationChangesInMosaicRegion, chunked. QueryExecutedTime is on the first
        /// chunk only; DeletedIds and IsLast are on the last chunk only.
        /// </summary>
        public override async Task StreamLocationChangesInMosaicRegion(
            GetLocationChangesInMosaicRegionRequest request,
            IServerStreamWriter<LocationRegionChunk> responseStream,
            ServerCallContext context)
        {
            try
            {
                var queryStart = DateTime.UtcNow;
                var queryExecutedTime = Timestamp.FromDateTime(DateTime.SpecifyKind(queryStart, DateTimeKind.Utc));
                var modifiedAfter = TimestampFilters.ModifiedAfterOrNull(request.ModifiedAfterThisUtcTime);
                var ct = context.CancellationToken;

                var batch = new List<EfLocation>(RegionStreamBatchSize);
                var isFirst = true;

                await foreach (var location in MosaicRegionQuery(request.Z, request.Region, request.MinRadius, modifiedAfter)
                                   .AsAsyncEnumerable()
                                   .WithCancellation(ct))
                {
                    batch.Add(location);
                    if (batch.Count < RegionStreamBatchSize)
                        continue;

                    var chunk = new LocationRegionChunk { IsLast = false };
                    if (isFirst)
                    {
                        chunk.QueryExecutedTime = queryExecutedTime;
                        isFirst = false;
                    }

                    chunk.Locations.AddRange(batch.Select(l => l.ToProtobufMessage()));
                    await AttachLocationLinksAsync(chunk.Locations, ct);
                    await responseStream.WriteAsync(chunk);
                    batch.Clear();
                }

                var final = new LocationRegionChunk { IsLast = true };
                if (isFirst)
                    final.QueryExecutedTime = queryExecutedTime;
                final.Locations.AddRange(batch.Select(l => l.ToProtobufMessage()));
                await AttachLocationLinksAsync(final.Locations, ct);
                final.DeletedIds.AddRange(await DeletedIdsSince(request.Z, modifiedAfter, ct));
                await responseStream.WriteAsync(final);
            }
            catch (OperationCanceledException) { throw; }
            catch (RpcException) { throw; }
            catch (Exception e) { throw Failure(nameof(StreamLocationChangesInMosaicRegion), e); }
        }

        /// <summary>
        /// Same payload as GetAnnotationsInMosaicRegion, chunked. QueryExecutedTime on first chunk;
        /// DeletedIds and IsLast on the last.
        /// </summary>
        public override async Task StreamAnnotationsInMosaicRegion(
            GetAnnotationsInMosaicRegionRequest request,
            IServerStreamWriter<AnnotationRegionChunk> responseStream,
            ServerCallContext context)
        {
            try
            {
                var queryStart = DateTime.UtcNow;
                var queryExecutedTime = Timestamp.FromDateTime(DateTime.SpecifyKind(queryStart, DateTimeKind.Utc));
                var modifiedAfter = TimestampFilters.ModifiedAfterOrNull(request.ModifiedAfterThisUtcTime);
                var ct = context.CancellationToken;

                var batch = new List<EfLocation>(RegionStreamBatchSize);
                var isFirst = true;

                await foreach (var location in MosaicRegionQuery(request.Z, request.Region, request.MinRadius, modifiedAfter)
                                   .AsAsyncEnumerable()
                                   .WithCancellation(ct))
                {
                    batch.Add(location);
                    if (batch.Count < RegionStreamBatchSize)
                        continue;

                    var chunk = new AnnotationRegionChunk
                    {
                        IsLast = false,
                        Partial = await AnnotationSetForLocationBatch(batch, ct)
                    };
                    if (isFirst)
                    {
                        chunk.QueryExecutedTime = queryExecutedTime;
                        isFirst = false;
                    }

                    await responseStream.WriteAsync(chunk);
                    batch.Clear();
                }

                var final = new AnnotationRegionChunk
                {
                    IsLast = true,
                    Partial = batch.Count > 0
                        ? await AnnotationSetForLocationBatch(batch, ct)
                        : new AnnotationSet()
                };
                if (isFirst)
                    final.QueryExecutedTime = queryExecutedTime;
                final.DeletedIds.AddRange(await DeletedIdsSince(request.Z, modifiedAfter, ct));
                await responseStream.WriteAsync(final);
            }
            catch (OperationCanceledException) { throw; }
            catch (RpcException) { throw; }
            catch (Exception e) { throw Failure(nameof(StreamAnnotationsInMosaicRegion), e); }
        }

        public override async Task<GetLocationLinksForSectionResponse> GetLocationLinksForSection(GetLocationLinksForSectionRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var queryStart = DateTime.UtcNow;
                // Clients pass 0 (or other pre-SQL dates) to mean "no lower bound". SqlDateTime
                // cannot represent DateTime.FromBinary(0), so skip the filter in that case.
                var modifiedAfter = ModifiedAfterOrNull(request.ModifiedAfterThisTime);

                // Use the section TVF (same as WCF). Joining LocationLink → Location.Z goes
                // through the circle-shape interceptor and fails on production CurvePolygons.
                var response = new GetLocationLinksForSectionResponse
                {
                    QueryExecutedTime = Timestamp.FromDateTime(queryStart)
                };
                if (modifiedAfter.HasValue)
                {
                    var rows = await _context.SectionLocationLinksModifiedAfterDate(request.Section, modifiedAfter.Value)
                        .ToListAsync(ct);
                    response.Results.AddRange(rows.Select(l => new ProtoLocationLink { SourceId = l.A, TargetId = l.B }));
                }
                else
                {
                    var rows = await _context.SectionLocationLinks(request.Section).ToListAsync(ct);
                    response.Results.AddRange(rows.Select(l => new ProtoLocationLink { SourceId = l.A, TargetId = l.B }));
                }

                try
                {
                    response.Deleted.AddRange(await DeletedLocationLinksSince(request.Section, modifiedAfter, ct));
                }
                catch (Exception e)
                {
                    // WCF always returned an empty deleted list; the table is optional on older DBs.
                    _logger.LogWarning(e, "DeletedLocationLinks unavailable for section {Section}", request.Section);
                }

                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetLocationLinksForSection), e); }
        }

        public override async Task<GetLocationLinksForSectionInMosaicRegionResponse> GetLocationLinksForSectionInMosaicRegion(GetLocationLinksForSectionInMosaicRegionRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var bbox = request.Bbox;
                // Filter locations on scalar columns only. Joining LocationLink → Location
                // pulled MosaicShape/VolumeShape and broke under the circle interceptor.
                var locationIds = await _context.Locations.AsNoTracking()
                    .Where(l => l.Z == request.Section
                                && l.X >= bbox.Xmin && l.X <= bbox.Xmax
                                && l.Y >= bbox.Ymin && l.Y <= bbox.Ymax
                                && l.Radius >= request.MinRadius)
                    .Select(l => l.Id)
                    .ToListAsync(ct);

                var links = await LocationLinksTouchingAsync(locationIds, ct);

                var response = new GetLocationLinksForSectionInMosaicRegionResponse();
                response.Results.AddRange(links.Select(ToProtobufMessage));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetLocationLinksForSectionInMosaicRegion), e); }
        }

        #endregion

        #region Writes

        /// <summary>
        /// Inserts the row, then PersistCircleShapesIfNeededAsync. NTS cannot write SQL CurvePolygon;
        /// circles are stored as POINT until that SQL update.
        /// </summary>
        public override async Task<CreateLocationResponse> CreateLocation(CreateLocationRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var username = AnnotationRpc.CallerName(context);
                var row = request.Obj.ToLocation();
                row.Username = username;
                row.Created = DateTime.UtcNow;
                row.LastModified = row.Created;

                await using var tx = await _context.Database.BeginTransactionAsync(ct);

                var added = await _context.Locations.AddAsync(row, ct);
                await _context.SaveChangesAsync(ct);
                await PersistCircleShapesIfNeededAsync(request.Obj, added.Entity, ct);
                await PersistProtoLinksAsync(request.Obj.Links, added.Entity.Id, username, ct);
                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                var result = added.Entity.ToProtobufMessage();
                await AttachLocationLinksAsync(new[] { result }, ct);
                return new CreateLocationResponse { Result = result };
            }
            catch (Exception e) { throw Failure(nameof(CreateLocation), e); }
        }

        public override async Task<UpdateLocationsResponse> Update(UpdateLocationsRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var response = new UpdateLocationsResponse();
                var username = AnnotationRpc.CallerName(context);
                var pendingCircles = new List<(LocationChangeResponse Response, EfLocation Entity, ProtoLocation Proto, bool IsCreate)>();

                await using var tx = await _context.Database.BeginTransactionAsync(ct);

                foreach (var change in request.Locations)
                {
                    var rowResponse = new LocationChangeResponse();

                    switch (change.ActionCase)
                    {
                        case LocationChangeRequest.ActionOneofCase.Create:
                            var toInsert = change.Create.ToLocation();
                            toInsert.Username = username;
                            toInsert.Created = DateTime.UtcNow;
                            toInsert.LastModified = toInsert.Created;
                            var inserted = await _context.Locations.AddAsync(toInsert, ct);
                            pendingCircles.Add((rowResponse, inserted.Entity, change.Create, true));
                            rowResponse.Success = true;
                            break;

                        case LocationChangeRequest.ActionOneofCase.Update:
                            var existing = await _context.Locations.FirstOrDefaultAsync(l => l.Id == change.Update.Id, ct);
                            if (existing == null)
                            {
                                rowResponse.Success = false;
                                break;
                            }

                            ApplyUpdate(change.Update, existing, username);
                            pendingCircles.Add((rowResponse, existing, change.Update, false));
                            rowResponse.Success = true;
                            break;

                        case LocationChangeRequest.ActionOneofCase.Delete:
                            var toDelete = await _context.Locations.FirstOrDefaultAsync(l => l.Id == change.Delete, ct);
                            if (toDelete == null)
                            {
                                rowResponse.Success = false;
                                break;
                            }

                            // The link rows reference the location, so they have to go first.
                            var attached = await _context.LocationLinks
                                .Where(link => link.A == toDelete.Id || link.B == toDelete.Id).ToListAsync(ct);
                            foreach (var link in attached)
                                await LogDeletedLocationLinkAsync(link.A, link.B, ct);
                            _context.LocationLinks.RemoveRange(attached);

                            // Record the delete so incremental GetLocationChanges* calls can
                            // tell clients to drop the ID. (A FOR DELETE trigger cannot be used:
                            // EF Core DELETE statements include OUTPUT, which SQL Server rejects
                            // when the target table has triggers.)
                            var alreadyLogged = await _context.DeletedLocations
                                .AnyAsync(d => d.Id == toDelete.Id, ct);
                            if (!alreadyLogged)
                            {
                                await _context.DeletedLocations.AddAsync(new DeletedLocation
                                {
                                    Id = toDelete.Id,
                                    Z = toDelete.Z,
                                    DeletedOn = DateTime.UtcNow
                                }, ct);
                            }

                            _context.Locations.Remove(toDelete);
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
                foreach (var (rowResponse, entity, proto, isCreate) in pendingCircles)
                {
                    await PersistCircleShapesIfNeededAsync(proto, entity, ct);
                    if (isCreate)
                        await PersistProtoLinksAsync(proto.Links, entity.Id, username, ct);
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                foreach (var (rowResponse, entity, proto, isCreate) in pendingCircles)
                {
                    var mapped = entity.ToProtobufMessage();
                    if (isCreate)
                        rowResponse.Created = mapped;
                    else
                        rowResponse.Updated = mapped;
                }

                var createdOrUpdated = pendingCircles
                    .Select(p => p.IsCreate ? p.Response.Created : p.Response.Updated)
                    .Where(p => p != null)
                    .ToList();
                await AttachLocationLinksAsync(createdOrUpdated, ct);

                return response;
            }
            catch (Exception e) { throw Failure(nameof(Update), e); }
        }

        public override async Task<CreateLocationLinkResponse> CreateLocationLink(CreateLocationLinkRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                await AddLinkIfMissing(request.SourceId, request.TargetId, AnnotationRpc.CallerName(context), ct);
                await _context.SaveChangesAsync(ct);
                return new CreateLocationLinkResponse();
            }
            catch (Exception e) { throw Failure(nameof(CreateLocationLink), e); }
        }

        public override async Task<DeleteLocationLinkResponse> DeleteLocationLink(DeleteLocationLinkRequest request, ServerCallContext context)
        {
            try
            {
                var ct = context.CancellationToken;
                var (a, b) = Ordered(request.SourceId, request.TargetId);
                var link = await _context.LocationLinks.FirstOrDefaultAsync(l => l.A == a && l.B == b, ct);
                if (link == null)
                    throw new RpcException(new Status(StatusCode.NotFound,
                        $"No link between locations {request.SourceId} and {request.TargetId}"));

                await LogDeletedLocationLinkAsync(a, b, ct);
                _context.LocationLinks.Remove(link);
                await _context.SaveChangesAsync(ct);
                return new DeleteLocationLinkResponse();
            }
            catch (RpcException) { throw; }
            catch (Exception e) { throw Failure(nameof(DeleteLocationLink), e); }
        }

        #endregion

        public override Task<GetLocationChangeLogResponse> GetLocationChangeLog(GetLocationChangeLogRequest request, ServerCallContext context)
        {
            // The change log lives in audit tables the EF Core model does not map. Reporting
            // this plainly beats returning an empty list that looks like "no changes".
            throw new RpcException(new Status(StatusCode.Unimplemented,
                $"{nameof(GetLocationChangeLog)} requires the location audit tables, which are not mapped by the EF Core model."));
        }

        #region Helpers

        private const int RegionStreamBatchSize = 128;

        private async Task<AnnotationSet> AnnotationSetForLocationBatch(IReadOnlyList<EfLocation> locations, CancellationToken ct)
        {
            var set = new AnnotationSet();
            set.Locations.AddRange(locations.Select(l => l.ToProtobufMessage()));
            await AttachLocationLinksAsync(set.Locations, ct);

            var parentIds = locations.Select(l => l.ParentId).Distinct().ToArray();
            foreach (var parentChunk in parentIds.Chunk())
            {
                var structures = await _context.Structures.AsNoTracking()
                    .Where(s => parentChunk.Contains(s.Id))
                    .ToListAsync(ct);
                set.Structures.AddRange(structures.Select(s => s.ToProtobufMessage()));
            }

            return set;
        }

        /// <summary>
        /// Decode a DateTime.ToBinary tick payload used by older WCF clients. Zero / values
        /// outside SQL datetime range mean "no lower bound".
        /// </summary>
        private static DateTime? ModifiedAfterOrNull(long binaryTime)
        {
            if (binaryTime == 0)
                return null;

            try
            {
                var value = DateTime.FromBinary(binaryTime);
                var sqlMin = (DateTime)System.Data.SqlTypes.SqlDateTime.MinValue;
                return value < sqlMin ? null : value;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>Location links are stored with the lower ID first, so callers may pass either order.</summary>
        private static (long A, long B) Ordered(long x, long y) => x < y ? (x, y) : (y, x);

        private static ProtoLocationLink ToProtobufMessage(EfLocationLink src) =>
            new ProtoLocationLink { SourceId = src.A, TargetId = src.B };

        /// <summary>
        /// FindAsync / AsNoTracking by-ID queries do not load LocationLink navigations.
        /// Batch-fill Location.Links (peer IDs) so clients can hydrate LocationLinkStore.
        /// Chunks ID lists so SQL never sees more than ~2000 parameters.
        /// </summary>
        private async Task AttachLocationLinksAsync(IList<ProtoLocation> locations, CancellationToken ct)
        {
            if (locations.Count == 0)
                return;

            var ids = locations.Select(l => l.Id).ToArray();
            var links = await LocationLinksTouchingAsync(ids, ct);
            if (links.Count == 0)
                return;

            var byId = locations.ToDictionary(l => l.Id);
            foreach (var link in links)
            {
                if (byId.TryGetValue(link.A, out var a) && !a.Links.Contains(link.B))
                    a.Links.Add(link.B);
                if (link.B != link.A && byId.TryGetValue(link.B, out var b) && !b.Links.Contains(link.A))
                    b.Links.Add(link.A);
            }
        }

        private async Task<List<EfLocationLink>> LocationLinksTouchingAsync(IReadOnlyList<long> ids, CancellationToken ct)
        {
            var links = new List<EfLocationLink>();
            if (ids.Count == 0)
                return links;

            var seen = new HashSet<(long A, long B)>();
            foreach (var chunk in ids.ToArray().Chunk())
            {
                var rows = await _context.LocationLinks.AsNoTracking()
                    .Where(l => chunk.Contains(l.A) || chunk.Contains(l.B))
                    .ToListAsync(ct);
                foreach (var link in rows)
                {
                    if (seen.Add((link.A, link.B)))
                        links.Add(link);
                }
            }

            return links;
        }

        private async Task PersistProtoLinksAsync(IEnumerable<long> peerIds, long locationId, string username, CancellationToken ct)
        {
            foreach (var peerId in peerIds)
            {
                if (peerId == 0 || peerId == locationId)
                    continue;
                await AddLinkIfMissing(locationId, peerId, username, ct);
            }
        }

        private async Task AddLinkIfMissing(long source, long target, string username, CancellationToken ct)
        {
            if (source == target)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "A location cannot link to itself"));

            var (a, b) = Ordered(source, target);
            var exists = await _context.LocationLinks.AnyAsync(l => l.A == a && l.B == b, ct);
            if (exists)
                return;

            await _context.LocationLinks.AddAsync(new EfLocationLink
            {
                A = a,
                B = b,
                Username = username,
                Created = DateTime.UtcNow
            }, ct);
        }

        private async Task<List<long>> LinkedIdsOf(long id, CancellationToken ct)
        {
            var links = await _context.LocationLinks.AsNoTracking()
                .Where(l => l.A == id || l.B == id).ToListAsync(ct);
            return links.Select(l => l.A == id ? l.B : l.A).ToList();
        }

        private IQueryable<EfLocation> MosaicRegionQuery(long z, ProtoGeometry region, double minRadius, DateTime? modifiedAfter)
        {
            var bounds = BoundsOf(region);

            // Filter on the persisted centroid and radius columns rather than the geometry so
            // the predicate stays translatable. Circle CurvePolygons are omitted on read by
            // SqlServerCircleShapeCommandInterceptor.
            var query = _context.Locations.AsNoTracking()
                .Where(l => l.Z == z
                            && l.Radius >= minRadius
                            && l.X >= bounds.MinX && l.X <= bounds.MaxX
                            && l.Y >= bounds.MinY && l.Y <= bounds.MaxY);

            if (modifiedAfter.HasValue)
                query = query.Where(l => l.LastModified > modifiedAfter.Value);

            return query;
        }

        /// <summary>
        /// The region is sent as a general geometry (typically a polygon), so the caller's
        /// bounding box is recovered here rather than requiring a dedicated bbox message.
        /// </summary>
        private static (double MinX, double MinY, double MaxX, double MaxY) BoundsOf(ProtoGeometry region)
        {
            var geometry = region?.ToNetTopologyGeometry();
            if (geometry == null)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "A region is required"));

            var envelope = geometry.EnvelopeInternal;
            return (envelope.MinX, envelope.MinY, envelope.MaxX, envelope.MaxY);
        }

        /// <summary>
        /// Deleted location IDs for one section. Rows with null Z were written before section
        /// was stored and are returned for every section so older watermarks stay complete.
        /// </summary>
        private async Task<List<long>> DeletedIdsSince(long section, DateTime? modifiedAfter, CancellationToken ct)
        {
            if (modifiedAfter.HasValue == false)
                return new List<long>();

            return await _context.DeletedLocations.AsNoTracking()
                .Where(d => d.DeletedOn > modifiedAfter.Value
                            && (d.Z == null || d.Z == section))
                .Select(d => d.Id)
                .ToListAsync(ct);
        }

        private async Task LogDeletedLocationLinkAsync(long a, long b, CancellationToken ct)
        {
            var (orderedA, orderedB) = Ordered(a, b);
            var alreadyLogged = await _context.DeletedLocationLinks
                .AnyAsync(d => d.A == orderedA && d.B == orderedB, ct);
            if (alreadyLogged)
                return;

            var zs = await _context.Locations.AsNoTracking()
                .Where(l => l.Id == orderedA || l.Id == orderedB)
                .Select(l => new { l.Id, l.Z })
                .ToListAsync(ct);

            await _context.DeletedLocationLinks.AddAsync(new DeletedLocationLink
            {
                A = orderedA,
                B = orderedB,
                Az = zs.FirstOrDefault(z => z.Id == orderedA)?.Z,
                Bz = zs.FirstOrDefault(z => z.Id == orderedB)?.Z,
                DeletedOn = DateTime.UtcNow
            }, ct);
        }

        /// <summary>
        /// Deleted links that touched <paramref name="section"/> (either endpoint's Z at delete
        /// time). Rows with both AZ and BZ null predate those columns and are returned for every
        /// section so older watermarks stay complete.
        /// </summary>
        private async Task<List<ProtoLocationLink>> DeletedLocationLinksSince(long section, DateTime? modifiedAfter, CancellationToken ct)
        {
            if (modifiedAfter.HasValue == false)
                return new List<ProtoLocationLink>();

            var rows = await _context.DeletedLocationLinks.AsNoTracking()
                .Where(d => d.DeletedOn > modifiedAfter.Value
                            && ((d.Az == null && d.Bz == null) || d.Az == section || d.Bz == section))
                .ToListAsync(ct);

            return rows.Select(d => new ProtoLocationLink
            {
                SourceId = d.A,
                TargetId = d.B
            }).ToList();
        }

        private async Task PersistCircleShapesIfNeededAsync(ProtoLocation proto, EfLocation entity, CancellationToken ct)
        {
            if (proto.TypeCode != AnnotationType.Circle)
                return;

            var (mosaicWkt, volumeWkt) = proto.CircleShapeWkt();
            await _context.PersistCircleShapesAsync(entity, mosaicWkt, volumeWkt, ct);
        }

        private static void ApplyUpdate(ProtoLocation src, EfLocation dst, string username)
        {
            var updated = src.ToLocation();

            // Proto3 defaults missing ParentId to 0; keep the existing parent rather than
            // writing an invalid FK.
            if (updated.ParentId != 0)
                dst.ParentId = updated.ParentId;
            dst.Z = updated.Z;
            dst.Closed = updated.Closed;
            dst.Tags = updated.Tags;
            dst.Terminal = updated.Terminal;
            dst.OffEdge = updated.OffEdge;
            dst.TypeCode = updated.TypeCode;
            dst.MosaicShape = updated.MosaicShape;
            dst.VolumeShape = updated.VolumeShape;
            dst.Width = updated.Width;
            dst.Username = username;
            dst.LastModified = DateTime.UtcNow;
        }

        #endregion
    }
}
