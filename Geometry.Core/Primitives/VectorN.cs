using System;
using System.Collections;
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
        public AxisComparer(Axis[] axisCompareOrder)
        {
            AxisCompareOrder = [.. axisCompareOrder.Cast<int>()];
        }

        /// <summary>
        /// This constructor allows the order that axes are compared in
        /// </summary>
        /// <param name="axisCompareOrder"></param>
        public AxisComparer(int[] axisCompareOrder) : this(axisCompareOrder, [.. axisCompareOrder.Select(a => true)])
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
            if (A.Coords.LongLength != B.Coords.LongLength)
                throw new ArgumentException("Dimensions of compared points must match."); //But do they really?  Or should we just compare what we can...

            if (AxisCompareOrder != null && AxisCompareOrder.LongLength != A.Coords.LongLength)
            {
                throw new ArgumentException("Custom axis compare order must match dimensionality of passed points"); //But do they really?  Or should we just compare what we can...
            }
#endif  
            //This comparison is a bit contorted, but we need to use the same equality test as our epsilon value to be consistent with the rest of the code

            double[] diff = new double[A.Coords.LongLength];
            for (long i = 0; i < A.Coords.LongLength; i++)
            {
                long iAxis = AxisCompareOrder != null ? AxisCompareOrder[i] : i;
                diff[i] = Ascending[iAxis] ? A.Coords[iAxis] - B.Coords[iAxis] : B.Coords[iAxis] - A.Coords[iAxis];

                if (diff[i] == 0)
                    continue;

                return diff[i] > 0 ? 1 : -1;
            }

            return 0;
        }
    }

    [Serializable]
    public readonly struct VectorN : ICloneable, IComparable, IPointN,
                                IComparable<VectorN>, IComparer<VectorN>, IEquatable<VectorN>,
                                IComparable<IPointN>, IComparer<IPointN>, IEquatable<IPointN>, IEquatable<IShape2D>
    {
        readonly double[] _coords;

        public int DimensionCount => _coords.Length;

        public double[] Coords => [.. _coords];

        public VectorN(double[] input)
        {
            //Make sure we copy so we don't take a reference on the array
            _coords = new double[input.Length];
            input.CopyTo(_coords, 0);
        }

        public VectorN(IEnumerable<double> input)
        {
            //Make sure we copy so we don't take a reference on the array
            _coords = [.. input];
        }

        private static void ThrowOnDimensionMismatch(IPointN A, IPointN B)
        {
            if (A.Coords.Length != B.Coords.Length)
            {
                throw new ArgumentException(string.Format("Both points must have the same dimensions. {0} vs {1}", A, B));
            }
        }

        public bool Equals(VectorN B) => (DistanceSquared(this, B) <= Tolerance.EpsilonSquared);

        public bool Equals(IPointN B)
        {
            if (B is null)
                return false;

            return (DistanceSquared(this, B) <= Tolerance.EpsilonSquared);
        }

        public override bool Equals(object obj)
        {
            if (obj is VectorN otherGVN)
                return Equals(otherGVN);
            if (obj is IPointN otherIPN)
                return Equals(otherIPN);

            return false;
        }

        public override int GetHashCode() => GeometryHashCode.PointN(Coords);

        /// <summary>
        /// The block distance along each axis between the two points
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns>Array of distances along each axis</returns>
        public static double[] Diff(IPointN A, IPointN B)
        {
            ThrowOnDimensionMismatch(A, B);

            double[] diff = new double[A.Coords.Length];

            for (int iAxis = 0; iAxis < A.Coords.Length; iAxis++)
            {
                diff[iAxis] = A.Coords[iAxis] - B.Coords[iAxis];
            }

            return diff;
        }

        public static double DistanceSquared(IPointN A, IPointN B)
        {
            ThrowOnDimensionMismatch(A, B);

            return Diff(A, B).Select(dist => dist * dist).Sum();
        }

        public static double Magnitude(IPointN A) => Math.Sqrt(A.Coords.Select(val => val * val).Sum());

        public VectorN Normalize() => Normalize(this);

        public static VectorN Normalize(IPointN A)
        {
            double mag = Magnitude(A);

            double[] normalized = [.. A.Coords.Select(val => val / mag)];
            return new VectorN(normalized);
        }

        public object Clone()
        {
            double[] cpy = new double[this.DimensionCount];
            Coords.CopyTo(cpy, 0);
            return new VectorN(cpy);
        }

        public int CompareTo(VectorN other) => Compare((IPointN)this, (IPointN)other);

        public int Compare(VectorN A, VectorN B) => Compare((IPointN)A, (IPointN)B);

        public int CompareTo(IPointN other) => Compare((IPointN)this, (IPointN)other);

        public int Compare(IPointN A, IPointN B)
        {
            ThrowOnDimensionMismatch(A, B);

            for (int iAxis = 0; iAxis < _coords.Length; iAxis++)
            {
                double diff = A.Coords[iAxis] - B.Coords[iAxis];
                if (diff == 0)
                    continue;

                return diff > 0 ? 1 : -1;
            }

            return 0;
        }

        public bool Equals(IShape2D other)
        {
            if (other is null)
                return false;

            IPointN p = other as IPointN;
            return ((IEquatable<IPointN>)this).Equals(p);

        }

        public int CompareTo(object obj)
        {
            IPointN B = (IPointN)obj;

            return Compare(this, B);
        }

        public static VectorN operator -(VectorN A) => new VectorN([.. A._coords.Select(val => -val)]);

        public static VectorN operator -(VectorN A, VectorN B) => new VectorN([.. A._coords.Select((val, i) => val - B._coords[i])]);

        public static VectorN operator +(VectorN A, VectorN B) => new VectorN([.. A._coords.Select((val, i) => val + B._coords[i])]);

        public static VectorN operator *(VectorN A, double scalar) => new VectorN([.. A._coords.Select((val, i) => val * scalar)]);

        public static VectorN operator *(VectorN A, VectorN B) => new VectorN([.. A._coords.Select((a, i) => a * B._coords[i])]);

        public static VectorN operator /(VectorN A, double scalar) => new VectorN([.. A._coords.Select((val, i) => val / scalar)]);

        public static VectorN operator /(VectorN A, VectorN B) => new VectorN([.. A._coords.Select((a, i) => a / B._coords[i])]);

        /*
        #region IPointN operators

        static public VectorN operator -(IPointN A)
        {
            return new VectorN(A.coords.Select(val => -val).ToArray());
        }

        static public VectorN operator -(IPointN A, IPointN B)
        {
            return new VectorN(A.coords.Select((val, i) => val - B.coords[i]).ToArray());
        }

        static public VectorN operator +(IPointN A, IPointN B)
        {
            return new VectorN(A.coords.Select((val, i) => val + B.coords[i]).ToArray());
        }

        static public VectorN operator *(IPointN A, double scalar)
        {
            return new VectorN(A.coords.Select((val, i) => val * scalar).ToArray());
        }

        static public VectorN operator *(IPointN A, IPointN B)
        {
            return new VectorN(A.coords.Select((a, i) => a * B.coords[i]).ToArray());
        }

        static public VectorN operator /(IPointN A, double scalar)
        {
            return new VectorN(A.coords.Select((val, i) => val / scalar).ToArray());
        }

        static public VectorN operator /(IPointN A, IPointN B)
        {
            return new VectorN(A.coords.Select((a, i) => a / B.coords[i]).ToArray());
        }
        
        #endregion
        */

        public static bool operator ==(VectorN A, VectorN B) => A.Equals(B);

        public static bool operator !=(VectorN A, VectorN B) => !A.Equals(B);

        public static bool operator ==(VectorN A, IPointN B) => A.Equals(B);

        public static bool operator !=(VectorN A, IPointN B) => !A.Equals(B);

        public double this[Axis axis] => _coords[(int)axis];
    }
}
