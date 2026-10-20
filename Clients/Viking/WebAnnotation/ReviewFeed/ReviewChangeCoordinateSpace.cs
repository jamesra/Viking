namespace WebAnnotation.ReviewFeed
{
    /// <summary>
    /// Which annotation shape the Review card bbox/ring use. Volume matches the viewer transform;
    /// mosaic is the section pyramid (faster when volume mapping is unavailable).
    /// </summary>
    public enum ReviewChangeCoordinateSpace
    {
        Volume = 0,
        Mosaic = 1
    }
}
