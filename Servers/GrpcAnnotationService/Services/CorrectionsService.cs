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
    /// <summary>
    /// Read-only gRPC service that serves published section-correction sets (residual lattice fields)
    /// from the in-memory <see cref="CorrectionCatalog"/> to the Viking clients.
    /// </summary>
    /// <remarks>
    /// Registered in <c>Startup</c> behind the protected-scope authorization policy. Every RPC only reads
    /// the catalog, so the service holds no per-call state and is safe for concurrent calls. A missing
    /// or blank correction set or section is reported as <see cref="StatusCode.NotFound"/>; malformed
    /// <see cref="CorrectPoints"/> requests are <see cref="StatusCode.InvalidArgument"/>.
    /// </remarks>
    public sealed class CorrectionsService : AnnotateCorrections.AnnotateCorrectionsBase
    {
        readonly CorrectionCatalog _catalog;
        readonly ILogger<CorrectionsService> _logger;

        /// <summary>Creates the service over the shared catalog of published correction sets.</summary>
        /// <param name="catalog">Catalog of published sets; must outlive the service.</param>
        /// <param name="logger">Logger for this service.</param>
        public CorrectionsService(CorrectionCatalog catalog, ILogger<CorrectionsService> logger)
        {
            _catalog = catalog;
            _logger = logger;
        }

        /// <summary>Lists every published correction set with its provenance, ordered by stos group name (case-insensitive).</summary>
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

        /// <summary>Lists the section Z values that have a correction in the requested set, ascending.</summary>
        /// <exception cref="RpcException"><see cref="StatusCode.NotFound"/> when the set does not exist.</exception>
        public override Task<ListCorrectedSectionsResponse> ListCorrectedSections(
            ListCorrectedSectionsRequest request,
            ServerCallContext context)
        {
            PublishedCorrectionSet set = RequireSet(request.StosGroup);
            ListCorrectedSectionsResponse response = new();
            response.Z.AddRange(set.Sections.Keys.OrderBy(z => z));
            return Task.FromResult(response);
        }

        /// <summary>Returns the set's provenance, volume URL (empty when unknown), and corrected section Z values.</summary>
        /// <exception cref="RpcException"><see cref="StatusCode.NotFound"/> when the set does not exist.</exception>
        public override Task<CorrectionManifest> GetCorrectionManifest(
            GetCorrectionManifestRequest request,
            ServerCallContext context)
        {
            PublishedCorrectionSet set = RequireSet(request.StosGroup);
            return Task.FromResult(ToManifestProto(set));
        }

        /// <summary>
        /// Returns one section's lattice of correction vectors with the raster origin and pitch (nm)
        /// needed to place the nodes in volume space.
        /// </summary>
        /// <exception cref="RpcException"><see cref="StatusCode.NotFound"/> when the set or the section does not exist.</exception>
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

        /// <summary>
        /// Applies the stored correction to batches of volume points, one result section per request section
        /// in request order.
        /// </summary>
        /// <remarks>
        /// A section with no correction is not an error: it is returned with <c>FoundSection</c> false and
        /// every point unmoved and untrusted, so callers can tell "no correction" from "zero correction".
        /// Requests are validated before the set is looked up.
        /// </remarks>
        /// <exception cref="RpcException">
        /// <see cref="StatusCode.InvalidArgument"/> for no sections, a section without points, or a repeated Z;
        /// <see cref="StatusCode.NotFound"/> when the set does not exist.
        /// </exception>
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

        /// <summary>Looks up a published set, throwing <see cref="StatusCode.NotFound"/> for a blank or unknown name.</summary>
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

        /// <summary>Builds the manifest message for a set; a missing volume URL becomes an empty string.</summary>
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

        /// <summary>Converts stored provenance to its message; null provenance yields an empty message.</summary>
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

        /// <summary>Converts to a protobuf timestamp, treating an unspecified-kind value as UTC rather than local time.</summary>
        static Timestamp ToTimestamp(DateTime value)
        {
            DateTime utc = value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value.ToUniversalTime();
            return Timestamp.FromDateTime(utc);
        }
    }
}
