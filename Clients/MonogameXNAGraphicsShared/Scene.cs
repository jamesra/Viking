using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace VikingXNA
{
    /// <summary>
    /// Combines a viewport and a camera to produce world, projection, and view matricies and mappings from the scene to the screen
    /// </summary>
    public class Scene : IScene, IDisposable
    {
        public event PropertyChangedEventHandler OnSceneChanged;

        private Matrix _Projection;
        private Matrix _World;
        private Matrix _WorldViewProj;

        private float _MinDrawDistance = -100f;
        private float _MaxDrawDistance = 10f;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnSceneChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Matrix Projection => _Projection;

        public Matrix View => this.Camera.View;

        public Matrix ViewProj => this.Camera.View * this.Projection;

        public Matrix World
        {
            get => _World;
            set
            {
                _World = value;
                _WorldViewProj = (_World * Camera.View) * _Projection;
                OnPropertyChanged();
            }
        }

        public Matrix WorldViewProj => _WorldViewProj;


        private PropertyChangedEventHandler cameraPropertyChangedEventHandler = null;

        private Camera _camera;
        public Camera Camera
        {
            get => _camera;
            set
            {
                if (value.Equals(_camera))
                    return;

                if (_camera != null)
                    _camera.PropertyChanged -= cameraPropertyChangedEventHandler;

                if (value != null)
                {
                    value.PropertyChanged += cameraPropertyChangedEventHandler;
                    _camera = value;
                    UpdateProjectionMatrix();
                }

                OnPropertyChanged();
            }
        }

        private Viewport _Viewport;
        /// <summary>
        /// The viewport used for this scene.
        /// </summary>
        public Viewport Viewport
        {
            get => _Viewport;
            set
            {
                if (_Viewport.Equals(value) == false)
                    _Viewport = value;

                UpdateProjectionMatrix();
                OnPropertyChanged();
            }
        }

        public float MinDrawDistance
        {
            get => _MinDrawDistance;
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
            get => _MaxDrawDistance;
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

        public Scene(Viewport v, Camera cam)
        {

            this.cameraPropertyChangedEventHandler = new PropertyChangedEventHandler(OnCameraPropertyChanged);

            this._camera = cam;
            if (_camera != null)
                _camera.PropertyChanged += cameraPropertyChangedEventHandler;

            _Viewport = v;
            _World = Matrix.Identity;

            UpdateProjectionMatrix();
        }

        private void UpdateProjectionMatrix()
        {
            _Projection = Matrix.CreateOrthographic((float)(_Viewport.Width * _camera.Downsample), (float)(_Viewport.Height * _camera.Downsample), MinDrawDistance, MaxDrawDistance);
            _WorldViewProj = (World * Camera.View) * _Projection;
            ResetVisibleWorldBounds();
        }

        private void OnCameraPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Debug.Assert(sender.Equals(this._camera));
            if (e.PropertyName == "Downsample")
            {
                UpdateProjectionMatrix();
            }
            else
            {
                _WorldViewProj = (_World * Camera.View) * _Projection;
                ResetVisibleWorldBounds();
            }

            OnPropertyChanged("Camera." + e.PropertyName);
        }

        private readonly SemaphoreSlim initVisibleWorldBoundsSemaphore = new SemaphoreSlim(1);
        private Geometry.GridRectangle? _VisibleWorldBounds; //This should only be set by using ResetVisibleWorldBounds

        public Geometry.GridRectangle VisibleWorldBounds
        {
            get
            {
                var visibleBounds = _VisibleWorldBounds;
                if (visibleBounds.HasValue)
                    return visibleBounds.Value;

                try
                {
                    initVisibleWorldBoundsSemaphore.Wait();
                    visibleBounds = _VisibleWorldBounds;
                    if (visibleBounds.HasValue)
                        return visibleBounds.Value;

                    double offset = 0;
                    var projectedArea = new GridRectangle(new GridVector2(0, 0), ((double)_Viewport.Width * Camera.Downsample), (double)_Viewport.Height * Camera.Downsample); ;
                    var BottomLeft = ScreenToWorld(offset, _Viewport.Height);
                    var result = new GridRectangle(BottomLeft, projectedArea.Width, projectedArea.Height);
                    _VisibleWorldBounds = result;
                    return result;
                }
                finally
                {
                    initVisibleWorldBoundsSemaphore.Release();
                } 
            }
            set
            {
                Camera.LookAt = new Vector2((float)value.Center.X, (float)value.Center.Y);
                Camera.Downsample = Math.Max(value.Height, value.Width) / Math.Min(Viewport.Height, Viewport.Width);
                try
                {
                    initVisibleWorldBoundsSemaphore.Wait();
                    _VisibleWorldBounds = value;
                }
                finally
                {
                    initVisibleWorldBoundsSemaphore.Release();
                }
            }
        }

        private void ResetVisibleWorldBounds()
        {
            try
            {
                initVisibleWorldBoundsSemaphore.Wait();
                _VisibleWorldBounds = null;
            }
            finally
            {
                initVisibleWorldBoundsSemaphore.Release();
            }
        }

        public double MinVisibleWorldBorderLength => Math.Min(this.VisibleWorldBounds.Width, this.VisibleWorldBounds.Height);

        public double MaxVisibleWorldBorderLength => Math.Max(this.VisibleWorldBounds.Width, this.VisibleWorldBounds.Height);

        /// <summary>
        /// Returns how large a single pixel is on the device in world coordinates
        /// </summary>
        public double DevicePixelWidth => this.VisibleWorldBounds.Width / (double)this.Viewport.Width;

        /// <summary>
        /// Returns how large a single pixel is on the device in world coordinates
        /// </summary>
        public double DevicePixelHeight => this.VisibleWorldBounds.Height / (double)this.Viewport.Height;

        public double ScreenPixelSizeInVolume => Math.Min(this.DevicePixelHeight, this.DevicePixelWidth);

        public Geometry.GridVector2 ScreenToWorld(GridVector2 pos)
        {
            return ScreenToWorld(pos.X, pos.Y);
        }

        public Geometry.GridVector2 ScreenToWorld(double X, double Y)
        {
            //The screen coordinates used by Windows and XNA put the Y origin at the top and bottom of the screen
            double XPos = ((X - ((double)_Viewport.Width / 2)) * Camera.Downsample) + Camera.LookAt.X;
            double YPos = -((Y - ((double)_Viewport.Height / 2)) * Camera.Downsample) + Camera.LookAt.Y;

            return new GridVector2(XPos, YPos);
        }

        public Geometry.GridVector2 WorldToScreen(GridVector2 pos)
        {
            return WorldToScreen(pos.X, pos.Y);
        }

        public Geometry.GridVector2 WorldToScreen(double X, double Y)
        {
            Vector3 p = _Viewport.Project(new Vector3((float)X, (float)Y, 0), _Projection, Camera.View, World);
            return new GridVector2(p.X, p.Y);
        }

        protected void Dispose(bool freeManagedObjectsAlso)
        {
            if (freeManagedObjectsAlso)
            {
                if (_camera != null)
                {
                    _camera.PropertyChanged -= this.cameraPropertyChangedEventHandler;
                    _camera = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

    }
}
