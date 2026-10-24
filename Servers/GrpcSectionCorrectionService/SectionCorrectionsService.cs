using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Viking.SectionCorrection;
using Viking.SectionCorrectionServiceTypes.gRPC.V1.Protos;
using SectionCorrectionMsg = Viking.SectionCorrectionServiceTypes.gRPC.V1.Protos.SectionCorrection;
using FitMode = AnnotationVizLib.CorrectionMode;
using ProtoFitMode = Viking.SectionCorrectionServiceTypes.gRPC.V1.Protos.CorrectionMode;
using GeoVector2 = Geometry.Vector2;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// Protocol V1 of AnnotateSectionCorrections (section_correction.proto): translates V1 messages to and from
    /// <see cref="CorrectionServiceCore"/>.  Kept only for clients that have not moved to V2
    /// (<see cref="SectionCorrectionsServiceV2"/>): installed Viking builds and SBFSEM-tools.  Delete this class and the
    /// V1 proto once they have.
    /// </summary>
    public sealed class SectionCorrectionsService : AnnotateSectionCorrections.AnnotateSectionCorrectionsBase
    {
        readonly CorrectionServiceCore _core;

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
            _core = new CorrectionServiceCore(catalog, volumes, connections, configuration, rebuild, logger, rebuildRunner, lifetime);
        }

        public override async Task<ListVolumesResponse> ListVolumes(ListVolumesRequest request, ServerCallContext context)
        {
            ListVolumesResponse response = new();
            foreach (CorrectionServiceCore.VolumeEntry v in await _core.ListVolumesAsync(context.CancellationToken).ConfigureAwait(false))
                response.Volumes.Add(new VolumeInfo { VolumeName = v.Name, VikingxmlEndpoint = v.VikingXmlEndpoint, AnnotationEndpoint = v.AnnotationEndpoint });
            return response;
        }

        public override Task<RebuildStatus> GetRebuildStatus(GetRebuildStatusRequest request, ServerCallContext context)
        {
            RebuildSnapshot s = _core.RebuildStatus();
            return Task.FromResult(new RebuildStatus
            {
                InProgress = s.InProgress,
                LastStartedUtc = CorrectionServiceCore.ToTimestamp(s.LastStartedUtc),
                LastCompletedUtc = CorrectionServiceCore.ToTimestamp(s.LastCompletedUtc),
                LastError = s.LastError,
                ServiceVersion = RebuildStatusStore.ServiceVersion,
                CurrentVolume = s.CurrentVolume,
                CurrentStep = s.CurrentStep,
                GroupsDone = s.GroupsDone,
                GroupsTotal = s.GroupsTotal
            });
        }

        public override Task<RebuildVolumeResponse> RebuildVolume(RebuildVolumeRequest request, ServerCallContext context)
        {
            (bool started, string message) = _core.StartRebuild(context, request.VolumeName, request.Force);
            return Task.FromResult(new RebuildVolumeResponse { Started = started, Message = message });
        }

        public override Task<ListCorrectionSetsResponse> ListCorrectionSets(ListCorrectionSetsRequest request, ServerCallContext context)
        {
            ListCorrectionSetsResponse response = new();
            foreach ((string volumeName, PublishedCorrectionSet set) in _core.Sets(request.VolumeName))
                response.Sets.Add(new CorrectionSetInfo { VolumeName = volumeName, StosGroup = set.StosGroup, Provenance = ToProto(set.Provenance) });
            return Task.FromResult(response);
        }

        public override Task<ListCorrectedSectionsResponse> ListCorrectedSections(ListCorrectedSectionsRequest request, ServerCallContext context)
        {
            ListCorrectedSectionsResponse response = new();
            response.Z.AddRange(_core.RequireSet(request.VolumeName, request.StosGroup).Sections.Keys.OrderBy(z => z));
            return Task.FromResult(response);
        }

        public override Task<CorrectionManifest> GetCorrectionManifest(GetCorrectionManifestRequest request, ServerCallContext context)
        {
            PublishedCorrectionSet set = _core.RequireSet(request.VolumeName, request.StosGroup);
            CorrectionManifest message = new() { Provenance = ToProto(set.Provenance) };
            message.Z.AddRange(set.Sections.Keys.OrderBy(z => z));
            return Task.FromResult(message);
        }

        public override Task<SectionCorrectionMsg> GetSectionCorrection(GetSectionCorrectionRequest request, ServerCallContext context)
        {
            SectionVectorField field = _core.RequireSection(request.VolumeName, request.StosGroup, request.Z);
            SectionCorrectionMsg message = new() { PitchNm = field.PitchNm };
            foreach (LatticeNode node in field.Nodes)
                message.Nodes.Add(new LatticeVector { Gx = node.Gx, Gy = node.Gy, Dx = node.Dx, Dy = node.Dy, VoteCount = node.VoteCount });
            return Task.FromResult(message);
        }

        public override Task<CorrectPointsResponse> CorrectPoints(CorrectPointsRequest request, ServerCallContext context)
        {
            List<(long Z, IReadOnlyList<GeoVector2> Points)> sections =
                [.. request.Sections.Select(s => (s.Z, (IReadOnlyList<GeoVector2>)[.. s.Points.Select(p => new GeoVector2(p.X, p.Y))]))];
            bool sampleField = ResolveMode(request.HasCorrection, request.Correction).HasFlag(FitMode.Neighbor);
            IReadOnlyList<CorrectionServiceCore.SectionSample> samples =
                _core.SamplePoints(request.VolumeName, request.StosGroup, sections, sampleField);

            CorrectPointsResponse response = new() { VolumeName = request.VolumeName, StosGroup = request.StosGroup };
            for (int i = 0; i < sections.Count; i++)
            {
                CorrectedSection outSection = new() { Z = sections[i].Z, FoundSection = samples[i].Found };
                for (int j = 0; j < sections[i].Points.Count; j++)
                {
                    GeoVector2 p = sections[i].Points[j];
                    (GeoVector2 offset, bool trusted) = samples[i].Points[j];
                    outSection.Points.Add(new CorrectedXY { X = p.X + offset.X, Y = p.Y + offset.Y, Dx = offset.X, Dy = offset.Y, Trusted = trusted });
                }

                response.Sections.Add(outSection);
            }

            return Task.FromResult(response);
        }

        public override async Task<GetStructureFingerprintResponse> GetStructureFingerprint(GetStructureFingerprintRequest request, ServerCallContext context)
        {
            StructureFingerprint fingerprint = await _core.FingerprintAsync(
                request.VolumeName, request.StructureIds, request.IncludeChildren, context.CancellationToken).ConfigureAwait(false);
            return new GetStructureFingerprintResponse
            {
                VolumeName = request.VolumeName ?? "",
                AnnotationFingerprint = fingerprint.Token,
                AnnotationWatermark = CorrectionServiceCore.ToTimestamp(fingerprint.Newest),
                ServiceVersion = RebuildStatusStore.ServiceVersion
            };
        }

        public override async Task<CorrectStructuresResponse> CorrectStructures(CorrectStructuresRequest request, ServerCallContext context)
        {
            FitMode mode = ResolveMode(request.HasCorrection, request.Correction);
            CorrectionServiceCore.StructuresAnswer answer = await _core.CorrectStructuresAsync(
                request.VolumeName, request.StosGroup, request.StructureIds, request.IncludeChildren, mode, context.CancellationToken).ConfigureAwait(false);

            CorrectStructuresResponse response = new()
            {
                VolumeName = request.VolumeName ?? "",
                StosGroup = request.StosGroup ?? "",
                ServiceVersion = RebuildStatusStore.ServiceVersion,
                AnnotationFingerprint = answer.Fingerprint.Token,
                AnnotationWatermark = CorrectionServiceCore.ToTimestamp(answer.Fingerprint.Newest)
            };
            foreach (CorrectionServiceCore.CorrectedLocationData l in answer.Locations)
            {
                response.Locations.Add(new CorrectedLocation
                {
                    LocationId = l.LocationId,
                    StructureId = l.StructureId,
                    Z = l.Z,
                    X = l.Position.X,
                    Y = l.Position.Y,
                    Dx = l.Offset.X,
                    Dy = l.Offset.Y
                });
            }

            return response;
        }

        /// <summary>V1 sends the mode as an optional flag value; unset means All.</summary>
        static FitMode ResolveMode(bool hasCorrection, ProtoFitMode value)
        {
            if (!hasCorrection)
                return FitMode.All;

            FitMode mode = (FitMode)(int)value;
            if ((mode & ~FitMode.All) != 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"unknown correction mode {value}"));
            return mode;
        }

        static CorrectionProvenance ToProto(CorrectionProvenanceDto dto) => dto is null
            ? new CorrectionProvenance()
            : new CorrectionProvenance
            {
                BuiltUtc = CorrectionServiceCore.ToTimestamp(dto.BuiltUtc),
                PitchNm = dto.PitchNm,
                KernelRadiusNm = dto.KernelRadiusNm
            };
    }
}
