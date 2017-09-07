using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Geometry;

namespace VikingXNAGraphics
{
    public class TextureOverlayView : BillboardViewBase
    {
        #region static

        static double BeginFadeCutoff = 0.1;
        static double InvisibleCutoff = 1f;

        #endregion

        public Texture2D Texture;
        bool FlipTexture = false;
         
        private GridRectangle _BoundingRect;

        public GridRectangle BoundingRect
        {
            get;
            set;
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
            GridVector2 offset = new GridVector2(Texture.Width / 2.0, Texture.Height / 2.0);
            this.BoundingRect = boundingRect;
        }


        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          BasicEffect basicEffect,
                          AnnotationOverBackgroundLumaEffect overlayEffect,
                          TextureOverlayView[] listToDraw)
        {
            if (listToDraw.Length == 0)
                return;

            BillboardViewBase.SetupGraphicsDevice(device, basicEffect, overlayEffect);

            BlendState originalState = device.BlendState;
            device.BlendState = BlendState.NonPremultiplied;

            var textureGroups = listToDraw.GroupBy(l => l.Texture);
            foreach (var textureGroup in textureGroups)
            {
                TextureOverlayView[] views = textureGroup.ToArray();
                overlayEffect.AnnotateWithTexture(textureGroup.Key);
                basicEffect.Texture = textureGroup.Key;

                int[] indicies;
                VertexPositionColorTexture[] VertArray = AggregatePrimitives(views, out indicies);

                foreach (EffectPass pass in overlayEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    device.DrawUserIndexedPrimitives<VertexPositionColorTexture>(PrimitiveType.TriangleList,
                                                                                         VertArray,
                                                                                         0,
                                                                                         VertArray.Length,
                                                                                         indicies,
                                                                                         0,
                                                                                         indicies.Length / 3);
                }
            }

            device.BlendState = originalState;

            //TextureCircleView.RestoreGraphicsDevice(device, basicEffect);
        }
    }
}
