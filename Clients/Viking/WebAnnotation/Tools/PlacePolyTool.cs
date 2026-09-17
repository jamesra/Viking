using System.Collections.Generic;
using System.Windows.Input;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// Click vertices; Enter or double-click commits. Esc cancels. Used for polyline and polygon.
    /// </summary>
    public sealed class PlacePolyTool : IAnnotationTool
    {
        readonly AnnotationToolContext _context;
        readonly LocationType _shapeType;
        readonly List<Vector2> _vertices = new();

        public PlacePolyTool(AnnotationToolContext context, LocationType shapeType, Vector2 firstVertex)
        {
            _context = context;
            _shapeType = shapeType;
            _vertices.Add(firstVertex);
            _context.SetStatus(StatusText);
        }

        int MinVertices => _shapeType == LocationType.POLYGON ? 3 : 2;

        public string StatusText =>
            $"{_shapeType}: {_vertices.Count} vertices (click to add, Enter to commit, Esc to cancel)";

        public AnnotationToolResult OnMouseDown(Vector2 screen, MouseButton button, int clickCount)
        {
            if (button != MouseButton.Left)
                return AnnotationToolResult.Continue;

            Vector2 world = _context.ScreenToWorld(screen);
            if (clickCount > 1)
                return Commit();

            _vertices.Add(world);
            _context.SetStatus(StatusText);
            return AnnotationToolResult.Continue;
        }

        public void OnMouseMove(Vector2 screen)
        {
        }

        public void OnMouseUp()
        {
        }

        public AnnotationToolResult OnKeyDown(Key key)
        {
            if (key == Key.Escape)
            {
                Cancel();
                return AnnotationToolResult.Done;
            }

            if (key == Key.Enter || key == Key.Return)
                return Commit();

            if (key == Key.Back && _vertices.Count > 1)
            {
                _vertices.RemoveAt(_vertices.Count - 1);
                _context.SetStatus(StatusText);
            }

            return AnnotationToolResult.Continue;
        }

        public void Cancel()
        {
            _vertices.Clear();
            _context.SetStatus(null);
        }

        AnnotationToolResult Commit()
        {
            if (_vertices.Count < MinVertices)
            {
                _context.SetStatus($"Need at least {MinVertices} vertices");
                return AnnotationToolResult.Continue;
            }

            Vector2[] points = _vertices.ToArray();
            _ = AnnotationToolActions.CreateStructureAtAsync(_context, _shapeType, points[0], points);
            _context.SetStatus(null);
            return AnnotationToolResult.Done;
        }
    }
}
