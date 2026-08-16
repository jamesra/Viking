using System;
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
    /// Toolkit-agnostic create/move/link/save on an <see cref="IViewportHost"/>.
    /// Active cell only. WPF hosts map mouse into this controller.
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
            object hit = _scene.HitTest(sectionNumber, world, out _);

            if (button == MouseButton.Right && hit is LocationCanvasView rightView)
            {
                GoToRequested?.Invoke(this, Store.Locations.GetObjectByID(rightView.ID, false));
                return;
            }

            if (button != MouseButton.Left)
                return;

            if (clickCount > 1 && hit is LocationCanvasView jumpView)
            {
                LocationObj loc = Store.Locations.GetObjectByID(jumpView.ID, false);
                if (loc != null)
                    GoToRequested?.Invoke(this, loc);
                return;
            }

            if (hit is IMouseActionSupport support)
            {
                LocationAction action = support.GetMouseClickActionForPositionOnAnnotation(
                    world, sectionNumber, _host.CurrentModifiers, out long locationId);

                if (action == LocationAction.TRANSLATE || action == LocationAction.SCALETRANSLATE)
                {
                    _dragLocation = Store.Locations.GetObjectByID(locationId, false);
                    if (_dragLocation != null)
                    {
                        _dragWorldOrigin = world;
                        _dragMosaicOrigin = _dragLocation.Position;
                        _host.CapturePointer();
                    }
                    return;
                }

                if (action == LocationAction.CREATELINK && hit is IViewLocationLink linkView)
                {
                    return;
                }

                if (action == LocationAction.CREATELINKEDLOCATION)
                {
                    LocationObj existing = Store.Locations.GetObjectByID(locationId, true);
                    if (existing is null || !mapper.TryVolumeToSection(world, out Vector2 mosaic))
                        return;

                    LocationObj created = new(existing.Parent, (int)Math.Round(existing.Z) == sectionNumber ? sectionNumber : sectionNumber, LocationType.CIRCLE);
                    LocationActions.UpdateCircleLocationNoSaveCallback(created, world, mosaic, Math.Max(existing.Radius, Global.MinRadius));
                    Store.Locations.Create(created, [existing.ID]);
                    Global.LastEditedAnnotationID = created.ID;
                    _host.Invalidate();
                    return;
                }

                if (action == LocationAction.CREATELINK && hit is LocationCanvasView other)
                {
                    if (Global.LastEditedAnnotationID.HasValue)
                    {
                        Store.LocationLinks.CreateLink(Global.LastEditedAnnotationID.Value, other.ID);
                        _host.Invalidate();
                    }
                    return;
                }
            }

            if (hit is null)
            {
                if (!SelectedStructureTypeId.HasValue && Store.IsInitialized && Store.StructureTypes.RootObjects.Count > 0)
                    SelectedStructureTypeId = (long)Store.StructureTypes.RootObjects[0];

                if (SelectedStructureTypeId.HasValue && mapper.TryVolumeToSection(world, out Vector2 sectionPos))
                {
                    StructureTypeObj type = Store.StructureTypes.GetObjectByID(SelectedStructureTypeId.Value, true);
                    if (type is null)
                        return;

                    StructureObj structure = new(type);
                    LocationObj location = new(structure, sectionNumber, LocationType.CIRCLE);
                    LocationActions.UpdateCircleLocationNoSaveCallback(location, world, sectionPos, 16);
                    var result = Store.Structures.Create(structure, location).GetAwaiter().GetResult();
                    if (result.Location != null)
                        Global.LastEditedAnnotationID = result.Location.ID;
                    _host.Invalidate();
                }
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

            Store.Locations.Save();
            Global.LastEditedAnnotationID = _dragLocation.ID;
            _dragLocation = null;
            _host.ReleasePointer();
            _host.Invalidate();
        }
    }
}
