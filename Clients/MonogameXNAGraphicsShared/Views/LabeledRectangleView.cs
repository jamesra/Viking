using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VikingXNA;

namespace VikingXNAGraphics
{
    /// <summary>
    /// Text in a box!
    /// </summary>
    public class LabeledRectangleView : IRenderable, IDualColorView, IViewPosition2D, IHitTesting, IText
    {
        VikingXNAGraphics.RectangleView BackgroundBox = null;
        VikingXNAGraphics.LabelView Label = null;

        public string Text
        {
            get => ((IText)Label).Text;
            set
            {
                ((IText)Label).Text = value;
                BackgroundBox.BoundingRect = Label.BoundingRect;
            }
        }

        public double FontSize
        {
            get => ((IText)Label).FontSize;
            set
            {
                ((IText)Label).FontSize = value;
                BackgroundBox.BoundingRect = Label.BoundingRect;
            }
        }

        public GridRectangle BoundingBox
        {
            get
            {
                return ((IHitTesting)BackgroundBox).BoundingBox;
            }
            set
            {
                if (BackgroundBox.BoundingRect == value)
                    return;

                BackgroundBox.BoundingRect = value;
                Label.FontSize = Label.GetFontSizeToFitBounds(value);
            }
        }

        public IColorView ForegroundColor => (IColorView)Label;

        public IColorView BackgroundColor => (IColorView)BackgroundBox;

        public GridVector2 Position
        {
            get {
                return BackgroundBox.Position;
            } 
            set
            {
                BackgroundBox.Position = value;
                Label.Position = value; 
            }
        }

        public LabeledRectangleView(string text, GridRectangle bbox, Alignment alignment = null, bool scaleFontWithScene = true, double fontSize = 16.0)
        {
            Label = new LabelView(text, bbox.Center, alignment, Anchor.CenterCenter, scaleFontWithScene, fontSize: fontSize);
            Label.FontSize = Label.GetFontSizeToFitBounds(bbox);
            BackgroundBox = new RectangleView(bbox, Color.Gray);
        }

        public LabeledRectangleView(string text, GridVector2 position, Alignment alignment = null, Anchor anchor = null, bool scaleFontWithScene = true, double fontSize = 16.0)
        {
            Label = new LabelView(text, position, alignment, anchor, scaleFontWithScene, fontSize: fontSize);
            BackgroundBox = new RectangleView(Label.BoundingRect, Color.Gray);
        }

        public LabeledRectangleView(string text, GridRectangle bbox, Color foreground, Color background, Alignment alignment = null, bool scaleFontWithScene = true, double fontSize = 16.0)
            : this(text, bbox, alignment, scaleFontWithScene, fontSize)
        {
            Label.Color = foreground;
            BackgroundBox.Color = background;
        }

        public LabeledRectangleView(string text, GridVector2 position, Color foreground, Color background, Alignment alignment = null, Anchor anchor = null, bool scaleFontWithScene = true, double fontSize = 16.0)
            : this(text, position, alignment, anchor, scaleFontWithScene, fontSize)
        {
            Label.Color = foreground;
            BackgroundBox.Color = background;
        }

        public bool Contains(GridVector2 Position)
        {
            return ((IHitTesting)BackgroundBox).Contains(Position);
        }

        public void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay)
        {
            RectangleView.Draw(device, scene, Overlay, new RectangleView[] { BackgroundBox });
            var fontData = DeviceFontStore.TryGet(device);
            LabelView.Draw(fontData.SpriteBatch, Label.font, scene, new LabelView[] { Label });
        }

        public void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items)
        {
            var label_rectangle_views = items.Where(i => i is LabeledRectangleView).Select(i => (LabeledRectangleView)i).ToArray();

            RectangleView.Draw(device, scene, Overlay, label_rectangle_views.Select(i => i.BackgroundBox).ToArray() );
            var fontData = DeviceFontStore.TryGet(device);
            LabelView.Draw(fontData.SpriteBatch, Label.font, scene, label_rectangle_views.Select(i => i.Label).ToArray()); 
        }

        public static void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay, LabeledRectangleView[] label_rectangle_views)
        { 
            RectangleView.Draw(device, scene, Overlay, label_rectangle_views.Select(i => i.BackgroundBox).ToArray());
            var fontData = DeviceFontStore.TryGet(device);

            foreach(var fontGroup in label_rectangle_views.GroupBy(lr => lr.Label.font) )
            {
                LabelView.Draw(fontData.SpriteBatch, fontGroup.Key, scene, fontGroup.Select(i => i.Label).ToArray());
            }
        }
    }
}
