using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MathNet.Numerics.Statistics;

namespace Viking.VolumeModel
{

    public static class MappingExtensions
    {
        public static Microsoft.SqlServer.Types.SqlGeometry TryMapShapeSectionToVolume(this Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            List<Vector2[]> VolumeInnerRings = null;

            //Circles are represented by curve polygons.  When we map the points through a transform the results are not a circle.
            //So we special case the mapping of circles
            if (shape.GeometryType() == SupportedGeometryType.CURVEPOLYGON)
            {
                return TryMapCurvePolygonSectionToVolume(mapper, shape);
            }

            Vector2[] points = shape.ToPoints();

            bool[] mappedPosition = mapper.TrySectionToVolume(points, out Vector2[] VolumePositions);
            if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
            {
                Trace.WriteLine("MapShapeSectionToVolume: Shape #" + shape.ToString() + " was unmappable.", "WebAnnotation");
                return null;
            }

            if (shape.HasInteriorRings())
            {
                List<Vector2[]> innerRings = shape.InteriorRingPoints();
                VolumeInnerRings = new List<Vector2[]>(innerRings.Count);

                foreach (Vector2[] innerRing in innerRings)
                {
                    mappedPosition = mapper.TrySectionToVolume(innerRing, out Vector2[] VolumeRingPositions);
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

            List<Vector2[]> SectionInnerRings = null;
            Vector2[] points = shape.ToPoints();

            bool[] mappedPosition = mapper.TryVolumeToSection(points, out Vector2[] SectionPositions);
            if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
            {
                Trace.WriteLine("TryMapShapeVolumeToSection: Shape #" + shape.ToString() + " was unmappable.", "WebAnnotation");
                return null;
            }

            if (shape.HasInteriorRings())
            {
                List<Vector2[]> innerRings = shape.InteriorRingPoints();
                SectionInnerRings = new List<Vector2[]>(innerRings.Count);

                foreach (Vector2[] innerRing in innerRings)
                {
                    mappedPosition = mapper.TryVolumeToSection(innerRing, out Vector2[] SectionRingPositions);
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
        /// This maps the centre and four rim points and averages the scale they give, so the radius follows the transform (see <see cref="Geometry.CircleMapping"/>).
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="shape"></param>
        /// <returns></returns>
        private static Microsoft.SqlServer.Types.SqlGeometry TryMapCurvePolygonSectionToVolume(this Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry shape) => TryMapCurvePolygonCircle(mapper, shape, useSectionToVolumeDirection: true);

        /// <summary>
        /// In Viking CURVEPOLYGONS are always circles.  When we map the points through a transform the results are not a circle.
        /// This maps the centre and four rim points and averages the scale they give, so the radius follows the transform (see <see cref="Geometry.CircleMapping"/>).
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="shape"></param>
        /// <returns></returns>
        private static Microsoft.SqlServer.Types.SqlGeometry TryMapCurvePolygonVolumeToSection(this Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry shape) => TryMapCurvePolygonCircle(mapper, shape, useSectionToVolumeDirection: false);

        private static Microsoft.SqlServer.Types.SqlGeometry TryMapCurvePolygonCircle(
            this Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry shape,
            bool useSectionToVolumeDirection)
        {
            if (shape.GeometryType() != SupportedGeometryType.CURVEPOLYGON)
            {
                throw new ArgumentException("CURVEPOLYGON shape argument required");
            }

            // The ring of a circle is symmetric about its centre, so the centroid is the centre and the bounding box spans the diameter.
            Vector2 center = shape.Centroid();
            Rectangle bbox = shape.BoundingBox();
            double radius = Math.Max(bbox.Width, bbox.Height) / 2.0;

            // Five-point scale rule (see CircleMapping): the radius follows the transform's scale.
            Circle source = new(center, radius);
            Circle? mapped = useSectionToVolumeDirection ? mapper.TryMapShapeSectionToVolume(source) : mapper.TryMapShapeVolumeToSection(source);
            if (mapped is not Circle result)
            {
                Trace.WriteLine("TryMapCurvePolygonCircle: Shape #" + shape.ToString() + " was unmappable.", "WebAnnotation");
                return null;
            }

            return SqlGeometryUtils.Extensions.ToCircle(result.Center.X, result.Center.Y, 0, result.Radius);
        }
    }
}
