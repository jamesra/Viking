using System.Collections.Generic;
using System.Windows.Input;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using VikingXNAGraphics;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// Viking's measure command: click an origin, and a line follows the pointer with its length in the volume's units
    /// until the second click.
    /// </summary>
    public sealed class MeasureDistanceTool : IAnnotationTool, IAnnotationToolPreview
    {
        readonly AnnotationToolContext _context;
        Vector2? _first;
        Vector2? _pointer;

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

            _context.SetStatus($"Distance: {FormatDistance(Vector2.Distance(_first.Value, world))}");
            _first = null;
            _pointer = null;
            _context.Invalidate();
            return AnnotationToolResult.Done;
        }

        public void OnMouseMove(Vector2 screen)
        {
            if (!_first.HasValue)
                return;

            _pointer = _context.ScreenToWorld(screen);
            _context.SetStatus($"Distance: {FormatDistance(Vector2.Distance(_first.Value, _pointer.Value))}");
            _context.Invalidate();
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
            _pointer = null;
            _context.SetStatus(null);
            _context.Invalidate();
        }

        public void DrawPreview(GraphicsDevice device, VikingXNA.Scene scene, RoundLineCode.RoundLineManager lineManager, OverlayShaderEffect overlayEffect)
        {
            if (!_first.HasValue || !_pointer.HasValue)
                return;

            List<LineView> lines = new() { new LineView(_first.Value, _pointer.Value, 3 * _context.Host.Downsample, ToolPreviewDrawing.ActionColor, LineStyle.Standard) };
            ToolPreviewDrawing.Draw(device, scene, lineManager, lines);
        }

        /// <summary>
        /// <paramref name="worldDistance"/> (volume pixels) in the volume's units, switching nanometers to micrometers past
        /// 1000; volume pixels when the volume has no scale.
        /// </summary>
        public static string FormatDistance(double worldDistance)
        {
            UnitsAndScale.IAxisUnits scale = AnnotationBootstrap.Volume?.DefaultXYScale;
            double perPixel = scale?.Value ?? Global.Scale.X;
            string units = scale?.Units ?? "nm";
            if (!(perPixel > 0))
                return $"{worldDistance:F1} px";

            double length = worldDistance * perPixel;
            if (units == "nm" && length >= 1000)
                return $"{length / 1000:G4} µm";
            return $"{length:G4} {units}";
        }
    }
}
