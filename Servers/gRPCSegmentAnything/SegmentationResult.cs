using Geometry;

namespace gRPCSegmentAnything
{
    public class SegmentationResult
    {
        /// <summary>
        /// Where the origin of the image is relative to the input image
        /// </summary>
        GridVector2 ImageSpaceOrigin { get; set; }
        
        
    }
}
