#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using WebAnnotation.View;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.UI
{
    /// <summary>What an eye in the Structure Types panel shows for a type and its descendants.</summary>
    internal enum StructureTypeEyeState
    {
        /// <summary>The type and all of its descendants are shown.</summary>
        Visible,

        /// <summary>The type and all of its descendants are hidden.</summary>
        Hidden,

        /// <summary>Some of the type and its descendants are hidden and some are shown.</summary>
        Mixed,
    }

    /// <summary>
    /// The structure types the user hid with the eye in the Structure Types panel. Annotations whose structure has a
    /// hidden type are neither drawn nor hit-tested. The set lives only in memory for this session: nothing is saved
    /// to the server. The type tree is supplied as a delegate so the logic can be tested without the annotation store.
    /// Thread-safe: drawing, mouse handling and the panel all read it.
    /// </summary>
    internal sealed class HiddenStructureTypeSet
    {
        private readonly object gate = new();
        private readonly HashSet<long> hiddenTypeIds = [];

        /// <summary>The set the viewer and the Structure Types panel share.</summary>
        public static HiddenStructureTypeSet Current { get; } = new();

        /// <summary>Raised after the hidden set changed, so the viewer can redraw and the panel can repaint.</summary>
        public event Action? Changed;

        /// <summary>True when no type is hidden, so callers can skip filtering.</summary>
        public bool IsEmpty
        {
            get
            {
                lock (gate)
                {
                    return hiddenTypeIds.Count == 0;
                }
            }
        }

        /// <summary>True when <paramref name="typeId"/> itself is hidden.</summary>
        public bool IsHidden(long typeId)
        {
            lock (gate)
            {
                return hiddenTypeIds.Contains(typeId);
            }
        }

        /// <summary>
        /// Hides or shows <paramref name="typeId"/> and every descendant of it.
        /// </summary>
        /// <param name="typeId">The type whose eye was clicked.</param>
        /// <param name="hide">True to hide the type and its descendants, false to show them.</param>
        /// <param name="childrenOf">Direct child type IDs of a type. Must tolerate unknown IDs.</param>
        public void SetSubtreeHidden(long typeId, bool hide, Func<long, IEnumerable<long>> childrenOf)
        {
            lock (gate)
            {
                foreach (long id in SubtreeOf(typeId, childrenOf))
                {
                    if (hide)
                        hiddenTypeIds.Add(id);
                    else
                        hiddenTypeIds.Remove(id);
                }
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// The eye was clicked: a type that is fully visible hides itself and its descendants; a hidden or
        /// partly hidden one shows them all.
        /// </summary>
        public void Toggle(long typeId, Func<long, IEnumerable<long>> childrenOf) =>
            SetSubtreeHidden(typeId, StateOf(typeId, childrenOf) == StructureTypeEyeState.Visible, childrenOf);

        /// <summary>Whether <paramref name="typeId"/> and its descendants are shown, hidden, or a mix.</summary>
        public StructureTypeEyeState StateOf(long typeId, Func<long, IEnumerable<long>> childrenOf)
        {
            lock (gate)
            {
                int hiddenCount = 0;
                int total = 0;
                foreach (long id in SubtreeOf(typeId, childrenOf))
                {
                    total++;
                    if (hiddenTypeIds.Contains(id))
                        hiddenCount++;
                }

                if (hiddenCount == 0)
                    return StructureTypeEyeState.Visible;

                return hiddenCount == total ? StructureTypeEyeState.Hidden : StructureTypeEyeState.Mixed;
            }
        }

        /// <summary>Shows every type again.</summary>
        public void Clear()
        {
            lock (gate)
            {
                hiddenTypeIds.Clear();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// The type and its descendants, each once. The visited set also stops a cycle in the tree.
        /// </summary>
        private static List<long> SubtreeOf(long typeId, Func<long, IEnumerable<long>> childrenOf)
        {
            List<long> result = [typeId];
            HashSet<long> seen = [typeId];
            for (int i = 0; i < result.Count; i++)
            {
                foreach (long child in childrenOf(result[i]))
                {
                    if (seen.Add(child))
                        result.Add(child);
                }
            }

            return result;
        }

        /// <summary>True when the structure with <paramref name="typeId"/> as its type is hidden.</summary>
        public bool IsTypeHidden(long? typeId) => typeId is long id && IsHidden(id);
    }

    /// <summary>
    /// Filters draw lists and hit-test results against <see cref="HiddenStructureTypeSet.Current"/>. Each method
    /// returns its input unchanged when no type is hidden, so the usual frame allocates nothing.
    /// </summary>
    internal static class HiddenStructureTypes
    {
        /// <summary>The type of the structure that owns <paramref name="location"/>, or null when unknown.</summary>
        private static long? TypeIdOf(LocationCanvasView location) => location.Parent?.modelObj.TypeID;

        /// <summary>True when the location belongs to a structure whose type is hidden.</summary>
        public static bool IsHidden(LocationCanvasView location) =>
            HiddenStructureTypeSet.Current.IsTypeHidden(TypeIdOf(location));

        /// <summary>Returns <paramref name="locations"/> without the locations of hidden types.</summary>
        public static List<LocationCanvasView> ExcludeLocations(List<LocationCanvasView> locations)
        {
            HiddenStructureTypeSet set = HiddenStructureTypeSet.Current;
            return set.IsEmpty ? locations : [.. locations.Where(l => !set.IsTypeHidden(TypeIdOf(l)))];
        }

        /// <summary>Returns the hits that are not a location of a hidden type.</summary>
        public static List<HitTestResult> ExcludeHits(List<HitTestResult> hits)
        {
            HiddenStructureTypeSet set = HiddenStructureTypeSet.Current;
            if (hits is null || set.IsEmpty)
                return hits!;

            return [.. hits.Where(h => h.obj is not LocationCanvasView location || !set.IsTypeHidden(TypeIdOf(location)))];
        }

        /// <summary>
        /// Returns the location links with neither end's location in a hidden type.
        /// </summary>
        /// <param name="typeOfLocation">Type ID of the structure owning a location ID, or null when unknown.</param>
        public static ICollection<LocationLinkView> ExcludeLocationLinks(ICollection<LocationLinkView> links, Func<long, long?> typeOfLocation)
        {
            HiddenStructureTypeSet set = HiddenStructureTypeSet.Current;
            if (set.IsEmpty)
                return links;

            return [.. links.Where(l => !set.IsTypeHidden(typeOfLocation(l.Key.A)) && !set.IsTypeHidden(typeOfLocation(l.Key.B)))];
        }

        /// <summary>Returns the structure links with neither end's structure in a hidden type.</summary>
        /// <param name="typeOfStructure">Type ID of a structure ID, or null when unknown.</param>
        public static List<StructureLinkViewModelBase> ExcludeStructureLinks(List<StructureLinkViewModelBase> links, Func<long, long?> typeOfStructure)
        {
            HiddenStructureTypeSet set = HiddenStructureTypeSet.Current;
            if (set.IsEmpty)
                return links;

            return [.. links.Where(l => !set.IsTypeHidden(typeOfStructure(l.SourceID)) && !set.IsTypeHidden(typeOfStructure(l.TargetID)))];
        }

        /// <summary>Direct child type IDs of <paramref name="typeId"/> from the structure type store.</summary>
        public static IEnumerable<long> ChildTypeIds(long typeId)
        {
            StructureTypeObj? type = Store.StructureTypes.GetObjectByID(typeId, false);
            return type is null ? [] : type.Children.Select(c => c.ID);
        }
    }
}
