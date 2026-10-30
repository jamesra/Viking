using Geometry;
using Geometry.Meshing;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using rogue1.codepharm.net.XSD.WebAnnotationUserSettings.xsd;
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

    /// <summary>
    /// Modifier checks on <see cref="System.Windows.Forms.Control.ModifierKeys"/> for pen and mouse annotation actions.
    /// Each is a flag test: Ctrl+Shift satisfies both <see cref="ShiftPressed"/> and <see cref="CtrlPressed"/>
    /// (an <c>if Shift … else if Ctrl</c> chain takes its Shift branch), and Alt is ignored.
    /// </summary>
    public static class KeysExtensions
    {
        /// <summary>True when Shift or Ctrl is held, with or without other modifiers.</summary>
        public static bool ShiftOrCtrlPressed(this System.Windows.Forms.Keys ModifierKeys) =>
            ModifierKeys.ShiftPressed() || ModifierKeys.CtrlPressed();

        /// <summary>True when Shift is held, with or without Ctrl or Alt.</summary>
        public static bool ShiftPressed(this System.Windows.Forms.Keys ModifierKeys) =>
            (ModifierKeys & System.Windows.Forms.Keys.Shift) != 0;

        /// <summary>True when Ctrl is held, with or without Shift or Alt.</summary>
        public static bool CtrlPressed(this System.Windows.Forms.Keys ModifierKeys) =>
            (ModifierKeys & System.Windows.Forms.Keys.Control) != 0;
    }

    public static class HitTestResultExtensions
    {

        /// <summary>
        /// Select annotations in this order
        /// 1. Locations on our section or Structure Links, whichever is closer, with a max distance <= 1.  (>1 indicates mouse is not over the annotation, or is in a polygon hole)
        /// 2. Locations on adjacent section
        /// 3. Locations who have a distance greater than 1
        /// 3. Location Links
        /// When <paramref name="preferCloserLocationLinks"/> is true, a location link the mouse is over (distance &lt;= 1)
        /// beats the step 1 result if its normalized distance is strictly smaller, so a link arrow drawn under a larger
        /// circle stays selectable. Only hover/select callers opt in; link-target pickers keep the circle-first order.
        /// </summary>
        /// <param name="listHitTestObjects"></param>
        /// <param name="SectionNumber"></param>
        /// <param name="preferCloserLocationLinks">Opt in to the closer-location-link rule above.</param>
        /// <returns></returns>
        public static HitTestResult NearestObjectOnCurrentSectionThenAdjacent(
            this ICollection<HitTestResult> listHitTestObjects,
            int SectionNumber,
            bool preferCloserLocationLinks = false)
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
                HitTestResult primary = listLocationsOnSectionContainingPointAndStructureLinks.First();

                // Depth ordering alone hides a location link drawn under a current-section circle. Link distance is
                // normalized by the link's own line radius, so a link that is genuinely closer to the mouse wins,
                // and only if the mouse is over it (<= 1). Adjacent-section circles never take this path.
                if (preferCloserLocationLinks)
                {
                    HitTestResult closerLink = NearestLocationLinkContainingPoint(listHitTestObjects);
                    if (closerLink != null && closerLink.Distance < primary.Distance)
                    {
                        return closerLink;
                    }
                }

                return primary;
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
        /// The closest location link that the mouse is actually over
        /// (<see cref="HitTestResult.Distance"/> &lt;= 1, normalized by the link's own radius).
        /// Returns null when none qualifies.
        /// </summary>
        internal static HitTestResult NearestLocationLinkContainingPoint(IEnumerable<HitTestResult> hits)
        {
            List<HitTestResult> links = [.. hits.Where(h => h.Distance <= 1.0 && h.obj is IViewLocationLink)];
            if (links.Count == 0)
            {
                return null;
            }

            links.Sort(new HitTest_Distance_Sorter());
            return links[0];
        }

        /// <summary>
        /// Replace container canvas views with the nested object under the mouse if applicable.
        /// </summary>
        /// <param name="listHitTestObjects"></param>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        public static List<HitTestResult> ExpandICanvasViewContainers(this IEnumerable<HitTestResult> listHitTestObjects, Vector2 WorldPos) =>
            [.. listHitTestObjects.Select(lc => lc.ExpandICanvasViewContainer(WorldPos))];

        /// <summary>
        /// The nested object under <paramref name="WorldPos"/> when <paramref name="hit"/> is a container (a location with
        /// link markers), the hit itself when it is not a container or the mouse is on the container's own shape, or null
        /// when the container has nothing there. Every caller that looks inside a location uses this.
        /// </summary>
        public static HitTestResult ExpandICanvasViewContainer(this HitTestResult hit, Vector2 WorldPos)
        {
            if (hit.obj is not ICanvasViewContainer container)
            {
                return hit;
            }

            ICanvasView nestedObj = container.GetAnnotationAtPosition(WorldPos);
            if (nestedObj is null)
            {
                return null;
            }

            return nestedObj != hit.obj
                ? new HitTestResult(nestedObj, hit.Z, hit.VisualHeight, nestedObj.DistanceFromCenterNormalized(WorldPos))
                : hit;
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

        public static Viking.AnnotationServiceTypes.Interfaces.LocationType GetLocationType(this rogue1.codepharm.net.XSD.WebAnnotationUserSettings.xsd.CreateStructureCommandAction command) => StringToLocationType(command.AnnotationType);

        public static Viking.AnnotationServiceTypes.Interfaces.LocationType GetLocationType(this rogue1.codepharm.net.XSD.WebAnnotationUserSettings.xsd.ChangeLocationAnnotationTypeAction command) => StringToLocationType(command.AnnotationType);

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
            if (l.VolumeGeometry() is not Viking.VolumeModel.LocationVolumeGeometry geometry || !geometry.IsExact)
            {
                return double.MaxValue;
            }

            Vector2 vPos = geometry.Center;

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
    /// Interior points of polygons, used for SAM2 foreground clicks.
    /// </summary>
    public static class AnnotationPointExtensions
    {
        /// <summary>
        /// One interior point per constrained Delaunay triangle, in mesh order. Triangulate is
        /// centroid-relative, so each face centroid is translated back. SAM2 foreground clicks
        /// decimate this set in <see cref="WebAnnotation.UI.Commands.Segmentation.PolygonSegmentationPrompts"/>.
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
    }

    /// <summary>
    /// Lookups that map spatial-index keys to the views stored for them.
    /// </summary>
    internal static class ConcurrentDictionaryExtensions
    {
        /// <summary>
        /// Returns the values stored for <paramref name="keys"/>, in key order, skipping keys that are not in
        /// <paramref name="dictionary"/>. The section link view models remove a link from their dictionary before
        /// its R-tree entry and readers take no lock, so a key just read from the tree can already be gone; the
        /// indexer would throw <see cref="KeyNotFoundException"/> there.
        /// </summary>
        /// <param name="dictionary">The views by key.</param>
        /// <param name="keys">Keys to look up, usually an R-tree query result.</param>
        /// <returns>A new list holding the value of each key still present.</returns>
        internal static List<TValue> ValuesForKeys<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, IEnumerable<TKey> keys)
        {
            List<TValue> values = keys is ICollection<TKey> collection ? new(collection.Count) : [];
            foreach (TKey key in keys)
            {
                if (dictionary.TryGetValue(key, out TValue value))
                {
                    values.Add(value);
                }
            }

            return values;
        }
    }

    /// <summary>
    /// <c>ForEach</c> helpers for sequences. Arrays use the <see cref="IEnumerable{T}"/> overloads; the indexed overload passes a zero-based position. A null <c>source</c> or <c>action</c> throws <see cref="ArgumentNullException"/>.
    /// </summary>
    public static class LINQLikeExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
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
                throw new ArgumentNullException(nameof(source));
            }

            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            int i = 0;
            foreach (T item in source)
            {
                action(item, i);
                i++;
            }
        }
    }
}
