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
    /// <para><b>Missing transform.</b> A volume without transforms, or a section the volume does not align (the reference
    /// section, a NonStosSectionNumbers section: no entry in the default transform), is mapped by identity, as the viewer and
    /// <c>MappingManager</c> place such a section (<see cref="IDefaultVolumeTransformProvider"/>). A section whose default
    /// .stos failed to load, or a default transform that did not load at all while others did, has <b>no basis</b>:
    /// <see cref="Basis"/> throws, the save is refused and the refresh stops at that section. Neither identity nor the
    /// transform on screen is written instead; the first would store section positions for an aligned section after a
    /// transient server failure, the second a cache in another transform's space.</para>
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
        /// <exception cref="ArgumentException">The open volume has no default transform for the section (<see cref="NoBasisMessage"/>):
        /// its default transform, or the section's .stos in it, did not load. The transform on screen is never used instead.</exception>
        public static IVolumeToSectionTransform Basis(LocationObj location, IVolumeToSectionTransform transform)
        {
            if (location is null)
                throw new ArgumentNullException(nameof(location));
            if (transform is null)
                throw new ArgumentNullException(nameof(transform));

            IDefaultVolumeTransformProvider? volume = DefaultTransforms();
            if (volume is null)
                return transform;

            return volume.GetDefaultSectionToVolumeTransform(location.Section) ?? throw new ArgumentException(NoBasisMessage);
        }

        /// <summary>Why a save is refused when the open volume's default transform is missing for the location's section.</summary>
        public const string NoBasisMessage =
            "The volume's default alignment for this section is not loaded, so the annotation's stored volume position cannot be computed and it cannot be saved. Reopen the volume once the alignment loads.";

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
