using System.Collections.Generic;
using System.Linq;
using Viking.gRPC.SegmentationServiceTypes.V1;

namespace Viking.SegmentationPrompts.Rules
{
    /// <summary>
    /// Which annotations near the targets are background (what the object is not).
    /// </summary>
    /// <remarks>
    /// Viking's rule (<c>CircleSegmentationPrompts.IsOtherStructure</c>): not one of the targets, and not the targets'
    /// structure or one of its descendants (a child structure lies inside its parent and is part of the object).
    /// Limited to what the served model saw as negatives in training: circles, polygons and curve polygons of structures
    /// whose type is Cell or a child type of Cell (the exporter's
    /// <c>Parent/TypeID eq 1 or Parent/Type/ParentID eq 1</c>).
    /// </remarks>
    internal sealed class NeighbourRule
    {
        /// <summary>StructureType.ID of Cell.</summary>
        public const long CellTypeId = 1;

        private readonly HashSet<long> targetIds;
        private readonly HashSet<long> targetStructures;
        private readonly Dictionary<long, PromptStructure> structures;

        public NeighbourRule(IEnumerable<PromptAnnotation> targets, IEnumerable<PromptStructure> structures)
        {
            List<PromptAnnotation> list = targets.ToList();
            targetIds = new HashSet<long>(list.Select(target => target.Id));
            targetStructures = new HashSet<long>(list.Select(target => target.StructureId).Where(id => id != 0));
            this.structures = new Dictionary<long, PromptStructure>();
            foreach (PromptStructure structure in structures)
                this.structures[structure.Id] = structure;
        }

        /// <summary>True when the candidate is background by type, structure and relation to the targets.</summary>
        public bool Accepts(PromptAnnotation candidate)
        {
            if (targetIds.Contains(candidate.Id))
                return false;
            if (candidate.TypeCode != LocationTypeCode.Circle && candidate.TypeCode != LocationTypeCode.Polygon &&
                candidate.TypeCode != LocationTypeCode.CurvePolygon)
            {
                return false;
            }
            if (!structures.TryGetValue(candidate.StructureId, out PromptStructure? structure) || !IsCellLevel(structure))
                return false;
            return !IsTargetOrDescendant(candidate.StructureId);
        }

        private static bool IsCellLevel(PromptStructure structure) =>
            structure.TypeId == CellTypeId || structure.TypeParentId == CellTypeId;

        /// <summary>Walks the structure's parents up; a cycle in bad data ends the walk instead of looping.</summary>
        private bool IsTargetOrDescendant(long structureId)
        {
            var seen = new HashSet<long>();
            long id = structureId;
            while (id != 0 && seen.Add(id))
            {
                if (targetStructures.Contains(id))
                    return true;
                id = structures.TryGetValue(id, out PromptStructure? structure) ? structure.ParentId : 0;
            }
            return false;
        }
    }
}
