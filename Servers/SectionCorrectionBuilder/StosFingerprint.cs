using Geometry;
using Geometry.Transforms;
using System.Collections.Generic;
using Viking.SectionCorrection;
using Viking.VolumeModel;

namespace Viking.SectionCorrectionBuilder
{
    /// <summary>Identifies the stos transform that maps each section, so a re-registered section is rebuilt.</summary>
    internal static class StosFingerprint
    {
        /// <summary>
        /// Identity of the transform for one section: empty for identity mapping, otherwise its type and the modification
        /// time of the file it was loaded from. Sections the group has no stos for map through identity and are named
        /// "none", so adding a stos later changes the value.
        /// </summary>
        public static string For(Volume volume, string stosGroup, long z)
        {
            if (string.IsNullOrEmpty(stosGroup) || stosGroup == "None")
                return "";
            if (!volume.Transforms.TryGetValue(stosGroup, out SortedList<int, ITransform> perSection))
                return "missing";
            if (!perSection.TryGetValue((int)z, out ITransform transform))
                return "none";

            TransformBasicInfo info = (transform as ITransformInfo)?.Info;
            return info is null
                ? transform.GetType().Name
                : transform.GetType().Name + ":" + info.LastModified.Ticks;
        }

        /// <summary>Fills in the stos identity of <paramref name="stosGroup"/> on each fingerprint, and returns the same dictionary.</summary>
        public static Dictionary<long, SectionFingerprintDto> Apply(
            Dictionary<long, SectionFingerprintDto> fingerprints,
            Volume volume,
            string stosGroup)
        {
            foreach (KeyValuePair<long, SectionFingerprintDto> kv in fingerprints)
                kv.Value.Stos = For(volume, stosGroup, kv.Key);
            return fingerprints;
        }
    }
}
