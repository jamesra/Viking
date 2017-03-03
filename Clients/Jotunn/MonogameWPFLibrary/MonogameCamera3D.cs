using Microsoft.Xna;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input; 

namespace MonogameWPFLibrary
{
    public class MonogameCamera3D : DependencyObject
    {
        private static Vector3 DefaultPositionVector = -Vector3.UnitZ * 5;
        private static Vector3 DefaultLookAtVector = Vector3.Zero;
        private static Vector3 DefaultUpVector = Vector3.UnitZ;

        public static Vector3 DefaultRotationVector = Vector3.Zero;

        public Vector3 Position
        {
            get { return (Vector3)GetValue(PositionProperty); }
            set {
                if (double.IsNaN(value.X) || double.IsNaN(value.Y) || double.IsNaN(value.Z))
                    return;

                SetValue(PositionProperty, value);
            }
        }

        // Using a DependencyProperty as the backing store for Position.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PositionProperty =
            DependencyProperty.Register("Position", typeof(Vector3), typeof(MonogameCamera3D),
                new PropertyMetadata(DefaultPositionVector, OnCameraPropertyChanged));
         
        public Vector3 Rotation
        {
            get { return (Vector3)GetValue(RotationProperty); }
            set { SetValue(RotationProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Rotation.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty RotationProperty =
            DependencyProperty.Register("Rotation", typeof(Vector3), typeof(MonogameCamera3D), 
                new PropertyMetadata(DefaultRotationVector, OnCameraPropertyChanged));

        public double Yaw
        {
            get { return Rotation.X; }
            set {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    return;

                if (value < 0)
                    value += Math.PI * 2;
                else if (value > Math.PI * 2)
                    value -= Math.PI * 2;


                Rotation = new Vector3((float)value, Rotation.Y, Rotation.Z);
            }
        }

        public double Pitch
        {
            get { return Rotation.Y; }
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    return;

                if (value < 0)
                    value += Math.PI * 2;
                else if (value > Math.PI * 2)
                    value -= Math.PI * 2; 

                Rotation = new Vector3(Rotation.X, (float)value, Rotation.Z);
            }
        }


        public Vector3 LookAt
        {
            get { return (Vector3)GetValue(LookAtProperty); }
            set { SetValue(LookAtProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Position.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LookAtProperty =
            DependencyProperty.Register("LookAt", typeof(Vector3), typeof(MonogameCamera3D),
                new PropertyMetadata(DefaultLookAtVector));
         

        public Vector3 Up
        {
            get { return (Vector3)GetValue(UpProperty); }
            set { SetValue(UpProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Up.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty UpProperty =
            DependencyProperty.Register("Up", typeof(Vector3), typeof(MonogameCamera3D),
                new PropertyMetadata(DefaultUpVector, OnCameraPropertyChanged));
         
        public Matrix View
        {
            get { return (Matrix)GetValue(ViewProperty); }
            protected set { SetValue(ViewPropertyKey, value); }
        }

        // Using a DependencyProperty as the backing store for View.  This enables animation, styling, binding, etc...
        protected static readonly DependencyPropertyKey ViewPropertyKey = DependencyProperty.RegisterReadOnly("View", typeof(Matrix), typeof(MonogameCamera3D),
                new FrameworkPropertyMetadata(Matrix.CreateLookAt(DefaultPositionVector, DefaultLookAtVector, DefaultUpVector),
                                              FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ViewProperty = ViewPropertyKey.DependencyProperty;

        static MonogameCamera3D()
        {
        }

        public MonogameCamera3D()
        {
            UpdateViewMatrix();
        }


        private static void OnCameraPropertyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            MonogameCamera3D camera = o as MonogameCamera3D;

            camera.UpdateViewMatrix();
        }

        protected void UpdateViewMatrix()
        {
            //View = Matrix.CreateLookAt(Position, LookAt, Up);
            Vector3 LineOfSightUnitVector = CalculateLineOfSightUnitVector(Rotation.X, Rotation.Y);
            Vector3 OffsetLookAtVector = Position + LineOfSightUnitVector;
            View = Matrix.CreateLookAt(Position, OffsetLookAtVector, Up);
        }

        /// <summary>
        /// Calculate the lookat vector based on the rotation parameters
        /// </summary>
        /// <returns></returns>
        private Vector3 CalculateLineOfSightUnitVector(float yaw, float pitch)
        {
            Vector3 LineOfSightUnitVector = new Vector3(
                (float)(Math.Cos(yaw) * Math.Sin(pitch)),
                (float)(Math.Sin(yaw) * Math.Sin(pitch)),
                (float)(Math.Cos(pitch)));

            return LineOfSightUnitVector;

        }
    }
}
