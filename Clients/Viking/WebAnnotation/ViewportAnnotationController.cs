using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.Input;
using Viking.VolumeModel;
using WebAnnotation.View;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation
{
    /// <summary>
    /// Reduced vs WinForms overlay: no pen, CREATELINK on a link view is a no-op.
    /// LocationID from the view may not be the hit object (overlap children). Drag writes mosaic+volume; save is on mouse-up.
    /// Linked create is CIRCLE only.
    /// </summary>
    public sealed class ViewportAnnotationController
    {
        readonly IViewportHost _host;
        readonly AnnotationScene _scene;
        readonly VolumeTransformProvider _transforms;
        LocationObj _dragLocation;
        Vector2 _dragWorldOrigin;
        Vector2 _dragMosaicOrigin;

        public long? SelectedStructureTypeId { get; set; }

        public event EventHandler<LocationObj> GoToRequested;

        public ViewportAnnotationController(IViewportHost host, AnnotationScene scene)
        {
            _host = host;
            _scene = scene;
            _transforms = scene.Transforms;
        }

        public void OnMouseDown(Vector2 screen, MouseButton button, int clickCount)
        {
            Vector2 world = _host.ScreenToWorld(screen);
            int sectionNumber = _host.SectionNumber;
            IVolumeToSectionTransform mapper = _transforms.GetSectionToVolumeTransform(sectionNumber);
            object hit;
            try
            {
                hit = _scene.HitTest(sectionNumber, world, out _);
            }
            catch (Exception ex)
            {
                ReportAnnotationFault("Annotation hit test", ex);
                return;
            }

            if (button == MouseButton.Right && hit is LocationCanvasView rightView)
            {
                GoToRequested?.Invoke(this, Store.Locations.TryGetObjectByID(rightView.ID, out var rightLoc) ? rightLoc : null);
                return;
            }

            if (button != MouseButton.Left)
                return;

            if (clickCount > 1 && hit is LocationCanvasView jumpView)
            {
                if (Store.Locations.TryGetObjectByID(jumpView.ID, out LocationObj loc) && loc != null)
                    GoToRequested?.Invoke(this, loc);
                return;
            }

            if (hit is IMouseActionSupport support)
            {
                try
                {
                    LocationAction action = support.GetMouseClickActionForPositionOnAnnotation(
                        world, sectionNumber, _host.CurrentModifiers, out long locationId);

                    if (action == LocationAction.TRANSLATE || action == LocationAction.SCALETRANSLATE)
                    {
                        if (Store.Locations.TryGetObjectByID(locationId, out _dragLocation) && _dragLocation != null)
                        {
                            _dragWorldOrigin = world;
                            _dragMosaicOrigin = _dragLocation.Position;
                            _host.CapturePointer();
                        }
                        return;
                    }

                    if (action == LocationAction.CREATELINK && hit is IViewLocationLink)
                    {
                        return;
                    }

                    if (action == LocationAction.CREATELINKEDLOCATION)
                    {
                        _ = CreateLinkedLocationAsync(locationId, world, sectionNumber, mapper);
                        return;
                    }

                    if (action == LocationAction.CREATELINK && hit is LocationCanvasView other)
                    {
                        if (Global.LastEditedAnnotationID.HasValue)
                            _ = CreateLocationLinkAsync(Global.LastEditedAnnotationID.Value, other.ID);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    ReportAnnotationFault("Annotation click action", ex);
                    return;
                }
            }

            if (hit is null && clickCount == 1)
            {
                if (!SelectedStructureTypeId.HasValue && Store.IsInitialized && Store.StructureTypes.RootObjects.Count > 0)
                    SelectedStructureTypeId = (long)Store.StructureTypes.RootObjects[0];

                if (SelectedStructureTypeId.HasValue && mapper.TryVolumeToSection(world, out Vector2 sectionPos))
                    _ = CreateStructureAtAsync(SelectedStructureTypeId.Value, world, sectionPos, sectionNumber);
            }
        }

        async Task CreateLinkedLocationAsync(long locationId, Vector2 world, int sectionNumber, IVolumeToSectionTransform mapper)
        {
            try
            {
                LocationObj existing = await Store.Locations.GetObjectByID(locationId);
                if (existing is null || !mapper.TryVolumeToSection(world, out Vector2 mosaic))
                    return;

                int existingSection = (int)Math.Round(existing.Z);
                if (existingSection == sectionNumber)
                    return;

                LocationObj created = new(existing.Parent, sectionNumber, LocationType.CIRCLE);
                LocationActions.UpdateCircleLocationNoSaveCallback(created, world, mosaic, Math.Max(existing.Radius, Global.MinRadius));
                await Store.Locations.Create(created, [existing.ID]);
                Global.LastEditedAnnotationID = created.ID;
                _host.Invalidate();
            }
            catch (Exception ex)
            {
                ReportAnnotationFault("Create linked location", ex);
            }
        }

        async Task CreateLocationLinkAsync(long a, long b)
        {
            try
            {
                await Store.LocationLinks.CreateLink(a, b);
                _host.Invalidate();
            }
            catch (Exception ex)
            {
                ReportAnnotationFault("Create location link", ex);
            }
        }

        async Task CreateStructureAtAsync(long typeId, Vector2 world, Vector2 sectionPos, int sectionNumber)
        {
            try
            {
                if (!Store.StructureTypes.TryGetObjectByID(typeId, out StructureTypeObj type) || type == null)
                    return;

                StructureObj structure = new(type);
                LocationObj location = new(structure, sectionNumber, LocationType.CIRCLE);
                LocationActions.UpdateCircleLocationNoSaveCallback(location, world, sectionPos, 16);
                var result = await Store.Structures.Create(structure, location);
                if (result.Location != null)
                    Global.LastEditedAnnotationID = result.Location.ID;
                _host.Invalidate();
            }
            catch (Exception ex)
            {
                ReportAnnotationFault("Create structure", ex);
            }
        }

        public void OnMouseMove(Vector2 screen)
        {
            if (_dragLocation is null)
                return;

            Vector2 world = _host.ScreenToWorld(screen);
            IVolumeToSectionTransform mapper = _transforms.GetSectionToVolumeTransform(_host.SectionNumber);
            if (!mapper.TryVolumeToSection(world, out Vector2 mosaic))
                return;

            LocationActions.UpdateCircleLocationNoSaveCallback(_dragLocation, world, mosaic, _dragLocation.Radius);
            _host.Invalidate();
        }

        public void OnMouseUp()
        {
            if (_dragLocation is null)
                return;

            LocationObj saved = _dragLocation;
            _dragLocation = null;
            _host.ReleasePointer();
            _ = SaveDraggedLocationAsync(saved);
        }

        async Task SaveDraggedLocationAsync(LocationObj location)
        {
            try
            {
                await Store.Locations.Save();
                Global.LastEditedAnnotationID = location.ID;
                _host.Invalidate();
            }
            catch (Exception ex)
            {
                ReportAnnotationFault("Save location", ex);
            }
        }

        static void ReportAnnotationFault(string action, Exception ex)
        {
            Trace.WriteLine($"{action} failed: {ex}");
        }
    }
}
