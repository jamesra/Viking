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
        public readonly static GridVector3 Zero  = new GridVector3(0, 0, 0);

        public double[] coords;

        public double X { get { return coords[(int)AXIS.X]; } }
        public double Y { get { return coords[(int)AXIS.Y]; } }
        public double Z { get { return coords[(int)AXIS.Z]; } }

        public GridVector3(double[] new_coords)
        {
            this.coords = new_coords;
        }

        public GridVector3(double x, double y, double z)
        {
            this.coords = new double[] { x, y, z };
        }

        int IComparable.CompareTo(object Obj)
        {
            GridVector3 B = (GridVector3)Obj;

            double[] axisdiff = this.coords.Select((val, i) => val - B.coords[i]).ToArray();

            for (int iAxis = 0; iAxis < axisdiff.Count(); iAxis++)
            {
                if (axisdiff[iAxis] == 0.0)
                    continue;
                else
                    return axisdiff[iAxis] > 0 ? 1 : -1;
            }

            return 0; 
        }

        object ICloneable.Clone()
        {
            double[] coord_copy = new double[coords.Count()];
            coords.CopyTo(coord_copy, 0);
            return new GridVector3(coord_copy); 
        }

        public override int GetHashCode()
        {
            double prod = this.coords.Aggregate((accumulator, val) => accumulator * val);
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
            return coords.ToCSV();
        }

        public static string ToMatlab(GridVector3[] array)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < array.Length; i++)
            {
                sb.Append(array[i].coords.ToCSV(" "));
                sb.AppendLine(";");
            }
            sb.Append(']');

            return sb.ToString();
        }

        static public double Magnitude(GridVector3 A)
        {
            double[] squares = A.coords.Select(val => val * val).ToArray();
            return Math.Sqrt(squares.Sum());
        }

        public void Normalize()
        {
            double mag = Magnitude(this);
            double[] normalized = this.coords.Select(val => val / mag).ToArray();
            this.coords = normalized;
        }

        static public GridVector3 Normalize(GridVector3 A)
        {
            double mag = Magnitude(A);
            double[] normalized = A.coords.Select(val => val / mag).ToArray();
            return new GridVector3(normalized);
        }

        static public double Distance(GridVector3 A, GridVector3 B)
        {
            double[] diff = A.coords.Select((Aval, i) => Aval - B.coords[i]).ToArray();
            return Math.Sqrt(diff.Sum((val) => (val * val)));
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
            return A.coords.Select((val, i) => val * B.coords[i]).Sum();
            /*
            double AX = (double)(float)A.X;
            double AY = (double)(float)A.Y;
            double AZ = (double)(float)A.Z;

            double BX = (double)(float)B.X;
            double BY = (double)(float)B.Y;
            double BZ = (double)(float)A.Z;

            return (AX * BX) + (AY * BY) + (AZ * BZ);
            */
        }

        /// <summary>
        /// Return the cross product of (B - A) X (C - A)
        /// </summary>
        /// <param name="A"></param>
        /// <param name="C"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        static public GridVector3 Cross(GridVector3 A, GridVector3 B, GridVector3 C)
        {
            /*
            double[,] m = new double[,] { { 1,1,1},
                                          { AB.coords[0], AB.coords[1], AB.coords[2] },
                                          { AC.coords[0], AC.coords[1], AC.coords[2]} };

            var matrix = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.DenseOfArray(m);
            */

            GridVector3 AB = B - A;
            GridVector3 AC = C - A;

            return Cross(AB, AC);
        }

        static public GridVector3 Cross(GridVector3 AB, GridVector3 AC)
        { 
            double X = (AB.Y * AC.Z) - (AC.Y * AB.Z);
            double Y = (AB.Z * AC.X) - (AC.Z * AB.X);
            double Z = (AB.X * AC.Y) - (AC.X * AB.Y);

            return new GridVector3(X, Y, Z);
        }
          
        /// <summary>
        /// Angle to B from A
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        static public double Angle(GridVector3 VectorA, GridVector3 VectorB)
        {
            double dot = Dot(VectorA, VectorB);
            return Math.Acos(dot / (Magnitude(VectorA) * Magnitude(VectorB)));
        }

        /// <summary>
        /// Angle to B from A
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        static public double ArcAngle(GridVector3 Origin, GridVector3 A, GridVector3 B)
        {
            A = A - Origin;
            B = B - Origin;
            return Angle(A, B);
        }

        static public GridVector3 operator -(GridVector3 A)
        {
            return new GridVector3(A.coords.Select(val => -val).ToArray());
        }

        static public GridVector3 operator -(GridVector3 A, GridVector3 B)
        {
            return new GridVector3(A.coords.Select((val, i) => val - B.coords[i]).ToArray());
        }

        static public GridVector3 operator +(GridVector3 A, GridVector3 B)
        {
            return new GridVector3(A.coords.Select((val, i) => val + B.coords[i]).ToArray());
        }

        static public GridVector3 operator *(GridVector3 A, double scalar)
        {
            return new GridVector3(A.coords.Select((val, i) => val * scalar).ToArray());
        }

        static public GridVector3 operator /(GridVector3 A, double scalar)
        {
            return new GridVector3(A.coords.Select((val, i) => val / scalar).ToArray());
        }

        static public bool operator ==(GridVector3 A, GridVector3 B)
        {
            return A.coords.Select((val, i) => val == B.coords[i]).All(result => result);
        }

        static public bool operator !=(GridVector3 A, GridVector3 B)
        {
            return !A.coords.Select((val, i) => val == B.coords[i]).All(result => result);
        }

        static public GridVector3 FromBarycentric(GridVector3 v1, GridVector3 v2, GridVector3 v3, double u, double v)
        {
            double[] coords = v1.coords.Select((v1Val, i) => (v1Val * (1 - u - v)) + (v2.coords[i] * u) + (v3.coords[i] * v)).ToArray();

            throw new NotImplementedException("GridVector3::FromBarycentric");
            /* I think the Z calculation is incorrect */
            /*
            double x = (v1.X * (1 - u - v)) + (v2.X * u) + (v3.X * v);
            double y = (v1.Y * (1 - u - v)) + (v2.Y * u) + (v3.Y * v);
            double z = (v1.Y * (1 - u - v)) + (v2.Y * u) + (v3.Y * v);
            */
            return new GridVector3(coords); 
        }

        public static GridVector3 Scale(GridVector3 A, double scalar)
        {
            return new GridVector3(A.coords.Select((val,i) => val * scalar).ToArray());
        }

        public void Scale(double scalar)
        {
            this.coords = this.coords.Select(val => val * scalar).ToArray();
        }
        
        public static GridBox BoundingBox(GridVector3[] points)
        {
            return GridBox.GetBoundingBox(points); 
        } 
        
        #region IPoint Members

        double IPoint2D.X
        {
            get
            {
                return coords[(int)AXIS.X];
            }
            set
            {
                coords[(int)AXIS.X] = value; 
            }
        }

        double IPoint2D.Y
        {
            get
            {
                return coords[(int)AXIS.Y]; 
            }
            set
            {
                coords[(int)AXIS.Y] = value; 
            }
        }

        double IPoint.Z
        {
            get
            {
                return coords[(int)AXIS.Z];
            }
            set
            {
                coords[(int)AXIS.Z] = value;
            }
        }

        #endregion
    }
}
