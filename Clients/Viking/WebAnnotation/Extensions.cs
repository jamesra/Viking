using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Entity.Spatial;
using RTree;
using Geometry;
using SqlGeometryUtils;
using WebAnnotation.ViewModel;
using connectomes.utah.edu.XSD.WebAnnotationUserSettings.xsd;

namespace WebAnnotation
{

    public static class HotkeyExtensions
    {
        public static string BuildModifierString(this Hotkey hkey)
        {
            string keystr = "";
            if (hkey.Ctrl)
            {
                keystr += "CTRL + ";
            }

            if (hkey.Shift)
            {
                keystr += "SHIFT + ";
            }

            return keystr;
        }
    }

    public static class KeysExtensions
    {
        public static bool ShiftOrCtrlPressed(this System.Windows.Forms.Keys ModifierKeys)
        {
            return ModifierKeys == System.Windows.Forms.Keys.Control ||
               ModifierKeys == System.Windows.Forms.Keys.Shift;
        }

        public static bool ShiftPressed(this System.Windows.Forms.Keys ModifierKeys)
        {
            return ModifierKeys == System.Windows.Forms.Keys.Shift;
        }

        public static bool CtrlPressed(this System.Windows.Forms.Keys ModifierKeys)
        {
            return ModifierKeys == System.Windows.Forms.Keys.Control;
        }
    }

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

    public static class HitTestResultExtensions
    {
        public static HitTestResult NearestObjectOnCurrentSectionThenAdjacent(this ICollection<HitTestResult> listHitTestObjects, int SectionNumber)
        {
            List<HitTestResult> listObjectsOnOurSection = listHitTestObjects.Where(l => l.Z == SectionNumber).ToList();
            if (listObjectsOnOurSection.Count > 0)
            {
                listObjectsOnOurSection.Sort(new HitTest_Z_Distance_Sorter());
                return listObjectsOnOurSection.First();
            }
            else if (listHitTestObjects.Count > 0)
            {
                List<HitTestResult> listObjectsOnAdjacentSection = listHitTestObjects.ToList();
                listObjectsOnAdjacentSection.Sort(new HitTest_Z_Distance_Sorter());
                return listHitTestObjects.First();
            }

            return null;
        }

        /// <summary>
        /// Replace container canvas views with the nested object under the mouse if applicable.
        /// </summary>
        /// <param name="listHitTestObjects"></param>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        public static List<HitTestResult> ExpandICanvasViewContainers(this ICollection<HitTestResult> listHitTestObjects, GridVector2 WorldPos)
        {
           List<HitTestResult> nestedContainers = listHitTestObjects.Select(lc =>
                {
                    ICanvasViewContainer container = lc.obj as ICanvasViewContainer;
                    if (container == null)
                        return lc; 

                    ICanvasView nestedObj = container.GetAnnotationAtPosition(WorldPos);
                    if (nestedObj != lc.obj)
                    {
                        return new HitTestResult(nestedObj, lc.Z, nestedObj.DistanceFromCenterNormalized(WorldPos));
                    }
                    else
                    {
                        return lc;
                    }

                }).ToList();

            return nestedContainers.ToList();
        }
    }

    public static class GridRectangleExtensions
    {
        public static GridRectangle ToMosaicSpace(this GridRectangle VolumeRect, Viking.VolumeModel.IVolumeToSectionTransform mapper)
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

        public static void SubscribeToPropertyChangeEvents(this WebAnnotationModel.StructureObj s, System.Windows.IWeakEventListener listener)
        {
            WebAnnotation.ViewModel.NotifyPropertyChangingEventManager.AddListener(s, listener);
            WebAnnotation.ViewModel.NotifyPropertyChangedEventManager.AddListener(s, listener);
        }

        public static void UnsubscribeToPropertyChangeEvents(this WebAnnotationModel.StructureObj s, System.Windows.IWeakEventListener listener)
        {
            WebAnnotation.ViewModel.NotifyPropertyChangingEventManager.RemoveListener(s, listener);
            WebAnnotation.ViewModel.NotifyPropertyChangedEventManager.RemoveListener(s, listener);
        }
    }

    internal static class MappingExtensions
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
            GridVector2[] points = shape.ToPoints();

            bool[] mappedPosition = mapper.TrySectionToVolume(points, out VolumePositions);
            if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
            {
                Trace.WriteLine("MapShapeSectionToVolume: Shape #" + shape.ToString() + " was unmappable.", "WebAnnotation");
                return null;
            }

            return SqlGeometryUtils.GeometryExtensions.ToGeometry(shape.STGeometryType(), VolumePositions);
        }

        public static Microsoft.SqlServer.Types.SqlGeometry TryMapShapeVolumeToSection(this Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            GridVector2[] SectionPositions;
            GridVector2[] points = shape.ToPoints();

            bool[] mappedPosition = mapper.TryVolumeToSection(points, out SectionPositions);
            if (mappedPosition.Any(success => success == false)) //Remove locations we can't map
            {
                Trace.WriteLine("MapShapeSectionToVolume: Shape #" + shape.ToString() + " was unmappable.", "WebAnnotation");
                return null;
            }

            return SqlGeometryUtils.GeometryExtensions.ToGeometry(shape.STGeometryType(), SectionPositions);
        }
    }

    public static class LINQLikeExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (action == null) throw new ArgumentNullException("action");

            foreach (T item in source)
            {
                action(item);
            }
        }

        public static void ForEach<T>(this IEnumerable<T> source, Action<T, int> action)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (action == null) throw new ArgumentNullException("action");

            int i = 0;
            foreach(T item in source)
            { 
                action(item, i);
                i++;
            }
        }

        public static void ForEach<T>(this T[] source, Action<T,int> action)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (action == null) throw new ArgumentNullException("action");

            for(int i = 0; i < source.Length; i++)
            {
                action(source[i], i);
            }
        }
    }
}
