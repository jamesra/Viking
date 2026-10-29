using Geometry;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using VikingXNAGraphics;
using WebAnnotationModel;

namespace WebAnnotation.View
{
    /// <summary>
    /// Renders labels for a Structure along a run of a polygon, one row per <see cref="LabelPlacement.RowCenters"/> entry,
    /// rotated with the run. Built in two steps so the rows <see cref="LabelLayout.Fit(LabelContent, double)"/> measured are the rows placed:
    /// construct, pass <see cref="Content"/> to the layout, then <see cref="Place"/> the result.
    /// </summary>
    public class StructurePlacedLabels : StructureLabelsBase
    {
        private readonly IReadOnlyList<(LabelRowKind Kind, string Text, double Scale)> LabelRows;
        private LabelPlacement Placement;

        public override double DefaultFontSize => Placement.FontSize;

        protected override double IDLineWidth => Placement.MaxLineWidth;

        public StructurePlacedLabels(LocationObj obj, bool ShowAttributeLabels = true) : base(obj, ShowAttributeLabels)
        {
            LabelRows = Rows(obj, ShowAttributeLabels);
        }

        /// <summary>
        /// What the label must hold: its rows measured in the default font, and the counts of links to sections above and
        /// below.
        /// </summary>
        public LabelContent Content(int upCount, int downCount) =>
            new([.. LabelRows.Select(r => new LabelRow(LabelMetrics.UnitWidth(r.Text), r.Scale))], upCount, downCount);

        /// <summary>Creates the row views at a run placement that <see cref="LabelLayout.Fit(LabelContent, double)"/> made from <see cref="Content"/>.</summary>
        public void Place(LabelPlacement placement)
        {
            Placement = placement;
            StructureLabelView = null;
            StructureAttributeView = null;
            ParentStructureLabelView = null;

            for (int i = 0; i < LabelRows.Count; i++)
            {
                LabelView view = new(LabelRows[i].Text, placement.RowCenters[i], fontSize: DefaultFontSize * LabelRows[i].Scale)
                {
                    Outlined = true,
                    Rotation = (float)placement.Angle,
                    MaxLineWidth = placement.MaxLineWidth
                };

                switch (LabelRows[i].Kind)
                {
                    case LabelRowKind.StructureLabel:
                        StructureLabelView = view;
                        break;
                    case LabelRowKind.Tags:
                        StructureAttributeView = view;
                        break;
                    case LabelRowKind.ID:
                        view._Color = IDColor;
                        StructureIDLabelView = view;
                        break;
                    case LabelRowKind.Parent:
                        view._Color = Color.Red;
                        ParentStructureLabelView = view;
                        break;
                }
            }
        }
    }
}
