using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Util
{
    public class MappingTriangle : ICloneable
    {
        public Triangle Control;
        public Triangle Mapped;
        static VertexDeclaration vertDeclare = null;

        public MappingTriangle(Triangle control, Triangle mapped)
        {
            this.Control = control;
            this.Mapped = mapped;

            if (mapped.Color == control.Color)
            {
                Mapped.Color = Color.Magenta;
                Control.Color = new Color(0, 255, 0); 
            }
        }

        public MappingTriangle Copy()
        {
            return ((ICloneable)this).Clone() as MappingTriangle;
        }

        object ICloneable.Clone()
        {
            return this.MemberwiseClone(); 
        }

        public bool IntersectsMapped(Vector2 Point)
        {
            return Mapped.Intersects(Point);
        }

        public bool IntersectsControl(Vector2 Point)
        {
            return Mapped.Intersects(Point); 
        }

        public Vector2 Transform(Vector2 Point)
        {
            Vector2 uv = Mapped.Barycentric(Point);
            Debug.Assert(uv.X >= 0.0 && uv.Y >= 0.0 && (uv.X + uv.Y <= 1.0));


            Vector3 translated = Vector3.Barycentric(new Vector3(Control.p1, 0), new Vector3(Control.p2, 0), new Vector3(Control.p3, 0), uv.Y, uv.X);
            return new Vector2(translated.X, translated.Y);

        }
        public Vector2 InverseTransform(Vector2 Point)
        {
            Vector2 uv = Control.Barycentric(Point);
  //          Debug.Assert(uv.X >= 0.0 && uv.Y >= 0.0 && (uv.X + uv.Y <= 1.0));

            Vector3 translated = Vector3.Barycentric(new Vector3(Mapped.p1, 0), new Vector3(Mapped.p2, 0), new Vector3(Mapped.p3, 0), uv.Y, uv.X);
            return new Vector2(translated.X, translated.Y);
        }

        public void Draw(GraphicsDevice graphicsDevice)
        {
            if(MappingTriangle.vertDeclare == null)
                vertDeclare = new VertexDeclaration(graphicsDevice, VertexPositionColor.VertexElements);
            
            graphicsDevice.VertexDeclaration = vertDeclare;

     //       Mapped.Draw(graphicsDevice);
            Control.Draw(graphicsDevice); 
        }
    }
}
