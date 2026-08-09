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
            var obj = await _context.Locations.FindAsync(request.Id);
            if (obj == null)
                throw new RpcException(new Status(StatusCode.NotFound, $"Location ID {request.Id} not found"));

            return new GetLocationByIDResponse { Result = obj.ToProtobufMessage() };
        }

        public override async Task<GetLocationsByIDResponse> GetLocationsByID(GetLocationsByIDRequest request, ServerCallContext context)
        {
            try
            {
                var response = new GetLocationsByIDResponse();
                foreach (var chunk in request.Ids.ToArray().Chunk())
                {
                    var rows = await _context.Locations.AsNoTracking()
                        .Where(l => chunk.Contains(l.Id)).ToListAsync();
                    response.Results.AddRange(rows.Select(l => l.ToProtobufMessage()));
                }

                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetLocationsByID), e); }
        }

        public override async Task<GetLastModifiedLocationResponse> GetLastModifiedLocation(GetLastModifiedLocationRequest request, ServerCallContext context)
        {
            try
            {
                var obj = await _context.Locations.AsNoTracking()
                    .OrderByDescending(l => l.LastModified).FirstOrDefaultAsync();

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
                response.Results.AddRange(await LinkedIdsOf(request.Id));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetLinkedLocations), e); }
        }

        public override async Task<GetLocationsForSectionResponse> GetLocationsForSection(GetLocationsForSectionRequest request, ServerCallContext context)
        {
            try
            {
                // Stamp the time before reading so a caller polling for changes cannot miss a
                // write that lands while this query runs.
                var queryStart = DateTime.UtcNow;

                var rows = await _context.Locations.AsNoTracking()
                    .Where(l => l.Z == request.Section).ToListAsync();

                var response = new GetLocationsForSectionResponse
                {
                    QueryExecutedTime = Timestamp.FromDateTime(queryStart)
                };
                response.Results.AddRange(rows.Select(l => l.ToProtobufMessage()));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetLocationsForSection), e); }
        }

        public override async Task<GetStructureLocationsResponse> GetStructureLocations(GetStructureLocationsRequest request, ServerCallContext context)
        {
            try
            {
                var rows = await _context.Locations.AsNoTracking()
                    .Where(l => l.ParentId == request.StructureId).ToListAsync();

                var response = new GetStructureLocationsResponse();
                response.Results.AddRange(rows.Select(l => l.ToProtobufMessage()));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetStructureLocations), e); }
        }

        public override async Task<GetLocationChangesResponse> GetLocationChanges(GetLocationChangesRequest request, ServerCallContext context)
        {
            try
            {
                var queryStart = DateTime.UtcNow;
                var modifiedAfter = request.ModifiedAfterThisUtcTime?.ToDateTime();

                var query = _context.Locations.AsNoTracking().Where(l => l.Z == request.Section);
                if (modifiedAfter.HasValue)
                    query = query.Where(l => l.LastModified > modifiedAfter.Value);

                var rows = await query.ToListAsync();

                var response = new GetLocationChangesResponse
                {
                    QueryExecutedTime = Timestamp.FromDateTime(queryStart)
                };
                response.Results.AddRange(rows.Select(l => l.ToProtobufMessage()));
                response.DeletedIds.AddRange(await DeletedIdsSince(request.Section, modifiedAfter));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetLocationChanges), e); }
        }

        public override async Task<GetLocationChangesInMosaicRegionResponse> GetLocationChangesInMosaicRegion(GetLocationChangesInMosaicRegionRequest request, ServerCallContext context)
        {
            try
            {
                var queryStart = DateTime.UtcNow;
                var modifiedAfter = request.ModifiedAfterThisUtcTime?.ToDateTime();

                var rows = await MosaicRegionQuery(request.Z, request.Region, request.MinRadius, modifiedAfter)
                    .ToListAsync();

                var response = new GetLocationChangesInMosaicRegionResponse
                {
                    QueryExecutedTime = Timestamp.FromDateTime(queryStart)
                };
                response.Results.AddRange(rows.Select(l => l.ToProtobufMessage()));
                response.DeletedIds.AddRange(await DeletedIdsSince(request.Z, modifiedAfter));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetLocationChangesInMosaicRegion), e); }
        }

        public override async Task<GetAnnotationsInMosaicRegionResponse> GetAnnotationsInMosaicRegion(GetAnnotationsInMosaicRegionRequest request, ServerCallContext context)
        {
            try
            {
                var queryStart = DateTime.UtcNow;
                var modifiedAfter = request.ModifiedAfterThisUtcTime?.ToDateTime();

                var locations = await MosaicRegionQuery(request.Z, request.Region, request.MinRadius, modifiedAfter)
                    .ToListAsync();

                var parentIds = locations.Select(l => l.ParentId).Distinct().ToArray();

                var set = new AnnotationSet();
                set.Locations.AddRange(locations.Select(l => l.ToProtobufMessage()));
                foreach (var chunk in parentIds.Chunk())
                {
                    var structures = await _context.Structures.AsNoTracking()
                        .Where(s => chunk.Contains(s.Id)).ToListAsync();
                    set.Structures.AddRange(structures.Select(s => s.ToProtobufMessage()));
                }

                var response = new GetAnnotationsInMosaicRegionResponse
                {
                    Result = set,
                    QueryExecutedTime = Timestamp.FromDateTime(queryStart)
                };
                response.DeletedIds.AddRange(await DeletedIdsSince(request.Z, modifiedAfter));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetAnnotationsInMosaicRegion), e); }
        }

        public override async Task StreamLocationChangesInMosaicRegion(
            GetLocationChangesInMosaicRegionRequest request,
            IServerStreamWriter<LocationRegionChunk> responseStream,
            ServerCallContext context)
        {
            try
            {
                var queryStart = DateTime.UtcNow;
                var queryExecutedTime = Timestamp.FromDateTime(DateTime.SpecifyKind(queryStart, DateTimeKind.Utc));
                var modifiedAfter = request.ModifiedAfterThisUtcTime?.ToDateTime();
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
                    await responseStream.WriteAsync(chunk);
                    batch.Clear();
                }

                var final = new LocationRegionChunk { IsLast = true };
                if (isFirst)
                    final.QueryExecutedTime = queryExecutedTime;
                final.Locations.AddRange(batch.Select(l => l.ToProtobufMessage()));
                final.DeletedIds.AddRange(await DeletedIdsSince(request.Z, modifiedAfter));
                await responseStream.WriteAsync(final);
            }
            catch (OperationCanceledException) { throw; }
            catch (RpcException) { throw; }
            catch (Exception e) { throw Failure(nameof(StreamLocationChangesInMosaicRegion), e); }
        }

        public override async Task StreamAnnotationsInMosaicRegion(
            GetAnnotationsInMosaicRegionRequest request,
            IServerStreamWriter<AnnotationRegionChunk> responseStream,
            ServerCallContext context)
        {
            try
            {
                var queryStart = DateTime.UtcNow;
                var queryExecutedTime = Timestamp.FromDateTime(DateTime.SpecifyKind(queryStart, DateTimeKind.Utc));
                var modifiedAfter = request.ModifiedAfterThisUtcTime?.ToDateTime();
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
                final.DeletedIds.AddRange(await DeletedIdsSince(request.Z, modifiedAfter));
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
                var queryStart = DateTime.UtcNow;
                // Clients pass 0 (or other pre-SQL dates) to mean "no lower bound". SqlDateTime
                // cannot represent DateTime.FromBinary(0), so skip the filter in that case.
                var modifiedAfter = ModifiedAfterOrNull(request.ModifiedAfterThisTime);
                var query = LinksTouchingSection(request.Section);
                if (modifiedAfter.HasValue)
                    query = query.Where(link => link.Created > modifiedAfter.Value);

                var links = await query.ToListAsync();

                var response = new GetLocationLinksForSectionResponse
                {
                    QueryExecutedTime = Timestamp.FromDateTime(queryStart)
                };
                response.Results.AddRange(links.Select(ToProtobufMessage));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetLocationLinksForSection), e); }
        }

        public override async Task<GetLocationLinksForSectionInMosaicRegionResponse> GetLocationLinksForSectionInMosaicRegion(GetLocationLinksForSectionInMosaicRegionRequest request, ServerCallContext context)
        {
            try
            {
                var bbox = request.Bbox;
                var links = await _context.LocationLinks.AsNoTracking()
                    .Where(link =>
                        (link.ANavigation.Z == request.Section &&
                         link.ANavigation.X >= bbox.Xmin && link.ANavigation.X <= bbox.Xmax &&
                         link.ANavigation.Y >= bbox.Ymin && link.ANavigation.Y <= bbox.Ymax &&
                         link.ANavigation.Radius >= request.MinRadius)
                        ||
                        (link.BNavigation.Z == request.Section &&
                         link.BNavigation.X >= bbox.Xmin && link.BNavigation.X <= bbox.Xmax &&
                         link.BNavigation.Y >= bbox.Ymin && link.BNavigation.Y <= bbox.Ymax &&
                         link.BNavigation.Radius >= request.MinRadius))
                    .ToListAsync();

                var response = new GetLocationLinksForSectionInMosaicRegionResponse();
                response.Results.AddRange(links.Select(ToProtobufMessage));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetLocationLinksForSectionInMosaicRegion), e); }
        }

        #endregion

        #region Writes

        public override async Task<CreateLocationResponse> CreateLocation(CreateLocationRequest request, ServerCallContext context)
        {
            try
            {
                var row = request.Obj.ToLocation();
                row.Username = CallerName(context);
                row.Created = DateTime.UtcNow;
                row.LastModified = row.Created;

                var added = await _context.Locations.AddAsync(row);
                await _context.SaveChangesAsync();

                return new CreateLocationResponse { Result = added.Entity.ToProtobufMessage() };
            }
            catch (Exception e) { throw Failure(nameof(CreateLocation), e); }
        }

        public override async Task<UpdateLocationsResponse> Update(UpdateLocationsRequest request, ServerCallContext context)
        {
            try
            {
                var response = new UpdateLocationsResponse();
                var username = CallerName(context);

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
                            var inserted = await _context.Locations.AddAsync(toInsert);
                            rowResponse.Success = true;
                            rowResponse.Created = inserted.Entity.ToProtobufMessage();
                            break;

                        case LocationChangeRequest.ActionOneofCase.Update:
                            var existing = await _context.Locations.FirstOrDefaultAsync(l => l.Id == change.Update.Id);
                            if (existing == null)
                            {
                                rowResponse.Success = false;
                                break;
                            }

                            ApplyUpdate(change.Update, existing, username);
                            rowResponse.Success = true;
                            rowResponse.Updated = _context.Locations.Update(existing).Entity.ToProtobufMessage();
                            break;

                        case LocationChangeRequest.ActionOneofCase.Delete:
                            var toDelete = await _context.Locations.FirstOrDefaultAsync(l => l.Id == change.Delete);
                            if (toDelete == null)
                            {
                                rowResponse.Success = false;
                                break;
                            }

                            // The link rows reference the location, so they have to go first.
                            var attached = await _context.LocationLinks
                                .Where(link => link.A == toDelete.Id || link.B == toDelete.Id).ToListAsync();
                            _context.LocationLinks.RemoveRange(attached);

                            // Record the delete so incremental GetLocationChanges* calls can
                            // tell clients to drop the ID. (A FOR DELETE trigger cannot be used:
                            // EF Core DELETE statements include OUTPUT, which SQL Server rejects
                            // when the target table has triggers.)
                            var alreadyLogged = await _context.DeletedLocations
                                .AnyAsync(d => d.Id == toDelete.Id);
                            if (!alreadyLogged)
                            {
                                await _context.DeletedLocations.AddAsync(new DeletedLocation
                                {
                                    Id = toDelete.Id,
                                    DeletedOn = DateTime.UtcNow
                                });
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

                await _context.SaveChangesAsync();
                return response;
            }
            catch (Exception e) { throw Failure(nameof(Update), e); }
        }

        public override async Task<CreateLocationLinkResponse> CreateLocationLink(CreateLocationLinkRequest request, ServerCallContext context)
        {
            try
            {
                await AddLinkIfMissing(request.SourceId, request.TargetId, CallerName(context));
                await _context.SaveChangesAsync();
                return new CreateLocationLinkResponse();
            }
            catch (Exception e) { throw Failure(nameof(CreateLocationLink), e); }
        }

        public override async Task<DeleteLocationLinkResponse> DeleteLocationLink(DeleteLocationLinkRequest request, ServerCallContext context)
        {
            try
            {
                var (a, b) = Ordered(request.SourceId, request.TargetId);
                var link = await _context.LocationLinks.FirstOrDefaultAsync(l => l.A == a && l.B == b);
                if (link == null)
                    throw new RpcException(new Status(StatusCode.NotFound,
                        $"No link between locations {request.SourceId} and {request.TargetId}"));

                _context.LocationLinks.Remove(link);
                await _context.SaveChangesAsync();
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

        private static string CallerName(ServerCallContext context) =>
            context.GetHttpContext()?.User?.Identity?.Name ?? "unknown";

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

        private async Task AddLinkIfMissing(long source, long target, string username)
        {
            if (source == target)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "A location cannot link to itself"));

            var (a, b) = Ordered(source, target);
            var exists = await _context.LocationLinks.AnyAsync(l => l.A == a && l.B == b);
            if (exists)
                return;

            await _context.LocationLinks.AddAsync(new EfLocationLink
            {
                A = a,
                B = b,
                Username = username,
                Created = DateTime.UtcNow
            });
        }

        private async Task<List<long>> LinkedIdsOf(long id)
        {
            var links = await _context.LocationLinks.AsNoTracking()
                .Where(l => l.A == id || l.B == id).ToListAsync();
            return links.Select(l => l.A == id ? l.B : l.A).ToList();
        }

        private IQueryable<EfLocationLink> LinksTouchingSection(long section) =>
            _context.LocationLinks.AsNoTracking()
                .Where(l => l.ANavigation.Z == section || l.BNavigation.Z == section);

        private IQueryable<EfLocation> MosaicRegionQuery(long z, ProtoGeometry region, double minRadius, DateTime? modifiedAfter)
        {
            var bounds = BoundsOf(region);

            // Filter on the persisted centroid and radius columns rather than the geometry, so
            // the query stays translatable and never has to parse a CurvePolygon.
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

        private async Task<List<long>> DeletedIdsSince(long section, DateTime? modifiedAfter)
        {
            if (modifiedAfter.HasValue == false)
                return new List<long>();

            return await _context.DeletedLocations.AsNoTracking()
                .Where(d => d.DeletedOn > modifiedAfter.Value)
                .Select(d => d.Id)
                .ToListAsync();
        }

        private static void ApplyUpdate(ProtoLocation src, EfLocation dst, string username)
        {
            var updated = src.ToLocation();

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
