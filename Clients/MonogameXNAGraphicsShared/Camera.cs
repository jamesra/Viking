using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace VikingXNA
{
    public class Camera : INotifyPropertyChanged
    {
        private Vector3 _LookAt = new(0, 0, 0);

        private float _Pan = MathHelper.ToRadians(0f);
        private float _Tilt = MathHelper.ToRadians(0f);
        private float _Rotation = MathHelper.ToRadians(0f);

        private double _Downsample = 1;

        private Vector2 _Shim = Vector2.Zero;

        /// <summary>
        /// View Matrix is only worth updating when the LookAt parameter changes.
        /// </summary>
        private Matrix _View;
        public Matrix View => _View;

        public Camera()
        {
            UpdateViewMatrix();
        }

        private void UpdateViewMatrix()
        {
            Vector3 target = new(_LookAt.X - _Shim.X, _LookAt.Y - _Shim.Y, _LookAt.Z);
            Vector3 transformedPos = Vector3.Backward + target;

            _View = Matrix.CreateLookAt(transformedPos, target, Vector3.UnitY);
        }

        /// <summary>
        /// View-only translation in volume units. Everything drawn through <see cref="View"/> (tiles, annotations,
        /// labels) appears displaced by +Shim relative to <see cref="LookAt"/>, so the content under the screen centre
        /// has true volume position <see cref="EffectiveLookAt"/>. Scene.ScreenToWorld undoes it, so pointer input and
        /// saved annotation coordinates stay in true volume space. Set by the section-correction shim; zero when no
        /// correction applies.
        /// </summary>
        public Vector2 Shim
        {
            get => _Shim;
            set
            {
                if (_Shim == value)
                    return;

                _Shim = value;
                UpdateViewMatrix();
                CallOnPropertyChanged(new PropertyChangedEventArgs(nameof(Shim)));
            }
        }

        /// <summary>
        /// True volume position of the content at the screen centre: <see cref="LookAt"/> minus <see cref="Shim"/>.
        /// </summary>
        public Vector2 EffectiveLookAt => new(_LookAt.X - _Shim.X, _LookAt.Y - _Shim.Y);


        public float Pan
        {
            get => MathHelper.ToDegrees(_Pan);
            set
            {
                _Pan = MathHelper.ToRadians(value);
                CallOnPropertyChanged(new PropertyChangedEventArgs(nameof(Pan)));
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

                CallOnPropertyChanged(new PropertyChangedEventArgs(nameof(Tilt)));

            }
        }

        public Vector2 LookAt
        {
            get => new(_LookAt.X, _LookAt.Y);
            set
            {
                _LookAt = new Vector3(value, Vector3.Backward.Z);
                UpdateViewMatrix();
                CallOnPropertyChanged(new PropertyChangedEventArgs(nameof(LookAt)));
            }
        }

        public float Rotation
        {
            get => MathHelper.ToDegrees(_Rotation);
            set
            {
                if (float.IsNaN(value))
                    return;

                float val = MathHelper.ToRadians(value);

                if (float.IsNaN(val))
                    val = 0.0f;

                _Rotation = val;
                CallOnPropertyChanged(new PropertyChangedEventArgs(nameof(Rotation)));
            }
        }


        public virtual double Downsample
        {
            set
            {
                Debug.Assert(!double.IsInfinity(value));
                Debug.Assert(!double.IsNegativeInfinity(value));
                Debug.Assert(!double.IsNaN(value));
                if (value < 0)
                    return;

                if (_Downsample != value)
                {
                    _Downsample = value;
                    CallOnPropertyChanged(new PropertyChangedEventArgs(nameof(Downsample)));
                }
            }
            get => _Downsample;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void CallOnPropertyChanged(PropertyChangedEventArgs e) => PropertyChanged?.Invoke(this, e);
    }
}
