using Geometry;
using Geometry.Meshing;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Concurrent;
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
        public static List<HitTestResult> ExpandICanvasViewContainers(this IEnumerable<HitTestResult> listHitTestObjects, Vector2 WorldPos)
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

    public static class RectangleExtensions
    {
        public static Rectangle ToMosaicSpace(this in Rectangle volumeRect, Viking.VolumeModel.IVolumeToSectionTransform mapper)
        {
            Vector2[] MosaicCorners = mapper.VolumeToSection([volumeRect.LowerLeft, volumeRect.LowerRight, volumeRect.UpperLeft, volumeRect.UpperRight]);

            double MinX = MosaicCorners.Min(p => p.X);
            double MaxX = MosaicCorners.Max(p => p.X);
            double MinY = MosaicCorners.Min(p => p.Y);
            double MaxY = MosaicCorners.Max(p => p.Y);

            return new Rectangle(MinX, MaxX, MinY, MaxY);
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
        public static double DistanceToPoint3D(this WebAnnotationModel.LocationObj l, Vector3 origin)
        {
            Viking.VolumeModel.IVolumeToSectionTransform mapper = Viking.UI.State.volume.GetSectionToVolumeTransform((int)l.Z);
            if (mapper is null)
            {
                return double.MaxValue;
            }

            if (!mapper.TrySectionToVolume(l.Position, out Vector2 vPos))
            {
                return double.MaxValue;
            }

            Vector3 p = new(vPos.X * Global.Scale.X, vPos.Y * Global.Scale.Y, l.Z * Global.Scale.Z);
            return Vector3.Distance(p, origin);
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
    /// Centroid of one constrained Delaunay triangle and that triangle's area.
    /// Area is Heron's formula from <see cref="Triangle.Area"/>, so it is never negative.
    /// </summary>
    internal readonly struct PolygonTriangleCentroid
    {
        public PolygonTriangleCentroid(Vector2 point, double area)
        {
            Point = point;
            Area = area;
        }

        public Vector2 Point { get; }

        public double Area { get; }
    }

    /// <summary>
    /// Extensions for generating representative points from annotations for segmentation background prompts.
    /// </summary>
    public static class AnnotationPointExtensions
    {
        internal static readonly PolygonNegativePromptCache PolygonPromptCache = new();

        /// <summary>
        /// Returns representative points for a collection of annotations.
        /// For polygons: one centroid per constrained-triangulation face of the
        /// unsmoothed control-point ring (<see cref="LocationObj.MosaicShape"/>).
        /// <see cref="LocationObj.VolumeShape"/> is <c>GetSmoothedShape</c> for
        /// CURVEPOLYGON and must not be meshed here — interpolation multiplies
        /// faces. Polygon avoid marks are the centroid of the largest MosaicShape
        /// Delaunay triangle (one point), not every face. Falls back to the polygon
        /// centroid if meshing fails and that centroid is inside. For lines/points:
        /// vertices. For circles/ellipses: center.
        /// Polygon samples are cached by location ID and LastModified so a batch of
        /// SegmentImage calls does not retriangulate the same neighbors.
        /// </summary>
        /// <returns>Points in section/mosaic coordinates.</returns>
        public static IReadOnlyList<Vector2> GetAnnotationRepresentativePoints(IEnumerable<LocationObj> annotations)
        {
            if (annotations is null)
                return [];

            List<Vector2> result = [];
            foreach (LocationObj loc in annotations)
            {
                if (loc?.MosaicShape is null)
                    continue;

                LocationType typeCode = loc.TypeCode;

                switch (typeCode)
                {
                    case LocationType.POLYGON:
                    case LocationType.CURVEPOLYGON:
                    case LocationType.CLOSEDCURVE:
                        result.AddRange(PolygonPromptCache.GetOrAdd(
                            loc.ID,
                            loc.LastModified,
                            typeCode,
                            () => TryGetUnsmoothedPolygonRepresentativePoints(loc)));
                        break;
                    case LocationType.POLYLINE:
                    case LocationType.OPENCURVE:
                        Vector2[] linePoints = loc.MosaicShape.ToPoints();
                        if (linePoints?.Length > 0)
                            result.AddRange(linePoints);
                        break;
                    case LocationType.POINT:
                        result.Add(loc.Position);
                        break;
                    case LocationType.CIRCLE:
                    case LocationType.ELLIPSE:
                        result.Add(loc.MosaicShape.BoundingBox().Center);
                        break;
                    default:
                        break;
                }
            }
            return result;
        }

        /// <summary>
        /// One interior point per constrained Delaunay triangle, in mesh order. Triangulate is
        /// centroid-relative, so each face centroid is translated back. SAM2 foreground clicks
        /// decimate this set in <see cref="WebAnnotation.UI.Commands.Segmentation.PolygonSegmentationPrompts"/>.
        /// Neighbor avoid marks use <see cref="GetLargestTriangleCentroidPoint"/> instead.
        /// </summary>
        internal static IReadOnlyList<Vector2> GetPolygonTriangleCentroidPoints(Polygon polygon)
            => [.. GetPolygonTriangleCentroidSamples(polygon).Select(sample => sample.Point)];

        /// <summary>
        /// Same centroids as <see cref="GetPolygonTriangleCentroidPoints"/>, each with its triangle area.
        /// Mesh order is preserved so a later stable sort can keep ties in that order. The fallback
        /// polygon centroid, when meshing fails and the point is inside, has area 0.
        /// </summary>
        internal static IReadOnlyList<PolygonTriangleCentroid> GetPolygonTriangleCentroidSamples(Polygon polygon)
        {
            if (polygon is null)
                return [];

            try
            {
                TriangulationMesh<IVertex2D<PolygonIndex>> mesh = polygon.Triangulate();
                if (mesh?.Faces.Count > 0)
                {
                    Vector2 origin = polygon.Centroid;
                    List<PolygonTriangleCentroid> samples = [];
                    foreach (IFace face in mesh.Faces)
                    {
                        if (!face.IsTriangle())
                            continue;

                        try
                        {
                            Vector2 centroid = mesh.Centroid(face) + origin;
                            if (!polygon.Contains(centroid))
                                continue;

                            samples.Add(new PolygonTriangleCentroid(centroid, mesh.ToTriangle(face).Area));
                        }
                        catch (ArgumentException)
                        {
                        }
                    }

                    if (samples.Count > 0)
                        return samples;
                }
            }
            catch (EdgesIntersectTriangulationException)
            {
            }
            catch (NonconformingTriangulationException)
            {
            }
            catch (ArgumentException)
            {
            }

            Vector2 fallback = polygon.Centroid;
            return polygon.Contains(fallback) ? [new PolygonTriangleCentroid(fallback, 0)] : [];
        }

        /// <summary>
        /// Centroid of the largest constrained Delaunay face whose centroid is inside
        /// the ring. Called from the polygon negative-prompt factory so dense neighbors
        /// send one avoid mark, not one per triangle. Fallback is the polygon centroid
        /// when meshing fails and that point is inside.
        /// </summary>
        internal static IReadOnlyList<Vector2> GetLargestTriangleCentroidPoint(Polygon polygon)
        {
            if (polygon is null)
                return [];

            try
            {
                TriangulationMesh<IVertex2D<PolygonIndex>> mesh = polygon.Triangulate();
                if (mesh?.Faces.Count > 0)
                {
                    Vector2 origin = polygon.Centroid;
                    Vector2 best = default;
                    double bestArea = double.NegativeInfinity;
                    bool found = false;
                    foreach (IFace face in mesh.Faces)
                    {
                        if (!face.IsTriangle())
                            continue;

                        try
                        {
                            Vector2 centroid = mesh.Centroid(face) + origin;
                            if (!polygon.Contains(centroid))
                                continue;

                            double area = mesh.ToTriangle(face).Area;
                            if (area <= bestArea)
                                continue;

                            bestArea = area;
                            best = centroid;
                            found = true;
                        }
                        catch (ArgumentException)
                        {
                        }
                    }

                    if (found)
                        return [best];
                }
            }
            catch (EdgesIntersectTriangulationException)
            {
            }
            catch (NonconformingTriangulationException)
            {
            }
            catch (ArgumentException)
            {
            }

            Vector2 fallback = polygon.Centroid;
            return polygon.Contains(fallback) ? [fallback] : [];
        }

        /// <summary>
        /// Largest MosaicShape Delaunay-face centroid of the stored control-point ring.
        /// Called from the polygon cache factory. Do not mesh VolumeShape here.
        /// </summary>
        private static IReadOnlyList<Vector2> TryGetUnsmoothedPolygonRepresentativePoints(LocationObj loc)
        {
            try
            {
                SqlGeometry mosaic = loc?.MosaicShape;
                if (mosaic is null)
                    return [];

                if (mosaic.GeometryType() != SupportedGeometryType.POLYGON &&
                    mosaic.GeometryType() != SupportedGeometryType.CURVEPOLYGON)
                {
                    return [];
                }

                return GetLargestTriangleCentroidPoint(mosaic.ToPolygon());
            }
            catch (ArgumentException)
            {
                return [];
            }
        }
    }

    /// <summary>
    /// Caches polygon negative-prompt points by location ID. Entries are reused until
    /// LastModified or TypeCode changes, then dropped when the cache exceeds a cap so
    /// unused IDs from earlier sections do not grow without bound.
    /// </summary>
    internal sealed class PolygonNegativePromptCache
    {
        internal const int MaxEntries = 2048;

        private readonly ConcurrentDictionary<long, (DateTime LastModified, LocationType TypeCode, IReadOnlyList<Vector2> Points)> entries = new();

        public IReadOnlyList<Vector2> GetOrAdd(
            long locationId,
            DateTime lastModified,
            LocationType typeCode,
            Func<IReadOnlyList<Vector2>> factory)
        {
            if (entries.TryGetValue(locationId, out var cached) &&
                cached.LastModified == lastModified &&
                cached.TypeCode == typeCode)
            {
                return cached.Points;
            }

            IReadOnlyList<Vector2> points = factory?.Invoke() ?? [];
            if (entries.Count >= MaxEntries)
                entries.Clear();

            entries[locationId] = (lastModified, typeCode, points);
            return points;
        }

        public void Remove(long locationId) => entries.TryRemove(locationId, out _);

        public void Clear() => entries.Clear();
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
