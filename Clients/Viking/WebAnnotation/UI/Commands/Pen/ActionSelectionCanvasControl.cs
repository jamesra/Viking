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
        private CircularButton? CancelButton;
        private readonly List<IAction> _labeledActions = [];
        private LabelView[] _buttonLabels = [];
        private readonly Dictionary<IAction, IIconTexture> _ActionIcona = [];

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

        /// <summary>Target button diameter in screen pixels for the right-side chrome column.</summary>
        private const double ButtonScreenPixels = 56;

        /// <summary>Gap between the bottom of the circle and the top of its caption, in radii. World Y decreases down the screen.</summary>
        private const double LabelGapRadii = 0.28;

        /// <summary>Caption wrap width in button radii so names stay under the circle.</summary>
        private const double LabelWidthRadii = 2.6;

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
        /// Create a button for every action that requires it
        /// </summary>
        private void GenerateActionButtons(Dictionary<IAction, IIconTexture> actionIcons)
        {
            BoundingBox = CalculateBoundingBox(ActionInteractables);

            if (!Global.ShowPenActionButtons)
            {
                _Buttons = [];
                _buttonLabels = [];
                _labeledActions.Clear();
                PublishChoiceHelp(actionIcons.Keys);
                return;
            }

            List<CircularButton> buttons = new(actionIcons.Count);

            foreach (KeyValuePair<IAction, IIconTexture> item in actionIcons)
            {
                IAction action = item.Key;
                IIconTexture value = item.Value;

                CircleView btnView = null;

                Color color = value is not IColorView colorView ? Color.Green : colorView.Color;

                Circle circle = new(Geometry.Vector2.Zero, 1); //Button is positioned later.  This is just to call constructor. 
                btnView = value.Icon != BuiltinTexture.None
                    ? new TextureCircleView(value.Icon.GetTexture(), circle, color)
                    : new CircleView(circle, color);

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

            AppendCancelButton();

            LayoutButtons();
            CreateButtonLabels();
            PublishChoiceHelp(_labeledActions);
        }

        /// <summary>
        /// Fills the F1 bar from the choice captions. Newlines in a button label become spaces so the combo stays one line per choice.
        /// </summary>
        private void PublishChoiceHelp(IEnumerable<IAction> actions)
        {
            _choiceHelp.Clear();
            _choiceHelp.Add("Click a button to choose");
            foreach (IAction action in actions)
            {
                string line = LabelFor(action).Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
                _choiceHelp.Add(line);
            }

            _choiceHelp.Add("Cancel");
        }

        /// <summary>
        /// Text drawn under each choice button. Called after <see cref="LayoutButtons"/>.
        /// Long names wrap via <see cref="LabelView.MaxLineWidth"/>. Font size tracks the
        /// screen-fixed button radius so captions stay readable under the chrome.
        /// Cancel is the last button and is not in <see cref="_labeledActions"/>.
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
                string text = i < _labeledActions.Count ? LabelFor(_labeledActions[i]) : "Cancel";
                labels.Add(CreateLabelForButton(Buttons[i], text, maxLineWidth));
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
            double fontSize = ScreenFixedLabelFontSize();
            for (int i = 0; i < Buttons.Length; i++)
            {
                Circle circle = Buttons[i].Circle;
                _buttonLabels[i].Position = LabelPositionUnderCircle(circle);
                _buttonLabels[i].MaxLineWidth = maxLineWidth;
                _buttonLabels[i].FontSize = fontSize;
            }
        }

        private LabelView CreateLabelForButton(CircularButton button, string text, double maxLineWidth)
        {
            Circle circle = button.Circle;
            Geometry.Vector2 position = LabelPositionUnderCircle(circle);
            LabelView label = new(
                text,
                position,
                Color.White,
                Alignment.CenterCenter,
                Anchor.TopCenter,
                scaleFontWithScene: true,
                fontSize: ScreenFixedLabelFontSize())
            {
                MaxLineWidth = maxLineWidth
            };
            return label;
        }

        /// <summary>
        /// World position of the caption's top edge, centered under the circle.
        /// </summary>
        private static Geometry.Vector2 LabelPositionUnderCircle(Circle circle) =>
            new(circle.Center.X, circle.Center.Y - circle.Radius * (1 + LabelGapRadii));

        /// <summary>
        /// World-space wrap width so captions stay under the circle and wrap onto extra lines.
        /// </summary>
        private double LabelMaxLineWidthWorld() => ScreenFixedButtonRadius() * LabelWidthRadii;

        /// <summary>
        /// Font size in world units so captions stay proportional to the screen-fixed button radius.
        /// </summary>
        private double ScreenFixedLabelFontSize() => ScreenFixedButtonRadius() * 0.55;

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
                case RemoveHoleAction:
                    return "Remove hole";
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
        /// Places action buttons in vertical columns along the right edge of the visible view.
        /// Fills top-to-bottom, then adds columns to the left. Cancel sits under the last
        /// action in the rightmost column when there is room, otherwise at the bottom margin.
        /// </summary>
        private void LayoutButtons()
        {
            if (Buttons.Length == 0)
                return;

            Geometry.Rectangle visible = Parent.Scene.VisibleWorldBounds;
            double radius = ScreenFixedButtonRadius();
            double margin = radius * 1.5;
            double horizontalSpacing = radius * 3.4;
            // Room under each circle for a wrapped caption before the next button.
            double verticalSpacing = radius * 4.6;

            int maxRows = Math.Max(1, (int)Math.Floor((visible.Height - 2 * margin) / verticalSpacing));
            int actionCount = Math.Max(0, Buttons.Length - 1);

            double rightColX = visible.Right - margin;
            double topY = visible.Top - margin;

            for (int i = 0; i < actionCount; i++)
            {
                int col = i / maxRows;
                int row = i % maxRows;
                double x = rightColX - col * horizontalSpacing;
                double y = topY - row * verticalSpacing;
                Buttons[i].Circle = new Circle(new Geometry.Vector2(x, y), radius);
            }

            int actionsInRightCol = Math.Min(actionCount, maxRows);
            double cancelX = rightColX;
            double cancelY = topY - actionsInRightCol * verticalSpacing;
            if (cancelY - radius < visible.Bottom + margin * 0.25)
            {
                // Right column is full; place cancel one column left at the bottom.
                cancelX = rightColX - horizontalSpacing;
                cancelY = visible.Bottom + margin;
            }

            Buttons[Buttons.Length - 1].Circle = new Circle(new Geometry.Vector2(cancelX, cancelY), radius);
        }

        /// <summary>
        /// Create the cancel button
        /// </summary>
        private void AppendCancelButton()
        {
            double cancelRadius = ScreenFixedButtonRadius();
            Circle buttonCircle = new(Geometry.Vector2.Zero, cancelRadius);
            TextureCircleView cancelBtnView = new(BuiltinTexture.X.GetTexture(), buttonCircle, Color.Magenta);
            CancelButton = CircularButton.CreateSimple(cancelBtnView, () => { return; });

            _Buttons = Buttons.Add(CancelButton);
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
                CircleView.Draw(graphicsDevice, scene, OverlayStyle.Alpha, [.. Buttons.Select(b => b.circleView)]);

            // LabelView.Draw owns SpriteBatch.Begin/End; nesting Begin here leaves the batch open if the inner Begin throws.
            if (_buttonLabels.Length > 0 && Parent.fontArial != null && Parent.spriteBatch != null)
                LabelView.Draw(Parent.spriteBatch, Parent.fontArial, scene, _buttonLabels);
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

            Geometry.Vector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);
            if (CancelButton is not null && CancelButton.Contains(WorldPosition) && CancelButton.OnClick(CancelButton, WorldPosition, InputDevice.Mouse, e.Button.ToVikingButton()))
            {
                Deactivated = true;
                return;
            }

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
            UpdateActiveView(WorldPosition);
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e) => base.OnMouseUp(sender, e);

        protected override void OnMouseWheel(object sender, MouseEventArgs e) => base.OnMouseWheel(sender, e);

        protected override void OnPenContact(object sender, PenEventArgs e)
        {
            Geometry.Vector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);

            if (CancelButton is not null && CancelButton.Contains(WorldPosition) && CancelButton.OnClick(CancelButton, WorldPosition, InputDevice.Pen, e))
            {
                Deactivated = true;
                return;
            }

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
                UpdateActiveView(WorldPosition);
            }

            base.OnPenMove(sender, e);
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) => base.OnPropertyChanged(e);

        protected override bool ShouldSerializeProperty(DependencyProperty dp) => base.ShouldSerializeProperty(dp);

        protected void UpdateActiveView(Geometry.Vector2 WorldPosition)
        {
            if (CancelButton is not null && CancelButton.Contains(WorldPosition))
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

                if (a is Change2DContourAction change2D)
                {
                    Change2DContourActionView view = new(change2D);
                    ClickableGeometryWrapper clickable = ClickableGeometryWrapper.CreateSimple(change2D.NewSmoothedVolumePolygon, a.Execute);

                    actionSelectors.Add(clickable);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is Change1DContourAction change1D)
                {
                    Change1DContourActionView view = new(change1D);
                    ClickableGeometryWrapper clickable = ClickableGeometryWrapper.CreateSimple(change1D.NewSmoothVolumePolyline, a.Execute);

                    actionSelectors.Add(clickable);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is CutHoleAction cutHole)
                {
                    CutHoleActionView view = new(cutHole);
                    ClickableGeometryWrapper clickable = ClickableGeometryWrapper.CreateSimple(cutHole.NewSmoothVolumeInteriorPolygon, a.Execute);

                    actionSelectors.Add(clickable);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is LinkLocationAction)
                {
                    LinkLocationAction action = a as LinkLocationAction;

                    LinkLocationActionView view = new(action);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is LinkStructureAction)
                {
                    LinkStructureAction action = a as LinkStructureAction;

                    LinkStructureActionView view = new(action);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is RemoveHoleAction)
                {
                    RemoveHoleAction action = a as RemoveHoleAction;

                    RemoveHoleActionView view = new(action);

                    ClickableGeometryWrapper clickable = ClickableGeometryWrapper.CreateSimple(action.VolumePolygonToRemove, a.Execute);

                    actionSelectors.Add(clickable);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is ChangeToPolygonAction changeToPolygon)
                {
                    ChangeToPolygonActionView view = new(changeToPolygon);

                    ClickableGeometryWrapper clickable = ClickableGeometryWrapper.CreateSimple(changeToPolygon.NewSmoothVolumePolygon, a.Execute);

                    actionSelectors.Add(clickable);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is ChangeToPolylineAction changeToPolyline)
                {
                    ChangeToPolylineActionView view = new(changeToPolyline);

                    ClickableGeometryWrapper clickable = ClickableGeometryWrapper.CreateSimple(changeToPolyline.NewSmoothVolumePolyline, a.Execute);

                    actionSelectors.Add(clickable);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is CreateStructureActionBase createStructure)
                {
                    CreateStructureActionView view = new(createStructure);

                    ClickableGeometryWrapper clickable = ClickableGeometryWrapper.CreateSimple(view.Shape, a.Execute);

                    actionSelectors.Add(clickable);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is CreateNewLinkedLocationAction createLinked)
                {
                    CreateNewLinkedLocationActionView view = new(createLinked);

                    ClickableGeometryWrapper clickable = ClickableGeometryWrapper.CreateSimple(view.Shape, a.Execute);

                    actionSelectors.Add(clickable);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
            }

            control.GenerateActionButtons(actionButtons);

            return control;
        }
    }
}
