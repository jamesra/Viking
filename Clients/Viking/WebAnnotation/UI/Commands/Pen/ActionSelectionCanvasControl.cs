using Geometry;
using Rectangle = Geometry.Rectangle;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Viking.UI;
using Viking.UI.Controls;
using VikingXNA;
using VikingXNAGraphics;
using VikingXNAGraphics.Controls;
using WebAnnotation.UI.Actions;
using WebAnnotation.UI.ActionViews;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.UI.Commands
{

    /// <summary>
    /// Presents a set of overlays on a canvas that allow the user to select an action
    /// </summary>
    internal class ActionSelectionCanvasControl : Viking.UI.Commands.Command
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
        private CircularButton CancelButton;
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



        public delegate void OnCommandSuccess();

        private readonly OnCommandSuccess? SuccessCallback = null;

        private Rectangle BoundingBox;

        /// <summary>
        /// If the mouse or pen hover over a button we only display the active animation for the button if it exists
        /// </summary>
        private IAction? active_action = null;

        /// <summary>
        /// True if the input device is over the cancel button
        /// </summary>
        private bool CancelHover = false;

        /// <summary>
        /// Fraction of the total shape area a button should occupy by default
        /// </summary>
        private readonly double CircleAreaScalar = 10;

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

        private double GetButtonRadius(IShape2D shape, double CircleAreaFraction)
        {
            CircleAreaFraction = CircleAreaFraction > Buttons.Length ? CircleAreaFraction : Buttons.Length + 1;
            return Math.Sqrt((shape.BoundingBox.Area / CircleAreaFraction) / Math.PI);
        }


        /// <summary>
        /// Create a button for every action that requires it
        /// </summary>
        private void GenerateActionButtons(Dictionary<IAction, IIconTexture> actionIcons)
        {
            BoundingBox = CalculateBoundingBox(ActionInteractables);

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

        }

        private Rectangle CalculateBoundingBox(Dictionary<IAction, List<IHitTesting>> ActionInteractables)
        {
            Rectangle output = new();

            bool First = true;
            foreach (List<IHitTesting> controls in ActionInteractables.Values)
            {
                foreach (IHitTesting control in controls)
                {
                    output = First ? control.BoundingBox : Rectangle.Union(output, control.BoundingBox);
                }
            }

            //Check that the bounding box is not too large
            Rectangle renderTargetBounds = Parent.RenderTargetBounds();
            if (output.Width > renderTargetBounds.Width)
            {
                output = new Rectangle(renderTargetBounds.Left, renderTargetBounds.Right, output.Bottom,
                    output.Top);
            }

            if (output.Height > renderTargetBounds.Height)
            {
                output = new Rectangle(output.Left, output.Right, renderTargetBounds.Bottom,
                    renderTargetBounds.Top);
            }

            //Check that the bounding box is not too small
            if (output.Width < renderTargetBounds.Width / 5)
            {
                output = new Rectangle(output.Left, output.Left + renderTargetBounds.Width / 5, output.Bottom,
                    output.Top);
            }

            if (output.Height < renderTargetBounds.Height / 5)
            {
                output = new Rectangle(output.Left, output.Right, output.Bottom,
                    output.Bottom + renderTargetBounds.Height / 5);
            }

            return output;
        }

        /// <summary>
        /// Starting at the top left we layout everything but the cancel button
        /// </summary>
        private void LayoutButtons()
        {
            Rectangle bbox = BoundingBox;
            //TODO: Ensure buttons are visible on the screen

            double Radius = GetButtonRadius(BoundingBox, CircleAreaScalar);

            Geometry.Vector2 Origin = bbox.UpperLeft;
            Origin = bbox.UpperLeft - new Geometry.Vector2(Radius, Radius);

            Rectangle visible_world = Parent.Scene.VisibleWorldBounds;

            if (visible_world.Left > Origin.X)
            {
                Origin = new Geometry.Vector2(visible_world.Left, Origin.Y);
            }

            if (visible_world.Bottom > Origin.Y)
            {
                Origin = new Geometry.Vector2(Origin.X, visible_world.Bottom);
            }

            //Origin = Origin - new Geometry.Vector2(Radius, 0);

            Geometry.Vector2 NextPosition = Origin;
            double HorizontalSpacing = Radius * 3;
            double VerticalSpacing = Radius * 3;
            //Place everything but the cancel button, which is the last button in the list.  The cancel button
            //is positioned at creation time
            int iRow = 0;
            int iCol = 0;
            int nCols = (int)(bbox.Width / HorizontalSpacing);

            for (int i = 0; i < Buttons.Length - 1; i++)
            {
                NextPosition = Origin + new Geometry.Vector2((iCol) * HorizontalSpacing, 0 - (VerticalSpacing * iRow));
                Buttons[i].Circle = new Circle(NextPosition, Radius);
                iCol++;

                if (iCol > nCols)
                {
                    iRow -= 1;
                    iCol = 0;
                    //    NextPosition = new Geometry.Vector2(Origin.X - Radius, NextPosition.Y);
                }
                Trace.WriteLine(NextPosition);
            }

            //Place the cancel button one row up and one column right of the normal button positions
            NextPosition = Origin + new Geometry.Vector2((nCols + 1) * HorizontalSpacing, 0 - (VerticalSpacing * -1));
            Buttons[Buttons.Length - 1].Circle = new Circle(NextPosition, Radius);
        }

        /// <summary>
        /// Create the cancel button
        /// </summary>
        private void AppendCancelButton()
        {
            Geometry.Vector2 ButtonCenter = BoundingBox.UpperRight;
            double CancelCircleRadius = GetButtonRadius(BoundingBox, CircleAreaScalar);
            ButtonCenter = ButtonCenter + new Geometry.Vector2(CancelCircleRadius, CancelCircleRadius);
            Circle ButtonCircle = new(ButtonCenter, CancelCircleRadius);

            //CancelView = new CircularButton(ButtonCircle, Color.Magenta);
            TextureCircleView cancelBtnView = new(BuiltinTexture.X.GetTexture(), ButtonCircle, Color.Magenta);
            CancelButton = CircularButton.CreateSimple(cancelBtnView, () => { return; });

            _Buttons = Buttons.Add(CancelButton);
        }

        public override void OnActivate() => base.OnActivate();

        public override void OnDraw(GraphicsDevice graphicsDevice, Scene scene, BasicEffect basicEffect)
        {
            CircleView.Draw(graphicsDevice, scene, OverlayStyle.Alpha, [.. Buttons.Select(b => b.circleView)]);

            if (CancelHover)
            {
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
                else
                {
                    view_list = [];
                }
            }

            //Show the passive views for all buttons if there is no active view

            /*
            if (active_action_view is null)
            {
                foreach (IActionView action in this.action_views.Where(av => av.Passive != null))
                {
                    action.Passive.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
                }
            }
            else
            {
                active_action_view.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            }
            */


            base.OnDraw(graphicsDevice, scene, basicEffect);
        }

        private static void DrawView(GraphicsDevice graphicsDevice, Scene scene, object action, bool UseActive)
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


        protected override void OnCameraChanged(object sender, PropertyChangedEventArgs e) => base.OnCameraChanged(sender, e);

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
            if (CancelButton.Contains(WorldPosition) && CancelButton.OnClick(CancelButton, WorldPosition, InputDevice.Mouse, e.Button.ToVikingButton()))
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

            if (CancelButton.Contains(WorldPosition) && CancelButton.OnClick(CancelButton, WorldPosition, InputDevice.Pen, e))
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
            if (CancelButton.Contains(WorldPosition))
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
