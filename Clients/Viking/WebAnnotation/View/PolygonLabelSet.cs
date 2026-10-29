using Geometry;
using System.Collections.Generic;
using System.Linq;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

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

        public readonly LabelPlacementKind Kind;

        private PolygonLabelSet(StructureLabelsBase labels, OverlappedLinkCircleView? linkView, LabelPlacementKind kind)
        {
            Labels = labels;
            LinkView = linkView;
            Kind = kind;
        }

        /// <summary>
        /// Fits <paramref name="obj"/>'s label and its <paramref name="links"/> into <paramref name="layout"/>: in the
        /// inscribed circle as circle annotations are drawn, or along a run with down links at its left end and up links
        /// at its right end. How readily the run may tilt is the Label rotation preference, read on every call so a
        /// rebuild picks up a change.
        /// </summary>
        public static PolygonLabelSet Build(LabelLayout layout, LocationObj obj, long id, int z, ICollection<long> links)
        {
            ICollection<LocationObj> linked = [];
            if (links.Count > 0)
            {
                Store.Locations.TryGetObjectsByIDs(links, out var found, out _);
                linked = [.. found];
            }

            int up = linked.Count(l => l.Z > z);
            int down = linked.Count(l => l.Z < z);

            StructurePlacedLabels placed = new(obj);
            LabelPlacement placement = layout.Fit(placed.Content(up, down), Global.AnnotationSettings.LabelRotationGain);
            if (placement.Kind == LabelPlacementKind.Run)
            {
                placed.Place(placement);
                return new PolygonLabelSet(placed,
                    links.Count == 0 ? null : new OverlappedLinkCircleView(placement, id, z, links),
                    placement.Kind);
            }

            return new PolygonLabelSet(new StructureCircleLabels(obj, placement.Circle),
                links.Count == 0 ? null : new OverlappedLinkCircleView(placement.Circle, id, z, links),
                placement.Kind);
        }
    }
}
