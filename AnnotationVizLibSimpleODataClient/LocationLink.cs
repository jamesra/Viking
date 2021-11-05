using Viking.AnnotationServiceTypes.Interfaces;
using System;
using System.Collections.Generic;

namespace AnnotationVizLib.SimpleOData
{
    public class LocationLink : ILocationLinkKey, IEquatable<LocationLink>
    {
        public static LocationLink FromDictionary(IDictionary<string, object> dict)
        {
            LocationLink ll = new LocationLink { A = System.Convert.ToUInt64(dict["A"]), B = System.Convert.ToUInt64(dict["B"]) };
            return ll;
        }

        public ulong A { get; private set; }
        public ulong B { get; private set; }

        public bool Equals(ILocationLinkKey other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (other.A == this.A && other.B == this.B)
                return true;

            if (other.A == this.B && other.B == this.A)
                return true;

            return false;
        }

        public bool Equals(LocationLink other)
        {
            return this.Equals((ILocationLinkKey)other);
        }

        int IComparable<ILocationLinkKey>.CompareTo(ILocationLinkKey other)
        {
            int aa = A.CompareTo(other.A);
            if (aa != 0)
                return aa;

            int bb = B.CompareTo(other.B);
            return bb;
        }

        public ulong OtherKey(ulong key)
        {
            if (key == A) return B;
            if (key == B) return A;

            throw new ArgumentException($"{key} is not found in location link {this}");
        }
    }
}
