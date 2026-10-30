using System.Collections.Concurrent;
using System.Collections.Generic;
using Geometry;
using Geometry.Transforms;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Stos-only mapping for annotations. Does not select tiles — use MappingManager for draw.
    /// </summary>
    /// <remarks>
    /// Jotunn draws annotations through one of these: <see cref="TransformName"/> follows the volume transform the tiles are
    /// drawn with, and <see cref="GetDefaultSectionToVolumeTransform"/> gives the volume's default transform, the basis every
    /// save writes the stored volume cache in (<c>VolumeCache</c>, <c>.cursor/rules/annotation-volume-geometry.mdc</c>).
    /// </remarks>
    public sealed class VolumeTransformProvider : IVolumeTransformProvider, IDefaultVolumeTransformProvider
    {
        readonly Volume _volume;

        volatile string _transformName;

        /// <summary>
        /// Volume.Transforms group name the annotations are drawn in: the active volume transform. Empty or "None" is identity
        /// (mosaic == volume); setting null or empty selects the volume's default. A host that changes the transform on screen
        /// sets it and rebuilds its views (<c>AnnotationScene.SetVolumeTransform</c>).
        /// </summary>
        public string TransformName
        {
            get => _transformName;
            set => _transformName = string.IsNullOrEmpty(value) ? _volume?.DefaultVolumeTransform : value;
        }

        public VolumeTransformProvider(Volume volume, string transformName = null)
        {
            _volume = volume;
            TransformName = transformName;
        }

        /// <summary>
        /// The mapping last made for each (transform group, section), with the stos it wraps (null for identity). Annotation
        /// geometry is cached per transform object (LocationVolumeGeometry), so the same section must keep the same object until
        /// its stos changes; a reloaded stos is a new ITransform and gets a new mapping.
        /// </summary>
        private readonly ConcurrentDictionary<(string Group, int Section), (ITransform Source, IVolumeToSectionTransform Mapping)> _mappings = new();

        /// <summary>
        /// The section's mapping under <see cref="TransformName"/>. Missing stos for this section returns identity so hit-test
        /// still works on the reference section. The same object is returned for a section until its stos changes.
        /// </summary>
        public IVolumeToSectionTransform GetSectionToVolumeTransform(int SectionNumber) => GetMapping(TransformName, SectionNumber);

        /// <summary>
        /// The section's mapping under the volume's default transform, whatever <see cref="TransformName"/> is. Identity for a
        /// volume without transforms or a section the default has no entry for (the reference section); null, no basis, when
        /// the volume names a default transform that did not load while others did, or when the section's own .stos in it
        /// failed to load (<see cref="Volume.StosLoadFailed"/>: drawn by identity, but identity is not its basis).
        /// </summary>
        public IVolumeToSectionTransform GetDefaultSectionToVolumeTransform(int SectionNumber)
        {
            string defaultTransform = _volume?.DefaultVolumeTransform;
            if (!string.IsNullOrEmpty(defaultTransform) && defaultTransform != "None" &&
                _volume.Transforms.Count > 0 && !_volume.Transforms.ContainsKey(defaultTransform))
            {
                return null;
            }

            if (_volume?.StosLoadFailed(defaultTransform, SectionNumber) == true)
                return null;

            return GetMapping(defaultTransform, SectionNumber);
        }

        private IVolumeToSectionTransform GetMapping(string transformName, int SectionNumber)
        {
            ITransform source = null;
            if (!string.IsNullOrEmpty(transformName) && transformName != "None" && _volume is not null &&
                _volume.Transforms.TryGetValue(transformName, out SortedList<int, ITransform> sectionTransforms))
            {
                sectionTransforms.TryGetValue(SectionNumber, out source);
            }

            (string, int) key = (source is null ? string.Empty : transformName, SectionNumber);
            if (_mappings.TryGetValue(key, out var cached) && ReferenceEquals(cached.Source, source))
                return cached.Mapping;

            IVolumeToSectionTransform mapping = source is null
                ? new VolumeToSectionTransform($"Identity-{SectionNumber:D4}", new IdentityTransform())
                : new VolumeToSectionTransform($"{transformName}-{SectionNumber:D4}", source);
            _mappings[key] = (source, mapping);
            return mapping;
        }
    }
}
