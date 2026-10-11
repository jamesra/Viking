using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Viking.SectionCorrectionServiceTypes.gRPC.V1.Protos;

namespace Viking.SectionCorrectionClient
{
    /// <summary>
    /// <see cref="ISectionShimSource"/> backed by the AnnotateSectionCorrections <c>CorrectPoints</c> RPC.
    /// The shim is the mean offset of the trusted samples, so a point far from any annotation support cannot
    /// drag the whole view.
    /// </summary>
    public sealed class GrpcSectionShimSource : ISectionShimSource
    {
        readonly Func<Uri, CallInvoker> _invokerFactory;

        /// <param name="invokerFactory">
        /// Returns the call invoker for a service URL, normally from the shared gRPC channel manager so channels are
        /// reused. Called once per request.
        /// </param>
        public GrpcSectionShimSource(Func<Uri, CallInvoker> invokerFactory)
        {
            _invokerFactory = invokerFactory ?? throw new ArgumentNullException(nameof(invokerFactory));
        }

        /// <inheritdoc/>
        public async Task<ShimSample?> GetShimAsync(
            Uri serviceUrl,
            string annotationDatabase,
            string stosGroup,
            long z,
            IReadOnlyList<(double X, double Y)> pointsNm,
            CancellationToken cancellationToken)
        {
            var client = new AnnotateSectionCorrections.AnnotateSectionCorrectionsClient(_invokerFactory(serviceUrl));

            var section = new SectionPoints { Z = z };
            foreach ((double x, double y) in pointsNm)
                section.Points.Add(new VolumeXY { X = x, Y = y });

            var request = new CorrectPointsRequest
            {
                VolumeName = annotationDatabase,
                StosGroup = stosGroup ?? string.Empty,
                // Only the published residual field is wanted; the curve fit needs structures.
                Correction = CorrectionMode.Neighbor
            };
            request.Sections.Add(section);

            CorrectPointsResponse response;
            try
            {
                response = await client.CorrectPointsAsync(request, cancellationToken: cancellationToken).ResponseAsync.ConfigureAwait(false);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                return null;
            }

            CorrectedSection found = response.Sections.FirstOrDefault(s => s.Z == z && s.FoundSection);
            if (found == null)
                return null;

            List<CorrectedXY> trusted = found.Points.Where(p => p.Trusted).ToList();
            if (trusted.Count == 0)
                return null;

            return new ShimSample(trusted.Average(p => p.Dx), trusted.Average(p => p.Dy));
        }
    }
}
