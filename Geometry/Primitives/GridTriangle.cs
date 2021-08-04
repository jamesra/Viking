using Geometry.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
    public struct GridTriangle : ICloneable, IShape2D, ITriangle2D, IEquatable<GridTriangle>, IEquatable<ITriangle2D>
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
            _Segments = null;
        }

        public GridTriangle(IReadOnlyList<GridVector2> points)
            : this(points[0], points[1], points[2])
        {
            if (points.Count != 3)
                throw new ArgumentException("GridTriangle must have three points in array");
        }

        public override bool Equals(object obj)
        {  
            if (obj is GridTriangle otherTri)
                return this == otherTri;
            if (obj is IShape2D otherShape)
                return Equals(otherShape);

            return false;
        }

        public bool Equals(IShape2D obj)
        {  
            if (obj is ITriangle2D otherTri)
            {
                for (int i = 0; i < Points.Length; i++)
                {
                    bool equal = Points[i].Equals(otherTri.Points[i]);
                    if (!equal) return false;
                }

                return true;
            }

            return false;
        }

        public bool Equals(ITriangle2D other)
        {
            if (object.ReferenceEquals(other, null)) return false;

            for (int i = 0; i < Points.Length; i++)
            {
                bool equal = Points[i].Equals(other.Points[i]);
                if (!equal) return false;
            }

            return true; 
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
            get {

                if (!_BarycentricCoefficients.HasValue)
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
            _Segments = null;

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

            //if (this.Area < Global.EpsilonSquared)
            //    throw new ArgumentException("This is not a triangle, it is a line");
        }

        public GridVector2 Centroid
        {
            get{
                return GridVector2.FromBarycentric(p1, p2, p3, 1 / 3.0, 1 / 3.0); 
            }
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
                double a = GridVector2.Distance(p1, p2);
                double b = GridVector2.Distance(p2, p3);
                double c = GridVector2.Distance(p3, p1);
                double[] lengths = { a, b, c };
                double s = (a + b + c) / 2.0;
                double area = Math.Sqrt(s * (s - a) * (s - b) * (s - c));
                return area;
                //return Math.Abs(this.VectorProducts) / 2.0;
            }
        }
        
        public double[] Angles
        {
            get
            {
                double[] angles = new double[3];

                //c^2 = a^2 + b^2 - 2ab cos(theta)
                double a = GridVector2.Distance(p1, p2);
                double b = GridVector2.Distance(p2, p3);
                double c = GridVector2.Distance(p3, p1);

                double asqr = Math.Pow(a, 2);
                double bsqr = Math.Pow(b, 2);
                double csqr = Math.Pow(c, 2);

                angles[0] = Math.Acos((asqr + bsqr - csqr) / (2 * a * b));
                angles[1] = Math.Acos((bsqr + csqr - asqr) / (2 * b * c));
                angles[2] = Math.Acos((csqr + asqr - bsqr) / (2 * c * a));

                return angles; 
            }
        }
        
        /*
        public double[] Angles
        {
            get
            {
                double[] Angles = new double[3];

                Angles[0] = Math.Abs(GridVector2.ArcAngle(p1, p2, p3));
                Angles[1] = Math.Abs(GridVector2.ArcAngle(p2, p1, p3));
                Angles[2] = Math.Abs(GridVector2.ArcAngle(p3, p1, p2));

                if (Angles[0] > Math.PI)
                    Angles[0] = (Math.PI * 2) - Angles[0];

                if (Angles[1] > Math.PI)
                    Angles[1] = (Math.PI * 2) - Angles[1];

                if (Angles[2] > Math.PI)
                    Angles[2] = (Math.PI * 2) - Angles[2];

                Debug.Assert(Math.Round(Angles.Sum(), 6) == Math.Round(Math.PI, 6));

                return Angles;
            }
        }
        */

        /// <summary>
        /// Returns true if the Point is inside the triangle
        /// </summary>
        /// <param name="test"></param>
        /// <returns></returns>
        public bool Contains(IPoint2D point)
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
        IPoint2D[] ITriangle2D.Points
        {
            get
            {
                return new IPoint2D[] { p1, p2, p3 };
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

        public GridVector2 Barycentric(IPoint2D p)
        {
            return Barycentric(p.Convert());
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

        public GridVector2 BaryToVector(GridVector2 bary)
        {
            return GridVector2.FromBarycentric(p1, p2, p3, bary.X, bary.Y);
        }
        
        public bool Intersects(IShape2D shape)
        {
            return ShapeExtensions.TriangleIntersects(this, shape);
        }

        public bool Intersects(ICircle2D c)
        {
            GridCircle circle = c.Convert();
            return this.Intersects(circle);
        }

        public bool Intersects(GridCircle circle)
        {
            return TriangleIntersectionExtensions.Intersects(this, circle);
        }


        public bool Intersects(ILineSegment2D l)
        {
            GridLineSegment line = l.Convert();
            return this.Intersects(line);
        }

        public bool Intersects(GridLineSegment line)
        {
            return TriangleIntersectionExtensions.Intersects(this, line);
        }

        public bool Intersects(ITriangle2D t)
        {
            GridTriangle tri = t.Convert();
            return this.Intersects(tri);
        }

        public bool Intersects(GridTriangle other)
        {
            if (false == other.BoundingBox.Intersects(this.BoundingBox))
                return false;

            foreach (GridVector2 p in this.Points)
            {
                if (other.Contains(p))
                    return true;
            }

            foreach (GridVector2 p in other.Points)
            {
                if (this.Contains(p))
                    return true;
            }

            return false;
        }

        public bool Intersects(IPolygon2D p)
        {
            GridPolygon poly = p.Convert();
            return this.Intersects(poly);
        }

        public bool Intersects(GridPolygon poly)
        {
            return TriangleIntersectionExtensions.Intersects(this, poly);
        }

        public IShape2D Translate(IPoint2D offset)
        {
            GridVector2 vector = offset.Convert();
            return new GridTriangle(this.Points.Select(p => p + vector).ToArray());
        }

        public RotationDirection Winding
        {
            get
            {
                double result = (_p2.Y - _p1.Y) * (_p3.X - _p2.X) -
                                (_p2.X - _p1.X) * (_p3.Y - _p2.Y);

                if (result == 0)
                    return RotationDirection.COLINEAR;

                return result > 0 ? RotationDirection.CLOCKWISE : RotationDirection.COUNTERCLOCKWISE;
            }
        }

        public static RotationDirection GetWinding(GridVector2 _p1, GridVector2 _p2, GridVector2 _p3)
        {
            double result = (_p2.Y - _p1.Y) * (_p3.X - _p2.X) -
                            (_p2.X - _p1.X) * (_p3.Y - _p2.Y);

            if (result == 0)
                return RotationDirection.COLINEAR;

            return result > 0 ? RotationDirection.CLOCKWISE : RotationDirection.COUNTERCLOCKWISE;
        }

        public static RotationDirection GetWinding(GridVector2[] pts)
        {
            if (pts.Length > 3)
                throw new ArgumentException("GridTriangle winding expects less than three points.");


            return GridTriangle.GetWinding(pts[0], pts[1], pts[2]);
        }

        bool IEquatable<GridTriangle>.Equals(GridTriangle other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            return this.p1 == other.p1 &&
                   this.p2 == other.p2 &&
                   this.p3 == other.p3;
        }

        public ShapeType2D ShapeType
        {
            get { return ShapeType2D.TRIANGLE; }
        } 

        private GridLineSegment[] _Segments;
        public GridLineSegment[] Segments
        {
            get
            {
                if(_Segments == null)
                {
                    _Segments = new GridLineSegment[] { new GridLineSegment(p1,p2),
                                                        new GridLineSegment(p2,p3),
                                                        new GridLineSegment(p3,p1)};
                }
                return _Segments;
            }
        }
    }
}
