using Geometry;
using Rectangle = Geometry.Rectangle;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using VikingXNA;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace VikingXNAGraphics
{
    public class RectangleView : BillboardViewBase, IHitTesting
    {
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

        public Rectangle BoundingBox => BoundingRect;

        public RectangleView(Rectangle boundingRect, Color color) : base(color)
        {
            this.BoundingRect = boundingRect;
        }

        public override void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items) => RectangleView.Draw(device, scene, Overlay, [.. items.Select(i => i as RectangleView).Where(i => i != null)]);

        public override void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay) => RectangleView.Draw(device, scene, Overlay, [this]);

        public static void Draw(GraphicsDevice device,
                          VikingXNA.IScene scene,
                          OverlayStyle Overlay,
                          RectangleView[] listToDraw)
        {
            if (listToDraw.Length == 0)
                return;

            device.SetVertexBuffer(GlobalPrimitives.GetUnitSquareVertexBuffer(device));
            device.Indices = GlobalPrimitives.GetUnitSquareIndexBuffer(device);
            //BillboardViewBase.SetupGraphicsDevice(device, basicEffect, overlayEffect);

            OverlayShaderEffect overlayEffect = VikingXNAGraphics.DeviceEffectsStore<OverlayShaderEffect>.TryGet(device);
            if (overlayEffect is null)
                return;

            BlendState originalState = device.BlendState;
            device.BlendState = BlendState.NonPremultiplied;

            RectangleView[] views = [.. listToDraw.Where(v => v != null)];
            //overlayEffect.AnnotateWithTexture(null);
            overlayEffect.Technique = Overlay == OverlayStyle.Alpha ?
                OverlayShaderEffect.Techniques.SingleColorAlphaOverlayEffect :
                OverlayShaderEffect.Techniques.SingleColorLumaOverlayEffect;

            foreach (RectangleView rv in listToDraw)
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

            device.BlendState = originalState;
        }

        public bool Contains(Geometry.Vector2 Position) => BoundingRect.Contains(Position);
    }
}
