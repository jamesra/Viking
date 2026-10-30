using System.Collections.Concurrent;
using System.Collections.Generic;
using Geometry;
using Geometry.Transforms;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Stos-only mapping for annotations. Does not select tiles — use MappingManager for draw.
    /// </summary>
    public sealed class VolumeTransformProvider : IVolumeTransformProvider
    {
        readonly Volume _volume;

        /// <summary>
        /// Volume.Transforms group name. Empty or "None" is identity (mosaic == volume).
        /// </summary>
        public string TransformName { get; }

        public VolumeTransformProvider(Volume volume, string transformName = null)
        {
            _volume = volume;
            TransformName = string.IsNullOrEmpty(transformName) ? volume?.DefaultVolumeTransform : transformName;
        }

        /// <summary>
        /// The mapping last made for each section, with the stos it wraps (null for identity). Annotation geometry is cached
        /// per transform object (LocationVolumeGeometry), so the same section must keep the same object until its stos
        /// changes; a reloaded stos is a new ITransform and gets a new mapping.
        /// </summary>
        private readonly ConcurrentDictionary<int, (ITransform Source, IVolumeToSectionTransform Mapping)> _mappings = new();

        /// <summary>
        /// Missing stos for this section returns identity so hit-test still works on the reference section. The same
        /// object is returned for a section until its stos changes.
        /// </summary>
        public IVolumeToSectionTransform GetSectionToVolumeTransform(int SectionNumber)
        {
            ITransform source = null;
            if (!string.IsNullOrEmpty(TransformName) && TransformName != "None" &&
                _volume.Transforms.TryGetValue(TransformName, out SortedList<int, ITransform> sectionTransforms))
            {
                sectionTransforms.TryGetValue(SectionNumber, out source);
            }

            if (_mappings.TryGetValue(SectionNumber, out var cached) && ReferenceEquals(cached.Source, source))
                return cached.Mapping;

            IVolumeToSectionTransform mapping = source is null
                ? new VolumeToSectionTransform($"Identity-{SectionNumber:D4}", new IdentityTransform())
                : new VolumeToSectionTransform($"{TransformName}-{SectionNumber:D4}", source);
            _mappings[SectionNumber] = (source, mapping);
            return mapping;
        }
    }
}
