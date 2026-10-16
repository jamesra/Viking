using Geometry;
using System.Collections.Generic;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// The shape that starts a segmentation, in volume space: a circle's disc, or a rectangle (a group's
    /// inscribed rectangle). Sent to the server as the foreground mask: everything inside is object.
    /// </summary>
    /// <remarks>
    /// A circle annotation is masked as the circle itself, not its inscribed square, and a polygon as its rings
    /// (exterior and holes). <see cref="Bounds"/> is the shape's axis-aligned box.
    /// </remarks>
    internal readonly struct SegmentationSeed
    {
        private SegmentationSeed(Rectangle bounds, Circle? circle, IReadOnlyList<Vector2[]>? rings = null, Vector2? interior = null, RasterMask? mask = null)
        {
            Bounds = bounds;
            Circle = circle;
            Rings = rings;
            interiorPoint = interior;
            Mask = mask;
        }

        private readonly Vector2? interiorPoint;

        /// <summary>Axis-aligned bounds of the shape.</summary>
        public Rectangle Bounds { get; }

        /// <summary>The disc when the seed is a circle annotation; null for a rectangle.</summary>
        public Circle? Circle { get; }

        /// <summary>The exterior ring followed by any holes when the seed is a polygon; null otherwise. Volume space.</summary>
        public IReadOnlyList<Vector2[]>? Rings { get; }

        /// <summary>The bitmap when the seed is an edited mask; null otherwise. Sent as the foreground mask without tracing it.</summary>
        public RasterMask? Mask { get; }

        /// <summary>
        /// A point that is inside the shape: the circle's or rectangle's centre, or the interior click a polygon
        /// was given (a concave polygon's centroid can lie outside it).
        /// </summary>
        public Vector2 Center => interiorPoint ?? Circle?.Center ?? Bounds.Center;

        /// <summary>A seed that is the disc of <paramref name="circle"/>.</summary>
        public static SegmentationSeed FromCircle(Circle circle) => new(circle.BoundingBox, circle);

        /// <summary>A seed that is <paramref name="polygon"/> with its holes; <paramref name="interior"/> must lie inside it.</summary>
        public static SegmentationSeed FromPolygon(Polygon polygon, Vector2 interior) =>
            new(polygon.BoundingBox, null, [polygon.ExteriorRing, .. polygon.InteriorRings], interior);

        /// <summary>
        /// A seed that is the set pixels of <paramref name="mask"/>, or null when the mask is empty. The bitmap goes to the
        /// server as it is: no outline is traced.
        /// </summary>
        public static SegmentationSeed? FromMask(RasterMask mask)
        {
            Vector2? interior = mask?.InteriorPoint();
            return interior is null ? null : new SegmentationSeed(mask.WorldBounds, null, null, interior, mask);
        }

        /// <summary>A rectangle is a seed filled to its edges.</summary>
        public static implicit operator SegmentationSeed(Rectangle rectangle) => new(rectangle, null);
    }
}
