using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Viking.SectionCorrectionServiceTypes.gRPC.V2.Protos;

namespace Viking.SectionCorrectionClient
{
    /// <summary>
    /// <see cref="ISectionShimSource"/> backed by the AnnotateSectionCorrections (protocol V2) <c>GetSectionCorrection</c>,
    /// <c>ListCorrectionSets</c> and <c>GetRebuildStatus</c> RPCs.
    /// </summary>
    public sealed class GrpcSectionShimSource : ISectionShimSource
    {
        readonly Func<Uri, CallInvoker> _invokerFactory;

        // GetSectionCorrection does not carry the kernel radius; it comes from the set read by GetVersionAsync.
        readonly ConcurrentDictionary<(string Database, string Stos), double> _kernelRadiusNm =
            new ConcurrentDictionary<(string, string), double>();

        /// <param name="invokerFactory">
        /// Returns the call invoker for a service URL, normally from the shared gRPC channel manager so channels are
        /// reused. Called once per request.
        /// </param>
        public GrpcSectionShimSource(Func<Uri, CallInvoker> invokerFactory)
        {
            _invokerFactory = invokerFactory ?? throw new ArgumentNullException(nameof(invokerFactory));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Call <see cref="GetVersionAsync"/> first for the set so the lattice gets the set's kernel radius; before that
        /// the service default applies.
        /// </remarks>
        public async Task<SectionLatticeField> GetLatticeAsync(
            Uri serviceUrl,
            string annotationDatabase,
            string stosGroup,
            long z,
            CancellationToken cancellationToken)
        {
            var client = new AnnotateSectionCorrections.AnnotateSectionCorrectionsClient(_invokerFactory(serviceUrl));
            var request = new GetSectionCorrectionRequest
            {
                Set = new CorrectionSetId { VolumeName = annotationDatabase, StosGroup = stosGroup ?? string.Empty },
                Z = z
            };

            try
            {
                SectionCorrection response = await client.GetSectionCorrectionAsync(request, cancellationToken: cancellationToken)
                    .ResponseAsync.ConfigureAwait(false);
                _kernelRadiusNm.TryGetValue((annotationDatabase, stosGroup ?? string.Empty), out double kernel);
                return new SectionLatticeField(
                    response.PitchNm,
                    kernel,
                    response.Nodes.Select(n => (n.Node?.Col ?? 0, n.Node?.Row ?? 0, n.Offset?.X ?? 0, n.Offset?.Y ?? 0)));
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<string> GetVersionAsync(
            Uri serviceUrl,
            string annotationDatabase,
            string stosGroup,
            CancellationToken cancellationToken)
        {
            var client = new AnnotateSectionCorrections.AnnotateSectionCorrectionsClient(_invokerFactory(serviceUrl));
            ListCorrectionSetsResponse listed;
            try
            {
                listed = await client
                    .ListCorrectionSetsAsync(new ListCorrectionSetsRequest { VolumeName = annotationDatabase },
                        cancellationToken: cancellationToken).ResponseAsync.ConfigureAwait(false);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                return null;
            }

            CorrectionSet set = listed.Sets.FirstOrDefault(s =>
                s.Id != null &&
                string.Equals(s.Id.VolumeName, annotationDatabase, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.Id.StosGroup, stosGroup ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            if (set is null)
                return null;

            RebuildStatus status = await client
                .GetRebuildStatusAsync(new Empty(), cancellationToken: cancellationToken)
                .ResponseAsync.ConfigureAwait(false);

            _kernelRadiusNm[(annotationDatabase, stosGroup ?? string.Empty)] = set.KernelRadiusNm;

            // The host version changes on algorithm deploys even when the published field's build time does not.
            string built = set.BuiltUtc?.ToDateTime().Ticks.ToString() ?? string.Empty;
            return built + "|" + status.ServiceVersion;
        }
    }
}
