using System;
using System.Collections.Generic;
using System.Windows.Input;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.Input;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// Thin router: current tool gets pointer events; a short queue covers place-then-link and continue-last.
    /// </summary>
    public sealed class AnnotationToolHost
    {
        readonly IViewportHost _host;
        readonly AnnotationScene _scene;
        readonly AnnotationToolContext _context;
        readonly Queue<Func<IAnnotationTool>> _queue = new();
        IAnnotationTool _current;
        DefaultHitTestTool _defaultTool;

        public AnnotationToolHost(IViewportHost host, AnnotationScene scene)
        {
            _host = host;
            _scene = scene;
            _context = new AnnotationToolContext(
                host,
                scene,
                () => SelectedStructureTypeId,
                StartTool,
                loc => GoToRequested?.Invoke(this, loc),
                text => StatusChanged?.Invoke(this, text));
            _defaultTool = new DefaultHitTestTool(_context);
            _current = _defaultTool;
        }

        public long? SelectedStructureTypeId { get; set; }

        /// <summary>The location the user last clicked, or null after a click on empty space.</summary>
        public LocationObj SelectedLocation => _context.SelectedLocation;

        public event EventHandler<LocationObj> GoToRequested;

        public event EventHandler<string> StatusChanged;

        public AnnotationPlaceKind ArmedPlaceKind
        {
            get => _context.ArmedPlaceKind;
            set => _context.ArmedPlaceKind = value;
        }

        public bool IsIdle => ReferenceEquals(_current, _defaultTool);

        public void ArmPlace(AnnotationPlaceKind kind)
        {
            ArmedPlaceKind = kind;
            CancelCurrent();
            StatusChanged?.Invoke(this, $"Place {kind}: click in the view");
        }

        public void StartMeasure()
        {
            StartTool(new MeasureDistanceTool(_context));
        }

        public void StartTool(IAnnotationTool tool)
        {
            _current?.Cancel();
            _current = tool ?? _defaultTool;
            ShowPreviewOf(_current);
            StatusChanged?.Invoke(this, _current.StatusText);
        }

        /// <summary>Lets the scene draw the active tool's feedback, if it has any.</summary>
        void ShowPreviewOf(IAnnotationTool tool)
        {
            _scene.ToolPreview = tool as IAnnotationToolPreview;
            _scene.ToolPreviewSection = _host.SectionNumber;
            _host.Invalidate();
        }

        public void Enqueue(Func<IAnnotationTool> factory)
        {
            if (factory != null)
                _queue.Enqueue(factory);
        }

        public void OnMouseDown(Vector2 screen, MouseButton button, int clickCount)
        {
            AnnotationToolResult result = _current.OnMouseDown(screen, button, clickCount);
            HandleResult(result);
        }

        public void OnMouseMove(Vector2 screen)
        {
            _scene.ToolPreviewSection = _host.SectionNumber;
            _current.OnMouseMove(screen);
        }

        public void OnMouseUp()
        {
            _current.OnMouseUp();
            if (!ReferenceEquals(_current, _defaultTool) && _current is TranslateLocationTool or ResizeCircleTool)
                FinishCurrent(AnnotationToolResult.Done);
        }

        public bool OnKeyDown(Key key)
        {
            AnnotationToolResult result = _current.OnKeyDown(key);
            if (result != AnnotationToolResult.Continue)
            {
                HandleResult(result);
                return true;
            }

            return false;
        }

        public void CancelCurrent()
        {
            _current?.Cancel();
            _queue.Clear();
            RestoreDefault();
        }

        public void CommitCurrent()
        {
            HandleResult(_current.OnKeyDown(Key.Enter));
        }

        public void ContinueLast(Vector2 world)
        {
            if (!Global.LastEditedAnnotationID.HasValue)
                return;
            if (Store.Locations.TryGetObjectByID(Global.LastEditedAnnotationID.Value, out LocationObj last) && last != null
                && (int)Math.Round(last.Z) == _host.SectionNumber)
                return;

            // Cache miss still continues: the location may have been dropped after skipping a bad section.
            // CreateLinkedLocationAsync loads it from the server.
            long lastId = Global.LastEditedAnnotationID.Value;
            _ = AnnotationToolActions.CreateLinkedLocationAsync(_context, lastId, world, LocationType.CIRCLE, null);
        }

        /// <summary>The location Delete would remove: the selected one, else the last edited one; null if neither is cached.</summary>
        public LocationObj DeletionTarget
        {
            get
            {
                LocationObj loc = SelectedLocation;
                if (loc == null && Global.LastEditedAnnotationID.HasValue)
                    Store.Locations.TryGetObjectByID(Global.LastEditedAnnotationID.Value, out loc);
                return loc;
            }
        }

        /// <summary>Deletes <paramref name="location"/> and clears the selection if it was selected.</summary>
        public void DeleteLocation(LocationObj location)
        {
            if (location == null)
                return;

            if (ReferenceEquals(_context.SelectedLocation, location))
                _context.SelectedLocation = null;
            _ = AnnotationToolActions.DeleteLocationAsync(_context, location);
        }

        public void ToggleAnnotationsVisible()
        {
            _scene.Visible = !_scene.Visible;
            _host.Invalidate();
            StatusChanged?.Invoke(this, _scene.Visible ? null : "Annotations hidden");
        }

        void HandleResult(AnnotationToolResult result)
        {
            if (result == AnnotationToolResult.Done || result == AnnotationToolResult.DoneRepeat)
                FinishCurrent(result);
        }

        void FinishCurrent(AnnotationToolResult result)
        {
            bool repeat = result == AnnotationToolResult.DoneRepeat;
            AnnotationPlaceKind kind = ArmedPlaceKind;
            RestoreDefault();
            if (repeat)
                ArmPlace(kind);
            else if (_queue.Count > 0)
                StartTool(_queue.Dequeue()());
        }

        void RestoreDefault()
        {
            _current = _defaultTool;
            ShowPreviewOf(_current);
        }
    }
}
