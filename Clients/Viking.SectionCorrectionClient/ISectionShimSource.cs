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
    /// Asks a correction service how far the image of a section should be shifted around some points.
    /// Implementations must be safe to call from any thread.
    /// </summary>
    public interface ISectionShimSource
    {
        /// <summary>
        /// Samples the correction field at <paramref name="pointsNm"/> and reduces it to one translation.
        /// </summary>
        /// <param name="serviceUrl">Correction service endpoint Identity sent for the annotation context.</param>
        /// <param name="annotationDatabase">Annotation database; with <paramref name="stosGroup"/> it keys the correction set.</param>
        /// <param name="stosGroup">Active transform (StosGroup) name.</param>
        /// <param name="z">Section number.</param>
        /// <param name="pointsNm">Points in volume space, in nanometres.</param>
        /// <param name="cancellationToken">Cancelled when a newer request supersedes this one.</param>
        /// <returns>The translation, or null when the service has no usable correction here (unknown set, no map for the section, no trusted support).</returns>
        Task<ShimSample?> GetShimAsync(
            Uri serviceUrl,
            string annotationDatabase,
            string stosGroup,
            long z,
            IReadOnlyList<(double X, double Y)> pointsNm,
            CancellationToken cancellationToken);
    }
}
