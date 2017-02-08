using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
