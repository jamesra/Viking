using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    [Serializable]
    readonly struct BaryCoefs
    {
        public readonly Vector2 vCA;
        public readonly Vector2 vBA;

        public readonly double dotCACA;
        public readonly double dotCABA;
        public readonly double dotBABA;

        public readonly double invDenom;

        public BaryCoefs(in Vector2 P1, in Vector2 P2, in Vector2 P3)
        {
            vCA = P3 - P1;
            vBA = P2 - P1;

            dotCACA = Vector2.Dot(in vCA, in vCA);
            dotCABA = Vector2.Dot(in vCA, in vBA);
            dotBABA = Vector2.Dot(in vBA, in vBA);

            invDenom = 1.0 / ((dotCACA * dotBABA) - (dotCABA * dotCABA));
        }

        public BaryCoefs(in Triangle T) : this(T.P1, T.P2, T.P3)
        { }
    }

    /// <summary>
    /// Grid triangle uses pointers to nodes in the grid.  This means any alteration to nodes automatically affects the triangle
    /// </summary>
    /// 
    [Serializable]
    public readonly struct Triangle : ICloneable, IShape2D, ITriangle2D, IEquatable<Triangle>, IEquatable<ITriangle2D>
    {
        readonly Vector2[] _points;

        /// <summary>Copy of the three vertices. Mutating the returned array does not change the triangle.</summary>
        public Vector2[] Points => (Vector2[])_points.Clone();

        IPoint2D[] ITriangle2D.Points => [.. _points.Cast<IPoint2D>()];

        public Vector2 P1 => _points[0];
        public Vector2 P2 => _points[1];
        public Vector2 P3 => _points[2];

        public readonly Rectangle BoundingBox;
        Rectangle IShape2D.BoundingBox => this.BoundingBox;

        public readonly LineSegment[] Segments;

        private readonly BaryCoefs _BarycentricCoefficients;

        public Triangle(IReadOnlyList<Vector2> points)
            : this(points[0], points[1], points[2])
        {
            if (points.Count != 3)
                throw new ArgumentException("Triangle must have three points in array");
        }

        public Triangle(in Vector2 p1, in Vector2 p2, in Vector2 p3)
        {
            if (Vector2.DistanceSquared(p1, p2) <= Tolerance.EpsilonSquared ||
                Vector2.DistanceSquared(p2, p3) <= Tolerance.EpsilonSquared ||
                Vector2.DistanceSquared(p3, p1) <= Tolerance.EpsilonSquared)
            {
                throw new ArgumentException("This is not a triangle, it is a line");
            }

            _points = [p1, p2, p3];

            BoundingBox = _points.BoundingBox();

            Segments = [ new(p1,p2),
                                                new(p2,p3),
                                                new(p3,p1)];

            _BarycentricCoefficients = new BaryCoefs(p1, p2, p3);
            /*
            this.P1.X = Math.Round(this.P1.X, 2);
            this.P2.X = Math.Round(this.P2.X, 2);
            this.P3.X = Math.Round(this.P3.X, 2);
            this.P1.Y = Math.Round(this.P1.Y, 2);
            this.P2.Y = Math.Round(this.P2.Y, 2);
            this.P3.Y = Math.Round(this.P3.Y, 2);
            */

            //if (this.Area < Tolerance.EpsilonSquared)
            //    throw new ArgumentException("This is not a triangle, it is a line");
        }

        public override bool Equals(object obj)
        {
            if (obj is Triangle otherTri)
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


        public override int GetHashCode() =>
            GeometryHashCode.Combine(P1.GetHashCode(), P2.GetHashCode(), P3.GetHashCode());

        public static bool operator ==(in Triangle A, in Triangle B)
        {
            return ((A.P1 == B.P1) &&
                   (A.P2 == B.P2) &&
                   (A.P3 == B.P3));
        }

        public static bool operator !=(in Triangle A, in Triangle B) => !(A == B);

        public Vector2 Centroid => Vector2.FromBarycentric(P1, P2, P3, 1 / 3.0, 1 / 3.0);

        IPoint2D ICentroid.Centroid => Centroid;

        public Circle Circle => Circle.CircleFromThreePoints([P1, P2, P3]);

        object ICloneable.Clone() => this.MemberwiseClone();

        //public double VectorProducts => (P1.X * (P2.Y - P3.Y)) + (P2.X * (P3.Y - P1.Y)) + (P3.X * (P1.Y - P1.Y));

        public double Area
        {
            get
            {
                double a = Vector2.Distance(P1, P2);
                double b = Vector2.Distance(P2, P3);
                double c = Vector2.Distance(P3, P1);
                double[] lengths = [a, b, c];
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
                double a = Vector2.Distance(P1, P2);
                double b = Vector2.Distance(P2, P3);
                double c = Vector2.Distance(P3, P1);

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

                Angles[0] = Math.Abs(Vector2.ArcAngle(p1, p2, p3));
                Angles[1] = Math.Abs(Vector2.ArcAngle(p2, p1, p3));
                Angles[2] = Math.Abs(Vector2.ArcAngle(p3, p1, p2));

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

            Vector2 uv = Barycentric(point);

            if (uv.X >= 0 && uv.Y >= 0)
            {
                if (uv.X + uv.Y <= 1.0f)
                    return true;
            }

            return false;
        }

        public ShapeRelation GetRelation(in IPoint2D p)
        {
            if (false == BoundingBox.Contains(p))
            {
                //False positives can happen in cases where the points have floating point precision issues.
                //Particularly in GridTransforms.  This should be handled by rounding the transform results. 
                //However it may be worth the computation cost to do Barycentric calculation instead.
                return ShapeRelation.None;
            }

            //Find out if the point is on any line segment of the triangle
            Vector2 uv = Barycentric(p);
            Vector3 uvw = new(uv.X, uv.Y, 1 - uv.X - uv.Y);

            if (uvw.X >= 0 && uvw.Y >= 0 && uvw.Z >= 0)
            {
                if (uvw.X + uvw.Y + uvw.Z <= 1.0)
                {
                    //The point is on or inside the triangle if any barycentric coordinate is 0
                    if (uvw.Coords.Any(c => c == 0))
                        return ShapeRelation.Touching;

                    return ShapeRelation.Contained;
                }
            }

            return ShapeRelation.None;
        }

        public ShapeRelation GetRelation(LineSegment line)
        {
            //This is very similar to the logic for Rectangle
            ShapeRelation relA = this.GetRelation(line.A);
            ShapeRelation relB = this.GetRelation(line.B);

            ShapeRelation composite = relA | relB;

            bool containsA = relA == ShapeRelation.Contained;
            bool containsB = relB == ShapeRelation.Contained;

            if (containsA && containsB)
                return ShapeRelation.Contained;

            //Edge case where one end of the line is contained and the other is exactly on the edge
            if (composite.HasFlag(ShapeRelation.Touching | ShapeRelation.Contained))
                return ShapeRelation.Contained;

            //Edge case where the line is exactly along the edge... not sure if this should be touching or contained, if not the same edge it is contained, but if the same edge it is touching
            if (relA.HasFlag(ShapeRelation.Touching) && relB.HasFlag(ShapeRelation.Touching))
            {
                //Check if the line is touching the same segment in two places
                foreach (LineSegment e in this.Segments)
                    if (e.Intersects(line.A) && e.Intersects(line.B))
                        return ShapeRelation.Touching;

                return ShapeRelation.Contained;
            }

            //Check if line crosses the bounding box but both points are outside the box
            foreach (LineSegment e in this.Segments)
                if (e.Intersects(line))
                    return ShapeRelation.Intersecting;

            //OK, make sure one endpoint isn't touching and the rest of the line is outside the triangle
            if (composite.HasFlag(ShapeRelation.Touching))
                return ShapeRelation.Touching;

            return ShapeRelation.None;
        }

        ShapeRelation IShape2D.GetRelation(in Geometry.ILineSegment2D line) => GetRelation(line.Convert());

        public Vector2 Barycentric(in IPoint2D p) => Barycentric(p.Convert());

        /// <summary>
        /// Returns u,v coordinate of point in triangle.  Calculates areas and returns fractions of area.  This can return 0,0 if the point is well outside the 
        /// triangle because the math hits the limit of the double data-type
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public Vector2 Barycentric(in Vector2 point)
        {
            Vector2 vPA = point - P1;

            double dotCAPA = Vector2.Dot(_BarycentricCoefficients.vCA, vPA);
            double dotBAPA = Vector2.Dot(_BarycentricCoefficients.vBA, vPA);

            double u = ((_BarycentricCoefficients.dotBABA * dotCAPA) - (_BarycentricCoefficients.dotCABA * dotBAPA)) * _BarycentricCoefficients.invDenom;
            double v = ((_BarycentricCoefficients.dotCACA * dotBAPA) - (_BarycentricCoefficients.dotCABA * dotCAPA)) * _BarycentricCoefficients.invDenom;

            if (u < 0 && u >= -Tolerance.Epsilon)
                u = 0.0;

            if (v < 0 && v >= -Tolerance.Epsilon)
                v = 0.0;

            if (u > 0 && v > 0 && u + v > 1.0f && u + v <= 1.0f + Tolerance.Epsilon)
            {
                double diff = ((u + v) - 1) + (Tolerance.Epsilon / 100);
                u -= diff * u;
                v -= diff * v;

                Debug.Assert(u + v <= 1.0f, "Failed to correct for u+v near 1.0f + epsilon case in barycentric conversion");
            }

            return new Vector2(u, v);
        }

        /// <summary>
        /// Returns u,v coordinate of point in triangle.  Calculates areas and returns fractions of area.  This can return 0,0 if the point is well outside the 
        /// triangle because the math hits the limit of the double data-type
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public Vector2[] Barycentric(in Vector2[] points)
        {
            Vector2[] uv = new Vector2[points.Length];
            for (int i = 0; i < uv.Length; i++)
            {
                uv[i] = Barycentric(points[i]);
            }

            return uv;
        }

        public Vector2 BaryToVector(in Vector2 bary) => Vector2.FromBarycentric(P1, P2, P3, bary.X, bary.Y);

        public bool Intersects(in IShape2D shape) => ShapeExtensions.TriangleIntersects(this, in shape);

        public bool Intersects(in Rectangle r) => RectangleIntersectionExtensions.Intersects(r, this);

        public bool Intersects(in ICircle2D c) => Intersects(c.Convert());

        public bool Intersects(in Circle circle) => TriangleIntersectionExtensions.Intersects(this, circle);

        public bool Intersects(in ILineSegment2D l) => Intersects(l.Convert());

        public bool Intersects(in LineSegment line) => TriangleIntersectionExtensions.Intersects(this, line);

        public bool Intersects(in ITriangle2D t) => Intersects(t.Convert());

        public bool Intersects(in Triangle other)
        {
            if (false == other.BoundingBox.Intersects(BoundingBox))
                return false;

            foreach (Vector2 p in Points)
            {
                if (other.Contains(p))
                    return true;
            }

            foreach (Vector2 p in other.Points)
            {
                if (this.Contains(p))
                    return true;
            }

            foreach (LineSegment edge in Segments)
            {
                foreach (LineSegment otherEdge in other.Segments)
                {
                    if (edge.Intersects(otherEdge, out _))
                        return true;
                }
            }

            return false;
        }

        public bool Intersects(in IPolygon2D p) => Intersects(p.Convert());

        public bool Intersects(in Polygon poly) => TriangleIntersectionExtensions.Intersects(this, poly);

        public IShape2D Translate(in IPoint2D offset)
        {
            Vector2 vector = offset.Convert();
            return new Triangle([.. this.Points.Select(p => p + vector)]);
        }

        public RotationDirection Winding
        {
            get
            {
                double result = (P2.Y - P1.Y) * (P3.X - P2.X) -
                                (P2.X - P1.X) * (P3.Y - P2.Y);

                if (result == 0)
                    return RotationDirection.Colinear;

                return result > 0 ? RotationDirection.Clockwise : RotationDirection.Counterclockwise;
            }
        }

        public static RotationDirection GetWinding(Vector2 P1, Vector2 P2, Vector2 P3)
        {
            double result = (P2.Y - P1.Y) * (P3.X - P2.X) -
                            (P2.X - P1.X) * (P3.Y - P2.Y);

            if (result == 0)
                return RotationDirection.Colinear;

            return result > 0 ? RotationDirection.Clockwise : RotationDirection.Counterclockwise;
        }

        public static RotationDirection GetWinding(Vector2[] pts)
        {
            if (pts.Length > 3)
                throw new ArgumentException("Triangle winding expects less than three points.");


            return Triangle.GetWinding(pts[0], pts[1], pts[2]);
        }

        bool IEquatable<Triangle>.Equals(Triangle other)
        {
            return this.P1 == other.P1 &&
                   this.P2 == other.P2 &&
                   this.P3 == other.P3;
        }

        public ShapeType2D ShapeType => ShapeType2D.Triangle;
    }
}
