using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Viking.SectionCorrection;

namespace Viking.SectionCorrectionBuilder
{
    /// <summary>
    /// The annotation query that decides whether a published set is out of date, implemented over SQL and over OData.
    /// The rebuild itself keeps its own loaders.
    /// </summary>
    internal interface ISectionFingerprintSource
    {
        /// <summary>One fingerprint per Z that has location rows. Stos identity is not set here.</summary>
        Task<Dictionary<long, SectionFingerprintDto>> LoadFingerprintsAsync(CancellationToken cancellationToken);
    }
}
