using System.Runtime.CompilerServices;
using Viking.gRPC.SegmentationServiceTypes.V1;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Remembers, per <see cref="SegmentationResponse"/>, the tile pyramid level the request that produced it was
    /// sent at. A <see cref="SegmentationViewportSession"/> is shared by overlapping requests and by response
    /// processing running on worker threads, and its own level field is overwritten by every new request. Mapping
    /// a mask back to world space with that field can use the next request's level. The level recorded here is
    /// fixed when the response is created and never changes.
    /// </summary>
    /// <remarks>
    /// Thread safety: <see cref="ConditionalWeakTable{TKey, TValue}"/> is thread-safe. Entries live only as long as the
    /// response object, so nothing needs to be removed.
    /// </remarks>
    internal sealed class SegmentationResultContexts
    {
        private sealed class Entry(int downsample)
        {
            public int Downsample { get; } = downsample;
        }

        private readonly ConditionalWeakTable<SegmentationResponse, Entry> entries = new();

        /// <summary>Records the level <paramref name="response"/> was requested at. A second call for the same response is ignored.</summary>
        public void Record(SegmentationResponse response, int downsample)
        {
            if (response is null)
                return;

            entries.GetValue(response, _ => new Entry(downsample));
        }

        /// <summary>
        /// Level recorded for <paramref name="response"/>, or <paramref name="fallback"/> for a response this
        /// session did not request (a test fake, or a response adopted from elsewhere).
        /// </summary>
        public int DownsampleOrDefault(SegmentationResponse response, int fallback)
            => response is not null && entries.TryGetValue(response, out Entry? entry) ? entry.Downsample : fallback;
    }
}
