using System.Windows.Input;
using Geometry;

namespace WebAnnotation.Tools
{
    public sealed class MeasureDistanceTool : IAnnotationTool
    {
        readonly AnnotationToolContext _context;
        Vector2? _first;

        public MeasureDistanceTool(AnnotationToolContext context)
        {
            _context = context;
            _context.SetStatus("Click two points to measure");
        }

        public string StatusText => _first.HasValue ? "Click the second point" : "Click the first point";

        public AnnotationToolResult OnMouseDown(Vector2 screen, MouseButton button, int clickCount)
        {
            if (button != MouseButton.Left)
                return AnnotationToolResult.Continue;

            Vector2 world = _context.ScreenToWorld(screen);
            if (!_first.HasValue)
            {
                _first = world;
                _context.SetStatus(StatusText);
                return AnnotationToolResult.Continue;
            }

            double dx = world.X - _first.Value.X;
            double dy = world.Y - _first.Value.Y;
            double dist = System.Math.Sqrt(dx * dx + dy * dy);
            _context.SetStatus($"Distance: {dist:F1}");
            _first = null;
            return AnnotationToolResult.Done;
        }

        public void OnMouseMove(Vector2 screen)
        {
            if (!_first.HasValue)
                return;
            Vector2 world = _context.ScreenToWorld(screen);
            double dx = world.X - _first.Value.X;
            double dy = world.Y - _first.Value.Y;
            _context.SetStatus($"Distance: {System.Math.Sqrt(dx * dx + dy * dy):F1}");
        }

        public void OnMouseUp()
        {
        }

        public AnnotationToolResult OnKeyDown(Key key)
        {
            if (key != Key.Escape)
                return AnnotationToolResult.Continue;
            Cancel();
            return AnnotationToolResult.Done;
        }

        public void Cancel()
        {
            _first = null;
            _context.SetStatus(null);
        }
    }
}
