using System;
using System.Diagnostics;
using Viking.AnnotationServiceTypes.Interfaces;

namespace Viking.AnnotationServiceTypes
{
    public readonly struct LocationLinkKey : IComparable<LocationLinkKey>, IEquatable<LocationLinkKey>, IEquatable<ILocationLink>, ILocationLinkReadOnly,  IEquatable<ILocationLinkReadOnly>, IComparable<ILocationLinkReadOnly>, ILocationLink
    {
        public readonly long A;
        public readonly long B;

        ulong ILocationLinkReadOnly.A => (ulong)A;

        ulong ILocationLinkReadOnly.B => (ulong)B;

        ulong ILocationLink.A => (ulong)A;

        ulong ILocationLink.B => (ulong)B;

        public LocationLinkKey(long a, long b)
        {
            Debug.Assert(a != b);
            A = a < b ? a : b;
            B = b < a ? a : b;
        }

        public LocationLinkKey(ILocationLinkReadOnly obj)
        {
            this.A = (long)obj.A;
            this.B = (long)obj.B;
        }

        public LocationLinkKey(ILocationLink obj)
        {
            this.A = (long)obj.A;
            this.B = (long)obj.B;
        }

        public int CompareTo(LocationLinkKey other)
        {
            if (A != other.A)
                return (int)(other.A - A);
            else
                return (int)(other.B - B);
        }

        public override string ToString()
        {
            return A.ToString() + " - " + B.ToString();
        }

        public override int GetHashCode()
        {
            return (int)(A % int.MaxValue);
        }

        public static bool operator ==(LocationLinkKey A, LocationLinkKey B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return true;
            }

            if (A is object)
                return A.Equals(B);

            return false;
        }

        public static bool operator !=(LocationLinkKey A, LocationLinkKey B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return false;
            }

            if (A is object)
                return !A.Equals(B);

            return true;
        }
         
        public override bool Equals(object obj)
        {
            if (System.Object.ReferenceEquals(this, obj))
                return true;
            if (obj is null)
                return false;
            if (obj is LocationLinkKey otherKey)
                return Equals(otherKey);
            if (obj is ILocationLink otherLocLinkInterface)
                return Equals(otherLocLinkInterface);
            if (obj is ILocationLinkReadOnly otherReadonlyLocLinkInterface)
                return Equals(otherReadonlyLocLinkInterface);

            return false;
        }

        public bool Equals(LocationLinkKey other)
        {
            return (this.A == other.A && this.B == other.B);
        }

        bool IEquatable<ILocationLink>.Equals(ILocationLink other)
        {
            if (other is null)
                return false;

            return ((ulong)this.A == other.A && (ulong)this.B == other.B) || ((ulong)this.B == other.A && (ulong)this.A == other.B);
        }

        bool IEquatable<ILocationLinkReadOnly>.Equals(ILocationLinkReadOnly other)
        {
            if (other is null)
                return false;

            return ((ulong)this.A == other.A && (ulong)this.B == other.B) || ((ulong)this.B == other.A && (ulong)this.A == other.B);
        }

        public int CompareTo(ILocationLinkReadOnly other)
        {
            if (A != (long)other.A)
                return (int)((long)other.A - A);
            else
                return (int)((long)other.B - B);
        }
    }
}
