#nullable enable
using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using WebAnnotation.View;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI
{
    /// <summary>
    /// Works out which structures to hide while the user retraces a cell's contour with the pen, and filters
    /// draw lists and hit-test results against that set. Pure logic: the structure tree and link graph are
    /// supplied as delegates so it can be tested without the annotation store.
    /// </summary>
    internal static class HiddenStructures
    {
        /// <summary>
        /// Returns every descendant of <paramref name="targetStructureId"/> plus every structure linked to one of
        /// those descendants. The target and its ancestors are never included, even when a descendant links to them,
        /// because hiding them would hide the contour being retraced.
        /// </summary>
        /// <param name="targetStructureId">The cell being retraced.</param>
        /// <param name="childrenOf">Direct child structure IDs of a structure. Must tolerate unknown IDs.</param>
        /// <param name="linkedTo">IDs at the other end of a structure's structure links. Must tolerate unknown IDs.</param>
        /// <param name="parentOf">Parent structure ID, or null for a root. Used to find the target's ancestors.</param>
        /// <returns>The hidden structure IDs. Empty when the target has no descendants.</returns>
        public static HashSet<long> Compute(
            long targetStructureId,
            Func<long, IEnumerable<long>> childrenOf,
            Func<long, IEnumerable<long>> linkedTo,
            Func<long, long?> parentOf)
        {
            HashSet<long> protectedIds = [targetStructureId];
            long? ancestor = parentOf(targetStructureId);
            while (ancestor is long id && protectedIds.Add(id))
                ancestor = parentOf(id);

            HashSet<long> descendants = [];
            Queue<long> pending = new();
            pending.Enqueue(targetStructureId);
            while (pending.Count > 0)
            {
                foreach (long child in childrenOf(pending.Dequeue()))
                {
                    // protectedIds also guards against a cycle that leads back to the target.
                    if (!protectedIds.Contains(child) && descendants.Add(child))
                        pending.Enqueue(child);
                }
            }

            if (descendants.Count == 0)
                return descendants;

            HashSet<long> hidden = [.. descendants];
            foreach (long descendant in descendants)
            {
                foreach (long linked in linkedTo(descendant))
                {
                    if (!protectedIds.Contains(linked))
                        hidden.Add(linked);
                }
            }

            return hidden;
        }

        /// <summary>
        /// For a pen stroke's new log entries: the structures to hide while it retraces a cell, or null while no target is
        /// known yet. The target is the first annotation the stroke starts inside (PENDOWN) or crosses into (ENTER) that
        /// has descendants, so a stroke may begin over a child or in empty space. Uses the structure store's cache.
        /// </summary>
        public static HashSet<long>? ForStrokeEntries(IEnumerable<InteractionLogEvent> added)
        {
            foreach (InteractionLogEvent entry in added)
            {
                if (entry.Interaction is not (AnnotationRegionInteraction.ENTER or AnnotationRegionInteraction.PENDOWN) ||
                    entry.Annotation is not LocationCanvasView { ParentID: long structureId })
                {
                    continue;
                }

                HashSet<long> hidden = Compute(
                    structureId,
                    id => Store.Structures.GetCachedObject(id)?.Children.Select(c => c.ID) ?? [],
                    LinkedStructureIDs,
                    id => Store.Structures.GetCachedObject(id)?.ParentID);

                if (hidden.Count > 0)
                    return hidden;
            }

            return null;
        }

        private static IEnumerable<long> LinkedStructureIDs(long structureId)
        {
            StructureObj? structure = Store.Structures.GetCachedObject(structureId);
            if (structure is null)
                return [];

            return structure.LinksCopy?.Select(link => link.SourceID == structureId ? link.TargetID : link.SourceID) ?? [];
        }

        /// <summary>Returns true when the location belongs to a hidden structure.</summary>
        public static bool IsHidden(LocationCanvasView location, ISet<long>? hidden) =>
            hidden is { Count: > 0 } && location.ParentID is long parentId && hidden.Contains(parentId);

        /// <summary>
        /// Returns <paramref name="locations"/> without the locations of hidden structures. Returns the input list
        /// unchanged when nothing is hidden so the common (not retracing) frame allocates nothing.
        /// </summary>
        public static List<LocationCanvasView> ExcludeLocations(List<LocationCanvasView> locations, ISet<long>? hidden) =>
            hidden is { Count: > 0 } ? [.. locations.Where(l => !IsHidden(l, hidden))] : locations;

        /// <summary>
        /// Returns the location links that do not touch a hidden structure. A link is dropped when either end's
        /// location belongs to a hidden structure.
        /// </summary>
        /// <param name="parentOfLocation">Structure ID owning a location ID, or null when unknown.</param>
        public static ICollection<LocationLinkView> ExcludeLocationLinks(
            ICollection<LocationLinkView> links,
            ISet<long>? hidden,
            Func<long, long?> parentOfLocation)
        {
            if (hidden is not { Count: > 0 })
                return links;

            return [.. links.Where(l => !IsHiddenStructure(parentOfLocation(l.Key.A), hidden) &&
                                        !IsHiddenStructure(parentOfLocation(l.Key.B), hidden))];
        }

        /// <summary>Returns the structure links with neither end in a hidden structure.</summary>
        internal static List<StructureLinkViewModelBase> ExcludeStructureLinks(
            List<StructureLinkViewModelBase> links,
            ISet<long>? hidden) =>
            hidden is { Count: > 0 }
                ? [.. links.Where(l => !hidden.Contains(l.SourceID) && !hidden.Contains(l.TargetID))]
                : links;

        private static bool IsHiddenStructure(long? structureId, ISet<long> hidden) =>
            structureId is long id && hidden.Contains(id);
    }

    /// <summary>
    /// Wraps another <see cref="ICanvasViewHitTesting"/> and removes locations of hidden structures from every
    /// result, so a pen stroke cannot log, target or take actions on annotations that are not drawn.
    /// The hidden set is read on each call, so it can be supplied after the stroke has started.
    /// </summary>
    /// <param name="inner">The hit-testing source to filter.</param>
    /// <param name="hiddenStructureIds">Returns the current hidden set, or null when nothing is hidden. Called on the
    /// thread that hit-tests (the UI thread).</param>
    internal sealed class HiddenStructureHitTestFilter(ICanvasViewHitTesting inner, Func<ISet<long>?> hiddenStructureIds) : ICanvasViewHitTesting
    {
        /// <inheritdoc/>
        public List<HitTestResult> GetAnnotations(Vector2 WorldPosition) => Filter(inner.GetAnnotations(WorldPosition));

        /// <inheritdoc/>
        public List<HitTestResult> GetAnnotations(LineSegment line) => Filter(inner.GetAnnotations(line));

        /// <inheritdoc/>
        public List<HitTestResult> GetAnnotations(Geometry.Rectangle rect) => Filter(inner.GetAnnotations(rect));

        private List<HitTestResult> Filter(List<HitTestResult> results)
        {
            ISet<long>? hidden = hiddenStructureIds();
            if (results is null || hidden is not { Count: > 0 })
                return results!;

            return [.. results.Where(r => r.obj is not LocationCanvasView location || !HiddenStructures.IsHidden(location, hidden))];
        }
    }
}
