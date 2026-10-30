#nullable enable
using Microsoft.SqlServer.Types;
using System;
using WebAnnotationModel;

namespace Viking.VolumeModel
{
    /// <summary>
    /// The stored volume cache of a location: <c>VolumeShape</c>, and the <c>VolumeX</c>/<c>VolumeY</c> the server computes
    /// from it. Mosaic is the truth; the cache is always the forward owner's geometry (<see cref="LocationVolumeGeometry.CacheShape"/>)
    /// under the volume's <b>default</b> transform, whatever transform is shown on screen, so server queries, OData and export
    /// see one consistent space. Every save path (<see cref="LocationObjExtensions.SetShapeFromGeometryInSection"/>,
    /// <see cref="LocationObjExtensions.TryApply"/>) and the refresh after a .stos change (<c>VolumePositionUpdateJob</c>,
    /// VikingAU) write the cache through <see cref="Shape"/>. See <c>.cursor/rules/annotation-volume-geometry.mdc</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>Missing transform.</b> A volume without transforms, or a section the default transform has no entry for
    /// (the reference section, or a .stos that did not load), is mapped by identity, as the viewer and <c>MappingManager</c>
    /// place such a section (<see cref="IDefaultVolumeTransformProvider"/>).</para>
    /// <para><b>Mapping failure.</b> A location the default transform cannot map exactly (a point outside the .stos hull)
    /// gets the forward owner's one mapping-failure policy (extrapolated points; see <see cref="LocationVolumeGeometry"/>), not
    /// no cache: the WCF server writes a circle of the mosaic radius at the old <c>VolumePosition</c> when no
    /// <c>VolumeShape</c> is sent, which is wrong for every other type. When even the extrapolation fails, <see cref="Shape"/>
    /// throws <see cref="ArgumentException"/>: a save is refused and the refresh skips the location.</para>
    /// </remarks>
    public static class VolumeCache
    {
        /// <summary>
        /// The open volume's default transforms, or null when no volume is open. The viewer sets it when its annotation
        /// overlay loads; unit tests replace it.
        /// </summary>
        public static Func<IDefaultVolumeTransformProvider?> DefaultTransforms { get; set; } = () => null;

        /// <summary>
        /// The transform the cache of <paramref name="location"/> is written in: the open volume's default transform for its
        /// section. Without an open volume (VikingAU, unit tests) it is <paramref name="transform"/>, the transform the caller
        /// passed, which such a caller must make the default one.
        /// </summary>
        public static IVolumeToSectionTransform Basis(LocationObj location, IVolumeToSectionTransform transform)
        {
            if (location is null)
                throw new ArgumentNullException(nameof(location));
            if (transform is null)
                throw new ArgumentNullException(nameof(transform));

            return DefaultTransforms()?.GetDefaultSectionToVolumeTransform(location.Section) ?? transform;
        }

        /// <summary>
        /// The <c>VolumeShape</c> to store for <paramref name="location"/>'s current mosaic shape:
        /// <see cref="LocationVolumeGeometry.CacheShape"/> under <paramref name="defaultTransform"/>, which must be the
        /// volume's default transform for the location's section (<see cref="Basis"/>).
        /// </summary>
        /// <exception cref="ArgumentException">Even the owner's mapping-failure policy cannot place the location.</exception>
        public static SqlGeometry Shape(LocationObj location, IVolumeToSectionTransform defaultTransform) =>
            LocationVolumeGeometry.For(location, defaultTransform).CacheShape;
    }
}
