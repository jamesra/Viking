namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Why an interactive Segment attempt produced no overlay. Stored on
    /// <see cref="SegmentationViewportSession"/> for the command to show as transient status.
    /// </summary>
    internal enum SegmentationSkipKind
    {
        None = 0,
        ZoomTooCoarse,
        UploadFailed,
        TilesUnavailable,
        EmptyMask,
        NoClient,
        Cancelled,
        Error
    }
}
