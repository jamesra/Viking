using Geometry; 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;  
using MathNet.Numerics.Statistics;

namespace WebAnnotationModel
{
    public enum TransformDirection
    {
        SectionToVolume,
        VolumeToSection
    }

    public static class MappingExtensions
    {   
        public static IShape2D TryMapShape(this Viking.VolumeModel.IVolumeToSectionTransform mapper, IShape2D shape,
            TransformDirection direction)
        {
            if (shape is null) throw new ArgumentNullException(nameof(shape));

            if (shape is IPoint2D p)
            {
                return direction == TransformDirection.SectionToVolume
                    ? mapper.SectionToVolume(p.ToGridVector2())
                    : mapper.VolumeToSection(p.ToGridVector2());
            }
            else if (shape is IPolygon2D polygon)
            {
                return mapper.TryMapPolygon(polygon, direction);
            }
            else if (shape is ICircle2D circle)
            {
                return mapper.TryMapCurvePolygonCircle(circle, direction);
            }
            else if (shape is IPolyLine2D polyLine)
            {
                return mapper.TryMapPolyline(polyLine, direction);
            }
            else if (shape is ITriangle2D tri)
            {
                return mapper.TryMapTriangle(tri, direction);
            }
            else if (shape is IRectangle rect)
            {
                return mapper.TryMapRectangle(rect, direction);
            }

            throw new NotImplementedException($"Shape does not have an interface that can be mapped {shape}");
        }

        private static IShape2D TryMapRectangle(this Viking.VolumeModel.IVolumeToSectionTransform mapper, IRectangle shape, TransformDirection direction)
        { 
            GridRectangle r = shape.ToGridRectangle();
            GridVector2[] points = r.Corners;
            GridVector2[] mappedPoints;

            bool[] mappedPosition = direction == TransformDirection.SectionToVolume ?
                mapper.TrySectionToVolume(points, out mappedPoints) :
                mapper.TryVolumeToSection(points, out mappedPoints);

            if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
            {
                Trace.WriteLine($"TryMapShapeVolumeToSection: Shape #{shape} was unmappable.", "WebAnnotation");
                return null;
            }

            return new GridPolygon(mappedPoints.EnsureClosedRing());
        }

        private static ITriangle2D TryMapTriangle(this Viking.VolumeModel.IVolumeToSectionTransform mapper, ITriangle2D shape, TransformDirection direction)
        {
            GridVector2[] points = shape.Points.ToGridVector2();
            GridVector2[] mappedPoints; 

            bool[] mappedPosition = direction == TransformDirection.SectionToVolume ?
                mapper.TrySectionToVolume(points, out mappedPoints) :
                mapper.TryVolumeToSection(points, out mappedPoints);

            if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
            {
                Trace.WriteLine($"TryMapShapeVolumeToSection: Shape #{shape} was unmappable.", "WebAnnotation");
                return null;
            }

            return new GridTriangle(mappedPoints);
        }

        private static IPolyLine2D TryMapPolyline(this Viking.VolumeModel.IVolumeToSectionTransform mapper, IPolyLine2D shape, TransformDirection direction)
        {
            GridVector2[] points = shape.Points.ToGridVector2();
            GridVector2[] mappedPoints; 

            bool[] mappedPosition = direction == TransformDirection.SectionToVolume ?
                mapper.TrySectionToVolume(points, out mappedPoints) :
                mapper.TryVolumeToSection(points, out mappedPoints);

            if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
            {
                Trace.WriteLine($"TryMapShapeVolumeToSection: Shape #{shape} was unmappable.", "WebAnnotation");
                return null;
            }

            return new GridPolyline(mappedPoints, false);
        }

        private static IPolygon2D TryMapPolygon(this Viking.VolumeModel.IVolumeToSectionTransform mapper, IPolygon2D shape, TransformDirection direction)
        {
            List<GridVector2[]> mappedInteriorRings = null; 
            GridVector2[] points = shape.ExteriorRing.ToGridVector2();

            GridVector2[] mappedPoints; 

            bool[] mappedPosition = direction == TransformDirection.SectionToVolume ?
                mapper.TrySectionToVolume(points, out mappedPoints) :
                mapper.TryVolumeToSection(points, out mappedPoints);

            if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
            {
                Trace.WriteLine($"TryMapShapeVolumeToSection: Shape #{shape} was unmappable.", "WebAnnotation");
                return null;
            }

            
            if (shape.InteriorRings.Any())
            { 
                mappedInteriorRings = new List<GridVector2[]>(shape.InteriorRings.Count);

                foreach (var innerRing in shape.InteriorRings)
                {
                    GridVector2[] sectionRingPositions; 

                    mappedPosition = direction == TransformDirection.SectionToVolume ?
                        mapper.TrySectionToVolume(innerRing.ToGridVector2(), out sectionRingPositions) :
                        mapper.TryVolumeToSection(innerRing.ToGridVector2(), out sectionRingPositions);
                     
                    if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
                    {
                        Trace.WriteLine($"TryMapShapeVolumeToSection: Shape #{shape} inner ring was unmappable.", "WebAnnotation");
                        return null;
                    }

                    mappedInteriorRings.Add(sectionRingPositions);
                }
            }

            return new GridPolygon(mappedPoints, mappedInteriorRings);
        }
         
        /// <summary>
        /// In Viking CURVEPOLYGONS are always circles.  When we map the points through a transform the results are not a circle. 
        /// This function maps the center and preserves the radius, at the cost of not adapting the radius.  
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="shape"></param>
        /// <returns></returns>
        private static IShape2D TryMapCurvePolygonSectionToVolume(this Viking.VolumeModel.IVolumeToSectionTransform mapper, IShape2D shape)
        {
            return TryMapCurvePolygonCircle(mapper, shape, TransformDirection.SectionToVolume);
        }

        /// <summary>
        /// In Viking CURVEPOLYGONS are always circles.  When we map the points through a transform the results are not a circle. 
        /// This function maps the center and preserves the radius, at the cost of not adapting the radius.
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="shape"></param>
        /// <returns></returns>
        private static IShape2D TryMapCurvePolygonVolumeToSection(this Viking.VolumeModel.IVolumeToSectionTransform mapper, IShape2D shape)
        {
            return TryMapCurvePolygonCircle(mapper, shape, TransformDirection.VolumeToSection);
        }

        private static ICircle2D TryMapCurvePolygonCircle(
            this Viking.VolumeModel.IVolumeToSectionTransform mapper, IShape2D shape,
            TransformDirection direction)
        {
            if (shape.ShapeType != ShapeType2D.CURVEPOLYGON || shape.ShapeType != ShapeType2D.CIRCLE)
            {
                throw new ArgumentException($"{nameof(shape.ShapeType)} must be {nameof(ShapeType2D.CURVEPOLYGON)} or {nameof(ShapeType2D.CIRCLE)}");
            }

            GridVector2 center = shape.Centroid; 
            GridRectangle bbox = shape.BoundingBox;

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
            bool[] mappedCorrectly = direction == TransformDirection.SectionToVolume ? 
                mapper.TrySectionToVolume(points, out mappedPoints) : 
                mapper.TryVolumeToSection(points, out mappedPoints);
                
            if (!mappedCorrectly[0])
            {
                Trace.WriteLine($"TryMapCurvePolygonSectionToVolume: Shape #{shape} was unmappable.", "WebAnnotation");
                return null;
            }

            GridVector2 mappedCenter = mappedPoints[0];

            //Take the median radius measurement from the four cardinal points to adjust the radius of the circle for the transformation
            double radiiSquared = mappedPoints.Where((p, i) => i > 0 && mappedCorrectly[i]).Select(p => GridVector2.DistanceSquared(mappedCenter, p)).Median();
            double radius = Math.Sqrt(radiiSquared);
              
            return new GridCircle(mappedCenter, radius);
        }
    }
}
