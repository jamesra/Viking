using Geometry;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// The shape that starts a segmentation, in volume space: a circle's disc, or a rectangle (a group's
    /// inscribed rectangle). Sent to the server as the foreground mask: everything inside is object.
    /// </summary>
    /// <remarks>
    /// A circle annotation is masked as the circle itself, not its inscribed square. <see cref="Bounds"/> is the
    /// shape's axis-aligned box and is what callers use to find where it is.
    /// </remarks>
    internal readonly struct SegmentationSeed
    {
        private SegmentationSeed(Rectangle bounds, Circle? circle)
        {
            Bounds = bounds;
            Circle = circle;
        }

        /// <summary>Axis-aligned bounds of the shape.</summary>
        public Rectangle Bounds { get; }

        /// <summary>The disc when the seed is a circle annotation; null for a rectangle.</summary>
        public Circle? Circle { get; }

        /// <summary>Centre of the circle, or of the rectangle. Always inside the shape.</summary>
        public Vector2 Center => Circle?.Center ?? Bounds.Center;

        /// <summary>A seed that is the disc of <paramref name="circle"/>.</summary>
        public static SegmentationSeed FromCircle(Circle circle) => new(circle.BoundingBox, circle);

        /// <summary>A rectangle is a seed filled to its edges.</summary>
        public static implicit operator SegmentationSeed(Rectangle rectangle) => new(rectangle, null);
    }
}
