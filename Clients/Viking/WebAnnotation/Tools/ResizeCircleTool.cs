using System;
using System.Windows.Input;
using Geometry;
using WebAnnotationModel.Objects;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// Viking's SCALE drag on a circle's rim: the radius follows the pointer's distance from the center until the button
    /// is released, then the location is saved. Escape restores the original radius.
    /// </summary>
    public sealed class ResizeCircleTool : IAnnotationTool
    {
        readonly AnnotationToolContext _context;
        readonly LocationObj _location;
        readonly Vector2 _volumeCenter;
        readonly Vector2 _mosaicCenter;
        readonly double _originalRadius;
        readonly IShape2D _originalMosaic;
        readonly Viking.AnnotationServiceTypes.Interfaces.DBACTION _originalAction;

        public ResizeCircleTool(AnnotationToolContext context, LocationObj location)
        {
            _context = context;
            _location = location;
            _mosaicCenter = location.Position;
            // As Viking does, map the mosaic center through this section's transform; the stored volume position can lag it.
            _volumeCenter = context.Mapper is { } mapper && mapper.TrySectionToVolume(_mosaicCenter, out Vector2 mapped) ? mapped : location.VolumePosition;
            _originalRadius = location.Radius;
            _originalMosaic = location.MosaicShape;
            _originalAction = location.DBAction;
            _context.Host.CapturePointer();
        }

        public string StatusText => $"Radius {_location.Radius:F1} (release to save, Esc to cancel)";

        public AnnotationToolResult OnMouseDown(Vector2 screen, MouseButton button, int clickCount) =>
            AnnotationToolResult.Continue;

        public void OnMouseMove(Vector2 screen)
        {
            Vector2 world = _context.ScreenToWorld(screen);
            double radius = Math.Max(Global.MinRadius, Vector2.Distance(_volumeCenter, world));
            LocationActions.UpdateCircleLocationNoSaveCallback(_location, _volumeCenter, _mosaicCenter, radius);
            _context.SetStatus(StatusText);
            _context.Invalidate();
        }

        public void OnMouseUp()
        {
            _context.Host.ReleasePointer();
            if (_location.Radius != _originalRadius && _originalMosaic != null)
            {
                Microsoft.SqlServer.Types.SqlGeometry undoShape = SqlGeometryUtils.Extensions.ToSqlGeometry(_originalMosaic);
                Viking.VolumeModel.IVolumeToSectionTransform mapper = _context.Mapper;
                _ = AnnotationToolActions.SaveLocationAsync(_context, _location,
                    () => UndoManager.PushLocationShape("Resize", _location, mapper, undoShape, _location.TypeCode));
            }
            _context.SetStatus(null);
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
            if (_location.Radius != _originalRadius)
            {
                LocationActions.UpdateCircleLocationNoSaveCallback(_location, _volumeCenter, _mosaicCenter, _originalRadius);
                _location.DBAction = _originalAction;
            }

            _context.SetStatus(null);
            _context.Invalidate();
        }
    }
}
