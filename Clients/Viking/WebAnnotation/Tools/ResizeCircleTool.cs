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
        readonly Vector2 _mosaicCenter;
        readonly IShape2D _originalMosaic;
        readonly IShape2D _originalVolume;
        bool _resized;
        readonly Viking.AnnotationServiceTypes.Interfaces.DBACTION _originalAction;

        public ResizeCircleTool(AnnotationToolContext context, LocationObj location)
        {
            _context = context;
            _location = location;
            _mosaicCenter = location.Position;
            // As Viking does, map the mosaic center through this section's transform; the stored volume position can lag it.
            _volumeCenter = context.Mapper is { } mapper && mapper.TrySectionToVolume(_mosaicCenter, out Vector2 mapped) ? mapped : location.VolumePosition;
            _originalMosaic = location.MosaicShape;
            _originalVolume = location.VolumeShape;
            _originalAction = location.DBAction;
            _context.Host.CapturePointer();
        }

        public string StatusText => $"Radius {_location.Radius:F1} (release to save, Esc to cancel)";

        public AnnotationToolResult OnMouseDown(Vector2 screen, MouseButton button, int clickCount) =>
            AnnotationToolResult.Continue;

        public void OnMouseMove(Vector2 screen)
        {
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
                // Restore the shapes as they were; a radius cannot be copied between mosaic and volume space.
                _location.MosaicShape = _originalMosaic;
                _location.VolumeShape = _originalVolume;
                _location.DBAction = _originalAction;
            }

            _context.SetStatus(null);
            _context.Invalidate();
        }
    }
}
