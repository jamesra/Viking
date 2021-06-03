using Microsoft.Xaml.Behaviors;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using VikingXNAGraphics;

namespace MonogameWPFLibrary.Views
{
    public class Camera3DBehavior : Behavior<MeshView>
    {
        private bool mouseDown;
        Point LastMousePosition;
        private MonogameCamera3D camera;

        public Geometry.GridBox BoundingBox
        {
            get { return (Geometry.GridBox)GetValue(BoundingBoxProperty); }
            set { SetValue(BoundingBoxProperty, value); }
        }

        // Using a DependencyProperty as the backing store for View.  This enables animation, styling, binding, etc...
        protected static readonly DependencyProperty BoundingBoxProperty = DependencyProperty.Register("BoundingBox", typeof(Geometry.GridBox), typeof(Camera3DBehavior), new PropertyMetadata());
          
        protected override void OnAttached()
        {
            camera = AssociatedObject.Camera as MonogameCamera3D;

            System.Windows.Data.Binding binding = new Binding("BoundingBox");
            binding.Source = AssociatedObject;

            BindingOperations.SetBinding(this, Camera3DBehavior.BoundingBoxProperty, binding);
            
            if(camera == null)
            {
                throw new ArgumentException("Camera3DBehavior requires being attached to a MonogameCamera3D object");
            }

            AssociatedObject.MouseLeftButtonDown += (s, e) =>
            {
                mouseDown = true;
                LastMousePosition = e.GetPosition(AssociatedObject.Parent as System.Windows.IInputElement);
            };

            AssociatedObject.MouseMove += (s, e) =>
            {
                Point UpdatedPosition = e.GetPosition(AssociatedObject.Parent as System.Windows.IInputElement);
                Vector Delta = UpdatedPosition - LastMousePosition;
                LastMousePosition = UpdatedPosition;

                if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                {
                    Microsoft.Xna.Framework.Vector3 translation = this.camera.View.TranslateRelativeToViewMatrix((-Delta.X / AssociatedObject.ActualWidth) * BoundingBox.dimensions.Max(), (Delta.Y / AssociatedObject.ActualHeight) * BoundingBox.dimensions.Max(), 0.0);

                    camera.Position += translation;
                }

                if( e.RightButton == System.Windows.Input.MouseButtonState.Pressed)
                {
                    Delta.X = (Delta.X / AssociatedObject.ActualWidth) * Math.PI * 2;
                    Delta.Y = (Delta.Y / AssociatedObject.ActualHeight) * Math.PI * 2;
                    camera.Rotation += Delta.ToXNAVector3(0f);
                }
            };

            AssociatedObject.MouseWheel += (s, e) =>
            {
                double LinesToScrollEntireBoundingBox = 100;
                double lineDelta = (double)-e.Delta / (double)System.Windows.Input.Mouse.MouseWheelDeltaForOneLine;
                /*lineDelta *= lineDelta;
                if (e.Delta > 0)
                    lineDelta = -lineDelta;
                    */
                double percentOfVolume = lineDelta / LinesToScrollEntireBoundingBox;
                double totalDelta = percentOfVolume * BoundingBox.dimensions.Max();

                Microsoft.Xna.Framework.Vector3 translation = this.camera.View.TranslateRelativeToViewMatrix(0, 0, totalDelta);
                camera.Position += translation;
            };

            AssociatedObject.MouseLeftButtonUp += (s, e) =>
            {
                mouseDown = false;
            };
        }
    }
}
