using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Geometry
{
    [Serializable]
    public struct GridVector3 : IPoint, ICloneable, IComparable, IEquatable<GridVector3>
    {
        public static double EpsilonSquared = 0.00001;

        //Return a new instance for the static in case someone writes back to the value somewhere

        /*
        public static GridVector3 UnitX { get { return new GridVector3(1, 0, 0); } }
        public static GridVector3 UnitY { get { return new GridVector3(0, 1, 0); } }
        public static GridVector3 UnitZ { get { return new GridVector3(1, 0, 1); } }
        public static GridVector3 Zero { get { return new GridVector3(0, 0, 0); } }
        */

        public readonly static GridVector3 UnitX = new GridVector3(1, 0, 0);
        public readonly static GridVector3 UnitY = new GridVector3(0, 1, 0); 
        public readonly static GridVector3 UnitZ = new GridVector3(1, 0, 1); 
        public readonly static GridVector3 Zero = new GridVector3(0, 0, 0); 


        public double[] coords { get => _coords; } 
        private readonly double[] _coords;

        public double X { get => _coords[(int)AXIS.X]; }
        public double Y { get => _coords[(int)AXIS.Y]; }
        public double Z { get =>_coords[(int)AXIS.Z]; } 

        public GridVector3(double[] input)
        {
            Debug.Assert(input != null, "Passed null to GridVector3(double[]) constructor");
            Debug.Assert(input.Length != 3, string.Format("Passing an array of length {0} to GridVector3 constructor, expected 3 elements", input.Length));
            _coords = new double[3];
            Array.Copy(input, _coords, 3);
        }

        public GridVector3(IEnumerable<double> input)
        {
            Debug.Assert(input != null, "Passed null to GridVector3(IEnumerable<double>) constructor");
            //Make sure we copy so we don't take a reference on the array
            _coords = input.ToArray();
            Debug.Assert(_coords.Length == 3, string.Format("Passing an IEnumerable<double> of count {0} to GridVector3 constructor, expected 3 elements", _coords.Length));            
        }

        public GridVector3(double x, double y, double z)
        {
            this._coords = new double[] { x, y, z };
        }

        public void Deconstruct(out double x, out double y, out double z)
        {
            x = X;
            y = Y;
            z = Z; 
        }

        int IComparable.CompareTo(object Obj)
        {
            GridVector3 B = (GridVector3)Obj;

            //Check for direct equality to account for epsilon scale differences
            if (this.Equals(B))
                return 0; 

            double[] axisdiff = this._coords.Select((val, i) => val - B._coords[i]).ToArray();

            for (int iAxis = 0; iAxis < axisdiff.Length; iAxis++)
            {
                if (Math.Abs(axisdiff[iAxis]) <= Global.Epsilon)
                    continue;
                else
                    return axisdiff[iAxis] > 0 ? 1 : -1;
            }

            return 0; 
        }

        object ICloneable.Clone()
        { 
            var newObj = new GridVector3(_coords); //The current constructor will take care of copying the array
            System.Diagnostics.Debug.Assert(object.ReferenceEquals(this._coords, newObj._coords) == false);
            return newObj;
        }

        public override int GetHashCode()
        {
            double prod = this._coords.Aggregate((accumulator, val) => accumulator * val);
            double code = Math.Abs(prod);
            if (code < 1)
            {
                return (int)(1.0 / code);
            }
            return (int)prod; 
        }

        public bool Equals(IPoint2D other)
        {
            return false;
        }

        public bool Equals(IPoint other)
        {
            return Distance(this, other) <= EpsilonSquared;
        }

        public override bool Equals(object obj)
        {
            if (obj is GridVector3 other)
                return Equals(other);

            if (obj is IPoint iOther)
                return Equals(iOther);

            return false;
        }

        public bool Equals(GridVector3 B)
        {
            return GridVector3.Distance(this, B) <= EpsilonSquared;
        }

        public override string ToString()
        {
            return _coords.ToCSV();
        }

        public static string ToMatlab(GridVector3[] array)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < array.Length; i++)
            {
                sb.Append(array[i]._coords.ToCSV(" "));
                sb.AppendLine(";");
            }
            sb.Append(']');

            return sb.ToString();
        }

        static public double Magnitude(GridVector3 A)
        {
            return GridVectorN.Magnitude(A);
        }

        public void Normalize()
        {
            double mag = Magnitude(this);
            if (mag == 0)
                return; 

            for (int iAxis = 0; iAxis < _coords.Length; iAxis++)
            {
                _coords[iAxis] = _coords[iAxis] / mag;
            }
        }

        static public GridVector3 Normalize(GridVector3 A)
        {
            double mag = Magnitude(A); 
            return new GridVector3(A._coords.Select(val => val / mag));
        }

        static public double Distance(GridVector3 A, GridVector3 B)
        {
            Debug.Assert(A.coords.Length == B.coords.Length);
            double[] diff = A._coords.Select((Aval, i) => Aval - B._coords[i]).ToArray();
            return Math.Sqrt(diff.Sum((val) => (val * val)));
        }

        static public double Distance(IPoint A, IPoint B)
        {
            if (A == null || B == null)
                throw new ArgumentNullException("A or B");

            return Math.Sqrt(GridVectorN.DistanceSquared(A, B));
            /*
            double dX = A.X - B.X;
            double dY = A.Y - B.Y;
            double dZ = A.Z - B.Z;

            return Math.Sqrt((dX * dX) + (dY * dY) + (dZ * dZ));
            */
        }

        /// <summary>
        /// Returns dot product of two vectors. Input is rounded to 2 decimal places because of problems I had with double size limit
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        static public double Dot(GridVector3 A, GridVector3 B)
        {
            return A._coords.Select((val, i) => val * B._coords[i]).Sum();
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
            return new GridVector3(-A.X, -A.Y, -A.Z);
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

        static public GridVector3 operator *(GridVector3 A, GridVector3 B)
        {
            return new GridVector3(A.X * B.X, A.Y * B.Y, A.Z * B.Z);
        }

        static public GridVector3 operator /(GridVector3 A, double scalar)
        {
            return new GridVector3(A.X / scalar, A.Y / scalar, A.Z / scalar);
        }

        static public GridVector3 operator /(GridVector3 A, GridVector3 B)
        {
            return new GridVector3(A.X / B.X, A.Y / B.Y, A.Z / B.Z);
        }

        static public bool operator ==(GridVector3 A, GridVector3 B)
        {
            return A.Equals(B);
        }

        static public bool operator !=(GridVector3 A, GridVector3 B)
        {
            return !A.Equals(B);
        }

        public double this[AXIS axis]
        {
            get
            {
                return coords[(int)axis];
            }
            set
            {
                coords[(int)axis] = value;
            }
        }

        static public GridVector3 FromBarycentric(GridVector3 v1, GridVector3 v2, GridVector3 v3, double u, double v)
        {
            double[] coords = v1._coords.Select((v1Val, i) => (v1Val * (1 - u - v)) + (v2._coords[i] * u) + (v3._coords[i] * v)).ToArray();

            throw new NotImplementedException("GridVector3::FromBarycentric");
            /* I think the Z calculation is incorrect */
            /*
            double x = (v1.X * (1 - u - v)) + (v2.X * u) + (v3.X * v);
            double y = (v1.Y * (1 - u - v)) + (v2.Y * u) + (v3.Y * v);
            double z = (v1.Y * (1 - u - v)) + (v2.Y * u) + (v3.Y * v);
            */
            //return new GridVector3(coords); 
        }

        public static GridVector3 Scale(GridVector3 A, double scalar)
        {
            return new GridVector3(A._coords.Select((val,i) => val * scalar));
        }

        public GridVector3 Scale(double scalar)
        {
            return new GridVector3(this._coords.Select(val => val * scalar));
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
                return _coords[(int)AXIS.X];
            }
            set
            {
                _coords[(int)AXIS.X] = value; 
            }
        }

        double IPoint2D.Y
        {
            get
            {
                return _coords[(int)AXIS.Y]; 
            }
            set
            {
                _coords[(int)AXIS.Y] = value; 
            }
        }

        double IPoint.Z
        {
            get
            {
                return _coords[(int)AXIS.Z];
            }
            set
            {
                _coords[(int)AXIS.Z] = value;
            }
        }

        #endregion

        IPoint2D ICentroid.Centroid => this.XY();
    }
}
