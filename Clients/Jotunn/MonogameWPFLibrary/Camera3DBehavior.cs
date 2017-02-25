using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interactivity;
using MonogameWPFLibrary.Views;
using MathNet.Numerics.LinearAlgebra; 

namespace MonogameWPFLibrary.Views
{
    public class Camera3DBehavior : Behavior<MeshView>
    {
        private bool mouseDown;
        Point LastMousePosition;
        private MonogameCamera3D camera; 

        protected override void OnAttached()
        {
            camera = AssociatedObject.Camera as MonogameCamera3D;

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
                    Microsoft.Xna.Framework.Vector3 translation = TranslateRelativeToCameraView(-Delta.X / AssociatedObject.ActualWidth, Delta.Y / AssociatedObject.ActualHeight, 0.0);

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
                double totalDelta = (double)-e.Delta / (double)System.Windows.Input.Mouse.MouseWheelDeltaForOneLine;
                Microsoft.Xna.Framework.Vector3 translation = TranslateRelativeToCameraView(0, 0, totalDelta);
                camera.Position += translation;
            };

            AssociatedObject.MouseLeftButtonUp += (s, e) =>
            {
                mouseDown = false;
            };
        }

        /// <summary>
        /// Translate the provided difference vector according to the current view direction of the camera
        /// </summary>
        /// <param name="X">Left/Right Yaw</param>
        /// <param name="Y">Up/Down Pitch</param>
        /// <param name="Z">In/Out of screen</param>
        /// <returns></returns>
        private Microsoft.Xna.Framework.Vector3 TranslateRelativeToCameraView(double X, double Y, double Z)
        {
            Vector<double> oDelta = Vector<double>.Build.DenseOfArray(new double[] { X,Y,Z, 1.0 });
            Matrix<double> view = camera.View.ToMathnetMatrix();
            Vector<double> tDelta = view * oDelta;

            Microsoft.Xna.Framework.Vector3 translation = tDelta.ToXNAVector3();
            return translation;
        }
    }
}
