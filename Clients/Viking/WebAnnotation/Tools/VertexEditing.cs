using System.Collections.Generic;
using System.Windows.Input;
using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework.Graphics;
using SqlGeometryUtils;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using VikingXNAGraphics;
using WebAnnotation.UI.Commands;
using WebAnnotationModel.Objects;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// The control points Viking edits for a location: polygon rings for polygons and curve polygons, a point list for
    /// polylines and curves. Edits start from the forward owner's geometry (<see cref="LocationVolumeGeometry"/>), are made in
    /// volume space and are mapped back to the section by the inverse owner, as Viking's commands do
    /// (<c>.cursor/rules/annotation-volume-geometry.mdc</c>).
    /// </summary>
    sealed class VertexEditTarget
    {
        public LocationObj Location { get; }
        public IVolumeToSectionTransform Mapper { get; }
        public bool IsPolygon { get; }
        public bool IsClosed { get; }

        /// <summary>Volume-space polygon before the edit (polygons only).</summary>
        public Polygon VolumePolygon { get; }

        /// <summary>Volume-space control points before the edit (lines and curves only).</summary>
        public Vector2[] VolumePoints { get; }

        readonly SqlGeometry _undoShape;

        VertexEditTarget(LocationObj location, IVolumeToSectionTransform mapper, Polygon volumePolygon, Vector2[] volumePoints)
        {
            Location = location;
            Mapper = mapper;
            VolumePolygon = volumePolygon;
            VolumePoints = volumePoints;
            IsPolygon = volumePolygon != null;
            IsClosed = IsPolygon || location.TypeCode == LocationType.CLOSEDCURVE;
            _undoShape = location.MosaicShape.ToSqlGeometry();
        }

        /// <summary>The editable control points of <paramref name="location"/>, or null for circles or unmappable shapes.</summary>
        public static VertexEditTarget TryCreate(AnnotationToolContext context, LocationObj location) =>
            TryCreate(context.Mapper, location);

        /// <summary>As <see cref="TryCreate(AnnotationToolContext, LocationObj)"/>, for the section <paramref name="mapper"/> maps.</summary>
        public static VertexEditTarget TryCreate(IVolumeToSectionTransform mapper, LocationObj location)
        {
            if (location?.MosaicShape == null || mapper == null)
                return null;

            LocationVolumeGeometry geometry;
            try
            {
                geometry = LocationVolumeGeometry.For(location, mapper);
            }
            catch (System.ArgumentException)
            {
                return null;
            }

            // The failure policy's extrapolated outline is not editable.
            if (!geometry.IsExact)
                return null;

            switch (location.TypeCode)
            {
                case LocationType.POLYGON:
                case LocationType.CURVEPOLYGON:
                    return new VertexEditTarget(location, mapper, geometry.ControlPolygon(), null);
                case LocationType.POLYLINE:
                case LocationType.OPENCURVE:
                case LocationType.CLOSEDCURVE:
                    return new VertexEditTarget(location, mapper, null, [.. geometry.ControlPoints]);
                default:
                    return null;
            }
        }

        /// <summary>Viking's add-control-point click: a vertex at <paramref name="world"/> on the nearest edge.</summary>
        public bool TryAdd(Vector2 world)
        {
            if (IsPolygon)
                return ApplyPolygon(AddPolygonVertexCommand.AddControlPoint(VolumePolygon, world));

            return ApplyPoints(AddLineControlPointCommand.AddControlPoint(VolumePoints, world, out _));
        }

        /// <summary>Viking's remove-control-point click: drops the vertex nearest <paramref name="world"/>, keeping a valid shape.</summary>
        public bool TryRemove(Vector2 world)
        {
            if (IsPolygon)
                return ApplyPolygon(RemovePolygonVertexCommand.RemoveControlPoint((Polygon)VolumePolygon.Clone(), world));

            int minimum = IsClosed ? 3 : 2;
            Vector2[] points = RemoveLineControlPointCommand.RemoveControlPoint(VolumePoints, world, IsClosed);
            return points != null && points.Length >= minimum && ApplyPoints(points);
        }

        /// <summary>Sets the location's shape from an edited volume polygon; false when it does not map onto the section.</summary>
        public bool ApplyPolygon(Polygon volumePolygon)
        {
            if (volumePolygon == null)
                return false;

            try
            {
                return AnnotationCommandBase.TryApplyInSection(Mapper, volumePolygon,
                    mosaic => Location.SetShapeFromGeometryInSection(Mapper, mosaic.ToSqlGeometry()), nameof(VertexEditTarget));
            }
            catch (System.ArgumentException)
            {
                // The mapped polygon is not valid geometry or does not map forward again; the location keeps its shape.
                return false;
            }
        }

        /// <summary>
        /// Sets the location's shape from edited volume control points through the inverse owner
        /// (<see cref="LocationActions.SaveVolumeLineEdit"/>); false, with the shape unchanged, when a point does not map.
        /// </summary>
        public bool ApplyPoints(Vector2[] volumePoints)
        {
            if (volumePoints == null || volumePoints.Length < 2)
                return false;

            return LocationActions.SaveVolumeLineEdit(Location, Mapper, volumePoints, null, out _);
        }

        /// <summary>Saves the edit and records it for Ctrl+Z.</summary>
        public void Save(AnnotationToolContext context)
        {
            SqlGeometry undoShape = _undoShape;
            IVolumeToSectionTransform mapper = Mapper;
            LocationObj location = Location;
            LocationType type = location.TypeCode;
            _ = AnnotationToolActions.SaveShapeEditAsync(context, location, "Edit Shape", mapper, undoShape, type);
        }

        /// <summary>Puts the shape back as it was before the edit, without saving.</summary>
        public void Restore()
        {
            Location.SetShapeFromGeometryInSection(Mapper, _undoShape);
        }
    }

    /// <summary>
    /// Viking's ADJUST drag: the control point nearest the click follows the pointer, drawn as an outline, and is applied
    /// and saved on release. Escape leaves the shape as it was.
    /// </summary>
    public sealed class AdjustVertexTool : IAnnotationTool, IAnnotationToolPreview
    {
        readonly AnnotationToolContext _context;
        readonly VertexEditTarget _target;
        readonly Polygon _adjusted;
        readonly PolygonIndex _polygonIndex;
        readonly Vector2[] _points;
        readonly int _pointIndex;
        bool _moved;

        internal AdjustVertexTool(AnnotationToolContext context, VertexEditTarget target, Vector2 world)
        {
            _context = context;
            _target = target;
            if (target.IsPolygon)
            {
                _adjusted = (Polygon)target.VolumePolygon.Clone();
                _adjusted.NearestVertex(world, out _polygonIndex);
            }
            else
            {
                _points = (Vector2[])target.VolumePoints.Clone();
                _pointIndex = _points.NearestPoint(world, out _);
            }

            _context.Host.CapturePointer();
            _context.SetStatus(StatusText);
        }

        public string StatusText => "Drag the control point (release to save, Esc to cancel)";

        public AnnotationToolResult OnMouseDown(Vector2 screen, MouseButton button, int clickCount) => AnnotationToolResult.Continue;

        public void OnMouseMove(Vector2 screen)
        {
            Vector2 world = _context.ScreenToWorld(screen);
            if (_adjusted != null)
                _adjusted[_polygonIndex] = world;
            else
            {
                _points[_pointIndex] = world;
                if (_target.IsClosed && _pointIndex == 0 && _points.Length > 1 && _points[_points.Length - 1] == _target.VolumePoints[0])
                    _points[_points.Length - 1] = world;
            }

            _moved = true;
            _context.Invalidate();
        }

        public void OnMouseUp()
        {
            _context.Host.ReleasePointer();
            _context.SetStatus(null);
            if (!_moved)
                return;

            bool applied = _adjusted != null ? _target.ApplyPolygon(_adjusted) : _target.ApplyPoints(_points);
            if (applied)
                _target.Save(_context);
            else
                _context.SetStatus("The edited shape does not fit this section's mapping; not saved");
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
            _moved = false;
            _context.SetStatus(null);
            _context.Invalidate();
        }

        public void DrawPreview(GraphicsDevice device, VikingXNA.Scene scene, RoundLineCode.RoundLineManager lineManager, OverlayShaderEffect overlayEffect)
        {
            if (!_moved)
                return;

            double width = 2 * _context.Host.Downsample;
            List<LineView> lines = new();
            if (_adjusted != null)
            {
                ToolPreviewDrawing.AddPath(lines, _adjusted.ExteriorRing, closed: true, width, ToolPreviewDrawing.ActionColor);
                foreach (Polygon hole in _adjusted.InteriorPolygons)
                    ToolPreviewDrawing.AddPath(lines, hole.ExteriorRing, closed: true, width, ToolPreviewDrawing.ActionColor);
            }
            else
                ToolPreviewDrawing.AddPath(lines, _points, closed: _target.IsClosed, width, ToolPreviewDrawing.ActionColor);

            ToolPreviewDrawing.Draw(device, scene, lineManager, lines);
        }
    }
}
