using Geometry;
using Rectangle = Geometry.Rectangle;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Viking.UI;
using Viking.UI.Controls;
using VikingXNA;
using VikingXNAGraphics;
using VikingXNAGraphics.Controls;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// Presents the drawn shape and a cancel button.  User either accepts or rejects the shape.
    /// </summary>
    public class ActionConfirmationCommand : Viking.UI.Commands.Command
    {
        //private IShape2D VolumeShape = null;

        //Shapes the user can click to confirm
        private readonly PositionColorMeshModel? DrawnShape2D = null;  //Shape to draw for filled 2D shapes, Polygons, circles, etc...
        private readonly PolyLineView? DrawnShape1D = null;            //Shape to draw for 1D shapes or open 2D shapes, lines, closed curves, etc...
        private readonly CircleView? circleView = null;
        private readonly double Width;  //Line width for line types

        /// <summary>
        /// Button user can click to cancel
        /// </summary>
        private CircularButton? CancelButton = null;
        private Color ShapeColor = Color.Green;
        private readonly IAction[]? AvailableActions = null;
        private List<CircularButton> Buttons = [];

        public delegate void OnCommandSuccess();

        private readonly OnCommandSuccess? SuccessCallback = null;

        private readonly Rectangle BoundingBox;

        private readonly IActionView[]? action_views = null;

        /// <summary>
        /// If the mouse or pen hover over a button we only display the active animation for the button if it exists
        /// </summary>
        private IRenderable? active_action_view = null;

        /// <summary>
        /// Fraction of the total shape area a button should occupy by default
        /// </summary>
        private readonly double CircleAreaScalar = 10;

        public ActionConfirmationCommand(SectionViewerControl parent, IAction[] actions, Rectangle bounding_box, OnCommandSuccess? success_callback = null) : base(parent)
        {
            BoundingBox = bounding_box;
            AvailableActions = actions;
            SuccessCallback = success_callback;

            action_views = [.. actions.Select(a => a as IActionView).Where(a => a != null)];

            //UpdateViews();
            CreateButtonsForActions();
            AppendCancelButton();
            LayoutButtons();
        }

        private double GetButtonRadius(IShape2D shape, double CircleAreaFraction)
        {
            CircleAreaFraction = CircleAreaFraction > Buttons.Count ? CircleAreaFraction : Buttons.Count + 1;
            return Math.Sqrt((shape.BoundingBox.Area / CircleAreaFraction) / Math.PI);
        }

        /// <summary>
        /// Create a button for every action that requires it
        /// </summary>
        private void CreateButtonsForActions()
        {
            Buttons = new List<CircularButton>(AvailableActions.Length);

            foreach (IAction action in AvailableActions)
            {
                CircleView btnView = null;

                Color color = action is not IColorView colorView ? Color.Green : colorView.Color;

                Circle circle = new(Geometry.Vector2.Zero, 1); //Button is positioned later.  This is just to call constructor.
                btnView = action is IIconTexture view && view.Icon != BuiltinTexture.None
                    ? new TextureCircleView(view.Icon.GetTexture(), circle, color)
                    : new CircleView(circle, color);

                //TODO: Sort and Map visuals on the circlular buttons according to action types
                CircularButton circularButton = CircularButton.CreateSimple(btnView, action.Execute);
                Buttons.Add(circularButton);
            }
        }

        /// <summary>
        /// Starting at the top left we layout everything but the cancel button
        /// </summary>
        private void LayoutButtons()
        {
            Rectangle bbox = BoundingBox;
            Geometry.Vector2 Origin = bbox.UpperLeft;

            //TODO: Ensure buttons are visible on the screen

            double Radius = GetButtonRadius(BoundingBox, CircleAreaScalar);
            //Origin = Origin - new Geometry.Vector2(Radius, 0);

            Geometry.Vector2 NextPosition = Origin;
            double HorizontalSpacing = Radius * 3;
            double VerticalSpacing = Radius * 3;
            //Place everything but the cancel button, which is the last button in the list.  The cancel button
            //is positioned at creation time
            int iRow = 0;
            int iCol = 0;
            int nCols = (int)(bbox.Width / HorizontalSpacing);
            for (int i = 0; i < Buttons.Count - 1; i++)
            {
                NextPosition = Origin + new Geometry.Vector2((iCol) * HorizontalSpacing, 0 - (VerticalSpacing * iRow));
                Buttons[i].Circle = new Circle(NextPosition, Radius);
                iCol++;

                if (iCol > nCols)
                {
                    iRow += 1;
                    iCol = 0;
                    //    NextPosition = new Geometry.Vector2(Origin.X - Radius, NextPosition.Y);
                }
                //                Trace.WriteLine(NextPosition);
            }

            //Place the cancel button one row up and one column right of the normal button positions
            NextPosition = Origin + new Geometry.Vector2((nCols + 1) * HorizontalSpacing, 0 - (VerticalSpacing * -1));
            Buttons[Buttons.Count - 1].Circle = new Circle(NextPosition, Radius);
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
            CancelButton = new CircularButton(cancelBtnView);

            Buttons.Add(CancelButton);
        }
        /*
        /// <summary>
        /// Create the visuals for the shapes
        /// </summary>
        private void UpdateViews()
        {
            switch (VolumeShape.ShapeType)
            {
                case ShapeType2D.Circle:
                    circleView = new CircleView((Circle)VolumeShape, ShapeColor);
                    break;

                case ShapeType2D.ClosedCurve:
                    DrawnShape1D = new PolyLineView(ShapeColor, null, Width, LineStyle.Standard);
                    break;

                case ShapeType2D.CurvePolygon:
                case ShapeType2D.Polygon:
                    DrawnShape2D = TriangleNetExtensions.CreateMeshForPolygon2D((Polygon)VolumeShape, ShapeColor.ConvertToHCL());
                    break;

                case ShapeType2D.Point:
                case ShapeType2D.Ellipse:
                    throw new NotImplementedException();

                case ShapeType2D.Polyline:
                case ShapeType2D.OpenCurve:
                    DrawnShape1D = new PolyLineView(ShapeColor, null, Width, LineStyle.Standard);
                    break;
            }
        }*/

        public override void OnActivate() => base.OnActivate();

        public override void OnDraw(GraphicsDevice graphicsDevice, Scene scene, BasicEffect basicEffect)
        {
            /*if (CancelView != null)
            {
                CircleView.Draw(graphicsDevice, scene, Parent.basicEffect, Parent.AnnotationOverlayEffect, new CircleView[] { CancelView });
            }

            if (circleView != null)
            {
                CircleView.Draw(graphicsDevice, scene, Parent.basicEffect, Parent.AnnotationOverlayEffect, new CircleView[] { circleView });
            }
            */

            CircleView.Draw(graphicsDevice, scene, OverlayStyle.Alpha, [.. Buttons.Select(b => b.circleView)]);

            if (DrawnShape2D != null)
            {
                float originalInputLumaAlpha = Parent.PolygonOverlayEffect.InputLumaAlphaValue;
                Parent.PolygonOverlayEffect.InputLumaAlphaValue = 0.5f;

                MeshView<Microsoft.Xna.Framework.Graphics.VertexPositionColor>.Draw(graphicsDevice, scene, Parent.PolygonOverlayEffect, cullmode: CullMode.None, meshmodels: new PositionColorMeshModel[] { DrawnShape2D });
                Parent.PolygonOverlayEffect.InputLumaAlphaValue = originalInputLumaAlpha;
            }

            //Show the passive views for all buttons if there is no active view
            if (active_action_view is null)
            {
                foreach (IActionView action in action_views.Where(av => av.Passive != null))
                {
                    action.Passive.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
                }
            }
            else
            {
                active_action_view.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            }

            if (DrawnShape1D != null)
            {
                PolyLineView.Draw(graphicsDevice, scene, OverlayStyle.Luma, [DrawnShape1D]);
            }

            base.OnDraw(graphicsDevice, scene, basicEffect);
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

            foreach (CircularButton button in Buttons)
            {
                if (button.Contains(WorldPosition) && button.OnClick(button, WorldPosition, InputDevice.Mouse, e.Button.ToVikingButton()))
                {
                    Deactivated = true;
                    return;
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

            foreach (CircularButton button in Buttons)
            {
                if (button.Contains(WorldPosition) && button.OnClick(button, WorldPosition, InputDevice.Pen, e))
                {
                    Deactivated = true;
                    return;
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
            for (int iButton = 0; iButton < Buttons.Count; iButton++)
            {
                CircularButton button = Buttons[iButton];
                if (button.Contains(WorldPosition))
                {
                    if (iButton < action_views.Length)
                    {
                        active_action_view = action_views[iButton].Active;
                        return;
                    }
                    //Todo: Hide all of the passive views if we are over the cancel button?
                }
            }

            //Reset the view to null so that passive views are shown if we are not over a button
            active_action_view = null;
            return;
        }
    }
}
