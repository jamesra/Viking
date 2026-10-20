#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VikingXNAGraphics;
using WebAnnotation.View;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI
{
    /// <summary>
    /// Re-rolls the colors cells are drawn with. Display only: a cell's color is a client-side value (a hash of its
    /// structure id until it is set), it is never sent to the database, and reloading the volume restores the
    /// original colors.
    /// </summary>
    /// <remarks>
    /// Only cells (structure type 1) draw with a per-structure color; every other structure uses its type's color,
    /// so these commands leave them alone. Call from the UI thread: they edit the colors of views the overlay is
    /// drawing. Views the overlay has not built yet pick the new color up from the structure when they are built.
    /// </remarks>
    internal static class StructureColors
    {
        /// <summary>Structure type id of a cell, the type whose polygons and circles use the structure's own color.</summary>
        private const long CellTypeId = 1;

        private static readonly Random Rng = new();

        /// <summary>
        /// Gives every loaded cell a new random color and recolors the views already built for them.
        /// Backs the Annotation menu's "Randomize All Colors".
        /// </summary>
        /// <returns>How many cells were recolored.</returns>
        public static int RandomizeAll()
        {
            HashSet<long> changed = [];
            foreach (StructureObj structure in Store.Structures.CachedObjects)
            {
                if (structure.TypeID != CellTypeId)
                    continue;

                structure.Color = StructureObj.RandomColor(Rng);
                changed.Add(structure.ID);
            }

            ApplyToLoadedViews(changed);
            return changed.Count;
        }

        /// <summary>
        /// Gives one cell a new random color and recolors its views. Backs the structure menu's "New Color".
        /// Does nothing for a structure that is not a cell.
        /// </summary>
        public static void NewColor(StructureObj structure)
        {
            if (structure is null)
                throw new ArgumentNullException(nameof(structure));
            if (structure.TypeID != CellTypeId)
                return;

            structure.Color = StructureObj.RandomColor(Rng);
            ApplyToLoadedViews(new HashSet<long> { structure.ID });
        }

        /// <summary>
        /// Sets the color of every polygon, circle and curve view already built for the given cells, keeping each
        /// view's current opacity, then asks the viewer to redraw.
        /// </summary>
        private static void ApplyToLoadedViews(ISet<long> structureIds)
        {
            if (structureIds.Count == 0 || Viking.UI.State.volume?.SectionViewModels is null)
                return;

            foreach (int sectionNumber in Viking.UI.State.volume.SectionViewModels.Keys.ToArray())
            {
                SectionAnnotationsView? sectionView = AnnotationOverlay.GetAnnotationsForSection(sectionNumber);
                if (sectionView is null)
                    continue;

                foreach (LocationCanvasView view in sectionView.GetLocations().ToArray())
                {
                    if (view is not IColorView colorView ||
                        view is not (LocationPolygonView or LocationCircleView or LocationCurveView) ||
                        view.ParentID is not long structureId ||
                        !structureIds.Contains(structureId) ||
                        !Store.Structures.TryGetObjectByID(structureId, out StructureObj parent))
                    {
                        continue;
                    }

                    try
                    {
                        colorView.Color = LocationCanvasView.ColorForStructure(parent, colorView.Color.GetAlpha());
                    }
                    catch (Exception ex)
                    {
                        // A view that is still being built can refuse a color change; it takes the structure's new color when it finishes.
                        Debug.WriteLine($"Could not recolor location {view.ID}: {ex.Message}");
                    }
                }
            }

            AnnotationOverlay.CurrentOverlay?.InvalidateParent();
        }
    }
}
