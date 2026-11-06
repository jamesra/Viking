namespace Viking.Identity.Models
{
    /// <summary>
    /// A segmentation server (SegmentationServer). It reads tiles itself for the annotation contexts it is assigned,
    /// instead of asking the Viking client for them. Several segmentation services may serve the same context. The
    /// container signs in as the shared "segmentation" OAuth client.
    /// </summary>
    public class SegmentationService : ContextService
    {
    }
}
