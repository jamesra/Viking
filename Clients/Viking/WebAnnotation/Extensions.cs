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
        public static void SetShapeFromPointsInVolume(this WebAnnotationModel.LocationObj location, Viking.VolumeModel.IVolumeToSectionTransform mapper, GridVector2[] volumePoints, ICollection<GridVector2[]> volume_innerRingPoints)
        {
            GridVector2[] mosaic_points = mapper.VolumeToSection(volumePoints);

            location.VolumeShape = location.TypeCode.GetSmoothedShape(volumePoints, volume_innerRingPoints);
            location.MosaicShape = location.TypeCode.GetShape(mosaic_points, VolumeInnerRingPointsToSection(mapper, volume_innerRingPoints));

            return;
        }

        /// <summary>
        /// Takes unsmoothed points and sets both the mosaic and volume shape for a locationObj
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="location"></param>
        /// <param name="volumePoints"></param>
        /// <param name="volume_innerRingPoints"></param>
        public static void SetShapeFromPointsInSection(this WebAnnotationModel.LocationObj location, Viking.VolumeModel.IVolumeToSectionTransform mapper, GridVector2[] sectionPoints, ICollection<GridVector2[]> section_innerRingPoints)
        {
            GridVector2[] volume_points = mapper.SectionToVolume(sectionPoints);

            location.VolumeShape = location.TypeCode.GetSmoothedShape(volume_points, SectionInnerRingPointsToVolume(mapper, section_innerRingPoints));
            location.MosaicShape = location.TypeCode.GetShape(sectionPoints, section_innerRingPoints);

            return;
        }

        /// <summary>
        /// Takes unsmoothed points and sets both the mosaic and volume shape for a locationObj
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="location"></param>
        /// <param name="volumePoints"></param>
        /// <param name="volume_innerRingPoints"></param>
        public static void SetShapeFromGeometryInSection(this WebAnnotationModel.LocationObj location, Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            if (!shape.STIsValid().Value)
                throw new ArgumentException("Shape must be valid SQL Geometry " + shape.IsValidDetailed());

            Microsoft.SqlServer.Types.SqlGeometry volume_shape = mapper.TryMapShapeSectionToVolume(shape);

            location.VolumeShape = location.TypeCode.GetSmoothedShape(volume_shape);
            location.MosaicShape = shape;

            return;
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
                SetShapeFromGeometryInSection(location, mapper, shape);
            }
            catch (ArgumentException e)
            {
                System.Windows.MessageBox.Show(parent, e.Message, "Could not save Polygon", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            } 
        }

        private static ICollection<GridVector2[]> VolumeInnerRingPointsToSection(Viking.VolumeModel.IVolumeToSectionTransform mapper, ICollection<GridVector2[]> volume_innerRingPoints)
        {
            if (volume_innerRingPoints == null)
                return null;

            List<GridVector2[]> mosaic_innerRingPoints = new List<GridVector2[]>(volume_innerRingPoints.Count);
            foreach (GridVector2[] volume_ring in volume_innerRingPoints)
            {
                mosaic_innerRingPoints.Add(mapper.VolumeToSection(volume_ring));
            }

            return mosaic_innerRingPoints;
        }

        private static ICollection<GridVector2[]> SectionInnerRingPointsToVolume(Viking.VolumeModel.IVolumeToSectionTransform mapper, ICollection<GridVector2[]> section_innerRingPoints)
        {
            if (section_innerRingPoints == null)
                return null;

            List<GridVector2[]> volume_innerRingPoints = new List<GridVector2[]>(section_innerRingPoints.Count);
            foreach (GridVector2[] volume_ring in section_innerRingPoints)
            {
                volume_innerRingPoints.Add(mapper.SectionToVolume(volume_ring));
            }

            return volume_innerRingPoints;
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

            if(shape.HasInteriorRings())
            {
                ICollection<GridVector2[]> innerRings = shape.InteriorRingPoints();
                SectionInnerRings = new List<GridVector2[]>(innerRings.Count);

                foreach(GridVector2[] innerRing in innerRings)
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

    public static class ShapeSmoothingExtensions
    {
        public static Microsoft.SqlServer.Types.SqlGeometry GetShape(this WebAnnotationModel.LocationType shapeType, GridVector2[] points, ICollection<GridVector2[]> innerRingPoints = null)
        {
            Microsoft.SqlServer.Types.SqlGeometry shape = null;

            switch (shapeType)
            {
                case WebAnnotationModel.LocationType.POINT:
                    return points[0].ToGeometryPoint();
                case WebAnnotationModel.LocationType.CIRCLE:
                    return points.ToCircle();
                case WebAnnotationModel.LocationType.OPENCURVE:
                case WebAnnotationModel.LocationType.POLYLINE:
                case WebAnnotationModel.LocationType.CLOSEDCURVE:
                    return points.ToPolyLine();
                case WebAnnotationModel.LocationType.POLYGON:
                case WebAnnotationModel.LocationType.CURVEPOLYGON:
                    return points.ToPolygon(innerRingPoints);
                default:
                    throw new ArgumentException("Unexpected location type " + shapeType.ToString());
            }
        }

        public static Microsoft.SqlServer.Types.SqlGeometry GetSmoothedShape(this WebAnnotationModel.LocationType shapeType, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            GridVector2[] points = shape.ToPoints();

            switch (shapeType)
            {
                case WebAnnotationModel.LocationType.POINT:
                    return points[0].ToGeometryPoint();
                case WebAnnotationModel.LocationType.CIRCLE:
                    return points.ToCircle();
                case WebAnnotationModel.LocationType.OPENCURVE:
                    return points.CalculateCurvePoints(Global.NumOpenCurveInterpolationPoints, false).ToArray().ToPolyLine();
                case WebAnnotationModel.LocationType.POLYLINE:
                    return points.ToPolyLine();
                case WebAnnotationModel.LocationType.POLYGON:
                    return points.ToPolygon(shape.InteriorRingPoints());
                case WebAnnotationModel.LocationType.CLOSEDCURVE:
                    return points.CalculateCurvePoints(Global.NumClosedCurveInterpolationPoints, true).ToArray().ToPolyLine();
                case WebAnnotationModel.LocationType.CURVEPOLYGON:
                    List<GridVector2[]> curved_innerRingPoints = InnerRingPointsToCurvedRingPoints(shape.InteriorRingPoints());
                    GridVector2[] curved_outerRing = points.CalculateCurvePoints(Global.NumClosedCurveInterpolationPoints, true).ToArray();
                    return curved_outerRing.ToPolygon(curved_innerRingPoints);
                default:
                    throw new ArgumentException("Unexpected location type " + shapeType.ToString());
            }
        }

        public static Microsoft.SqlServer.Types.SqlGeometry GetSmoothedShape(this WebAnnotationModel.LocationType shapeType, GridVector2[] points, ICollection<GridVector2[]> innerRingPoints = null)
        {
            Microsoft.SqlServer.Types.SqlGeometry shape = null;

            switch (shapeType)
            {
                case WebAnnotationModel.LocationType.POINT:
                    return points[0].ToGeometryPoint();
                case WebAnnotationModel.LocationType.CIRCLE:
                    return points.ToCircle(); 
                case WebAnnotationModel.LocationType.OPENCURVE:
                    return points.CalculateCurvePoints(Global.NumOpenCurveInterpolationPoints, false).ToArray().ToPolyLine();
                case WebAnnotationModel.LocationType.CLOSEDCURVE:
                    return points.CalculateCurvePoints(Global.NumClosedCurveInterpolationPoints, true).ToArray().ToPolyLine();
                case WebAnnotationModel.LocationType.POLYLINE:
                    return points.ToPolyLine();
                case WebAnnotationModel.LocationType.POLYGON:
                    return points.ToPolygon(innerRingPoints);
                case WebAnnotationModel.LocationType.CURVEPOLYGON:
                    ICollection<GridVector2[]> curved_innerRingPoints = InnerRingPointsToCurvedRingPoints(innerRingPoints);
                    GridVector2[] curved_outerRing = points.CalculateCurvePoints(Global.NumClosedCurveInterpolationPoints, true).ToArray();
                    return curved_outerRing.ToPolygon(curved_innerRingPoints);
                default:
                    throw new ArgumentException("Unexpected location type " + shapeType.ToString());
            }
        }

        private static List<GridVector2[]> InnerRingPointsToCurvedRingPoints(ICollection<GridVector2[]> innerRingPoints)
        {
            if (innerRingPoints == null)
                return null;

            List<GridVector2[]> curved_innerRingPoints = new List<GridVector2[]>(innerRingPoints.Count);
            foreach (GridVector2[] ringPoints in innerRingPoints)
            {
                curved_innerRingPoints.Add(ringPoints.CalculateCurvePoints(Global.NumClosedCurveInterpolationPoints, true).ToArray());
            }

            return curved_innerRingPoints;
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
