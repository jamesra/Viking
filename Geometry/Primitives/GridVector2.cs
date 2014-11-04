using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry
{
    public interface IPoint
    {
        double X { get; set; }
        double Y { get; set; }
        double Z { get; set; }
    }

    [Serializable]
    public struct GridVector2 : IPoint, ICloneable, IComparable, IComparable<GridVector2>, IComparer<GridVector2>
    {
        public double X;
        public double Y;
        
        public GridVector2(double x, double y)
        {
            this.X = x;
            this.Y = y; 
        }

        /// <summary>
        /// Compares two vectors, assumes they have the same position if they are within the epsilon squared distance of each other
        /// </summary>
        /// <param name="B"></param>
        /// <param name="Epsilon"></param>
        /// <returns></returns>
        public bool Equals(GridVector2 B, double EpsilonSquared = 0.00001)
        {
            return (DistanceSquared(this, B) <= EpsilonSquared);
        }

        /// <summary>
        /// Compares two vectors, assumes they have the same position if they are within the epsilon squared distance of each other
        /// </summary>
        /// <param name="B"></param>
        /// <param name="Epsilon"></param>
        /// <returns></returns>
        public static bool Equals(GridVector2 A, GridVector2 B, double EpsilonSquared = 0.00001)
        {
            return (DistanceSquared(A, B) <= EpsilonSquared);
        }

        public int Compare(GridVector2 A, GridVector2 B)
        {
            double diff = A.X - B.X;

            if (diff == 0.0)
            {
                diff = A.Y - B.Y;
            }

            if (diff > 0)
                return 1;
            if (diff < 0)
                return -1;

            return 0; 
        }

        int IComparable.CompareTo(Object Obj)
        {
            GridVector2 B = (GridVector2)Obj;

            return this.Compare(this, B);
        }

        int IComparable<GridVector2>.CompareTo(GridVector2 B)
        {
            return this.Compare(this, B);
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
            GridVector2 B = (GridVector2)obj;

            return this == B; 
        }

        public override string ToString()
        {
            return "X: " + X.ToString("F2") + "\t Y: " + Y.ToString("F2");
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

        static public double DistanceSquared(IPoint A, IPoint B)
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

        static public double Angle(GridVector2 Origin, GridVector2 A, GridVector2 B)
        {
            A = A - Origin;
            B = B - Origin;
            return Angle(A, B); 
        }

        static public double Angle(GridVector2 A, GridVector2 B)
        {
            double AngleA = Math.Atan2(A.Y, A.X);
            double AngleB = Math.Atan2(B.Y, B.X);

            return AngleB - AngleA; 
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

        static public GridVector2 operator *(GridVector2 A, double scalar)
        {
            return new GridVector2(A.X * scalar, A.Y * scalar);
        }

        static public GridVector2 operator /(GridVector2 A, double scalar)
        {
            return new GridVector2(A.X / scalar, A.Y / scalar);
        }

        static public bool operator ==(GridVector2 A, GridVector2 B)
        {
            return GridVector2.Equals(A, B); 
        }

        static public bool operator !=(GridVector2 A, GridVector2 B)
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
            if(points == null)
                throw new ArgumentNullException("points");

            if (points.Length == 0)
                throw new ArgumentException("GridRectangle Border is empty", "points"); 

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

        #region IPoint Members

        double IPoint.X
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

        double IPoint.Y
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

        #endregion

        
    }
}
