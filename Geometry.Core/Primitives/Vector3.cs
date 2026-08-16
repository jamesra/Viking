using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Geometry
{
    public class Vector3Comparer(bool xyOrder = true) : IComparer<Vector3>, IComparer<IPoint3D>
    {
        public readonly bool XYOrder = xyOrder;

        public int Compare(IPoint3D A, IPoint3D B) => XYOrder ? Vector3ComparerXYZ.CompareXYZ(in A, in B) : Vector3ComparerZYX.CompareZYX(in A, in B);

        public int Compare(Vector3 A, Vector3 B) => XYOrder ? Vector3ComparerXYZ.CompareXYZ(A, B) : Vector3ComparerZYX.CompareZYX(A, B);
    }

    /// <summary>
    /// Exact Z-then-Y-then-X ordering. Does not use <see cref="Tolerance.Epsilon"/>; Delaunay splits require exact compares.
    /// </summary>
    public class Vector3ComparerZYX : IComparer<Vector3>, IComparer<IPoint3D>
    {
        public static int CompareZYX(in IPoint3D A, in IPoint3D B)
        {
            double diffZ = A.Z - B.Z;
            if (diffZ == 0)
            {
                double diffY = A.Y - B.Y;
                if (diffY == 0)
                {
                    double diffX = A.X - B.X;
                    if (diffX == 0)
                        return 0;

                    return diffX > 0 ? 1 : -1;
                }

                return diffY > 0 ? 1 : -1;
            }

            return diffZ > 0 ? 1 : -1;
        }

        public int Compare(IPoint3D A, IPoint3D B) => Vector3ComparerZYX.CompareZYX(in A, in B);

        public int Compare(Vector3 x, Vector3 y) => Vector3ComparerZYX.CompareZYX(x, y);
    }

    /// <summary>
    /// Exact X-then-Y-then-Z ordering. Does not use <see cref="Tolerance.Epsilon"/>; Delaunay splits require exact compares.
    /// </summary>
    public class Vector3ComparerXYZ : IComparer<Vector3>, IComparer<IPoint3D>
    {
        public static int CompareXYZ(in IPoint3D A, in IPoint3D B)
        {
            double diffX = A.X - B.X;
            if (diffX == 0)
            {
                double diffY = A.Y - B.Y;
                if (diffY == 0)
                {
                    double diffZ = A.Z - B.Z;
                    if (diffZ == 0)
                        return 0;

                    return diffZ > 0 ? 1 : -1;
                }

                return diffY > 0 ? 1 : -1;
            }

            return diffX > 0 ? 1 : -1;
        }

        public int Compare(IPoint3D A, IPoint3D B) => Vector3ComparerXYZ.CompareXYZ(in A, in B);

        public int Compare(Vector3 a, Vector3 b) => Vector3ComparerXYZ.CompareXYZ(a, b);
    }

    [Serializable]
    public readonly struct Vector3 : IPoint3D, ICloneable, IComparable, IComparable<Vector3>, IEquatable<Vector3>
    {
        public static readonly Vector3 UnitX = new(1, 0, 0);
        public static readonly Vector3 UnitY = new(0, 1, 0);
        public static readonly Vector3 UnitZ = new(0, 0, 1);
        public static readonly Vector3 Zero = new(0, 0, 0);
        public static readonly Vector3 NaN = new(double.NaN, double.NaN, double.NaN);

        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public readonly double[] Coords => [X, Y, Z];

        public Vector3(double[] input)
        {
            if (input is null)
                throw new ArgumentNullException(nameof(input));
            if (input.Length != 3)
                throw new ArgumentException($"Passing an array of length {input.Length} to Vector3 constructor, expected 3 elements", nameof(input));

            X = input[0];
            Y = input[1];
            Z = input[2];
        }

        public Vector3(IEnumerable<double> input)
        {
            if (input is null)
                throw new ArgumentNullException(nameof(input));
            double[] values = [.. input];
            if (values.Length != 3)
                throw new ArgumentException($"Passing an IEnumerable<double> of count {values.Length} to Vector3 constructor, expected 3 elements", nameof(input));

            X = values[0];
            Y = values[1];
            Z = values[2];
        }

        public Vector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public readonly void Deconstruct(out double x, out double y, out double z)
        {
            x = X;
            y = Y;
            z = Z;
        }

        readonly int IComparable.CompareTo(object Obj)
        {
            Vector3 B = (Vector3)Obj;
            return CompareTo(B);
        }

        public readonly int CompareTo(Vector3 B) => Vector3ComparerXYZ.CompareXYZ(this, B);

        readonly object ICloneable.Clone() => new Vector3(X, Y, Z);

        public override readonly int GetHashCode() => GeometryHashCode.Point3D(X, Y, Z);

        public readonly bool Equals(IPoint3D other)
        {
            if (other is null)
                return false;

            return Distance(this, other) <= Tolerance.EpsilonSquared;
        }

        public override readonly bool Equals(object obj)
        {
            if (obj is Vector3 other)
                return Equals(other);

            if (obj is IPoint3D iOther)
                return Equals(iOther);

            return false;
        }

        public readonly bool Equals(Vector3 B) => DistanceSquared(this, B) <= Tolerance.EpsilonSquared;

        public override readonly string ToString() => $"X: {X:F2} Y: {Y:F2} Z: {Z:F2}";

        public static string ToMatlab(Vector3[] array)
        {
            StringBuilder sb = new();
            sb.Append('[');
            for (int i = 0; i < array.Length; i++)
            {
                sb.Append(array[i].X);
                sb.Append(' ');
                sb.Append(array[i].Y);
                sb.Append(' ');
                sb.Append(array[i].Z);
                sb.AppendLine(";");
            }
            sb.Append(']');

            return sb.ToString();
        }

        public static double Magnitude(Vector3 A) => Math.Sqrt((A.X * A.X) + (A.Y * A.Y) + (A.Z * A.Z));

        public Vector3 Normalize() => Normalize(this);

        public static Vector3 Normalize(Vector3 A)
        {
            double mag = Magnitude(A);
            if (mag == 0)
                return A;

            return new Vector3(A.X / mag, A.Y / mag, A.Z / mag);
        }

        public static double Distance(Vector3 A, Vector3 B) => Math.Sqrt(DistanceSquared(A, B));

        public static double DistanceSquared(Vector3 A, Vector3 B)
        {
            double dX = A.X - B.X;
            double dY = A.Y - B.Y;
            double dZ = A.Z - B.Z;
            return (dX * dX) + (dY * dY) + (dZ * dZ);
        }

        public static double Distance(IPoint3D A, IPoint3D B)
        {
            if (A is null)
                throw new ArgumentNullException(nameof(A));
            if (B is null)
                throw new ArgumentNullException(nameof(B));

            double dX = A.X - B.X;
            double dY = A.Y - B.Y;
            double dZ = A.Z - B.Z;
            return Math.Sqrt((dX * dX) + (dY * dY) + (dZ * dZ));
        }

        public static double Dot(Vector3 A, Vector3 B) => (A.X * B.X) + (A.Y * B.Y) + (A.Z * B.Z);

        /// <summary>
        /// Cross product of (B - A) × (C - A).
        /// </summary>
        public static Vector3 Cross(Vector3 A, Vector3 B, Vector3 C) => Cross(B - A, C - A);

        public static Vector3 Cross(Vector3 AB, Vector3 AC)
        {
            double x = (AB.Y * AC.Z) - (AC.Y * AB.Z);
            double y = (AB.Z * AC.X) - (AC.Z * AB.X);
            double z = (AB.X * AC.Y) - (AC.X * AB.Y);

            return new Vector3(x, y, z);
        }

        public static double Angle(Vector3 VectorA, Vector3 VectorB)
        {
            double mag = Magnitude(VectorA) * Magnitude(VectorB);
            if (mag == 0)
                return 0;

            double cos = Dot(VectorA, VectorB) / mag;
            if (cos > 1)
                cos = 1;
            else if (cos < -1)
                cos = -1;

            return Math.Acos(cos);
        }

        public static double ArcAngle(Vector3 Origin, Vector3 A, Vector3 B)
        {
            A -= Origin;
            B -= Origin;
            return Angle(A, B);
        }

        public static Vector3 operator -(Vector3 A) => new(-A.X, -A.Y, -A.Z);

        public static Vector3 operator -(Vector3 A, Vector3 B) => new(A.X - B.X, A.Y - B.Y, A.Z - B.Z);

        public static Vector3 operator +(Vector3 A, Vector3 B) => new(A.X + B.X, A.Y + B.Y, A.Z + B.Z);

        public static Vector3 operator *(Vector3 A, double scalar) => new(A.X * scalar, A.Y * scalar, A.Z * scalar);

        public static Vector3 operator *(Vector3 A, Vector3 B) => new(A.X * B.X, A.Y * B.Y, A.Z * B.Z);

        public static Vector3 operator /(Vector3 A, double scalar) => new(A.X / scalar, A.Y / scalar, A.Z / scalar);

        public static Vector3 operator /(Vector3 A, Vector3 B) => new(A.X / B.X, A.Y / B.Y, A.Z / B.Z);

        public static bool operator ==(Vector3 A, Vector3 B) => A.Equals(B);

        public static bool operator !=(Vector3 A, Vector3 B) => !A.Equals(B);

        public static bool operator <(Vector3 left, Vector3 right) => left.CompareTo(right) < 0;

        public static bool operator <=(Vector3 left, Vector3 right) => left.CompareTo(right) <= 0;

        public static bool operator >(Vector3 left, Vector3 right) => left.CompareTo(right) > 0;

        public static bool operator >=(Vector3 left, Vector3 right) => left.CompareTo(right) >= 0;

        public double this[Axis axis] => axis switch
        {
            Axis.X => X,
            Axis.Y => Y,
            Axis.Z => Z,
            _ => throw new IndexOutOfRangeException($"Axis not supported for {nameof(Vector3)}"),
        };

        public static Vector3 FromBarycentric(Vector3 v1, Vector3 v2, Vector3 v3, double u, double v)
        {
            double w = 1 - u - v;
            return new Vector3(
                (v1.X * w) + (v2.X * u) + (v3.X * v),
                (v1.Y * w) + (v2.Y * u) + (v3.Y * v),
                (v1.Z * w) + (v2.Z * u) + (v3.Z * v));
        }

        public static Vector3 Scale(Vector3 A, double scalar) => A * scalar;

        public readonly Vector3 Scale(double scalar) => this * scalar;

        double IPoint3D.X => X;
        double IPoint3D.Y => Y;
        double IPoint3D.Z => Z;
    }
}
