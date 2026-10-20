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

        /// <summary>Set while a tool handles an event; a tool started meanwhile waits in <see cref="_pendingStart"/>.</summary>
        int _dispatchDepth;
        IAnnotationTool _pendingStart;

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
            _context.ProposeChoices = choices => ChoicesProposed?.Invoke(this, choices);
            // Structures a pen stroke creates are saved here, asking for a parent when the type needs one.
            UI.Actions.CreateStructureActionBase.CommitHandler = (structure, location) =>
                AnnotationToolActions.CreateOrPickParentAsync(_context, structure, location);
            _defaultTool = new DefaultHitTestTool(_context);
            _current = _defaultTool;
            _viewSection = host.SectionNumber;
            host.ViewportChanged += OnViewportChanged;
        }

        public long? SelectedStructureTypeId { get; set; }

        int _viewSection;

        /// <summary>
        /// A tool works on the section it started on: when the view moves to another section the active tool is
        /// cancelled (nothing it was building is saved) and the hover target is dropped.
        /// </summary>
        void OnViewportChanged(object sender, EventArgs e)
        {
            if (_host.SectionNumber == _viewSection)
                return;

            _viewSection = _host.SectionNumber;
            ClearHover();
            if (IsIdle)
                return;

            _current.Cancel();
            _queue.Clear();
            RestoreDefault();
            StatusChanged?.Invoke(this, "The section changed, so the tool in progress was cancelled");
        }

        /// <summary>Forgets the annotation under the pointer (the pointer left the view, or the section changed).</summary>
        public void ClearHover()
        {
            if (!IsIdle)
                return;

            _defaultTool.Cancel();
            _host.Invalidate();
        }

        /// <summary>The location the user last clicked, or null after a click on empty space.</summary>
        public LocationObj SelectedLocation => _context.SelectedLocation;

        public event EventHandler<LocationObj> GoToRequested;

        public event EventHandler<string> StatusChanged;

        /// <summary>Choices for the user to pick from at the pointer, such as what a pen stroke should do.</summary>
        public event EventHandler<IReadOnlyList<AnnotationChoice>> ChoicesProposed;

        /// <summary>Viking's Pen Mode (numpad * or Shift+8): a drag on the view draws a stroke instead of continuing a trace.</summary>
        public void TogglePenMode()
        {
            Global.PenMode = !Global.PenMode;
            StatusChanged?.Invoke(this, Global.PenMode ? "Pen mode on: drag to draw a stroke" : "Pen mode off");
        }

        public AnnotationPlaceKind ArmedPlaceKind
        {
            get => _context.ArmedPlaceKind;
            set => _context.ArmedPlaceKind = value;
        }

        public bool IsIdle => ReferenceEquals(_current, _defaultTool);

        /// <summary>True while interactive segmentation is the active tool.</summary>
        public bool IsSegmenting => _current is SegmentTool;

        /// <summary>The segmentation service's channels, for tools and auto-polygonize; null when there is no service.</summary>
        public Viking.Services.Grpc.IGrpcChannelManager SegmentationChannels { get; set; }

        /// <summary>The pointer moved over the view (volume coordinates), with or without a button held.</summary>
        public event EventHandler<Vector2> PointerMoved;

        /// <summary>
        /// Offered every press while no tool is active, before the default tool (auto-polygonize proposals take double
        /// clicks); returns true when it handled the press.
        /// </summary>
        public Func<Vector2, MouseButton, int, bool> PreviewMouseDown { get; set; }

        /// <summary>Auto-polygonize's "Add points" on a proposal: interactive segmentation from its prompts.</summary>
        internal void StartProposalSegmentation(UI.Commands.Segmentation.ISegmentationViewport viewport, UI.AutoPolygonize.ProposalSegmentationRequest request)
        {
            StartTool(new SegmentTool(_context, viewport, SegmentationChannels, request.StructureTypeId, request.Foreground,
                request.StartingBoxes, request.OnAccepted, request.OnAbandoned));
        }

        /// <summary>True while the active tool takes right and middle clicks itself (see <see cref="IAnnotationToolUsesAllButtons"/>).</summary>
        public bool ToolUsesAllButtons => _current is IAnnotationToolUsesAllButtons;

        public void ArmPlace(AnnotationPlaceKind kind)
        {
            ArmedPlaceKind = kind;
            CancelCurrent();
            StatusChanged?.Invoke(this, SelectedStructureTypeId.HasValue
                ? $"Place {kind}: click in the view"
                : $"Place {kind}: choose a structure type, then click in the view");
        }

        public void StartMeasure()
        {
            StartTool(new MeasureDistanceTool(_context));
        }

        /// <summary>
        /// Makes <paramref name="tool"/> current. Called from inside another tool's event (a placement that goes on to ask
        /// for a parent structure), it starts once that event's result has been handled, so the finishing tool does not
        /// replace it.
        /// </summary>
        public void StartTool(IAnnotationTool tool)
        {
            if (_dispatchDepth > 0)
            {
                _pendingStart = tool ?? _defaultTool;
                return;
            }

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
            if (IsIdle && PreviewMouseDown?.Invoke(_host.ScreenToWorld(screen), button, clickCount) == true)
                return;

            Dispatch(() => _current.OnMouseDown(screen, button, clickCount));
        }

        /// <summary>Runs one tool event, handles its result, then starts any tool the event asked for.</summary>
        AnnotationToolResult Dispatch(Func<AnnotationToolResult> toolEvent)
        {
            AnnotationToolResult result;
            _dispatchDepth++;
            try
            {
                result = toolEvent();
            }
            finally
            {
                _dispatchDepth--;
            }

            HandleResult(result);
            if (_dispatchDepth == 0 && _pendingStart != null)
            {
                IAnnotationTool next = _pendingStart;
                _pendingStart = null;
                StartTool(next);
            }

            return result;
        }

        public void OnMouseMove(Vector2 screen)
        {
            _scene.ToolPreviewSection = _host.SectionNumber;
            _context.PointerWorld = _host.ScreenToWorld(screen);
            PointerMoved?.Invoke(this, _context.PointerWorld);
            _current.OnMouseMove(screen);
        }

        public void OnMouseUp()
        {
            _current.OnMouseUp();
            if (!ReferenceEquals(_current, _defaultTool) && _current is TranslateLocationTool or ResizeCircleTool or AdjustVertexTool or LinkAnnotationsTool or PenStrokeTool)
                FinishCurrent(AnnotationToolResult.Done);
        }

        public bool OnKeyDown(Key key)
        {
            if (key == Key.Escape && IsIdle && _context.ArmedStructure != null)
            {
                _context.ArmedStructure = null;
                StatusChanged?.Invoke(this, null);
                return true;
            }

            return Dispatch(() => _current.OnKeyDown(key)) != AnnotationToolResult.Continue;
        }

        public void CancelCurrent()
        {
            _context.ArmedStructure = null;
            _current?.Cancel();
            _queue.Clear();
            RestoreDefault();
        }

        public void CommitCurrent()
        {
            Dispatch(() => _current.OnKeyDown(Key.Enter));
        }

        public void ContinueLast(Vector2 world) => AnnotationToolActions.TryContinueLastTrace(_context, world);

        /// <summary>The volume's UserSettings shortcuts, or null when none were loaded.</summary>
        public UserSettingsHotkeys Hotkeys { get; set; }

        /// <summary>The user name the client signed in with; Viking's <c>@username</c> tag value expands to it.</summary>
        public string UserName { get; set; }

        /// <summary>
        /// Runs the UserSettings shortcut bound to this chord, as Viking's overlay does: structure shortcuts arm a placement
        /// for the next click; tag and terminal shortcuts act on the location under the pointer (else the selected one).
        /// Only while no tool is active. False when nothing is bound.
        /// </summary>
        public bool TryRunHotkey(Key key, bool shift, bool ctrl, bool alt)
        {
            if (Hotkeys == null || !IsIdle)
                return false;

            HotkeyCommand command = Hotkeys.Find(key, shift, ctrl, alt);
            if (command == null)
                return false;

            if (command.Kind == HotkeyCommandKind.CreateStructure)
            {
                _context.ArmedStructure = command;
                StatusChanged?.Invoke(this, $"{command.Name}: click to place, Esc to cancel");
                return true;
            }

            LocationObj target = _context.HoveredLocation ?? SelectedLocation;
            if (target == null)
            {
                StatusChanged?.Invoke(this, $"{command.Name}: point at an annotation first");
                return true;
            }

            switch (command.Kind)
            {
                case HotkeyCommandKind.ToggleStructureTag:
                    _ = AnnotationToolActions.ToggleStructureTagAsync(_context, target, command.Tag, ExpandTagValue(command.Value));
                    break;
                case HotkeyCommandKind.ToggleLocationTag:
                    _ = AnnotationToolActions.ToggleLocationTagAsync(_context, target, command.Tag, ExpandTagValue(command.Value));
                    break;
                case HotkeyCommandKind.ToggleLocationTerminal:
                    _ = AnnotationToolActions.ToggleTerminalAsync(_context, target);
                    break;
                case HotkeyCommandKind.ChangeLocationType:
                    ChangeLocationType(target, command.LocationType);
                    break;
                default:
                    StatusChanged?.Invoke(this, $"{command.Name} is not supported here yet");
                    break;
            }

            return true;
        }

        /// <summary>
        /// Viking's change-type hotkeys re-place the location in the new shape: a circle becomes one at the location's
        /// center and radius; curves and polygons are clicked out from the pointer, then replace the location's shape.
        /// Only locations on this section can change.
        /// </summary>
        void ChangeLocationType(LocationObj target, LocationType type)
        {
            if ((int)Math.Round(target.Z) != _host.SectionNumber)
            {
                StatusChanged?.Invoke(this, "Only annotations on this section can change type");
                return;
            }

            if (type == LocationType.CIRCLE)
            {
                _ = ShapeActions.ConvertToCircleAsync(_context, target);
                return;
            }

            if (type is LocationType.OPENCURVE or LocationType.CLOSEDCURVE or LocationType.CURVEPOLYGON or LocationType.POLYGON or LocationType.POLYLINE)
            {
                StartTool(new PlacePolyTool(_context, type, _context.PointerWorld) { Retarget = target });
                return;
            }

            StatusChanged?.Invoke(this, $"Changing an annotation to {type} is not supported");
        }

        string ExpandTagValue(string value) =>
            string.Equals(value, "@username", StringComparison.OrdinalIgnoreCase) ? UserName : value;

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

        /// <summary>The location drawn at <paramref name="screen"/> on the current section, or null.</summary>
        public LocationObj LocationAt(Vector2 screen)
        {
            try
            {
                object hit = _scene.HitTest(_host.SectionNumber, _host.ScreenToWorld(screen), out _);
                return hit is View.LocationCanvasView view && Store.Locations.TryGetObjectByID(view.ID, out LocationObj loc) ? loc : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Annotation hit test failed: {ex.Message}");
                return null;
            }
        }

        public void ToggleTerminal(LocationObj location) => _ = AnnotationToolActions.ToggleTerminalAsync(_context, location);

        public void ToggleOffEdge(LocationObj location) => _ = AnnotationToolActions.ToggleOffEdgeAsync(_context, location);

        /// <summary>
        /// Viking's Tab: interactive segmentation of a new structure of the type chosen in the type list, else type 1
        /// (cell) as in Viking. Only while no tool is active.
        /// </summary>
        public void StartSegmentation(UI.Commands.Segmentation.ISegmentationViewport viewport, Viking.Services.Grpc.IGrpcChannelManager channels)
        {
            if (!IsIdle)
                return;

            long? typeId = SelectedStructureTypeId;
            if (!typeId.HasValue && Store.IsInitialized && Store.StructureTypes.TryGetObjectByID(1, out StructureTypeObj cell) && cell != null)
                typeId = 1;
            if (!typeId.HasValue)
            {
                StatusChanged?.Invoke(this, "Choose a structure type to segment");
                return;
            }

            StartTool(new SegmentTool(_context, viewport, channels, typeId.Value));
        }

        /// <summary>
        /// Viking's "Segment to Polygon" on a circle (its prompt) or "Resegment" on a polygon (its triangle centroids):
        /// segments and replaces the location's shape on accept.
        /// </summary>
        public void StartSegmentationOf(LocationObj circle, UI.Commands.Segmentation.ISegmentationViewport viewport,
            Viking.Services.Grpc.IGrpcChannelManager channels)
        {
            if (!IsIdle || circle == null)
                return;

            if (circle.TypeCode is LocationType.POLYGON or LocationType.CURVEPOLYGON)
            {
                IReadOnlyList<Vector2> clicks = SegmentTool.PolygonPrompt(circle, _context.Mapper);
                if (clicks.Count == 0)
                {
                    StatusChanged?.Invoke(this, "This polygon could not be mapped onto the volume");
                    return;
                }

                StartTool(new SegmentTool(_context, viewport, channels, circle, clicks));
                return;
            }

            if (!SegmentTool.TryCreateCirclePrompt(circle, _context.Mapper, out var prompt))
            {
                StatusChanged?.Invoke(this, "This circle could not be mapped onto the volume");
                return;
            }

            StartTool(new SegmentTool(_context, viewport, channels, circle, prompt.Points, new[] { prompt.Seed }));
        }

        public void ConvertToCircle(LocationObj location) => _ = ShapeActions.ConvertToCircleAsync(_context, location);

        public void ConvertCircleToPolygon(LocationObj circle) => _ = ShapeActions.ConvertCircleToPolygonAsync(_context, circle);

        public void SimplifyPolygon(LocationObj polygon) => _ = ShapeActions.SimplifyPolygonAsync(_context, polygon);

        /// <summary>
        /// Viking's "Random Color" for a polygon: a new display color for this session (not saved), keeping its alpha.
        /// </summary>
        public void RandomizeColor(LocationObj polygon)
        {
            ViewModel.SectionAnnotationsView view = _scene.GetOrCreate((int)Math.Round(polygon.Z));
            if (view == null || !view.TryGetLocation(polygon.ID, out View.LocationCanvasView locationView)
                || locationView is not View.LocationPolygonView polygonView)
                return;

            float alpha = VikingXNAGraphics.ColorExtensions.GetAlpha(polygonView.Color);
            polygonView.Color = VikingXNAGraphics.ColorExtensions.SetAlpha(VikingXNAGraphics.ColorExtensions.Random(Microsoft.Xna.Framework.Color.Black), alpha);
            _host.Invalidate();
        }

        /// <summary>Viking's Ctrl+Z: reverts the newest recorded edit and saves it. Only while no tool is active.</summary>
        public bool Undo()
        {
            if (!IsIdle)
                return false;

            string description = UndoManager.NextUndoDescription;
            if (!UndoManager.UndoLast())
            {
                StatusChanged?.Invoke(this, "Nothing to undo");
                return false;
            }

            StatusChanged?.Invoke(this, $"Undid {description}");
            _host.Invalidate();
            return true;
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
