#nullable enable
using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;

namespace Viking.VolumeModel
{
    /// <summary>
    /// The mosaic geometry a volume-space edit maps back to (<see cref="LocationVolumeGeometry.ToMosaic(LocationType, SqlGeometry, IVolumeToSectionTransform, double?)"/>):
    /// the unsmoothed shape to store in <c>MosaicShape</c> and, for an edit that set a line width, the mosaic width. When the
    /// edit cannot be mapped, <see cref="Failure"/> says why and nothing else is set; a caller then keeps the old shape and
    /// shows the reason. Apply one with <c>LocationObjExtensions.TryApply</c>.
    /// </summary>
    public sealed class MosaicGeometry
    {
        private MosaicGeometry(SqlGeometry? shape, double? width, Circle? circle, string? failure)
        {
            Shape = shape;
            Width = width;
            Circle = circle;
            Failure = failure;
        }

        /// <summary>The unsmoothed mosaic shape; null when <see cref="Succeeded"/> is false.</summary>
        public SqlGeometry? Shape { get; }

        /// <summary>The mosaic width, when the edit gave a volume width; otherwise null.</summary>
        public double? Width { get; }

        /// <summary>
        /// The exact mosaic circle when the edit was a circle, which the gRPC <c>LocationObj</c> stores as a <see cref="Geometry.Circle"/>
        /// (a circle read back from <see cref="Shape"/> takes a bounding-box radius); otherwise null.
        /// </summary>
        public Circle? Circle { get; }

        /// <summary>Why the edit could not be mapped back to mosaic space, in words for the user; null on success.</summary>
        public string? Failure { get; }

        /// <summary>True when every point mapped and <see cref="Shape"/> holds the mosaic shape.</summary>
        public bool Succeeded => Failure is null;

        internal static MosaicGeometry Mapped(SqlGeometry shape, double? width, Circle? circle = null) => new(shape, width, circle, null);

        internal static MosaicGeometry Failed(string failure) => new(null, null, null, failure);
    }

    public sealed partial class LocationVolumeGeometry
    {
        /// <summary>Fixed-point steps that solve for a mosaic radius or width whose forward mapping gives the edited one.</summary>
        private const int ScaleIterations = 12;

        /// <summary>Relative change in a solved radius or width below which the fixed point has converged.</summary>
        private const double ScaleConvergence = 1e-12;

        /// <summary>
        /// The inverse owner: maps a geometry the user edited in volume space back to the mosaic geometry that is saved. Mosaic
        /// is the truth (<c>.cursor/rules/annotation-volume-geometry.mdc</c>); a command saves what this returns and lets the
        /// forward owner (<see cref="For(WebAnnotationModel.Objects.LocationObj, IVolumeToSectionTransform)"/>) derive the volume
        /// geometry again, so it never keeps the user's volume shape.
        /// </summary>
        /// <remarks>
        /// <para>Every point maps with the checked <c>TryVolumeToSection</c>; when any point does not map the result is a
        /// <see cref="MosaicGeometry.Failure"/> and no partial shape is returned. The mapped shape must also map forward again,
        /// or it could not be drawn.</para>
        /// <para>A circle (a <c>CURVEPOLYGON</c> shape of a circle or point location) goes through
        /// <see cref="ToMosaic(Circle, IVolumeToSectionTransform)"/>. A <paramref name="volumeWidth"/> becomes the mosaic width
        /// whose forward width (mosaic width times the five-point mean scale at the shape, as <see cref="Width"/> computes it)
        /// is <paramref name="volumeWidth"/>.</para>
        /// </remarks>
        /// <param name="type">The location type the shape is saved as; it picks the width's sample radius.</param>
        /// <param name="volumeShape">The edited, unsmoothed volume shape (control points, with any holes).</param>
        /// <param name="transform">The section-to-volume transform the edit was made under.</param>
        /// <param name="volumeWidth">The edited volume width of a line or curve, or null to leave the width alone.</param>
        public static MosaicGeometry ToMosaic(LocationType type, SqlGeometry volumeShape, IVolumeToSectionTransform transform, double? volumeWidth = null)
        {
            if (volumeShape is null)
                throw new ArgumentNullException(nameof(volumeShape));
            if (transform is null)
                throw new ArgumentNullException(nameof(transform));

            if (volumeShape.IsNull || volumeShape.STIsEmpty().IsTrue)
                return MosaicGeometry.Failed("The edited shape is empty.");

            if (IsCircleType(type) && volumeShape.GeometryType() == SupportedGeometryType.CURVEPOLYGON)
                return ToMosaic(volumeShape.ToCircle(), transform);

            SqlGeometry? mosaic = transform.TryMapShapeVolumeToSection(volumeShape);
            if (mosaic is null)
                return MosaicGeometry.Failed(Unmapped(AllPoints(volumeShape), transform, "edited shape"));

            if (transform.TryMapShapeSectionToVolume(mosaic) is null)
                return MosaicGeometry.Failed("The edited shape maps into the section but not back onto the volume, so it could not be drawn where it was edited.");

            double? width = volumeWidth is double w ? MosaicWidth(type, mosaic, w, transform) : null;
            return MosaicGeometry.Mapped(mosaic, width);
        }

        /// <summary>
        /// <see cref="ToMosaic(LocationType, SqlGeometry, IVolumeToSectionTransform, double?)"/> for a line, curve or polygon
        /// given as its edited volume control points (and holes).
        /// </summary>
        public static MosaicGeometry ToMosaic(LocationType type, IReadOnlyList<Vector2> volumeControlPoints, IVolumeToSectionTransform transform,
                                              double? volumeWidth = null, IReadOnlyCollection<Vector2[]>? volumeHoles = null)
        {
            if (volumeControlPoints is null)
                throw new ArgumentNullException(nameof(volumeControlPoints));
            if (transform is null)
                throw new ArgumentNullException(nameof(transform));

            if (!TryMapPointsToMosaic(volumeControlPoints, transform, out Vector2[] mosaicPoints, out string? failure))
                return MosaicGeometry.Failed(failure!);

            List<Vector2[]>? mosaicHoles = null;
            if (volumeHoles is not null && volumeHoles.Count > 0)
            {
                mosaicHoles = new List<Vector2[]>(volumeHoles.Count);
                foreach (Vector2[] hole in volumeHoles)
                {
                    if (!TryMapPointsToMosaic(hole, transform, out Vector2[] mosaicHole, out failure))
                        return MosaicGeometry.Failed(failure!);

                    mosaicHoles.Add(mosaicHole);
                }
            }

            SqlGeometry mosaic = type.GetShape(mosaicPoints, mosaicHoles);
            if (transform.TryMapShapeSectionToVolume(mosaic) is null)
                return MosaicGeometry.Failed("The edited shape maps into the section but not back onto the volume, so it could not be drawn where it was edited.");

            double? width = volumeWidth is double w ? MosaicWidth(type, mosaic, w, transform) : null;
            return MosaicGeometry.Mapped(mosaic, width);
        }

        /// <summary>
        /// <see cref="ToMosaic(LocationType, IReadOnlyList{Vector2}, IVolumeToSectionTransform, double?, IReadOnlyCollection{Vector2[]})"/>
        /// for an edited volume polygon with holes, saved as <paramref name="type"/> (a polygon or curve polygon).
        /// </summary>
        public static MosaicGeometry ToMosaic(LocationType type, Polygon volumePolygon, IVolumeToSectionTransform transform)
        {
            if (volumePolygon is null)
                throw new ArgumentNullException(nameof(volumePolygon));

            return ToMosaic(type, volumePolygon.ExteriorRing, transform, null, volumePolygon.HasInteriorRings ? volumePolygon.InteriorRings.ToList() : null);
        }

        /// <summary>
        /// A circle edited in volume space mapped back to mosaic space. The centre maps as a point. The mosaic radius starts at
        /// the inverse five-point mean (<see cref="CircleMapping"/>) and is refined until the forward five-point mean of the
        /// mosaic circle (what <see cref="For(WebAnnotationModel.Objects.LocationObj, IVolumeToSectionTransform)"/> draws) gives the
        /// edited volume radius: the inverse and forward five-point estimates differ under shear or unequal axis scales, so a
        /// one-step inverse would grow or shrink the circle on every save.
        /// </summary>
        /// <returns>The mosaic circle as a <c>CURVEPOLYGON</c>, or a failure when the centre or the rim does not map.</returns>
        public static MosaicGeometry ToMosaic(Circle volumeCircle, IVolumeToSectionTransform transform)
        {
            if (transform is null)
                throw new ArgumentNullException(nameof(transform));

            if (!(volumeCircle.Radius > 0) || double.IsInfinity(volumeCircle.Radius))
                return MosaicGeometry.Failed("The circle has no radius.");

            if (!transform.TryVolumeToSection(volumeCircle.Center, out Vector2 center))
                return MosaicGeometry.Failed($"The circle's centre {Describe(volumeCircle.Center)} lies outside the region this section's transform maps.");

            double radius = transform.TryMapShapeVolumeToSection(volumeCircle) is Circle inverse ? inverse.Radius : volumeCircle.Radius;
            for (int i = 0; i < ScaleIterations; i++)
            {
                if (transform.TryMapShapeSectionToVolume(new Circle(center, radius)) is not Circle forward)
                    return MosaicGeometry.Failed($"The rim of the circle at {Describe(volumeCircle.Center)} lies outside the region this section's transform maps.");

                double next = radius * (volumeCircle.Radius / forward.Radius);
                bool converged = Math.Abs(next - radius) <= ScaleConvergence * radius;
                radius = next;
                if (converged)
                    break;
            }

            return MosaicGeometry.Mapped(SqlGeometryUtils.Extensions.ToCircle(center.X, center.Y, 0, radius), null, new Circle(center, radius));
        }

        /// <summary>
        /// Maps edited volume points (a ring, a pen stroke's new points, a dragged handle) to mosaic space, all or none. The
        /// point-level inverse the other overloads and the splice-preserving cuts (which keep stored mosaic points and map only
        /// new ones) share.
        /// </summary>
        /// <param name="failure">Why the points could not be mapped, in words for the user; null on success.</param>
        /// <returns>False, with <paramref name="mosaicPoints"/> empty, when any point does not map.</returns>
        public static bool TryMapPointsToMosaic(IReadOnlyList<Vector2> volumePoints, IVolumeToSectionTransform transform, out Vector2[] mosaicPoints, out string? failure)
        {
            if (volumePoints is null)
                throw new ArgumentNullException(nameof(volumePoints));
            if (transform is null)
                throw new ArgumentNullException(nameof(transform));

            Vector2[] points = volumePoints as Vector2[] ?? [.. volumePoints];
            bool[] mapped = transform.TryVolumeToSection(points, out Vector2[] section);
            if (mapped.Any(ok => !ok))
            {
                mosaicPoints = [];
                failure = Unmapped(points, transform, "edited shape");
                return false;
            }

            mosaicPoints = section;
            failure = null;
            return true;
        }

        /// <summary>
        /// The mosaic width whose forward width is <paramref name="volumeWidth"/>: volume width divided by the five-point mean
        /// scale the forward owner samples at the shape's centre. For lines and open curves that sample radius is half the
        /// width itself, so the width is found by a short fixed-point iteration (one step for an affine transform).
        /// </summary>
        private static double MosaicWidth(LocationType type, SqlGeometry mosaic, double volumeWidth, IVolumeToSectionTransform transform)
        {
            Vector2 position = PositionOf(mosaic);
            double width = volumeWidth;
            for (int i = 0; i < ScaleIterations; i++)
            {
                double sampleRadius = IsLineType(type) ? width / 2.0 : RadiusOf(mosaic);
                double next = volumeWidth / LocalScale(transform, position, sampleRadius);
                bool converged = Math.Abs(next - width) <= ScaleConvergence * Math.Max(1, width);
                width = next;
                if (converged)
                    break;
            }

            return width;
        }

        /// <summary>
        /// The radius the gRPC <c>LocationObj.Radius</c> computes from a mosaic shape (<c>CalculateRadius</c>: the length of a
        /// line, the square root of a polygon's area, 8 for a point), which the forward owner samples a closed curve's or
        /// polygon's scale at. Legacy's WCF <c>LocationObj</c> computes it differently; keep this in step with dev's.
        /// </summary>
        private static double RadiusOf(SqlGeometry mosaic) => mosaic.STDimension().Value switch
        {
            0 => 8,
            1 => mosaic.STLength().Value,
            _ => Math.Sqrt(mosaic.STArea().Value),
        };

        /// <summary>
        /// The mosaic position the gRPC <c>LocationObj.Position</c> computes for a shape (its <see cref="ICentroid"/>, else the
        /// centre of its bounding box), where the forward owner samples the local scale. Keep this in step with
        /// <c>LocationObj.CenterOfLocationShape</c>.
        /// </summary>
        private static Vector2 PositionOf(SqlGeometry mosaic)
        {
            IShape2D shape = mosaic.ToShape2D();
            return shape is ICentroid c ? c.Centroid.ToVector2() : shape.BoundingBox.Center;
        }

        private static Vector2[] AllPoints(SqlGeometry shape)
        {
            Vector2[] exterior = shape.ToPoints();
            return shape.HasInteriorRings() ? [.. exterior, .. shape.InteriorRingPoints().SelectMany(r => r)] : exterior;
        }

        private static string Unmapped(Vector2[] volumePoints, IVolumeToSectionTransform transform, string what)
        {
            bool[] mapped = transform.TryVolumeToSection(volumePoints, out _);
            int failed = mapped.Count(ok => !ok);
            if (failed == 0)
                return $"The {what} could not be mapped into the section.";

            int first = Array.IndexOf(mapped, false);
            string where = first >= 0 ? $", the first at {Describe(volumePoints[first])}," : string.Empty;
            return $"{failed} of {volumePoints.Length} points of the {what}{where} lie outside the region this section's transform maps, so it cannot be saved in section coordinates.";
        }

        private static string Describe(Vector2 p) => $"({p.X:0}, {p.Y:0})";
    }
}
