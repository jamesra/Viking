using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using SqlGeometryUtils;
using System.Diagnostics;

namespace Viking.VolumeModel
{

    public static class MappingExtensions
    {
        /// <summary>
        /// A faster mapping technique for geometries that do not use control points such as circles and points.
        /// </summary>
        /// <param name="loc"></param>
        /// <returns></returns>
        private static bool MapLocationCentroidToVolume(this Viking.VolumeModel.IVolumeToSectionTransform mapper, WebAnnotationModel.LocationObj loc)
        {
            //Don't bother mapping if the location was already mapped
            if (loc.VolumeTransformID == mapper.ID)
                return true;

            GridVector2 VolumePosition = new GridVector2(-1, -1);

            bool mappedPosition = mapper.TrySectionToVolume(loc.Position, out VolumePosition);
            if (!mappedPosition) //Remove locations we can't map
            {
                Trace.WriteLine("MapLocationToVolumeByCentroid: Location #" + loc.ID.ToString() + " was unmappable.", "WebAnnotation");
                return false;
            }

            loc.VolumeTransformID = mapper.ID;
            if (VolumePosition != loc.VolumePosition)
                loc.VolumeShape = loc.VolumeShape.MoveTo(VolumePosition);

            //loc.VolumePosition = VolumePosition;

            return true;
        }

        public static Microsoft.SqlServer.Types.SqlGeometry TryMapShapeSectionToVolume(this Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            GridVector2[] VolumePositions;
            ICollection<GridVector2[]> VolumeInnerRings = null;
            GridVector2[] points = shape.ToPoints();

            bool[] mappedPosition = mapper.TrySectionToVolume(points, out VolumePositions);
            if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
            {
                Trace.WriteLine("MapShapeSectionToVolume: Shape #" + shape.ToString() + " was unmappable.", "WebAnnotation");
                return null;
            }

            if (shape.HasInteriorRings())
            {
                ICollection<GridVector2[]> innerRings = shape.InteriorRingPoints();
                VolumeInnerRings = new List<GridVector2[]>(innerRings.Count);

                foreach (GridVector2[] innerRing in innerRings)
                {
                    GridVector2[] VolumeRingPositions;
                    mappedPosition = mapper.TrySectionToVolume(innerRing, out VolumeRingPositions);
                    if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
                    {
                        Trace.WriteLine("TryMapShapeSectionToVolume: Shape #" + shape.ToString() + " inner ring was unmappable.", "WebAnnotation");
                        return null;
                    }

                    VolumeInnerRings.Add(VolumeRingPositions);
                }
            }

            return SqlGeometryUtils.Extensions.ToGeometry(shape.GeometryType(), VolumePositions, VolumeInnerRings);
        }

        public static Microsoft.SqlServer.Types.SqlGeometry TryMapShapeVolumeToSection(this Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            GridVector2[] SectionPositions;
            ICollection<GridVector2[]> SectionInnerRings = null;
            GridVector2[] points = shape.ToPoints();

            bool[] mappedPosition = mapper.TryVolumeToSection(points, out SectionPositions);
            if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
            {
                Trace.WriteLine("TryMapShapeVolumeToSection: Shape #" + shape.ToString() + " was unmappable.", "WebAnnotation");
                return null;
            }

            if (shape.HasInteriorRings())
            {
                ICollection<GridVector2[]> innerRings = shape.InteriorRingPoints();
                SectionInnerRings = new List<GridVector2[]>(innerRings.Count);

                foreach (GridVector2[] innerRing in innerRings)
                {
                    GridVector2[] SectionRingPositions;
                    mappedPosition = mapper.TryVolumeToSection(innerRing, out SectionRingPositions);
                    if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
                    {
                        Trace.WriteLine("TryMapShapeVolumeToSection: Shape #" + shape.ToString() + " inner ring was unmappable.", "WebAnnotation");
                        return null;
                    }

                    SectionInnerRings.Add(SectionRingPositions);
                }
            }

            return SqlGeometryUtils.Extensions.ToGeometry(shape.GeometryType(), SectionPositions, SectionInnerRings);
        }
    }
}
