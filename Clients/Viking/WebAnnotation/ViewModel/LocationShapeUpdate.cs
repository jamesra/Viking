using Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.UI.Controls;
using Viking.VolumeModel;
using WebAnnotation.ReviewFeed;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.ViewModel
{
    /// <summary>
    /// Persists a volume-space polygon onto a location, converting the type to CURVEPOLYGON.
    /// Used by Segment to Polygon and auto-polygonize accept; shows a MessageBox on save failure.
    /// Fits the ring once, then maps those control points back to mosaic space with the inverse owner
    /// (<see cref="LocationVolumeGeometry.ToMosaic(LocationType, Polygon, IVolumeToSectionTransform)"/>): MosaicShape is the
    /// unsmoothed control polygon and VolumeShape the forward owner's smoothed shape.
    /// </summary>
    internal static class LocationShapeUpdate
    {
        /// <summary>
        /// Simplifies <paramref name="volumePolygon"/> once, writes mosaic control points through
        /// the section transform, and saves. Boolean carve/union output is often vertex-dense;
        /// this is the choke point that keeps the database ring at CreatedShapeSimplify size.
        /// </summary>
        /// <returns>False when the location, polygon, or section is missing, a point cannot be mapped, or the save fails.</returns>
        public static Task<bool> ApplyVolumePolygon(LocationObj modelObj, Polygon volumePolygon, SectionViewerControl parent) =>
            parent?.Section is null
                ? Task.FromResult(false)
                : ApplyVolumePolygon(modelObj, volumePolygon, parent.Section.ActiveSectionToVolumeTransform, parent.Downsample);

        /// <summary>As above, for a view given by its section transform and downsample (Jotunn's scene host).</summary>
        public static async Task<bool> ApplyVolumePolygon(LocationObj modelObj, Polygon volumePolygon, IVolumeToSectionTransform transform, double downsample)
        {
            if (modelObj is null || volumePolygon is null || transform is null)
                return false;

            try
            {
                AssignSimplifiedVolumePolygon(
                    modelObj,
                    volumePolygon,
                    transform,
                    downsample);
                if (!await Store.Locations.Save())
                {
                    MessageBox.Show($"Failed to save the new shape of location {modelObj.ID}.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

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
        /// Callers that enqueue a later Save (new structure create) use this; Accept uses
        /// <see cref="ApplyVolumePolygon"/> which saves immediately.
        /// </summary>
        /// <exception cref="ArgumentException">A point of the simplified polygon cannot be mapped into the section (the
        /// message says which) or the mapped polygon is not valid; the location, its type included, is then unchanged.</exception>
        public static void AssignSimplifiedVolumePolygon(
            LocationObj modelObj,
            Polygon volumePolygon,
            IVolumeToSectionTransform transform,
            double downsample)
        {
            if (modelObj is null)
                throw new ArgumentNullException(nameof(modelObj));
            if (volumePolygon is null)
                throw new ArgumentNullException(nameof(volumePolygon));
            if (transform is null)
                throw new ArgumentNullException(nameof(transform));

            // Single save-time pass at the "Simplify Polygon" menu tolerance; no second fit follows.
            Polygon simplified = AutoPolygonizeSelection.SimplifyForSave(volumePolygon);

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
