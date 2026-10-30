#nullable enable

using System;
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
    /// Merges same-structure locations on one section into the survivor <see cref="LocationSiblingMerge"/> picks.
    /// Auto-polygonize accept, an accepted manual polygon and a pen loop drawn around a circle all end here, so the links move and the
    /// extra locations go the same way.
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
        /// After a manual polygon is saved on <paramref name="saved"/>: merges the polygons of the same structure on the same section
        /// that overlap it (see <see cref="Merge"/>). Circles are left alone.
        /// </summary>
        /// <returns>True when locations were merged.</returns>
        public static bool MergeOverlappingSiblings(LocationObj saved, SectionViewerControl parent) =>
            Merge(saved, parent, includeCircles: false, confirmPolygons: null);

        /// <summary>
        /// After a pen loop turned a circle into the polygon now saved on <paramref name="saved"/>: circles of the same structure that
        /// the loop touches or holds are merged without asking; polygons of the same structure that it touches or holds are merged only when
        /// <paramref name="confirmPolygons"/> says yes (a glial cell can split and bump into another of its processes, or the user may
        /// want them kept apart).
        /// </summary>
        /// <param name="confirmPolygons">Given the polygon locations that would join; true merges them too.</param>
        public static bool MergeAfterPenLoop(LocationObj saved, SectionViewerControl parent, Func<IReadOnlyList<LocationObj>, bool> confirmPolygons) =>
            Merge(saved, parent, includeCircles: true, confirmPolygons);

        /// <summary>
        /// True when <paramref name="other"/>, not a polygon, joins a merge as its disk: only a circle annotation does, and only when
        /// <paramref name="includeCircles"/>. Every location has a position and radius, so without the type check a point, line or
        /// curve of the structure would be swept into the merged polygon as a disk.
        /// </summary>
        internal static bool JoinsAsDisk(LocationObj other, bool includeCircles) =>
            includeCircles && other.TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE;

        /// <summary>
        /// Finds the same-structure, same-section locations whose outline overlaps <paramref name="saved"/>'s (chains of overlaps
        /// included; edge contact alone is not an overlap), writes the union of all of them onto the survivor and removes the rest,
        /// moving their links. Does nothing when nothing overlaps.
        /// </summary>
        /// <param name="includeCircles">Circle annotations join as their disks.</param>
        /// <param name="confirmPolygons">
        /// Null merges the polygons that overlap. Otherwise the polygons that would join are asked about first, and the merge
        /// takes only circles (and what chains through them) when it returns false.
        /// </param>
        private static bool Merge(LocationObj saved, SectionViewerControl parent, bool includeCircles, Func<IReadOnlyList<LocationObj>, bool>? confirmPolygons)
        {
            if (saved?.ParentID is not long structureId || parent?.Section is null)
                return false;

            Viking.VolumeModel.IVolumeToSectionTransform transform = parent.Section.ActiveSectionToVolumeTransform;
            if (!AutoPolygonizeSelection.TryGetVolumePolygon(saved, transform, out Polygon? savedPolygon) || savedPolygon is null)
                return false;

            SectionAnnotationsView? sectionView = AnnotationOverlay.GetOrCreateAnnotationsForSection(saved.Section);
            if (sectionView is null)
                return false;

            // The structure's other outlines on this section; a circle is its disk.
            List<Piece> pool = [];
            foreach (LocationCanvasView view in sectionView.GetLocations(savedPolygon.BoundingBox))
            {
                LocationObj? other = Store.Locations.GetObjectByID(view.ID, false);
                if (other is null || other.ID == saved.ID || other.ParentID != structureId || other.Section != saved.Section)
                    continue;

                if (AutoPolygonizeSelection.TryGetVolumePolygon(other, transform, out Polygon? polygon) && polygon is not null)
                    pool.Add(new Piece(other, polygon, IsCircle: false));
                else if (JoinsAsDisk(other, includeCircles) && LocationActions.TryGetVolumeCircle(other, transform) is Circle disk)
                    pool.Add(new Piece(other, AutoPolygonizeSelection.CircleToPolygon(disk.Center, disk.Radius), IsCircle: true));
            }

            Piece self = new(saved, savedPolygon, IsCircle: false);
            List<Piece> group = Group(self, pool);
            if (confirmPolygons is not null)
            {
                // Circles join unasked. A polygon in the group is asked about, and if the user declines it (and whatever chained through it) stays out.
                List<Piece> polygons = [.. group.Where(piece => !piece.IsCircle && piece.Location.ID != saved.ID)];
                if (polygons.Count > 0 && !confirmPolygons([.. polygons.Select(piece => piece.Location)]))
                    group = Group(self, [.. pool.Where(piece => piece.IsCircle)]);
            }

            if (group.Count < 2)
                return false;

            List<LocationObj> members = [.. group.Select(piece => piece.Location)];
            long survivorId = LocationSiblingMerge.ChooseSurvivorId(members);
            LocationObj survivor = members.First(member => member.ID == survivorId);
            Polygon? union = AutoPolygonizeSelection.UnionPolygons(group.Select(piece => piece.Polygon));
            if (union is null || !LocationShapeUpdate.ApplyVolumePolygon(survivor, union, parent))
                return false;

            TransferLinksAndRemove(survivor, [.. members.Where(member => member.ID != survivorId)], [.. members.Select(member => member.ID)]);
            parent.Invalidate();
            return true;
        }

        private readonly record struct Piece(LocationObj Location, Polygon Polygon, bool IsCircle);

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
