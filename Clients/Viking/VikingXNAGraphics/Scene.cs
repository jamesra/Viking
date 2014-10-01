using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.ComponentModel;
using Geometry; 

namespace VikingXNA
{
    /// <summary>
    /// Combines a viewport and a camera to produce world, projection, and view matricies and mappings from the scene to the screen
    /// </summary>
    public class Scene : IDisposable
    {
        private Matrix _Projection;

        public Matrix Projection
        {
            get { return _Projection; }
        }

        private Matrix _World;
        public Matrix World
        {
            get { return _World; }
            set { _World = value;
                  _WorldViewProj = (_World * Camera.View) * _Projection;
            }
        }

        private Matrix _WorldViewProj;

        public Matrix WorldViewProj
        {
            get { return _WorldViewProj;}
        }

        private PropertyChangedEventHandler cameraPropertyChangedEventHandler = null;

        private Camera _camera;
        public Camera Camera
        {
            get{ return _camera; }
            set
            {
                if(value.Equals(_camera))
                    return; 

                if(_camera != null)
                    _camera.PropertyChanged -= cameraPropertyChangedEventHandler;
                
                if(value != null)
                {
                    value.PropertyChanged += cameraPropertyChangedEventHandler;
                    _camera = value; 
                    UpdateProjectionMatrix();
                }
            }
        }

        public readonly float MaxDrawDistance = 10f;
        public readonly float MinDrawdistance = 0.5f;

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
                if(_Viewport.Equals(value) == false)
                    _Viewport = value;

                UpdateProjectionMatrix();
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
            _WorldViewProj = (_World * Camera.View) * _Projection;

            
        }

        private void UpdateProjectionMatrix()
        {
            _Projection = Matrix.CreateOrthographic((float)(_Viewport.Width * _camera.Downsample), (float)(_Viewport.Height * _camera.Downsample), MinDrawdistance, MaxDrawDistance);
            _WorldViewProj = (World * Camera.View) * _Projection;
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
            }
        }

        public GridRectangle VisibleWorldBounds
        {
            get
            {
                //For debugging
                const int offset = 0;

                GridRectangle projectedArea = new GridRectangle(new GridVector2(0, 0), ((double)_Viewport.Width * Camera.Downsample), (double)_Viewport.Height * Camera.Downsample); ;
                GridVector2 BottomLeft = ScreenToWorld(offset, _Viewport.Height);
                GridRectangle rect = new GridRectangle(BottomLeft, projectedArea.Width, projectedArea.Height);
                return rect;
            }
        }

        public GridVector2 ScreenToWorld(double X, double Y)
        {
            //The screen coordinates used by Windows and XNA put the Y origin at the top and bottom of the screen
            double XPos = ((X - ((double)_Viewport.Width / 2)) * Camera.Downsample) + Camera.LookAt.X;
            double YPos = -((Y - ((double)_Viewport.Height / 2)) * Camera.Downsample) + Camera.LookAt.Y;

            return new GridVector2(XPos, YPos);
        }

        public GridVector2 WorldToScreen(double X, double Y)
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
