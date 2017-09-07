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
using WebAnnotation.UI;
using Viking.VolumeModel;

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

    public static class HitTestResultExtensions
    {
        
        /// <summary>
        /// Select annotations in this order
        /// 1. Locations on our section or Structure Links, whichever is closer
        /// 2. Locations on adjacent section
        /// 3. Location Links
        /// </summary>
        /// <param name="listHitTestObjects"></param>
        /// <param name="SectionNumber"></param>
        /// <returns></returns>
        public static HitTestResult NearestObjectOnCurrentSectionThenAdjacent(this ICollection<HitTestResult> listHitTestObjects, int SectionNumber)
        {
            if (listHitTestObjects.Count == 0)
                return null;

            List<HitTestResult> listLocations = listHitTestObjects.Where(l => l.obj as IViewLocation != null).ToList();
            List<HitTestResult> listLocationsOnSection = listLocations.Where(l => l.Z == SectionNumber).ToList();

            if (listLocationsOnSection.Count > 0)
            {
                listLocationsOnSection.Sort(new HitTest_Z_Depth_Distance_Sorter());
                return listLocationsOnSection.First();
            } 
            else if (listLocations.Count > 0)
            {
                List<HitTestResult> listObjectsOnAdjacentSection = listLocations.ToList();
                listObjectsOnAdjacentSection.Sort(new HitTest_Z_Depth_Distance_Sorter());
                return listHitTestObjects.First();
            }

            List<HitTestResult> listStructureLinks = listHitTestObjects.Where(h => h.obj as IViewStructureLink != null).ToList();
            if(listStructureLinks.Count > 0)
            {
                listStructureLinks.Sort(new HitTest_Z_Distance_Sorter());
                return listStructureLinks.First();
            }

            //OK, no locations or structure links, return what is left by distance
            List<HitTestResult> remaining = new List<HitTestResult>(listHitTestObjects);
            remaining.Sort(new HitTest_Z_Distance_Sorter());
            return remaining.First();
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
                case "CurvePolygon":
                    return WebAnnotationModel.LocationType.CURVEPOLYGON;
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

    internal static class LocationObjExtensions
    {
        public static double DistanceToPoint3D(this WebAnnotationModel.LocationObj l, GridVector3 origin)
        {
            Viking.VolumeModel.IVolumeToSectionTransform mapper = Viking.UI.State.volume.GetSectionToVolumeTransform((int)l.Z);
            if (mapper == null)
                return double.MaxValue;

            GridVector2 vPos;
            if (!mapper.TrySectionToVolume(l.Position, out vPos))
                return double.MaxValue;

            GridVector3 p = new GridVector3(vPos.X * Global.Scale.X, vPos.Y * Global.Scale.Y, l.Z * Global.Scale.Z);
            return GridVector3.Distance(p, origin);
        }

         

        /// <summary>
        /// Takes unsmoothed points and sets both the mosaic and volume shape for a locationObj
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="location"></param>
        /// <param name="volumePoints"></param>
        /// <param name="volume_innerRingPoints"></param>
        public static void TrySetShapeFromGeometryInSectionShowErrorDialog(this WebAnnotationModel.LocationObj location, System.Windows.Window parent, Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            try
            {
                Viking.VolumeModel.LocationObjExtensions.SetShapeFromGeometryInSection(location, mapper, shape);
            }
            catch (ArgumentException e)
            {
                System.Windows.MessageBox.Show(parent, e.Message, "Could not save Polygon", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            } 
        }  

        public static bool IsLastEditedAnnotation(this WebAnnotationModel.LocationObj loc)
        { 
            if (!Global.LastEditedAnnotationID.HasValue)
            {
                return false;
            }

            return Global.LastEditedAnnotationID.Value == loc.ID;
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
