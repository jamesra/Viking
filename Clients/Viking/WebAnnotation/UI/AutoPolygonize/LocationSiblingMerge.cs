using System.Collections.Generic;
using System.Linq;
using WebAnnotationModel;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>
    /// Picks the survivor and the unique Z-links to copy before deleting same-section siblings.
    /// Used by auto-polygonize accept after a grouped SAM2 resubmit; does not touch the store.
    /// </summary>
    internal static class LocationSiblingMerge
    {
        /// <summary>
        /// Location with the most <see cref="LocationObj.LinksCopy"/> neighbors; lowest ID wins ties.
        /// </summary>
        public static long ChooseSurvivorId(IReadOnlyList<LocationObj> members)
        {
            LocationObj survivor = members
                .OrderByDescending(member => member.LinksCopy.Length)
                .ThenBy(member => member.ID)
                .First();
            return survivor.ID;
        }

        /// <summary>
        /// Same rule as <see cref="ChooseSurvivorId(IReadOnlyList{LocationObj})"/> when only IDs and link counts are available.
        /// </summary>
        public static long ChooseSurvivorId(IReadOnlyList<(long Id, int LinkCount)> members)
        {
            return members
                .OrderByDescending(member => member.LinkCount)
                .ThenBy(member => member.Id)
                .First()
                .Id;
        }

        /// <summary>
        /// Neighbors the survivor should gain from victims. Skips IDs already on the survivor
        /// and every group member (same-section siblings cannot be location-linked).
        /// </summary>
        public static IReadOnlyList<long> UniqueNeighborIdsToTransfer(
            IReadOnlyCollection<long> survivorLinks,
            IReadOnlyCollection<long> groupIds,
            IEnumerable<IReadOnlyCollection<long>> victimLinkSets)
        {
            HashSet<long> have = [.. survivorLinks];
            HashSet<long> group = [.. groupIds];
            List<long> create = [];
            foreach (IReadOnlyCollection<long> links in victimLinkSets)
            {
                if (links is null)
                    continue;

                foreach (long otherId in links)
                {
                    if (group.Contains(otherId) || have.Contains(otherId))
                        continue;

                    have.Add(otherId);
                    create.Add(otherId);
                }
            }

            return create;
        }
    }
}
