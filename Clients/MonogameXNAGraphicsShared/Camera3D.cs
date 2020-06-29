using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.CompilerServices;


namespace VikingXNA
{
    public class Camera3D : INotifyPropertyChanged, ICamera
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private static Vector3 DefaultPositionVector = -Vector3.UnitZ * 5;
        private static Vector3 DefaultLookAtVector = Vector3.Zero;
        private static Vector3 DefaultUpVector = Vector3.UnitZ;
        public static Vector3 DefaultRotationVector = Vector3.Zero;
        
        private Vector3 _LookAt = new Vector3(0, 0, 0);
        private Vector3 _Position = Vector3.Backward;
        private Vector3 _Up = DefaultUpVector;
        private Vector3 _Rotation = Vector3.Zero;

        private float _Pan = MathHelper.ToRadians(0f);
        private float _Tilt = MathHelper.ToRadians(0f);
        
        
        /// <summary>
        /// View Matrix is only worth updating when the LookAt parameter changes.
        /// </summary>
        private Matrix _View;
        public Matrix View { get { return _View; } }

        private void UpdateViewMatrix()
        {
            Vector3 LineOfSightUnitVector = CalculateLineOfSightUnitVector(Rotation.X, Rotation.Y);
            Vector3 OffsetLookAtVector = Position + LineOfSightUnitVector;
            //OffsetLookAtVector.Normalize();
            Vector3 LineOfSightUnitVectorAccountingForRoundingError = Position - OffsetLookAtVector;
            if (LineOfSightUnitVectorAccountingForRoundingError == _Up)
            {
                _View = Matrix.CreateLookAt(Position, OffsetLookAtVector, Vector3.UnitY);
            }
            else if(LineOfSightUnitVectorAccountingForRoundingError == -_Up)
            {
                _View = Matrix.CreateLookAt(Position, OffsetLookAtVector, -Vector3.UnitY);
            }
            else
            {
                _View = Matrix.CreateLookAt(Position, OffsetLookAtVector, Up);
            }

            //_View = Matrix.CreateLookAt(Position, _LookAt, Up);
            
        }

        /// <summary>
        /// Calculate the lookat vector based on the rotation parameters
        /// </summary>
        /// <returns></returns>
        private static Vector3 CalculateLineOfSightUnitVector(float yaw, float pitch)
        { 

            Vector3 LineOfSightUnitVector = new Vector3(
                (float)(Math.Cos(yaw) * Math.Sin(pitch)),
                (float)(Math.Sin(yaw) * Math.Sin(pitch)),
                (float)(Math.Cos(pitch)));

            LineOfSightUnitVector.Normalize();

            return LineOfSightUnitVector;

        }

        /// <summary>
        /// Calculate the lookat vector based on the rotation parameters
        /// </summary>
        /// <returns></returns>
        private static void CalculateRotationFromLineOfSightUnitVector(Vector3 v, out double yaw, out double pitch)
        {
            v.Normalize();
            yaw = Math.Asin(-v.Y);
            pitch = Math.Atan2(v.X, v.Z);
        }

        public float Pan
        {
            get { return MathHelper.ToDegrees(_Pan); }
            set
            {
                _Pan = MathHelper.ToRadians(value);
                CallOnPropertyChanged();
            }
        }

        public float Tilt
        {
            get { return MathHelper.ToDegrees(_Tilt); }
            set
            {
                if (value >= 90)
                    value = 89;
                else if (value <= 0)
                    value = float.Epsilon;

                _Tilt = MathHelper.ToRadians(value);

                CallOnPropertyChanged();

            }
        }

        public Vector3 LookAt
        {
            get
            {
                return _LookAt;
            }
            set
            {
                if (value == _Position)
                    return;

                _LookAt = value;
                var lineOfSightVector = _LookAt - _Position;

                CalculateRotationFromLineOfSightUnitVector(lineOfSightVector, out double yaw, out double pitch);
                this.Rotation = new Vector3((float)yaw, (float)pitch, (float)this.Rotation.Z);
                
                //UpdateViewMatrix();
                //CallOnPropertyChanged();
            }
        }

        public Vector3 Position
        {
            get
            {
                return _Position;
            }
            set
            {
                _Position = value;
                UpdateViewMatrix();
                CallOnPropertyChanged();
            }
        }

        public Vector3 Rotation
        {
            get
            {
                return _Rotation;
            }
            set
            {
                _Rotation = value;
                UpdateViewMatrix();
                CallOnPropertyChanged();
            }
        }
         
        public double Yaw
        {
            get { return Rotation.X; }
            set
            {
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


        public Vector3 Up
        {
            get { return _Up; }
        } 

        public Camera3D()
        {
            UpdateViewMatrix();
        }

        protected void CallOnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
