using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Viking.SectionCorrection;
using Viking.SectionCorrectionServiceTypes.gRPC.V2.Protos;
using FitMode = AnnotationVizLib.CorrectionMode;
using GeoVector2 = Geometry.Vector2;
using GridCell = Viking.gRPC.Geometry.V1.GridCell;
using ProtoVector2 = Viking.gRPC.Geometry.V1.Vector2;
using SectionCorrectionMsg = Viking.SectionCorrectionServiceTypes.gRPC.V2.Protos.SectionCorrection;
using V2LatticeNode = Viking.SectionCorrectionServiceTypes.gRPC.V2.Protos.LatticeNode;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// AnnotateSectionCorrections (section_correction_v2.proto; version 1 is retired): translates its messages to and
    /// from <see cref="CorrectionServiceCore"/>.  Positions and offsets are volume nm in the shared geometry Vector2.
    /// </summary>
    public sealed class SectionCorrectionsServiceV2 : AnnotateSectionCorrections.AnnotateSectionCorrectionsBase
    {
        readonly CorrectionServiceCore _core;

        public SectionCorrectionsServiceV2(
            VolumeCorrectionCatalog catalog,
            IIdentityVolumeSource volumes,
            AnnotationConnectionResolver connections,
            IConfiguration configuration,
            RebuildStatusStore rebuild,
            ILogger<SectionCorrectionsServiceV2> logger,
            CorrectionRebuildRunner rebuildRunner = null,
            IHostApplicationLifetime lifetime = null)
        {
            _core = new CorrectionServiceCore(catalog, volumes, connections, configuration, rebuild, logger, rebuildRunner, lifetime);
        }

        public override async Task<ListVolumesResponse> ListVolumes(Empty request, ServerCallContext context)
        {
            ListVolumesResponse response = new();
            foreach (CorrectionServiceCore.VolumeEntry v in await _core.ListVolumesAsync(context.CancellationToken).ConfigureAwait(false))
                response.Volumes.Add(new VolumeInfo { VolumeName = v.Name, VikingxmlEndpoint = v.VikingXmlEndpoint, AnnotationEndpoint = v.AnnotationEndpoint });
            return response;
        }

        public override Task<RebuildStatus> GetRebuildStatus(Empty request, ServerCallContext context)
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
            {
                CorrectionSet message = new()
                {
                    Id = new CorrectionSetId { VolumeName = volumeName, StosGroup = set.StosGroup },
                    BuiltUtc = CorrectionServiceCore.ToTimestamp(set.Provenance?.BuiltUtc),
                    PitchNm = set.Provenance?.PitchNm ?? 0,
                    KernelRadiusNm = set.Provenance?.KernelRadiusNm ?? 0
                };
                message.Z.AddRange(set.Sections.Keys.OrderBy(z => z));
                message.DamagedZ.AddRange((set.Provenance?.DamagedSections ?? []).Select(z => (long)z));
                response.Sets.Add(message);
            }

            return Task.FromResult(response);
        }

        public override Task<SectionCorrectionMsg> GetSectionCorrection(GetSectionCorrectionRequest request, ServerCallContext context)
        {
            SectionVectorField field = _core.RequireSection(request.Set?.VolumeName, request.Set?.StosGroup, request.Z);
            SectionCorrectionMsg message = new() { PitchNm = field.PitchNm };
            foreach (Viking.SectionCorrection.LatticeNode node in field.Nodes)
                message.Nodes.Add(new V2LatticeNode { Node = new GridCell { Row = node.Gy, Col = node.Gx }, Offset = new ProtoVector2 { X = node.Dx, Y = node.Dy }, Votes = node.VoteCount });
            return Task.FromResult(message);
        }

        public override Task<CorrectPointsResponse> CorrectPoints(CorrectPointsRequest request, ServerCallContext context)
        {
            List<(long Z, IReadOnlyList<GeoVector2> Points)> sections =
                [.. request.Sections.Select(s => (s.Z, (IReadOnlyList<GeoVector2>)[.. s.Points.Select(ToGeometry)]))];
            IReadOnlyList<CorrectionServiceCore.SectionSample> samples =
                _core.SamplePoints(request.Set?.VolumeName, request.Set?.StosGroup, sections);

            CorrectPointsResponse response = new();
            foreach (CorrectionServiceCore.SectionSample sample in samples)
            {
                SectionOffsets offsets = new() { Found = sample.Found };
                foreach ((GeoVector2 offset, bool trusted) in sample.Points)
                    offsets.Points.Add(new PointOffset { Offset = ToProto(offset), Trusted = trusted });
                response.Sections.Add(offsets);
            }

            return Task.FromResult(response);
        }

        public override async Task<CorrectStructuresResponse> CorrectStructures(CorrectStructuresRequest request, ServerCallContext context)
        {
            CorrectionServiceCore.StructuresAnswer answer = await _core.CorrectStructuresAsync(
                request.Set?.VolumeName, request.Set?.StosGroup, request.StructureIds, request.IncludeChildren,
                ToFitMode(request.Correction), context.CancellationToken).ConfigureAwait(false);

            CorrectStructuresResponse response = new() { Stamp = Stamp(answer.Fingerprint, answer.FieldBuiltUtc) };
            foreach (CorrectionServiceCore.CorrectedLocationData l in answer.Locations)
            {
                response.Locations.Add(new CorrectedLocation
                {
                    LocationId = l.LocationId,
                    StructureId = l.StructureId,
                    Z = l.Z,
                    Position = ToProto(l.Position),
                    Offset = ToProto(l.Offset)
                });
            }

            return response;
        }

        public override async Task<AnswerStamp> GetAnswerStamp(CorrectStructuresRequest request, ServerCallContext context)
        {
            (StructureFingerprint fingerprint, System.DateTime? fieldBuiltUtc) = await _core.AnswerStampAsync(
                request.Set?.VolumeName, request.Set?.StosGroup, request.StructureIds, request.IncludeChildren,
                ToFitMode(request.Correction), context.CancellationToken).ConfigureAwait(false);
            return Stamp(fingerprint, fieldBuiltUtc);
        }

        static AnswerStamp Stamp(StructureFingerprint fingerprint, System.DateTime? fieldBuiltUtc) => new()
        {
            ServiceVersion = RebuildStatusStore.ServiceVersion,
            FieldBuiltUtc = CorrectionServiceCore.ToTimestamp(fieldBuiltUtc),
            AnnotationFingerprint = fingerprint.Token,
            AnnotationWatermark = CorrectionServiceCore.ToTimestamp(fingerprint.Newest)
        };

        /// <summary>
        /// V2's zero value is Neighbor (the published field), so the mode needs no presence flag.
        /// <para>
        /// 1.5.2 (2026-10-27): the CurveFit pass is back, at James Anderson's request, so the damaged-section
        /// measurements can be run against it. The proto (gRPC_Protos 5560e47) reserves 2 and 3. Production SBFSEM-tools
        /// still sends 2 and 3 and has been given the field since 1.5.1, so both stay the field: no client moves to
        /// fit-only at the cutover. The fit is reached only by 4, a raw integer not in the proto: field, then CurveFit
        /// (what 0 meant before 1.5.0). 0 and 1 are unchanged. Any other value is refused.
        /// </para>
        /// </summary>
        public static FitMode ToFitMode(CorrectionMode mode) => (int)mode switch
        {
            (int)CorrectionMode.Neighbor or 2 or 3 => FitMode.Neighbor,
            (int)CorrectionMode.None => FitMode.None,
            4 => FitMode.All,
            _ => throw new RpcException(new Status(StatusCode.InvalidArgument, $"unknown correction mode {(int)mode}"))
        };

        static ProtoVector2 ToProto(GeoVector2 v) => new() { X = v.X, Y = v.Y };

        static GeoVector2 ToGeometry(ProtoVector2 v) => v is null ? GeoVector2.Zero : new GeoVector2(v.X, v.Y);
    }
}
