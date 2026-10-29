using Geometry;
using System.Collections.Generic;
using System.Linq;
using WebAnnotationModel;

namespace WebAnnotation.View
{
    /// <summary>
    /// The structure labels and overlapped-link markers of a polygon or closed curve. They are built together from one
    /// <see cref="LabelLayout"/> because where the text goes depends on how many links go up and how many go down.
    /// </summary>
    internal readonly struct PolygonLabelSet
    {
        public readonly StructureLabelsBase Labels;

        /// <summary>Null when there are no overlapped links.</summary>
        public readonly OverlappedLinkCircleView? LinkView;

        /// <summary>
        /// The placement the labels were built at. The view keeps it and passes it back to the next
        /// <see cref="Build"/> for hysteresis, and drops it when its polygon (its <see cref="LabelLayout"/>) changes.
        /// </summary>
        public readonly LabelPlacement Placement;

        public LabelPlacementKind Kind => Placement.Kind;

        private PolygonLabelSet(StructureLabelsBase labels, OverlappedLinkCircleView? linkView, LabelPlacement placement)
        {
            Labels = labels;
            LinkView = linkView;
            Placement = placement;
        }

        /// <summary>
        /// Fits <paramref name="obj"/>'s label and its <paramref name="links"/> into <paramref name="layout"/>: in the
        /// inscribed circle as circle annotations are drawn (up links above the text, down links below), or along a run
        /// with down links at its left end and up links at its right end. The markers are packed in staggered rows, and
        /// shrunk, when one row does not fit (see <see cref="LabelLayout.PackingOrder"/>). How readily the run may tilt
        /// is the Label rotation preference, read on every call so a rebuild picks up a change.
        /// </summary>
        /// <param name="previousPlacement">
        /// The placement of the labels being replaced, from this same layout, or null. It is kept while it still holds the
        /// label and the new best beats it by no more than <see cref="LabelLayout.Hysteresis"/>.
        /// </param>
        /// <param name="previousLabels">
        /// The labels being replaced, or null for a first build. When the placement changes, the new labels ease from
        /// where these are shown to their new place; the link markers move at once.
        /// </param>
        public static PolygonLabelSet Build(LabelLayout layout, LocationObj obj, long id, int z, ICollection<long> links,
            LabelPlacement? previousPlacement = null, StructureLabelsBase? previousLabels = null)
        {
            ICollection<LocationObj> linked = links.Count == 0 ? [] : Store.Locations.GetObjectsByIDs(links, false);
            int up = linked.Count(l => l.Z > z);
            int down = linked.Count(l => l.Z < z);

            StructurePlacedLabels placed = new(obj);
            LabelPlacement placement = layout.Fit(placed.Content(up, down), Global.AnnotationSettings.LabelRotationGain, previousPlacement);
            StructureLabelsBase labels;
            if (placement.Kind == LabelPlacementKind.Run)
            {
                placed.Place(placement);
                labels = placed;
            }
            else
            {
                labels = new StructureCircleLabels(obj, placement.Circle);
            }

            // Both kinds take the markers' slots from the placement, which checked each against the outline and holes
            OverlappedLinkCircleView? linkView = links.Count == 0 ? null : new OverlappedLinkCircleView(placement, id, z, links);

            labels.TransitionFrom(previousLabels);
            return new PolygonLabelSet(labels, linkView, placement);
        }
    }
}
