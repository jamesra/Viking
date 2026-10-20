namespace Viking.UI.Controls
{
    /// <summary>
    /// Fixed status-bar chip slots for ambient mode feedback. Owned by
    /// <see cref="SectionViewerControl"/>; WebAnnotation fills the text via
    /// <c>SetViewerStatusChip</c>.
    /// </summary>
    public enum ViewerStatusChipSlot
    {
        PenMode = 0,
        AutoPolygonize = 1,
        Segmentation = 2
    }
}
