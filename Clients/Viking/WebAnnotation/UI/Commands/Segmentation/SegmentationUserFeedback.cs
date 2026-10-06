using Viking.UI.Controls;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Non-modal Segment skip messages for the viewer status bar. Interactive
    /// <see cref="SegmentationCommand"/> calls this; quiet deferrals (coalesce busy, upload in
    /// progress) stay silent because a follow-up will run.
    /// </summary>
    internal static class SegmentationUserFeedback
    {
        /// <summary>
        /// Shows a user-facing reason when <paramref name="kind"/> is not <see cref="SegmentationSkipKind.None"/>
        /// or <see cref="SegmentationSkipKind.Cancelled"/>.
        /// </summary>
        public static void NotifySkip(SectionViewerControl? parent, SegmentationSkipKind kind, string? detail = null)
        {
            if (parent is null || parent.IsDisposed)
                return;

            string? message = Format(kind, detail);
            if (message is null)
                return;

            SegmentationDiag.Log($"UserFeedback: {message}");
            parent.ShowTransientStatus(message);
        }

        /// <summary>Empty mask or polygonize produced no shapes after a non-null response.</summary>
        public static void NotifyEmptyMask(SectionViewerControl? parent)
            => NotifySkip(parent, SegmentationSkipKind.EmptyMask);

        /// <summary>
        /// Skip kind to show when the session returned no response. A session that gave up records its
        /// reason, so <see cref="SegmentationSkipKind.None"/> here means the reason was lost, not that the
        /// mask was empty: an empty mask arrives as a non-null response with no segments and is reported there.
        /// </summary>
        internal static SegmentationSkipKind KindForMissingResponse(SegmentationSkipKind recorded)
            => recorded == SegmentationSkipKind.None ? SegmentationSkipKind.Error : recorded;

        /// <summary>Maps skip kind to status text. Null means do not show UI.</summary>
        internal static string? Format(SegmentationSkipKind kind, string? detail)
        {
            double maxDs = Global.AnnotationSettings.AutoPolygonizeMaxDownsample;
            return kind switch
            {
                SegmentationSkipKind.ZoomTooCoarse =>
                    $"Segment skipped: zoom in (max downsample {maxDs:0.#}).",
                SegmentationSkipKind.UploadFailed =>
                    "Segment skipped: image upload failed.",
                SegmentationSkipKind.TilesUnavailable =>
                    "Segment skipped: mosaic tiles were not ready.",
                SegmentationSkipKind.EmptyMask =>
                    "Segment returned an empty mask — try another point.",
                SegmentationSkipKind.NoClient =>
                    "Segment skipped: no segmentation service is available.",
                SegmentationSkipKind.Error =>
                    string.IsNullOrWhiteSpace(detail)
                        ? "Segment failed."
                        : $"Segment failed: {detail}",
                _ => null
            };
        }
    }
}
