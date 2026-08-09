using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using gRPCAnnotationService.Protos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
            var obj = await _context.Structures.FindAsync(request.Id);
            if (obj == null)
                throw new RpcException(new Status(StatusCode.NotFound, $"Structure ID {request.Id} not found"));

            return new GetStructureByIDResponse { Result = obj.ToProtobufMessage() };
        }

        public override async Task<GetStructuresByIDResponse> GetStructuresByID(GetStructuresByIDRequest request, ServerCallContext context)
        {
            try
            {
                var response = new GetStructuresByIDResponse();
                foreach (var chunk in request.Ids.ToArray().Chunk())
                {
                    var rows = await _context.Structures.AsNoTracking()
                        .Where(s => chunk.Contains(s.Id)).ToListAsync();
                    response.Results.AddRange(rows.Select(s => s.ToProtobufMessage()));
                }

                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetStructuresByID), e); }
        }

        public override async Task<GetStructuresResponse> GetStructures(GetStructuresRequest request, ServerCallContext context)
        {
            try
            {
                var rows = await _context.Structures.AsNoTracking().ToListAsync();
                var response = new GetStructuresResponse();
                response.Results.AddRange(rows.Select(s => s.ToProtobufMessage()));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetStructures), e); }
        }

        public override async Task<GetStructuresForSectionResponse> GetStructuresForSection(GetStructuresForSectionRequest request, ServerCallContext context)
        {
            try
            {
                var queryStart = DateTime.UtcNow;
                var modifiedAfter = TimestampFilters.ModifiedAfterOrNull(request.ModifiedAfterThisUtcTime);

                var rows = await StructuresOnSection(request.Z, modifiedAfter).ToListAsync();

                var response = new GetStructuresForSectionResponse
                {
                    QueryExecutedTime = Timestamp.FromDateTime(queryStart)
                };
                response.Results.AddRange(rows.Select(s => s.ToProtobufMessage()));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetStructuresForSection), e); }
        }

        public override async Task<GetStructuresInMosaicRegionResponse> GetStructuresInMosaicRegion(GetStructuresInMosaicRegionRequest request, ServerCallContext context)
        {
            try
            {
                var queryStart = DateTime.UtcNow;
                var rows = await StructuresInRegion(request.Z, request.Region, request.MinRadius,
                    TimestampFilters.ModifiedAfterOrNull(request.ModifiedAfterThisUtcTime), useVolumeCoordinates: false);

                var response = new GetStructuresInMosaicRegionResponse
                {
                    QueryExecutedTime = Timestamp.FromDateTime(queryStart)
                };
                response.Results.AddRange(rows.Select(s => s.ToProtobufMessage()));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetStructuresInMosaicRegion), e); }
        }

        public override async Task<GetStructuresInVolumeRegionResponse> GetStructuresInVolumeRegion(GetStructuresInVolumeRegionRequest request, ServerCallContext context)
        {
            try
            {
                var queryStart = DateTime.UtcNow;
                var rows = await StructuresInRegion(request.Z, request.Region, request.MinRadius,
                    TimestampFilters.ModifiedAfterOrNull(request.ModifiedAfterThisUtcTime), useVolumeCoordinates: true);

                var response = new GetStructuresInVolumeRegionResponse
                {
                    QueryExecutedTime = Timestamp.FromDateTime(queryStart)
                };
                response.Results.AddRange(rows.Select(s => s.ToProtobufMessage()));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetStructuresInVolumeRegion), e); }
        }

        public override async Task<GetStructuresOfTypeResponse> GetStructuresOfType(GetStructuresOfTypeRequest request, ServerCallContext context)
        {
            try
            {
                var rows = await _context.Structures.AsNoTracking()
                    .Where(s => s.TypeId == request.Id).ToListAsync();

                var response = new GetStructuresOfTypeResponse();
                response.Results.AddRange(rows.Select(s => s.ToProtobufMessage()));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetStructuresOfType), e); }
        }

        public override async Task<GetChildStructuresResponse> GetChildStructures(GetChildStructuresRequest request, ServerCallContext context)
        {
            try
            {
                var rows = await _context.Structures.AsNoTracking()
                    .Where(s => s.ParentId == request.StructureId).ToListAsync();

                var response = new GetChildStructuresResponse();
                response.Results.AddRange(rows.Select(s => s.ToProtobufMessage()));
                return response;
            }
            catch (Exception e) { throw Failure(nameof(GetChildStructures), e); }
        }

        public override async Task<GetLinkedStructuresResponse> GetLinkedStructures(GetLinkedStructuresRequest request, ServerCallContext context)
        {
            try
            {
                var links = await _context.StructureLinks.AsNoTracking()
                    .Where(l => l.SourceId == request.Id || l.TargetId == request.Id).ToListAsync();

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
                    .CountAsync(l => l.ParentId == request.Id);
                return new NumberOfLocationsResponse { Result = count };
            }
            catch (Exception e) { throw Failure(nameof(NumberOfLocations), e); }
        }

        public override async Task<GetNetworkedStructuresResponse> GetNetworkedStructures(GetNetworkedStructuresRequest request, ServerCallContext context)
        {
            try
            {
                var rows = await _context.Procedures.SelectNetworkStructureIDsAsync(
                    IdTable(request.Ids), request.NumHops);

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
                    IdTable(request.Ids), request.NumHops);

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
                    IdTable(request.Ids), request.NumHops);

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
                var rows = await _context.Procedures.SelectUnfinishedStructureBranchesAsync(request.Id);
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
                var rows = await _context.Procedures.SelectUnfinishedStructureBranchesWithPositionAsync(request.Id);
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

        public override async Task<CreateStructureResponse> CreateStructure(CreateStructureRequest request, ServerCallContext context)
        {
            try
            {
                var username = CallerName(context);

                var structure = request.NewStructure.ToStructure();
                structure.Username = username;
                structure.Created = DateTime.UtcNow;
                structure.LastModified = structure.Created;
                var addedStructure = await _context.Structures.AddAsync(structure);
                await _context.SaveChangesAsync();

                EfLocation addedLocation = null;
                if (request.NewAnnotation != null)
                {
                    addedLocation = request.NewAnnotation.ToLocation();
                    addedLocation.ParentId = addedStructure.Entity.Id;
                    addedLocation.Username = username;
                    addedLocation.Created = DateTime.UtcNow;
                    addedLocation.LastModified = addedLocation.Created;
                    await _context.Locations.AddAsync(addedLocation);
                    await _context.SaveChangesAsync();
                }

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
                var response = new UpdateStructuresResponse();
                var username = CallerName(context);

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
                            var inserted = await _context.Structures.AddAsync(toInsert);
                            rowResponse.Success = true;
                            rowResponse.Created = inserted.Entity.ToProtobufMessage();
                            break;

                        case StructureChangeRequest.ActionOneofCase.Update:
                            var existing = await _context.Structures.FirstOrDefaultAsync(s => s.Id == change.Update.Id);
                            if (existing == null)
                            {
                                rowResponse.Success = false;
                                break;
                            }

                            ApplyUpdate(change.Update, existing, username);
                            rowResponse.Success = true;
                            rowResponse.Updated = _context.Structures.Update(existing).Entity.ToProtobufMessage();
                            break;

                        case StructureChangeRequest.ActionOneofCase.Delete:
                            // DeepDeleteStructure removes the locations, links and child structures
                            // that would otherwise block the delete on a foreign key.
                            await _context.Procedures.DeepDeleteStructureAsync(change.Delete);
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

        public override async Task<CreateStructureLinkResponse> CreateStructureLink(CreateStructureLinkRequest request, ServerCallContext context)
        {
            try
            {
                var link = request.NewLink;
                if (link.SourceId == link.TargetId)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "A structure cannot link to itself"));

                var row = new EfStructureLink
                {
                    SourceId = link.SourceId,
                    TargetId = link.TargetId,
                    Bidirectional = link.Bidirectional,
                    Tags = link.Tags,
                    Username = CallerName(context),
                    Created = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                };

                var added = await _context.StructureLinks.AddAsync(row);
                await _context.SaveChangesAsync();

                return new CreateStructureLinkResponse { Result = ToProtobufMessage(added.Entity) };
            }
            catch (RpcException) { throw; }
            catch (Exception e) { throw Failure(nameof(CreateStructureLink), e); }
        }

        public override async Task<UpdateStructureLinksResponse> UpdateLinks(UpdateStructureLinksRequest request, ServerCallContext context)
        {
            try
            {
                var username = CallerName(context);

                foreach (var link in request.Objs)
                {
                    var existing = await _context.StructureLinks.FirstOrDefaultAsync(
                        l => l.SourceId == link.SourceId && l.TargetId == link.TargetId);

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
                        });
                        continue;
                    }

                    existing.Bidirectional = link.Bidirectional;
                    existing.Tags = link.Tags;
                    existing.Username = username;
                    existing.LastModified = DateTime.UtcNow;
                    _context.StructureLinks.Update(existing);
                }

                await _context.SaveChangesAsync();
                return new UpdateStructureLinksResponse();
            }
            catch (Exception e) { throw Failure(nameof(UpdateLinks), e); }
        }

        public override async Task<DeleteStructureLinkResponse> DeleteStructureLink(DeleteStructureLinkRequest request, ServerCallContext context)
        {
            try
            {
                var existing = await _context.StructureLinks.FirstOrDefaultAsync(
                    l => l.SourceId == request.SourceId && l.TargetId == request.TargetId);
                if (existing == null)
                    throw new RpcException(new Status(StatusCode.NotFound,
                        $"StructureLink {request.SourceId}->{request.TargetId} not found"));

                _context.StructureLinks.Remove(existing);
                await _context.SaveChangesAsync();
                return new DeleteStructureLinkResponse();
            }
            catch (RpcException) { throw; }
            catch (Exception e) { throw Failure(nameof(DeleteStructureLink), e); }
        }

        public override async Task<MergeResponse> Merge(MergeRequest request, ServerCallContext context)
        {
            try
            {
                await _context.Procedures.MergeStructuresAsync(request.KeepId, request.MergeId);
                return new MergeResponse { KeptId = request.KeepId };
            }
            catch (Exception e) { throw Failure(nameof(Merge), e); }
        }

        public override async Task<SplitResponse> Split(SplitRequest request, ServerCallContext context)
        {
            try
            {
                var splitId = new OutputParameter<long?>();
                await _context.Procedures.SplitStructureAsync(request.FirstLocationIdOfSplitStructure, splitId);

                if (splitId.Value.HasValue == false)
                    throw new RpcException(new Status(StatusCode.FailedPrecondition,
                        $"Splitting structure {request.Id} at location {request.FirstLocationIdOfSplitStructure} produced no new structure"));

                return new SplitResponse { SplitStructureId = splitId.Value.Value };
            }
            catch (RpcException) { throw; }
            catch (Exception e) { throw Failure(nameof(Split), e); }
        }

        public override async Task<SplitAtLocationLinkResponse> SplitAtLocationLink(SplitAtLocationLinkRequest request, ServerCallContext context)
        {
            try
            {
                var splitId = new OutputParameter<long?>();
                await _context.Procedures.SplitStructureAtLocationLinkAsync(
                    request.LocationIdOfKeepStructure, request.LocationIdOfSplitStructure, splitId);

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

        private static string CallerName(ServerCallContext context) =>
            context.GetHttpContext()?.User?.Identity?.Name ?? "unknown";

        private static ProtoStructureLink ToProtobufMessage(EfStructureLink src) => new ProtoStructureLink
        {
            SourceId = src.SourceId,
            TargetId = src.TargetId,
            Bidirectional = src.Bidirectional,
            Tags = src.Tags ?? string.Empty,
            Username = src.Username ?? string.Empty
        };

        /// <summary>Builds the [dbo].[integer_list] table valued parameter the network procedures take.</summary>
        private static DataTable IdTable(IEnumerable<long> ids)
        {
            var table = new DataTable();
            table.Columns.Add("ID", typeof(long));
            foreach (var id in ids)
                table.Rows.Add(id);
            return table;
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
            double minRadius, DateTime? modifiedAfter, bool useVolumeCoordinates)
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

            return await query.ToListAsync();
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
