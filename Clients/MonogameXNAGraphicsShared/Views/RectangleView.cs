using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Geometry;

namespace VikingXNAGraphics
{
    public class RectangleView : BillboardViewBase
    {
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
        
        public RectangleView(GridRectangle boundingRect, Color color) : base(color)
        {
            this.BoundingRect = boundingRect;
        }

        protected static VertexPositionColor[] AggregatePrimitives(RectangleView[] listToDraw, out int[] indicies)
        {
            VertexPositionColor[] VertArray = new VertexPositionColor[listToDraw.Length * 4];
            indicies = new int[listToDraw.Length * 6];

            int iNextVert = 0;
            int iNextVertIndex = 0;

            for (int iObj = 0; iObj < listToDraw.Length; iObj++)
            {
                RectangleView locToDraw = listToDraw[iObj];
                int[] locIndicies;
                VertexPositionColorTexture[] objVerts = RectangleView.GetRenderableVerticies(locToDraw.BackgroundVerts, locToDraw.HSLColor, out locIndicies);

                if (objVerts == null)
                    continue;

                VertexPositionColor[] VPC_Verts = objVerts.Select(v => new VertexPositionColor(v.Position, v.Color)).ToArray();

                Array.Copy(VPC_Verts, 0, VertArray, iNextVert, objVerts.Length);

                for (int iVert = 0; iVert < locIndicies.Length; iVert++)
                {
                    indicies[iNextVertIndex + iVert] = locIndicies[iVert] + iNextVert;
                }

                iNextVert += objVerts.Length;
                iNextVertIndex += locIndicies.Length;
            }

            return VertArray;
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          BasicEffect basicEffect,
                          AnnotationOverBackgroundLumaEffect overlayEffect,
                          RectangleView[] listToDraw)
        {
            if (listToDraw.Length == 0)
                return;

            BillboardViewBase.SetupGraphicsDevice(device, basicEffect, overlayEffect);

            BlendState originalState = device.BlendState;
            device.BlendState = BlendState.NonPremultiplied;


            RectangleView[] views = listToDraw.Where(v => v != null).ToArray();
            overlayEffect.AnnotateWithTexture(null);
            basicEffect.Texture = null;

            int[] indicies;
            VertexPositionColor[] VertArray = RectangleView.AggregatePrimitives(views, out indicies);

            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();

                device.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.TriangleList,
                                                                                        VertArray,
                                                                                        0,
                                                                                        VertArray.Length,
                                                                                        indicies,
                                                                                        0,
                                                                                        indicies.Length / 3);
            }
            

            device.BlendState = originalState;

            BillboardViewBase.RestoreGraphicsDevice(device, basicEffect); 

            //TextureCircleView.RestoreGraphicsDevice(device, basicEffect);
        }
    }
}
