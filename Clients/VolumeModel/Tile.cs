using Geometry;

namespace Viking.VolumeModel
{
    public interface ITile
    {
        ITransform Transform { get; }
        GridRectangle SourceBounds { get; }
        GridRectangle TargetBounds { get; }

    }

    /// <summary>
    /// A tile is the combination of a transform and an image.
    /// </summary>
    public class Tile : ITile
    {
        public ITransform Transform { get; }
        public GridRectangle SourceBounds { get; }
        public GridRectangle TargetBounds { get; }
    }
}