using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AnnotationVizLib;
using Viking.SectionCorrection;
using Viking.SectionCorrectionBuilder;
using Viking.SectionCorrectionServiceTypes.gRPC.V1.Protos;
using Google.Protobuf.WellKnownTypes;
using Geometry;
using SectionCorrectionMsg = Viking.SectionCorrectionServiceTypes.gRPC.V1.Protos.SectionCorrection;
using FitMode = AnnotationVizLib.CorrectionMode;
using ProtoFitMode = Viking.SectionCorrectionServiceTypes.gRPC.V1.Protos.CorrectionMode;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// Serves published residual fields keyed by Identity volume name + StosGroup.
    /// Does not rebuild; CorrectionRebuildHostedService writes the catalog this class reads.
    /// </summary>
    public sealed class SectionCorrectionsService : AnnotateSectionCorrections.AnnotateSectionCorrectionsBase
    {
        readonly VolumeCorrectionCatalog _catalog;
        readonly IIdentityVolumeSource _volumes;
        readonly AnnotationConnectionResolver _connections;
        readonly IConfiguration _configuration;
        readonly RebuildStatusStore _rebuild;
        readonly ILogger<SectionCorrectionsService> _logger;
        readonly CorrectionRebuildRunner _rebuildRunner;
        readonly IHostApplicationLifetime _lifetime;

        public SectionCorrectionsService(
            VolumeCorrectionCatalog catalog,
            IIdentityVolumeSource volumes,
            AnnotationConnectionResolver connections,
            IConfiguration configuration,
            RebuildStatusStore rebuild,
            ILogger<SectionCorrectionsService> logger,
            CorrectionRebuildRunner rebuildRunner = null,
            IHostApplicationLifetime lifetime = null)
        {
            _rebuildRunner = rebuildRunner;
            _lifetime = lifetime;
            _catalog = catalog;
            _volumes = volumes;
            _connections = connections;
            _configuration = configuration;
            _rebuild = rebuild;
            _logger = logger;
        }

        public override async Task<ListVolumesResponse> ListVolumes(
            ListVolumesRequest request,
            ServerCallContext context)
        {
            // Only contexts Identity assigns to this service that have an annotation database are volumes here.
            // volume_name is the correction-set key: the annotation database.
            Dictionary<string, VolumeInfo> byName = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (IdentityVolumeRow row in await _volumes.ListAsync(context.CancellationToken).ConfigureAwait(false))
                {
                    if (!row.HasAnnotationDatabase)
                        continue;
                    byName[row.CorrectionDatabase] = new VolumeInfo
                    {
                        VolumeName = row.CorrectionDatabase,
                        VikingxmlEndpoint = row.VikingXmlEndpoint ?? "",
                        AnnotationEndpoint = row.AnnotationEndpoint ?? ""
                    };
                }
            }
            catch (Exception ex) when (IsIdentityTransportFailure(ex, context.CancellationToken))
            {
                _logger.LogWarning(ex, "Identity volume list failed.");
                throw new RpcException(new Status(
                    StatusCode.Unavailable,
                    $"Identity volume list unavailable: {ex.Message}"));
            }

            ListVolumesResponse response = new();
            foreach (VolumeInfo info in byName.Values.OrderBy(v => v.VolumeName, StringComparer.OrdinalIgnoreCase))
                response.Volumes.Add(info);
            return response;
        }

        public override Task<RebuildStatus> GetRebuildStatus(
            GetRebuildStatusRequest request,
            ServerCallContext context)
        {
            return Task.FromResult(_rebuild.Snapshot());
        }

        /// <summary>
        /// Manual rebuild trigger for the container's correction-menu console. The gRPC surface is otherwise
        /// anonymous, so this only accepts a caller on the loopback interface (a process inside the container);
        /// traffic published through Docker arrives from the bridge address and is refused.
        /// </summary>
        public override Task<RebuildVolumeResponse> RebuildVolume(
            RebuildVolumeRequest request,
            ServerCallContext context)
        {
            System.Net.IPAddress caller = context.GetHttpContext()?.Connection.RemoteIpAddress;
            if (caller is null || !System.Net.IPAddress.IsLoopback(caller))
                throw new RpcException(new Status(StatusCode.PermissionDenied,
                    "RebuildVolume is only available from inside the service host."));
            if (_rebuildRunner is null)
                throw new RpcException(new Status(StatusCode.Unimplemented, "This host has no rebuild runner."));

            string volume = request.VolumeName?.Trim() ?? "";
            bool started = _rebuildRunner.TryStartInBackground(
                volume, request.Force, _lifetime?.ApplicationStopping ?? CancellationToken.None);
            string target = volume.Length == 0 ? "all volumes" : $"'{volume}'";
            _logger.LogInformation("Manual rebuild of {Target} (force={Force}) {Result}.",
                target, request.Force, started ? "started" : "refused: one is already running");
            return Task.FromResult(new RebuildVolumeResponse
            {
                Started = started,
                Message = started ? $"Rebuild of {target} started." : "A rebuild is already running."
            });
        }

        public override Task<ListCorrectionSetsResponse> ListCorrectionSets(
            ListCorrectionSetsRequest request,
            ServerCallContext context)
        {
            IEnumerable<(string VolumeName, PublishedCorrectionSet Set)> sets = _catalog.Sets;
            if (!string.IsNullOrWhiteSpace(request.VolumeName))
                sets = sets.Where(s => string.Equals(s.VolumeName, request.VolumeName, StringComparison.OrdinalIgnoreCase));

            ListCorrectionSetsResponse response = new();
            foreach ((string volumeName, PublishedCorrectionSet set) in sets
                         .OrderBy(s => s.VolumeName, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(s => s.Set.StosGroup, StringComparer.OrdinalIgnoreCase))
            {
                response.Sets.Add(new CorrectionSetInfo
                {
                    VolumeName = volumeName,
                    StosGroup = set.StosGroup,
                    Provenance = ToProto(set.Provenance)
                });
            }

            return Task.FromResult(response);
        }

        public override Task<ListCorrectedSectionsResponse> ListCorrectedSections(
            ListCorrectedSectionsRequest request,
            ServerCallContext context)
        {
            PublishedCorrectionSet set = RequireSet(request.VolumeName, request.StosGroup);
            ListCorrectedSectionsResponse response = new()
            {
                VolumeName = request.VolumeName,
                StosGroup = set.StosGroup
            };
            response.Z.AddRange(set.Sections.Keys.OrderBy(z => z));
            return Task.FromResult(response);
        }

        public override Task<CorrectionManifest> GetCorrectionManifest(
            GetCorrectionManifestRequest request,
            ServerCallContext context)
        {
            PublishedCorrectionSet set = RequireSet(request.VolumeName, request.StosGroup);
            return Task.FromResult(ToManifestProto(request.VolumeName, set));
        }

        public override Task<SectionCorrectionMsg> GetSectionCorrection(
            GetSectionCorrectionRequest request,
            ServerCallContext context)
        {
            PublishedCorrectionSet set = RequireSet(request.VolumeName, request.StosGroup);
            if (!set.TryGetSection(request.Z, out SectionVectorField field))
            {
                throw new RpcException(new Status(
                    StatusCode.NotFound,
                    $"no correction for section {request.Z} in '{request.VolumeName}' '{request.StosGroup}'"));
            }

            (double originX, double originY, _, _) = field.OccupiedRasterBounds();
            SectionCorrectionMsg message = new()
            {
                VolumeName = request.VolumeName,
                StosGroup = set.StosGroup,
                Z = field.Z,
                OriginXNm = originX,
                OriginYNm = originY,
                PitchNm = field.PitchNm
            };
            foreach (LatticeNode node in field.Nodes)
            {
                message.Nodes.Add(new LatticeVector
                {
                    Gx = node.Gx,
                    Gy = node.Gy,
                    Dx = node.Dx,
                    Dy = node.Dy,
                    VoteCount = node.VoteCount
                });
            }

            return Task.FromResult(message);
        }

        public override Task<CorrectPointsResponse> CorrectPoints(
            CorrectPointsRequest request,
            ServerCallContext context)
        {
            if (request.Sections is null || request.Sections.Count == 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "sections is empty"));

            HashSet<long> seen = [];
            foreach (SectionPoints section in request.Sections)
            {
                if (section.Points is null || section.Points.Count == 0)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, $"section {section.Z} has no points"));
                if (!seen.Add(section.Z))
                    throw new RpcException(new Status(StatusCode.InvalidArgument, $"duplicate z {section.Z}"));
            }

            FitMode mode = ResolveMode(request.HasCorrection, request.Correction);
            bool sampleField = mode.HasFlag(FitMode.Neighbor);
            PublishedCorrectionSet set = sampleField ? RequireSet(request.VolumeName, request.StosGroup) : null;
            CorrectPointsResponse response = new()
            {
                VolumeName = request.VolumeName,
                StosGroup = request.StosGroup
            };
            foreach (SectionPoints section in request.Sections)
            {
                CorrectedSection outSection = new() { Z = section.Z };
                if (!sampleField)
                {
                    outSection.FoundSection = true;
                    AddUnshiftedPoints(outSection, section);
                }
                else if (!set.TryGetSection(section.Z, out SectionVectorField field))
                {
                    outSection.FoundSection = false;
                    AddUnshiftedPoints(outSection, section);
                }
                else
                {
                    outSection.FoundSection = true;
                    foreach (VolumeXY p in section.Points)
                    {
                        (Vector2 offset, bool trusted) = field.Sample(new Vector2(p.X, p.Y));
                        outSection.Points.Add(new CorrectedXY
                        {
                            X = p.X + offset.X,
                            Y = p.Y + offset.Y,
                            Dx = offset.X,
                            Dy = offset.Y,
                            Trusted = trusted
                        });
                    }
                }

                response.Sections.Add(outSection);
            }

            return Task.FromResult(response);
        }

        /// <summary>
        /// Fills <paramref name="outSection"/> with <paramref name="section"/>'s points unchanged: zero offset,
        /// not trusted. Used by <see cref="CorrectPoints"/> when no field is sampled or the section has none.
        /// </summary>
        static void AddUnshiftedPoints(CorrectedSection outSection, SectionPoints section)
        {
            foreach (VolumeXY p in section.Points)
            {
                outSection.Points.Add(new CorrectedXY
                {
                    X = p.X,
                    Y = p.Y,
                    Dx = 0,
                    Dy = 0,
                    Trusted = false
                });
            }
        }

        /// <summary>
        /// The structures' current annotation fingerprint from SQL aggregates alone, so a client can check cached
        /// <see cref="CorrectStructures"/> offsets in milliseconds.  Covers the same structures CorrectStructures does
        /// for the same <c>include_children</c>.
        /// </summary>
        public override async Task<GetStructureFingerprintResponse> GetStructureFingerprint(
            GetStructureFingerprintRequest request,
            ServerCallContext context)
        {
            if (request.StructureIds is null || request.StructureIds.Count == 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "structure_ids is empty"));

            string connection = _connections.Resolve(request.VolumeName);
            if (string.IsNullOrWhiteSpace(connection))
                throw new RpcException(new Status(StatusCode.FailedPrecondition, $"no annotation SQL mapping for '{request.VolumeName}'"));

            StructureFingerprint fingerprint = await SqlMorphologyLoader.LoadStructureFingerprintAsync(
                connection, request.StructureIds, request.IncludeChildren, context.CancellationToken).ConfigureAwait(false);

            GetStructureFingerprintResponse response = new()
            {
                VolumeName = request.VolumeName ?? "",
                AnnotationFingerprint = fingerprint.Token,
                ServiceVersion = RebuildStatusStore.ServiceVersion
            };
            if (fingerprint.Newest is DateTime watermark)
                response.AnnotationWatermark = ToTimestamp(watermark);

            return response;
        }

        public override async Task<CorrectStructuresResponse> CorrectStructures(
            CorrectStructuresRequest request,
            ServerCallContext context)
        {
            if (request.StructureIds is null || request.StructureIds.Count == 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "structure_ids is empty"));

            FitMode mode = ResolveMode(request.HasCorrection, request.Correction);
            string connection = _connections.Resolve(request.VolumeName);
            if (string.IsNullOrWhiteSpace(connection))
                throw new RpcException(new Status(StatusCode.FailedPrecondition, $"no annotation SQL mapping for '{request.VolumeName}'"));

            string volumeUrl = await VolumeUrlAsync(request.VolumeName, context).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(volumeUrl))
                throw new RpcException(new Status(StatusCode.NotFound, $"no VikingXML endpoint for '{request.VolumeName}'"));

            SqlMorphologyLoader.StructuresLoadResult loaded;
            try
            {
                loaded = await SqlMorphologyLoader.LoadStructuresAsync(
                    connection,
                    volumeUrl,
                    request.StosGroup,
                    request.StructureIds,
                    request.IncludeChildren,
                    _configuration["Corrections:CachePath"],
                    context.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or WebException or HttpRequestException or IOException)
            {
                // Volume XML / stos HTTP load shares the host with rebuild traffic; map transient load
                // failures to UNAVAILABLE so clients can wait rather than treat UNKNOWN as a hard fault.
                throw new RpcException(new Status(
                    StatusCode.Unavailable,
                    $"volume mapping temporarily unavailable for '{request.VolumeName}': {ex.Message}"));
            }

            NeighborResidualField field = null;
            if (mode.HasFlag(FitMode.Neighbor) &&
                _catalog.TryGet(request.VolumeName, request.StosGroup, out PublishedCorrectionSet published))
                field = ToNeighborField(published);

            CorrectStructuresResponse response = new()
            {
                VolumeName = request.VolumeName ?? "",
                StosGroup = request.StosGroup ?? "",
                ServiceVersion = RebuildStatusStore.ServiceVersion
            };
            response.AnnotationFingerprint = loaded.Fingerprint.Token;
            if (loaded.Fingerprint.Newest is DateTime watermark)
                response.AnnotationWatermark = ToTimestamp(watermark);

            foreach (MorphologyGraph cell in loaded.Cells)
            {
                Dictionary<ulong, Vector2> before = [];
                foreach (MorphologyNode node in cell.Nodes.Values)
                    before[node.Key] = new Vector2(node.Center.X, node.Center.Y);

                if (mode.HasFlag(FitMode.Neighbor))
                    MorphologyGraph.ApplyResidualField(cell, field);
                if (mode.HasFlag(FitMode.CurveFit))
                    MorphologyGraph.CurveFitProcesses(cell, new MorphologyGraph.CurveFitOptions(
                        MorphologyGraph.DefaultCurveFitHalfWindow, null, null));

                foreach (MorphologyNode node in cell.Nodes.Values)
                {
                    Vector2 origin = before[node.Key];
                    Vector3 corrected = node.Center;
                    response.Locations.Add(new CorrectedLocation
                    {
                        LocationId = (long)node.Key,
                        StructureId = (long)cell.StructureID,
                        Z = node.Location.UnscaledZ,
                        X = corrected.X,
                        Y = corrected.Y,
                        Dx = corrected.X - origin.X,
                        Dy = corrected.Y - origin.Y
                    });
                }
            }

            return response;
        }

        static FitMode ResolveMode(bool hasCorrection, ProtoFitMode value)
        {
            if (!hasCorrection)
                return FitMode.All;

            FitMode mode = (FitMode)(int)value;
            if ((mode & ~FitMode.All) != 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"unknown correction mode {value}"));
            return mode;
        }

        static NeighborResidualField ToNeighborField(PublishedCorrectionSet set)
        {
            double pitch = set.Provenance?.PitchNm > 0 ? set.Provenance.PitchNm : NeighborResidualField.GridSizeNm;
            double kernel = set.Provenance?.KernelRadiusNm > 0 ? set.Provenance.KernelRadiusNm : NeighborResidualField.KernelRadiusNm;
            return NeighborResidualField.FromLattice(set.EnumerateLattice(), pitch, kernel);
        }

        /// <summary>
        /// Looks up the VikingXML endpoint Identity lists for <paramref name="volumeName"/>; returns "" when the
        /// volume is not listed. Called only by <see cref="CorrectStructures"/>.
        /// </summary>
        /// <exception cref="RpcException">
        /// <see cref="StatusCode.Unavailable"/> when the Identity listing fails in transport (Identity unreachable,
        /// not configured, no access token, or an HTTP timeout), matching how the sibling volume-load failure is
        /// mapped so callers can retry instead of seeing UNKNOWN.
        /// </exception>
        /// <exception cref="OperationCanceledException">Rethrown unchanged when the call itself was cancelled.</exception>
        async Task<string> VolumeUrlAsync(string volumeName, ServerCallContext context)
        {
            try
            {
                foreach (IdentityVolumeRow row in await _volumes.ListAsync(context.CancellationToken).ConfigureAwait(false))
                {
                    if (string.Equals(row.Name, volumeName, StringComparison.OrdinalIgnoreCase))
                        return row.VikingXmlEndpoint;
                }

                return "";
            }
            catch (Exception ex) when (IsIdentityTransportFailure(ex, context.CancellationToken))
            {
                _logger.LogWarning(ex, "Identity volume list failed for CorrectStructures '{Volume}'.", volumeName);
                throw new RpcException(new Status(
                    StatusCode.Unavailable,
                    $"Identity volume list temporarily unavailable for '{volumeName}': {ex.Message}"));
            }
        }

        /// <summary>
        /// True for failures of an <see cref="IIdentityVolumeSource"/> that mean Identity is unreachable rather than
        /// a bug: HTTP and I/O errors, the missing-token <see cref="InvalidOperationException"/> from
        /// <see cref="IdentityAssignmentClient"/>, the not-configured error from <see cref="UnconfiguredIdentityVolumeSource"/>,
        /// and an HTTP timeout
        /// (an <see cref="OperationCanceledException"/> while <paramref name="callCancellation"/> is not cancelled).
        /// Caller cancellation is excluded so it surfaces as CANCELLED.
        /// </summary>
        static bool IsIdentityTransportFailure(Exception ex, CancellationToken callCancellation) => ex switch
        {
            OperationCanceledException => !callCancellation.IsCancellationRequested,
            HttpRequestException or IOException or InvalidOperationException => true,
            _ => false
        };

        PublishedCorrectionSet RequireSet(string volumeName, string stosGroup)
        {
            if (string.IsNullOrWhiteSpace(volumeName) ||
                string.IsNullOrWhiteSpace(stosGroup) ||
                !_catalog.TryGet(volumeName, stosGroup, out PublishedCorrectionSet set))
            {
                throw new RpcException(new Status(
                    StatusCode.NotFound,
                    $"no correction set '{volumeName}' '{stosGroup}'"));
            }

            return set;
        }

        static CorrectionManifest ToManifestProto(string volumeName, PublishedCorrectionSet set)
        {
            CorrectionManifest message = new()
            {
                VolumeName = volumeName,
                StosGroup = set.StosGroup,
                Provenance = ToProto(set.Provenance),
                VolumeUrl = set.Manifest.VolumeUrl ?? ""
            };
            message.Z.AddRange(set.Sections.Keys.OrderBy(z => z));
            return message;
        }

        static CorrectionProvenance ToProto(CorrectionProvenanceDto dto)
        {
            if (dto is null)
                return new CorrectionProvenance();

            ResidualWindowDto window = dto.ResidualWindow ?? new ResidualWindowDto();
            return new CorrectionProvenance
            {
                BuiltUtc = ToTimestamp(dto.BuiltUtc),
                AnnotationWatermark = ToTimestamp(dto.AnnotationWatermark),
                PitchNm = dto.PitchNm,
                KernelRadiusNm = dto.KernelRadiusNm,
                MinAnnotationVotes = dto.MinAnnotationVotes,
                ResidualWindow = new ResidualWindow
                {
                    MinBoth = window.MinBoth,
                    MaxBoth = window.MaxBoth,
                    MinOne = window.MinOne,
                    MaxOne = window.MaxOne
                }
            };
        }

        static Timestamp ToTimestamp(DateTime value)
        {
            DateTime utc = value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value.ToUniversalTime();
            return Timestamp.FromDateTime(utc);
        }
    }
}
