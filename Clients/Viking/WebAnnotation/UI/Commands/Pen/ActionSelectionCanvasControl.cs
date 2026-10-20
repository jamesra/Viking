using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Viking.Common;
using Viking.UI;
using Viking.UI.Controls;
using VikingXNA;
using VikingXNAGraphics;
using VikingXNAGraphics.Controls;
using WebAnnotation.UI.Actions;
using WebAnnotation.UI.ActionViews;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.UI.Commands
{

    /// <summary>
    /// Presents a set of overlays on a canvas that allow the user to select an action
    /// </summary>
    internal class ActionSelectionCanvasControl : Viking.UI.Commands.Command, IHelpStrings, IObservableHelpStrings
    {
        /// <summary>
        /// Maintains the set of interactable elements associated with each action. 
        /// This is used when we transition from active/passive view states for actions
        /// </summary>
        private readonly Dictionary<IAction, List<IHitTesting>> ActionInteractables = [];

        /// <summary>
        /// A per-action set of objects that either support IRenderable or IActionView
        /// </summary>
        private readonly Dictionary<IAction, List<object>> ActionViews = [];
        private PenCancelButton? cancelButton;
        private readonly List<IAction> _labeledActions = [];
        private LabelView[] _buttonLabels = [];

        //        IReadOnlyDictionary<IAction, CircularButton> _actionButtons = new Dictionary<IAction, CircularButton>();

        /*
    /// <summary>
    /// Buttons for action views.  Not all action views have a button
    /// </summary>
    IReadOnlyDictionary<IAction, IIconTexture> ActionButtons
    {
        get { return _actionButtons; }
        set
        {
            _actionButtons = value;
            _Buttons = value.Values.ToArray();
            LayoutButtons();
        }
    }
    */

        private CircularButton[] _Buttons = [];

        private CircularButton[] Buttons => _Buttons;

        private readonly ObservableCollection<string> _choiceHelp = [];

        /// <summary>
        /// One F1 line per choice button, using the same captions as <see cref="LabelFor"/>, then Cancel.
        /// Bound while this command is current so the bar does not keep the freehand stroke's text.
        /// </summary>
        public ObservableCollection<string> ObservableHelpStrings => _choiceHelp;

        /// <inheritdoc cref="ObservableHelpStrings"/>
        public string[] HelpStrings => [.. _choiceHelp];



        public delegate void OnCommandSuccess();

        private readonly OnCommandSuccess? SuccessCallback = null;

        private Geometry.Rectangle BoundingBox;

        /// <summary>
        /// If the mouse or pen hover over a button we only display the active animation for the button if it exists
        /// </summary>
        private IAction? active_action = null;

        /// <summary>
        /// True if the input device is over the cancel button
        /// </summary>
        private bool CancelHover = false;

        /// <summary>Target button diameter in screen pixels for the right-side chrome column. Big enough for a caption inside.</summary>
        private const double ButtonScreenPixels = 84;

        /// <summary>Distance from the right and bottom edges of the viewer to the buttons, in screen pixels.</summary>
        private const double EdgeMarginPixels = 24;

        /// <summary>Gap between neighbouring buttons, in screen pixels.</summary>
        private const double ButtonGapPixels = 14;

        /// <summary>Caption wrap width in button radii so names stay inside the circle.</summary>
        private const double LabelWidthRadii = 1.6;

        /// <summary>Caption font size in button radii before <see cref="PenButtonStyle.LabelFontScale"/> shrinks long words.</summary>
        private const double LabelFontRadii = 0.36;

        private ActionSelectionCanvasControl(SectionViewerControl parent, OnCommandSuccess? success_callback = null) : base(parent)
        {
            //BoundingBox = bounding_box;
            //AvailableActions = actions;
            SuccessCallback = success_callback;

            //action_views = actions.Select(a => a as IActionView).Where(a => a != null).ToArray();

            //UpdateViews();
            //CreateButtonsForActionViews();
            //AppendCancelButton();
            //LayoutButtons();


        }

        /// <summary>
        /// Create a button for every action that requires it. When
        /// <see cref="Global.ShowPenActionButtons"/> is false, action circles are omitted. The red cancel X is a
        /// <see cref="PenCancelButton"/> and is always shown, so the user is never stranded without an on-canvas out
        /// (Escape also cancels).
        /// </summary>
        private void GenerateActionButtons(Dictionary<IAction, IIconTexture> actionIcons)
        {
            BoundingBox = CalculateBoundingBox(ActionInteractables);

            _Buttons = [];
            _buttonLabels = [];
            _labeledActions.Clear();

            if (Global.ShowPenActionButtons)
            {
                List<CircularButton> buttons = new(actionIcons.Count);

                foreach (KeyValuePair<IAction, IIconTexture> item in actionIcons)
                {
                    IAction action = item.Key;
                    IIconTexture value = item.Value;

                    Color color = value is not IColorView colorView ? Color.Green : colorView.Color;

                    // The caption is drawn inside the disc, so the disc is plain and opaque rather than an icon.
                    Circle circle = new(Geometry.Vector2.Zero, 1); //Button is positioned later.  This is just to call constructor.
                    CircleView btnView = new(circle, color) { Alpha = PenButtonStyle.DiscAlpha };

                    //TODO: Sort and Map visuals on the circlular buttons according to action types
                    CircularButton circularButton = CircularButton.CreateSimple(btnView, action.Execute);
                    buttons.Add(circularButton);
                    _labeledActions.Add(action);

                    if (ActionInteractables.ContainsKey(action))
                    {
                        ActionInteractables[action].Insert(0, circularButton);
                    }
                    else
                    {
                        ActionInteractables.Add(action, [circularButton]);
                    }
                }

                _Buttons = [.. buttons];
            }

            LayoutButtons();
            CreateButtonLabels();
            PublishChoiceHelp(Global.ShowPenActionButtons ? _labeledActions : actionIcons.Keys);
        }

        /// <summary>
        /// Fills the F1 bar from the choice captions. Newlines in a button label become spaces so the combo stays one line per choice.
        /// Always lists Cancel and Escape so hidden action buttons do not hide how to leave the command.
        /// </summary>
        private void PublishChoiceHelp(IEnumerable<IAction> actions)
        {
            _choiceHelp.Clear();
            _choiceHelp.Add(Global.ShowPenActionButtons
                ? "Click a button to choose"
                : "Click a shape preview to choose");
            foreach (IAction action in actions)
            {
                string line = LabelFor(action).Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
                _choiceHelp.Add(line);
            }

            _choiceHelp.Add("Cancel");
            _choiceHelp.Add("Escape: Cancel");
        }

        /// <summary>
        /// Text drawn inside each choice button. Called after <see cref="LayoutButtons"/>.
        /// Long names wrap via <see cref="LabelView.MaxLineWidth"/>. Font size tracks the
        /// screen-fixed button radius so captions stay readable on the chrome, and the colour is black or white
        /// depending on the disc (<see cref="PenButtonStyle.LabelColorFor"/>).
        /// </summary>
        private void CreateButtonLabels()
        {
            if (Buttons.Length == 0)
            {
                _buttonLabels = [];
                return;
            }

            double maxLineWidth = LabelMaxLineWidthWorld();
            List<LabelView> labels = new(Buttons.Length);
            for (int i = 0; i < Buttons.Length; i++)
            {
                labels.Add(CreateLabelForButton(Buttons[i], LabelFor(_labeledActions[i]), maxLineWidth));
            }

            _buttonLabels = [.. labels];
        }

        /// <summary>
        /// Moves existing labels to match the current button circles after a camera move.
        /// </summary>
        private void UpdateButtonLabelPositions()
        {
            if (_buttonLabels.Length != Buttons.Length)
            {
                CreateButtonLabels();
                return;
            }

            double maxLineWidth = LabelMaxLineWidthWorld();
            for (int i = 0; i < Buttons.Length; i++)
            {
                _buttonLabels[i].Position = Buttons[i].Circle.Center;
                _buttonLabels[i].MaxLineWidth = maxLineWidth;
                _buttonLabels[i].FontSize = ScreenFixedLabelFontSize(_buttonLabels[i].Text);
            }
        }

        private LabelView CreateLabelForButton(CircularButton button, string text, double maxLineWidth)
        {
            LabelView label = new(
                text,
                button.Circle.Center,
                PenButtonStyle.LabelColorFor(button.Color),
                Alignment.CenterCenter,
                Anchor.CenterCenter,
                scaleFontWithScene: true,
                fontSize: ScreenFixedLabelFontSize(text))
            {
                MaxLineWidth = maxLineWidth
            };
            return label;
        }

        /// <summary>
        /// World-space wrap width so captions stay inside the circle and wrap onto extra lines.
        /// </summary>
        private double LabelMaxLineWidthWorld() => ScreenFixedButtonRadius() * LabelWidthRadii;

        /// <summary>
        /// Font size in world units so captions stay proportional to the screen-fixed button radius, smaller for
        /// captions with a long word.
        /// </summary>
        private double ScreenFixedLabelFontSize(string text) =>
            ScreenFixedButtonRadius() * LabelFontRadii * PenButtonStyle.LabelFontScale(text);

        /// <summary>
        /// Button radius in world units so the chrome is roughly <see cref="ButtonScreenPixels"/> tall on screen.
        /// </summary>
        private double ScreenFixedButtonRadius()
        {
            double downsample = Parent.Camera?.Downsample ?? Parent.Downsample;
            if (downsample <= 0)
                downsample = 1;
            return (ButtonScreenPixels * 0.5) * downsample;
        }

        /// <summary>
        /// Short name for a pen-stroke choice. Structure creates use the structure type name so each favorite is distinct.
        /// </summary>
        private static string LabelFor(IAction action)
        {
            switch (action)
            {
                case CreateStructureActionBase create:
                    // Cache-only: favorite structure types are already loaded. Legacy's bool overload does not exist on this store.
                    Store.StructureTypes.TryGetObjectByID(create.TypeID, out StructureTypeObj type);
                    return type?.Name ?? "New structure";
                case CreateNewLinkedLocationAction:
                    return "Linked location";
                case CutHoleAction:
                    return "Cut hole";
                case Change2DContourAction:
                    return "Replace boundary";
                case Change1DContourAction:
                    return "Replace line";
                case ChangeToPolygonAction convert:
                    return $"Convert\n{convert.Location.ID}\nto polygon";
                case ChangeToPolylineAction:
                    return "To line";
                case LinkLocationAction:
                    return "Link locations";
                case LinkStructureAction:
                    return "Link structures";
                default:
                    return action.Type.ToString();
            }
        }

        private Geometry.Rectangle CalculateBoundingBox(Dictionary<IAction, List<IHitTesting>> ActionInteractables)
        {
            Geometry.Rectangle output = new();

            bool First = true;
            foreach (List<IHitTesting> controls in ActionInteractables.Values)
            {
                foreach (IHitTesting control in controls)
                {
                    output = First ? control.BoundingBox : Geometry.Rectangle.Union(output, control.BoundingBox);
                }
            }

            //Check that the bounding box is not too large
            Geometry.Rectangle renderTargetBounds = Parent.RenderTargetBounds();
            if (output.Width > renderTargetBounds.Width)
            {
                output = new Geometry.Rectangle(renderTargetBounds.Left, renderTargetBounds.Right, output.Bottom,
                    output.Top);
            }

            if (output.Height > renderTargetBounds.Height)
            {
                output = new Geometry.Rectangle(output.Left, output.Right, renderTargetBounds.Bottom,
                    renderTargetBounds.Top);
            }

            //Check that the bounding box is not too small
            if (output.Width < renderTargetBounds.Width / 5)
            {
                output = new Geometry.Rectangle(output.Left, output.Left + renderTargetBounds.Width / 5, output.Bottom,
                    output.Top);
            }

            if (output.Height < renderTargetBounds.Height / 5)
            {
                output = new Geometry.Rectangle(output.Left, output.Right, output.Bottom,
                    output.Bottom + renderTargetBounds.Height / 5);
            }

            return output;
        }

        /// <summary>
        /// Places action buttons in vertical columns along the right edge of the visible view, below the
        /// <see cref="PenCancelButton"/>. Fills top-to-bottom, then adds columns to the left.
        /// </summary>
        private void LayoutButtons()
        {
            if (Buttons.Length == 0)
                return;

            Geometry.Rectangle visible = Parent.Scene.VisibleWorldBounds;
            double downsample = Parent.Camera?.Downsample ?? Parent.Downsample;
            if (downsample <= 0)
                downsample = 1;

            // The toolstrip, status bar and F1 help bar are docked over the render surface; keep clear of them.
            System.Windows.Forms.Padding insets = Parent.DockedChromeInsets;

            double radius = ScreenFixedButtonRadius();
            double margin = (EdgeMarginPixels + insets.Right) * downsample;
            double step = 2 * radius + ButtonGapPixels * downsample;
            double topY = visible.Top - (insets.Top + PenCancelButton.ReservedTopPixels) * downsample - radius;
            double usableHeight = visible.Height
                - (insets.Top + PenCancelButton.ReservedTopPixels + EdgeMarginPixels + insets.Bottom) * downsample;
            int maxRows = Math.Max(1, (int)Math.Floor((usableHeight + ButtonGapPixels * downsample) / step));

            double rightColX = visible.Right - margin - radius;
            for (int i = 0; i < Buttons.Length; i++)
            {
                int col = i / maxRows;
                int row = i % maxRows;
                double x = rightColX - col * step;
                double y = topY - row * step;
                Buttons[i].Circle = new Circle(new Geometry.Vector2(x, y), radius);
            }
        }

        public override void OnActivate() => base.OnActivate();

        public override void OnDraw(GraphicsDevice graphicsDevice, Scene scene, BasicEffect basicEffect)
        {
            // Keep chrome pinned to the right edge while the camera moves.
            if (Buttons.Length > 0)
            {
                LayoutButtons();
                UpdateButtonLabelPositions();
            }

            if (CancelHover)
            {
                DrawButtonsAndLabels(graphicsDevice, scene);
                return;
            }

            List<object> view_list;
            if (active_action is null)
            {
                view_list = [.. ActionViews.Values.SelectMany(v => v)];
                foreach (object view in view_list)
                {
                    DrawView(graphicsDevice, scene, view, false);
                }
            }
            else
            {
                if (ActionViews.ContainsKey(active_action))
                {
                    view_list = ActionViews[active_action];
                    foreach (object view in view_list)
                    {
                        DrawView(graphicsDevice, scene, view, true);
                    }
                }
            }

            // Labels after stroke previews so caption text is not covered by action graphics.
            DrawButtonsAndLabels(graphicsDevice, scene);

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }

        /// <summary>
        /// Draws the circular choice buttons then their captions. Captions are last so they
        /// stay above stroke/action overlays.
        /// </summary>
        private void DrawButtonsAndLabels(GraphicsDevice graphicsDevice, Scene scene)
        {
            if (Buttons.Length > 0)
            {
                // Discs first, then the polygon-vertex rings framing them (CircleView.Draw draws rings last).
                IRenderable[] views =
                [
                    .. Buttons.Select(b => b.circleView),
                    .. Buttons.Select(b => PenButtonStyle.RingFor(b.Circle, b.Color)),
                ];
                CircleView.Draw(graphicsDevice, scene, OverlayStyle.Alpha, views);
            }

            // LabelView.Draw owns SpriteBatch.Begin/End; nesting Begin here leaves the batch open if the inner Begin throws.
            if (_buttonLabels.Length > 0 && Parent.fontArial != null && Parent.spriteBatch != null)
                LabelView.Draw(Parent.spriteBatch, Parent.fontArial, scene, _buttonLabels);

            (cancelButton ??= new PenCancelButton(Parent)).Draw(graphicsDevice, scene);
        }

        private static void DrawView(GraphicsDevice graphicsDevice, Scene scene, object action, bool UseActive)
        {
            try
            {
                if (action is IActionView)
                {
                    IActionView view = (IActionView)action;
                    if (UseActive == false || view.Active is null)
                    {
                        view.Passive?.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
                    }
                    else if (view.Active != null && UseActive)
                    {
                        view.Active.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
                    }
                }
                else if (action is IRenderable view)
                {
                    view.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
                }
            }
            catch (Exception ex)
            {
                // A bad preview must not escape paint. The chooser would stay the current command and block the next pen stroke.
                Trace.WriteLine($"Pen action preview could not be drawn: {ex.GetBaseException().Message}");
            }
        }

        public override void Redo() => base.Redo();

        public override string ToString() => base.ToString();

        public override void Undo() => base.Undo();

        protected override void Execute()
        {
            if (SuccessCallback != null)
            {
                SuccessCallback();
            }

            base.Execute();
        }


        protected override void OnCameraChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Buttons.Length > 0)
            {
                LayoutButtons();
                UpdateButtonLabelPositions();
            }

            base.OnCameraChanged(sender, e);
        }

        protected override void OnDeactivate() => base.OnDeactivate();

        protected override void OnKeyDown(object sender, KeyEventArgs e) => base.OnKeyDown(sender, e);

        protected override void OnKeyPress(object sender, KeyPressEventArgs e) => base.OnKeyPress(sender, e);

        protected override void OnKeyUp(object sender, KeyEventArgs e) => base.OnKeyUp(sender, e);

        protected override void OnMouseClick(object sender, MouseEventArgs e) => base.OnMouseClick(sender, e);

        protected override void OnMouseDoubleClick(object sender, MouseEventArgs e) => base.OnMouseDoubleClick(sender, e);

        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            base.OnMouseDown(sender, e);

            if (e.Button == MouseButtons.Left && (cancelButton ??= new PenCancelButton(Parent)).HitTest(e.X, e.Y))
            {
                CancelCommand();
                return;
            }

            Geometry.Vector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);

            foreach (List<IHitTesting> actionInteractables in ActionInteractables.Values)
            {
                foreach (IHitTesting interactable in actionInteractables.Where(ai => ai is IClickable).Where(ai => ai.Contains(WorldPosition)))
                {
                    IClickable clickable = interactable as IClickable;
                    if (clickable.OnClick(clickable, WorldPosition, InputDevice.Mouse, e.Button.ToVikingButton()))
                    {
                        Deactivated = true;
                        return;
                    }
                }
            }



            /*
            if (VolumeShape.Covers(WorldPosition))
            {
                this.Execute();
            }
            */
        }

        protected override void OnMouseEnter(object sender, EventArgs e) => base.OnMouseEnter(sender, e);

        protected override void OnMouseHover(object sender, EventArgs e) => base.OnMouseHover(sender, e);

        protected override void OnMouseLeave(object sender, EventArgs e) => base.OnMouseLeave(sender, e);

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            Geometry.Vector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);
            UpdateActiveView(WorldPosition, e.X, e.Y);
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e) => base.OnMouseUp(sender, e);

        protected override void OnMouseWheel(object sender, MouseEventArgs e) => base.OnMouseWheel(sender, e);

        protected override void OnPenContact(object sender, PenEventArgs e)
        {
            if ((cancelButton ??= new PenCancelButton(Parent)).HitTest(e.X, e.Y))
            {
                CancelCommand();
                return;
            }

            Geometry.Vector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);

            foreach (List<IHitTesting> actionInteractables in ActionInteractables.Values)
            {
                foreach (IHitTesting interactable in actionInteractables)
                {
                    if (interactable is IClickable clickable && clickable.Contains(WorldPosition) && clickable.OnClick(clickable, WorldPosition, InputDevice.Pen, e))
                    {
                        Deactivated = true;
                        return;
                    }
                }
            }



            /*
            if (BoundingBox.Covers(WorldPosition))
            {
                this.Execute();
            }
            */

            base.OnPenContact(sender, e);
        }

        protected override void OnPenEnterRange(object sender, PenEventArgs e) => base.OnPenEnterRange(sender, e);

        protected override void OnPenLeaveContact(object sender, PenEventArgs e) => base.OnPenLeaveContact(sender, e);

        protected override void OnPenLeaveRange(object sender, PenEventArgs e) => base.OnPenLeaveRange(sender, e);

        protected override void OnPenMove(object sender, PenEventArgs e)
        {
            if (e.InContact == false)
            {
                Geometry.Vector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);
                UpdateActiveView(WorldPosition, e.X, e.Y);
            }

            base.OnPenMove(sender, e);
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) => base.OnPropertyChanged(e);

        protected override bool ShouldSerializeProperty(DependencyProperty dp) => base.ShouldSerializeProperty(dp);

        protected void UpdateActiveView(Geometry.Vector2 WorldPosition, double screenX, double screenY)
        {
            if ((cancelButton ??= new PenCancelButton(Parent)).HitTest(screenX, screenY))
            {
                active_action = null;
                CancelHover = true;
                //Trace.WriteLine("Hover Cancel");
                return;
            }

            CancelHover = false;

            foreach (IAction action in ActionInteractables.Keys)
            {
                IEnumerable<IHitTesting> interactables = ActionInteractables[action].Where(i => i is CircularButton);

                foreach (IHitTesting interactable in interactables)
                {
                    if (interactable.Contains(WorldPosition))
                    {
                        if (active_action != action)
                        {
                            Trace.WriteLine($"Hover Action: {action}");
                        }

                        active_action = action;
                        return;
                    }
                }
            }

            //Reset the view to null so that passive views are shown if we are not over a button

            //if (active_action != null)
            //Trace.WriteLine(string.Format("No Hover Action"));

            active_action = null;
            return;
        }

        /// <summary>
        /// The view that previews <paramref name="a"/>: its button icon, and the shape it would make (Passive, or Active
        /// while pointed at). Null for an action without a view. Viking's choice buttons and Jotunn's choice menu use it.
        /// </summary>
        internal static IActionView? ViewFor(IAction a) => a switch
        {
            Change2DContourAction change2D => new Change2DContourActionView(change2D),
            Change1DContourAction change1D => new Change1DContourActionView(change1D),
            CutHoleAction cutHole => new CutHoleActionView(cutHole),
            LinkLocationAction link => new LinkLocationActionView(link),
            LinkStructureAction link => new LinkStructureActionView(link),
            ChangeToPolygonAction changeToPolygon => new ChangeToPolygonActionView(changeToPolygon),
            ChangeToPolylineAction changeToPolyline => new ChangeToPolylineActionView(changeToPolyline),
            CreateStructureActionBase createStructure => new CreateStructureActionView(createStructure),
            CreateNewLinkedLocationAction createLinked => new CreateNewLinkedLocationActionView(createLinked),
            _ => null,
        };

        public static ActionSelectionCanvasControl CreateViews(SectionViewerControl parent, IAction[] actions, OnCommandSuccess? success_callback = null)
        {
            List<IClickable> clickables = [];
            List<IActionView> views = [];

            ActionSelectionCanvasControl control = new(parent, success_callback);

            Dictionary<IAction, IIconTexture> actionButtons = [];

            foreach (IAction a in actions)
            {
                List<IHitTesting> actionSelectors = [];
                control.ActionInteractables.Add(a, actionSelectors);

                List<object> actionViews = [];
                control.ActionViews.Add(a, actionViews);

                IActionView? view = ViewFor(a);
                if (view is null)
                    continue;

                actionViews.Add(view);
                actionButtons[a] = (IIconTexture)view;

                // What a click on the drawing of the choice selects; link choices are chosen from their button only.
                if (a is Change2DContourAction change2D)
                    actionSelectors.Add(ClickableGeometryWrapper.CreateSimple(change2D.NewSmoothedVolumePolygon, a.Execute));
                else if (a is Change1DContourAction change1D)
                    actionSelectors.Add(ClickableGeometryWrapper.CreateSimple(change1D.NewSmoothVolumePolyline, a.Execute));
                else if (a is CutHoleAction cutHole)
                    actionSelectors.Add(ClickableGeometryWrapper.CreateSimple(cutHole.NewSmoothVolumeInteriorPolygon, a.Execute));
                else if (a is ChangeToPolygonAction changeToPolygon)
                    actionSelectors.Add(ClickableGeometryWrapper.CreateSimple(changeToPolygon.NewSmoothVolumePolygon, a.Execute));
                else if (a is ChangeToPolylineAction changeToPolyline)
                    actionSelectors.Add(ClickableGeometryWrapper.CreateSimple(changeToPolyline.NewSmoothVolumePolyline, a.Execute));
                else if (view is CreateStructureActionView createStructureView)
                    actionSelectors.Add(ClickableGeometryWrapper.CreateSimple(createStructureView.Shape, a.Execute));
                else if (view is CreateNewLinkedLocationActionView createLinkedView)
                    actionSelectors.Add(ClickableGeometryWrapper.CreateSimple(createLinkedView.Shape, a.Execute));
            }

            control.GenerateActionButtons(actionButtons);

            return control;
        }
    }
}
