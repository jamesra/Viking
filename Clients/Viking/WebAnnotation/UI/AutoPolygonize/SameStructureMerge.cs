using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Geometry;
using Viking.VolumeModel;
using WebAnnotation.View;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>
    /// Merges same-structure polygon locations on one section into the survivor <see cref="LocationSiblingMerge"/> picks.
    /// Auto-polygonize accept and an accepted manual polygon (Segment to Polygon, Resegment, Convert to Polygon and an edited
    /// mask, in Viking's context menu and in Jotunn's tools) all end here, so the links move and the extra locations go the
    /// same way.
    /// </summary>
    /// <remarks>
    /// Overlaps are tested in volume space, on shapes derived from the authoritative mosaic shapes: a polygon is its mosaic
    /// shape mapped through the section transform (<see cref="AutoPolygonizeSelection.TryGetVolumePolygon"/>). The union is
    /// written with <see cref="LocationShapeUpdate.ApplyVolumePolygon(LocationObj, Polygon, IVolumeToSectionTransform, double)"/>,
    /// which maps it back to mosaic space before the save, so no volume coordinate is saved as a mosaic one.
    /// </remarks>
    internal static class SameStructureMerge
    {
        /// <summary>A location that may join the merge, with its outline in volume space.</summary>
        internal readonly record struct Piece(LocationObj Location, Polygon Polygon);

        /// <summary>
        /// Gives the survivor the Z-links the victims had that it lacks, deletes the victims and saves.
        /// Same-section group members are skipped as link targets because they cannot be linked to each other.
        /// </summary>
        /// <param name="groupIds">Every location of the group: the survivor and the victims.</param>
        /// <returns>True when victims were deleted.</returns>
        public static async Task<bool> TransferLinksAndRemoveAsync(LocationObj survivor, IReadOnlyList<LocationObj> victims, IReadOnlyCollection<long> groupIds)
        {
            IReadOnlyList<long> toLink = LocationSiblingMerge.UniqueNeighborIdsToTransfer(
                survivor.LinksCopy,
                groupIds,
                victims.Select(victim => (IReadOnlyCollection<long>)victim.LinksCopy));
            foreach (long neighborId in toLink)
                await Store.LocationLinks.CreateLink(survivor.ID, neighborId);

            foreach (LocationObj victim in victims)
                await Store.Locations.Remove(victim);

            if (victims.Count == 0)
                return false;

            await AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
            return true;
        }

        /// <summary>
        /// After a manual polygon is saved on <paramref name="saved"/>: finds the polygons of the same structure on the same section
        /// that overlap it (chains of overlaps included), writes the union of all of them onto the survivor and removes the rest,
        /// moving their links. Edge contact alone is not an overlap. Does nothing when nothing overlaps.
        /// </summary>
        /// <param name="transform">Section-to-volume transform of <paramref name="saved"/>'s section.</param>
        /// <param name="downsample">The view's downsample, passed on to <see cref="LocationShapeUpdate.ApplyVolumePolygon(LocationObj, Polygon, IVolumeToSectionTransform, double)"/>.</param>
        /// <param name="invalidate">Redraws the view once locations were merged; may be null.</param>
        /// <returns>True when locations were merged.</returns>
        public static async Task<bool> MergeOverlappingSiblingsAsync(LocationObj saved, IVolumeToSectionTransform transform, double downsample, Action? invalidate = null)
        {
            if (saved?.ParentID is null || transform is null)
                return false;

            if (!AutoPolygonizeSelection.TryGetVolumePolygon(saved, transform, out Polygon? savedPolygon) || savedPolygon is null)
                return false;

            // The candidates are the locations whose views meet the saved outline's bounding box, so a sibling that touches the
            // group only through another sibling lying outside that box is not found.
            SectionAnnotationsView? sectionView = AnnotationOverlay.GetAnnotationsForSection((int)saved.Section);
            if (sectionView is null)
                return false;

            List<LocationObj> nearby = [];
            foreach (LocationCanvasView view in sectionView.GetLocations(savedPolygon.BoundingBox))
            {
                if (Store.Locations.TryGetObjectByID(view.ID, out LocationObj other) && other is not null)
                    nearby.Add(other);
            }

            List<Piece> group = FindGroup(saved, savedPolygon, nearby, transform);
            if (group.Count < 2)
                return false;

            List<LocationObj> members = [.. group.Select(piece => piece.Location)];
            long survivorId = LocationSiblingMerge.ChooseSurvivorId(members);
            LocationObj survivor = members.First(member => member.ID == survivorId);
            Polygon? union = AutoPolygonizeSelection.UnionPolygons(group.Select(piece => piece.Polygon));
            if (union is null || !await LocationShapeUpdate.ApplyVolumePolygon(survivor, union, transform, downsample))
                return false;

            await TransferLinksAndRemoveAsync(survivor, [.. members.Where(member => member.ID != survivorId)], [.. members.Select(member => member.ID)]);
            invalidate?.Invoke();
            return true;
        }

        /// <summary>
        /// <paramref name="saved"/> and the polygons among <paramref name="nearby"/> of its structure and section that overlap it,
        /// directly or through a chain of overlaps (<see cref="AutoPolygonizeSelection.ShouldGroupSiblingPolygons"/>). Circles,
        /// curves and other structures' locations never join. The first piece is always <paramref name="saved"/>.
        /// </summary>
        /// <param name="savedPolygon"><paramref name="saved"/>'s outline in volume space.</param>
        /// <param name="transform">Maps each candidate's mosaic shape into volume space.</param>
        internal static List<Piece> FindGroup(LocationObj saved, Polygon savedPolygon, IEnumerable<LocationObj> nearby, IVolumeToSectionTransform transform)
        {
            List<Piece> pool = [];
            foreach (LocationObj other in nearby)
            {
                if (other is null || other.ID == saved.ID || other.ParentID != saved.ParentID || other.Section != saved.Section)
                    continue;
                if (pool.Any(piece => piece.Location.ID == other.ID))
                    continue;

                if (AutoPolygonizeSelection.TryGetVolumePolygon(other, transform, out Polygon? polygon) && polygon is not null)
                    pool.Add(new Piece(other, polygon));
            }

            return Group(new Piece(saved, savedPolygon), pool);
        }

        /// <summary>The seed and every piece of the pool reachable from it through overlaps.</summary>
        private static List<Piece> Group(Piece seed, IReadOnlyList<Piece> pool)
        {
            List<Piece> group = [seed];
            Queue<Polygon> pending = new([seed.Polygon]);
            while (pending.Count > 0)
            {
                Polygon current = pending.Dequeue();
                foreach (Piece candidate in pool.Where(piece => group.All(member => member.Location.ID != piece.Location.ID)).ToList())
                {
                    if (!AutoPolygonizeSelection.ShouldGroupSiblingPolygons(current, candidate.Polygon))
                        continue;

                    group.Add(candidate);
                    pending.Enqueue(candidate.Polygon);
                }
            }

            return group;
        }
    }
}
