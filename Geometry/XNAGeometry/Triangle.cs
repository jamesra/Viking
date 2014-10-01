using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics; 
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Util
{
    public struct Triangle 
    {
        const float ZHeight = 1.0f;

        Vector2 _p1;
        Vector2 _p2;
        Vector2 _p3;

        Color color; 

        VertexPositionColor _v1
        {
            get
            {
                return new VertexPositionColor(new Vector3(_p1, ZHeight), color);
            }
        }

        VertexPositionColor _v2
        {
            get
            {
                return new VertexPositionColor(new Vector3(_p2, ZHeight), color);
            }
        }

        VertexPositionColor _v3
        {
            get
            {
                return new VertexPositionColor(new Vector3(_p3, ZHeight), color);
            }
        }

        public Triangle(Vector3 p1, Vector3 p2, Vector3 p3)
            : this(new Vector2(p1.X,p1.Y), new Vector2(p2.X,p2.Y), new Vector2(p3.X,p3.Y), Color.Blue)
        {

        }

        public Triangle(Vector2 p1, Vector2 p2, Vector2 p3) : this(p1,p2,p3, Color.Blue)
        {
            
        }

        public Triangle(Vector2 p1, Vector2 p2, Vector2 p3, Color color)
        {
            if (Vector2.Distance(p1, p2) <= Global.epsilon ||
               Vector2.Distance(p2, p3) <= Global.epsilon ||
               Vector2.Distance(p3, p1) <= Global.epsilon)
            {
                
                throw new ArgumentException("This is not a triangle, it is a line");
            }

            this._p1 = p1; 
            this._p2 = p2;
            this._p3 = p3;

            this.color = color; 

          //  _v1 = 
          //  _v2 = new VertexPositionColor(new Vector3(_p2, ZHeight), color);
          //  _v3 = new VertexPositionColor(new Vector3(_p3, ZHeight), color);
        }

        public Vector2 p1
        {
            get { return _p1; }
            set
            {
               if (Vector2.Distance(value, p2) <= Global.epsilon ||
               Vector2.Distance(value, p3) <= Global.epsilon)
                {

                    throw new ArgumentException("This is not a triangle, it is a line");
                }

                _p1 = value;
//                _v1.Position = new Vector3(value, ZHeight);
            }
        }

        public Vector2 p2
        {
            get { return _p2; }
            set
            {
                if (Vector2.Distance(value, p3) <= Global.epsilon ||
               Vector2.Distance(value, p1) <= Global.epsilon)
                {

                    throw new ArgumentException("This is not a triangle, it is a line");
                }
                _p2 = value;
//                _v2.Position = new Vector3(value, ZHeight);
            }
        }

        public Vector2 p3
        {
            get { return _p3; }
            set
            {
               if (Vector2.Distance(value, p1) <= Global.epsilon ||
               Vector2.Distance(value, p2) <= Global.epsilon)
                {

                    throw new ArgumentException("This is not a triangle, it is a line");
                }

                _p3 = value;
//                _v3.Position = new Vector3(value, ZHeight);
            }
        }


        public Color Color
        {
            get { return this.color; }
            set
            {
                this.color = value; 
            }
        }

        public double VectorProducts
        {
            get
            {
                //z = x1 (y2 - y3) + x2 (y3 - y1) + x3 (y1 - y2) 
                double z = (_p1.X * (_p2.Y - _p3.Y)) + (_p2.X * (_p3.Y - _p1.Y)) + (_p3.X * (_p1.Y - _p1.Y));
                return z;
            }
        }

        public double Area
        {
            get{
                return Math.Abs(this.VectorProducts) / 2.0;
            }
        }

        /// <summary>
        /// Returns true if the Point is inside the triangle
        /// </summary>
        /// <param name="test"></param>
        /// <returns></returns>
        public bool Intersects(Vector2 point)
        {
            Vector2 uv = Barycentric(point);

            if (uv.X >= 0 && uv.Y >= 0)
            {
                if (uv.X + uv.Y <= 1.0f)
                    return true; 
            }

            return false; 
        }

        /// <summary>
        /// Returns u,v coordinate of point in triangle.  Calculates areas and returns fractions of area
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public Vector2 Barycentric(Vector2 point)
        {
            Vector2 vCA = _p3 - _p1;
            Vector2 vBA = _p2 - _p1;
            Vector2 vPA = point - _p1;

            double dotCACA = Vector2.Dot(vCA, vCA);
            double dotCABA = Vector2.Dot(vCA, vBA);
            double dotCAPA = Vector2.Dot(vCA, vPA);
            double dotBABA = Vector2.Dot(vBA, vBA);
            double dotBAPA = Vector2.Dot(vBA, vPA);

            double invDenom = 1 / ((dotCACA * dotBABA) - (dotCABA * dotCABA));
            double u = ((dotBABA * dotCAPA) - (dotCABA * dotBAPA)) * invDenom;
            double v = ((dotCACA * dotBAPA) - (dotCABA * dotCAPA)) * invDenom;

            Vector2 uv = new Vector2((float)u, (float)v);

            //There is always a little floating point error, so cut some slack
            if (uv.X < 0 && uv.X >= -Global.epsilon)
                uv.X = 0.0f;

            if (uv.Y < 0 && uv.Y >= -Global.epsilon)
                uv.Y = 0.0f;

            if (uv.X > 0 && uv.Y > 0 && uv.X + uv.Y > 1.0f && uv.X + uv.Y <= 1.0f + Global.epsilon)
            {
                //TODO: This correction could probably by more precise
                double diff = ((u + v) - 1) + (Global.epsilon / 100);
                uv.X -= (float)(diff * u);
                uv.Y -= (float)(diff * v);

                Debug.Assert(uv.X + uv.Y <= 1.0f, "Failed to correct for u+v near 1.0f + epsilon case in barycentric conversion"); 
            }

            return uv;
        }

        /// <summary>
        /// Draw the triangle on a graphics device
        /// </summary>
        /// <param name="graphicsDevice"></param>
        public void Draw(GraphicsDevice graphicsDevice)
        {
            VertexPositionColor[] verts = {_v1,_v2,_v3};
            int[] indicies = { 0, 1, 1, 2, 2, 0};

            graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.LineList, verts, 0, verts.Length, indicies, 0, indicies.Length / 2);
        }
    }
}
