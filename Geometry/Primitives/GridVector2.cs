using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry
{
    public class GridVectorComparer : IComparer<GridVector2>, IComparer<IPoint2D>
    {
        public bool XYOrder = true;

        public GridVectorComparer(bool xyOrder=true)
        {
            XYOrder = xyOrder;
        }

        public int Compare(IPoint2D A, IPoint2D B)
        {
            return XYOrder ? GridVectorComparerXY.CompareXY(A, B) : GridVectorComparerYX.CompareYX(A, B);
        }

        public int Compare(GridVector2 A, GridVector2 B)
        {
            return XYOrder ? GridVectorComparerXY.CompareXY(A, B) : GridVectorComparerYX.CompareYX(A, B);
        }
    }

    public class GridVectorComparerYX : IComparer<GridVector2>, IComparer<IPoint2D>
    {
        public static int CompareYX(IPoint2D A, IPoint2D B)
        {
            //We need to use the same equality standard as our epsilon value
            double diffY = A.Y - B.Y; 

            if (Math.Abs(diffY) <= Global.Epsilon)
            {
                double diffX = A.X - B.X;
                if (diffX * diffX + diffY * diffY < Global.EpsilonSquared)
                    return 0;

                if (Math.Abs(diffX) <= Global.Epsilon)
                {
                    //Edge case. The points aren't equal by our standard, so check again and figure out which axis isn't equal first
                    if (diffY == 0)
                    {
                        return diffX > 0 ? 1 : -1;
                    }
                    else
                    {
                        return diffY > 0 ? 1 : -1;
                    }
                }

                return diffX > 0 ? 1 : -1;
            }

            return diffY > 0 ? 1 : -1;
        }

        public int Compare(IPoint2D A, IPoint2D B)
        {
            return GridVectorComparerYX.CompareYX(A, B);
        }

        public int Compare(GridVector2 x, GridVector2 y)
        {
            return GridVectorComparerYX.CompareYX((IPoint2D)x, (IPoint2D)y);
        }
    }

    public class GridVectorComparerXY : IComparer<GridVector2>, IComparer<IPoint2D>
    {
        public static int CompareXY(IPoint2D A, IPoint2D B)
        {
            //We need to use the same equality standard as our epsilon value
            double diffX = A.X - B.X;

            if (Math.Abs(diffX) <= Global.Epsilon)
            {
                double diffY = A.Y - B.Y;
                if (diffX * diffX + diffY * diffY < Global.EpsilonSquared)
                    return 0;

                if (Math.Abs(diffY) <= Global.Epsilon)
                {
                    //Edge case. The points aren't equal by our standard, so check again and figure out which axis isn't equal first
                    if (diffX == 0)
                    {
                        return diffY > 0 ? 1 : -1;
                    }
                    else
                    {
                        return diffX > 0 ? 1 : -1;
                    }
                }

                return diffY > 0 ? 1 : -1;
            }

            return diffX > 0 ? 1 : -1;
        }

        public int Compare(IPoint2D A, IPoint2D B)
        {
            return GridVectorComparerXY.CompareXY(A, B);
        }

        public int Compare(GridVector2 x, GridVector2 y)
        {
            return GridVectorComparerXY.CompareXY((IPoint2D)x, (IPoint2D)y);
        }
    }


    [Serializable]
    public struct GridVector2 : IShape2D, IPoint, ICloneable, IComparable,
                                IComparable<GridVector2>, IEquatable<GridVector2>,
                                IComparable<IPoint2D>, IEquatable<IPoint2D>, IEquatable<IShape2D>
    {
        public readonly static GridVector2 UnitX = new GridVector2(1, 0);
        public readonly static GridVector2 UnitY = new GridVector2(0, 1);
        public readonly static GridVector2 Zero = new GridVector2(0, 0);

        public double X;
        public double Y;

        public double[] coords { get { return new double[] { X,Y }; } }

        public GridVector2(double x, double y)
        {
            this.X = x;
            this.Y = y;
        }

        public GridVector3 ToGridVector3(double Z)
        {
            return new GridVector3(this.X, this.Y, Z);
        }

        /// <summary>
        /// Compares two vectors, assumes they have the same position if they are within the epsilon squared distance of each other
        /// </summary>
        /// <param name="B"></param>
        /// <param name="Epsilon"></param>
        /// <returns></returns>
        public bool Equals(GridVector2 B)
        {
            return DistanceSquared(this, B) <= Global.EpsilonSquared;
        }

        /// <summary>
        /// Compares two vectors, assumes they have the same position if they are within the epsilon squared distance of each other
        /// </summary>
        /// <param name="B"></param>
        /// <param name="Epsilon"></param>
        /// <returns></returns>
        public static bool Equals(GridVector2 A, GridVector2 B)
        {
            return DistanceSquared(A, B) <= Global.EpsilonSquared;
        }

        bool IEquatable<IShape2D>.Equals(IShape2D other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            IPoint2D p = other as IPoint2D;
            return ((IEquatable<IPoint2D>)this).Equals(p);
        }

        bool IEquatable<GridVector2>.Equals(GridVector2 B)
        { 
            return DistanceSquared(this, B) <= Global.EpsilonSquared;
        }

        bool IEquatable<IPoint2D>.Equals(IPoint2D B)
        {
            if (object.ReferenceEquals(B, null))
                return false;
             
            return DistanceSquared((IPoint2D)this, B) <= Global.EpsilonSquared;
        }

        /*
        
        public int Compare(GridVector2 A, GridVector2 B)
        {
            return this.Compare((IPoint2D)A, (IPoint2D)B);
        }
        public int Compare(IPoint2D A, IPoint2D B)
        {
            //We need to use the same equality test as our epsilon value
            double diffX = A.X - B.X;
            double diffY = A.Y - B.Y;
            if (diffX * diffX + diffY * diffY < Global.EpsilonSquared)
                return 0;

            if (Math.Abs(diffX) < Global.Epsilon)
            {
                if (Math.Abs(diffY) < Global.Epsilon)
                {
                    //Edge case. The points aren't equal by our standard, so check again and figure out which axis isn't equal first
                    if (diffX == 0)
                    {
                        return diffY > 0 ? 1 : -1;
                    }
                    else
                    {
                        return diffX > 0 ? 1 : -1;
                    }
                }

                return diffY > 0 ? 1 : -1;
            }

            return diffX > 0 ? 1 : -1;
        }
        */

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
            double prod = X * Y;
            double code = Math.Abs(prod);
            if (code < 1)
            {
                return (int)(1.0 / code);
            }
            return (int)prod; 
        }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null))
                return false;

            GridVector2 B = (GridVector2)obj;

            return this == B; 
        }

        public override string ToString()
        {
            return string.Format("X: {0:F2} Y: {1:F2}", X, Y);
        }

        public string ToLabel()
        {
            return string.Format("{0:F2} {1:F2}", X, Y);
        }

        public static string ToMatlab(GridVector2[] array)
        {
            if (array == null)
                throw new ArgumentNullException("array"); 

            string s = "[";
            for (int i = 0; i < array.Length; i++)
            {
                s += array[i].X.ToString() + " " + array[i].Y.ToString() + ";" + System.Environment.NewLine;
            }
            s += "]";

            return s;
        }

        static public double Magnitude(GridVector2 A)
        {
            return Math.Sqrt((A.X * A.X) + (A.Y * A.Y));
        }

        public void Normalize()
        {
            double mag = Magnitude(this);
            X = X / mag;
            Y = Y / mag; 
        }

        static public GridVector2 Rotate90(GridVector2 A)
        {
            return new GridVector2(-A.Y, A.X);
        }

        static public GridVector2 Normalize(GridVector2 A)
        {
            double mag = Magnitude(A);
            return new GridVector2(A.X / mag, A.Y / mag); 
        }

        static public double Distance(GridVector2 A, GridVector2 B)
        {
            double dX = A.X - B.X; 
            double dY = A.Y - B.Y;

            return Math.Sqrt((dX*dX)+(dY*dY));
        }

        static public double Distance(IPoint A, IPoint B)
        {
            if(A == null)
                throw new ArgumentNullException("A"); 
            if(B == null)
                throw new ArgumentNullException("B"); 

            double dX = A.X - B.X;
            double dY = A.Y - B.Y;

            return Math.Sqrt((dX * dX) + (dY * dY));
        }

        static public double DistanceSquared(GridVector2 A, GridVector2 B)
        {
            double dX = A.X - B.X;
            double dY = A.Y - B.Y;

            return (dX * dX) + (dY * dY);
        }
          
        static public double DistanceSquared(IPoint2D A, IPoint2D B)
        {
            if (A == null)
                throw new ArgumentNullException("A");
            if (B == null)
                throw new ArgumentNullException("B"); 

            double dX = A.X - B.X;
            double dY = A.Y - B.Y;

            return (dX * dX) + (dY * dY);
        }

        /// <summary>
        /// Returns dot product of two vectors. Input is rounded to 2 decimal places because of problems I had with double size limit
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        static public double Dot(GridVector2 A, GridVector2 B)
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
        /// Angle of arc between A & B with Origin
        /// </summary>
        /// <param name="Origin"></param>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        static public double ArcAngle(GridVector2 Origin, GridVector2 A, GridVector2 B)
        {
            A = A - Origin;
            B = B - Origin;
            double AngleA = Math.Atan2(A.Y, A.X);
            double AngleB = Math.Atan2(B.Y, B.X);

            double Angle = AngleB - AngleA;

            if (Angle < -Math.PI)
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
        static public double ArcAngle(IPoint2D Origin, IPoint2D A, IPoint2D B)
        {
            A = new GridVector2(A.X - Origin.X, A.Y - Origin.Y);
            B = new GridVector2(B.X - Origin.X, B.Y - Origin.Y);
            double AngleA = Math.Atan2(A.Y, A.X);
            double AngleB = Math.Atan2(B.Y, B.X);

            double Angle = AngleB - AngleA;

            if (Angle < -Math.PI)
                Angle += Math.PI * 2;
            else if (Angle > Math.PI)
                Angle -= Math.PI * 2;

            return Angle;
        }

        /// <summary>
        /// Angle to B from A
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        static public double Angle(GridVector2 A, GridVector2 B)
        {
            GridVector2 delta = B - A;
            return Math.Atan2(delta.Y, delta.X);
        }
        
        static public GridVector2 operator -(GridVector2 A)
        {
            return new GridVector2(-A.X, -A.Y); 
        }

        static public GridVector2 operator -(GridVector2 A, GridVector2 B)
        {
            return new GridVector2(A.X - B.X, A.Y - B.Y); 
        }

        static public GridVector2 operator +(GridVector2 A, GridVector2 B)
        {
            return new GridVector2(A.X + B.X, A.Y + B.Y); 
        }

        static public GridVector2 operator -(GridVector2 A, IPoint2D B)
        {
            return new GridVector2(A.X - B.X, A.Y - B.Y);
        }

        static public GridVector2 operator +(GridVector2 A, IPoint2D B)
        {
            return new GridVector2(A.X + B.X, A.Y + B.Y);
        }

        static public GridVector2 operator *(GridVector2 A, double scalar)
        {
            return new GridVector2(A.X * scalar, A.Y * scalar);
        }

        static public GridVector2 operator *(GridVector2 A, GridVector2 B)
        {
            return new GridVector2(A.X * B.X, A.Y * B.Y);
        }

        static public GridVector2 operator /(GridVector2 A, double scalar)
        {
            return new GridVector2(A.X / scalar, A.Y / scalar);
        }

        static public GridVector2 operator /(GridVector2 A, GridVector2 B)
        {
            return new GridVector2(A.X / B.X, A.Y / B.Y);
        }

        static public bool operator ==(GridVector2 A, GridVector2 B)
        {
            return GridVector2.Equals(A, B); 
        }

        static public bool operator !=(GridVector2 A, GridVector2 B)
        {
            return !GridVector2.Equals(A, B); 
        }

        static public bool operator ==(GridVector2 A, IPoint2D B)
        {
            return GridVector2.Equals(A, B);
        }

        static public bool operator !=(GridVector2 A, IPoint2D B)
        {
            return !GridVector2.Equals(A, B);
        }

        static public GridVector2 FromBarycentric(GridVector2 v1, GridVector2 v2, GridVector2 v3, double u, double v)
        {
            double x = (v1.X * (1 - u - v)) + (v2.X * u) + (v3.X * v);
            double y = (v1.Y * (1 - u - v)) + (v2.Y * u) + (v3.Y * v);
            return new GridVector2(x, y); 
        }

        public static GridVector2 Scale(GridVector2 A, double scalar)
        {
            return new GridVector2(A.X * scalar, A.Y * scalar);
        }

        public void Scale(double scalar)
        {
            X = X * scalar;
            Y = Y * scalar; 
        }

        public static GridRectangle Border(GridVector2[] points)
        {
            return points.BoundingBox();
        }

        public static GridRectangle Border(IPoint[] points)
        {
            if (points == null)
            {
                throw new ArgumentNullException("GridRectangle Border");
            }

            if (points.Length == 0)
                throw new ArgumentException("GridRectangle Border"); 

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

        bool IShape2D.Contains(IPoint2D p)
        {
            return p.X == this.X && p.Y == this.Y;
        }

        bool IShape2D.Intersects(IShape2D shape)
        {
            return shape.Contains(this);
        }

        IShape2D IShape2D.Translate(IPoint2D offset)
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
        public static int IsLeftSide(GridVector2 t, GridVector2[] pqr)
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
            get
            {
                return X; 
            }
            set
            {
                X = value; 
            }
        }

        double IPoint2D.Y
        {
            get
            {
                return Y; 
            }
            set
            {
                Y = value; 
            }
        }

        double IPoint.Z
        {
            get
            {
                return 0;
            }
            set
            {

            }
        }

        GridRectangle IShape2D.BoundingBox
        {
            get
            {
                return new GridRectangle(this, 0, 0);
            }
        }

        double IShape2D.Area
        {
            get
            {
                return 0;
            }
        }

        ShapeType2D IShape2D.ShapeType
        {
            get
            {
                return ShapeType2D.POINT;
            }
        }
         

        #endregion


    }
}
