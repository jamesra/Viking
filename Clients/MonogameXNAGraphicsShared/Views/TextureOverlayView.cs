using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Geometry;
using VikingXNA;

namespace VikingXNAGraphics
{
    public class TextureOverlayView : BillboardViewBase
    {
        #region static

        static double BeginFadeCutoff = 0.1;
        static double InvisibleCutoff = 1f;

        #endregion

        public Texture2D Texture;
        //bool FlipTexture = false;
         
        private GridRectangle _BoundingRect; 

        public override GridRectangle BoundingRect
        {
            get { return _BoundingRect; }
            set
            {
                if (_BoundingRect != value)
                {
                    _BoundingRect = value;
                    ClearCachedData();
                }
            }
        }

        public override GridVector2 Position
        {
            get
            {
                return BoundingRect.Center;
            }

            set
            {
                GridVector2 Offset = BoundingRect.Center - BoundingRect.LowerLeft;
                _BoundingRect = new GridRectangle(value - Offset, _BoundingRect.Width, _BoundingRect.Height);
                ClearCachedData();
            }
        }

        public override IShape2D Shape
        {
            get
            {
                return BoundingRect;
            }
        }

        public TextureOverlayView(Texture2D texture, GridVector2 Center, Color color) : base(color)
        {
            this.Texture = texture;
            if (Texture == null)
            {
                GridVector2 offset = new GridVector2(Texture.Width / 2.0, Texture.Height / 2.0);
                this.BoundingRect = new GridRectangle(Center - offset, Center + offset);
            }
        }

        public TextureOverlayView(Texture2D texture, GridRectangle boundingRect, Color color) : base(color)
        {
            this.Texture = texture;
            this.BoundingRect = boundingRect;
        }

        public override void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items)
        {
            OverlayShaderEffect overlayEffect = VikingXNAGraphics.DeviceEffectsStore<OverlayShaderEffect>.TryGet(device);
            if (overlayEffect == null)
                return;

            overlayEffect.Technique = Overlay == OverlayStyle.Alpha ?
                    OverlayShaderEffect.Techniques.TextureAlphaOverlayEffect :
                    OverlayShaderEffect.Techniques.TextureLumaOverlayEffect;

            TextureOverlayView.Draw(device, scene, overlayEffect, items.Select(i => i as TextureOverlayView).Where(i => i != null).ToArray());
        }

        public override void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay)
        {
            OverlayShaderEffect overlayEffect = VikingXNAGraphics.DeviceEffectsStore<OverlayShaderEffect>.TryGet(device);
            if (overlayEffect == null)
                return;

            overlayEffect.Technique = Overlay == OverlayStyle.Alpha ?
                    OverlayShaderEffect.Techniques.TextureAlphaOverlayEffect :
                    OverlayShaderEffect.Techniques.TextureLumaOverlayEffect;

            TextureOverlayView.Draw(device, scene, overlayEffect, new TextureOverlayView[] { this });
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.IScene scene,
                          OverlayShaderEffect overlayEffect,
                          TextureOverlayView[] listToDraw)
        {
            if (listToDraw.Length == 0)
                return;
              
            BlendState originalState = device.BlendState;
            device.BlendState = BlendState.NonPremultiplied;

            var textureGroups = listToDraw.GroupBy(l => l.Texture);
            foreach (var textureGroup in textureGroups)
            {
                TextureOverlayView[] views = textureGroup.ToArray();

                //overlayEffect.AnnotateWithTexture(textureGroup.Key);
                overlayEffect.AnnotationTexture = textureGroup.Key;

                foreach (TextureOverlayView rv in listToDraw)
                {
                    overlayEffect.AnnotationColorHSL = rv.HSLColor;
                    overlayEffect.WorldViewProjMatrix = (rv.ModelMatrix * scene.World) * scene.ViewProj;
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
