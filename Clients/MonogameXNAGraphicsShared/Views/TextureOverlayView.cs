using Geometry;
using Rectangle = Geometry.Rectangle;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using VikingXNA;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace VikingXNAGraphics
{
    public class TextureOverlayView : BillboardViewBase
    {
        #region static

        static readonly double BeginFadeCutoff = 0.1;
        static readonly double InvisibleCutoff = 1f;

        #endregion

        public Texture2D Texture;
        //bool FlipTexture = false;

        private Rectangle _BoundingRect;

        public override Rectangle BoundingRect
        {
            get => _BoundingRect;
            set
            {
                if (_BoundingRect != value)
                {
                    _BoundingRect = value;
                    ClearCachedData();
                }
            }
        }

        public override Geometry.Vector2 Position
        {
            get => BoundingRect.Center;

            set
            {
                Geometry.Vector2 Offset = BoundingRect.Center - BoundingRect.LowerLeft;
                _BoundingRect = new Rectangle(value - Offset, _BoundingRect.Width, _BoundingRect.Height);
                ClearCachedData();
            }
        }

        public override IShape2D Shape => BoundingRect;

        public TextureOverlayView(Texture2D texture, Geometry.Vector2 Center, Color color) : base(color)
        {
            this.Texture = texture;
            if (Texture is not null)
            {
                Geometry.Vector2 offset = new(Texture.Width / 2.0, Texture.Height / 2.0);
                this.BoundingRect = new Rectangle(Center - offset, Center + offset);
            }
        }

        public TextureOverlayView(Texture2D texture, Rectangle boundingRect, Color color) : base(color)
        {
            this.Texture = texture;
            this.BoundingRect = boundingRect;
        }

        public override void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items)
        {
            OverlayShaderEffect overlayEffect = VikingXNAGraphics.DeviceEffectsStore<OverlayShaderEffect>.TryGet(device);
            if (overlayEffect is null)
                return;

            overlayEffect.Technique = Overlay == OverlayStyle.Alpha ?
                    OverlayShaderEffect.Techniques.TextureAlphaOverlayEffect :
                    OverlayShaderEffect.Techniques.TextureLumaOverlayEffect;

            TextureOverlayView.Draw(device, scene, overlayEffect, [.. items.Select(i => i as TextureOverlayView).Where(i => i != null)]);
        }

        public override void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay)
        {
            OverlayShaderEffect overlayEffect = VikingXNAGraphics.DeviceEffectsStore<OverlayShaderEffect>.TryGet(device);
            if (overlayEffect is null)
                return;

            overlayEffect.Technique = Overlay == OverlayStyle.Alpha ?
                    OverlayShaderEffect.Techniques.TextureAlphaOverlayEffect :
                    OverlayShaderEffect.Techniques.TextureLumaOverlayEffect;

            TextureOverlayView.Draw(device, scene, overlayEffect, [this]);
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.IScene scene,
                          OverlayShaderEffect overlayEffect,
                          TextureOverlayView[] listToDraw)
        {
            if (listToDraw.Length == 0)
                return;

            device.Indices = GlobalPrimitives.GetUnitSquareIndexBuffer(device);
            device.SetVertexBuffer(GlobalPrimitives.GetUnitSquareVertexBuffer(device));
            BlendState originalState = device.BlendState;
            device.BlendState = BlendState.NonPremultiplied;

            Matrix worldViewProj = scene.World * scene.ViewProj;
            var textureGroups = listToDraw.GroupBy(l => l.Texture);
            foreach (var textureGroup in textureGroups)
            {
                TextureOverlayView[] views = [.. textureGroup];

                //overlayEffect.AnnotateWithTexture(textureGroup.Key);
                overlayEffect.AnnotationTexture = textureGroup.Key;

                foreach (TextureOverlayView rv in views)
                {
                    overlayEffect.AnnotationColorHSL = rv.HSLColor;
                    overlayEffect.WorldViewProjMatrix = rv.ModelMatrix * worldViewProj;
                    //TODO: Use GlobalPrimitives and model matricies instead of verticies

                    foreach (EffectPass pass in overlayEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();

                        device.DrawIndexedPrimitives(PrimitiveType.TriangleList,
                            0,
                            0,
                            6,
                            0,
                            2);
                    }
                }
            }

            device.BlendState = originalState;
        }

    }
}
