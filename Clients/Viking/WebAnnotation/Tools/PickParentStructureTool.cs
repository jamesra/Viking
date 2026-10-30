using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Input;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using VikingXNAGraphics;
using WebAnnotation.View;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Color = Microsoft.Xna.Framework.Color;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// Viking's "Select Parent Structure" step for a new structure whose type has a parent type: the new annotation is
    /// drawn with a line to the annotation under the pointer, and clicking one makes its structure the parent and saves
    /// the new structure. Escape discards the new structure, as clearing Viking's command queue does.
    /// </summary>
    public sealed class PickParentStructureTool : IAnnotationTool, IAnnotationToolPreview
    {
        readonly AnnotationToolContext _context;
        readonly StructureObj _structure;
        readonly LocationObj _location;
        Vector2 _pointer;
        LocationObj _candidate;

        public PickParentStructureTool(AnnotationToolContext context, StructureObj structure, LocationObj location)
        {
            _context = context;
            _structure = structure;
            _location = location;
            _pointer = location.VolumeCenter();
        }

        public string StatusText => $"Click the parent structure of the new {_structure.Type?.Name ?? "structure"} (Esc cancels)";

        public AnnotationToolResult OnMouseDown(Vector2 screen, MouseButton button, int clickCount)
        {
            if (button != MouseButton.Left)
                return AnnotationToolResult.Continue;

            LocationObj parent = CandidateAt(_context.ScreenToWorld(screen));
            if (parent?.ParentID is not long parentStructureId)
            {
                _context.SetStatus("Click an annotation of the parent structure");
                return AnnotationToolResult.Continue;
            }

            _structure.ParentID = parentStructureId;
            _ = AnnotationToolActions.CreateOrPickParentAsync(_context, _structure, _location);
            return AnnotationToolResult.Done;
        }

        public void OnMouseMove(Vector2 screen)
        {
            _pointer = _context.ScreenToWorld(screen);
            _candidate = CandidateAt(_pointer);
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
            _candidate = null;
            _context.SetStatus(null);
            _context.Invalidate();
        }

        /// <summary>Viking's rule: any annotation except the new one and those already belonging to the new structure.</summary>
        LocationObj CandidateAt(Vector2 world)
        {
            try
            {
                object hit = _context.Scene.HitTest(_context.SectionNumber, world, out _);
                if (hit is not LocationCanvasView view || view.ID == _location.ID || view.ParentID == _structure.ID)
                    return null;

                return Store.Locations.TryGetObjectByID(view.ID, out LocationObj location) ? location : null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Parent hit test failed: {ex.Message}");
                return null;
            }
        }

        public void DrawPreview(GraphicsDevice device, VikingXNA.Scene scene, RoundLineCode.RoundLineManager lineManager, OverlayShaderEffect overlayEffect)
        {
            double width = 3 * _context.Host.Downsample;
            Color color = _candidate != null ? ToolPreviewDrawing.ActionColor : ToolPreviewDrawing.HoverColor;
            List<LineView> lines = new();
            ToolPreviewDrawing.AddOutline(lines, _location, width, ToolPreviewDrawing.ActionColor);
            Vector2 target = _candidate?.VolumeCenter() ?? _pointer;
            lines.Add(new LineView(_location.VolumeCenter(), target, width, color, LineStyle.Standard));
            if (_candidate != null)
                ToolPreviewDrawing.AddOutline(lines, _candidate, width, color);
            ToolPreviewDrawing.Draw(device, scene, lineManager, lines);
        }
    }
}
