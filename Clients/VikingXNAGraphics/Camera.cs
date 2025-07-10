using Geometry;
using Microsoft.Xna.Framework;
using System.ComponentModel;

namespace VikingXNA
{
    public class Camera : ICamera, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private Vector2 _LookAt = Vector2.Zero;
        private float _Downsample = 1.0f;

        public Vector2 LookAt
        {
            get => _LookAt;
            set
            {
                if (_LookAt != value)
                {
                    _LookAt = value;
                    OnPropertyChanged(nameof(LookAt));
                }
            }
        }

        public float Downsample
        {
            get => _Downsample;
            set
            {
                if (_Downsample != value)
                {
                    _Downsample = value;
                    OnPropertyChanged(nameof(Downsample));
                }
            }
        }

        public Matrix View
        {
            get
            {
                return Matrix.CreateTranslation(-_LookAt.X, -_LookAt.Y, 0);
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 