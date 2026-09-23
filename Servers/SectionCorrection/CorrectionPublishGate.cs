using System;

namespace Viking.SectionCorrection
{
    /// <summary>
    /// Watermark skip used by SectionCorrectionBuilder and the multi-volume rebuild host.
    /// Skip when a published set already covers the current annotation watermark, unless force is set.
    /// </summary>
    public static class CorrectionPublishGate
    {
        public static bool ShouldSkip(DateTime? existingWatermark, DateTime currentWatermark, bool force)
        {
            if (force)
                return false;
            if (!existingWatermark.HasValue)
                return false;
            return existingWatermark.Value >= currentWatermark;
        }
    }
}
