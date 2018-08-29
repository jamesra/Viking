using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Geometry;

namespace VikingXNAGraphics
{
    public abstract class BillboardViewBase : IColorView, IViewPosition2D
    {
        public abstract IShape2D Shape { get; }

        public BillboardViewBase(Color color)
        { 
            this.Color = color; 
        }

        public virtual float Alpha
        {
            get
            {
                return _Color.GetAlpha();
            }
            set
            {
                Color = this._Color.SetAlpha(value);
            }
        }
        
        protected Microsoft.Xna.Framework.Color _Color;
        public virtual Microsoft.Xna.Framework.Color Color
        {
            get
            {
                return _Color;
            }
            set
            {
                _Color = value;
                _HSLColor = value.ConvertToHSL();
                ClearCachedData();
            }
        }

        protected Microsoft.Xna.Framework.Color _HSLColor;
        public virtual Microsoft.Xna.Framework.Color HSLColor
        {
            get
            {
                return _HSLColor;
            }
        }

        protected VertexPositionColorTexture[] _BackgroundVerts = null;
        public virtual VertexPositionColorTexture[] BackgroundVerts
        {
            get
            {
                if (_BackgroundVerts == null)
                {
                    _BackgroundVerts = CreateVerticies(this.Shape);
                }

                return _BackgroundVerts;
            }
        }

        public abstract GridVector2 Position { get; set; }

        /// <summary>
        /// Called when the position or color of the view change
        /// </summary>
        protected virtual void ClearCachedData()
        {
            _BackgroundVerts = null;
        }

        /// <summary>
        /// Create billboard primitive the size and position of the circle
        /// </summary>
        /// <param name="shape"></param>
        /// <returns></returns>
        public virtual VertexPositionColorTexture[] CreateVerticies(IShape2D shape)
        {
            VertexPositionColorTexture[] Verts = new VertexPositionColorTexture[GlobalPrimitives.SquareVerts.Length];
            GlobalPrimitives.SquareVerts.CopyTo(Verts, 0);

            GridRectangle rect = shape.BoundingBox;

            GridVector2 offset = rect.UpperRight - rect.Center;

            for (int i = 0; i < Verts.Length; i++)
            {
                Verts[i].Position.X *= (float)offset.X;
                Verts[i].Position.Y *= (float)offset.Y;
                Verts[i].Position.X += (float)rect.Center.X;
                Verts[i].Position.Y += (float)rect.Center.Y;
            }

            return Verts;
        }
  
        /// <summary>
        /// The verticies should really be cached and handed up to LocationObjRenderer so all similiar objects can be rendered in one
        /// call.  This method is in the middle of a change from using triangles to draw circles to using textures. 
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="DirectionToVisiblePlane"></param>
        /// <param name="color"></param>
        public static VertexPositionColorTexture[] GetRenderableVerticies(VertexPositionColorTexture[] PositionedVerticies, Microsoft.Xna.Framework.Color HSLColor, out int[] indicies)
        {
            //            GridVector2 Pos = this.VolumePosition;

            //Can't populate until we've referenced CircleVerts
            indicies = GlobalPrimitives.SquareIndicies;
            //            float radius = (float)this.Radius;
             
            float SatScalar = HSLColor.B / 255f;

            //Draw an opaque border around the background
            for (int i = 0; i < PositionedVerticies.Length; i++)
            {
                PositionedVerticies[i].Color = HSLColor;
                //verts[i].Color.G = (byte)((((float)HSLColor.G / 255f) * SatScalar) * 255); // This line restores the nice luma blending effect I had pre-curce annotations
            }

            return PositionedVerticies;
        }

        public static void SetupGraphicsDevice(GraphicsDevice device, BasicEffect basicEffect, AnnotationOverBackgroundLumaEffect overlayEffect)
        {
            DeviceStateManager.SaveDeviceState(device);
            //DeviceStateManager.SetRenderStateForShapes(device);
            //DeviceStateManager.SetRasterizerStateForShapes(device);

            basicEffect.TextureEnabled = false;
            basicEffect.VertexColorEnabled = true;
            basicEffect.LightingEnabled = false; 
        }

        public static void RestoreGraphicsDevice(GraphicsDevice graphicsDevice, BasicEffect basicEffect)
        {
            DeviceStateManager.RestoreDeviceState(graphicsDevice);

            basicEffect.TextureEnabled = false;
            basicEffect.VertexColorEnabled = false;
        }

        protected static VertexPositionColorTexture[] AggregatePrimitives(BillboardViewBase[] listToDraw, out int[] indicies)
        {
            VertexPositionColorTexture[] VertArray = new VertexPositionColorTexture[listToDraw.Length * 4];
            indicies = new int[listToDraw.Length * 6];

            int iNextVert = 0;
            int iNextVertIndex = 0;

            for (int iObj = 0; iObj < listToDraw.Length; iObj++)
            {
                BillboardViewBase locToDraw = listToDraw[iObj];
                int[] locIndicies;
                VertexPositionColorTexture[] objVerts = BillboardViewBase.GetRenderableVerticies(locToDraw.BackgroundVerts, locToDraw.HSLColor, out locIndicies);

                if (objVerts == null)
                    continue;

                Array.Copy(objVerts, 0, VertArray, iNextVert, objVerts.Length);

                for (int iVert = 0; iVert < locIndicies.Length; iVert++)
                {
                    indicies[iNextVertIndex + iVert] = locIndicies[iVert] + iNextVert;
                }

                iNextVert += objVerts.Length;
                iNextVertIndex += locIndicies.Length;
            }

            return VertArray;
        }

    }
}
