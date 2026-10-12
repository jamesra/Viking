using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Viking.SectionCorrectionServiceTypes.gRPC.V1.Protos;

namespace Viking.SectionCorrectionClient
{
    /// <summary>
    /// <see cref="ISectionShimSource"/> backed by the AnnotateSectionCorrections <c>GetSectionCorrection</c>,
    /// <c>GetCorrectionManifest</c> and <c>GetRebuildStatus</c> RPCs.
    /// </summary>
    public sealed class GrpcSectionShimSource : ISectionShimSource
    {
        readonly Func<Uri, CallInvoker> _invokerFactory;

        // GetSectionCorrection does not carry the kernel radius; it comes from the manifest read by GetVersionAsync.
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
                VolumeName = annotationDatabase,
                StosGroup = stosGroup ?? string.Empty,
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
                    response.Nodes.Select(n => (n.Gx, n.Gy, n.Dx, n.Dy)));
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
            try
            {
                CorrectionManifest manifest = await client
                    .GetCorrectionManifestAsync(new GetCorrectionManifestRequest
                    {
                        VolumeName = annotationDatabase,
                        StosGroup = stosGroup ?? string.Empty
                    }, cancellationToken: cancellationToken).ResponseAsync.ConfigureAwait(false);
                RebuildStatus status = await client
                    .GetRebuildStatusAsync(new GetRebuildStatusRequest(), cancellationToken: cancellationToken)
                    .ResponseAsync.ConfigureAwait(false);

                _kernelRadiusNm[(annotationDatabase, stosGroup ?? string.Empty)] = manifest.Provenance?.KernelRadiusNm ?? 0;

                // The host version changes on algorithm deploys even when the published field's build time does not.
                string built = manifest.Provenance?.BuiltUtc?.ToDateTime().Ticks.ToString() ?? string.Empty;
                return built + "|" + status.ServiceVersion;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                return null;
            }
        }
    }
}
