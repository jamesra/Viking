using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.SectionCorrection;
using Google.Protobuf.WellKnownTypes;
using Geometry;

namespace gRPCAnnotationService
{
    public sealed class CorrectionsService : AnnotateCorrections.AnnotateCorrectionsBase
    {
        readonly CorrectionCatalog _catalog;
        readonly ILogger<CorrectionsService> _logger;

        public CorrectionsService(CorrectionCatalog catalog, ILogger<CorrectionsService> logger)
        {
            _catalog = catalog;
            _logger = logger;
        }

        public override Task<ListCorrectionSetsResponse> ListCorrectionSets(
            ListCorrectionSetsRequest request,
            ServerCallContext context)
        {
            ListCorrectionSetsResponse response = new();
            foreach (PublishedCorrectionSet set in _catalog.Sets.OrderBy(s => s.StosGroup, StringComparer.OrdinalIgnoreCase))
            {
                response.Sets.Add(new CorrectionSetInfo
                {
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
            PublishedCorrectionSet set = RequireSet(request.StosGroup);
            ListCorrectedSectionsResponse response = new();
            response.Z.AddRange(set.Sections.Keys.OrderBy(z => z));
            return Task.FromResult(response);
        }

        public override Task<CorrectionManifest> GetCorrectionManifest(
            GetCorrectionManifestRequest request,
            ServerCallContext context)
        {
            PublishedCorrectionSet set = RequireSet(request.StosGroup);
            return Task.FromResult(ToManifestProto(set));
        }

        public override Task<SectionCorrection> GetSectionCorrection(
            GetSectionCorrectionRequest request,
            ServerCallContext context)
        {
            PublishedCorrectionSet set = RequireSet(request.StosGroup);
            if (!set.TryGetSection(request.Z, out SectionVectorField field))
            {
                throw new RpcException(new Status(
                    StatusCode.NotFound,
                    $"no correction for section {request.Z} in '{request.StosGroup}'"));
            }

            (double originX, double originY, _, _) = field.OccupiedRasterBounds();
            SectionCorrection message = new()
            {
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

            PublishedCorrectionSet set = RequireSet(request.StosGroup);
            CorrectPointsResponse response = new();
            foreach (SectionPoints section in request.Sections)
            {
                CorrectedSection outSection = new() { Z = section.Z };
                if (!set.TryGetSection(section.Z, out SectionVectorField field))
                {
                    outSection.FoundSection = false;
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

        PublishedCorrectionSet RequireSet(string stosGroup)
        {
            if (string.IsNullOrWhiteSpace(stosGroup) || !_catalog.TryGet(stosGroup, out PublishedCorrectionSet set))
            {
                throw new RpcException(new Status(
                    StatusCode.NotFound,
                    $"no correction set '{stosGroup}'"));
            }

            return set;
        }

        static CorrectionManifest ToManifestProto(PublishedCorrectionSet set)
        {
            CorrectionManifest message = new()
            {
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
