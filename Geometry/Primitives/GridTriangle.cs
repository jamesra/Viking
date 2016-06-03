using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Geometry.Graphics; 

namespace Geometry
{
    [Serializable]
    struct BaryCoefs
    {
        public readonly GridVector2 vCA;
        public readonly GridVector2 vBA;

        public readonly double dotCACA;
        public readonly double dotCABA;
        public readonly double dotBABA;

        public readonly double invDenom;

        public BaryCoefs(GridVector2 _p1, GridVector2 _p2, GridVector2 _p3)
        {
            vCA = _p3 - _p1;
            vBA = _p2 - _p1;

            dotCACA = GridVector2.Dot(vCA, vCA);
            dotCABA = GridVector2.Dot(vCA, vBA);
            dotBABA = GridVector2.Dot(vBA, vBA);

            invDenom = 1.0 / ((dotCACA * dotBABA) - (dotCABA * dotCABA));

#if DEBUG
            this.p1 = _p1;
            this.p2 = _p2;
            this.p3 = _p3; 
#endif 
        }

#if DEBUG
        //Record the values used to generate the coefficients to make sure they aren't being changed out from under us
        public readonly GridVector2 p1;
        public readonly GridVector2 p2;
        public readonly GridVector2 p3;

        public void Validate(GridTriangle T)
        {
            Debug.Assert(this.p1 == T.p1);
            Debug.Assert(this.p2 == T.p2);
            Debug.Assert(this.p3 == T.p3);
        }
#endif

        public BaryCoefs(GridTriangle T) : this(T.p1, T.p2, T.p3)
        {}  
    }

    /// <summary>
    /// Grid triangle uses pointers to nodes in the grid.  This means any alteration to nodes automatically affects the triangle
    /// </summary>
    /// 
    [Serializable]
    public struct GridTriangle : ICloneable
    {
        readonly GridVector2 _p1;
        readonly GridVector2 _p2;
        readonly GridVector2 _p3;

        Color color; 
        /*
        public GridTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
            : this(new GridVector2(p1.X, p1.Y), new GridVector2(p2.X, p2.Y), new GridVector2(p3.X, p3.Y), Color.Blue)
        {

        }
        */
        public GridTriangle(GridVector2 p1, GridVector2 p2, GridVector2 p3)
            : this(p1, p2, p3, Color.Blue)
        {
            _HashCode = new int?();   
        }

        public override bool Equals(object obj)
        {
            return (GridTriangle)obj == this; 
        }

        int? _HashCode;

        public override int GetHashCode()
        {
            if (!_HashCode.HasValue)
            {
                _HashCode = this._p1.GetHashCode();
            }

            return _HashCode.Value;
        }

        public static bool operator ==(GridTriangle A, GridTriangle B)
        {
            return ((A._p1 == B._p1) &&
                   (A._p2 == B._p2) &&
                   (A._p3 == B._p3));
        }

        public static bool operator !=(GridTriangle A, GridTriangle B)
        {
            return !(A == B);
        }

        BaryCoefs? _BarycentricCoefficients;
        private BaryCoefs BarycentricCoefficients
        {
            get{
                
                if(!_BarycentricCoefficients.HasValue)
                {
                    _BarycentricCoefficients = new BaryCoefs(this); 
                }
#if DEBUG
                else
                {
                    //Make sure the verticies have not been changed
                    _BarycentricCoefficients.Value.Validate(this); 
                }
#endif 
                return _BarycentricCoefficients.Value; 
            }
        }

        /// <summary>
        /// Records whether we need to calculate barycentric coordinates before use
        /// Should be called if grid coordinates change
        /// </summary>
        public void ClearBarycentric()
        {
            _BarycentricCoefficients = null; 
        }

        public GridTriangle(GridVector2 p1, GridVector2 p2, GridVector2 p3, Color color)
        {
            _BarycentricCoefficients = null;

            if (GridVector2.Distance(p1, p2) <= Global.Epsilon ||
               GridVector2.Distance(p2, p3) <= Global.Epsilon ||
               GridVector2.Distance(p3, p1) <= Global.Epsilon)
            {
                
                throw new ArgumentException("This is not a triangle, it is a line");
            }

            this._p1 = p1; 
            this._p2 = p2;
            this._p3 = p3;
            /*
            this._p1.X = Math.Round(this._p1.X, 2);
            this._p2.X = Math.Round(this._p2.X, 2);
            this._p3.X = Math.Round(this._p3.X, 2);
            this._p1.Y = Math.Round(this._p1.Y, 2);
            this._p2.Y = Math.Round(this._p2.Y, 2);
            this._p3.Y = Math.Round(this._p3.Y, 2);

             */
            this.color = color; 

          //  _v1 = 
          //  _v2 = new VertexPositionColor(new Vector3(_p2, ZHeight), color);
          //  _v3 = new VertexPositionColor(new Vector3(_p3, ZHeight), color);

            _HashCode = new int?();
        }

        public GridCircle Circle
        {
            get
            {
                return GridCircle.CircleFromThreePoints(new GridVector2[] { _p1, _p2, _p3 });
            }
        }

        object ICloneable.Clone()
        {
            return this.MemberwiseClone();
        }

        public GridVector2 p1
        {
            get { return _p1; }
           
        }

        public GridVector2 p2
        {
            get { return _p2; }
            
        }

        public GridVector2 p3
        {
            get { return _p3; }
            
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
        public bool Intersects(GridVector2 point)
        {                
            GridVector2 uv = Barycentric(point);

            if (uv.X >= 0 && uv.Y >= 0)
            {
                if (uv.X + uv.Y <= 1.0f)
                    return true; 
            }

            return false; 
        }

        public GridVector2[] Points
        {
            get
            {
                return new GridVector2[] { p1, p2, p3 };
            }
        }

        public GridRectangle BoundingBox
        {
            get
            {
                GridVector2[] points = this.Points;
                double minx = points.Min(p => p.X);
                double maxx = points.Max(p => p.X);
                double miny = points.Min(p => p.Y);
                double maxy = points.Max(p => p.Y);
                return new GridRectangle(minx, maxx, miny, maxy);
            }
        }

        /// <summary>
        /// Returns u,v coordinate of point in triangle.  Calculates areas and returns fractions of area.  This can return 0,0 if the point is well outside the 
        /// triangle because the math hits the limit of the double data-type
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public GridVector2 Barycentric(GridVector2 point)
        {
            BaryCoefs CachedCoefs = this.BarycentricCoefficients;
            GridVector2 vPA = point - _p1;

            double dotCAPA = GridVector2.Dot(CachedCoefs.vCA, vPA);
            double dotBAPA = GridVector2.Dot(CachedCoefs.vBA, vPA);

            double u = ((CachedCoefs.dotBABA * dotCAPA) - (CachedCoefs.dotCABA * dotBAPA)) * CachedCoefs.invDenom;
            double v = ((CachedCoefs.dotCACA * dotBAPA) - (CachedCoefs.dotCABA * dotCAPA)) * CachedCoefs.invDenom;

            GridVector2 uv = new GridVector2(u, v);

            //There is always a little floating point error, so cut some slack
            if (uv.X < 0 && uv.X >= -Global.Epsilon)
                uv.X = 0.0f;

            if (uv.Y < 0 && uv.Y >= -Global.Epsilon)
                uv.Y = 0.0f;

            if (uv.X > 0 && uv.Y > 0 && uv.X + uv.Y > 1.0f && uv.X + uv.Y <= 1.0f + Global.Epsilon)
            {
                //TODO: This correction could probably by more precise
                double diff = ((u + v) - 1) + (Global.Epsilon / 100);
                uv.X -= diff * u;
                uv.Y -= diff * v;

                Debug.Assert(uv.X + uv.Y <= 1.0f, "Failed to correct for u+v near 1.0f + epsilon case in barycentric conversion"); 
            }

            return uv;
        }
    }
}
