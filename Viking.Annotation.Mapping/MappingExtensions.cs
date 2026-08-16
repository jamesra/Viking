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
                    ? mapper.SectionToVolume(p.ToVector2())
                    : mapper.VolumeToSection(p.ToVector2());
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
            else if (shape is IRectangle2D rect)
            {
                return mapper.TryMapRectangle(rect, direction);
            }

            throw new NotImplementedException($"Shape does not have an interface that can be mapped {shape}");
        }

        private static IShape2D TryMapRectangle(this Viking.VolumeModel.IVolumeToSectionTransform mapper, IRectangle2D shape, TransformDirection direction)
        { 
            Rectangle r = shape.ToRectangle();
            Vector2[] points = r.Corners;
            Vector2[] mappedPoints;

            bool[] mappedPosition = direction == TransformDirection.SectionToVolume ?
                mapper.TrySectionToVolume(points, out mappedPoints) :
                mapper.TryVolumeToSection(points, out mappedPoints);

            if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
            {
                Trace.WriteLine($"TryMapShapeVolumeToSection: Shape #{shape} was unmappable.", "WebAnnotation");
                return null;
            }

            return new Polygon(mappedPoints.EnsureClosedRing());
        }

        private static ITriangle2D TryMapTriangle(this Viking.VolumeModel.IVolumeToSectionTransform mapper, ITriangle2D shape, TransformDirection direction)
        {
            Vector2[] points = shape.Points.ToVector2();
            Vector2[] mappedPoints; 

            bool[] mappedPosition = direction == TransformDirection.SectionToVolume ?
                mapper.TrySectionToVolume(points, out mappedPoints) :
                mapper.TryVolumeToSection(points, out mappedPoints);

            if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
            {
                Trace.WriteLine($"TryMapShapeVolumeToSection: Shape #{shape} was unmappable.", "WebAnnotation");
                return null;
            }

            return new Triangle(mappedPoints);
        }

        private static IPolyLine2D TryMapPolyline(this Viking.VolumeModel.IVolumeToSectionTransform mapper, IPolyLine2D shape, TransformDirection direction)
        {
            Vector2[] points = shape.Points.ToVector2();
            Vector2[] mappedPoints; 

            bool[] mappedPosition = direction == TransformDirection.SectionToVolume ?
                mapper.TrySectionToVolume(points, out mappedPoints) :
                mapper.TryVolumeToSection(points, out mappedPoints);

            if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
            {
                Trace.WriteLine($"TryMapShapeVolumeToSection: Shape #{shape} was unmappable.", "WebAnnotation");
                return null;
            }

            return new Polyline(mappedPoints, false);
        }

        private static IPolygon2D TryMapPolygon(this Viking.VolumeModel.IVolumeToSectionTransform mapper, IPolygon2D shape, TransformDirection direction)
        {
            List<Vector2[]> mappedInteriorRings = null; 
            Vector2[] points = shape.ExteriorRing.ToVector2();

            Vector2[] mappedPoints; 

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
                mappedInteriorRings = new List<Vector2[]>(shape.InteriorRings.Count);

                foreach (var innerRing in shape.InteriorRings)
                {
                    Vector2[] sectionRingPositions; 

                    mappedPosition = direction == TransformDirection.SectionToVolume ?
                        mapper.TrySectionToVolume(innerRing.ToVector2(), out sectionRingPositions) :
                        mapper.TryVolumeToSection(innerRing.ToVector2(), out sectionRingPositions);
                     
                    if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
                    {
                        Trace.WriteLine($"TryMapShapeVolumeToSection: Shape #{shape} inner ring was unmappable.", "WebAnnotation");
                        return null;
                    }

                    mappedInteriorRings.Add(sectionRingPositions);
                }
            }

            return new Polygon(mappedPoints, mappedInteriorRings);
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
            if (shape.ShapeType != ShapeType2D.Circle)
            {
                throw new ArgumentException($"{nameof(shape.ShapeType)} must be {nameof(ShapeType2D.Circle)}");
            }

            Vector2 center = ((ICentroid)shape).Centroid.ToVector2(); 
            Rectangle bbox = shape.BoundingBox;

            //In some cases the transform can have significant distortions corrected.  To handle this we map points on the circle at the cardinal directions and then recalculate the radius
            var points = new Vector2[]
            {
                center,
                new Vector2(bbox.Left, center.Y),
                new Vector2(center.X, bbox.Bottom),
                new Vector2(bbox.Right, center.Y),
                new Vector2(center.X, bbox.Top)
            };

            Vector2[] mappedPoints;
            bool[] mappedCorrectly = direction == TransformDirection.SectionToVolume ? 
                mapper.TrySectionToVolume(points, out mappedPoints) : 
                mapper.TryVolumeToSection(points, out mappedPoints);
                
            if (!mappedCorrectly[0])
            {
                Trace.WriteLine($"TryMapCurvePolygonSectionToVolume: Shape #{shape} was unmappable.", "WebAnnotation");
                return null;
            }

            Vector2 mappedCenter = mappedPoints[0];

            //Take the median radius measurement from the four cardinal points to adjust the radius of the circle for the transformation
            double radiiSquared = mappedPoints.Where((p, i) => i > 0 && mappedCorrectly[i]).Select(p => Vector2.DistanceSquared(mappedCenter, p)).Median();
            double radius = Math.Sqrt(radiiSquared);
              
            return new Circle(mappedCenter, radius);
        }
    }
}
