using Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Viking.VolumeModel
{
    public static class RectangleMappingExtensions
    {
        public static GridRectangle? ApproximateVisibleMosaicBounds(this in GridRectangle VisibleWorldBounds, IVolumeToSectionTransform mapper)
        {
            GridVector2[] VolumeRectCorners = new GridVector2[] { VisibleWorldBounds.LowerLeft, VisibleWorldBounds.LowerRight, VisibleWorldBounds.UpperLeft, VisibleWorldBounds.UpperRight };
            GridVector2[] MosaicRectCorners;
            bool[] mapped = mapper.TryVolumeToSection(VolumeRectCorners, out MosaicRectCorners);

            GridVector2[] MappedMosaicCorners = MosaicRectCorners.Where((p, i) => mapped[i]).ToArray();

            if (MappedMosaicCorners.Length == 4)
            {
                //If we map at least three corners we know we can construct a reasonable approximation of the correct rectangle in mosaic space
                double MinX = MappedMosaicCorners.Min(p => p.X);
                double MaxX = MappedMosaicCorners.Max(p => p.X);
                double MaxY = MappedMosaicCorners.Max(p => p.Y);
                double MinY = MappedMosaicCorners.Min(p => p.Y);

                return new GridRectangle(MinX, MaxX, MinY, MaxY);
            }
            else if (MappedMosaicCorners.Length > 0)
            {
                //We mapped one or two points but not opposite corners.  Guesstimate the region by using the width/height in volume space since we know the mappings have minimal distortion.
                return EstimateMosaicRectangle(in mapped, in MosaicRectCorners, in VisibleWorldBounds);
            }
            else
            {
                if (VisibleWorldBounds.Contains(mapper.VolumeBounds.Value))
                {
                    return mapper.SectionBounds;
                }
                //All four points are outside the control points.  Return the bounding box of the mapped space.
                //This check is a hack.  When we are zoomed out and can only see a sliver of the volume we need a way to load the visible tiles.
                else if (VisibleWorldBounds.Intersects(mapper.VolumeBounds.Value) && ((VisibleWorldBounds.Width > mapper.VolumeBounds.Value.Width / 2.0) || (VisibleWorldBounds.Height > mapper.VolumeBounds.Value.Height / 2.0)))
                {
                    return mapper.SectionBounds;
                }
                else
                {
                    //We must be past the convex hull with no overlap
                    return new GridRectangle?();
                    //return mapper.SectionBounds;
                }
            }
        }

        /// <summary>
        /// Assuming an array of length 4, with order (LowerLeft, LowerRight, UpperLeft, UpperLeft) with true values where points are mapped.
        /// Find or approximate the lower-left point
        /// </summary>
        /// <param name="mappedCorners"></param>
        /// <returns></returns>
        private static GridRectangle? EstimateMosaicRectangle(in bool[] IsMapped, in GridVector2[] points, in GridRectangle VisibleWorldBounds)
        {
            //If we map at least three corners we know we can construct a reasonable approximation of the correct rectangle in mosaic space
            bool[] tempMapped = IsMapped;
            GridVector2[] ValidPoints = points.Where((p, i) => tempMapped[i]).ToArray();
            double MinX = ValidPoints.Min(p => p.X);
            double MaxX = ValidPoints.Max(p => p.X);
            double MaxY = ValidPoints.Max(p => p.Y);
            double MinY = ValidPoints.Min(p => p.Y);

            //We don't know the rotation of the rectangle.  We assume the worst case of a 45 degree angle so we multiply width or height by 1.44
            //So we create a grid circle at the point with the radius of Max(Width,Height).  Then we return the bounding box of the GridCircle

            if (IsMapped.Count(b => b) == 3)
            {
                return GridCircle.CircleFromThreePoints(ValidPoints).BoundingBox;
            }

            if (OppositeCornersMapped(IsMapped))
            {
                //Find the center of the opposite corners and the distance.  Create a circle and return the bounding box.
                if (IsMapped[0] && IsMapped[2])
                {
                    return CircleFromTwoPoints(points[0], points[2]).BoundingBox;
                }
                else
                {
                    return CircleFromTwoPoints(points[1], points[3]).BoundingBox;
                }
            }

            //Reaching here means only one point is mapped or two points on the same edge
            double CircleRadius = Math.Max(VisibleWorldBounds.Width, VisibleWorldBounds.Height) * 1.44; //Sqrt(2)
            for (int iPoint = 0; iPoint < points.Length; iPoint++)
            {
                if (IsMapped[iPoint])
                {
                    return new GridCircle(points[iPoint], CircleRadius).BoundingBox;
                }
            }

            return new GridRectangle?();
        }

        /// <summary>
        /// Return a circle bisected by points A, B with line AB passing through the center of the circle
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        private static GridCircle CircleFromTwoPoints(GridVector2 A, GridVector2 B)
        {
            double Distance = GridVector2.Distance(A, B);
            double X = (A.X + B.X) / 2.0;
            double Y = (A.Y + B.Y) / 2.0;

            return new GridCircle(new GridVector2(X, Y), Distance / 2.0);
        }

        /// <summary>
        /// Assuming an array of length 4, with order (LowerLeft, LowerRight, UpperLeft, UpperLeft) returns true if opposite corners are true
        /// </summary>
        /// <param name="mappedCorners"></param>
        /// <returns></returns>
        private static bool OppositeCornersMapped(bool[] mappedCorners)
        {
            return (mappedCorners[0] && mappedCorners[2]) || (mappedCorners[1] && mappedCorners[3]);
        }
    }

    public static class VolumeToSectionMappingExtensions
    {
        public static bool[] TrySectionToVolume(this Viking.VolumeModel.IVolumeToSectionTransform mapper, IEnumerable<IPoint2D> points, out GridVector2[] output)
        {
            return mapper.TrySectionToVolume(points.Select(p => new GridVector2(p.X, p.Y)).ToArray(), out output);
        }

        public static GridVector2[] SectionToVolume(this Viking.VolumeModel.IVolumeToSectionTransform mapper, IEnumerable<IPoint2D> points)
        {
            return mapper.SectionToVolume(points.Select(p => new GridVector2(p.X, p.Y)).ToArray());
        }

        public static bool[] TryVolumeToSection(this Viking.VolumeModel.IVolumeToSectionTransform mapper, IEnumerable<IPoint2D> points, out GridVector2[] output)
        {
            return mapper.TryVolumeToSection(points.Select(p => new GridVector2(p.X, p.Y)).ToArray(), out output);
        }

        public static GridVector2[] VolumeToSection(this Viking.VolumeModel.IVolumeToSectionTransform mapper, IEnumerable<IPoint2D> points)
        {
            return mapper.VolumeToSection(points.Select(p => new GridVector2(p.X, p.Y)).ToArray());
        }

        public static GridPolygon TryMapShapeSectionToVolume(this Viking.VolumeModel.IVolumeToSectionTransform mapper, GridPolygon shape)
        {
            GridVector2[] VolumePositions;

            bool[] mappedPosition = mapper.TrySectionToVolume(shape.ExteriorRing, out VolumePositions);
            if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
            {
                Trace.WriteLine("MapShapeSectionToVolume: Shape #" + shape.ToString() + " was unmappable.", "WebAnnotation");
                return null;
            }

            GridPolygon transformed_polygon = new GridPolygon(VolumePositions);

            if (shape.HasInteriorRings)
            {
                IEnumerable<GridPolygon> transformedPolygons = shape.InteriorPolygons.Select(ip => mapper.TryMapShapeSectionToVolume(ip));
                foreach (GridPolygon inner_poly in transformedPolygons)
                {
                    transformed_polygon.AddInteriorRing(inner_poly);
                }
            }

            return transformed_polygon;
        }

        public static GridPolygon TryMapShapeVolumeToSection(this Viking.VolumeModel.IVolumeToSectionTransform mapper, GridPolygon shape)
        {
            GridVector2[] SectionPositions;

            bool[] mappedPosition = mapper.TryVolumeToSection(shape.ExteriorRing, out SectionPositions);
            if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
            {
                Trace.WriteLine("MapShapeSectionToVolume: Shape #" + shape.ToString() + " was unmappable.", "WebAnnotation");
                return null;
            }

            GridPolygon transformed_polygon = new GridPolygon(SectionPositions);

            if (shape.HasInteriorRings)
            {
                IEnumerable<GridPolygon> transformedPolygons = shape.InteriorPolygons.Select(ip => mapper.TryMapShapeVolumeToSection(ip));
                foreach (GridPolygon inner_poly in transformedPolygons)
                {
                    transformed_polygon.AddInteriorRing(inner_poly);
                }
            }

            return transformed_polygon;
        }

        public static GridPolyline TryMapShapeSectionToVolume(this Viking.VolumeModel.IVolumeToSectionTransform mapper, GridPolyline shape)
        {
            GridVector2[] VolumePositions;

            bool[] mappedPosition = mapper.TrySectionToVolume(shape.Points, out VolumePositions);
            if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
            {
                Trace.WriteLine("TryMapShapeSectionToVolume: Shape #" + shape.ToString() + " was unmappable.", "WebAnnotation");
                return null;
            }

            GridPolyline transformed_shape = new GridPolyline(VolumePositions, shape.AllowsSelfIntersection);

            return transformed_shape;
        }

        public static GridPolyline TryMapShapeVolumeToSection(this Viking.VolumeModel.IVolumeToSectionTransform mapper, GridPolyline shape)
        {
            GridVector2[] SectionPositions;

            bool[] mappedPosition = mapper.TryVolumeToSection(shape.Points, out SectionPositions);
            if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
            {
                Trace.WriteLine("TryMapShapeVolumeToSection: Shape #" + shape.ToString() + " was unmappable.", "WebAnnotation");
                return null;
            }

            GridPolyline transformed_shape = new GridPolyline(SectionPositions, shape.AllowsSelfIntersection);

            return transformed_shape;
        }
    }
}
