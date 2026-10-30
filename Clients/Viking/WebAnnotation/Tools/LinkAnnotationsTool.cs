using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using SqlGeometryUtils;
using Viking.AnnotationServiceTypes.Interfaces;
using VikingXNAGraphics;
using WebAnnotation.UI.Commands;
using WebAnnotation.View;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Color = Microsoft.Xna.Framework.Color;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// Viking's CREATELINK drag: a line follows the pointer from the origin annotation; releasing over a location of the
    /// same structure on another section links the two locations, over another structure links the structures (both ways
    /// when they are the same type). The line is green over a valid target and red over an invalid one.
    /// </summary>
    public sealed class LinkAnnotationsTool : IAnnotationTool, IAnnotationToolPreview
    {
        static readonly Color ValidColor = new(0, 255, 0, 200);
        static readonly Color InvalidColor = new(255, 0, 0, 200);
        static readonly Color NoTargetColor = new(255, 255, 255, 160);

        readonly AnnotationToolContext _context;
        readonly LocationObj _origin;
        readonly Vector2 _originPosition;
        Vector2 _pointer;
        LocationObj _candidate;

        public LinkAnnotationsTool(AnnotationToolContext context, LocationObj origin, Vector2 world)
        {
            _context = context;
            _origin = origin;
            _originPosition = OriginFor(origin);
            _pointer = world;
            _context.Host.CapturePointer();
            _context.SetStatus(StatusText);
        }

        public string StatusText => "Release over an annotation to link it (Esc to cancel)";

        /// <summary>
        /// Where the link line starts: the middle point of a line or curve as drawn, else the location's volume centre, both
        /// from the forward owner (<see cref="LocationVolumeGeometryExtensions.VolumeGeometry"/>), as Viking's link command does.
        /// </summary>
        static Vector2 OriginFor(LocationObj location)
        {
            if (location.VolumeGeometry() is not Viking.VolumeModel.LocationVolumeGeometry geometry)
                return location.Position;

            if (location.TypeCode is LocationType.OPENCURVE or LocationType.POLYLINE)
            {
                Vector2[] points = geometry.Shape.ToPoints();
                if (points.Length > 0)
                    return points[points.Length / 2];
            }

            return geometry.Center;
        }

        public AnnotationToolResult OnMouseDown(Vector2 screen, MouseButton button, int clickCount) => AnnotationToolResult.Continue;

        public void OnMouseMove(Vector2 screen)
        {
            _pointer = _context.ScreenToWorld(screen);
            _candidate = FindCandidate(_pointer);
            _context.SetStatus(_candidate == null ? StatusText : Describe(_candidate));
            _context.Invalidate();
        }

        public void OnMouseUp()
        {
            _context.Host.ReleasePointer();
            LocationObj target = FindCandidate(_pointer);
            if (LocationLinkView.IsValidLocationLinkTarget(target, _origin))
                _ = CreateLocationLinkAsync(target);
            else if (StructureLinkViewModelBase.IsValidStructureLinkTarget(target, _origin))
                _ = CreateStructureLinkAsync(target);
            else
                _context.SetStatus(target == null ? null : $"Location {target.ID} cannot be linked to location {_origin.ID}");

            _candidate = null;
            _context.Invalidate();
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
            _context.Host.ReleasePointer();
            _candidate = null;
            _context.SetStatus(null);
            _context.Invalidate();
        }

        public void DrawPreview(GraphicsDevice device, VikingXNA.Scene scene, RoundLineCode.RoundLineManager lineManager, OverlayShaderEffect overlayEffect)
        {
            Color color = _candidate == null ? NoTargetColor : IsValid(_candidate) ? ValidColor : InvalidColor;
            double width = 3 * _context.Host.Downsample;
            List<LineView> lines = new() { new LineView(_originPosition, _pointer, width, color, LineStyle.Standard) };
            if (_candidate != null)
                ToolPreviewDrawing.AddOutline(lines, _candidate, width, color);
            ToolPreviewDrawing.Draw(device, scene, lineManager, lines);
        }

        bool IsValid(LocationObj target) =>
            LocationLinkView.IsValidLocationLinkTarget(target, _origin) || StructureLinkViewModelBase.IsValidStructureLinkTarget(target, _origin);

        string Describe(LocationObj target)
        {
            if (LocationLinkView.IsValidLocationLinkTarget(target, _origin))
                return $"Release to link location {_origin.ID} to location {target.ID}";
            if (StructureLinkViewModelBase.IsValidStructureLinkTarget(target, _origin))
                return $"Release to link structure {_origin.ParentID} to structure {target.ParentID}";
            return $"Location {target.ID} cannot be linked to location {_origin.ID}";
        }

        /// <summary>Viking's candidate search: the nearest annotation under the pointer, on this section first.</summary>
        LocationObj FindCandidate(Vector2 world)
        {
            try
            {
                SectionAnnotationsView view = _context.Scene.GetOrCreate(_context.SectionNumber);
                if (view == null)
                    return null;

                IViewLocation nearest = LinkAnnotationsCommand.FindBestLinkCandidate(view, world, _origin, out _);
                return nearest != null && Store.Locations.TryGetObjectByID(nearest.ID, out LocationObj target) ? target : null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Link candidate search failed: {ex.Message}");
                return null;
            }
        }

        async Task CreateLocationLinkAsync(LocationObj target)
        {
            try
            {
                await Store.LocationLinks.CreateLink(_origin.ID, target.ID);
                _context.SetStatus($"Linked location {_origin.ID} to location {target.ID}");
                _context.Invalidate();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Create location link failed: {ex}");
                _context.SetStatus($"Could not link location {_origin.ID} to location {target.ID}");
            }
        }

        async Task CreateStructureLinkAsync(LocationObj target)
        {
            try
            {
                bool bidirectional = target.Parent?.Type?.ID == _origin.Parent?.Type?.ID;
                StructureLinkObj link = new(_origin.ParentID.Value, target.ParentID.Value, bidirectional);
                await Store.StructureLinks.Create(link);
                _context.SetStatus($"Linked structure {_origin.ParentID} to structure {target.ParentID}");
                _context.Invalidate();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Create structure link failed: {ex}");
                _context.SetStatus($"Could not link structure {_origin.ParentID} to structure {target.ParentID}");
            }
        }
    }
}
