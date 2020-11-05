using System.Collections.Generic;
using System.Linq;

namespace AnnotationVizLib
{
    public static class DistanceExtensions
    {
        internal static bool NodeContainsStructureOfType(this MorphologyNode node, SortedSet<ulong> TypeIDs)
        {
            return node.Subgraphs.Where(n => TypeIDs.Contains(n.structureType.ID)).Any();
        }
    }
}
