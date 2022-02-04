using Geometry.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    [Serializable]
    readonly struct BaryCoefs
    {
        public readonly GridVector2 vCA;
        public readonly GridVector2 vBA;

        public readonly double dotCACA;
        public readonly double dotCABA;
        public readonly double dotBABA;

        public readonly double invDenom;

        public BaryCoefs(in GridVector2 _p1, in GridVector2 _p2, in GridVector2 _p3)
        {
            vCA = _p3 - _p1;
            vBA = _p2 - _p1;

            dotCACA = GridVector2.Dot(in vCA, in vCA);
            dotCABA = GridVector2.Dot(in vCA, in vBA);
            dotBABA = GridVector2.Dot(in vBA, in vBA);

            invDenom = 1.0 / ((dotCACA * dotBABA) - (dotCABA * dotCABA)); 
        }
          
        public BaryCoefs(in GridTriangle T) : this(T.p1, T.p2, T.p3)
        {}  
    }

    /// <summary>
    /// Grid triangle uses pointers to nodes in the grid.  This means any alteration to nodes automatically affects the triangle
    /// </summary>
    /// 
    [Serializable]
    public readonly struct GridTriangle : ICloneable, IShape2D, ITriangle2D, IEquatable<GridTriangle>, IEquatable<ITriangle2D>
    {
        public readonly GridVector2[] Points;
        IPoint2D[] ITriangle2D.Points => this.Points.Cast<IPoint2D>().ToArray();
         
        private GridVector2 _p1 => Points[0];
        private GridVector2 _p2 => Points[1];
        private GridVector2 _p3 => Points[2];

        public readonly GridRectangle BoundingBox;
        GridRectangle IShape2D.BoundingBox => this.BoundingBox;
        
        public readonly GridLineSegment[] Segments;
          
        private readonly BaryCoefs _BarycentricCoefficients;

        public GridTriangle(IReadOnlyList<GridVector2> points)
            : this(points[0], points[1], points[2])
        {
            if (points.Count != 3)
                throw new ArgumentException("GridTriangle must have three points in array");
        }

        public GridTriangle(in GridVector2 p1, in GridVector2 p2, in GridVector2 p3)
        {
            if (GridVector2.DistanceSquared(p1, p2) <= Global.EpsilonSquared ||
                GridVector2.DistanceSquared(p2, p3) <= Global.EpsilonSquared ||
                GridVector2.DistanceSquared(p3, p1) <= Global.EpsilonSquared)
            {
                throw new ArgumentException("This is not a triangle, it is a line");
            }
            
            Points = new GridVector2[] { p1, p2, p3 };

            BoundingBox = Points.BoundingBox();
              
            Segments = new GridLineSegment[] { new GridLineSegment(p1,p2),
                                                new GridLineSegment(p2,p3),
                                                new GridLineSegment(p3,p1)};

            _BarycentricCoefficients = new BaryCoefs(p1,p2,p3);
            /*
            this._p1.X = Math.Round(this._p1.X, 2);
            this._p2.X = Math.Round(this._p2.X, 2);
            this._p3.X = Math.Round(this._p3.X, 2);
            this._p1.Y = Math.Round(this._p1.Y, 2);
            this._p2.Y = Math.Round(this._p2.Y, 2);
            this._p3.Y = Math.Round(this._p3.Y, 2);
            */

            //if (this.Area < Global.EpsilonSquared)
            //    throw new ArgumentException("This is not a triangle, it is a line");
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
            if (other is null) return false;

            for (int i = 0; i < Points.Length; i++)
            {
                bool equal = Points[i].Equals(other.Points[i]);
                if (!equal) return false;
            }

            return true; 
        }


        public override int GetHashCode() => throw new InvalidOperationException("It is not possible to generate a hashcode for points when using an epsilon value, see GridVector2.GetHashCode");

        public static bool operator ==(in GridTriangle A, in GridTriangle B)
        {
            return ((A._p1 == B._p1) &&
                   (A._p2 == B._p2) &&
                   (A._p3 == B._p3));
        }

        public static bool operator !=(in GridTriangle A, in GridTriangle B)
        {
            return !(A == B);
        }
         
        public GridVector2 Centroid => GridVector2.FromBarycentric(p1, p2, p3, 1 / 3.0, 1 / 3.0);

        IPoint2D ICentroid.Centroid => Centroid;

        public GridCircle Circle => GridCircle.CircleFromThreePoints(new GridVector2[] { _p1, _p2, _p3 });

        object ICloneable.Clone()
        {
            return this.MemberwiseClone();
        }

        public GridVector2 p1 => _p1;

        public GridVector2 p2 => _p2;

        public GridVector2 p3 => _p3;
         
        //public double VectorProducts => (_p1.X * (_p2.Y - _p3.Y)) + (_p2.X * (_p3.Y - _p1.Y)) + (_p3.X * (_p1.Y - _p1.Y));

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
        public bool Contains(in IPoint2D point)
        {
            if (false == BoundingBox.Contains(point))
            {
                //False positives can happen in cases where the points have floating point precision issues.
                //Particularly in GridTransforms.  This should be handled by rounding the transform results. 
                //However it may be worth the computation cost to do Barycentric calculation instead.
                return false;
            }

            GridVector2 uv = Barycentric(point);

            if (uv.X >= 0 && uv.Y >= 0)
            {
                if (uv.X + uv.Y <= 1.0f)
                    return true; 
            }

            return false; 
        } 

        public GridVector2 Barycentric(in IPoint2D p)
        {
            return Barycentric(p.Convert());
        }

        /// <summary>
        /// Returns u,v coordinate of point in triangle.  Calculates areas and returns fractions of area.  This can return 0,0 if the point is well outside the 
        /// triangle because the math hits the limit of the double data-type
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public GridVector2 Barycentric(in GridVector2 point)
        { 
            GridVector2 vPA = point - _p1;

            double dotCAPA = GridVector2.Dot(_BarycentricCoefficients.vCA, vPA);
            double dotBAPA = GridVector2.Dot(_BarycentricCoefficients.vBA, vPA);

            double u = ((_BarycentricCoefficients.dotBABA * dotCAPA) - (_BarycentricCoefficients.dotCABA * dotBAPA)) * _BarycentricCoefficients.invDenom;
            double v = ((_BarycentricCoefficients.dotCACA * dotBAPA) - (_BarycentricCoefficients.dotCABA * dotCAPA)) * _BarycentricCoefficients.invDenom;

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

        public GridVector2 BaryToVector(in GridVector2 bary)
        {
            return GridVector2.FromBarycentric(p1, p2, p3, bary.X, bary.Y);
        }
        
        public bool Intersects(in IShape2D shape)
        {
            return ShapeExtensions.TriangleIntersects(this, in shape);
        }

        public bool Intersects(in GridRectangle r) => RectangleIntersectionExtensions.Intersects(r, this);

        public bool Intersects(in ICircle2D c) => Intersects(c.Convert());

        public bool Intersects(in GridCircle circle) => TriangleIntersectionExtensions.Intersects(this, circle);
         
        public bool Intersects(in ILineSegment2D l) => Intersects(l.Convert());

        public bool Intersects(in GridLineSegment line) => TriangleIntersectionExtensions.Intersects(this, line);

        public bool Intersects(in ITriangle2D t) => Intersects(t.Convert());
        
        public bool Intersects(in GridTriangle other)
        {
            if (false == other.BoundingBox.Intersects( BoundingBox))
                return false;

            foreach (GridVector2 p in Points)
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

        public bool Intersects(in IPolygon2D p) => Intersects(p.Convert());
        
        public bool Intersects(in GridPolygon poly) => TriangleIntersectionExtensions.Intersects(this, poly);

        public IShape2D Translate(in IPoint2D offset)
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
            return this.p1 == other.p1 &&
                   this.p2 == other.p2 &&
                   this.p3 == other.p3;
        }

        public ShapeType2D ShapeType => ShapeType2D.TRIANGLE; 
    }
}
