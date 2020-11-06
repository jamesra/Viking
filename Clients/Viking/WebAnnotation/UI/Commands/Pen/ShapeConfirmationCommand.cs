using Geometry;
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

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// Presents the drawn shape and a cancel button.  User either accepts or rejects the shape.
    /// </summary> bb bb                                                                                                                                                                                                                                           
    public class ShapeConfirmationCommand : Viking.UI.Commands.Command
    {
        private IShape2D VolumeShape = null;

        //Shapes the user can click to confirm
        PositionColorMeshModel DrawnShape2D = null;  //Shape to draw for filled 2D shapes, Polygons, circles, etc...
        PolyLineView DrawnShape1D = null;            //Shape to draw for 1D shapes or open 2D shapes, lines, closed curves, etc...
        CircleView circleView = null;

        double Width;  //Line width for line types

        /// <summary>
        /// Button user can click to cancel
        /// </summary>
        CircularButton CancelButton = null;
        Color ShapeColor = Color.Green;

        List<CircularButton> Buttons = new List<CircularButton>();

        public delegate void OnCommandSuccess();
        OnCommandSuccess SuccessCallback = null;

        /// <summary>
        /// Fraction of the total shape area a button should occupy by default
        /// </summary>
        double CircleAreaScalar = 10;

        public ShapeConfirmationCommand(SectionViewerControl parent, IShape2D volume_shape, double width = 16.0, OnCommandSuccess success_callback = null) : base(parent)
        {
            VolumeShape = volume_shape;
            Width = width;
            SuccessCallback = success_callback;

            UpdateViews();
            AppendCancelButton();
            LayoutButtons();
        }

        private double GetButtonRadius(IShape2D shape, double CircleAreaFraction)
        {
            CircleAreaFraction = CircleAreaFraction > Buttons.Count ? CircleAreaFraction : Buttons.Count + 1;
            return Math.Sqrt((shape.BoundingBox.Area / CircleAreaFraction) / Math.PI);
        }

        /// <summary>
        /// Starting at the top left we layout everything but the cancel button
        /// </summary>
        private void LayoutButtons()
        {
            GridVector2 Origin = VolumeShape.BoundingBox.UpperLeft;

            double Radius = GetButtonRadius(VolumeShape, CircleAreaScalar);
            Origin = Origin - new GridVector2(Radius, Radius);

            //Place everything but the cancel button, which is the last button in the list.  The cancel button
            //is positioned at creation time
            for (int i = 0; i < Buttons.Count - 1; i++)
            {
                GridVector2 position = Origin + new GridVector2(i * (Radius * 2), 0);
                Buttons[i].Circle = new GridCircle(position, Radius);
            }
        }

        /// <summary>
        /// Create the cancel button
        /// </summary>
        private void AppendCancelButton()
        {
            GridVector2 ButtonCenter = VolumeShape.BoundingBox.UpperRight;
            double CancelCircleRadius = GetButtonRadius(VolumeShape, CircleAreaScalar);
            ButtonCenter = ButtonCenter - new GridVector2(CancelCircleRadius, CancelCircleRadius);
            GridCircle ButtonCircle = new GridCircle(ButtonCenter, CancelCircleRadius);

            //CancelView = new CircularButton(ButtonCircle, Color.Magenta);
            CancelButton = new CircularButton(ButtonCircle, Color.Magenta);

            Buttons.Add(CancelButton);
        }

        /// <summary>
        /// Create the visuals for the shapes
        /// </summary>
        private void UpdateViews()
        {
            switch (VolumeShape.ShapeType)
            {
                case ShapeType2D.CIRCLE:
                    circleView = new CircleView((GridCircle)VolumeShape, ShapeColor);
                    break;

                case ShapeType2D.CLOSEDCURVE:
                    DrawnShape1D = new PolyLineView(ShapeColor, null, Width, LineStyle.Standard);
                    break;

                case ShapeType2D.CURVEPOLYGON:
                case ShapeType2D.POLYGON:
                    DrawnShape2D = TriangleNetExtensions.CreateMeshForPolygon2D((GridPolygon)VolumeShape, ShapeColor.ConvertToHSL());
                    break;

                case ShapeType2D.POINT:
                case ShapeType2D.ELLIPSE:
                    throw new NotImplementedException();

                case ShapeType2D.POLYLINE:
                case ShapeType2D.OPENCURVE:
                    DrawnShape1D = new PolyLineView(ShapeColor, null, Width, LineStyle.Standard);
                    break;
            }
        }

        public override void OnActivate()
        {
            base.OnActivate();
        }

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

            CircleView.Draw(graphicsDevice, scene, OverlayStyle.Alpha, Buttons.Select(b => b.circleView).ToArray());

            if (DrawnShape2D != null)
            {
                float originalInputLumaAlpha = Parent.PolygonOverlayEffect.InputLumaAlphaValue;
                Parent.PolygonOverlayEffect.InputLumaAlphaValue = 0.5f;

                MeshView<Microsoft.Xna.Framework.Graphics.VertexPositionColor>.Draw(graphicsDevice, scene, Parent.PolygonOverlayEffect, cullmode: CullMode.None, meshmodels: new PositionColorMeshModel[] { DrawnShape2D });
                Parent.PolygonOverlayEffect.InputLumaAlphaValue = originalInputLumaAlpha;
            }

            if (DrawnShape1D != null)
            {
                PolyLineView.Draw(graphicsDevice, scene, OverlayStyle.Alpha, new PolyLineView[] { this.DrawnShape1D });
            }

            base.OnDraw(graphicsDevice, scene, basicEffect);
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

            foreach (var button in Buttons)
            {
                if (button.Contains(WorldPosition) && button.OnClick(button, WorldPosition, InputDevice.Mouse, e.Button.ToVikingButton()))
                {
                    this.Deactivated = true;
                    return;
                }
            }
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
            base.OnMouseMove(sender, e);
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

            foreach (var button in Buttons)
            {
                if (button.Contains(WorldPosition) && button.OnClick(button, WorldPosition, InputDevice.Pen, e))
                {
                    this.Deactivated = true;
                    return;
                }
            }

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
    }
}
