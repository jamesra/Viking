using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry
{
    [Serializable]
    public struct GridVector3 : IPoint, ICloneable, IComparable
    {
        public readonly static GridVector3 UnitX = new GridVector3(1, 0, 0);
        public readonly static GridVector3 UnitY = new GridVector3(0, 1, 0);
        public readonly static GridVector3 UnitZ = new GridVector3(0, 0, 1);

        public double X;
        public double Y;
        public double Z;

        public GridVector3(int index, double x, double y, double z) : this(x,y,z)
        {
        }

        public GridVector3(double x, double y, double z)
        {
            this.X = x;
            this.Y = y; 
            this.Z = z; 
        }

        int IComparable.CompareTo(object Obj)
        {
            GridVector3 B = (GridVector3)Obj;
          
            double diff = this.X - B.X;

            if (diff == 0.0)
            {
                diff = this.Y - B.Y;
                if (diff == 0.0)
                {
                    diff = this.Z - B.Z; 
                }
            }

            if (diff > 0)
                return 1;
            if (diff < 0)
                return -1;

            return 0; 
        }

        object ICloneable.Clone()
        {
            return new GridVector3(X, Y, Z); 
        }

        public override int GetHashCode()
        {
            double prod = X * Y * Z;
            double code = Math.Abs(prod);
            if (code < 1)
            {
                return (int)(1.0 / code);
            }
            return (int)prod; 
        }

        public override bool Equals(object obj)
        {
            GridVector3 B = (GridVector3)obj;

            return this == B;
        }

        public override string ToString()
        {
            return "X: " + X.ToString("F2") + "\t Y: " + Y.ToString("F2") + "\t Z: " + Z.ToString("F2");
        }

        public static string ToMatlab(GridVector3[] array)
        {
            string s = "[";
            for (int i = 0; i < array.Length; i++)
            {
                s += array[i].X.ToString("F2") + " " + array[i].Y.ToString("F2") + " " + array[i].Z.ToString("F2") + ";" + System.Environment.NewLine;
            }
            s += "]";

            return s;
        }

        static public double Magnitude(GridVector3 A)
        {
            return Math.Sqrt((A.X * A.X) + (A.Y * A.Y) + (A.Z * A.Z));
        }

        public void Normalize()
        {
            double mag = Magnitude(this);
            X = X / mag;
            Y = Y / mag;
            Z = Z / mag;
        }

        static public GridVector3 Normalize(GridVector3 A)
        {
            double mag = Magnitude(A);
            return new GridVector3(A.X / mag, A.Y / mag, A.Z / mag); 
        }

        static public double Distance(GridVector3 A, GridVector3 B)
        {
            double dX = A.X - B.X; 
            double dY = A.Y - B.Y;
            double dZ = A.Z - B.Z; 

            return Math.Sqrt((dX*dX)+(dY*dY)+(dZ*dZ));
        }

        static public double Distance(IPoint A, IPoint B)
        {
            if (A == null || B == null)
                throw new ArgumentNullException("A or B"); 

            double dX = A.X - B.X;
            double dY = A.Y - B.Y;
            double dZ = A.Z - B.Z;

            return Math.Sqrt((dX * dX) + (dY * dY) + (dZ * dZ));
        }

        /// <summary>
        /// Returns dot product of two vectors. Input is rounded to 2 decimal places because of problems I had with double size limit
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        static public double Dot(GridVector3 A, GridVector3 B)
        {
            /*
            double AX = Math.Round(A.X, 3);
            double AY = Math.Round(A.Y, 3);
            double BX = Math.Round(B.X, 3);
            double BY = Math.Round(B.Y, 3);
             */

            double AX = (double)(float)A.X;
            double AY = (double)(float)A.Y;
            double AZ = (double)(float)A.Z;

            double BX = (double)(float)B.X;
            double BY = (double)(float)B.Y;
            double BZ = (double)(float)A.Z;

            return (AX * BX) + (AY * BY) + (AZ * BZ);
        }
        
        /*
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
        */

        static public GridVector3 operator -(GridVector3 A)
        {
            return new GridVector3(-A.X, -A.Y, A.Z); 
        }

        static public GridVector3 operator -(GridVector3 A, GridVector3 B)
        {
            return new GridVector3(A.X - B.X, A.Y - B.Y, A.Z - B.Z); 
        }

        static public GridVector3 operator +(GridVector3 A, GridVector3 B)
        {
            return new GridVector3(A.X + B.X, A.Y + B.Y, A.Z + B.Z); 
        }

        static public GridVector3 operator *(GridVector3 A, double scalar)
        {
            return new GridVector3(A.X * scalar, A.Y * scalar, A.Z * scalar);
        }

        static public GridVector3 operator /(GridVector3 A, double scalar)
        {
            return new GridVector3(A.X / scalar, A.Y / scalar, A.Z / scalar);
        }

        static public bool operator ==(GridVector3 A, GridVector3 B)
        {
            return (A.X == B.X && A.Y == B.Y && A.Z == B.Z);
        }

        static public bool operator !=(GridVector3 A, GridVector3 B)
        {
            return !(A.X == B.X && A.Y == B.Y && A.Z == B.Z);
        }

        static public GridVector3 FromBarycentric(GridVector3 v1, GridVector3 v2, GridVector3 v3, double u, double v)
        {
            double x = (v1.X * (1 - u - v)) + (v2.X * u) + (v3.X * v);
            double y = (v1.Y * (1 - u - v)) + (v2.Y * u) + (v3.Y * v);
            double z = (v1.Y * (1 - u - v)) + (v2.Y * u) + (v3.Y * v);
            return new GridVector3(x, y, z); 
        }

        public static GridVector3 Scale(GridVector3 A, double scalar)
        {
            return new GridVector3(A.X * scalar, A.Y * scalar, A.Z * scalar);
        }

        public void Scale(double scalar)
        {
            X = X * scalar;
            Y = Y * scalar;
            Z = Z * scalar; 
        }
        /*
        public static GridRectangle Border(GridVector3[] points)
        {
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
        */
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
