using System.Collections.Generic;
using System.Windows.Input;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using VikingXNAGraphics;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotation.View;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// Idle tool: hit-test, start translate/resize/link/create, or arm a place tool on empty clicks. Outlines the annotation
    /// under the pointer and names what a click there would do, as Viking's cursor does.
    /// </summary>
    public sealed class DefaultHitTestTool : IAnnotationTool, IAnnotationToolPreview
    {
        readonly AnnotationToolContext _context;
        LocationObj _hovered;
        LocationAction _hoverAction;

        public DefaultHitTestTool(AnnotationToolContext context)
        {
            _context = context;
        }

        public string StatusText => "Click to place or edit";

        public AnnotationToolResult OnMouseDown(Vector2 screen, MouseButton button, int clickCount)
        {
            Vector2 world = _context.ScreenToWorld(screen);
            int sectionNumber = _context.SectionNumber;
            object hit;
            try
            {
                hit = _context.Scene.HitTest(sectionNumber, world, out _);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Annotation hit test failed: {ex}");
                return AnnotationToolResult.Continue;
            }

            if (button == MouseButton.Right && hit is LocationCanvasView rightView)
            {
                if (Store.Locations.TryGetObjectByID(rightView.ID, out LocationObj rightLoc))
                    _context.RequestGoTo(rightLoc);
                return AnnotationToolResult.Continue;
            }

            if (button != MouseButton.Left)
                return AnnotationToolResult.Continue;

            _context.SelectedLocation = hit is LocationCanvasView selected && Store.Locations.TryGetObjectByID(selected.ID, out LocationObj selectedLoc)
                ? selectedLoc
                : null;

            if (clickCount > 1 && hit is LocationCanvasView jumpView)
            {
                if (Store.Locations.TryGetObjectByID(jumpView.ID, out LocationObj loc) && loc != null)
                    _context.RequestGoTo(loc);
                return AnnotationToolResult.Continue;
            }

            if (hit is IMouseActionSupport support)
            {
                try
                {
                    LocationAction action = support.GetMouseClickActionForPositionOnAnnotation(
                        world, sectionNumber, _context.Host.CurrentModifiers, out long locationId);

                    if (action == LocationAction.SCALE)
                    {
                        if (Store.Locations.TryGetObjectByID(locationId, out LocationObj resize) && resize?.TypeCode == LocationType.CIRCLE)
                            _context.StartTool(new ResizeCircleTool(_context, resize));
                        return AnnotationToolResult.Continue;
                    }

                    if (action == LocationAction.TRANSLATE || action == LocationAction.SCALETRANSLATE)
                    {
                        if (Store.Locations.TryGetObjectByID(locationId, out LocationObj drag) && drag != null)
                            _context.StartTool(new TranslateLocationTool(_context, drag, world));
                        return AnnotationToolResult.Continue;
                    }

                    if (action == LocationAction.CREATELINKEDLOCATION)
                    {
                        _ = AnnotationToolActions.CreateLinkedLocationAsync(_context, locationId, world, LocationType.CIRCLE, null);
                        return AnnotationToolResult.Continue;
                    }

                    if (action == LocationAction.CREATELINK && hit is LocationCanvasView other)
                    {
                        if (Global.LastEditedAnnotationID.HasValue)
                            _ = AnnotationToolActions.CreateLocationLinkAsync(_context, Global.LastEditedAnnotationID.Value, other.ID);
                        return AnnotationToolResult.Continue;
                    }
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"Annotation click action failed: {ex}");
                    return AnnotationToolResult.Continue;
                }
            }

            if (hit is null && clickCount == 1)
            {
                if (_context.ArmedPlaceKind == AnnotationPlaceKind.Polyline)
                {
                    _context.StartTool(new PlacePolyTool(_context, LocationType.POLYLINE, world));
                    return AnnotationToolResult.Continue;
                }

                if (_context.ArmedPlaceKind == AnnotationPlaceKind.Polygon)
                {
                    _context.StartTool(new PlacePolyTool(_context, LocationType.POLYGON, world));
                    return AnnotationToolResult.Continue;
                }

                _ = AnnotationToolActions.CreateStructureAtAsync(_context, LocationType.CIRCLE, world, null);
            }

            return AnnotationToolResult.Continue;
        }

        public void OnMouseMove(Vector2 screen)
        {
            Vector2 world = _context.ScreenToWorld(screen);
            int sectionNumber = _context.SectionNumber;
            LocationObj hovered = null;
            LocationAction action = LocationAction.NONE;
            try
            {
                object hit = _context.Scene.HitTest(sectionNumber, world, out _);
                if (hit is LocationCanvasView view && Store.Locations.TryGetObjectByID(view.ID, out LocationObj loc))
                {
                    hovered = loc;
                    if (hit is IMouseActionSupport support)
                        action = support.GetMouseClickActionForPositionOnAnnotation(world, sectionNumber, _context.Host.CurrentModifiers, out _);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Annotation hover hit test failed: {ex.Message}");
            }

            if (ReferenceEquals(hovered, _hovered) && action == _hoverAction)
                return;

            _hovered = hovered;
            _hoverAction = action;
            _context.SetStatus(HoverHint(hovered, action));
            _context.Invalidate();
        }

        public void OnMouseUp()
        {
        }

        public AnnotationToolResult OnKeyDown(Key key) => AnnotationToolResult.Continue;

        public void Cancel()
        {
            _hovered = null;
            _hoverAction = LocationAction.NONE;
        }

        public void DrawPreview(GraphicsDevice device, VikingXNA.Scene scene, RoundLineCode.RoundLineManager lineManager, OverlayShaderEffect overlayEffect)
        {
            if (_hovered == null)
                return;

            List<LineView> lines = new();
            bool acts = _hoverAction != LocationAction.NONE;
            ToolPreviewDrawing.AddOutline(lines, _hovered, (acts ? 3 : 2) * _context.Host.Downsample,
                acts ? ToolPreviewDrawing.ActionColor : ToolPreviewDrawing.HoverColor);
            ToolPreviewDrawing.Draw(device, scene, lineManager, lines);
        }

        static string HoverHint(LocationObj hovered, LocationAction action)
        {
            if (hovered == null)
                return null;

            string what = $"Location {hovered.ID} of structure {hovered.ParentID}";
            return action switch
            {
                LocationAction.TRANSLATE => $"{what}: drag to move",
                LocationAction.SCALE => $"{what}: drag to resize",
                LocationAction.SCALETRANSLATE => $"{what}: drag to move",
                LocationAction.CREATELINKEDLOCATION => $"{what}: click to continue it on this section",
                LocationAction.CREATELINK => $"{what}: click to link the last edited location to it",
                _ => what
            };
        }
    }
}
