using System;
using System.Collections.Generic;

namespace Geometry
{

    public class Vector2Comparer(bool xyOrder = true) : IComparer<Vector2>, IComparer<IPoint2D>
    {
        public readonly bool XYOrder = xyOrder;

        public int Compare(IPoint2D A, IPoint2D B) => XYOrder ? Vector2ComparerXY.CompareXY(in A, in B) : Vector2ComparerYX.CompareYX(in A, in B);

        public int Compare(Vector2 A, Vector2 B) => XYOrder ? Vector2ComparerXY.CompareXY(A, B) : Vector2ComparerYX.CompareYX(A, B);
    }

    /// <summary>
    /// Exact Y-then-X ordering. Does not use <see cref="Tolerance.Epsilon"/>; Delaunay splits require exact compares.
    /// </summary>
    public class Vector2ComparerYX : IComparer<Vector2>, IComparer<IPoint2D>
    {
        public static int CompareYX(in IPoint2D A, in IPoint2D B)
        {
            // Exact compare (no Tolerance.Epsilon): same reason as Vector2ComparerXY — Delaunay splits.
            double diffY = A.Y - B.Y;

            if (diffY == 0)//Math.Abs(diffY) <= Tolerance.Epsilon)
            {
                double diffX = A.X - B.X;
                //if (diffX * diffX + diffY * diffY < Tolerance.EpsilonSquared)
                //return 0;

                if (diffX == 0)//Math.Abs(diffX) <= Tolerance.Epsilon)
                {
                    return 0;
                    //Edge case. The points aren't equal by our standard, so check again and figure out which axis isn't equal first
                    /*if (diffY == 0)
                    {*/
                    //    return diffX > 0 ? 1 : -1;
                    /*}
                    else
                    {
                        return diffY > 0 ? 1 : -1;
                    }*/
                }

                return diffX > 0 ? 1 : -1;
            }

            return diffY > 0 ? 1 : -1;
        }

        public int Compare(IPoint2D A, IPoint2D B) => Vector2ComparerYX.CompareYX(in A, in B);

        public int Compare(Vector2 x, Vector2 y) => Vector2ComparerYX.CompareYX((IPoint2D)x, (IPoint2D)y);
    }

    /// <summary>
    /// Exact X-then-Y ordering. Does not use <see cref="Tolerance.Epsilon"/>; Delaunay splits require exact compares.
    /// </summary>
    public class Vector2ComparerXY : IComparer<Vector2>, IComparer<IPoint2D>
    {
        public static int CompareXY(in IPoint2D A, in IPoint2D B)
        {
            // Exact compare (no Tolerance.Epsilon): epsilon equality would collapse nearby points and
            // break Delaunay divide-and-conquer, which splits sorted sets into equal halves.
            // SortedSet uniqueness therefore does not match Vector2.Equals.
            double diffX = A.X - B.X;

            if (diffX == 0)//Math.Abs(diffX) <= Tolerance.Epsilon)
            {
                double diffY = A.Y - B.Y;
                //if (diffX * diffX + diffY * diffY < Tolerance.EpsilonSquared)
                //    return 0;

                if (diffY == 0)//Math.Abs(diffY) <= Tolerance.Epsilon)
                {
                    return 0;
                    //Edge case. The points aren't equal by our standard, so check again and figure out which axis isn't equal first
                    /*if (diffX == 0)
                    {*/
                    //                        return diffY > 0 ? 1 : -1;
                    /*}
                    else
                    {
                        return diffX > 0 ? 1 : -1;
                    }*/
                }

                return diffY > 0 ? 1 : -1;
            }

            return diffX > 0 ? 1 : -1;
        }

        public int Compare(IPoint2D A, IPoint2D B) => Vector2ComparerXY.CompareXY(in A, in B);

        public int Compare(Vector2 x, Vector2 y) => Vector2ComparerXY.CompareXY((IPoint2D)x, (IPoint2D)y);
    }


    /// <summary>
    /// Double-precision 2D point. Distinct from <c>System.Numerics.Vector2</c> (single-precision) and not a drop-in replacement.
    /// </summary>
    [Serializable]
    public readonly struct Vector2 : IShape2D, IHasControlPoints, IPoint2D, ICloneable, IComparable,
                                IComparable<Vector2>, IEquatable<Vector2>,
                                IComparable<IPoint2D>, IEquatable<IPoint2D>
    {
        public static readonly Vector2 UnitX = new(1, 0);
        public static readonly Vector2 UnitY = new(0, 1);
        public static readonly Vector2 Zero = new(0, 0);
        public static readonly Vector2 One = new(1, 1);
        /// <summary>
        /// A NaN constant for an unintitialized point
        /// </summary>
        public static readonly Vector2 NaN = new(double.NaN, double.NaN);

        public readonly double X;
        public readonly double Y;

        public readonly double[] Coords => [X, Y];

        public Vector2(in double x, in double y)
        {
            this.X = x;
            this.Y = y;
        }

        public Vector2(in IPoint2D p)
        {
            this.X = p.X;
            this.Y = p.Y;
        }

        public readonly void Deconstruct(out double x, out double y)
        {
            x = X;
            y = Y;
        }

        /*
        static System.Random random = new Random();

        public static Vector2 Random()
        {
            //Todo: Move this to a static global
            var p = new Vector2(random.NextDouble(), random.NextDouble());
            //System.Diagnostics.Trace.WriteLine(string.Format("{0}", p));

            return p;
        }
        */

        public readonly Vector3 ToVector3(in double z) => new Vector3(this.X, this.Y, z);

        /// <summary>
        /// True when the points coincide within <see cref="Tolerance.Epsilon"/>.
        /// </summary>
        public readonly bool Equals(Vector2 B) => Vector2.Equals(this, B);

        /// <summary>
        /// True when the points coincide within <see cref="Tolerance.Epsilon"/>.
        /// </summary>
        public static bool Equals(in Vector2 A, in Vector2 B)
        {
            double XDelta = A.X - B.X;

            if (XDelta < -Tolerance.Epsilon || XDelta > Tolerance.Epsilon)
                return false;

            double YDelta = A.Y - B.Y;
            if (YDelta < -Tolerance.Epsilon || YDelta > Tolerance.Epsilon)
                return false;

            return ((XDelta * XDelta) + (YDelta * YDelta)) <= Tolerance.EpsilonSquared;
        }

        readonly bool IEquatable<IShape2D>.Equals(IShape2D other)
        {
            if (other is null)
                return false;

            IPoint2D p = other as IPoint2D;
            return ((IEquatable<IPoint2D>)this).Equals(p);
        }

        readonly bool IEquatable<Vector2>.Equals(Vector2 B) => Vector2.Equals(this, B);

        readonly bool IEquatable<IPoint2D>.Equals(IPoint2D B)
        {
            if (B is null)
                return false;

            double XDelta = X - B.X;

            if (XDelta < -Tolerance.Epsilon || XDelta > Tolerance.Epsilon)
                return false;

            double YDelta = Y - B.Y;
            if (YDelta < -Tolerance.Epsilon || YDelta > Tolerance.Epsilon)
                return false;

            return ((XDelta * XDelta) + (YDelta * YDelta)) <= Tolerance.EpsilonSquared;

            //return DistanceSquared((IPoint2D)this, B) <= Tolerance.EpsilonSquared;
        }

        public override readonly bool Equals(object obj)
        {
            if (obj is null)
                return false;

            if (obj is Vector2 other)
                return Equals(other);
            if (obj is IPoint2D point2D)
                return Equals(point2D);

            return false;
        }

        public readonly int CompareTo(Object Obj)
        {
            IPoint2D B = (IPoint2D)Obj;

            return Vector2ComparerXY.CompareXY(this, B);
        }

        readonly int IComparable<Vector2>.CompareTo(Vector2 B) => Vector2ComparerXY.CompareXY((IPoint2D)this, (IPoint2D)B);

        public readonly int CompareTo(IPoint2D other) => Vector2ComparerXY.CompareXY(this, other);

        readonly object ICloneable.Clone() => new Vector2(X, Y);

        public override readonly int GetHashCode() => GeometryHashCode.Point2D(X, Y);

        public override readonly string ToString() => $"X: {X:F2} Y: {Y:F2}";//return '{' + string.Format("\"X\":{0:F2},\"Y\":{1:F2}", X, Y) + '}';

        public readonly string ToJSON() => '{' + $"\"X\":{X:F2},\"Y\":{Y:F2}" + '}';

        public readonly string ToLabel() => $"{X:F2} {Y:F2}";

        public static string ToMatlab(Vector2[] array)
        {
            if (array is null)
                throw new ArgumentNullException(nameof(array));

            string s = "[";
            for (int i = 0; i < array.Length; i++)
            {
                s += $"{array[i].X} {array[i].Y};{System.Environment.NewLine}";
            }
            s += "]";

            return s;
        }

        public readonly double Magnitude => Math.Sqrt(Math.Pow(X, 2) + Math.Pow(Y, 2));

        public Vector2 Normalize() => Normalize(this);

        public static Vector2 Rotate90(in Vector2 A) => new Vector2(-A.Y, A.X);

        public Vector2 Rotate(double radians)
        {
            double c = Math.Cos(radians);
            double s = Math.Sin(radians);
            return new Vector2((X * c) - (Y * s), (X * s) + (Y * c));
        }

        public static Vector2 Normalize(in Vector2 A)
        {
            double mag = A.Magnitude;
            if (mag <= Tolerance.Epsilon)
                return A;

            return new Vector2(A.X / mag, A.Y / mag);
        }

        public static double Distance(in Vector2 A, in Vector2 B)
        {
            var dX = A.X - B.X;
            var dY = A.Y - B.Y;

            return Math.Sqrt((dX * dX) + (dY * dY));
        }

        public static double Distance(in IPoint2D A, in IPoint2D B)
        {
            if (A is null)
                throw new ArgumentNullException(nameof(A));
            if (B is null)
                throw new ArgumentNullException(nameof(B));

            double dX = A.X - B.X;
            double dY = A.Y - B.Y;

            return Math.Sqrt((dX * dX) + (dY * dY));
        }

        public static double DistanceSquared(in Vector2 A, in Vector2 B)
        {
            double dX = A.X - B.X;
            double dY = A.Y - B.Y;

            return (dX * dX) + (dY * dY);
        }

        public static double DistanceSquared(in IPoint2D A, in IPoint2D B)
        {
            if (A is null)
                throw new ArgumentNullException(nameof(A));
            if (B is null)
                throw new ArgumentNullException(nameof(B));

            double dX = A.X - B.X;
            double dY = A.Y - B.Y;

            return (dX * dX) + (dY * dY);
        }

        /// <summary>Coordinates rounded to <paramref name="precision"/> decimal places.</summary>
        public readonly Vector2 Round(int precision) => new Vector2(Math.Round(this.X, precision), Math.Round(this.Y, precision));

        public static double Dot(in Vector2 A, in Vector2 B) => (A.X * B.X) + (A.Y * B.Y);

        /// <summary>
        /// Signed arc from A to B about Origin. Negative is counter-clockwise when <paramref name="Clockwise"/> is false.
        /// </summary>
        public static double ArcAngle(in Vector2 Origin, in Vector2 A, in Vector2 B, bool Clockwise = false)
        {
            var U = A - Origin;
            var V = B - Origin;
            double AngleA = Math.Atan2(U.Y, U.X);
            double AngleB = Math.Atan2(V.Y, V.X);
            double Angle = Clockwise ? AngleB - AngleA : AngleA - AngleB;

            if (Angle <= -Math.PI)
                Angle += Math.PI * 2;
            else if (Angle > Math.PI)
                Angle -= Math.PI * 2;

            return Angle;
        }

        /// <summary>
        /// Signed arc from A to B about Origin. Negative is counter-clockwise when <paramref name="Clockwise"/> is false.
        /// </summary>
        public static double ArcAngle(in IPoint2D Origin, IPoint2D A, IPoint2D B, bool Clockwise = false)
        {
            A = new Vector2(A.X - Origin.X, A.Y - Origin.Y);
            B = new Vector2(B.X - Origin.X, B.Y - Origin.Y);
            double AngleA = Math.Atan2(A.Y, A.X);
            double AngleB = Math.Atan2(B.Y, B.X);
            double Angle = Clockwise ? AngleB - AngleA : AngleA - AngleB;

            if (Angle < -Math.PI)
                Angle += Math.PI * 2;
            else if (Angle > Math.PI)
                Angle -= Math.PI * 2;

            return Angle;
        }

        /// <summary>
        /// Unsigned arc from A to B about Origin in [0, 2π). Direction follows <paramref name="Clockwise"/>.
        /// </summary>
        public static double AbsArcAngle(in Vector2 Origin, Vector2 A, Vector2 B, bool Clockwise = false)
        {
            A = new Vector2(A.X - Origin.X, A.Y - Origin.Y);
            B = new Vector2(B.X - Origin.X, B.Y - Origin.Y);
            double AngleA = Math.Atan2(A.Y, A.X);
            double AngleB = Math.Atan2(B.Y, B.X);
            double Angle = Clockwise ? AngleB - AngleA : AngleA - AngleB;

            if (Angle < 0)
                Angle += Math.PI * 2;
            else if (Angle >= Math.PI * 2)
                Angle -= Math.PI * 2;

            return Angle;
        }

        /// <summary>
        /// Unsigned angle from <paramref name="BaseLine"/>'s direction to <paramref name="P"/>, in [0, 2π).
        /// </summary>
        public static double AbsArcAngle(in Line BaseLine, Vector2 P, bool Clockwise = false)
        {
            Vector2 A = new(P.X - BaseLine.Origin.X, P.Y - BaseLine.Origin.Y);
            Vector2 B = BaseLine.Direction;
            double AngleA = Math.Atan2(A.Y, A.X);
            double AngleB = Math.Atan2(B.Y, B.X);
            double Angle = Clockwise ? AngleB - AngleA : AngleA - AngleB;

            if (Angle < 0)
                Angle += Math.PI * 2;
            else if (Angle >= Math.PI * 2)
                Angle -= Math.PI * 2;

            return Angle;
        }

        /// <summary>
        /// Unsigned arc from A to B about Origin in [0, 2π). Direction follows <paramref name="Clockwise"/>.
        /// </summary>
        public static double AbsArcAngle(in IPoint2D Origin, IPoint2D A, IPoint2D B, bool Clockwise = false)
        {
            A = new Vector2(A.X - Origin.X, A.Y - Origin.Y);
            B = new Vector2(B.X - Origin.X, B.Y - Origin.Y);
            double AngleA = Math.Atan2(A.Y, A.X);
            double AngleB = Math.Atan2(B.Y, B.X);
            double Angle = Clockwise ? AngleB - AngleA : AngleA - AngleB;

            if (Angle < 0)
                Angle += Math.PI * 2;
            else if (Angle >= Math.PI * 2)
                Angle -= Math.PI * 2;

            return Angle;
        }

        /// <summary>
        /// Angle to B from A from the X-Axis
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static double Angle(in Vector2 A, in Vector2 B)
        {
            Vector2 delta = B - A;
            return Math.Atan2(delta.Y, delta.X);
        }

        public static Vector2 operator -(in Vector2 A) => new Vector2(-A.X, -A.Y);

        public static Vector2 operator -(in Vector2 A, in Vector2 B) => new Vector2(A.X - B.X, A.Y - B.Y);

        public static Vector2 operator +(in Vector2 A, in Vector2 B) => new Vector2(A.X + B.X, A.Y + B.Y);

        public static Vector2 operator -(in Vector2 A, in IPoint2D B) => new Vector2(A.X - B.X, A.Y - B.Y);

        public static Vector2 operator +(in Vector2 A, in IPoint2D B) => new Vector2(A.X + B.X, A.Y + B.Y);

        public static Vector2 operator *(in Vector2 A, double scalar) => new Vector2(A.X * scalar, A.Y * scalar);

        public static Vector2 operator *(in Vector2 A, in Vector2 B) => new Vector2(A.X * B.X, A.Y * B.Y);

        public static Vector2 operator /(in Vector2 A, double scalar) => new Vector2(A.X / scalar, A.Y / scalar);

        public static Vector2 operator /(in Vector2 A, in Vector2 B) => new Vector2(A.X / B.X, A.Y / B.Y);

        public static bool operator ==(in Vector2 A, in Vector2 B) => Vector2.Equals(A, B);

        public static bool operator !=(in Vector2 A, in Vector2 B) => !Vector2.Equals(A, B);

        public static bool operator ==(in Vector2 A, in IPoint2D B) => Vector2.Equals(A, B);

        public static bool operator !=(in Vector2 A, in IPoint2D B) => !Vector2.Equals(A, B);

        public double this[Axis axis] => axis switch
        {
            Axis.X => X,
            Axis.Y => Y,
            _ => throw new IndexOutOfRangeException($"Axis not supported for {nameof(Vector2)}"),
        };

        /// <summary>
        /// Point in triangle (v1, v2, v3) from barycentric (u, v), with w = 1 − u − v at v1.
        /// </summary>
        public static Vector2 FromBarycentric(in Vector2 v1, in Vector2 v2, in Vector2 v3, in double u, in double v)
        {
            double x = (v1.X * (1 - u - v)) + (v2.X * u) + (v3.X * v);
            double y = (v1.Y * (1 - u - v)) + (v2.Y * u) + (v3.Y * v);
            return new Vector2(x, y);
        }

        public static Vector2 Scale(in Vector2 A, in double scalar) => new Vector2(A.X * scalar, A.Y * scalar);
        /*
                public void Scale(double scalar)
                {
                    X = X * scalar;
                    Y = Y * scalar; 
                }
        */
        public static Rectangle Border(in Vector2[] points) => points.BoundingBox();

        public static Rectangle Border(in IPoint2D[] points)
        {
            if (points is null)
                throw new ArgumentNullException(nameof(points));

            if (points.Length == 0)
                throw new ArgumentException("points must not be empty", nameof(points));

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            for (int i = 0; i < points.Length; i++)
            {
                minX = Math.Min(minX, points[i].X);
                maxX = Math.Max(maxX, points[i].X);
                minY = Math.Min(minY, points[i].Y);
                maxY = Math.Max(maxY, points[i].Y);
            }

            return new Rectangle(minX, maxX, minY, maxY);
        }

        readonly bool IShape2D.Contains(in IPoint2D p) =>
            ((IShape2D)this).GetRelation(p).IsContains();

        readonly bool IShape2D.Covers(in IPoint2D p) =>
            ((IShape2D)this).GetRelation(p).IsCovers();

        /// <summary>
        /// Equal points are Contained. A point has no boundary, so this never returns Touching.
        /// </summary>
        readonly ShapeRelation IShape2D.GetRelation(in Geometry.IPoint2D p)
        {
            return p is not null && Equals(p) ? ShapeRelation.Contained : ShapeRelation.None;
        }

        readonly ShapeRelation IShape2D.GetRelation(in Geometry.ILineSegment2D l) => l.GetRelation(this);

        readonly bool IShape2D.Intersects(in IShape2D shape) => shape.Covers(this);

        readonly IShape2D IShape2D.Translate(in IPoint2D offset) => this + offset.Convert();

        public readonly int IsLeftSide(Vector2[] pqr) => Vector2.IsLeftSide(this, pqr);

        /// <summary>
        /// Return true if t is on the left side of two half lines described by pqr
        /// 
        ///               p
        ///              /
        /// Right-Side  q  Left-Side
        ///             |
        ///             r
        ///             
        /// </summary>
        /// <param name="t"></param>
        /// <param name="pqr"></param>
        /// <returns>1 if left
        ///          0 if on a line
        ///          -1 if right</returns>
        public static int IsLeftSide(in Vector2 t, Vector2[] pqr)
        {
            System.Diagnostics.Debug.Assert(pqr.Length == 3);

            //Figure out which line the point projects to.
            LineSegment QP = new(pqr[1], pqr[0]);
            LineSegment QR = new(pqr[1], pqr[2]);

            bool OnQP = QP.Dot(t) >= 0;
            bool OnQR = QR.Dot(t) >= 0;

            int LeftQP = -QP.IsLeft(t); //Use negative QP.IsLeft because we reversed line order
            int LeftQR = QR.IsLeft(t); //Use not QP because we reversed line order


            if (OnQP && OnQR)
            {
                //
                //    p     r
                //     \ t /
                //      \ /
                //       q
                //

                //Use not QP because we reversed line order
                if (LeftQP == 0 || LeftQR == 0)
                    return 0;

                return LeftQP > 0 && LeftQR > 0 ? 1 : -1;
            }
            else if (OnQR)
            {
                return LeftQR;
            }
            else if (OnQP)
            {
                //Use not QP because we reversed line order
                return LeftQP;
            }
            else
            {
                //
                //    p     r
                //     \   /
                //      \ /
                //       q
                //
                //    t
                //

                return -1;
            }
        }



        #region IPoint2D Members

        double IPoint2D.X => X;

        double IPoint2D.Y => Y;

        readonly Rectangle IShape2D.BoundingBox => new(this, 0, 0);

        readonly double IShape2D.Area => 0;

        readonly ShapeType2D IShape2D.ShapeType => ShapeType2D.Point;

        readonly IReadOnlyList<IPoint2D> IHasControlPoints.ControlPoints => [this];


        #endregion
        readonly IPoint2D ICentroid.Centroid => this;

        public static bool operator <(Vector2 left, Vector2 right) => left.CompareTo(right) < 0;

        public static bool operator <=(Vector2 left, Vector2 right) => left.CompareTo(right) <= 0;

        public static bool operator >(Vector2 left, Vector2 right) => left.CompareTo(right) > 0;

        public static bool operator >=(Vector2 left, Vector2 right) => left.CompareTo(right) >= 0;
    }
}
