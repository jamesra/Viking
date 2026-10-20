using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using VikingXNAGraphics;
using WebAnnotation.UI;
using WebAnnotation.UI.Actions;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// One choice a finished pen stroke offers, as a menu item: what it does, doing it, and a drawing of its result to
    /// show while the item is pointed at (null when the choice has none).
    /// </summary>
    public sealed record AnnotationChoice(string Label, Action Execute, IAnnotationToolPreview Preview = null);

    /// <summary>Draws a pen action's view (Viking's choice-button preview): its Active drawing, else its Passive one.</summary>
    sealed class ActionViewPreview(IActionView view) : IAnnotationToolPreview
    {
        public void DrawPreview(GraphicsDevice device, VikingXNA.Scene scene, RoundLineCode.RoundLineManager lineManager, OverlayShaderEffect overlayEffect) =>
            (view.Active ?? view.Passive)?.Draw(device, scene, OverlayStyle.Alpha);
    }

    /// <summary>
    /// Viking's Pen Mode stroke: the path follows the pointer while the button is held, logging the annotations it
    /// enters and leaves as Viking's free-draw command does. On release the touched annotations propose their actions
    /// (retrace a boundary, extend a curve, link, ...), a closed loop adds convert-circle and cut-hole choices, and new
    /// structures of the chosen type and the favorite types are offered; the host shows them for the user to pick.
    /// </summary>
    public sealed class PenStrokeTool : IAnnotationTool, IAnnotationToolPreview
    {
        readonly AnnotationToolContext _context;
        readonly Path _path = new();
        readonly PathInteractionLogger _logger;
        readonly int _sectionNumber;
        HashSet<long> _hidden;

        readonly Action<Vector2> _onClick;

        /// <param name="onClick">What a press that drew no stroke does instead (the default tool's click).</param>
        public PenStrokeTool(AnnotationToolContext context, Vector2 origin, Action<Vector2> onClick = null)
        {
            _context = context;
            _onClick = onClick;
            _sectionNumber = context.SectionNumber;
            ICanvasViewHitTesting hitTesting = context.Scene.GetOrCreate(_sectionNumber);
            if (hitTesting != null)
            {
                // As in Viking, once the stroke enters a cell its children are hidden: not drawn, not logged, not targets.
                _logger = new PathInteractionLogger(_path, new HiddenStructureHitTestFilter(hitTesting, () => _hidden));
                _logger.Log.OnLogChanged += OnLogChanged;
            }
            _path.Push(origin);
            _context.Host.CapturePointer();
        }

        public string StatusText => "Pen: draw, then release to choose what the stroke does (Esc cancels)";

        void OnLogChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (_hidden != null || e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Add || e.NewItems == null)
                return;

            _hidden = HiddenStructures.ForStrokeEntries(e.NewItems.OfType<InteractionLogEvent>());
            if (_hidden != null)
            {
                _context.Scene.HiddenStructureIds = _hidden;
                _context.Invalidate();
            }
        }

        /// <summary>Draws the hidden children again once the stroke is over.</summary>
        void Unhide()
        {
            if (_hidden == null)
                return;

            _hidden = null;
            _context.Scene.HiddenStructureIds = null;
            _context.Invalidate();
        }

        public AnnotationToolResult OnMouseDown(Vector2 screen, MouseButton button, int clickCount) => AnnotationToolResult.Continue;

        public void OnMouseMove(Vector2 screen)
        {
            Vector2 world = _context.ScreenToWorld(screen);
            // Viking's pen input samples about every two screen pixels.
            if (Vector2.Distance(world, _path.Peek()) < 2 * _context.Host.Downsample)
                return;

            try
            {
                _path.Push(world);
            }
            catch (ArgumentException ex)
            {
                Trace.WriteLine($"Pen point skipped: {ex.Message}");
            }

            _context.Invalidate();
        }

        public void OnMouseUp()
        {
            _context.Host.ReleasePointer();
            _context.SetStatus(null);
            Unhide();
            if (_path.Points.Count < 2)
            {
                _onClick?.Invoke(_path.Points[0]);
                return;
            }

            List<IAction> actions;
            try
            {
                actions = _logger == null ? [] : PenStrokeActions.FromTouchedAnnotations(_path, _logger.Log.Entries, _sectionNumber);
                PenStrokeActions.AddLoopActions(_path, _sectionNumber, actions);
                PenStrokeActions.AddCreateStructureActions(_path, _sectionNumber, CreatableTypes(), actions, _context.Mapper);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Pen stroke actions failed: {ex}");
                _context.SetStatus($"Pen stroke failed: {ex.Message}");
                return;
            }

            List<AnnotationChoice> choices = actions.Distinct().Select(a => new AnnotationChoice(Describe(a), () => Run(a), PreviewFor(a))).ToList();
            if (choices.Count == 0)
                _context.SetStatus("The stroke offers nothing to do here");
            else
                _context.ProposeChoices?.Invoke(choices);
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
            Unhide();
            _context.Host.ReleasePointer();
            _context.SetStatus(null);
            _context.Invalidate();
        }

        /// <summary>The type chosen in the type list first, then Viking's favorite structure types.</summary>
        IEnumerable<long> CreatableTypes()
        {
            if (_context.SelectedStructureTypeId() is long selected)
                yield return selected;
            foreach (ulong favorite in Global.UserFavoriteStructureTypes)
                yield return (long)favorite;
        }

        static IAnnotationToolPreview PreviewFor(IAction action)
        {
            try
            {
                IActionView view = UI.Commands.ActionSelectionCanvasControl.ViewFor(action);
                return view == null ? null : new ActionViewPreview(view);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"No preview for {action.Type}: {ex.Message}");
                return null;
            }
        }

        void Run(IAction action)
        {
            try
            {
                action.Execute();
                _context.Invalidate();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Pen action failed: {ex}");
                _context.SetStatus($"Could not {Describe(action).ToLowerInvariant()}: {ex.Message}");
            }
        }

        /// <summary>A menu label for a pen action, naming what it changes.</summary>
        internal static string Describe(IAction action) => action switch
        {
            Create2DStructureAction create => $"Create {TypeName(create.TypeID)} (closed shape)",
            Create1DStructureAction create => $"Create {TypeName(create.TypeID)} (curve)",
            ChangeToPolygonAction change => $"Convert location {change.Location.ID} to a polygon",
            ChangeToPolylineAction change => $"Convert location {change.Location.ID} to a curve",
            CutHoleAction cut => $"Cut a hole in location {cut.Location.ID}",
            Change2DContourAction contour => $"Retrace the boundary of location {contour.Location.ID}",
            Change1DContourAction contour => $"Redraw curve location {contour.Location.ID}",
            LinkLocationAction link => $"Link location {link.A.ID} to location {link.B.ID}",
            LinkStructureAction link => $"Link structure {link.Source.ParentID} to structure {link.Target.ParentID}",
            CreateNewLinkedLocationAction linked => $"Continue location {linked.ExistingLocID} on this section",
            _ => action.Type.ToString(),
        };

        static string TypeName(long typeId) =>
            Store.IsInitialized && Store.StructureTypes.TryGetObjectByID(typeId, out StructureTypeObj type) && type != null
                ? type.Name
                : $"type {typeId}";

        public void DrawPreview(GraphicsDevice device, VikingXNA.Scene scene, RoundLineCode.RoundLineManager lineManager, OverlayShaderEffect overlayEffect)
        {
            List<LineView> lines = new();
            ToolPreviewDrawing.AddPath(lines, _path.Points, closed: false, 2 * _context.Host.Downsample, ToolPreviewDrawing.ActionColor);
            ToolPreviewDrawing.Draw(device, scene, lineManager, lines);
        }
    }
}
