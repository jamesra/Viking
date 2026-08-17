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
        /// Missing stos for this section returns identity so hit-test still works on the reference section.
        /// </summary>
        public IVolumeToSectionTransform GetSectionToVolumeTransform(int SectionNumber)
        {
            string keyBase = string.IsNullOrEmpty(TransformName) || TransformName == "None" ? "Identity" : TransformName;
            if (string.IsNullOrEmpty(TransformName) || TransformName == "None")
                return new VolumeToSectionTransform($"{keyBase}-{SectionNumber:D4}", new IdentityTransform());

            if (_volume.Transforms.TryGetValue(TransformName, out SortedList<int, ITransform> sectionTransforms) &&
                sectionTransforms.TryGetValue(SectionNumber, out ITransform transform))
            {
                return new VolumeToSectionTransform($"{TransformName}-{SectionNumber:D4}", transform);
            }

            return new VolumeToSectionTransform($"Identity-{SectionNumber:D4}", new IdentityTransform());
        }
    }
}
