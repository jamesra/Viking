using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// Sorts points on the axis in order
    /// </summary>
    public class AxisComparer : IComparer<IPointN>
    {
        private readonly int[] AxisCompareOrder = null;
        private readonly bool[] Ascending = null; //True if the axis should be sorted in ascending order

        /// <summary>
        /// Defaults to comparing the axis values in the order they appear in the points coordinate array. i.e. X,Y,Z..
        /// </summary>
        public AxisComparer()
        {
        }

        /// <summary>
        /// Defaults to comparing the axis values in the order they appear in the points coordinate array. i.e. X,Y,Z..
        /// </summary>
        public AxisComparer(AXIS[] axisCompareOrder)
        {
            AxisCompareOrder = axisCompareOrder.Cast<int>().ToArray();
        }

        /// <summary>
        /// This constructor allows the order that axes are compared in
        /// </summary>
        /// <param name="axisCompareOrder"></param>
        public AxisComparer(int[] axisCompareOrder) : this(axisCompareOrder, axisCompareOrder.Select(a => true).ToArray())
        {
        }

        public AxisComparer(int[] axisCompareOrder, bool[] axisAscending)
        {
            Debug.Assert(AxisCompareOrder.Length == axisAscending.Length);

            AxisCompareOrder = new int[axisCompareOrder.LongLength];
            axisCompareOrder.CopyTo(AxisCompareOrder, 0);

            Ascending = new bool[axisAscending.LongLength];
            axisAscending.CopyTo(Ascending, 0);

        }

        public int Compare(IPointN A, IPointN B)
        {
#if DEBUG
            if (A.coords.LongLength != B.coords.LongLength)
                throw new ArgumentException("Dimensions of compared points must match."); //But do they really?  Or should we just compare what we can...

            if (AxisCompareOrder != null && AxisCompareOrder.LongLength != A.coords.LongLength)
            {
                throw new ArgumentException("Custom axis compare order must match dimensionality of passed points"); //But do they really?  Or should we just compare what we can...
            }
#endif  
            //This comparison is a bit contorted, but we need to use the same equality test as our epsilon value to be consistent with the rest of the code

            double[] diff = new double[A.coords.LongLength];
            for (long i = 0; i < A.coords.LongLength; i++)
            {
                long iAxis = AxisCompareOrder != null ? AxisCompareOrder[i] : i;
                diff[i] = Ascending[iAxis] ? A.coords[iAxis] - B.coords[iAxis] : B.coords[iAxis] - A.coords[iAxis];

                //We need to use the same equality test as our epsilon value, so check against epsilon
                if (Math.Abs(diff[i]) <= Global.Epsilon)
                    continue;

                return diff[i] > 0 ? 1 : -1;
            }

            //Edge case: If we reach this point none of the diff values was greater than our epsilon threshold.  Check for outright equality.
            if (A.Equals(B))
                return 0;

            for (long i = 0; i < diff.LongLength; i++)
            {
                if (diff[i] != 0)
                    return diff[i] > 0 ? 1 : -1;
            }

            return 0;
        }
    }

    [Serializable]
    public struct GridVectorN : ICloneable, IComparable, IPointN,
                                IComparable<GridVectorN>, IComparer<GridVectorN>, IEquatable<GridVectorN>,
                                IComparable<IPointN>, IComparer<IPointN>, IEquatable<IPointN>, IEquatable<IShape2D>
    {
        readonly double[] _coords;

        public int nDims { get => _coords.Length; }

        public double[] coords { get => _coords; }

        public GridVectorN(double[] input)
        {
            //Make sure we copy so we don't take a reference on the array
            _coords = new double[input.Length];
            input.CopyTo(_coords, 0);
        }

        public GridVectorN(IEnumerable<double> input)
        {
            //Make sure we copy so we don't take a reference on the array
            _coords = input.ToArray();
        }

        private static void ThrowOnDimensionMismatch(IPointN A, IPointN B)
        {
            if (A.coords.Length != B.coords.Length)
            {
                throw new ArgumentException(string.Format("Both points must have the same dimensions. {0} vs {1}", A, B));
            }
        }

        public bool Equals(GridVectorN B)
        {  
            return (DistanceSquared(this, B) <= Global.EpsilonSquared);
        }

        public bool Equals(IPointN B)
        {
            if (object.ReferenceEquals(B, null))
                return false;

            return (DistanceSquared(this, B) <= Global.EpsilonSquared);
        }

        public override bool Equals(object obj)
        {
            if (obj is GridVectorN otherGVN)
                return Equals(otherGVN);
            if (obj is IPointN otherIPN)
                return Equals(otherIPN);

            return false;
        }

        /// <summary>
        /// The block distance along each axis between the two points
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns>Array of distances along each axis</returns>
        static public double[] Diff(IPointN A, IPointN B)
        {
            ThrowOnDimensionMismatch(A, B);

            double[] diff = new double[A.coords.Length];

            for (int iAxis = 0; iAxis < A.coords.Length; iAxis++)
            {
                diff[iAxis] = A.coords[iAxis] - B.coords[iAxis];
            }

            return diff;
        }

        static public double DistanceSquared(IPointN A, IPointN B)
        {
            ThrowOnDimensionMismatch(A, B);

            return Diff(A, B).Select(dist => dist * dist).Sum();
        }

        static public double Magnitude(IPointN A)
        {
            return Math.Sqrt(A.coords.Select(val => val * val).Sum());
        }

        public void Normalize()
        {
            double mag = Magnitude(this);
            for (int iAxis = 0; iAxis < nDims; iAxis++)
            {
                _coords[iAxis] = _coords[iAxis] / mag;
            }
        }

        static public GridVectorN Normalize(IPointN A)
        {
            double mag = Magnitude(A);

            double[] normalized = A.coords.Select(val => val / mag).ToArray();
            return new GridVectorN(normalized);
        }

        public object Clone()
        {
            double[] cpy = new double[this.nDims];
            coords.CopyTo(cpy, 0);
            return new GridVectorN(cpy);
        }

        public int CompareTo(GridVectorN other)
        {
            return Compare((IPointN)this, (IPointN)other);
        }

        public int Compare(GridVectorN A, GridVectorN B)
        {
            return Compare((IPointN)A, (IPointN)B);
        }

        public int CompareTo(IPointN other)
        {
            return Compare((IPointN)this, (IPointN)other);
        }

        public int Compare(IPointN A, IPointN B)
        {
            ThrowOnDimensionMismatch(A, B);

            //We need to use the same equality test as our epsilon value
            if (A.Equals(B))
                return 0;

            for (int iAxis = 0; iAxis < _coords.Length; iAxis++)
            {
                double diff = A.coords[iAxis] - B.coords[iAxis];
                if (Math.Abs(diff) <= Global.Epsilon)
                    continue;

                return diff > 0 ? 1 : -1;
            }

            return 0;
        }

        public bool Equals(IShape2D other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            IPointN p = other as IPointN;
            return ((IEquatable<IPointN>)this).Equals(p);

        }

        public int CompareTo(object obj)
        {
            IPointN B = (IPointN)obj;

            return Compare(this, B);
        }

        public static GridVectorN operator -(GridVectorN A)
        {
            return new GridVectorN(A._coords.Select(val => -val).ToArray());
        }

        public static GridVectorN operator -(GridVectorN A, GridVectorN B)
        {
            return new GridVectorN(A._coords.Select((val, i) => val - B._coords[i]).ToArray());
        }

        public static GridVectorN operator +(GridVectorN A, GridVectorN B)
        {
            return new GridVectorN(A._coords.Select((val, i) => val + B._coords[i]).ToArray());
        }

        public static GridVectorN operator *(GridVectorN A, double scalar)
        {
            return new GridVectorN(A._coords.Select((val, i) => val * scalar).ToArray());
        }

        public static GridVectorN operator *(GridVectorN A, GridVectorN B)
        {
            return new GridVectorN(A._coords.Select((a, i) => a * B._coords[i]).ToArray());
        }

        public static GridVectorN operator /(GridVectorN A, double scalar)
        {
            return new GridVectorN(A._coords.Select((val, i) => val / scalar).ToArray());
        }

        public static GridVectorN operator /(GridVectorN A, GridVectorN B)
        {
            return new GridVectorN(A._coords.Select((a, i) => a / B._coords[i]).ToArray());
        }

        /*
        #region IPointN operators

        static public GridVectorN operator -(IPointN A)
        {
            return new GridVectorN(A.coords.Select(val => -val).ToArray());
        }

        static public GridVectorN operator -(IPointN A, IPointN B)
        {
            return new GridVectorN(A.coords.Select((val, i) => val - B.coords[i]).ToArray());
        }

        static public GridVectorN operator +(IPointN A, IPointN B)
        {
            return new GridVectorN(A.coords.Select((val, i) => val + B.coords[i]).ToArray());
        }

        static public GridVectorN operator *(IPointN A, double scalar)
        {
            return new GridVectorN(A.coords.Select((val, i) => val * scalar).ToArray());
        }

        static public GridVectorN operator *(IPointN A, IPointN B)
        {
            return new GridVectorN(A.coords.Select((a, i) => a * B.coords[i]).ToArray());
        }

        static public GridVectorN operator /(IPointN A, double scalar)
        {
            return new GridVectorN(A.coords.Select((val, i) => val / scalar).ToArray());
        }

        static public GridVectorN operator /(IPointN A, IPointN B)
        {
            return new GridVectorN(A.coords.Select((a, i) => a / B.coords[i]).ToArray());
        }
        
        #endregion
        */

        public static bool operator ==(GridVectorN A, GridVectorN B)
        {
            return A.Equals(B);
        }

        public static bool operator !=(GridVectorN A, GridVectorN B)
        {
            return !A.Equals(B);
        }

        public static bool operator ==(GridVectorN A, IPointN B)
        {
            return A.Equals(B);
        }

        public static bool operator !=(GridVectorN A, IPointN B)
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
    }
}
