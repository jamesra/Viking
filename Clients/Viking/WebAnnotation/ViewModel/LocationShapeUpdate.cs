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
    /// Fits the ring once, then maps those control points to mosaic space — MosaicShape stays
    /// the unsmoothed control polygon; VolumeShape is the Catmull-smoothed render copy.
    /// </summary>
    internal static class LocationShapeUpdate
    {
        /// <summary>
        /// Simplifies <paramref name="volumePolygon"/> once, writes mosaic control points through
        /// the section transform, and saves. Boolean carve/union output is often vertex-dense;
        /// this is the choke point that keeps the database ring at CreatedShapeSimplify size.
        /// </summary>
        /// <returns>False when the location, polygon, or section is missing, or save throws.</returns>
        public static bool ApplyVolumePolygon(LocationObj modelObj, Polygon volumePolygon, SectionViewerControl parent)
        {
            if (modelObj is null || volumePolygon is null || parent?.Section is null)
                return false;

            try
            {
                AssignSimplifiedVolumePolygon(
                    modelObj,
                    volumePolygon,
                    parent.Section.ActiveSectionToVolumeTransform,
                    parent.Downsample);
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
        /// One CreatedShapeSimplify fit, then mosaic control points + smoothed volume shape.
        /// Callers that enqueue a later Save (new structure create) use this; Accept uses
        /// <see cref="ApplyVolumePolygon"/> which saves immediately.
        /// </summary>
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

            Polygon simplified = AutoPolygonizeSelection.SimplifyForCreatedShape(volumePolygon, downsample);
            modelObj.TypeCode = LocationType.CURVEPOLYGON;

            ICollection<Vector2[]> interior = simplified.HasInteriorRings
                ? simplified.InteriorRings.ToList()
                : null;

            modelObj.SetShapeFromPointsInVolume(
                transform,
                simplified.ExteriorRing,
                interior);
        }
    }
}
