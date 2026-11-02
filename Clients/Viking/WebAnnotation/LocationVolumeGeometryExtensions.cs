using Geometry;
using SqlGeometryUtils;
using System;
using Viking.VolumeModel;
using WebAnnotationModel;

namespace WebAnnotation
{
    /// <summary>
    /// Gives code that holds a location but no transform (labels, flag feedback, link previews, navigation) the
    /// location's <see cref="LocationVolumeGeometry"/> under the open volume, so it never maps a point or reads the stored
    /// volume columns itself.
    /// </summary>
    internal static class LocationVolumeGeometryExtensions
    {
        /// <summary>The open volume's transforms; null when no volume is open. Tests replace it.</summary>
        internal static Func<IVolumeTransformProvider?> Provider = () => Viking.UI.State.volume;

        /// <summary>
        /// The location's volume geometry under the open volume's transform for its section, or null when no volume is
        /// open or the location cannot be mapped (see the mapping failure policy on <see cref="LocationVolumeGeometry"/>).
        /// </summary>
        public static LocationVolumeGeometry? VolumeGeometry(this LocationObj location)
        {
            IVolumeTransformProvider? volume = Provider();
            if (volume is null || location?.MosaicShape is null)
                return null;

            try
            {
                return LocationVolumeGeometry.For(location, volume);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Where labels and markers for the location go in volume space: <see cref="LocationVolumeGeometry.Center"/>, or the
        /// mosaic position when there is no volume geometry (no volume open, which draws mosaic as volume).
        /// </summary>
        public static Vector2 VolumeCenter(this LocationObj location) => location.VolumeGeometry()?.Center ?? location.Position;

        /// <summary>The location's volume radius (<see cref="LocationVolumeGeometry.Radius"/>), or its mosaic radius without one.</summary>
        public static double VolumeRadius(this LocationObj location) => location.VolumeGeometry()?.Radius ?? location.Radius;

        /// <summary>
        /// The width of the location's drawn volume shape, or of its mosaic shape without volume geometry. Zoom to fit uses this,
        /// because the viewer is in volume space and a section can map into the volume at a scale other than 1.
        /// </summary>
        public static double VolumeWidth(this LocationObj location) =>
            location.VolumeGeometry()?.Shape is { IsNull: false } shape
                ? shape.BoundingBox().Width
                : location.MosaicShape.BoundingBox().Width;
    }
}
