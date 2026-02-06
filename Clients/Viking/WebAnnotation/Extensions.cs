using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using rouge1.codepharm.net.XSD.WebAnnotationUserSettings.xsd;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;

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

        public static bool ShiftPressed(this System.Windows.Forms.Keys ModifierKeys) => ModifierKeys == System.Windows.Forms.Keys.Shift;

        public static bool CtrlPressed(this System.Windows.Forms.Keys ModifierKeys) => ModifierKeys == System.Windows.Forms.Keys.Control;
    }

    public static class HitTestResultExtensions
    {

        /// <summary>
        /// Select annotations in this order
        /// 1. Locations on our section or Structure Links, whichever is closer, with a max distance <= 1.  (>1 indicates mouse is not over the annotation, or is in a polygon hole)
        /// 2. Locations on adjacent section
        /// 3. Locations who have a distance greater than 1
        /// 3. Location Links
        /// </summary>
        /// <param name="listHitTestObjects"></param>
        /// <param name="SectionNumber"></param>
        /// <returns></returns>
        public static HitTestResult NearestObjectOnCurrentSectionThenAdjacent(this ICollection<HitTestResult> listHitTestObjects, int SectionNumber)
        {
            if (listHitTestObjects.Count == 0)
            {
                return null;
            }

            List<HitTestResult> listLocations = [.. listHitTestObjects.Where(l => l.obj as IViewLocation != null)];
            List<HitTestResult> listLocationsOnSection = [.. listLocations.Where(l => l.Z == SectionNumber)];
            List<HitTestResult> listLocationsOnSectionContainsPoint = [.. listLocationsOnSection.Where(l => l.Distance <= 1.0)];
            List<HitTestResult> listStructureLinks = [.. listHitTestObjects.Where(h => h.obj as IViewStructureLink != null)];

            List<HitTestResult> listLocationsOnSectionContainingPointAndStructureLinks = [.. listLocationsOnSectionContainsPoint];
            listLocationsOnSectionContainingPointAndStructureLinks.AddRange(listStructureLinks);

            if (listLocationsOnSectionContainingPointAndStructureLinks.Count > 0)
            {
                listLocationsOnSectionContainingPointAndStructureLinks.Sort(new HitTest_Z_Depth_Distance_Sorter());
                return listLocationsOnSectionContainingPointAndStructureLinks.First();
            }

            List<HitTestResult> listObjectsOnAdjacentSection = [.. listLocations.Where(l => l.Z != SectionNumber)];
            if (listObjectsOnAdjacentSection.Count > 0)
            {
                listObjectsOnAdjacentSection.Sort(new HitTest_Distance_Sorter());
                return listObjectsOnAdjacentSection.First();
            }

            if (listLocations.Count > 0)
            {
                listLocations.Sort(new HitTest_Z_Depth_Distance_Sorter());
                return listLocations.First();
            }

            /*
            if(listStructureLinks.Count > 0)
            {
                listStructureLinks.Sort(new HitTest_Z_Distance_Sorter());
                return listStructureLinks.First();
            }
            */

            //OK, no locations or structure links, return what is left by distance
            List<HitTestResult> remaining = [.. listHitTestObjects];
            remaining.Sort(new HitTest_Z_Distance_Sorter());
            return remaining.First();
        }

        /// <summary>
        /// Replace container canvas views with the nested object under the mouse if applicable.
        /// </summary>
        /// <param name="listHitTestObjects"></param>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        public static List<HitTestResult> ExpandICanvasViewContainers(this IEnumerable<HitTestResult> listHitTestObjects, GridVector2 WorldPos)
        {
            List<HitTestResult> nestedContainers = [.. listHitTestObjects.Select(lc =>
                 {
                     if (lc.obj is not ICanvasViewContainer container)
                     {
                         return lc;
                     }

                     ICanvasView nestedObj = container.GetAnnotationAtPosition(WorldPos);
                     if (nestedObj is null)
                     {
                         return null;
                     }
                     else if (nestedObj != lc.obj)
                     {
                         return new HitTestResult(nestedObj, lc.Z, lc.VisualHeight, nestedObj.DistanceFromCenterNormalized(WorldPos));
                     }
                     else
                     {
                         return lc;
                     }

                 })];

            return [.. nestedContainers];
        }
    }

    public static class GridRectangleExtensions
    {
        public static GridRectangle ToMosaicSpace(this in GridRectangle volumeRect, Viking.VolumeModel.IVolumeToSectionTransform mapper)
        {
            GridVector2[] MosaicCorners = mapper.VolumeToSection([volumeRect.LowerLeft, volumeRect.LowerRight, volumeRect.UpperLeft, volumeRect.UpperRight]);

            double MinX = MosaicCorners.Min(p => p.X);
            double MaxX = MosaicCorners.Max(p => p.X);
            double MinY = MosaicCorners.Min(p => p.Y);
            double MaxY = MosaicCorners.Max(p => p.Y);

            return new GridRectangle(MinX, MaxX, MinY, MaxY);
        }
    }

    public static class AnnotationExtensions
    {
        private static Viking.AnnotationServiceTypes.Interfaces.LocationType StringToLocationType(string annotationType)
        {
            return annotationType switch
            {
                "Circle" => Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE,
                "ClosedCurve" => Viking.AnnotationServiceTypes.Interfaces.LocationType.CLOSEDCURVE,
                "OpenCurve" => Viking.AnnotationServiceTypes.Interfaces.LocationType.OPENCURVE,
                "Polygon" => Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYGON,
                "Polyline" => Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYLINE,
                "Point" => Viking.AnnotationServiceTypes.Interfaces.LocationType.POINT,
                "Ellipse" => Viking.AnnotationServiceTypes.Interfaces.LocationType.ELLIPSE,
                "CurvePolygon" => Viking.AnnotationServiceTypes.Interfaces.LocationType.CURVEPOLYGON,
                _ => Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE,
            };
            throw new ArgumentException("Unknown annotation type " + annotationType);
        }

        public static Viking.AnnotationServiceTypes.Interfaces.LocationType GetLocationType(this rouge1.codepharm.net.XSD.WebAnnotationUserSettings.xsd.CreateStructureCommandAction command) => StringToLocationType(command.AnnotationType);

        public static Viking.AnnotationServiceTypes.Interfaces.LocationType GetLocationType(this rouge1.codepharm.net.XSD.WebAnnotationUserSettings.xsd.ChangeLocationAnnotationTypeAction command) => StringToLocationType(command.AnnotationType);

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
            if (mapper is null)
            {
                return double.MaxValue;
            }

            if (!mapper.TrySectionToVolume(l.Position, out GridVector2 vPos))
            {
                return double.MaxValue;
            }

            GridVector3 p = new(vPos.X * Global.Scale.X, vPos.Y * Global.Scale.Y, l.Z * Global.Scale.Z);
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

    /// <summary>
    /// Extensions for generating representative points from annotations for segmentation background prompts.
    /// </summary>
    public static class AnnotationPointExtensions
    {
        /// <summary>
        /// Returns representative points for a collection of annotations.
        /// For polygons: centroid if inside polygon, else skipped. For lines/points: vertices. For circles/ellipses: center.
        /// </summary>
        /// <param name="annotations">Annotations to extract points from</param>
        /// <returns>List of points in section/mosaic coordinates</returns>
        public static IReadOnlyList<GridVector2> GetAnnotationRepresentativePoints(IEnumerable<LocationObj> annotations)
        {
            if (annotations is null)
                return [];

            List<GridVector2> result = [];
            foreach (LocationObj loc in annotations)
            {
                if (loc?.MosaicShape is null)
                    continue;

                SqlGeometry shape = loc.MosaicShape;
                LocationType typeCode = loc.TypeCode;

                switch (typeCode)
                {
                    case LocationType.POLYGON:
                    case LocationType.CURVEPOLYGON:
                    case LocationType.CLOSEDCURVE:
                        GridVector2? polygonPoint = TryGetPolygonRepresentativePoint(shape);
                        if (polygonPoint.HasValue)
                            result.Add(polygonPoint.Value);
                        break;
                    case LocationType.POLYLINE:
                    case LocationType.OPENCURVE:
                        GridVector2[] linePoints = shape.ToPoints();
                        if (linePoints?.Length > 0)
                            result.AddRange(linePoints);
                        break;
                    case LocationType.POINT:
                        result.Add(loc.Position);
                        break;
                    case LocationType.CIRCLE:
                    case LocationType.ELLIPSE:
                        result.Add(shape.BoundingBox().Center);
                        break;
                    default:
                        break;
                }
            }
            return result;
        }

        private static GridVector2? TryGetPolygonRepresentativePoint(SqlGeometry shape)
        {
            try
            {
                if (shape.GeometryType() != SupportedGeometryType.POLYGON && shape.GeometryType() != SupportedGeometryType.CURVEPOLYGON)
                    return null;

                GridPolygon polygon = shape.ToPolygon();
                GridVector2 centroid = polygon.Centroid;
                return polygon.Contains(centroid) ? centroid : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }

    public static class LINQLikeExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            if (source is null)
            {
                throw new ArgumentNullException("source");
            }

            if (action is null)
            {
                throw new ArgumentNullException("action");
            }

            foreach (T item in source)
            {
                action(item);
            }
        }

        public static void ForEach<T>(this IEnumerable<T> source, Action<T, int> action)
        {
            if (source is null)
            {
                throw new ArgumentNullException("source");
            }

            if (action is null)
            {
                throw new ArgumentNullException("action");
            }

            int i = 0;
            foreach (T item in source)
            {
                action(item, i);
                i++;
            }
        }

        public static void ForEach<T>(this T[] source, Action<T, int> action)
        {
            if (source is null)
            {
                throw new ArgumentNullException("source");
            }

            if (action is null)
            {
                throw new ArgumentNullException("action");
            }

            for (int i = 0; i < source.Length; i++)
            {
                action(source[i], i);
            }
        }
    }
}
