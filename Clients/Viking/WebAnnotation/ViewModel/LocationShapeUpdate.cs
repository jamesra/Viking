using Geometry;
using SqlGeometryUtils;
using System;
using System.Diagnostics;
using System.Windows.Forms;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.UI.Controls;
using Viking.VolumeModel;
using WebAnnotationModel;

namespace WebAnnotation.ViewModel
{
    /// <summary>
    /// Persists a volume-space polygon onto a location, converting the type to CURVEPOLYGON.
    /// Used by Segment to Polygon and auto-polygonize accept; shows a MessageBox on save failure.
    /// </summary>
    internal static class LocationShapeUpdate
    {
        /// <summary>
        /// Writes <paramref name="volumePolygon"/> through the section-to-volume transform and saves.
        /// </summary>
        /// <returns>False when the location, polygon, or section is missing, or save throws.</returns>
        public static bool ApplyVolumePolygon(LocationObj modelObj, GridPolygon volumePolygon, SectionViewerControl parent)
        {
            if (modelObj is null || volumePolygon is null || parent?.Section is null)
                return false;

            try
            {
                modelObj.TypeCode = LocationType.CURVEPOLYGON;
                modelObj.SetShapeFromGeometryInVolume(
                    parent.Section.ActiveSectionToVolumeTransform,
                    volumePolygon.ToSqlGeometry());
                Store.Locations.Save();
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
    }
}
