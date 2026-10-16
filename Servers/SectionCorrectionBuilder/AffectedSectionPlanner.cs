using AnnotationVizLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Viking.SectionCorrectionBuilder
{
    /// <summary>
    /// Decides which published sections an annotation change can alter. A residual depends only on its own structure's
    /// process chain within the residual window, so a change at one location reaches the sections of the chain positions
    /// near it; those are the sections of its neighbours in the chain, which are further away in Z where the chain skips
    /// sections.
    /// </summary>
    /// <remarks>
    /// The answer may be larger than necessary but is never smaller than what a full rebuild would change, as long as every
    /// structure that owns a changed location, or has a chain neighbour across a deleted one, is among the cells given.
    /// </remarks>
    internal static class AffectedSectionPlanner
    {
        /// <summary>How many chain positions away from a changed position can still change, with slack for edge rules.</summary>
        public static int Radius(MorphologyGraph.ResidualWindowOptions window) =>
            Math.Max(window.MaxBoth, window.MaxOne) + 2;

        /// <summary>Section number of a node, as <see cref="NeighborResidualField.CollectResiduals"/> assigns residuals.</summary>
        public static long SectionOf(MorphologyNode node) => (long)Math.Round(node.UnscaledZ);

        /// <summary>
        /// Sections whose published lattice may change.
        /// </summary>
        /// <param name="cells">Structures that have a location near a dirty section, with all of their locations.</param>
        /// <param name="dirty">Sections whose fingerprint changed.</param>
        /// <param name="window">Residual window the build uses.</param>
        /// <param name="minLocations">Smallest structure that contributes; structures near that size are taken whole because
        /// one more or one fewer location adds or removes the whole structure.</param>
        /// <returns>The dirty sections plus every section whose residual samples may differ.</returns>
        public static SortedSet<long> Affected(
            IEnumerable<MorphologyGraph> cells,
            SortedSet<long> dirty,
            MorphologyGraph.ResidualWindowOptions window,
            int minLocations)
        {
            SortedSet<long> affected = new(dirty);
            int radius = Radius(window);
            int small = Math.Max(2 * radius + 2, minLocations + 1);

            foreach (MorphologyGraph cell in cells)
            {
                if (cell is null || cell.Nodes.Count == 0)
                    continue;

                if (cell.Nodes.Count <= small)
                {
                    foreach (MorphologyNode node in cell.Nodes.Values)
                        affected.Add(SectionOf(node));
                    continue;
                }

                foreach (ulong[] process in cell.Processes())
                    MarkProcess(cell, process, dirty, radius, affected);
            }

            return affected;
        }

        static void MarkProcess(
            MorphologyGraph cell,
            ulong[] process,
            SortedSet<long> dirty,
            int radius,
            SortedSet<long> affected)
        {
            int n = process.Length;
            long[] z = new long[n];
            for (int i = 0; i < n; i++)
                z[i] = SectionOf(cell.Nodes[process[i]]);

            bool[] hot = new bool[n];
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                if (dirty.Contains(z[i]))
                {
                    hot[i] = true;
                    any = true;
                }

                // A dirty section strictly between two chain neighbours is where a location was deleted or never was.
                if (i + 1 < n && z[i + 1] - z[i] > 1 && dirty.GetViewBetween(z[i] + 1, z[i + 1] - 1).Count > 0)
                {
                    hot[i] = true;
                    hot[i + 1] = true;
                    any = true;
                }
            }

            if (!any)
                return;

            for (int i = 0; i < n; i++)
            {
                if (!hot[i])
                    continue;
                int lo = Math.Max(0, i - radius);
                int hi = Math.Min(n - 1, i + radius);
                for (int j = lo; j <= hi; j++)
                    affected.Add(z[j]);
            }
        }
    }
}
