using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;

namespace Viking.VolumeModel
{

    public static class LocationObjExtensions
    {

        /// <summary>
        /// Takes unsmoothed points and sets both the mosaic and volume shape for a locationObj
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="location"></param>
        /// <param name="volumePoints"></param>
        /// <param name="volume_innerRingPoints"></param>
        public static void SetShapeFromPointsInVolume(this WebAnnotationModel.Objects.LocationObj location, Viking.VolumeModel.IVolumeToSectionTransform mapper, Vector2[] volumePoints, ICollection<Vector2[]> volume_innerRingPoints)
        {
            Vector2[] mosaic_points = mapper.VolumeToSection(volumePoints);

            location.VolumeShape = location.TypeCode.GetSmoothedShape(volumePoints, volume_innerRingPoints).ToShape2D();
            location.MosaicShape = location.TypeCode.GetShape(mosaic_points, VolumeInnerRingPointsToSection(mapper, volume_innerRingPoints)).ToShape2D();

            return;
        }

        /// <summary>
        /// Takes unsmoothed points and sets both the mosaic and volume shape for a locationObj
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="location"></param>
        /// <param name="volumePoints"></param>
        /// <param name="volume_innerRingPoints"></param>
        public static void SetShapeFromPointsInSection(this WebAnnotationModel.Objects.LocationObj location, Viking.VolumeModel.IVolumeToSectionTransform mapper, Vector2[] sectionPoints, ICollection<Vector2[]> section_innerRingPoints)
        {
            Vector2[] volume_points = mapper.SectionToVolume(sectionPoints);

            location.VolumeShape = location.TypeCode.GetSmoothedShape(volume_points, SectionInnerRingPointsToVolume(mapper, section_innerRingPoints)).ToShape2D();
            location.MosaicShape = location.TypeCode.GetShape(sectionPoints, section_innerRingPoints).ToShape2D();

            return;
        }

        /// <summary>
        /// Takes unsmoothed points and sets both the mosaic and volume shape for a locationObj
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="location"></param>
        /// <param name="volumePoints"></param>
        /// <param name="volume_innerRingPoints"></param>
        public static void SetShapeFromGeometryInSection(this WebAnnotationModel.Objects.LocationObj location, Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            if (!shape.STIsValid().Value)
                throw new ArgumentException("Shape must be valid SQL Geometry " + shape.IsValidDetailed());

            Microsoft.SqlServer.Types.SqlGeometry volume_shape = mapper.TryMapShapeSectionToVolume(shape);

            location.VolumeShape = location.TypeCode.GetSmoothedShape(volume_shape).ToShape2D();
            location.MosaicShape = shape.ToShape2D();

            return;
        }

        /// <summary>
        /// Takes unsmoothed points and sets both the mosaic and volume shape for a locationObj
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="location"></param>
        /// <param name="volumePoints"></param>
        /// <param name="volume_innerRingPoints"></param>
        public static void SetShapeFromGeometryInVolume(this WebAnnotationModel.Objects.LocationObj location, Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry volume_shape)
        {
            if (!volume_shape.STIsValid().Value)
                throw new ArgumentException("Shape must be valid SQL Geometry " + volume_shape.IsValidDetailed());

            Microsoft.SqlServer.Types.SqlGeometry mosaic_shape = mapper.TryMapShapeVolumeToSection(volume_shape);

            location.VolumeShape = location.TypeCode.GetSmoothedShape(volume_shape).ToShape2D();
            location.MosaicShape = mosaic_shape.ToShape2D();

            return;
        }

        private static List<Vector2[]> VolumeInnerRingPointsToSection(Viking.VolumeModel.IVolumeToSectionTransform mapper, ICollection<Vector2[]> volume_innerRingPoints)
        {
            if (volume_innerRingPoints is null)
                return null;

            List<Vector2[]> mosaic_innerRingPoints = new(volume_innerRingPoints.Count);
            foreach (Vector2[] volume_ring in volume_innerRingPoints)
            {
                mosaic_innerRingPoints.Add(mapper.VolumeToSection(volume_ring));
            }

            return mosaic_innerRingPoints;
        }

        private static List<Vector2[]> SectionInnerRingPointsToVolume(Viking.VolumeModel.IVolumeToSectionTransform mapper, ICollection<Vector2[]> section_innerRingPoints)
        {
            if (section_innerRingPoints is null)
                return null;

            List<Vector2[]> volume_innerRingPoints = new(section_innerRingPoints.Count);
            foreach (Vector2[] volume_ring in section_innerRingPoints)
            {
                volume_innerRingPoints.Add(mapper.SectionToVolume(volume_ring));
            }

            return volume_innerRingPoints;
        }

    }
}
