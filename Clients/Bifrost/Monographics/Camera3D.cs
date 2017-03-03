using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace Monographics
{
    public class Camera3D : INotifyPropertyChanged
    {
        private Vector3 _LookAt = new Vector3(0, 0, 0);
        private Vector3 _CameraPosition = Vector3.Backward;

        private float _Pan = MathHelper.ToRadians(0f);
        private float _Tilt = MathHelper.ToRadians(0f);
        private float _Rotation = MathHelper.ToRadians(0f);

        private double _Downsample = 256.0;

        /// <summary>
        /// View Matrix is only worth updating when the LookAt parameter changes.
        /// </summary>
        private Matrix _View;
        public Matrix View { get { return _View; } }

        private void UpdateViewMatrix()
        {  
            _View = Matrix.CreateLookAt(_CameraPosition, _LookAt, Vector3.UnitY);
        }

        
        public float Pan
        {
            get { return MathHelper.ToDegrees(_Pan); }
            set
            {
                _Pan = MathHelper.ToRadians(value);
                CallOnPropertyChanged(new PropertyChangedEventArgs("Pan"));
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

                CallOnPropertyChanged(new PropertyChangedEventArgs("Tilt"));

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
                _LookAt = value;
                UpdateViewMatrix();
                CallOnPropertyChanged(new PropertyChangedEventArgs("LookAt"));
            }
        }

        public Vector3 Position
        {
            get
            {
                return _CameraPosition;
            }
            set
            {
                _CameraPosition = value;
                UpdateViewMatrix();
                CallOnPropertyChanged(new PropertyChangedEventArgs("Position"));
            }
        }

        public float Rotation
        {
            get
            {
                return MathHelper.ToDegrees(_Rotation);
            }
            set
            {
                if (float.IsNaN(value))
                    return;

                float val = MathHelper.ToRadians(value);

                if (float.IsNaN(val))
                    val = 0.0f;

                _Rotation = val;
                CallOnPropertyChanged(new PropertyChangedEventArgs("Rotation"));
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
                _Downsample = value;
                CallOnPropertyChanged(new PropertyChangedEventArgs("Downsample"));
            }
            get
            {
                return _Downsample;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void CallOnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, e);
            }
        }
    }
}
