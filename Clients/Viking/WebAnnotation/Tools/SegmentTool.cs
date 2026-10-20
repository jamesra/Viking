using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using Viking.AnnotationServiceTypes.Interfaces;
using VikingXNAGraphics;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotation.UI.Commands.Segmentation;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Color = Microsoft.Xna.Framework.Color;
using SegmentationResponse = Viking.gRPC.SegmentationServiceTypes.V1.SegmentationResponse;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// Viking's interactive SAM2 segmentation (Tab) for hosts without its WinForms command. Left click adds an object
    /// point, right click a background point, middle click removes the nearest point, Backspace the last one. Each change
    /// asks the segmentation service through Viking's <see cref="SegmentationViewportSession"/> and outlines the result;
    /// Enter, or a double click inside an outline, creates a structure from it. Escape cancels.
    /// </summary>
    public sealed class SegmentTool : IAnnotationTool, IAnnotationToolPreview, IAnnotationToolUsesAllButtons
    {
        static readonly Color ForegroundColor = new(0, 220, 0, 230);
        static readonly Color BackgroundColor = new(230, 0, 0, 230);

        readonly AnnotationToolContext _context;
        readonly SegmentationViewportSession _session;
        readonly long _structureTypeId;
        readonly List<Vector2> _foreground = new();
        readonly List<Vector2> _background = new();
        IReadOnlyList<Polygon> _polygons = Array.Empty<Polygon>();
        CancellationTokenSource _request;
        int _generation;
        bool _finished;

        /// <param name="channels">The segmentation service's channel manager.</param>
        public SegmentTool(AnnotationToolContext context, ISegmentationViewport viewport,
            Viking.Services.Grpc.IGrpcChannelManager channels, long structureTypeId)
        {
            _context = context;
            _structureTypeId = structureTypeId;
            _session = new SegmentationViewportSession(viewport, channels);
            if (!_session.TryInitializeClient())
                _context.SetStatus("Segment: no segmentation service is available");
            else
                _context.SetStatus(StatusText);
        }

        public string StatusText =>
            "Segment: left-click the object, right-click background, Enter to accept, Backspace removes the last point, Esc cancels";

        public AnnotationToolResult OnMouseDown(Vector2 screen, MouseButton button, int clickCount)
        {
            if (_finished)
                return AnnotationToolResult.Done;

            Vector2 world = _context.ScreenToWorld(screen);
            if (button == MouseButton.Left && clickCount > 1)
                return Accept(world);

            switch (button)
            {
                case MouseButton.Left:
                    _foreground.Add(world);
                    break;
                case MouseButton.Right:
                    _background.Add(world);
                    break;
                case MouseButton.Middle:
                    if (!RemoveNearestPoint(world))
                        return AnnotationToolResult.Continue;
                    break;
                default:
                    return AnnotationToolResult.Continue;
            }

            RequestSegmentation();
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
            switch (key)
            {
                case Key.Escape:
                    Cancel();
                    return AnnotationToolResult.Done;
                case Key.Enter:
                    return Accept(_foreground.Count > 0 ? _foreground[0] : null);
                case Key.Back:
                    if (_background.Count > 0 && (_foreground.Count == 0 || _background.Count >= _foreground.Count))
                        _background.RemoveAt(_background.Count - 1);
                    else if (_foreground.Count > 0)
                        _foreground.RemoveAt(_foreground.Count - 1);
                    RequestSegmentation();
                    return AnnotationToolResult.Continue;
                default:
                    return AnnotationToolResult.Continue;
            }
        }

        public void Cancel()
        {
            _finished = true;
            _request?.Cancel();
            _session.CancelPendingWork();
            _ = DeleteServerImageAsync();
            _polygons = Array.Empty<Polygon>();
            _context.SetStatus(null);
            _context.Invalidate();
        }

        bool RemoveNearestPoint(Vector2 world)
        {
            double fgDistance = double.MaxValue;
            double bgDistance = double.MaxValue;
            int fg = _foreground.Count > 0 ? _foreground.NearestPoint(world, out fgDistance) : -1;
            int bg = _background.Count > 0 ? _background.NearestPoint(world, out bgDistance) : -1;
            if (fg < 0 && bg < 0)
                return false;

            if (fg >= 0 && fgDistance <= bgDistance)
                _foreground.RemoveAt(fg);
            else
                _background.RemoveAt(bg);
            return true;
        }

        /// <summary>Starts a request for the current points; a newer request supersedes an older one's result.</summary>
        void RequestSegmentation()
        {
            _request?.Cancel();
            _context.Invalidate();
            if (_foreground.Count == 0)
            {
                _polygons = Array.Empty<Polygon>();
                _context.SetStatus(StatusText);
                return;
            }

            CancellationTokenSource request = new();
            _request = request;
            int generation = ++_generation;
            Vector2[] foreground = _foreground.ToArray();
            Vector2[] background = _background.ToArray();
            double downsample = _context.Host.Downsample;
            _context.SetStatus("Segmenting…");
            _ = SegmentAsync(generation, foreground, background, downsample, request.Token);
        }

        async Task SegmentAsync(int generation, Vector2[] foreground, Vector2[] background, double downsample, CancellationToken token)
        {
            try
            {
                SegmentationResponse response = await _session.SegmentAsync(foreground, background, token);
                if (token.IsCancellationRequested || generation != _generation)
                    return;

                if (response is null)
                {
                    SegmentationSkipKind kind = SegmentationUserFeedback.KindForMissingResponse(_session.ConsumeLastSkip(out string detail));
                    _context.SetStatus(SegmentationUserFeedback.Format(kind, detail) ?? "Segment failed");
                    return;
                }

                IReadOnlyList<Polygon> polygons = await Task.Run(() =>
                    _session.CreatePolygonsFromResponse(response,
                            preserveHolesContainingWorldPoints: background,
                            cancellationToken: token,
                            keepComponentsContainingWorldPoints: foreground)
                        .Select(p => AutoPolygonizeSelection.SimplifyForCreatedShape(p, downsample))
                        .ToList(), token);
                if (token.IsCancellationRequested || generation != _generation)
                    return;

                _polygons = polygons;
                _context.SetStatus(polygons.Count == 0
                    ? SegmentationUserFeedback.Format(SegmentationSkipKind.EmptyMask, null)
                    : $"Segment: {polygons.Count} outline(s); Enter or double-click inside one to accept");
                _context.Invalidate();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Segmentation failed: {ex}");
                _context.SetStatus(SegmentationUserFeedback.Format(SegmentationSkipKind.Error, ex.Message));
            }
        }

        /// <summary>Creates a structure from the outline containing <paramref name="at"/>, else the largest outline.</summary>
        AnnotationToolResult Accept(Vector2? at)
        {
            Polygon chosen = at.HasValue ? _polygons.FirstOrDefault(p => p.Contains(at.Value)) : null;
            chosen ??= _polygons.OrderByDescending(p => p.Area).FirstOrDefault();
            if (chosen == null || chosen.ExteriorRing.Length < 4)
            {
                _context.SetStatus("Segment: nothing to accept yet");
                return AnnotationToolResult.Continue;
            }

            _finished = true;
            _request?.Cancel();
            _ = CreateStructureAsync(chosen, _context.Host.Downsample);
            _ = DeleteServerImageAsync();
            _polygons = Array.Empty<Polygon>();
            _context.Invalidate();
            return AnnotationToolResult.Done;
        }

        /// <summary>As Viking's CreateAnnotationFromPolygon: a curve polygon simplified at the save tolerance.</summary>
        async Task CreateStructureAsync(Polygon volumePolygon, double downsample)
        {
            try
            {
                if (!Store.StructureTypes.TryGetObjectByID(_structureTypeId, out StructureTypeObj type) || type == null)
                {
                    _context.SetStatus($"Structure type {_structureTypeId} is not in this volume");
                    return;
                }

                StructureObj structure = new(type);
                LocationObj location = new(structure, _context.SectionNumber, LocationType.POLYGON);
                LocationShapeUpdate.AssignSimplifiedVolumePolygon(location, volumePolygon, _context.Mapper, downsample);
                var result = await Store.Structures.Create(structure, location);
                if (result.Location != null)
                    Global.LastEditedAnnotationID = result.Location.ID;
                _context.SetStatus($"Created {type.Name} {result.Structure?.ID}");
                _context.Invalidate();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Create segmented structure failed: {ex}");
                _context.SetStatus($"Could not create the segmented structure: {ex.Message}");
            }
        }

        async Task DeleteServerImageAsync()
        {
            try
            {
                await _session.DeleteCurrentImageAsync();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Segmentation image cleanup failed: {ex.Message}");
            }
        }

        public void DrawPreview(GraphicsDevice device, VikingXNA.Scene scene, RoundLineCode.RoundLineManager lineManager, OverlayShaderEffect overlayEffect)
        {
            double width = 2 * _context.Host.Downsample;
            List<LineView> lines = new();
            foreach (Polygon polygon in _polygons)
            {
                ToolPreviewDrawing.AddPath(lines, polygon.ExteriorRing, closed: true, width, ToolPreviewDrawing.ActionColor);
                foreach (Polygon hole in polygon.InteriorPolygons)
                    ToolPreviewDrawing.AddPath(lines, hole.ExteriorRing, closed: true, width, ToolPreviewDrawing.ActionColor);
            }

            double radius = 5 * _context.Host.Downsample;
            foreach (Vector2 point in _foreground)
                ToolPreviewDrawing.AddRing(lines, point, radius, width, ForegroundColor);
            foreach (Vector2 point in _background)
                ToolPreviewDrawing.AddRing(lines, point, radius, width, BackgroundColor);
            ToolPreviewDrawing.Draw(device, scene, lineManager, lines);
        }
    }
}
