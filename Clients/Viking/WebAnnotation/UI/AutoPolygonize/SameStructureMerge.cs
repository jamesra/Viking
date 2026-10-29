#nullable enable

using System.Collections.Generic;
using System.Linq;
using Geometry;
using Viking.UI.Controls;
using WebAnnotation.View;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>
    /// Merges same-structure polygon locations on one section into the survivor <see cref="LocationSiblingMerge"/> picks.
    /// Auto-polygonize accept and an accepted manual polygon both end here, so the links move and the extra locations go the same way.
    /// </summary>
    internal static class SameStructureMerge
    {
        /// <summary>
        /// Gives the survivor the Z-links the victims had that it lacks, deletes the victims and saves.
        /// Same-section group members are skipped as link targets because they cannot be linked to each other.
        /// </summary>
        /// <param name="groupIds">Every location of the group: the survivor and the victims.</param>
        /// <returns>True when victims were deleted.</returns>
        public static bool TransferLinksAndRemove(LocationObj survivor, IReadOnlyList<LocationObj> victims, IReadOnlyCollection<long> groupIds)
        {
            IReadOnlyList<long> toLink = LocationSiblingMerge.UniqueNeighborIdsToTransfer(
                survivor.LinksCopy,
                groupIds,
                victims.Select(victim => (IReadOnlyCollection<long>)victim.LinksCopy));
            foreach (long neighborId in toLink)
                Store.LocationLinks.CreateLink(survivor.ID, neighborId);

            foreach (LocationObj victim in victims)
                Store.Locations.Remove(victim);

            if (victims.Count == 0)
                return false;

            AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
            return true;
        }

        /// <summary>
        /// After a manual polygon is saved on <paramref name="saved"/>: finds the polygons of the same structure on the same section
        /// that overlap it (chains of overlaps included), writes the union of all of them onto the survivor and removes the rest,
        /// moving their links. Edge contact alone is not an overlap. Does nothing when nothing overlaps.
        /// </summary>
        /// <returns>True when locations were merged.</returns>
        public static bool MergeOverlappingSiblings(LocationObj saved, SectionViewerControl parent)
        {
            if (saved?.ParentID is not long structureId || parent?.Section is null)
                return false;

            Viking.VolumeModel.IVolumeToSectionTransform transform = parent.Section.ActiveSectionToVolumeTransform;
            if (!AutoPolygonizeSelection.TryGetVolumePolygon(saved, transform, out Polygon? savedPolygon) || savedPolygon is null)
                return false;

            SectionAnnotationsView? sectionView = AnnotationOverlay.GetOrCreateAnnotationsForSection(saved.Section);
            if (sectionView is null)
                return false;

            // The structure's other polygons on this section, found by the bounding box of everything that could still join the group.
            List<(LocationObj Location, Polygon Polygon)> pool = [];
            foreach (LocationCanvasView view in sectionView.GetLocations(savedPolygon.BoundingBox))
            {
                LocationObj? other = Store.Locations.GetObjectByID(view.ID, false);
                if (other is null || other.ID == saved.ID || other.ParentID != structureId || other.Section != saved.Section)
                    continue;
                if (AutoPolygonizeSelection.TryGetVolumePolygon(other, transform, out Polygon? polygon) && polygon is not null)
                    pool.Add((other, polygon));
            }

            List<(LocationObj Location, Polygon Polygon)> group = [(saved, savedPolygon)];
            Queue<Polygon> pending = new([savedPolygon]);
            while (pending.Count > 0)
            {
                Polygon current = pending.Dequeue();
                foreach ((LocationObj Location, Polygon Polygon) candidate in pool.Where(c => group.All(g => g.Location.ID != c.Location.ID)).ToList())
                {
                    if (!AutoPolygonizeSelection.ShouldGroupSiblingPolygons(current, candidate.Polygon))
                        continue;

                    group.Add(candidate);
                    pending.Enqueue(candidate.Polygon);
                }
            }

            if (group.Count < 2)
                return false;

            List<LocationObj> members = [.. group.Select(g => g.Location)];
            long survivorId = LocationSiblingMerge.ChooseSurvivorId(members);
            LocationObj survivor = members.First(member => member.ID == survivorId);
            Polygon? union = AutoPolygonizeSelection.UnionPolygons(group.Select(g => g.Polygon));
            if (union is null || !LocationShapeUpdate.ApplyVolumePolygon(survivor, union, parent))
                return false;

            TransferLinksAndRemove(survivor, [.. members.Where(member => member.ID != survivorId)], [.. members.Select(member => member.ID)]);
            parent.Invalidate();
            return true;
        }
    }
}
