using Geometry;
using Microsoft.SqlServer.Types;
using System;
using System.Collections.Generic;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.UI.Controls;
using Viking.VolumeModel;
using WebAnnotation.ReviewFeed;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.UI.CellOverlap
{
    /// <summary>
    /// Gives an overlap region to one of the two cells that cover it: the area is cut out of the other cell's
    /// outline. The winner's outline already contains the region (it is the intersection of the two), so it is left alone.
    /// </summary>
    /// <remarks>
    /// Edits start from the owner's volume polygon and save mosaic through <see cref="LocationShapeUpdate"/>
    /// (<c>annotation-volume-geometry.mdc</c>). When the cut splits the other outline, the part that holds its seed stays on
    /// the location and every other part becomes a new unlinked location on the same structure and section, as the pen's
    /// split does (<c>RetraceAndReplacePathCommand.ApplyCut</c>). The pieces are saved as cut, without the save-time curve
    /// fit (<see cref="AutoPolygonizeSelection.SimplifyForSave"/>): their points are already the two outlines' own, and the fit
    /// moves control points far out at the sharp corners where the cut meets the outline, back over the winner, so the
    /// overlap stayed and giving it again changed nothing. Nothing is saved unless every piece maps. One Ctrl+Z undoes the
    /// whole edit.
    /// </remarks>
    internal static class AssignOverlapAction
    {
        /// <summary>
        /// The pieces an assignment produces, ready to save as they are; <see cref="Outline"/> stays on the location,
        /// <see cref="Extras"/> become new locations.
        /// </summary>
        internal sealed record Plan(LocationObj Loser, Polygon Outline, IReadOnlyList<Polygon> Extras);

        /// <summary>
        /// Volume pixels around the region within which the winner's outline is cut from the other cell
        /// (<see cref="CellOverlapGeometry.AreaToGive"/>): enough to reach past the region's rounded edges, narrow enough that
        /// another overlap of the same two cells is left for its own menu.
        /// </summary>
        internal const double GiveMargin = 16;

        /// <summary>
        /// Works out what giving <paramref name="region"/> to <paramref name="winnerId"/> does to the other cell.
        /// </summary>
        /// <param name="failure">Why there is no plan: nothing would be left of the other outline, or it cannot be mapped.</param>
        public static Plan? TryPlan(CellOverlapRegion region, long winnerId, IVolumeToSectionTransform transform, out string failure)
        {
            failure = string.Empty;
            if (region.Other(winnerId) is not long loserId)
            {
                failure = "That cell is not part of this overlap.";
                return null;
            }

            LocationObj? loser = Store.Locations.GetObjectByID(loserId, false);
            if (loser is null || !AutoPolygonizeSelection.TryGetVolumePolygon(loser, transform, out Polygon? outline) || outline is null)
            {
                failure = "The other outline cannot be mapped on this section, so it was not changed.";
                return null;
            }

            // What the other cell gives up: the winner's own outline near the region (the region itself when the winner's
            // outline is not at hand).
            LocationObj? winner = Store.Locations.GetObjectByID(winnerId, false);
            Polygon give = winner is not null && AutoPolygonizeSelection.TryGetVolumePolygon(winner, transform, out Polygon? winnerOutline) && winnerOutline is not null
                ? CellOverlapGeometry.AreaToGive(region.Shape, winnerOutline, GiveMargin) ?? region.Shape
                : region.Shape;

            Vector2 keepPoint = loser.VolumeCenter();
            if (!CellOverlapGeometry.TrySubtract(outline, give, keepPoint, out Polygon? primary, out IReadOnlyList<Polygon> extras) ||
                primary is null)
            {
                failure = $"The overlap covers all of structure {loser.ParentID} here, so nothing would be left of it. Delete that annotation instead.";
                return null;
            }

            return new Plan(loser, primary, extras);
        }

        /// <summary>
        /// Applies <see cref="TryPlan"/> and saves. Returns false, with the reason shown to the user, when nothing was changed.
        /// </summary>
        public static bool TryApply(CellOverlapRegion region, long winnerId, SectionViewerControl parent)
        {
            if (parent?.Section is null)
                return false;

            IVolumeToSectionTransform transform = parent.Section.ActiveSectionToVolumeTransform;
            Plan? plan = TryPlan(region, winnerId, transform, out string failure);
            if (plan is null)
            {
                parent.ShowTransientStatus(failure);
                return false;
            }

            LocationObj loser = plan.Loser;
            SqlGeometry oldShape = loser.MosaicShape;
            LocationType oldType = loser.TypeCode;
            List<LocationObj> siblings = [];
            try
            {
                // Every piece is mapped before the first is saved, so a refusal leaves everything as it was.
                foreach (Polygon extra in plan.Extras)
                {
                    LocationObj sibling = new(loser.Parent, loser.Section, loser.TypeCode);
                    if (loser.Width.HasValue)
                        sibling.Width = loser.Width;

                    LocationShapeUpdate.AssignVolumePolygon(sibling, extra, transform);
                    siblings.Add(sibling);
                }

                LocationShapeUpdate.AssignVolumePolygon(loser, plan.Outline, transform);
            }
            catch (ArgumentException ex)
            {
                parent.ShowTransientStatus($"The overlap was not assigned: {ex.Message}");
                return false;
            }

            Store.Locations.Save();
            ReviewChangeFeedIngress.PushUpsert(loser);

            List<LocationObj> created = [];
            foreach (LocationObj sibling in siblings)
                created.Add(Store.Locations.Create(sibling));

            UndoManager.PushLocationEdits("Give Overlap", [(loser, oldShape, oldType)], transform, created);
            parent.Invalidate();
            return true;
        }
    }
}
