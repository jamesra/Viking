using System.Windows.Input;
using Geometry;
using SqlGeometryUtils;
using Viking.VolumeModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// Viking's TRANSLATE drag: the location follows the pointer until the button is released, then it is saved and the
    /// move recorded for undo. Escape puts it back.
    /// </summary>
    public sealed class TranslateLocationTool : IAnnotationTool
    {
        readonly AnnotationToolContext _context;
        readonly LocationObj _location;
        readonly LocationTypeKind _kind;
        readonly IShape2D _originalMosaic;
        readonly IShape2D _originalVolume;
        readonly Viking.AnnotationServiceTypes.Interfaces.DBACTION _originalAction;
        bool _moved;

        enum LocationTypeKind { Circle, Shape }

        public TranslateLocationTool(AnnotationToolContext context, LocationObj location, Vector2 worldOrigin)
        {
            _context = context;
            _location = location;
            _kind = location.TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE
                || location.TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.POINT
                ? LocationTypeKind.Circle
                : LocationTypeKind.Shape;
            _originalMosaic = location.MosaicShape;
            _originalVolume = location.VolumeShape;
            _originalAction = location.DBAction;
            _context.Host.CapturePointer();
        }

        public string StatusText => "Drag to move";

        public AnnotationToolResult OnMouseDown(Vector2 screen, MouseButton button, int clickCount) =>
            AnnotationToolResult.Continue;

        public void OnMouseMove(Vector2 screen)
        {
            Vector2 world = _context.ScreenToWorld(screen);
            IVolumeToSectionTransform mapper = _context.Mapper;
            if (!mapper.TryVolumeToSection(world, out Vector2 mosaic))
                return;

            if (_kind == LocationTypeKind.Circle)
                LocationActions.UpdateCircleLocationNoSaveCallback(_location, world, mosaic, _location.Radius);
            else
            {
                if (_location.MosaicShape != null)
                    _location.MosaicShape = _location.MosaicShape.MoveTo(mosaic);
                if (_location.VolumeShape != null)
                    _location.VolumeShape = _location.VolumeShape.MoveTo(world);
            }

            _moved = true;
            _context.Invalidate();
        }

        public void OnMouseUp()
        {
            _context.Host.ReleasePointer();
            if (!_moved || _originalMosaic == null)
                return;

            Microsoft.SqlServer.Types.SqlGeometry undoShape = _originalMosaic.ToSqlGeometry();
            IVolumeToSectionTransform mapper = _context.Mapper;
            _ = AnnotationToolActions.SaveLocationAsync(_context, _location,
                () => UndoManager.PushLocationShape("Move", _location, mapper, undoShape, _location.TypeCode));
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

            _location.MosaicShape = _originalMosaic;
            _location.VolumeShape = _originalVolume;
            _location.DBAction = _originalAction;
            _moved = false;
            _context.Invalidate();
        }
    }
}
