using System.Collections.Generic;
using System.Windows.Input;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using Viking.AnnotationServiceTypes.Interfaces;
using VikingXNAGraphics;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// Click vertices; Enter or double-click commits. Esc cancels. Used for polyline and polygon. Draws the placed vertices
    /// and a rubber band to the pointer.
    /// </summary>
    public sealed class PlacePolyTool : IAnnotationTool, IAnnotationToolPreview
    {
        readonly AnnotationToolContext _context;
        readonly LocationType _shapeType;
        readonly List<Vector2> _vertices = new();
        readonly long? _typeId;
        readonly IReadOnlyList<string> _tags;
        Vector2? _pointer;

        /// <param name="typeId">The structure type to create; null uses the type chosen in the structure type list.</param>
        /// <param name="tags">Tags a structure hotkey adds to the new structure.</param>
        public PlacePolyTool(AnnotationToolContext context, LocationType shapeType, Vector2 firstVertex, long? typeId = null, IReadOnlyList<string> tags = null)
        {
            _context = context;
            _shapeType = shapeType;
            _typeId = typeId;
            _tags = tags;
            _vertices.Add(firstVertex);
            _context.SetStatus(StatusText);
        }

        bool IsClosed => _shapeType is LocationType.POLYGON or LocationType.CURVEPOLYGON or LocationType.CLOSEDCURVE;

        int MinVertices => IsClosed ? 3 : 2;

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
            _context.Invalidate();
            return AnnotationToolResult.Continue;
        }

        public void OnMouseMove(Vector2 screen)
        {
            _pointer = _context.ScreenToWorld(screen);
            _context.Invalidate();
        }

        public void DrawPreview(GraphicsDevice device, VikingXNA.Scene scene, RoundLineCode.RoundLineManager lineManager, OverlayShaderEffect overlayEffect)
        {
            if (_vertices.Count == 0)
                return;

            double width = 2 * _context.Host.Downsample;
            List<LineView> lines = new();
            List<Vector2> path = new(_vertices);
            if (_pointer.HasValue)
                path.Add(_pointer.Value);
            ToolPreviewDrawing.AddPath(lines, path, closed: IsClosed, width, ToolPreviewDrawing.ActionColor);
            foreach (Vector2 vertex in _vertices)
                ToolPreviewDrawing.AddRing(lines, vertex, 4 * _context.Host.Downsample, width, ToolPreviewDrawing.ActionColor);
            ToolPreviewDrawing.Draw(device, scene, lineManager, lines);
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
                _context.Invalidate();
            }

            return AnnotationToolResult.Continue;
        }

        public void Cancel()
        {
            _vertices.Clear();
            _context.SetStatus(null);
            _context.Invalidate();
        }

        AnnotationToolResult Commit()
        {
            if (_vertices.Count < MinVertices)
            {
                _context.SetStatus($"Need at least {MinVertices} vertices");
                return AnnotationToolResult.Continue;
            }

            Vector2[] points = _vertices.ToArray();
            _ = AnnotationToolActions.CreateStructureAtAsync(_context, _shapeType, points[0], points, _typeId, _tags);
            _context.SetStatus(null);
            return AnnotationToolResult.Done;
        }
    }
}
