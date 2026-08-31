using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace VikingXNA
{
    public class Camera3D : INotifyPropertyChanged, ICamera
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private static Vector3 DefaultPositionVector = -Vector3.UnitZ * 5;
        private static Vector3 DefaultLookAtVector = Vector3.Zero;
        private static readonly Vector3 DefaultUpVector = Vector3.UnitZ;
        public static Vector3 DefaultRotationVector = Vector3.Zero;

        private Vector3 _LookAt = new(0, 0, 0);
        private Vector3 _Position = Vector3.Backward;
        private readonly Vector3 _Up = DefaultUpVector;
        private Vector3 _Rotation = Vector3.Zero;

        private float _Pan = MathHelper.ToRadians(0f);
        private float _Tilt = MathHelper.ToRadians(0f);


        /// <summary>
        /// View Matrix is only worth updating when the LookAt parameter changes.
        /// </summary>
        private Matrix _View;
        public Matrix View => _View;

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
            else
            {
                _View = LineOfSightUnitVectorAccountingForRoundingError == -_Up
                    ? Matrix.CreateLookAt(Position, OffsetLookAtVector, -Vector3.UnitY)
                    : Matrix.CreateLookAt(Position, OffsetLookAtVector, Up);
            }

            //_View = Matrix.CreateLookAt(Position, _LookAt, Up);

        }

        /// <summary>
        /// Calculate the lookat vector based on the rotation parameters
        /// </summary>
        /// <returns></returns>
        private static Vector3 CalculateLineOfSightUnitVector(float yaw, float pitch)
        {

            Vector3 LineOfSightUnitVector = new(
                (float)(Math.Cos(yaw) * Math.Sin(pitch)),
                (float)(Math.Sin(yaw) * Math.Sin(pitch)),
                (float)(Math.Cos(pitch)));

            LineOfSightUnitVector.Normalize();

            return LineOfSightUnitVector;

        }

        /// <summary>
        /// Recover the rotation parameters that <see cref="CalculateLineOfSightUnitVector"/> would turn back into
        /// <paramref name="v"/>.  These two must stay exact inverses: the view matrix is built from the rotation,
        /// so any mismatch here silently aims the camera somewhere other than the requested LookAt.
        /// </summary>
        private static void CalculateRotationFromLineOfSightUnitVector(Vector3 v, double fallbackYaw, out double yaw, out double pitch)
        {
            v.Normalize();

            pitch = Math.Acos(MathHelper.Clamp(v.Z, -1f, 1f));

            //Looking straight along Z leaves yaw unconstrained, and atan2(0,0) would collapse it to zero
            //rather than leaving the caller's heading alone.
            double sinPitch = Math.Sin(pitch);
            yaw = Math.Abs(sinPitch) < 1e-6 ? fallbackYaw : Math.Atan2(v.Y, v.X);
        }

        public float Pan
        {
            get => MathHelper.ToDegrees(_Pan);
            set
            {
                _Pan = MathHelper.ToRadians(value);
                CallOnPropertyChanged();
            }
        }

        public float Tilt
        {
            get => MathHelper.ToDegrees(_Tilt);
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
            get => _LookAt;
            set
            {
                if (value == _Position)
                    return;

                _LookAt = value;
                var lineOfSightVector = _LookAt - _Position;

                CalculateRotationFromLineOfSightUnitVector(lineOfSightVector, _Rotation.X, out double yaw, out double pitch);

                //Assigning Rotation rebuilds the view matrix and raises the change notification.
                this.Rotation = new Vector3((float)yaw, (float)pitch, this.Rotation.Z);
            }
        }

        public Vector3 Position
        {
            get => _Position;
            set
            {
                _Position = value;
                UpdateViewMatrix();
                CallOnPropertyChanged();
            }
        }

        public Vector3 Rotation
        {
            get => _Rotation;
            set
            {
                _Rotation = value;
                UpdateViewMatrix();
                CallOnPropertyChanged();
            }
        }

        public double Yaw
        {
            get => Rotation.X;
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
            get => Rotation.Y;
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


        public Vector3 Up => _Up;

        public Camera3D()
        {
            UpdateViewMatrix();
        }

        protected void CallOnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
