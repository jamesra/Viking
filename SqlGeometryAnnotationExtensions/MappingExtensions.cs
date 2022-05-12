using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WebAnnotationModel.Objects;
using MathNet.Numerics.Statistics;

namespace Viking.VolumeModel
{

    public static class MappingExtensions
    {

        /// <summary>
        /// A faster mapping technique for geometries that do not use control points such as circles and points.
        /// </summary>
        /// <param name="loc"></param>
        /// <returns></returns>
        private static bool MapLocationCentroidToVolume(this Viking.VolumeModel.IVolumeToSectionTransform mapper, LocationObj loc)
        {
            throw new NotImplementedException();
            /*
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
            */
        }

        public static Microsoft.SqlServer.Types.SqlGeometry TryMapShapeSectionToVolume(this Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            GridVector2[] VolumePositions;
            ICollection<GridVector2[]> VolumeInnerRings = null;

            //Circles are represented by curve polygons.  When we map the points through a transform the results are not a circle.
            //So we special case the mapping of circles
            if (shape.GeometryType() == SupportedGeometryType.CURVEPOLYGON)
            {
                return TryMapCurvePolygonSectionToVolume(mapper, shape);
            }

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
            //Circles are represented by curve polygons.  When we map the points through a transform the results are not a circle.
            //So we special case the mapping of circles
            if (shape.GeometryType() == SupportedGeometryType.CURVEPOLYGON)
            {
                return TryMapCurvePolygonVolumeToSection(mapper, shape);
            }

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
         
        /// <summary>
        /// In Viking CURVEPOLYGONS are always circles.  When we map the points through a transform the results are not a circle. 
        /// This function maps the center and preserves the radius, at the cost of not adapting the radius.  
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="shape"></param>
        /// <returns></returns>
        private static Microsoft.SqlServer.Types.SqlGeometry TryMapCurvePolygonSectionToVolume(this Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            return TryMapCurvePolygonCircle(mapper, shape, useSectionToVolumeDirection: true);
        }

        /// <summary>
        /// In Viking CURVEPOLYGONS are always circles.  When we map the points through a transform the results are not a circle. 
        /// This function maps the center and preserves the radius, at the cost of not adapting the radius.
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="shape"></param>
        /// <returns></returns>
        private static Microsoft.SqlServer.Types.SqlGeometry TryMapCurvePolygonVolumeToSection(this Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            return TryMapCurvePolygonCircle(mapper, shape, useSectionToVolumeDirection: false);
        }

        private static Microsoft.SqlServer.Types.SqlGeometry TryMapCurvePolygonCircle(
            this Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry shape,
            bool useSectionToVolumeDirection)
        {
            if (shape.GeometryType() != SupportedGeometryType.CURVEPOLYGON)
            {
                throw new ArgumentException("CURVEPOLYGON shape argument required");
            }

            GridVector2 center = shape.Centroid(); 
            GridRectangle bbox = shape.BoundingBox();

            //In some cases the transform can have significant distortions corrected.  To handle this we map points on the circle at the cardinal directions and then recalculate the radius
            var points = new GridVector2[]
            {
                center,
                new GridVector2(bbox.Left, center.Y),
                new GridVector2(center.X, bbox.Bottom),
                new GridVector2(bbox.Right, center.Y),
                new GridVector2(center.X, bbox.Top)
            };

            GridVector2[] mappedPoints;
            bool[] mappedCorrectly = useSectionToVolumeDirection ? 
                mapper.TrySectionToVolume(points, out mappedPoints) : 
                mapper.TryVolumeToSection(points, out mappedPoints);
                
            if (!mappedCorrectly[0])
            {
                Trace.WriteLine("TryMapCurvePolygonSectionToVolume: Shape #" + shape.ToString() + " was unmappable.", "WebAnnotation");
                return null;
            }

            GridVector2 mappedCenter = mappedPoints[0];

            double radiiSquared = mappedPoints.Where((p, i) => i > 0 && mappedCorrectly[i]).Select(p => GridVector2.DistanceSquared(mappedCenter, p)).Median();
            double radius = Math.Sqrt(radiiSquared);
              
            return SqlGeometryUtils.Extensions.ToCircle(mappedCenter.X, mappedCenter.Y, 0, radius);
        }
    }
}
