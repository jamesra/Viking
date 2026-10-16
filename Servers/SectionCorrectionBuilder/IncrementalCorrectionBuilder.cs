using AnnotationVizLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnitsAndScale;
using Viking.SectionCorrection;
using Viking.VolumeModel;

namespace Viking.SectionCorrectionBuilder
{
    /// <summary>Sections rebuilt by an incremental publish.</summary>
    internal sealed class IncrementalResult
    {
        /// <summary>Sections whose fingerprint changed.</summary>
        public SortedSet<long> Dirty { get; init; }

        /// <summary>Sections recomputed: <see cref="Dirty"/> plus the sections their chain neighbours sit in.</summary>
        public SortedSet<long> Affected { get; init; }

        /// <summary>Recomputed sections that have lattice nodes. A section in <see cref="Affected"/> but not here is now empty.</summary>
        public Dictionary<long, SectionVectorField> Sections { get; init; }

        /// <summary>Structures read from the source.</summary>
        public int StructuresLoaded { get; init; }
    }

    /// <summary>
    /// Recomputes only the published sections an annotation change can alter, reading only the structures involved.
    /// The result for each recomputed section equals what a full rebuild would write for it.
    /// </summary>
    internal static class IncrementalCorrectionBuilder
    {
        /// <summary>
        /// How far from a dirty section, in sections, to look for structures whose chain crosses it. A deleted location
        /// leaves no row in its own section, so its former chain neighbours are found by proximity. Chains that skip more
        /// sections than this around a deletion are corrected by the next full rebuild.
        /// </summary>
        public const int DiscoveryPaddingSections = 16;

        /// <summary>
        /// Plans and builds an incremental update.
        /// </summary>
        /// <param name="source">Annotation source.</param>
        /// <param name="volume">Volume whose stos transforms map mosaic positions.</param>
        /// <param name="scale">Annotation database scale.</param>
        /// <param name="stosGroup">Transform group.</param>
        /// <param name="dirty">Sections whose fingerprint changed; not empty.</param>
        /// <param name="minLocations">Smallest structure that contributes residuals.</param>
        /// <param name="window">Residual window the published set was built with.</param>
        /// <param name="maxAffected">Give up when more sections than this need recomputing.</param>
        /// <param name="log">Progress sink.</param>
        /// <param name="cancellationToken">Cancels loading.</param>
        /// <returns>The rebuilt sections, or null when more than <paramref name="maxAffected"/> sections are affected
        /// and a full rebuild is the better choice.</returns>
        public static async Task<IncrementalResult> BuildAsync(
            IMorphologySource source,
            Volume volume,
            IScale scale,
            string stosGroup,
            SortedSet<long> dirty,
            int minLocations,
            MorphologyGraph.ResidualWindowOptions window,
            int maxAffected,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            Dictionary<long, List<SqlMorphologyLoader.CandidateRow>> rows = [];
            List<SqlMorphologyLoader.LinkRow> links = [];

            async Task LoadAsync(IEnumerable<long> parentIds)
            {
                List<long> missing = [.. parentIds.Where(id => !rows.ContainsKey(id))];
                if (missing.Count == 0)
                    return;

                (List<SqlMorphologyLoader.CandidateRow> candidates, List<SqlMorphologyLoader.LinkRow> structureLinks) =
                    await source.LoadStructureRowsAsync(missing, cancellationToken).ConfigureAwait(false);
                foreach (IGrouping<long, SqlMorphologyLoader.CandidateRow> group in candidates.GroupBy(c => c.ParentID))
                    rows[group.Key] = [.. group];
                foreach (long id in missing)
                    rows.TryAdd(id, []);
                links.AddRange(structureLinks);
            }

            List<MorphologyGraph> CellsOf(IEnumerable<long> parentIds, bool admittedOnly)
            {
                List<SqlMorphologyLoader.CandidateRow> candidates = [.. parentIds
                    .Distinct()
                    .Where(id => rows.TryGetValue(id, out var r) && r.Count > 0 && (!admittedOnly || r.Count >= minLocations))
                    .SelectMany(id => rows[id])];
                return SqlMorphologyLoader.FromRows(candidates, links, volume, scale, stosGroup).Cells;
            }

            // Structures with a location near a dirty section, whole, to find the sections their chains reach.
            List<long> nearDirty = await source.LoadParentIdsInRangesAsync(
                SectionRanges.Widen(dirty, DiscoveryPaddingSections), cancellationToken).ConfigureAwait(false);
            await LoadAsync(nearDirty).ConfigureAwait(false);

            SortedSet<long> affected = AffectedSectionPlanner.Affected(CellsOf(nearDirty, admittedOnly: false), dirty, window, minLocations);
            log?.Invoke($"{stosGroup}: {dirty.Count} changed sections affect {affected.Count} sections ({nearDirty.Count} structures near the changes)");
            if (affected.Count > maxAffected)
                return null;

            // Every structure that owns a location in an affected section pools into it, so each is read whole.
            List<long> crossing = await source.LoadParentIdsInRangesAsync(
                SectionRanges.ToRanges(affected), cancellationToken).ConfigureAwait(false);
            await LoadAsync(crossing).ConfigureAwait(false);

            List<MorphologyGraph> cells = CellsOf(crossing, admittedOnly: true);
            List<NeighborResidualField.ResidualSample> residuals = [.. NeighborResidualField
                .CollectResiduals(cells, window)
                .Where(r => affected.Contains(r.SectionZ))];
            log?.Invoke($"{stosGroup}: {residuals.Count} residuals in affected sections from {cells.Count} of {crossing.Count} structures");

            NeighborResidualField field = NeighborResidualField.Build(residuals);
            return new IncrementalResult
            {
                Dirty = dirty,
                Affected = affected,
                Sections = CorrectionPublisher.SectionsOf(field),
                StructuresLoaded = rows.Count
            };
        }
    }
}
