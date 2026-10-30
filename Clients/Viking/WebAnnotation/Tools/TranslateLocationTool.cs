using System.Windows.Input;
using Geometry;
using SqlGeometryUtils;
using Viking.VolumeModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// Viking's TRANSLATE drag: the location follows the pointer until the button is released, then it is saved and the
    /// move recorded for undo. Escape puts it back. As Viking's commands do (<c>.cursor/rules/annotation-volume-geometry.mdc</c>),
    /// a circle saves the moved volume circle through the inverse owner, a line or curve moves the control points and width
    /// the forward owner draws and maps them back, and a polygon moves its mosaic shape by the drag mapped back
    /// (<see cref="UI.Commands.TranslateScaleCommandBase.TryMosaicDelta"/>).
    /// </summary>
    public sealed class TranslateLocationTool : IAnnotationTool
    {
        readonly AnnotationToolContext _context;
        readonly LocationObj _location;
        readonly LocationTypeKind _kind;
        readonly Vector2 _worldOrigin;
        /// <summary>A circle keeps its size in the volume (what the user sees) as it is dragged. Null when it cannot be mapped.</summary>
        readonly double? _volumeRadius;
        /// <summary>The control points a line or curve is drawn with at the start of the drag; null otherwise or when unmappable.</summary>
        readonly Vector2[] _startVolumePoints;
        readonly double _startVolumeWidth;
        readonly Microsoft.SqlServer.Types.SqlGeometry _startMosaic;
        readonly IShape2D _originalMosaic;
        readonly double? _originalWidth;
        readonly Viking.AnnotationServiceTypes.Interfaces.DBACTION _originalAction;
        bool _moved;

        enum LocationTypeKind { Circle, Line, Shape }

        public TranslateLocationTool(AnnotationToolContext context, LocationObj location, Vector2 worldOrigin)
        {
            _context = context;
            _location = location;
            _worldOrigin = worldOrigin;
            _kind = location.TypeCode switch
            {
                Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE or Viking.AnnotationServiceTypes.Interfaces.LocationType.POINT => LocationTypeKind.Circle,
                Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYLINE or Viking.AnnotationServiceTypes.Interfaces.LocationType.OPENCURVE
                    or Viking.AnnotationServiceTypes.Interfaces.LocationType.CLOSEDCURVE => LocationTypeKind.Line,
                _ => LocationTypeKind.Shape,
            };
            _volumeRadius = LocationActions.TryGetVolumeCircle(location, context.Mapper)?.Radius;
            if (_kind == LocationTypeKind.Line && TryExactGeometry(location, context.Mapper) is LocationVolumeGeometry geometry)
            {
                _startVolumePoints = [.. geometry.ControlPoints];
                _startVolumeWidth = geometry.Width;
            }

            _originalMosaic = location.MosaicShape;
            _startMosaic = _originalMosaic is null ? null : LocationVolumeGeometry.ToSqlGeometry(_originalMosaic);
            _originalWidth = location.Width;
            _originalAction = location.DBAction;
            _context.Host.CapturePointer();
        }

        /// <summary>The location's geometry as drawn, or null when the owner cannot map it exactly (it is then not moved).</summary>
        static LocationVolumeGeometry TryExactGeometry(LocationObj location, IVolumeToSectionTransform mapper)
        {
            try
            {
                LocationVolumeGeometry geometry = LocationVolumeGeometry.For(location, mapper);
                return geometry.IsExact ? geometry : null;
            }
            catch (System.ArgumentException)
            {
                return null;
            }
        }

        public string StatusText => "Drag to move";

        public AnnotationToolResult OnMouseDown(Vector2 screen, MouseButton button, int clickCount) =>
            AnnotationToolResult.Continue;

        public void OnMouseMove(Vector2 screen)
        {
            Vector2 world = _context.ScreenToWorld(screen);
            IVolumeToSectionTransform mapper = _context.Mapper;
            switch (_kind)
            {
                case LocationTypeKind.Circle:
                    if (_volumeRadius is not double volumeRadius || !LocationActions.UpdateCircleLocationNoSaveCallback(_location, mapper, world, volumeRadius))
                        return;
                    break;
                case LocationTypeKind.Line:
                    if (_startVolumePoints is null)
                        return;

                    Vector2 drag = world - _worldOrigin;
                    Vector2[] moved = new Vector2[_startVolumePoints.Length];
                    for (int i = 0; i < moved.Length; i++)
                        moved[i] = _startVolumePoints[i] + drag;

                    if (!LocationActions.SaveVolumeLineEdit(_location, mapper, moved, _startVolumeWidth, out _))
                        return;
                    break;
                default:
                    if (_startMosaic is null || !UI.Commands.TranslateScaleCommandBase.TryMosaicDelta(mapper, _worldOrigin, world, out Vector2 mosaicDelta))
                        return;

                    try
                    {
                        _location.SetShapeFromGeometryInSection(mapper, _startMosaic.Translate(mosaicDelta));
                    }
                    catch (System.ArgumentException)
                    {
                        // The moved shape leaves the region this section's transform maps; it stays where it last fitted.
                        return;
                    }

                    break;
            }

            _moved = true;
            _context.Invalidate();
        }

        public void OnMouseUp()
        {
            _context.Host.ReleasePointer();
            if (!_moved || _originalMosaic == null)
                return;

            IVolumeToSectionTransform mapper = _context.Mapper;
            _ = AnnotationToolActions.SaveShapeEditAsync(_context, _location, "Move", mapper, _startMosaic, _location.TypeCode);
        }

        public AnnotationToolResult OnKeyDown(Key key)
        {
            if (key == Key.Escape)
            {
                Cancel();
                return AnnotationToolResult.Done;
            }

            return AnnotationToolResult.Continue;
        }

        public void Cancel()
        {
            _context.Host.ReleasePointer();
            if (!_moved)
                return;

            AnnotationToolActions.RestoreShape(_location, _context.Mapper, _originalMosaic, _originalWidth, _originalAction);
            _moved = false;
            _context.Invalidate();
        }
    }
}
