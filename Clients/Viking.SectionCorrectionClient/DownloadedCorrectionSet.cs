using System;
using System.Collections.Generic;
using System.Linq;

namespace Viking.SectionCorrectionClient
{
    /// <summary>
    /// One published residual field downloaded from an AnnotateSectionCorrections host (manifest + every section lattice).
    /// Callers such as MonogameTestbed convert <see cref="EnumerateLatticeNodes"/> into AnnotationVizLib's
    /// <c>NeighborResidualField</c>; view overlays sample <see cref="Sections"/> locally.
    /// </summary>
    public sealed class DownloadedCorrectionSet
    {
        readonly IReadOnlyList<(int Z, int Gx, int Gy, double Dx, double Dy, int Votes)> _nodes;

        /// <param name="volumeName">Annotation database / correction-set volume key the service used.</param>
        /// <param name="stosGroup">Published StosGroup name.</param>
        /// <param name="pitchNm">Lattice spacing; non-positive becomes <see cref="SectionLatticeField.DefaultPitchNm"/>.</param>
        /// <param name="kernelRadiusNm">Weighted-fallback reach; non-positive becomes <see cref="SectionLatticeField.DefaultKernelRadiusNm"/>.</param>
        /// <param name="sections">Per-section lattices keyed by Z; null entries are dropped.</param>
        /// <param name="nodes">Raw lattice nodes used to rebuild an AnnotationVizLib residual field (includes vote counts).</param>
        public DownloadedCorrectionSet(
            string volumeName,
            string stosGroup,
            double pitchNm,
            double kernelRadiusNm,
            IReadOnlyDictionary<long, SectionLatticeField> sections,
            IReadOnlyList<(int Z, int Gx, int Gy, double Dx, double Dy, int Votes)> nodes)
        {
            VolumeName = volumeName ?? throw new ArgumentNullException(nameof(volumeName));
            StosGroup = stosGroup ?? throw new ArgumentNullException(nameof(stosGroup));
            PitchNm = pitchNm > 0 ? pitchNm : SectionLatticeField.DefaultPitchNm;
            KernelRadiusNm = kernelRadiusNm > 0 ? kernelRadiusNm : SectionLatticeField.DefaultKernelRadiusNm;
            Sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _nodes = nodes ?? Array.Empty<(int, int, int, double, double, int)>();
        }

        /// <summary>Correction-set volume key (Identity annotation database name).</summary>
        public string VolumeName { get; }

        /// <summary>Published StosGroup name.</summary>
        public string StosGroup { get; }

        /// <summary>Lattice spacing in nanometres.</summary>
        public double PitchNm { get; }

        /// <summary>Weighted-fallback kernel radius in nanometres.</summary>
        public double KernelRadiusNm { get; }

        /// <summary>Per-section lattices; missing Z means the service had no map for that section.</summary>
        public IReadOnlyDictionary<long, SectionLatticeField> Sections { get; }

        /// <summary>Every lattice node across all downloaded sections, for building a multi-Z residual field.</summary>
        public IEnumerable<(int Z, int Gx, int Gy, double Dx, double Dy, int Votes)> EnumerateLatticeNodes() => _nodes;

        /// <summary>True when at least one section lattice was downloaded.</summary>
        public bool HasSections => Sections.Count > 0;

        /// <summary>Z values present in <see cref="Sections"/>, ascending.</summary>
        public IEnumerable<long> SectionZs => Sections.Keys.OrderBy(z => z);
    }
}
