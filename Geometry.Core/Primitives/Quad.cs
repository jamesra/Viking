using System;
using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// Two triangles that make a rectangle
    /// </summary>
    /// 
    [Serializable]
    public readonly struct Quad : IShape2D, IHasControlPoints, IEquatable<Quad>
    {
        readonly Triangle T0;
        readonly Triangle T1;

        public Quad(Vector2 pos, double Width, double Height)
            : this(pos, new Vector2(pos.X + Width, pos.Y), new Vector2(pos.X, pos.Y + Height), new Vector2(pos.X + Width, pos.Y + Height))
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p1">BottomLeft</param>
        /// <param name="p2">BottomRight</param>
        /// <param name="p3">TopLeft</param>
        /// <param name="p4">TopRight</param>
        /// <param name="color"></param>
        public Quad(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            T0 = new Triangle(p1, p2, p3);
            T1 = new Triangle(p2, p4, p3);
        }

        public Quad(Rectangle rect)
        {
            T0 = new Triangle(rect.LowerLeft, rect.LowerRight, rect.UpperLeft);
            T1 = new Triangle(rect.LowerRight, rect.UpperRight, rect.UpperLeft);
        }


        public Vector2 Center => new LineSegment(T0.P2, T0.P3).Bisect();

        public Vector2 BottomLeft => T0.P1;

        /*set {
                T0 = new Triangle(value, T0.P2, T0.P3); 
            }*/
        public Vector2 BottomRight => T0.P2;

        /*set {
                T0 = new Triangle(T0.P1, value, T0.P3);
                T1 = new Triangle(value, T1.P2, T1.P3); 
            }*/
        public Vector2 TopLeft => T0.P3;

        /*set {
                T0 = new Triangle(T0.P1, T0.P3, value);
                T1 = new Triangle(T1.P1, T1.P2, value); 
            }*/
        public Vector2 TopRight => T1.P2;
        /*set {
                T1 = new Triangle(T1.P1,  value, T1.P3); 
            }*/
        /*
        public void Scale(double scalar)
        {
            //Have to cache center because it changes as we update points
            Vector2 center = this.Center;
            Vector2 directionA = this.TopRight - center;
            Vector2 directionB = this.TopLeft - center; 

            directionA *= scalar;
            directionB *= scalar;

            this.BottomLeft = center - directionA;
            this.TopRight = center + directionA;

            this.BottomRight = center - directionB;
            this.TopLeft = center + directionB;
        }
        */

        public bool Contains(in Vector2 p) => GetRelation((IPoint2D)p).IsContains();

        public bool Covers(in Vector2 p) => GetRelation((IPoint2D)p).IsCovers();

        public bool Contains(in Rectangle R)
        {
            if (false == (T0.Intersects(R) || T1.Intersects(R)))
                return false;

            return Contains(new Quad(new Vector2(R.Left, R.Bottom), R.Width, R.Height));
        }

        public bool Contains(in Quad R)
        {
            Vector2 v1 = R.BottomLeft;
            Vector2 v2 = R.BottomRight;
            Vector2 v3 = R.TopRight;
            Vector2 v4 = R.TopLeft;

            //If any verticies are in the quad we return true 
            if (T0.Covers(v1) || T0.Covers(v2) || T0.Covers(v3) || T0.Covers(v4) ||
                T1.Covers(v1) || T1.Covers(v2) || T1.Covers(v3) || T1.Covers(v4))
                return true;

            if (R.T0.Covers(TopLeft) || R.T0.Covers(TopRight) || R.T0.Covers(BottomLeft) || R.T0.Covers(BottomRight) ||
                R.T1.Covers(TopLeft) || R.T1.Covers(TopRight) || R.T1.Covers(BottomLeft) || R.T1.Covers(BottomRight))
                return true;

            LineSegment RL1 = new(v1, v2);
            LineSegment RL2 = new(v2, v3);
            LineSegment RL3 = new(v3, v4);
            LineSegment RL4 = new(v4, v1);

            LineSegment L1 = new(this.BottomLeft, this.BottomRight);
            LineSegment L2 = new(this.BottomRight, this.TopRight);
            LineSegment L3 = new(this.TopRight, this.TopLeft);
            LineSegment L4 = new(this.TopLeft, this.BottomLeft);

            LineSegment[] RA = [RL1, RL2, RL3, RL4];
            LineSegment[] A = [L1, L2, L3, L4];

            foreach (LineSegment RL in RA)
            {
                foreach (LineSegment L in A)
                {
                    if (RL.Intersects(L, out Vector2 outparam))
                        return true;
                }
            }

            return false;
        }

        public double Area => T0.Area + T1.Area;

        public Rectangle BoundingBox => Rectangle.Union(T0.BoundingBox, T1.BoundingBox);

        public ShapeType2D ShapeType => ShapeType2D.Quad;

        IReadOnlyList<IPoint2D> IHasControlPoints.ControlPoints => [BottomLeft, BottomRight, TopRight, TopLeft];

        public bool Contains(in IPoint2D p) => GetRelation(p).IsContains();

        public bool Covers(in IPoint2D p) => GetRelation(p).IsCovers();

        public ShapeRelation GetRelation(in IPoint2D p)
        {
            Vector2 v = new(p.X, p.Y);
            ShapeRelation t0 = T0.GetRelation((IPoint2D)v);
            ShapeRelation t1 = T1.GetRelation((IPoint2D)v);
            if (t0 == ShapeRelation.None && t1 == ShapeRelation.None)
                return ShapeRelation.None;

            LineSegment[] outer =
            [
                new(BottomLeft, BottomRight),
                new(BottomRight, TopRight),
                new(TopRight, TopLeft),
                new(TopLeft, BottomLeft)
            ];
            foreach (LineSegment edge in outer)
            {
                if (edge.Covers(v))
                    return ShapeRelation.Touching;
            }

            return ShapeRelation.Contained;
        }

        public ShapeRelation GetRelation(in ILineSegment2D line)
        {
            LineSegment seg = line.Convert();
            ShapeRelation a = GetRelation((IPoint2D)seg.A);
            ShapeRelation b = GetRelation((IPoint2D)seg.B);
            if (a != ShapeRelation.None && b != ShapeRelation.None)
                return a == ShapeRelation.Touching && b == ShapeRelation.Touching ? ShapeRelation.Contained : ShapeRelation.Contained;
            if (T0.Intersects(seg) || T1.Intersects(seg))
                return ShapeRelation.Intersecting;
            return ShapeRelation.None;
        }

        public bool Intersects(in IShape2D shape) => T0.Intersects(shape) || T1.Intersects(shape);

        public IShape2D Translate(in IPoint2D offset)
        {
            Vector2 o = offset.Convert();
            return new Quad(BottomLeft + o, BottomRight + o, TopLeft + o, TopRight + o);
        }

        public bool Equals(IShape2D other) => other is Quad quad && Equals(quad);

        public bool Equals(Quad other) => T0.Equals(other.T0) && T1.Equals(other.T1);

        public override bool Equals(object obj) => obj is Quad quad && Equals(quad);

        public override int GetHashCode() => GeometryHashCode.Combine(T0.GetHashCode(), T1.GetHashCode());

        public static bool operator ==(in Quad a, in Quad b) => a.Equals(b);

        public static bool operator !=(in Quad a, in Quad b) => !a.Equals(b);
    }
}
