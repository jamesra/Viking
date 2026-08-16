using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using VikingXNA;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace VikingXNAGraphics
{
    public abstract class PointViewBase : IColorView, IRenderable
    {
        private ICollection<Geometry.Vector2> _Points = [];
        private Color _Color;

        public ICollection<Geometry.Vector2> Points
        {
            get => _Points;
            set
            {
                if (_Points is INotifyCollectionChanged collection)
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
            get => _Color;

            set
            {
                _Color = value;
                UpdateViews();
            }
        }

        public float Alpha
        {
            get => _Color.GetAlpha();

            set
            {
                _Color = _Color.SetAlpha(value);
                UpdateViews();
            }
        }

        public abstract void UpdateViews();

        internal virtual void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => UpdateViews();

        public abstract void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items);
        public abstract void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay);
    }
}
