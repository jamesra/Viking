using Geometry;

namespace Viking.VolumeModel
{
    public interface ITile
    {
        ITransform Transform { get; }
        Rectangle SourceBounds { get; }
        Rectangle TargetBounds { get; }

    }

    /// <summary>
    /// A tile is the combination of a transform and an image.
    /// </summary>
    public class Tile : ITile
    {
        public ITransform Transform { get; }
        public Rectangle SourceBounds { get; }
        public Rectangle TargetBounds { get; }
    }
}