using Geometry;
using System;
using System.Collections.Generic;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.Objects;
using Viking.Annotation.Geometry;

namespace WebAnnotationModel
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
        public static void SetShapeFromPointsInVolume(this LocationObj location, Viking.VolumeModel.IVolumeToSectionTransform mapper, GridVector2[] volumePoints, ICollection<GridVector2[]> volume_innerRingPoints)
        {
            GridVector2[] mosaic_points = mapper.VolumeToSection(volumePoints);

            location.VolumeShape = location.TypeCode.GetSmoothedShape(volumePoints, volume_innerRingPoints);
            location.MosaicShape = location.TypeCode.GetShape(mosaic_points, VolumeInnerRingPointsToSection(mapper, volume_innerRingPoints));

            return;
        }

        /// <summary>
        /// Takes unsmoothed points and sets both the mosaic and volume shape for a locationObj
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="location"></param>
        /// <param name="volumePoints"></param>
        /// <param name="volume_innerRingPoints"></param>
        public static void SetShapeFromPointsInSection(this LocationObj location, Viking.VolumeModel.IVolumeToSectionTransform mapper, GridVector2[] sectionPoints, ICollection<GridVector2[]> section_innerRingPoints)
        {
            GridVector2[] volume_points = mapper.SectionToVolume(sectionPoints);

            location.VolumeShape = location.TypeCode.GetSmoothedShape(volume_points, SectionInnerRingPointsToVolume(mapper, section_innerRingPoints));
            location.MosaicShape = location.TypeCode.GetShape(sectionPoints, section_innerRingPoints);

            return;
        }

        /// <summary>
        /// Takes unsmoothed points and sets both the mosaic and volume shape for a locationObj
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="location"></param>
        /// <param name="volumePoints"></param>
        /// <param name="volume_innerRingPoints"></param>
        public static void SetShapeFromGeometryInSection(this LocationObj location, Viking.VolumeModel.IVolumeToSectionTransform mapper, IShape2D shape)
        { 
            IShape2D volume_shape = mapper.TryMapShape(shape, TransformDirection.SectionToVolume);

            location.VolumeShape = location.TypeCode.GetSmoothedShape(volume_shape);
            location.MosaicShape = shape;

            return;
        }

        /// <summary>
        /// Takes unsmoothed points and sets both the mosaic and volume shape for a locationObj
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="location"></param>
        /// <param name="volumePoints"></param>
        /// <param name="volume_innerRingPoints"></param>
        public static void SetShapeFromGeometryInVolume(this LocationObj location, Viking.VolumeModel.IVolumeToSectionTransform mapper, IShape2D volume_shape)
        { 
            IShape2D mosaic_shape = mapper.TryMapShape(volume_shape, TransformDirection.VolumeToSection);

            location.VolumeShape = location.TypeCode.GetSmoothedShape(volume_shape);
            location.MosaicShape = mosaic_shape;

            return;
        }

        private static ICollection<GridVector2[]> VolumeInnerRingPointsToSection(Viking.VolumeModel.IVolumeToSectionTransform mapper, ICollection<GridVector2[]> volume_innerRingPoints)
        {
            if (volume_innerRingPoints == null)
                return null;

            List<GridVector2[]> mosaic_innerRingPoints = new List<GridVector2[]>(volume_innerRingPoints.Count);
            foreach (GridVector2[] volume_ring in volume_innerRingPoints)
            {
                mosaic_innerRingPoints.Add(mapper.VolumeToSection(volume_ring));
            }

            return mosaic_innerRingPoints;
        }

        private static ICollection<GridVector2[]> SectionInnerRingPointsToVolume(Viking.VolumeModel.IVolumeToSectionTransform mapper, ICollection<GridVector2[]> section_innerRingPoints)
        {
            if (section_innerRingPoints == null)
                return null;

            List<GridVector2[]> volume_innerRingPoints = new List<GridVector2[]>(section_innerRingPoints.Count);
            foreach (GridVector2[] volume_ring in section_innerRingPoints)
            {
                volume_innerRingPoints.Add(mapper.SectionToVolume(volume_ring));
            }

            return volume_innerRingPoints;
        }
    }
}
