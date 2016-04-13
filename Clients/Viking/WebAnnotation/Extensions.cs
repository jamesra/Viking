using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Entity.Spatial;
using RTree;
using Geometry;
using SqlGeometryUtils;

namespace WebAnnotation
{

    public static class ColorExtensions
    {
        public static Microsoft.Xna.Framework.Color ToXNAColor(this System.Drawing.Color color)
        {
            return new Microsoft.Xna.Framework.Color((int)color.R,
                                                    (int)color.G,
                                                    (int)color.B,
                                                    (int)color.A);
        }

        public static Microsoft.Xna.Framework.Color ToXNAColor(this System.Drawing.Color color, float alpha)
        {
            return new Microsoft.Xna.Framework.Color((int)color.R,
                                                    (int)color.G,
                                                    (int)color.B,
                                                    (int)(255f * alpha));
        }
    }

    public static class GridRectangleExtensions
    {
        public static GridRectangle ToMosaicSpace(this GridRectangle VolumeRect, Viking.VolumeModel.IVolumeToSectionMapper mapper)
        {
            GridVector2[] MosaicCorners = mapper.VolumeToSection(new GridVector2[] { VolumeRect.LowerLeft, VolumeRect.LowerRight, VolumeRect.UpperLeft, VolumeRect.UpperRight });

            double MinX = MosaicCorners.Min(p => p.X);
            double MaxX = MosaicCorners.Max(p => p.X);
            double MinY = MosaicCorners.Min(p => p.Y);
            double MaxY = MosaicCorners.Max(p => p.Y);

            return new GridRectangle(MinX, MaxX, MinY, MaxY);
        }
    }

    public static class AnnotationExtensions
    {
        private static WebAnnotationModel.LocationType StringToLocationType(string annotationType)
        {
            switch (annotationType)
            {
                case "Circle":
                    return WebAnnotationModel.LocationType.CIRCLE;
                case "ClosedCurve":
                    return WebAnnotationModel.LocationType.CLOSEDCURVE;
                case "OpenCurve":
                    return WebAnnotationModel.LocationType.OPENCURVE;
                case "Polygon":
                    return WebAnnotationModel.LocationType.POLYGON;
                case "Polyline":
                    return WebAnnotationModel.LocationType.POLYLINE;
                case "Point":
                    return WebAnnotationModel.LocationType.POINT;
                case "Ellipse":
                    return WebAnnotationModel.LocationType.ELLIPSE;
                default:
                    return WebAnnotationModel.LocationType.CIRCLE;
            }

            throw new ArgumentException("Unknown annotation type " + annotationType);
        }

        public static WebAnnotationModel.LocationType GetLocationType(this connectomes.utah.edu.XSD.WebAnnotationUserSettings.xsd.CreateStructureCommandAction command)
        {
            return StringToLocationType(command.AnnotationType);
        }

        public static WebAnnotationModel.LocationType GetLocationType(this connectomes.utah.edu.XSD.WebAnnotationUserSettings.xsd.ChangeLocationAnnotationTypeAction command)
        {
            return StringToLocationType(command.AnnotationType);
        }

        public static void SubscribeToPropertyChangeEvents(this WebAnnotationModel.LocationObj loc, System.Windows.IWeakEventListener listener)
        {
            WebAnnotation.ViewModel.NotifyPropertyChangingEventManager.AddListener(loc, listener);
            WebAnnotation.ViewModel.NotifyPropertyChangedEventManager.AddListener(loc, listener);
        }

        public static void UnsubscribeToPropertyChangeEvents(this WebAnnotationModel.LocationObj loc, System.Windows.IWeakEventListener listener)
        {
            WebAnnotation.ViewModel.NotifyPropertyChangingEventManager.RemoveListener(loc, listener);
            WebAnnotation.ViewModel.NotifyPropertyChangedEventManager.RemoveListener(loc, listener);
        }
    }

    public static class GeometryExtensions
    {
        public static RTree.Rectangle ToRTreeRect(this GridRectangle rect, float Z)
        {
            return new RTree.Rectangle((float)rect.Left, (float)rect.Bottom, (float)rect.Right, (float)rect.Top, Z, Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridRectangle rect, int Z)
        {
            return new RTree.Rectangle((float)rect.Left, (float)rect.Bottom, (float)rect.Right, (float)rect.Top, (float)Z, (float)Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridVector2 p, float Z)
        {
            return new RTree.Rectangle((float)p.X, (float)p.Y, (float)p.X, (float)p.Y, Z, Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridVector2 p, int Z)
        {
            return new RTree.Rectangle((float)p.X, (float)p.Y, (float)p.X, (float)p.Y, (float)Z, (float)Z);
        } 
    } 

    internal static class MappingExtensions
    { 
        public static bool MapLocationToVolume(this Viking.VolumeModel.IVolumeToSectionMapper mapper, WebAnnotationModel.LocationObj loc)
        {
            //Don't bother mapping if the location was already mapped
            if (loc.VolumeTransformID == mapper.ID)
                return true;

            switch (loc.TypeCode)
            {
                case WebAnnotationModel.LocationType.POINT:
                    return mapper.MapLocationCentroidToVolume(loc);
                case WebAnnotationModel.LocationType.CIRCLE:
                    return mapper.MapLocationCentroidToVolume(loc);
                default:
                    return mapper.MapLocationShapeToVolume(loc);
            }
        }

        /// <summary>
        /// A faster mapping technique for geometries that do not use control points such as circles and points.
        /// </summary>
        /// <param name="loc"></param>
        /// <returns></returns>
        private static bool MapLocationCentroidToVolume(this Viking.VolumeModel.IVolumeToSectionMapper mapper, WebAnnotationModel.LocationObj loc)
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

        public static Microsoft.SqlServer.Types.SqlGeometry TryMapShapeSectionToVolume(this Viking.VolumeModel.IVolumeToSectionMapper mapper, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            GridVector2[] VolumePositions;
            GridVector2[] points = shape.ToPoints();

            bool mappedPosition = mapper.TrySectionToVolume(points, out VolumePositions);
            if (!mappedPosition) //Remove locations we can't map
            {
                Trace.WriteLine("MapShapeSectionToVolume: Shape #" + shape.ToString() + " was unmappable.", "WebAnnotation");
                return null;
            }

            return SqlGeometryUtils.GeometryExtensions.ToGeometry(shape.STGeometryType(), VolumePositions);
        }

        public static Microsoft.SqlServer.Types.SqlGeometry TryMapShapeVolumeToSection(this Viking.VolumeModel.IVolumeToSectionMapper mapper, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            GridVector2[] SectionPositions;
            GridVector2[] points = shape.ToPoints();

            bool mappedPosition = mapper.TryVolumeToSection(points, out SectionPositions);
            if (!mappedPosition) //Remove locations we can't map
            {
                Trace.WriteLine("MapShapeSectionToVolume: Shape #" + shape.ToString() + " was unmappable.", "WebAnnotation");
                return null;
            }

            return SqlGeometryUtils.GeometryExtensions.ToGeometry(shape.STGeometryType(), SectionPositions);
        }

        /// <summary>
        /// Map all of the control points for the geometry individually
        /// </summary>
        /// <param name="loc"></param>
        /// <returns></returns>
        private static bool MapLocationShapeToVolume(this Viking.VolumeModel.IVolumeToSectionMapper mapper, WebAnnotationModel.LocationObj loc)
        {
            //Don't bother mapping if the location was already mapped
            if (loc.VolumeTransformID == mapper.ID)
                return true;

            Microsoft.SqlServer.Types.SqlGeometry mappedshape = mapper.TryMapShapeSectionToVolume(loc.MosaicShape);
            if (mappedshape == null)
            {
                Trace.WriteLine("MapLocationToVolume: Location #" + loc.ID.ToString() + " was unmappable.", "WebAnnotation");
                return false;
            }

            loc.VolumeShape = mappedshape;
            loc.VolumeTransformID = mapper.ID;

            return true;
        }
    }
}
