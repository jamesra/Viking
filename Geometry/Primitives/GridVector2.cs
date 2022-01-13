using System;
using System.Collections.Generic;

namespace Geometry
{
    public class GridVectorComparer : IComparer<GridVector2>, IComparer<IPoint2D>
    {
        public bool XYOrder;

        public GridVectorComparer(bool xyOrder=true)
        {
            XYOrder = xyOrder;
        }

        public int Compare(IPoint2D A, IPoint2D B)
        {
            return XYOrder ? GridVectorComparerXY.CompareXY(in A, in B) : GridVectorComparerYX.CompareYX(in A, in B);
        }

        public int Compare(GridVector2 A, GridVector2 B)
        {
            return XYOrder ? GridVectorComparerXY.CompareXY(A, B) : GridVectorComparerYX.CompareYX(A, B);
        }
    }

    public class GridVectorComparerYX : IComparer<GridVector2>, IComparer<IPoint2D>
    {
        public static int CompareYX(in IPoint2D A, in IPoint2D B)
        {
            //We need to use the same equality standard as our epsilon value
            double diffY = A.Y - B.Y; 

            if (diffY == 0)//Math.Abs(diffY) <= Global.Epsilon)
            {
                double diffX = A.X - B.X;
                //if (diffX * diffX + diffY * diffY < Global.EpsilonSquared)
                    //return 0;

                if (diffX == 0)//Math.Abs(diffX) <= Global.Epsilon)
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

        public int Compare( IPoint2D A, IPoint2D B)
        {
            return GridVectorComparerYX.CompareYX(in A, in B);
        }

        public int Compare(GridVector2 x, GridVector2 y)
        {
            return GridVectorComparerYX.CompareYX((IPoint2D)x, (IPoint2D)y);
        }
    }

    public class GridVectorComparerXY : IComparer<GridVector2>, IComparer<IPoint2D>
    {
        /// <summary>
        /// Sorts points on the X-Axis first, then Y-Axis
        /// 

        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static int CompareXY(in IPoint2D A, in IPoint2D B)
        {
            /// I struggled with how this code should behave.  For now it is the expected behaviour,
            /// however there is a global.epsilon value that is used to limit the precision of point 
            /// position to help with rounding errors in equality tests.  However that means two points
            /// can be equal according to the Viking code but still sort as non-equal.  That can be 
            /// an issue when using classes such as SortedSet to avoid duplicate points.  If we check 
            /// for the epsilon based equality first it breaks the delaunay implementation where point
            /// sets are divided equally into two parts. 
            /// 
            //We need to use the same equality standard as our epsilon value
            double diffX = A.X - B.X;

            if (diffX == 0)//Math.Abs(diffX) <= Global.Epsilon)
            {
                double diffY = A.Y - B.Y;
                //if (diffX * diffX + diffY * diffY < Global.EpsilonSquared)
                //    return 0;

                if (diffY == 0)//Math.Abs(diffY) <= Global.Epsilon)
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

        public int Compare(IPoint2D A, IPoint2D B)
        {
            return GridVectorComparerXY.CompareXY(in A, in B);
        }

        public int Compare(GridVector2 x, GridVector2 y)
        {
            return GridVectorComparerXY.CompareXY((IPoint2D)x, (IPoint2D)y);
        }
    }


    [Serializable]
    public struct GridVector2 : IShape2D, IPoint, ICloneable, IComparable,
                                IComparable<GridVector2>, IEquatable<GridVector2>,
                                IComparable<IPoint2D>, IEquatable<IPoint2D>
    {
        public static readonly GridVector2 UnitX = new GridVector2(1, 0);
        public static readonly GridVector2 UnitY = new GridVector2(0, 1);
        public static readonly GridVector2 Zero = new GridVector2(0, 0);

        public double X;
        public double Y;

        public double[] coords { get { return new double[] { X,Y }; } }

        public GridVector2(double x, double y)
        {
            this.X = x;
            this.Y = y;
        }

        public GridVector2(IPoint2D p)
        {
            this.X = p.X;
            this.Y = p.Y;
        }

        public void Deconstruct(out double x, out double y)
        {
            x = X;
            y = Y;
        }

        /*
        static System.Random random = new Random();

        public static GridVector2 Random()
        {
            //Todo: Move this to a static global
            var p = new GridVector2(random.NextDouble(), random.NextDouble());
            //System.Diagnostics.Trace.WriteLine(string.Format("{0}", p));

            return p;
        }
        */

        public GridVector3 ToGridVector3(double z)
        {
            return new GridVector3(this.X, this.Y, z);
        }

        /// <summary>
        /// Compares two vectors, assumes they have the same position if they are within the epsilon squared distance of each other
        /// </summary>
        /// <param name="B"></param>
        /// <param name="Epsilon"></param>
        /// <returns></returns>
        public bool Equals(in GridVector2 B)
        {
            return GridVector2.Equals(this, B);
        }

        /// <summary>
        /// Compares two vectors, assumes they have the same position if they are within the epsilon squared distance of each other
        /// </summary>
        /// <param name="B"></param>
        /// <param name="Epsilon"></param>
        /// <returns></returns>
        public static bool Equals(in GridVector2 A, in GridVector2 B)
        {
            double XDelta = A.X - B.X;
            
            if (XDelta < -Global.Epsilon || XDelta > Global.Epsilon)
                return false;

            double YDelta = A.Y - B.Y;
            if (YDelta < -Global.Epsilon || YDelta > Global.Epsilon)
                return false;

            return ((XDelta * XDelta) + (YDelta * YDelta)) <= Global.EpsilonSquared;
        }

        bool IEquatable<IShape2D>.Equals(IShape2D other)
        {
            if (other is null)
                return false;

            IPoint2D p = other as IPoint2D;
            return ((IEquatable<IPoint2D>)this).Equals(p);
        }

        bool IEquatable<GridVector2>.Equals(GridVector2 B)
        {
            return GridVector2.Equals(this, B);
        }

        bool IEquatable<IPoint2D>.Equals(IPoint2D B)
        {
            if (B is null)
                return false;

            double XDelta = X - B.X;

            if (XDelta < -Global.Epsilon || XDelta > Global.Epsilon)
                return false;

            double YDelta = Y - B.Y;
            if (YDelta < -Global.Epsilon || YDelta > Global.Epsilon)
                return false;

            return ((XDelta * XDelta) + (YDelta * YDelta)) <= Global.EpsilonSquared;

            //return DistanceSquared((IPoint2D)this, B) <= Global.EpsilonSquared;
        }

        public bool Equals(IPoint other)
        {
            double XDelta = X - other.X;

            if (XDelta < -Global.Epsilon || XDelta > Global.Epsilon)
                return false;

            double YDelta = Y - other.Y;
            if (YDelta < -Global.Epsilon || YDelta > Global.Epsilon)
                return false;

            return ((XDelta * XDelta) + (YDelta * YDelta)) <= Global.EpsilonSquared;
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;

            if (obj is GridVector2 other)
                return Equals(other);
            if (obj is IPoint2D point2D)
                return Equals(point2D);
            if (obj is IPoint point3D)
                return Equals(point3D);

            return false;
        } 

        public int CompareTo(Object Obj)
        {
            IPoint2D B = (IPoint2D)Obj;

            return GridVectorComparerXY.CompareXY(this, B);
        }

        int IComparable<GridVector2>.CompareTo(GridVector2 B)
        {
            return GridVectorComparerXY.CompareXY((IPoint2D)this, (IPoint2D)B);
        }
         
        public int CompareTo(IPoint2D other)
        {
            return GridVectorComparerXY.CompareXY(this, other);
        } 

        object ICloneable.Clone()
        {
            return new GridVector2(X, Y); 
        }

        public override int GetHashCode()
        {
            ///There have been bugs in the past where two points are within an epsilon distance
            ///and should be equal, but they will not get the same hash value.
            ///I believe rounding to a value that is an order of magnitude larger than the epsilon value 
            ///should fix this... but I'm not feeling 100% certain today.
            ///If my thinking is incorrec the fix is to 1) Avoid hashing or 2) Stop using epsilon and only use actual values.
            ///Changing the behavior to 2 may be OK.  It would take a lot of careful testing and time is short
            ///at the moment.  Hopefully this fixes the issue.
            double prod = Math.Round(X, Global.SignificantDigits-1) * Math.Round(Y, Global.SignificantDigits-1);
            double code = Math.Abs(prod);
            if (code < 1)
            {
                return (int)(1.0 / code);
            }
            return (int)prod; 
        }
        
        public override string ToString()
        {
            return string.Format("X: {0:F2} Y: {1:F2}", X, Y);
            //return '{' + string.Format("\"X\":{0:F2},\"Y\":{1:F2}", X, Y) + '}';
        }

        public string ToJSON()
        {
            return '{' + string.Format("\"X\":{0:F2},\"Y\":{1:F2}", X, Y) + '}';
        }

        public string ToLabel()
        {
            return string.Format("{0:F2} {1:F2}", X, Y);
        }

        public static string ToMatlab(GridVector2[] array)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array)); 

            string s = "[";
            for (int i = 0; i < array.Length; i++)
            {
                s += array[i].X.ToString() + " " + array[i].Y.ToString() + ";" + System.Environment.NewLine;
            }
            s += "]";

            return s;
        }
         
        public double Magnitude => Math.Sqrt(Math.Pow(X, 2) + Math.Pow(Y,2));

        public void Normalize()
        {
            double mag = this.Magnitude;
            X = X / mag;
            Y = Y / mag; 
        }

        public static GridVector2 Rotate90(in GridVector2 A)
        {
            return new GridVector2(-A.Y, A.X);
        }

        public static GridVector2 Normalize(in GridVector2 A)
        {
            double mag = A.Magnitude;
            return new GridVector2(A.X / mag, A.Y / mag); 
        }

        public static double Distance(in GridVector2 A, in GridVector2 B)
        {
            var dX = A.X - B.X; 
            var dY = A.Y - B.Y;

            return Math.Sqrt((dX*dX)+(dY*dY));
        }

        public static double Distance(in IPoint A, in IPoint B)
        {
            if(A == null)
                throw new ArgumentNullException(nameof(A)); 
            if(B == null)
                throw new ArgumentNullException(nameof(B)); 

            double dX = A.X - B.X;
            double dY = A.Y - B.Y;

            return Math.Sqrt((dX * dX) + (dY * dY));
        }

        public static double Distance(in IPoint2D A, in IPoint2D B)
        {
            if (A == null)
                throw new ArgumentNullException(nameof(A));
            if (B == null)
                throw new ArgumentNullException(nameof(B));

            double dX = A.X - B.X;
            double dY = A.Y - B.Y;

            return Math.Sqrt((dX * dX) + (dY * dY));
        }

        public static double DistanceSquared(in GridVector2 A, in GridVector2 B)
        {
            double dX = A.X - B.X;
            double dY = A.Y - B.Y;

            return (dX * dX) + (dY * dY);
        }

        public static double DistanceSquared(in IPoint2D A, in IPoint2D B)
        {
            if (A == null)
                throw new ArgumentNullException(nameof(A));
            if (B == null)
                throw new ArgumentNullException(nameof(B)); 

            double dX = A.X - B.X;
            double dY = A.Y - B.Y;

            return (dX * dX) + (dY * dY);
        }

        /// <summary>
        /// Rounds coordinates to nearest precision
        /// </summary>
        /// <param name="precision">Number of decimal places in the result</param>
        /// <returns></returns>
        public GridVector2 Round(int precision)
        {
            return new GridVector2(Math.Round(this.X, precision), Math.Round(this.Y, precision));
        }

        /// <summary>
        /// Returns dot product of two vectors. Input is rounded to 2 decimal places because of problems I had with double size limit
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static double Dot(in GridVector2 A, in GridVector2 B)
        {
            /*
            double AX = Math.Round(A.X, 3);
            double AY = Math.Round(A.Y, 3);
            double BX = Math.Round(B.X, 3);
            double BY = Math.Round(B.Y, 3);
             */

            double AX = (double)(float)A.X;
            double AY = (double)(float)A.Y;
            double BX = (double)(float)B.X;
            double BY = (double)(float)B.Y;

            return (AX * BX) + (AY * BY); 
        }

        /// <summary>
        /// Angle of arc from A to B measured at Origin. 
        /// A -negative value is a counter-clockwise arc
        /// </summary>
        /// <param name="Origin"></param>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static double ArcAngle(in GridVector2 Origin, in GridVector2 A, in GridVector2 B, bool Clockwise = false)
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
        /// Angle of arc between A & B with Origin
        /// </summary>
        /// <param name="Origin"></param>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static double ArcAngle(in IPoint2D Origin, IPoint2D A, IPoint2D B, bool Clockwise = false)
        {
            A = new GridVector2(A.X - Origin.X, A.Y - Origin.Y);
            B = new GridVector2(B.X - Origin.X, B.Y - Origin.Y);
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
        /// Angle of arc between A & B with Origin.  Measures only in one direction, no neegative angles will be returned
        /// </summary>
        /// <param name="Origin"></param>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static double AbsArcAngle(in GridVector2 Origin, GridVector2 A, GridVector2 B, bool Clockwise = false)
        {
            A = new GridVector2(A.X - Origin.X, A.Y - Origin.Y);
            B = new GridVector2(B.X - Origin.X, B.Y - Origin.Y);
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
        /// Angle of arc between A & B with Origin.  Measures only in one direction, no neegative angles will be returned
        /// </summary>
        /// <param name="Origin"></param>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static double AbsArcAngle(in GridLine BaseLine, GridVector2 P, bool Clockwise=false)
        {
            GridVector2 A = new GridVector2(P.X - BaseLine.Origin.X, P.Y - BaseLine.Origin.Y);
            GridVector2 B = BaseLine.Direction;
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
        /// Angle of arc between A & B with Origin.  Measures only in one direction, no neegative angles will be returned
        /// </summary>
        /// <param name="Origin"></param>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static double AbsArcAngle(in IPoint2D Origin, IPoint2D A, IPoint2D B, bool Clockwise = false)
        {
            A = new GridVector2(A.X - Origin.X, A.Y - Origin.Y);
            B = new GridVector2(B.X - Origin.X, B.Y - Origin.Y);
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
        public static double Angle(in GridVector2 A, in GridVector2 B)
        {
            GridVector2 delta = B - A;
            return Math.Atan2(delta.Y, delta.X);
        }

        public static GridVector2 operator -(in GridVector2 A)
        {
            return new GridVector2(-A.X, -A.Y); 
        }

        public static GridVector2 operator -(in GridVector2 A, in GridVector2 B)
        {
            return new GridVector2(A.X - B.X, A.Y - B.Y); 
        }

        public static GridVector2 operator +(in GridVector2 A, in GridVector2 B)
        {
            return new GridVector2(A.X + B.X, A.Y + B.Y); 
        }

        public static GridVector2 operator -(in GridVector2 A, in IPoint2D B)
        {
            return new GridVector2(A.X - B.X, A.Y - B.Y);
        }

        public static GridVector2 operator +(in GridVector2 A, in IPoint2D B)
        {
            return new GridVector2(A.X + B.X, A.Y + B.Y);
        }

        public static GridVector2 operator *(in GridVector2 A, double scalar)
        {
            return new GridVector2(A.X * scalar, A.Y * scalar);
        }

        public static GridVector2 operator *(in GridVector2 A, in GridVector2 B)
        {
            return new GridVector2(A.X * B.X, A.Y * B.Y);
        }

        public static GridVector2 operator /(in GridVector2 A, double scalar)
        {
            return new GridVector2(A.X / scalar, A.Y / scalar);
        }

        public static GridVector2 operator /(in GridVector2 A, in GridVector2 B)
        {
            return new GridVector2(A.X / B.X, A.Y / B.Y);
        }

        public static bool operator ==(in GridVector2 A, in GridVector2 B)
        {
            return GridVector2.Equals(A, B); 
        }

        public static bool operator !=(in GridVector2 A, in GridVector2 B)
        {
            return !GridVector2.Equals(A, B); 
        }

        public static bool operator ==(in GridVector2 A, in IPoint2D B)
        {
            return GridVector2.Equals(A, B);
        }

        public static bool operator !=(in GridVector2 A, in IPoint2D B)
        {
            return !GridVector2.Equals(A, B);
        }

        public double this[AXIS axis]
        {
            get
            {
                switch (axis)
                {
                    case AXIS.X:
                        return X;
                    case AXIS.Y:
                        return Y;
                    default:
                        throw new IndexOutOfRangeException($"Axis not supported for {nameof(GridVector2)}");
                } 
            }
            set
            {
                switch (axis)
                {
                    case AXIS.X:
                        X = value;
                        break;
                    case AXIS.Y:
                        Y = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException($"Axis not supported for {nameof(GridVector2)}");
                }
            }
        }

        public static GridVector2 FromBarycentric(in GridVector2 v1, in GridVector2 v2, in GridVector2 v3, in double u, in double v)
        {
            double x = (v1.X * (1 - u - v)) + (v2.X * u) + (v3.X * v);
            double y = (v1.Y * (1 - u - v)) + (v2.Y * u) + (v3.Y * v);
            return new GridVector2(x, y); 
        }

        public static GridVector2 Scale(in GridVector2 A, in double scalar)
        {
            return new GridVector2(A.X * scalar, A.Y * scalar);
        }
/*
        public void Scale(double scalar)
        {
            X = X * scalar;
            Y = Y * scalar; 
        }
*/
        public static GridRectangle Border(in GridVector2[] points)
        {
            return points.BoundingBox();
        }

        public static GridRectangle Border(in IPoint[] points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (points.Length == 0)
                throw new ArgumentException(nameof(points)); 

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

            return new GridRectangle(minX, maxX, minY, maxY);
        }

        bool IShape2D.Contains(in IPoint2D p)
        {
            return p.X == this.X && p.Y == this.Y;
        }

        bool IShape2D.Intersects(in IShape2D shape)
        {
            return shape.Contains(this);
        }

        IShape2D IShape2D.Translate(in IPoint2D offset)
        {
            return this + offset.Convert();
        }

        public int IsLeftSide(GridVector2[] pqr)
        {
            return GridVector2.IsLeftSide(this, pqr);
        }

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
        public static int IsLeftSide(in GridVector2 t, GridVector2[] pqr)
        {
            System.Diagnostics.Debug.Assert(pqr.Length == 3);

            //Figure out which line the point projects to.
            GridLineSegment QP = new GridLineSegment(pqr[1], pqr[0]);
            GridLineSegment QR = new GridLineSegment(pqr[1], pqr[2]);

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

       

        #region IPoint Members

        double IPoint2D.X
        {
            get => X;
            set => X = value;
        }

        double IPoint2D.Y
        {
            get => Y;
            set => Y = value;
        }

        double IPoint.Z
        {
            get => 0;
            set
            {

            }
        }

        GridRectangle IShape2D.BoundingBox => new GridRectangle(this, 0, 0);

        double IShape2D.Area => 0;

        ShapeType2D IShape2D.ShapeType => ShapeType2D.POINT;


        #endregion
        IPoint2D ICentroid.Centroid => this;

        public static bool operator <(GridVector2 left, GridVector2 right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(GridVector2 left, GridVector2 right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(GridVector2 left, GridVector2 right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(GridVector2 left, GridVector2 right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}
