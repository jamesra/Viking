using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Viking.SectionCorrectionClient
{
    /// <summary>
    /// A translation, in nanometres, to apply to the image of one section near the sampled points.
    /// </summary>
    public readonly struct ShimSample
    {
        public ShimSample(double dxNm, double dyNm)
        {
            DxNm = dxNm;
            DyNm = dyNm;
        }

        public double DxNm { get; }

        public double DyNm { get; }
    }

    /// <summary>
    /// Fetches correction data from a correction service. Implementations must be safe to call from any thread.
    /// </summary>
    public interface ISectionShimSource
    {
        /// <summary>
        /// Downloads the whole correction lattice of one section.
        /// </summary>
        /// <param name="serviceUrl">Correction service endpoint Identity sent for the annotation context.</param>
        /// <param name="annotationDatabase">Annotation database; with <paramref name="stosGroup"/> it keys the correction set.</param>
        /// <param name="stosGroup">Active transform (StosGroup) name.</param>
        /// <param name="z">Section number.</param>
        /// <param name="cancellationToken">Cancelled when the service is disposed or reconfigured.</param>
        /// <returns>The lattice, or null when the service has no correction for the section or no such set.</returns>
        Task<SectionLatticeField> GetLatticeAsync(
            Uri serviceUrl,
            string annotationDatabase,
            string stosGroup,
            long z,
            CancellationToken cancellationToken);

        /// <summary>
        /// Reads an opaque token that changes whenever the service publishes a new correction set (a rebuild or a
        /// deploy that changes the algorithm). Cached lattices are valid only while the token is unchanged.
        /// </summary>
        /// <param name="serviceUrl">Correction service endpoint.</param>
        /// <param name="annotationDatabase">Annotation database name.</param>
        /// <param name="stosGroup">Active transform (StosGroup) name.</param>
        /// <param name="cancellationToken">Cancelled when the service is disposed or reconfigured.</param>
        /// <returns>The token, or null when the service has no such set.</returns>
        Task<string> GetVersionAsync(
            Uri serviceUrl,
            string annotationDatabase,
            string stosGroup,
            CancellationToken cancellationToken);
    }
}
