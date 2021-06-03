using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using VikingXNA;

namespace VikingXNAGraphics
{
    public abstract class BillboardViewBase : IColorView, IViewPosition2D, IRenderable, IViewBoundingRect
    {
        public abstract IShape2D Shape { get; }


        /// <summary>
        /// Positions the billboard in the world
        /// </summary>
        protected Matrix ModelMatrix = Matrix.Identity;

        protected virtual void UpdateModelMatrix()
        {
            this.ModelMatrix = Matrix.CreateScale((float)Shape.BoundingBox.Width/2,
                                                  (float)Shape.BoundingBox.Height/2,
                                                  1f) * Matrix.CreateTranslation(Shape.BoundingBox.Center.ToXNAVector3(0));
        }

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


        public abstract GridVector2 Position { get; set; }
        public abstract GridRectangle BoundingRect { get; set; }

        /// <summary>
        /// Called when the position or color of the view change
        /// </summary>
        protected virtual void ClearCachedData()
        {
            UpdateModelMatrix();
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

        public static void SetupGraphicsDevice(GraphicsDevice device, BasicEffect basicEffect, OverlayShaderEffect overlayEffect)
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

        public abstract void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items);
        public abstract void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay);
    }
}
