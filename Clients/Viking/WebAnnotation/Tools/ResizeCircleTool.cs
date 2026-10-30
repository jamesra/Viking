using System;
using System.Windows.Input;
using Geometry;
using WebAnnotationModel.Objects;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// Viking's SCALE drag on a circle's rim: the radius follows the pointer's distance from the center until the button
    /// is released, then the location is saved. Escape restores the original circle.
    /// </summary>
    public sealed class ResizeCircleTool : IAnnotationTool
    {
        readonly AnnotationToolContext _context;
        readonly LocationObj _location;
        readonly Vector2 _volumeCenter;
        /// <summary>False when the circle cannot be mapped exactly; the drag then changes nothing.</summary>
        readonly bool _canResize;
        readonly IShape2D _originalMosaic;
        readonly double? _originalWidth;
        bool _resized;
        readonly Viking.AnnotationServiceTypes.Interfaces.DBACTION _originalAction;

        public ResizeCircleTool(AnnotationToolContext context, LocationObj location)
        {
            _context = context;
            _location = location;
            // As Viking does, start from the circle the forward owner draws; the stored volume position can lag it.
            Circle? drawn = context.Mapper is { } mapper ? LocationActions.TryGetVolumeCircle(location, mapper) : null;
            _canResize = drawn.HasValue;
            _volumeCenter = drawn?.Center ?? location.Position;
            _originalMosaic = location.MosaicShape;
            _originalWidth = location.Width;
            _originalAction = location.DBAction;
            _context.Host.CapturePointer();
        }

        public string StatusText => $"Radius {_location.Radius:F1} (release to save, Esc to cancel)";

        public AnnotationToolResult OnMouseDown(Vector2 screen, MouseButton button, int clickCount) =>
            AnnotationToolResult.Continue;

        public void OnMouseMove(Vector2 screen)
        {
            if (!_canResize)
                return;

            Vector2 world = _context.ScreenToWorld(screen);
            // The pointer is in volume space, so this is a volume radius; the mosaic circle is mapped from it (see circle-mapping-scale.mdc).
            double radius = Math.Max(Global.MinRadius, Vector2.Distance(_volumeCenter, world));
            _resized |= LocationActions.UpdateCircleLocationNoSaveCallback(_location, _context.Mapper, _volumeCenter, radius);
            _context.SetStatus(StatusText);
            _context.Invalidate();
        }

        public void OnMouseUp()
        {
            _context.Host.ReleasePointer();
            if (_resized && _originalMosaic != null)
            {
                Microsoft.SqlServer.Types.SqlGeometry undoShape = SqlGeometryUtils.Extensions.ToSqlGeometry(_originalMosaic);
                Viking.VolumeModel.IVolumeToSectionTransform mapper = _context.Mapper;
                _ = AnnotationToolActions.SaveShapeEditAsync(_context, _location, "Resize", mapper, undoShape, _location.TypeCode);
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
            if (_resized)
            {
                // Restore the mosaic circle as it was; the volume cache is derived from it again.
                AnnotationToolActions.RestoreShape(_location, _context.Mapper, _originalMosaic, _originalWidth, _originalAction);
            }

            _context.SetStatus(null);
            _context.Invalidate();
        }
    }
}
