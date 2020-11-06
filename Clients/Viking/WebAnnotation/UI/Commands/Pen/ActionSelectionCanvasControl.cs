using Geometry;
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

namespace WebAnnotation.UI.Commands
{

    /// <summary>
    /// Presents a set of overlays on a canvas that allow the user to select an action
    /// </summary>
    class ActionSelectionCanvasControl : Viking.UI.Commands.Command
    {
        /// <summary>
        /// Maintains the set of interactable elements associated with each action. 
        /// This is used when we transition from active/passive view states for actions
        /// </summary>
        Dictionary<IAction, List<IHitTesting>> ActionInteractables = new Dictionary<IAction, List<IHitTesting>>();

        /// <summary>
        /// A per-action set of objects that either support IRenderable or IActionView
        /// </summary>
        Dictionary<IAction, List<object>> ActionViews = new Dictionary<IAction, List<object>>();

        CircularButton CancelButton;

        Dictionary<IAction, IIconTexture> _ActionIcona = new Dictionary<IAction, IIconTexture>();

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

        CircularButton[] _Buttons = new CircularButton[0];

        CircularButton[] Buttons { get => _Buttons; }



        public delegate void OnCommandSuccess();
        OnCommandSuccess SuccessCallback = null;

        private GridRectangle BoundingBox;

        /// <summary>
        /// If the mouse or pen hover over a button we only display the active animation for the button if it exists
        /// </summary>
        private IAction active_action = null;

        /// <summary>
        /// True if the input device is over the cancel button
        /// </summary>
        private bool CancelHover = false;

        /// <summary>
        /// Fraction of the total shape area a button should occupy by default
        /// </summary>
        double CircleAreaScalar = 10;

        private ActionSelectionCanvasControl(SectionViewerControl parent, OnCommandSuccess success_callback = null) : base(parent)
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
            this.BoundingBox = CalculateBoundingBox(ActionInteractables);

            var buttons = new List<CircularButton>(actionIcons.Count);

            foreach (var item in actionIcons)
            {
                var action = item.Key;
                var value = item.Value;

                CircleView btnView = null;

                IColorView colorView = value as IColorView;
                Color color = colorView == null ? Color.Green : colorView.Color;

                var circle = new GridCircle(GridVector2.Zero, 1); //Button is positioned later.  This is just to call constructor. 
                if (value.Icon != BuiltinTexture.None)
                {
                    btnView = new TextureCircleView(value.Icon.GetTexture(), circle, color);
                }
                else
                {
                    btnView = new CircleView(circle, color);
                }

                //TODO: Sort and Map visuals on the circlular buttons according to action types
                CircularButton circularButton = CircularButton.CreateSimple(btnView, action.Execute);
                buttons.Add(circularButton);

                if (ActionInteractables.ContainsKey(action))
                {
                    ActionInteractables[action].Insert(0, circularButton);
                }
                else
                {
                    ActionInteractables.Add(action, new List<IHitTesting>(new CircularButton[] { circularButton }));
                }
            }

            this._Buttons = buttons.ToArray();

            AppendCancelButton();

            LayoutButtons();

        }

        private static GridRectangle CalculateBoundingBox(Dictionary<IAction, List<IHitTesting>> ActionInteractables)
        {
            GridRectangle output = new GridRectangle();

            bool First = true;
            foreach (var controls in ActionInteractables.Values)
            {
                foreach (var control in controls)
                {
                    if (First)
                    {
                        output = control.BoundingBox;
                    }
                    else
                    {
                        output.Union(control.BoundingBox);
                    }
                }
            }

            return output;
        }

        /// <summary>
        /// Starting at the top left we layout everything but the cancel button
        /// </summary>
        private void LayoutButtons()
        {
            GridRectangle bbox = BoundingBox;
            //TODO: Ensure buttons are visible on the screen

            double Radius = GetButtonRadius(BoundingBox, CircleAreaScalar);


            GridVector2 Origin = bbox.UpperLeft;
            Origin = bbox.UpperLeft - new GridVector2(Radius, Radius);

            GridRectangle visible_world = this.Parent.Scene.VisibleWorldBounds;

            if (visible_world.Left > Origin.X)
                Origin.X = visible_world.Left;

            if (visible_world.Bottom > Origin.Y)
                Origin.Y = visible_world.Bottom;

            //Origin = Origin - new GridVector2(Radius, 0);

            GridVector2 NextPosition = Origin;
            double HorizontalSpacing = Radius * 3;
            double VerticalSpacing = Radius * 3;
            //Place everything but the cancel button, which is the last button in the list.  The cancel button
            //is positioned at creation time
            int iRow = 0;
            int iCol = 0;
            int nCols = (int)(bbox.Width / HorizontalSpacing);

            for (int i = 0; i < Buttons.Length - 1; i++)
            {
                NextPosition = Origin + new GridVector2((iCol) * HorizontalSpacing, 0 - (VerticalSpacing * iRow));
                Buttons[i].Circle = new GridCircle(NextPosition, Radius);
                iCol++;

                if (iCol > nCols)
                {
                    iRow -= 1;
                    iCol = 0;
                    //    NextPosition = new GridVector2(Origin.X - Radius, NextPosition.Y);
                }
                Trace.WriteLine(NextPosition);
            }

            //Place the cancel button one row up and one column right of the normal button positions
            NextPosition = Origin + new GridVector2((nCols + 1) * HorizontalSpacing, 0 - (VerticalSpacing * -1));
            Buttons[Buttons.Length - 1].Circle = new GridCircle(NextPosition, Radius);
        }

        /// <summary>
        /// Create the cancel button
        /// </summary>
        private void AppendCancelButton()
        {
            GridVector2 ButtonCenter = BoundingBox.UpperRight;
            double CancelCircleRadius = GetButtonRadius(BoundingBox, CircleAreaScalar);
            ButtonCenter = ButtonCenter + new GridVector2(CancelCircleRadius, CancelCircleRadius);
            GridCircle ButtonCircle = new GridCircle(ButtonCenter, CancelCircleRadius);

            //CancelView = new CircularButton(ButtonCircle, Color.Magenta);
            var cancelBtnView = new TextureCircleView(BuiltinTexture.X.GetTexture(), ButtonCircle, Color.Magenta);
            CancelButton = CircularButton.CreateSimple(cancelBtnView, () => { return; });

            this._Buttons = Buttons.Add(CancelButton);
        }

        public override void OnActivate()
        {
            base.OnActivate();
        }

        public override void OnDraw(GraphicsDevice graphicsDevice, Scene scene, BasicEffect basicEffect)
        {
            CircleView.Draw(graphicsDevice, scene, OverlayStyle.Alpha, Buttons.Select(b => b.circleView).ToArray());

            if (CancelHover)
                return;

            List<object> view_list;
            if (active_action == null)
            {
                view_list = ActionViews.Values.SelectMany(v => v).ToList();
                foreach (var view in view_list)
                    DrawView(graphicsDevice, scene, view, false);
            }
            else
            {
                if (ActionViews.ContainsKey(active_action))
                {
                    view_list = ActionViews[active_action];
                    foreach (var view in view_list)
                        DrawView(graphicsDevice, scene, view, true);
                }
                else
                {
                    view_list = new List<object>();
                }
            }

            //Show the passive views for all buttons if there is no active view

            /*
            if (active_action_view == null)
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
                var view = (IActionView)action;
                if (UseActive == false || view.Active == null)
                {
                    if (view.Passive != null)
                        view.Passive.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
                }
                else if (view.Active != null && UseActive)
                    view.Active.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            }
            else if (action is IRenderable)
            {
                var view = (IRenderable)action;
                view.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            }
        }

        public override void Redo()
        {
            base.Redo();
        }

        public override string ToString()
        {
            return base.ToString();
        }

        public override void Undo()
        {
            base.Undo();
        }

        protected override void Execute()
        {
            if (SuccessCallback != null)
                SuccessCallback();

            base.Execute();
        }


        protected override void OnCameraChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnCameraChanged(sender, e);
        }

        protected override void OnDeactivate()
        {
            base.OnDeactivate();
        }

        protected override void OnKeyDown(object sender, KeyEventArgs e)
        {
            base.OnKeyDown(sender, e);
        }

        protected override void OnKeyPress(object sender, KeyPressEventArgs e)
        {
            base.OnKeyPress(sender, e);
        }

        protected override void OnKeyUp(object sender, KeyEventArgs e)
        {
            base.OnKeyUp(sender, e);
        }

        protected override void OnMouseClick(object sender, MouseEventArgs e)
        {
            base.OnMouseClick(sender, e);
        }

        protected override void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            base.OnMouseDoubleClick(sender, e);
        }

        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            base.OnMouseDown(sender, e);

            GridVector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);
            if (CancelButton.Contains(WorldPosition) && CancelButton.OnClick(CancelButton, WorldPosition, InputDevice.Mouse, e.Button.ToVikingButton()))
            {
                this.Deactivated = true;
                return;
            }

            foreach (var actionInteractables in ActionInteractables.Values)
            {
                foreach (var interactable in actionInteractables)
                {
                    IClickable clickable = interactable as IClickable;
                    if (clickable != null && clickable.Contains(WorldPosition) && clickable.OnClick(clickable, WorldPosition, InputDevice.Mouse, e.Button.ToVikingButton()))
                    {
                        this.Deactivated = true;
                        return;
                    }
                }
            }



            /*
            if (VolumeShape.Contains(WorldPosition))
            {
                this.Execute();
            }
            */
        }

        protected override void OnMouseEnter(object sender, EventArgs e)
        {
            base.OnMouseEnter(sender, e);
        }

        protected override void OnMouseHover(object sender, EventArgs e)
        {
            base.OnMouseHover(sender, e);
        }

        protected override void OnMouseLeave(object sender, EventArgs e)
        {
            base.OnMouseLeave(sender, e);
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);
            UpdateActiveView(WorldPosition);
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            base.OnMouseUp(sender, e);
        }

        protected override void OnMouseWheel(object sender, MouseEventArgs e)
        {
            base.OnMouseWheel(sender, e);
        }

        protected override void OnPenContact(object sender, PenEventArgs e)
        {
            GridVector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);

            if (CancelButton.Contains(WorldPosition) && CancelButton.OnClick(CancelButton, WorldPosition, InputDevice.Pen, e))
            {
                this.Deactivated = true;
                return;
            }

            foreach (var actionInteractables in ActionInteractables.Values)
            {
                foreach (var interactable in actionInteractables)
                {
                    IClickable clickable = interactable as IClickable;
                    if (clickable != null && clickable.Contains(WorldPosition) && clickable.OnClick(clickable, WorldPosition, InputDevice.Pen, e))
                    {
                        this.Deactivated = true;
                        return;
                    }
                }
            }



            /*
            if (BoundingBox.Contains(WorldPosition))
            {
                this.Execute();
            }
            */

            base.OnPenContact(sender, e);
        }

        protected override void OnPenEnterRange(object sender, PenEventArgs e)
        {
            base.OnPenEnterRange(sender, e);
        }

        protected override void OnPenLeaveContact(object sender, PenEventArgs e)
        {
            base.OnPenLeaveContact(sender, e);
        }

        protected override void OnPenLeaveRange(object sender, PenEventArgs e)
        {
            base.OnPenLeaveRange(sender, e);
        }

        protected override void OnPenMove(object sender, PenEventArgs e)
        {
            if (e.InContact == false)
            {
                GridVector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);
                UpdateActiveView(WorldPosition);
            }

            base.OnPenMove(sender, e);
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
        }

        protected override bool ShouldSerializeProperty(DependencyProperty dp)
        {
            return base.ShouldSerializeProperty(dp);
        }

        protected void UpdateActiveView(GridVector2 WorldPosition)
        {
            if (CancelButton.Contains(WorldPosition))
            {
                active_action = null;
                CancelHover = true;
                //Trace.WriteLine("Hover Cancel");
                return;
            }

            CancelHover = false;

            foreach (var action in ActionInteractables.Keys)
            {
                var interactables = ActionInteractables[action].Where(i => i is CircularButton);

                foreach (var interactable in interactables)
                {
                    if (interactable.Contains(WorldPosition))
                    {
                        if (active_action != action)
                            Trace.WriteLine(string.Format("Hover Action: {0}", action));
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

        public static ActionSelectionCanvasControl CreateViews(SectionViewerControl parent, IAction[] actions, OnCommandSuccess success_callback = null)
        {
            List<IClickable> clickables = new List<IClickable>();
            List<IActionView> views = new List<IActionView>();

            ActionSelectionCanvasControl control = new ActionSelectionCanvasControl(parent, success_callback);

            Dictionary<IAction, IIconTexture> actionButtons = new Dictionary<IAction, IIconTexture>();

            foreach (IAction a in actions)
            {
                var actionSelectors = new List<IHitTesting>();
                control.ActionInteractables.Add(a, actionSelectors);

                var actionViews = new List<object>();
                control.ActionViews.Add(a, actionViews);

                if (a is Change2DContourAction)
                {
                    var action = a as Change2DContourAction;

                    var view = new Change2DContourActionView(action);
                    ClickableGeometryWrapper clickable = ClickableGeometryWrapper.CreateSimple(action.NewSmoothedVolumePolygon, a.Execute);

                    actionSelectors.Add(clickable);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is Change1DContourAction)
                {
                    var action = a as Change1DContourAction;

                    var view = new Change1DContourActionView(action);
                    ClickableGeometryWrapper clickable = ClickableGeometryWrapper.CreateSimple(action.NewSmoothVolumePolyline, a.Execute);

                    actionSelectors.Add(clickable);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is CutHoleAction)
                {
                    var action = a as CutHoleAction;

                    var view = new CutHoleActionView(action);
                    ClickableGeometryWrapper clickable = ClickableGeometryWrapper.CreateSimple(action.NewSmoothVolumeInteriorPolygon, a.Execute);

                    actionSelectors.Add(clickable);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is LinkLocationAction)
                {
                    LinkLocationAction action = a as LinkLocationAction;

                    var view = new LinkLocationActionView(action);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is LinkStructureAction)
                {
                    var action = a as LinkStructureAction;

                    var view = new LinkStructureActionView(action);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is RemoveHoleAction)
                {
                    var action = a as RemoveHoleAction;

                    var view = new RemoveHoleActionView(action);

                    ClickableGeometryWrapper clickable = ClickableGeometryWrapper.CreateSimple(action.VolumePolygonToRemove, a.Execute);

                    actionSelectors.Add(clickable);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is ChangeToPolygonAction)
                {
                    var action = a as ChangeToPolygonAction;

                    var view = new ChangeToPolygonActionView(action);

                    ClickableGeometryWrapper clickable = ClickableGeometryWrapper.CreateSimple(action.NewSmoothVolumePolygon, a.Execute);

                    actionSelectors.Add(clickable);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is ChangeToPolylineAction)
                {
                    var action = a as ChangeToPolylineAction;

                    var view = new ChangeToPolylineActionView(action);

                    ClickableGeometryWrapper clickable = ClickableGeometryWrapper.CreateSimple(action.NewSmoothVolumePolyline, a.Execute);

                    actionSelectors.Add(clickable);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is CreateStructureActionBase)
                {
                    var action = a as CreateStructureActionBase;
                    var view = new CreateStructureActionView(action);

                    ClickableGeometryWrapper clickable = ClickableGeometryWrapper.CreateSimple(view.Shape, a.Execute);

                    actionSelectors.Add(clickable);
                    actionViews.Add(view);
                    actionButtons[a] = view;
                }
                else if (a is CreateNewLinkedLocationAction)
                {
                    var action = a as CreateNewLinkedLocationAction;
                    var view = new CreateNewLinkedLocationActionView(action);

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
