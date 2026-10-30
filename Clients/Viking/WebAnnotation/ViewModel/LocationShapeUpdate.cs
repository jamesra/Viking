using Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.UI.Controls;
using Viking.VolumeModel;
using WebAnnotation.ReviewFeed;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotationModel;

namespace WebAnnotation.ViewModel
{
    /// <summary>
    /// Persists a volume-space polygon onto a location, converting the type to CURVEPOLYGON.
    /// Used by Segment to Polygon and auto-polygonize accept; shows a MessageBox on save failure.
    /// Fits the ring once, then maps those control points back to mosaic space with the inverse owner
    /// (<see cref="LocationVolumeGeometry.ToMosaic(LocationType, Polygon, IVolumeToSectionTransform)"/>): MosaicShape is the
    /// unsmoothed control polygon and VolumeShape the forward owner's smoothed shape. A result with any point outside the
    /// section's transform is refused and nothing is saved.
    /// </summary>
    internal static class LocationShapeUpdate
    {
        /// <summary>
        /// Simplifies <paramref name="volumePolygon"/> once, writes mosaic control points through
        /// the section transform, and saves. Boolean carve/union output is often vertex-dense;
        /// this is the choke point that keeps the database ring at CreatedShapeSimplify size.
        /// </summary>
        /// <returns>False when the location, polygon, or section is missing, a point cannot be mapped, or save throws.</returns>
        public static bool ApplyVolumePolygon(
            LocationObj modelObj,
            Polygon volumePolygon,
            SectionViewerControl parent,
            IReadOnlyList<Vector2>? mustContain = null)
        {
            if (modelObj is null || volumePolygon is null || parent?.Section is null)
                return false;

            try
            {
                AssignSimplifiedVolumePolygon(
                    modelObj,
                    volumePolygon,
                    parent.Section.ActiveSectionToVolumeTransform,
                    parent.Downsample,
                    mustContain);
                Store.Locations.Save();
                // In-place shape updates do not raise Location CollectionChanged Add/Replace.
                ReviewChangeFeedIngress.PushUpsert(modelObj);
                Debug.WriteLine($"Successfully converted location {modelObj.ID} to polygon");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating location shape: {ex.Message}");
                MessageBox.Show($"Failed to update location shape: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// One <see cref="AutoPolygonizeSelection.SimplifyForSave"/> pass (the context-menu
        /// "Simplify Polygon" tolerance), then mosaic control points + smoothed volume shape.
        /// <paramref name="downsample"/> is retained for callers but no longer affects the fit.
        /// <paramref name="mustContain"/> (the user's green points) must stay inside the reduced ring; see <see cref="AutoPolygonizeSelection.SimplifyForSave"/>.
        /// Callers that enqueue a later Save (new structure create) use this; Accept uses
        /// <see cref="ApplyVolumePolygon"/> which saves immediately.
        /// </summary>
        /// <exception cref="ArgumentException">A point of the simplified polygon cannot be mapped into the section (the
        /// message says which) or the mapped polygon is not valid; the location, its type included, is then unchanged.</exception>
        public static void AssignSimplifiedVolumePolygon(
            LocationObj modelObj,
            Polygon volumePolygon,
            IVolumeToSectionTransform transform,
            double downsample,
            IReadOnlyList<Vector2>? mustContain = null)
        {
            if (modelObj is null)
                throw new ArgumentNullException(nameof(modelObj));
            if (volumePolygon is null)
                throw new ArgumentNullException(nameof(volumePolygon));
            if (transform is null)
                throw new ArgumentNullException(nameof(transform));

            // Single save-time pass at the "Simplify Polygon" menu tolerance; no second fit follows.
            Polygon simplified = AutoPolygonizeSelection.SimplifyForSave(volumePolygon, mustContain);

            // Mapped before anything changes, so a refused result leaves the location as it was.
            MosaicGeometry mosaic = LocationVolumeGeometry.ToMosaic(LocationType.CURVEPOLYGON, simplified, transform);
            if (!mosaic.Succeeded)
                throw new ArgumentException(mosaic.Failure);

            LocationType originalType = modelObj.TypeCode;
            modelObj.TypeCode = LocationType.CURVEPOLYGON;
            try
            {
                modelObj.TryApply(mosaic, transform);
            }
            catch (ArgumentException)
            {
                modelObj.TypeCode = originalType;
                throw;
            }
        }
    }
}
