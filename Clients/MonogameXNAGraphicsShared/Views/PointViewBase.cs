using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq; 
using System.Threading.Tasks;
using VikingXNA; 

namespace VikingXNAGraphics
{
    public abstract class PointViewBase : IColorView, IRenderable
    {
        private ICollection<GridVector2> _Points = new List<GridVector2>();
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

                UpdateViews();
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

        public abstract void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items);
        public abstract void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay);
    }
}
