using System;
using System.Diagnostics;
using Viking.AnnotationServiceTypes.Interfaces;
namespace Viking.AnnotationServiceTypes
{
    public readonly struct LocationLinkKey : IComparable<LocationLinkKey>, IEquatable<LocationLinkKey>, IEquatable<ILocationLink>, ILocationLinkKey,  IEquatable<ILocationLinkKey>, IComparable<ILocationLinkKey>
    {
        public readonly long A;
        public readonly long B;

        ulong ILocationLinkKey.A => (ulong)A;

        ulong ILocationLinkKey.B => (ulong)B;

        public LocationLinkKey(long a, long b)
        {
            Debug.Assert(a != b);
            A = a < b ? a : b;
            B = b < a ? a : b;
        }

        public LocationLinkKey(ILocationLinkKey obj)
        {
            this.A = (long)obj.A;
            this.B = (long)obj.B;
        }

        public ulong OtherKey(ulong key)
        {
            return (ulong)this.OtherKey((long)key);
        }

        /// <summary>
        /// Returns the side of the link that doesn't match the passed key.
        /// Throws an exception if the passed key does not match either A or B
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public long OtherKey(long key)
        {
            if (A == key)
                return B;
            if (B == key)
                return A;

            throw new ArgumentException($"{key} is not part of location link {A}-{B}");
        }

        public override bool Equals(object obj)
        { 
            if (obj is null)
                return false;

            if(obj is LocationLinkKey other)
                return (A == other.A) && (B == other.B);

            return false;
        }

        public override string ToString()
        {
            return $"{A} - {B}";
        }

        public override int GetHashCode()
        {
            return (int)(A % int.MaxValue);
        }

        public static bool operator ==(LocationLinkKey A, LocationLinkKey B)
        { 
            return A.Equals(B);
        }

        public static bool operator !=(LocationLinkKey A, LocationLinkKey B)
        {  
            return !A.Equals(B);
        }

        public int CompareTo(LocationLinkKey other)
        {
            if (A != other.A)
                return (int)(other.A - A);
            else
                return (int)(other.B - B);
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

        bool IEquatable<ILocationLinkKey>.Equals(ILocationLinkKey other)
        {
            if (other is null)
                return false;

            return ((ulong)this.A == other.A && (ulong)this.B == other.B) || ((ulong)this.B == other.A && (ulong)this.A == other.B);
        }

        public int CompareTo(ILocationLinkKey other)
        {
            if (A != (long)other.A)
                return (int)((long)other.A - A);
            else
                return (int)((long)other.B - B);
        }
    }
}
