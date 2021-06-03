using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using VikingXNA;

namespace VikingXNAGraphics
{
    public class RectangleView : BillboardViewBase, IHitTesting
    {
        private GridRectangle _BoundingRect;

        public override GridRectangle BoundingRect
        {
            get { return _BoundingRect; }
            set {
                if(_BoundingRect != value)
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

        public GridRectangle BoundingBox => BoundingRect;

        public RectangleView(GridRectangle boundingRect, Color color) : base(color)
        {
            this.BoundingRect = boundingRect;
        }

        public override void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items)
        {
            RectangleView.Draw(device, scene, Overlay, items.Select(i => i as RectangleView).Where(i => i != null).ToArray());
        }

        public override void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay)
        {
            RectangleView.Draw(device, scene, Overlay, new RectangleView[] { this });
        }

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
            if (overlayEffect == null)
                return;
            
            BlendState originalState = device.BlendState;
            device.BlendState = BlendState.NonPremultiplied;

            RectangleView[] views = listToDraw.Where(v => v != null).ToArray();
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

        public bool Contains(GridVector2 Position)
        {
            return BoundingRect.Contains(Position);
        }
    }
}
