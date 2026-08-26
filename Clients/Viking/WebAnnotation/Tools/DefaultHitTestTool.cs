using System.Windows.Input;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotation.View;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// Idle tool: hit-test, start translate/link/create, or arm a place tool on empty clicks.
    /// </summary>
    public sealed class DefaultHitTestTool : IAnnotationTool
    {
        readonly AnnotationToolContext _context;

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
        }

        public void OnMouseUp()
        {
        }

        public AnnotationToolResult OnKeyDown(Key key) => AnnotationToolResult.Continue;

        public void Cancel()
        {
        }
    }
}
