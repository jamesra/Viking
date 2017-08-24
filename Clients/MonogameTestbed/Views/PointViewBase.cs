using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using VikingXNAGraphics;
using Microsoft.Xna.Framework;
using VikingXNA;
using System.Collections.Specialized; 

namespace MonogameTestbed
{
    abstract class PointViewBase : IColorView
    {
        private ICollection<GridVector2> _Points;
        private Color _Color;

        public ICollection<GridVector2> Points
        {
            get
            {
                return _Points;
            }
            set
            {
                INotifyCollectionChanged collection = _Points as INotifyCollectionChanged;
                if (collection != null)
                {
                    collection.CollectionChanged -= this.OnCollectionChanged;
                }

                _Points = value;
                collection = _Points as INotifyCollectionChanged;
                if (collection != null)
                {
                    collection.CollectionChanged += this.OnCollectionChanged;
                }
            }
        }

        public Color Color
        {
            get
            {
                return _Color;
            }

            set
            {
                _Color = value;
                UpdateViews();
            }
        }

        public float Alpha
        {
            get
            {
                return _Color.GetAlpha();
            }

            set
            {
                _Color.SetAlpha(value);
                UpdateViews();
            }
        }

        public abstract void UpdateViews();

        internal virtual void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateViews();
        }

        public abstract void Draw(MonoTestbed window, Scene scene);
    }
}
