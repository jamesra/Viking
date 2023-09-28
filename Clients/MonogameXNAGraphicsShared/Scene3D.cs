
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel; 
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace VikingXNA
{
    public class Scene3D : IScene
    {
        public event PropertyChangedEventHandler OnSceneChanged;

        Matrix _Projection;
        Matrix _World;
        Matrix _WorldViewProj;

        private float _MinDrawDistance = 0.01f;
        private float _MaxDrawDistance = 100f;

        public Camera3D _Camera;

        private float _FieldOfView = (float)(Math.PI / 3.0f);

        public Matrix Projection
        {
            get
            {
                return _Projection;
            }
        }

        public Matrix View
        {
            get
            {
                return Camera.View;
            }
        }

        public Matrix ViewProj
        {
            get
            {
                return this.Camera.View * this.Projection;
            }
        }

        public Matrix World
        {
            get { return _World; }
            set
            {
                _World = value;
                _WorldViewProj = (_World * Camera.View) * _Projection;
                OnPropertyChanged();
            }
        }

        public Matrix WorldViewProj
        {
            get
            {
                return _WorldViewProj;
            }
        }

        private Viewport _Viewport;
        /// <summary>
        /// The viewport used for this scene.
        /// </summary>
        public Viewport Viewport
        {
            get
            {
                return _Viewport;
            }
            set
            {
                if (_Viewport.Equals(value) == false)
                    _Viewport = value;

                UpdateProjectionMatrix(); 
                OnPropertyChanged();
            }
        }

        public float FieldOfView
        {
            get { return _FieldOfView; }
            set
            {
                if (_FieldOfView != value)
                {
                    _FieldOfView = value;
                    UpdateProjectionMatrix();
                    OnPropertyChanged();
                }
            }
        }

        public float MinDrawDistance
        {
            get { return _MinDrawDistance; }
            set
            {
                if (_MinDrawDistance != value)
                {
                    _MinDrawDistance = value;
                    UpdateProjectionMatrix();
                    OnPropertyChanged();
                }
            }
        }

        public float MaxDrawDistance
        {
            get { return _MaxDrawDistance; }
            set
            {
                if (_MaxDrawDistance != value)
                {
                    _MaxDrawDistance = value;
                    UpdateProjectionMatrix();
                    OnPropertyChanged();
                }
            }
        }

        private readonly PropertyChangedEventHandler cameraPropertyChangedEventHandler = null;

        public Camera3D Camera
        {
            get { return _Camera; }
            set
            {
                if (value.Equals(_Camera))
                    return;

                if (_Camera != null)
                    _Camera.PropertyChanged -= cameraPropertyChangedEventHandler;

                if (value != null)
                {
                    value.PropertyChanged += cameraPropertyChangedEventHandler;
                    _Camera = value;
                    UpdateProjectionMatrix();
                }

                OnPropertyChanged();
            }
        }

        public Scene3D(Viewport v, Camera3D cam)
        {

            this.cameraPropertyChangedEventHandler = new PropertyChangedEventHandler(OnCameraPropertyChanged);

            this._Camera = cam;
            if (_Camera != null)
                _Camera.PropertyChanged += cameraPropertyChangedEventHandler;

            _Viewport = v;

            _World = Matrix.Identity;

            UpdateProjectionMatrix();
        }


        private void UpdateProjectionMatrix()
        {
            _Projection = Matrix.CreatePerspectiveFieldOfView(FieldOfView, Viewport.AspectRatio, _MinDrawDistance, _MaxDrawDistance);
            _WorldViewProj = (World * Camera.View) * _Projection; 
        }


        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnSceneChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnCameraPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Debug.Assert(sender.Equals(this.Camera));
            if (e.PropertyName == "Downsample")
            {
                UpdateProjectionMatrix();
            }
            else
            {
                _WorldViewProj = (_World * Camera.View) * _Projection;
            }

            OnPropertyChanged("Camera." + e.PropertyName);
        }
    }
}
